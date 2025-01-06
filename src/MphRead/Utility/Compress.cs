using System;
using System.IO;

namespace MphRead
{
    public static class LZUtil
    {
        public static unsafe int GetOccurrenceLength(byte* newPtr, int newLength, byte* oldPtr, int oldLength, out int disp, int minDisp = 1)
        {
            disp = 0;
            if (newLength == 0)
            {
                return 0;
            }

            int maxLength = 0;
            // try every possible 'disp' value (disp = oldLength - i)
            for (int i = 0; i < oldLength - minDisp; i++)
            {
                // work from the start of the old data to the end, to mimic the original implementation's behaviour
                // (and going from start to end or from end to start does not influence the compression ratio anyway)
                byte* currentOldStart = oldPtr + i;
                int currentLength = 0;
                // determine the length we can copy if we go back (oldLength - i) bytes
                // always check the next 'newLength' bytes, and not just the available 'old' bytes,
                // as the copied data can also originate from what we're currently trying to compress.
                for (int j = 0; j < newLength; j++)
                {
                    // stop when the bytes are no longer the same
                    if (*(currentOldStart + j) != *(newPtr + j))
                    {
                        break;
                    }

                    currentLength++;
                }

                // update the optimal value
                if (currentLength > maxLength)
                {
                    maxLength = currentLength;
                    disp = oldLength - i;

                    // if we cannot do better anyway, stop trying.
                    if (maxLength == newLength)
                    {
                        break;
                    }
                }
            }
            return maxLength;
        }
    }

    public static class LZ10
    {
        public static byte MagicByte { get; } = 0x10;

        public static long Decompress(string input, string output)
        {
            using FileStream inStream = File.Open(input, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream outStream = File.Open(output, FileMode.Create);
            return Decompress(inStream, inStream.Length, outStream);
        }

        public static long Decompress(Stream instream, long inLength, Stream outstream)
        {
            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            if (type != MagicByte)
            {
                throw new InvalidDataException("The provided stream is not a valid LZ-0x10 "
                    + "compressed stream (invalid type 0x" + type.ToString("X") + ")");
            }

            byte[] sizeBytes = new byte[3];
            instream.ReadExactly(sizeBytes, 0, 3);
            int decompressedSize = ToNDSu24(sizeBytes, 0);
            readBytes += 4;
            if (decompressedSize == 0)
            {
                sizeBytes = new byte[4];
                instream.ReadExactly(sizeBytes, 0, 4);
                decompressedSize = ToNDSs32(sizeBytes, 0);
                readBytes += 4;
            }

            // the maximum 'DISP-1' is 0xFFF.
            int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;


            int currentOutSize = 0;
            int flags = 0, mask = 1;
            while (currentOutSize < decompressedSize)
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (mask == 1)
                {
                    if (readBytes >= inLength)
                    {
                        throw new ProgramException($"Not enough data {currentOutSize}, {decompressedSize}");
                    }

                    flags = instream.ReadByte();
                    readBytes++;
                    if (flags < 0)
                    {
                        throw new ProgramException("Stream too short");
                    }

                    mask = 0x80;
                }
                else
                {
                    mask >>= 1;
                }
                #endregion

                // bit = 1 <=> compressed.
                if ((flags & mask) > 0)
                {
                    // (throws when < 2 bytes are available)
                    #region Get length and displacement('disp') values from next 2 bytes
                    // there are < 2 bytes available when the end is at most 1 byte away
                    if (readBytes + 1 >= inLength)
                    {
                        // make sure the stream is at the end
                        if (readBytes < inLength)
                        {
                            instream.ReadByte();
                            readBytes++;
                        }
                        throw new ProgramException($"Not enough data {currentOutSize}, {decompressedSize}");
                    }
                    int byte1 = instream.ReadByte();
                    readBytes++;
                    int byte2 = instream.ReadByte();
                    readBytes++;
                    if (byte2 < 0)
                    {
                        throw new ProgramException("Stream too short");
                    }

                    // the number of bytes to copy
                    int length = byte1 >> 4;
                    length += 3;

                    // from where the bytes should be copied (relatively)
                    int disp = ((byte1 & 0x0F) << 8) | byte2;
                    disp += 1;

                    if (disp > currentOutSize)
                    {
                        throw new InvalidDataException("Cannot go back more than already written. "
                            + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                            + " at 0x" + (instream.Position - 2).ToString("X"));
                    }
                    #endregion

                    int bufIdx = bufferOffset + bufferLength - disp;
                    for (int i = 0; i < length; i++)
                    {
                        byte next = buffer[bufIdx % bufferLength];
                        bufIdx++;
                        outstream.WriteByte(next);
                        buffer[bufferOffset] = next;
                        bufferOffset = (bufferOffset + 1) % bufferLength;
                    }
                    currentOutSize += length;
                }
                else
                {
                    if (readBytes >= inLength)
                    {
                        throw new ProgramException($"Not enough data {currentOutSize}, {decompressedSize}");
                    }

                    int next = instream.ReadByte();
                    readBytes++;
                    if (next < 0)
                    {
                        throw new ProgramException("Stream too short");
                    }

                    currentOutSize++;
                    outstream.WriteByte((byte)next);
                    buffer[bufferOffset] = (byte)next;
                    bufferOffset = (bufferOffset + 1) % bufferLength;
                }
                outstream.Flush();
            }

            if (readBytes < inLength)
            {
                // the input may be 4-byte aligned.
                if ((readBytes ^ (readBytes & 3)) + 4 < inLength)
                {
                    throw new ProgramException($"Too much input {readBytes}, {inLength}");
                }
            }

            return decompressedSize;
        }

        public static int Compress(string input, string output)
        {
            using FileStream inStream = File.Open(input, FileMode.Open);
            using FileStream outStream = File.Open(output, FileMode.Create);
            return Compress(inStream, inStream.Length, outStream);
        }

        public static unsafe int Compress(Stream instream, long inLength, Stream outstream)
        {
            // make sure the decompressed size fits in 3 bytes.
            // There should be room for four bytes, however I'm not 100% sure if that can be used
            // in every game, as it may not be a built-in function.
            if (inLength > 0xFFFFFF)
            {
                throw new ProgramException("Input too large");
            }

            // save the input data in an array to prevent having to go back and forth in a file
            byte[] indata = new byte[inLength];
            int numReadBytes = instream.Read(indata, 0, (int)inLength);
            if (numReadBytes != inLength)
            {
                throw new ProgramException("Stream too short");
            }

            // write the compression header first
            outstream.WriteByte(MagicByte);
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));

            int compressedLength = 4;

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                byte[] outbuffer = new byte[8 * 2 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;
                while (readBytes < inLength)
                {
                    #region If 8 blocks are bufferd, write them and reset the buffer
                    // we can only buffer 8 blocks at a time.
                    if (bufferedBlocks == 8)
                    {
                        outstream.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }
                    #endregion

                    // determine if we're dealing with a compressed or raw block.
                    // it is a compressed block when the next 3 or more bytes can be copied from
                    // somewhere in the set of already compressed bytes.
                    int oldLength = Math.Min(readBytes, 0x1000);
                    int length = LZUtil.GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 0x12),
                        instart + readBytes - oldLength, oldLength, out int disp);

                    // length not 3 or more? next byte is raw data
                    if (length < 3)
                    {
                        outbuffer[bufferlength++] = *(instart + readBytes++);
                    }
                    else
                    {
                        // 3 or more bytes can be copied? next (length) bytes will be compressed into 2 bytes
                        readBytes += length;

                        // mark the next block as compressed
                        outbuffer[0] |= (byte)(1 << (7 - bufferedBlocks));

                        outbuffer[bufferlength] = (byte)(((length - 3) << 4) & 0xF0);
                        outbuffer[bufferlength] |= (byte)(((disp - 1) >> 8) & 0x0F);
                        bufferlength++;
                        outbuffer[bufferlength] = (byte)((disp - 1) & 0xFF);
                        bufferlength++;
                    }
                    bufferedBlocks++;
                }

                // copy the remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    outstream.Write(outbuffer, 0, bufferlength);
                    compressedLength += bufferlength;
                    /* make the compressed file 4-byte aligned.
                    while ((compressedLength % 4) != 0)
                    {
                        outstream.WriteByte(0);
                        compressedLength++;
                    }*/
                }
            }

            return compressedLength;
        }

        private static int ToNDSu24(byte[] buffer, int offset)
        {
            return buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16);
        }

        private static int ToNDSs32(byte[] buffer, int offset)
        {
            return buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24);
        }
    }

    public static class LZBackward
    {
        public static long Decompress(string input, string output)
        {
            using FileStream inStream = File.Open(input, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream outStream = File.Open(output, FileMode.Create);
            return Decompress(inStream, inStream.Length, outStream);
        }

        public static long Decompress(Stream instream, long inLength, Stream outstream)
        {
            #region Format description
            // Overlay LZ compression is basically just LZ-0x10 compression.
            // however the order if reading is reversed: the compression starts at the end of the file.
            // Assuming we start reading at the end towards the beginning, the format is:
            /*
             * u32 extraSize; // decompressed data size = file length (including header) + this value
             * u8 headerSize;
             * u24 compressedLength; // can be less than file size (w/o header). If so, the rest of the file is uncompressed.
             *                       // may also be the file size
             * u8[headerSize-8] padding; // 0xFF-s
             * 
             * 0x10-like-compressed data follows (without the usual 4-byte header).
             * The only difference is that 2 should be added to the DISP value in compressed blocks
             * to get the proper value.
             * the u32 and u24 are read most significant byte first.
             * if extraSize is 0, there is no headerSize, decompressedLength or padding.
             * the data starts immediately, and is uncompressed.
             * 
             * arm9.bin has 3 extra u32 values at the 'start' (ie: end of the file),
             * which may be ignored. (and are ignored here) These 12 bytes also should not
             * be included in the computation of the output size.
             */
            #endregion

            #region First read the last 4 bytes of the stream (the 'extraSize')

            // first go to the end of the stream, since we're reading from back to front
            // read the last 4 bytes, the 'extraSize'
            instream.Position += inLength - 4;

            byte[] buffer = new byte[4];
            try
            {
                instream.ReadExactly(buffer, 0, 4);
            }
            catch (EndOfStreamException)
            {
                // since we're immediately checking the end of the stream, 
                // this is the only location where we have to check for an EOS to occur.
                throw new ProgramException("Stream too short");
            }
            uint extraSize = ToNDSu32(buffer, 0);

            #endregion

            // if the extra size is 0, there is no compressed part, and the header ends there.
            if (extraSize == 0)
            {
                #region just copy the input to the output

                // first go back to the start of the file. the current location is after the 'extraSize',
                // and thus at the end of the file.
                instream.Position -= inLength;
                // no buffering -> slow
                buffer = new byte[inLength - 4];
                instream.ReadExactly(buffer, 0, (int)(inLength - 4));
                outstream.Write(buffer, 0, (int)(inLength - 4));

                // make sure the input is positioned at the end of the file
                instream.Position += 4;

                return inLength - 4;

                #endregion
            }
            else
            {
                // get the size of the compression header first.
                instream.Position -= 5;
                int headerSize = instream.ReadByte();

                // then the compressed data size.
                instream.Position -= 4;
                instream.ReadExactly(buffer, 0, 3);
                int compressedSize = buffer[0] | (buffer[1] << 8) | (buffer[2] << 16);

                // the compressed size sometimes is the file size.
                if (compressedSize + headerSize >= inLength)
                {
                    compressedSize = (int)(inLength - headerSize);
                }

                #region copy the non-compressed data

                // copy the non-compressed data first.
                buffer = new byte[inLength - headerSize - compressedSize];
                instream.Position -= (inLength - 5);
                instream.ReadExactly(buffer, 0, buffer.Length);
                outstream.Write(buffer, 0, buffer.Length);

                #endregion

                // buffer the compressed data, such that we don't need to keep
                // moving the input stream position back and forth
                buffer = new byte[compressedSize];
                instream.ReadExactly(buffer, 0, compressedSize);

                // we're filling the output from end to start, so we can't directly write the data.
                // buffer it instead (also use this data as buffer instead of a ring-buffer for
                // decompression)
                byte[] outbuffer = new byte[compressedSize + headerSize + extraSize];

                int currentOutSize = 0;
                int decompressedLength = outbuffer.Length;
                int readBytes = 0;
                byte flags = 0, mask = 1;
                while (currentOutSize < decompressedLength)
                {
                    // (throws when requested new flags byte is not available)
                    #region Update the mask. If all flag bits have been read, get a new set.
                    // the current mask is the mask used in the previous run. So if it masks the
                    // last flag bit, get a new flags byte.
                    if (mask == 1)
                    {
                        if (readBytes >= compressedSize)
                        {
                            break;
                            //throw new ProgramException($"Not enough data {currentOutSize}, {decompressedLength}");
                        }
                        flags = buffer[buffer.Length - 1 - readBytes];
                        readBytes++;
                        mask = 0x80;
                    }
                    else
                    {
                        mask >>= 1;
                    }
                    #endregion

                    // bit = 1 <=> compressed.
                    if ((flags & mask) > 0)
                    {
                        // (throws when < 2 bytes are available)
                        #region Get length and displacement('disp') values from next 2 bytes
                        // there are < 2 bytes available when the end is at most 1 byte away
                        if (readBytes + 1 >= inLength)
                        {
                            throw new ProgramException($"Not enough data {currentOutSize}, {decompressedLength}");
                        }
                        int bufIndex = compressedSize - 1 - readBytes;
                        if (bufIndex == -1)
                        {
                            break;
                        }
                        int byte1 = buffer[bufIndex];
                        readBytes++;
                        int index2 = compressedSize - 1 - readBytes;
                        if (index2 == -1)
                        {
                            break;
                        }
                        int byte2 = buffer[index2];
                        readBytes++;

                        // the number of bytes to copy
                        int length = byte1 >> 4;
                        length += 3;

                        // from where the bytes should be copied (relatively)
                        int disp = ((byte1 & 0x0F) << 8) | byte2;
                        disp += 3;

                        if (disp > currentOutSize)
                        {
                            if (currentOutSize < 2)
                            {
                                throw new InvalidDataException("Cannot go back more than already written; "
                                    + "attempt to go back 0x" + disp.ToString("X") + " when only 0x"
                                    + currentOutSize.ToString("X") + " bytes have been written.");
                            }
                            // HACK. this seems to produce valid files, but isn't the most elegant solution.
                            // although this _could_ be the actual way to use a disp of 2 in this format,
                            // as otherwise the minimum would be 3 (and 0 is undefined, and 1 is less useful).
                            disp = 2;
                        }
                        #endregion

                        int bufIdx = currentOutSize - disp;
                        for (int i = 0; i < length; i++)
                        {
                            byte next = outbuffer[outbuffer.Length - 1 - bufIdx];
                            bufIdx++;
                            int outIndex = outbuffer.Length - 1 - currentOutSize;
                            if (outIndex == -1)
                            {
                                break;
                            }
                            outbuffer[outIndex] = next;
                            currentOutSize++;
                        }
                    }
                    else
                    {
                        if (readBytes >= inLength)
                        {
                            throw new ProgramException($"Not enough data {currentOutSize}, {decompressedLength}");
                        }
                        int nextIndex = buffer.Length - 1 - readBytes;
                        if (nextIndex == -1)
                        {
                            break;
                        }
                        byte next = buffer[nextIndex];
                        readBytes++;

                        outbuffer[outbuffer.Length - 1 - currentOutSize] = next;
                        currentOutSize++;
                    }
                }

                // write the decompressed data
                outstream.Write(outbuffer, 0, outbuffer.Length);

                // make sure the input is positioned at the end of the file; the stream is currently
                // at the compression header.
                instream.Position += headerSize;

                return decompressedLength + (inLength - headerSize - compressedSize);
            }
        }

        #region Compression method; delegates to CompressNormal
        /// <summary>
        /// Compresses the input using the LZ-Overlay compression scheme.
        /// </summary>
        public static int Compress(Stream instream, long inLength, Stream outstream)
        {
            // don't bother trying to get the optimal not-compressed - compressed ratio for now.
            // Either compress fully or don't compress (as the format cannot handle decompressed
            // sizes that are smaller than the compressed file).

            if (inLength > 0xFFFFFF)
            {
                throw new ProgramException("Input too large");
            }

            // read the input and reverse it
            byte[] indata = new byte[inLength];
            instream.ReadExactly(indata, 0, (int)inLength);
            Array.Reverse(indata);

            var inMemStream = new MemoryStream(indata);
            var outMemStream = new MemoryStream();
            int compressedLength = CompressNormal(inMemStream, inLength, outMemStream);

            int totalCompFileLength = (int)outMemStream.Length + 8;
            // make the file 4-byte aligned with padding in the header
            if (totalCompFileLength % 4 != 0)
            {
                totalCompFileLength += 4 - totalCompFileLength % 4;
            }

            if (totalCompFileLength < inLength)
            {
                byte[] compData = outMemStream.ToArray();
                Array.Reverse(compData);
                outstream.Write(compData, 0, compData.Length);
                int writtenBytes = compData.Length;
                // there always seem to be some padding FFs. Let's pad to make the file 4-byte aligned
                while (writtenBytes % 4 != 0)
                {
                    outstream.WriteByte(0xFF);
                    writtenBytes++;
                }

                outstream.WriteByte((byte)((compressedLength) & 0xFF));
                outstream.WriteByte((byte)((compressedLength >> 8) & 0xFF));
                outstream.WriteByte((byte)((compressedLength >> 16) & 0xFF));

                int headerLength = totalCompFileLength - compData.Length;
                outstream.WriteByte((byte)headerLength);

                int extraSize = (int)inLength - totalCompFileLength;
                outstream.WriteByte((byte)((extraSize) & 0xFF));
                outstream.WriteByte((byte)((extraSize >> 8) & 0xFF));
                outstream.WriteByte((byte)((extraSize >> 16) & 0xFF));
                outstream.WriteByte((byte)((extraSize >> 24) & 0xFF));

                return totalCompFileLength;
            }
            else
            {
                Array.Reverse(indata);
                outstream.Write(indata, 0, (int)inLength);
                outstream.WriteByte(0);
                outstream.WriteByte(0);
                outstream.WriteByte(0);
                outstream.WriteByte(0);
                return (int)inLength + 4;
            }
        }
        #endregion

        #region 'Normal' compression method. Delegates to CompressWithLA when LookAhead is set
        /// <summary>
        /// Compresses the given input stream with the LZ-Ovl compression, but compresses _forward_
        /// instad of backwards.
        /// </summary>
        /// <param name="instream">The input stream to compress.</param>
        /// <param name="inLength">The length of the input stream.</param>
        /// <param name="outstream">The stream to write to.</param>
        private static unsafe int CompressNormal(Stream instream, long inLength, Stream outstream)
        {
            // make sure the decompressed size fits in 3 bytes.
            // There should be room for four bytes, however I'm not 100% sure if that can be used
            // in every game, as it may not be a built-in function.
            if (inLength > 0xFFFFFF)
            {
                throw new ProgramException("Input too large");
            }

            // save the input data in an array to prevent having to go back and forth in a file
            byte[] indata = new byte[inLength];
            int numReadBytes = instream.Read(indata, 0, (int)inLength);
            if (numReadBytes != inLength)
            {
                throw new ProgramException("Stream too short");
            }

            int compressedLength = 0;

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                byte[] outbuffer = new byte[8 * 2 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;
                while (readBytes < inLength)
                {
                    #region If 8 blocks are bufferd, write them and reset the buffer
                    // we can only buffer 8 blocks at a time.
                    if (bufferedBlocks == 8)
                    {
                        outstream.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }
                    #endregion

                    // determine if we're dealing with a compressed or raw block.
                    // it is a compressed block when the next 3 or more bytes can be copied from
                    // somewhere in the set of already compressed bytes.
                    int oldLength = Math.Min(readBytes, 0x1001);
                    int length = LZUtil.GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 0x12),
                                                          instart + readBytes - oldLength, oldLength, out int disp);

                    // disp = 1 cannot be stored.
                    if (disp == 1)
                    {
                        length = 1;
                    }
                    // disp = 2 cannot be saved properly. use a too large disp instead.
                    // however since I'm not sure if that's actually how that's handled, don't compress instead.
                    else if (disp == 2)
                    {
                        length = 1;
                        /*if (readBytes < 0x1001)
                            disp = readBytes + 1;
                        else
                            length = 1;/**/
                    }

                    // length not 3 or more? next byte is raw data
                    if (length < 3)
                    {
                        outbuffer[bufferlength++] = *(instart + (readBytes++));
                    }
                    else
                    {
                        // 3 or more bytes can be copied? next (length) bytes will be compressed into 2 bytes
                        readBytes += length;

                        // mark the next block as compressed
                        outbuffer[0] |= (byte)(1 << (7 - bufferedBlocks));

                        outbuffer[bufferlength] = (byte)(((length - 3) << 4) & 0xF0);
                        outbuffer[bufferlength] |= (byte)(((disp - 3) >> 8) & 0x0F);
                        bufferlength++;
                        outbuffer[bufferlength] = (byte)((disp - 3) & 0xFF);
                        bufferlength++;
                    }
                    bufferedBlocks++;
                }

                // copy the remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    outstream.Write(outbuffer, 0, bufferlength);
                    compressedLength += bufferlength;
                    /*/ make the compressed file 4-byte aligned.
                    while ((compressedLength % 4) != 0)
                    {
                        outstream.WriteByte(0);
                        compressedLength++;
                    }/**/
                }
            }

            return compressedLength;
        }
        #endregion

        public static uint ToNDSu32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16)
                        | (buffer[offset + 3] << 24));
        }
    }
}
