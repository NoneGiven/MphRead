using System;
using System.IO;

namespace MphRead
{
    public static class Lz
    {
        private static readonly byte _magicByte = 0x10;

        public static long Decompress(string input, string output)
        {
            using FileStream inStream = File.Open(input, FileMode.Open);
            using FileStream outStream = File.Open(output, FileMode.Create);
            return Decompress(inStream, inStream.Length, outStream);
        }

        public static long Decompress(Stream instream, long inLength, Stream outstream)
        {
            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            if (type != _magicByte)
            {
                throw new InvalidDataException("The provided stream is not a valid LZ-0x10 "
                    + "compressed stream (invalid type 0x" + type.ToString("X") + ")");
            }

            byte[] sizeBytes = new byte[3];
            instream.Read(sizeBytes, 0, 3);
            int decompressedSize = ToNDSu24(sizeBytes, 0);
            readBytes += 4;
            if (decompressedSize == 0)
            {
                sizeBytes = new byte[4];
                instream.Read(sizeBytes, 0, 4);
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
            outstream.WriteByte(_magicByte);
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
                    int length = GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 0x12),
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

        private static unsafe int GetOccurrenceLength(byte* newPtr, int newLength, byte* oldPtr, int oldLength, out int disp, int minDisp = 1)
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
}
