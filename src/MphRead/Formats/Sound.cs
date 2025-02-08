using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MphRead.Sound;

namespace MphRead.Formats.Sound
{
    public static partial class SoundRead
    {
        public static void ExportSamples(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadSoundSamples(), adpcmRoundingError, prefix: "mph_");
        }

        public static void ExportWfsSamples(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadWfsSoundSamples(), adpcmRoundingError, prefix: "mph_wfs_");
        }

        private static void ExportSamples(IReadOnlyList<SoundSample> samples, bool adpcmRoundingError, string? prefix = null)
        {
            foreach (SoundSample sample in samples)
            {
                try
                {
                    ExportSample(sample, adpcmRoundingError, prefix);
                }
                catch (WaveExportException ex)
                {
                    Console.WriteLine($"[{sample.Id}] {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        public static void ExportSample(int id, bool adpcmRoundingError = false)
        {
            IReadOnlyList<SoundSample> samples = ReadSoundSamples();
            ExportSample(samples[id], adpcmRoundingError);
        }

        public static void ExportWfsSample(int id, bool adpcmRoundingError = false)
        {
            IReadOnlyList<SoundSample> samples = ReadWfsSoundSamples();
            ExportSample(samples[id], adpcmRoundingError);
        }

        public static byte[] GetWaveData(SoundSample sample, bool adpcmRoundingError = false)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            GetWaveData(sample.CreateSpan(), sample.Format, GetSampleCount(sample), adpcmRoundingError, writer);
            return ms.ToArray();
        }

        private static void GetWaveData(ReadOnlySpan<byte> data, WaveFormat format, uint sampleCount,
            bool adpcmRoundingError, BinaryWriter writer)
        {
            if (format == WaveFormat.ADPCM)
            {
                int transferred = 0;
                bool low = true;
                int sampleValue = BitConverter.ToInt16(data[..2]);
                int stepIndex = BitConverter.ToInt16(data[2..4]);
                transferred += 4;
                for (int i = 0; i < sampleCount; i++)
                {
                    byte value = data[transferred];
                    if (!low)
                    {
                        value >>= 4;
                        transferred++;
                    }
                    value &= 0x0F;
                    int step = Metadata.AdpcmTable[stepIndex];
                    int diff = step >> 3;
                    if ((value & 1) != 0)
                    {
                        diff += step >> 2;
                    }
                    if ((value & 2) != 0)
                    {
                        diff += step >> 1;
                    }
                    if ((value & 4) != 0)
                    {
                        diff += step;
                    }
                    if (adpcmRoundingError)
                    {
                        if ((value & 8) != 0)
                        {
                            sampleValue -= diff;
                            if (sampleValue < -32767)
                            {
                                sampleValue = -32767;
                            }
                        }
                        else
                        {
                            sampleValue += diff;
                            if (sampleValue > 32767)
                            {
                                sampleValue = 32767;
                            }
                        }
                    }
                    else
                    {
                        if ((value & 8) != 0)
                        {
                            sampleValue -= diff;
                        }
                        else
                        {
                            sampleValue += diff;
                        }
                        if (sampleValue < -32768)
                        {
                            sampleValue = -32768;
                        }
                        if (sampleValue > 32767)
                        {
                            sampleValue = 32767;
                        }
                    }
                    stepIndex += Metadata.ImaIndexTable[value];
                    if (stepIndex < 0)
                    {
                        stepIndex = 0;
                    }
                    else if (stepIndex > 88)
                    {
                        stepIndex = 88;
                    }
                    writer.Write2(sampleValue);
                    low = !low;
                }
            }
            else
            {
                Debug.Assert(format == WaveFormat.PCM8
                    || format == WaveFormat.None && sampleCount == 0 && data.Length == 0);
                for (int i = 0; i < sampleCount; i++)
                {
                    writer.Write((byte)(data[i] ^ 0x80));
                }
            }
        }

        private static uint GetSampleCount(SoundSample sample)
        {
            uint loopStart;
            uint loopLength;
            if (sample.Format == WaveFormat.ADPCM)
            {
                loopStart = (sample.SampleStart * 4u - 4u) * 2u;
                loopLength = sample.SampleLength * 8u;
            }
            else
            {
                // this is valid for FH's header format only
                loopStart = sample.SampleStart;
                loopLength = sample.SampleLength;
            }
            return loopStart + loopLength;
        }

        // MPH uses ADPCM, FH uses ADPCM and PCM8
        private static void ExportSample(SoundSample sample, bool adpcmRoundingError = false, string? prefix = null)
        {
            string id = sample.Id.ToString().PadLeft(3, '0');
            byte[] waveData = GetWaveData(sample, adpcmRoundingError);
            ExportAudio(waveData, GetSampleCount(sample), sample.SampleRate, sample.Format, id, prefix);
        }

        private static void ExportAudio(ReadOnlySpan<byte> waveData, uint sampleCount, ushort sampleRate,
            WaveFormat format, string name, string? prefix = null)
        {
            if (waveData.Length == 0)
            {
                throw new WaveExportException($"Sample {name} contains no data.");
            }
            if (format != WaveFormat.ADPCM && format != WaveFormat.PCM8)
            {
                throw new WaveExportException($"Format {format} is unsupported.");
            }
            uint bps = format == WaveFormat.PCM8 ? 8u : 16u;
            uint headerSize = 0x2C;
            uint waveSize = sampleCount * bps / 8 + headerSize;
            uint decodedSize = sampleCount * (bps / 8);
            string path = Paths.Combine(Paths.Export, "_SFX");
            Directory.CreateDirectory(path);
            path = Paths.Combine(path, $"{prefix + name}.wav");
            using FileStream file = File.OpenWrite(path);
            using var writer = new BinaryWriter(file);
            writer.WriteC("RIFF");
            writer.Write4(waveSize - 8);
            writer.WriteC("WAVE");
            writer.WriteC("fmt ");
            writer.Write4(16);
            writer.Write2(1);
            writer.Write2(1);
            writer.Write4(sampleRate);
            writer.Write4(sampleRate * (bps / 8));
            writer.Write2(bps / 8);
            writer.Write2(bps);
            writer.WriteC("data");
            writer.Write4(decodedSize);
            for (int i = 0; i < waveData.Length; i++)
            {
                writer.Write(waveData[i]);
            }
            Nop();
        }

        public static IReadOnlyList<SoundSample> ReadSoundSamples()
        {
            return ReadSoundSamples("SNDSAMPLES.DAT");
        }

        public static IReadOnlyList<SoundSample> ReadWfsSoundSamples()
        {
            return ReadSoundSamples("WFSSNDSAMPLES.DAT");
        }

        // note: SNDSAMPLES data is padded to multiples of 512 bytes, WFSSNDSAMPLES data is not
        // todo: confirm no unused bytes (given the padding)
        private static IReadOnlyList<SoundSample> ReadSoundSamples(string filename)
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", filename);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            IReadOnlyList<uint> offsets = Read.DoOffsets<uint>(bytes, 4, count);
            var samples = new List<SoundSample>();
            uint id = 0;
            foreach (uint offset in offsets)
            {
                if (offset == 0)
                {
                    samples.Add(SoundSample.CreateNull(id));
                }
                else
                {
                    // offset is also found in SNDTBLS.DAT, along with the size directly
                    // misc note: at runtime(?), the lid SFX offsets in the sndtbls list are changed to pointers
                    SoundSampleHeader header = Read.DoOffset<SoundSampleHeader>(bytes, offset);
                    long start = offset + Marshal.SizeOf<SoundSampleHeader>();
                    uint size = (header.LoopStart + header.LoopLength) * 4;
                    samples.Add(new SoundSample(id, offset, header, bytes.Slice(start, size)));
                }
                id++;
            }
            return samples;
        }

        public static IReadOnlyList<SoundSelectEntry> ReadBgmSelectList()
        {
            return ReadSelectList("BGMSELECTLIST.DAT");
        }

        public static IReadOnlyList<SoundSelectEntry> ReadSfxSelectList()
        {
            return ReadSelectList("SFXSELECTLIST.DAT");
        }

        private static IReadOnlyList<SoundSelectEntry> ReadSelectList(string filename)
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", filename);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            IReadOnlyList<RawSoundSelectEntry> rawEntries = Read.DoOffsets<RawSoundSelectEntry>(bytes, 4, count);
            long offset = count * Marshal.SizeOf<RawSoundSelectEntry>() + 4;
            IReadOnlyList<string> names = Read.ReadStrings(bytes, offset, count);
            var entries = new List<SoundSelectEntry>();
            for (int i = 0; i < rawEntries.Count; i++)
            {
                entries.Add(new SoundSelectEntry(names[i], rawEntries[i]));
            }
            return entries;
        }

        public static IReadOnlyList<Sound3dEntry> ReadSound3dList()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "SND3DLIST.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            return Read.DoOffsets<Sound3dEntry>(bytes, 4, count);
        }

        public static SoundTable ReadSoundTables()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "SNDTBLS.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            IReadOnlyList<RawSoundTableEntry> rawEntries = Read.DoOffsets<RawSoundTableEntry>(bytes, 4, count);
            long offset = count * Marshal.SizeOf<RawSoundTableEntry>() + 4;
            IReadOnlyList<string> names = Read.ReadStrings(bytes, offset, count);
            offset += names.Sum(n => n.Length) + names.Count;
            IReadOnlyList<string> categories = Read.ReadStrings(bytes, offset, rawEntries.Max(r => r.CategoryId) + 1);
            var entries = new List<SoundTableEntry>();
            for (int i = 0; i < rawEntries.Count; i++)
            {
                RawSoundTableEntry rawEntry = rawEntries[i];
                entries.Add(new SoundTableEntry(names[i], categories[rawEntry.CategoryId], rawEntry));
            }
            return new SoundTable(entries, categories);
        }

        public static IReadOnlyList<RoomMusic> ReadAssignMusic()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "ASSIGNMUSIC.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            return Read.DoOffsets<RoomMusic>(bytes, 4, count);
        }

        public static IReadOnlyList<MusicTrack> ReadInterMusicInfo()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "INTERMUSICINFO.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            var tracks = new List<MusicTrack>(0);
            uint id = 0;
            foreach (RawMusicTrack track in Read.DoOffsets<RawMusicTrack>(bytes, 4, count))
            {
                tracks.Add(new MusicTrack(id++, track));
            }
            return tracks;
        }

        public static IReadOnlyList<SfxScriptFile> ReadSfxScriptFiles()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "SFXSCRIPTFILES.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint fileCount = Read.SpanReadUint(bytes, 0);
            Debug.Assert(fileCount > 0);
            var files = new List<SfxScriptFile>();
            IReadOnlyList<SfxScriptHeader> headers = Read.DoOffsets<SfxScriptHeader>(bytes, 4, fileCount);
            IReadOnlyList<string> names = Read.ReadStrings(bytes, headers[^1].Offset + headers[^1].Size, fileCount);
            int max = 0;
            for (int i = 0; i < headers.Count; i++)
            {
                SfxScriptHeader header = headers[i];
                uint entryCount = Read.SpanReadUint(bytes, header.Offset);
                IReadOnlyList<RawSfxScriptEntry> raw = Read.DoOffsets<RawSfxScriptEntry>(bytes, header.Offset + 4, entryCount);
                files.Add(new SfxScriptFile(names[i], header, raw));
                max = Math.Max(max, raw.Count);
            }
            return files;
        }

        public static IReadOnlyList<DgnFile> ReadDgnFiles()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "DGNFILES.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint fileCount = Read.SpanReadUint(bytes, 0);
            Debug.Assert(fileCount > 0);
            var files = new List<DgnFile>();
            IReadOnlyList<DgnHeader> headers = Read.DoOffsets<DgnHeader>(bytes, 4, fileCount);
            IReadOnlyList<string> names = Read.ReadStrings(bytes, headers[^1].Offset + headers[^1].Size, fileCount);
            for (int i = 0; i < headers.Count; i++)
            {
                DgnHeader header = headers[i];
                var entries = new List<DgnFileEntry>();
                uint entryCount = Read.SpanReadUint(bytes, header.Offset);
                foreach (DgnEntry entry in Read.DoOffsets<DgnEntry>(bytes, header.Offset + 4, entryCount))
                {
                    var newEntry = new DgnFileEntry(
                        entry.SfxId,
                        Read.DoOffsets<DgnData>(bytes, header.Offset + entry.Offset1, entry.Count1),
                        Read.DoOffsets<DgnData>(bytes, header.Offset + entry.Offset2, entry.Count2),
                        Read.DoOffsets<DgnData>(bytes, header.Offset + entry.Offset3, entry.Count3),
                        Read.DoOffsets<DgnData>(bytes, header.Offset + entry.Offset4, entry.Count4)
                    );

                    static void CheckStuff(IReadOnlyList<DgnData> data)
                    {
                        foreach (DgnData item in data)
                        {
                            int flags = item.Value & 0xC000;
                            if (flags != 0 && flags != 0x4000)
                            {
                                Debugger.Break();
                            }
                        }
                    }

                    CheckStuff(newEntry.Data1);
                    CheckStuff(newEntry.Data2);
                    CheckStuff(newEntry.Data3);
                    CheckStuff(newEntry.Data4);

                    entries.Add(newEntry);
                }
                files.Add(new DgnFile(names[i], header, entries));
            }
            return files;
        }

        public static void Nop() { }

        public static SoundData ReadSdat()
        {
            string path = Paths.Combine(Paths.FileSystem, "data", "sound", "sound_data.sdat");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            SdatHeader sdatHeader = Read.ReadStruct<SdatHeader>(bytes);
            Debug.Assert(sdatHeader.Type.MarshalString() == "SDAT");
            Debug.Assert(sdatHeader.Magic == 0x100FEFF);
            Debug.Assert(sdatHeader.SymbolBlockOffset != 0);
            Debug.Assert(sdatHeader.InfoBlockOffset != 0);
            Debug.Assert(sdatHeader.FatOffset != 0);
            Debug.Assert(sdatHeader.FileBlockOffset != 0);

            // symbols
            BlockHeader symbHeader = Read.DoOffset<BlockHeader>(bytes, sdatHeader.SymbolBlockOffset);
            Debug.Assert(symbHeader.Type.MarshalString() == "SYMB");
            Debug.Assert(symbHeader.SeqOffset != 0);
            Debug.Assert(symbHeader.SeqarcOffset != 0);
            Debug.Assert(symbHeader.BankOffset != 0);
            Debug.Assert(symbHeader.WavearcOffset != 0);
            Debug.Assert(symbHeader.PlayerOffset != 0);
            Debug.Assert(symbHeader.GroupOffset != 0);
            Debug.Assert(symbHeader.StrmPlayerOffset != 0);
            Debug.Assert(symbHeader.StrmOffset != 0);

            static IReadOnlyList<string> GetNames(ReadOnlySpan<byte> bytes, uint offset)
            {
                var results = new List<string>();
                uint count = Read.SpanReadUint(bytes, offset);
                foreach (uint entryOffset in Read.DoOffsets<uint>(bytes, offset + 4, count))
                {
                    if (entryOffset != 0)
                    {
                        results.Add(Read.ReadString(bytes, entryOffset));
                    }
                }
                return results;
            }

            ReadOnlySpan<byte> symbBytes = bytes[(int)sdatHeader.SymbolBlockOffset..];
            IReadOnlyList<string> seqNames = GetNames(symbBytes, symbHeader.SeqOffset);
            IReadOnlyList<string> bankNames = GetNames(symbBytes, symbHeader.BankOffset);
            IReadOnlyList<string> wavearcNames = GetNames(symbBytes, symbHeader.WavearcOffset);
            IReadOnlyList<string> playerNames = GetNames(symbBytes, symbHeader.PlayerOffset);
            IReadOnlyList<string> groupNames = GetNames(symbBytes, symbHeader.GroupOffset);
            IReadOnlyList<string> strmPlayerNames = GetNames(symbBytes, symbHeader.StrmPlayerOffset);
            IReadOnlyList<string> strmNames = GetNames(symbBytes, symbHeader.StrmOffset);
            var seqarcNames = new List<(string, IReadOnlyList<string>)>();
            uint arcOffset = symbHeader.SeqarcOffset;
            uint seqarcCount = Read.SpanReadUint(symbBytes, arcOffset);
            IReadOnlyList<SeqArcEntries> seqarcs = Read.DoOffsets<SeqArcEntries>(symbBytes, arcOffset + 4, seqarcCount);
            foreach (SeqArcEntries seqarc in seqarcs)
            {
                string arcName = Read.ReadString(symbBytes, seqarc.EntryOffset);
                IReadOnlyList<string> fileNames = GetNames(symbBytes, seqarc.FilesOffset);
                seqarcNames.Add((arcName, fileNames));
            }

            // info
            BlockHeader infoHeader = Read.DoOffset<BlockHeader>(bytes, sdatHeader.InfoBlockOffset);
            Debug.Assert(infoHeader.Type.MarshalString() == "INFO");
            Debug.Assert(infoHeader.SeqOffset != 0);
            Debug.Assert(infoHeader.SeqarcOffset != 0);
            Debug.Assert(infoHeader.BankOffset != 0);
            Debug.Assert(infoHeader.WavearcOffset != 0);
            Debug.Assert(infoHeader.PlayerOffset != 0);
            Debug.Assert(infoHeader.GroupOffset != 0);
            Debug.Assert(infoHeader.StrmPlayerOffset != 0);
            Debug.Assert(infoHeader.StrmOffset != 0);

            static IReadOnlyList<T> GetStructs<T>(ReadOnlySpan<byte> bytes, uint offset) where T : struct
            {
                var results = new List<T>();
                uint count = Read.SpanReadUint(bytes, offset);
                foreach (uint strOffset in Read.DoOffsets<uint>(bytes, offset + 4, count))
                {
                    if (strOffset != 0)
                    {
                        results.Add(Read.DoOffset<T>(bytes, strOffset));
                    }
                }
                return results;
            }

            ReadOnlySpan<byte> infoBytes = bytes[(int)sdatHeader.InfoBlockOffset..];
            IReadOnlyList<SeqInfo> seqInfo = GetStructs<SeqInfo>(infoBytes, infoHeader.SeqOffset);
            IReadOnlyList<uint> seqarcInfo = GetStructs<uint>(infoBytes, infoHeader.SeqarcOffset);
            IReadOnlyList<BankInfo> bankInfo = GetStructs<BankInfo>(infoBytes, infoHeader.BankOffset);
            IReadOnlyList<uint> wavearcInfo = GetStructs<uint>(infoBytes, infoHeader.WavearcOffset);
            IReadOnlyList<PlayerInfo> playerInfo = GetStructs<PlayerInfo>(infoBytes, infoHeader.PlayerOffset);
            IReadOnlyList<StrmPlayerInfo> strmPlayerInfo = GetStructs<StrmPlayerInfo>(infoBytes, infoHeader.StrmPlayerOffset);
            IReadOnlyList<StrmInfo> strmInfo = GetStructs<StrmInfo>(infoBytes, infoHeader.StrmOffset);
            var groupInfo = new List<IReadOnlyList<GroupItemInfo>>();
            uint groupOffset = infoHeader.GroupOffset;
            uint groupCount = Read.SpanReadUint(infoBytes, groupOffset);
            groupOffset += 4;
            for (int i = 0; i < groupCount; i++)
            {
                uint itemCount = Read.SpanReadUint(infoBytes, groupOffset);
                groupOffset += 4;
                groupInfo.Add(Read.DoOffsets<GroupItemInfo>(infoBytes, groupOffset, itemCount));
                groupOffset += itemCount * 8;
            }

            // FAT
            uint fatOffset = sdatHeader.FatOffset;
            SdatFatHeader fatHeader = Read.DoOffset<SdatFatHeader>(bytes, fatOffset);
            Debug.Assert(fatHeader.Type.MarshalString() == "FAT ");
            fatOffset += 12;
            IReadOnlyList<SdatFatEntry> fatEntries = Read.DoOffsets<SdatFatEntry>(bytes, fatOffset, fatHeader.Count);
            SdatFileHeader fileHeader = Read.DoOffset<SdatFileHeader>(bytes, sdatHeader.FileBlockOffset);
            Debug.Assert(fileHeader.Type.MarshalString() == "FILE");
            Debug.Assert(fileHeader.Count == fatHeader.Count);
            int filesRead = 0;

            // SSEQ
            foreach (SeqInfo info in seqInfo)
            {
                // mustodo: read SSEQ files
                filesRead++;
            }

            // SSAR
            foreach (uint fileId in seqarcInfo)
            {
                // mustodo: read SSAR files
                filesRead++;
            }

            // SBNK
            foreach (BankInfo info in bankInfo)
            {
                // mustodo: read SBNK files
                filesRead++;
            }

            // SWAR
            foreach (uint fileId in wavearcInfo)
            {
                // mustodo: read SWAR files
                filesRead++;
            }

            // STRM
            var streams = new List<SoundStream>();
            for (int i = 0; i < strmInfo.Count; i++)
            {
                StrmInfo info = strmInfo[i];
                string name = strmNames[i];
                SdatFatEntry entry = fatEntries[(int)info.FileId];
                SoundStreamHeader header = Read.DoOffset<SoundStreamHeader>(bytes, entry.Offset);
                Debug.Assert(header.Type.MarshalString() == "STRM");
                Debug.Assert(header.HeadType.MarshalString() == "HEAD");
                Debug.Assert(header.Channels == 1 || header.Channels == 2);
                Debug.Assert(header.BlockCount > 1 || header.BlockSize == header.LastBlockSize);
                Debug.Assert(header.BlockCount > 1 || header.BlockSamples == header.LastBlockSamples);
                var format = (WaveFormat)header.Format;
                Debug.Assert(format == WaveFormat.PCM8 || format == WaveFormat.ADPCM);
                Debug.Assert(format == WaveFormat.ADPCM || header.BlockCount == 1);
                var channels = new List<byte[]>();
                uint channelSize = header.BlockSamples * (header.BlockCount - 1) + header.LastBlockSamples;
                if (format == WaveFormat.ADPCM)
                {
                    channelSize *= 2;
                }
                for (uint j = 0; j < header.Channels; j++)
                {
                    byte[] channel = new byte[channelSize];
                    using var ms = new MemoryStream(channel);
                    using var writer = new BinaryWriter(ms);
                    uint start = entry.Offset + header.DataOffset + header.BlockSize * j;
                    for (int k = 0; k < header.BlockCount; k++)
                    {
                        uint size;
                        uint samples;
                        uint increment;
                        if (k == header.BlockCount - 1)
                        {
                            size = header.LastBlockSize;
                            samples = header.LastBlockSamples;
                            increment = 0;
                        }
                        else
                        {
                            size = header.BlockSize;
                            samples = header.BlockSamples;
                            if (j > 0 && k == header.BlockCount - 2)
                            {
                                increment = size + header.LastBlockSize;
                            }
                            else
                            {
                                increment = size * header.Channels;
                            }
                        }
                        uint end = start + size;
                        ReadOnlySpan<byte> data = bytes[(int)start..(int)end];
                        GetWaveData(data, format, samples, adpcmRoundingError: false, writer);
                        start += increment;
                    }
                    channels.Add(channel);
                }
                streams.Add(new SoundStream(i, name, header, channels, info.Volume / 127f));
                filesRead++;
            }
            Debug.Assert(filesRead == fileHeader.Count);
            return new SoundData(streams);
        }

        public static void ExportStreams()
        {
            SoundData soundData = ReadSdat();
            foreach (SoundStream stream in soundData.Streams)
            {
                ExportStream(stream);
            }
        }

        public static void ExportStream(SoundStream stream)
        {
            for (int i = 0; i < stream.Channels.Count; i++)
            {
                byte[] channel = stream.Channels[i];
                string id = stream.Id.ToString().PadLeft(3, '0');
                string suffix = "";
                if (stream.Channels.Count == 2)
                {
                    suffix = i == 0 ? "_L" : "_R";
                }
                string filename = $"{id}_{stream.Name}{suffix}";
                int length = channel.Length;
                if (stream.Format == WaveFormat.ADPCM)
                {
                    length /= 2;
                }
                ExportAudio(channel, (uint)length, stream.SampleRate, stream.Format, filename);
            }
        }

        public static byte[] GetStreamBufferData(SoundStream stream)
        {
            int bytesPerSample = stream.Format == WaveFormat.ADPCM ? 2 : 1;
            int length = stream.Channels[0].Length * stream.Channels.Count;
            byte[] data = new byte[length];
            for (int i = 0; i < stream.Channels.Count; i++)
            {
                byte[] channel = stream.Channels[i];
                int destIndex = i * bytesPerSample;
                int srcIndex = 0;
                for (int j = 0; j < channel.Length; j += bytesPerSample)
                {
                    for (int k = 0; k < bytesPerSample; k++)
                    {
                        data[destIndex++] = channel[srcIndex++];
                    }
                    destIndex += bytesPerSample * (stream.Channels.Count - 1);
                }
            }
            return data;
        }

        private readonly struct SeqArcEntries
        {
            public readonly uint EntryOffset;
            public readonly uint FilesOffset;

            public SeqArcEntries(uint entryOffset, uint filesOffset)
            {
                EntryOffset = entryOffset;
                FilesOffset = filesOffset;
            }
        }
    }

    // size: 64
    public readonly struct SdatHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // SDAT - no terminator
        public readonly uint Magic;
        public readonly uint FileSize;
        public readonly ushort HeaderSize;
        public readonly ushort BlockCount;
        public readonly uint SymbolBlockOffset;
        public readonly uint SymbolBlockSize;
        public readonly uint InfoBlockOffset;
        public readonly uint InfoBlockSize;
        public readonly uint FatOffset;
        public readonly uint FatSize;
        public readonly uint FileBlockOffset;
        public readonly uint FileBlockSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly byte[] Reserved;
    }

    // size: 64
    public readonly struct BlockHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // SYMB or INFO - no terminator
        public readonly uint HeaderSize;
        public readonly uint SeqOffset; // relative offsets
        public readonly uint SeqarcOffset;
        public readonly uint BankOffset;
        public readonly uint WavearcOffset;
        public readonly uint PlayerOffset;
        public readonly uint GroupOffset;
        public readonly uint StrmPlayerOffset;
        public readonly uint StrmOffset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public readonly byte[] Reserved;
    }

    // size: 12
    public readonly struct SdatFatHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // FAT_ - no terminator
        public readonly uint Size;
        public readonly uint Count;
    }

    // size: 16
    public readonly struct SdatFatEntry
    {
        public readonly uint Offset;
        public readonly uint Size;
        public readonly uint Pointer; // set at runtime
        public readonly uint Reserved;
    }

    // size: 16
    public readonly struct SdatFileHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // FILE - no terminator
        public readonly uint Size;
        public readonly uint Count;
        public readonly uint Reserved;
    }

    // size: 12
    public readonly struct SeqInfo
    {
        public readonly uint FileId;
        public readonly ushort BankNo;
        public readonly byte Volume;
        public readonly byte ChannelPriority;
        public readonly byte PlayerPriority;
        public readonly byte PlayerNo;
        public readonly ushort Reserved;
    }

    // size: 12
    public readonly struct BankInfo
    {
        public readonly uint FileId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly ushort[] WaveArcNo;
    }

    // size: 8
    public readonly struct PlayerInfo
    {
        public readonly byte MaxSequences;
        public readonly byte Padding1;
        public readonly ushort AllocateChannelBits;
        public readonly uint HeapSize;
    }

    // size: 8
    public readonly struct GroupItemInfo
    {
        public readonly byte Type; // 0 - seq, 1 - bank, 2 - wavearc, 3 - seqarc
        public readonly byte LoadTypes; // 1 - sequence | 2 - bank | 4 - wave
        public readonly ushort Padding2;
        public readonly uint LoadIndex;
    }

    // size: 24
    public readonly struct StrmPlayerInfo
    {
        public readonly byte ChannelCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly byte[] ChannelNumbers;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public readonly byte[] Reserved;
    }

    // size: 8
    public readonly struct StrmInfo
    {
        public readonly uint FileId;
        public readonly byte Volume;
        public readonly byte PlayerPriority;
        public readonly byte PlayerNo;
        public readonly byte Flags;
    }

    // size: 12
    public readonly struct SoundSampleHeader
    {
        public readonly byte Format; // 0 - PCM8, 1 - PCM16, 2 - ADPCM
        public readonly byte LoopFlag; // boolean
        public readonly ushort SampleRate;
        public readonly ushort Timer; // SND_TIMER_CLOCK / SampleRate
        public readonly ushort LoopStart; // number of 32-bit words
        public readonly uint LoopLength; // number of 32-bit words
    }

    // size: 104
    public readonly struct SoundStreamHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // STRM - no terminator
        public readonly uint Magic;
        public readonly uint DataSize;
        public readonly ushort Size;
        public readonly ushort DataBlocks;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] HeadType; // HEAD - no terminator
        public readonly uint HeaderSize;
        public readonly byte Format; // 0 - PCM8, 1 - PCM16, 2 - ADPCM
        public readonly byte LoopFlag; // boolean
        public readonly byte Channels;
        public readonly byte Padding25;
        public readonly ushort SampleRate;
        public readonly ushort Timer; // SND_TIMER_CLOCK / SampleRate
        public readonly uint LoopStart;
        public readonly uint LoopEnd;
        public readonly uint DataOffset;
        public readonly uint BlockCount;
        public readonly uint BlockSize;
        public readonly uint BlockSamples;
        public readonly uint LastBlockSize;
        public readonly uint LastBlockSamples;
        // not included: 32 reserved bytes, "DATA"/size fields before data blocks
    }

    public class SoundData
    {
        public IReadOnlyList<SoundStream> Streams { get; }

        public SoundData(IReadOnlyList<SoundStream> streams)
        {
            Streams = streams;
        }
    }

    public class SoundStream
    {
        public int Id { get; }
        public string Name { get; }
        public WaveFormat Format { get; }
        public bool Loop { get; }
        public ushort SampleRate { get; }
        public uint LoopStart { get; }
        public uint LoopEnd { get; }

        private readonly IReadOnlyList<byte[]> _channels;
        public IReadOnlyList<byte[]> Channels => _channels;

        public float Volume { get; set; } = 1;
        public Lazy<byte[]> BufferData { get; }

        public SoundStream(int id, string name, SoundStreamHeader header,
            IReadOnlyList<byte[]> channels, float volume)
        {
            Id = id;
            Name = name;
            Format = (WaveFormat)header.Format;
            Loop = header.LoopFlag != 0;
            SampleRate = header.SampleRate;
            LoopStart = header.LoopStart;
            LoopEnd = header.LoopEnd;
            _channels = channels;
            Volume = volume;
            BufferData = new Lazy<byte[]>(() => SoundRead.GetStreamBufferData(this));
        }
    }

    public class SoundSample
    {
        public uint Id { get; }
        public uint Offset { get; }
        public WaveFormat Format { get; }
        public bool Loop { get; }
        public ushort SampleRate { get; }
        public uint SampleStart { get; }
        public uint SampleLength { get; }
        public int LoopStart { get; }
        public int LoopLength { get; }

        private readonly byte[] _data;
        public IReadOnlyList<byte> Data => _data;

        public float Volume { get; set; } = 1;
        public Lazy<byte[]> WaveData { get; }
        public int BufferId { get; set; }
        public int MaxBuffers { get; set; }
        public int BufferCount { get; set; }
        public int References { get; set; }
        public string? Name { get; set; }

        public SoundSample(uint id, uint offset, SoundSampleHeader header, ReadOnlySpan<byte> data)
        {
            if (header.Format < 0 || header.Format > 2)
            {
                throw new ProgramException($"Invalid wave format {header.Format}.");
            }
            Id = id;
            Offset = offset;
            Format = (WaveFormat)header.Format;
            Loop = header.LoopFlag != 0;
            SampleRate = header.SampleRate;
            SampleStart = header.LoopStart;
            SampleLength = header.LoopLength;
            _data = data.ToArray();
            WaveData = new Lazy<byte[]>(() => SoundRead.GetWaveData(this));
            if (Format == WaveFormat.ADPCM)
            {
                LoopStart = (int)((SampleStart * 4u - 4u) * 2u);
                LoopLength = (int)(SampleLength * 8u);
            }
        }

        public SoundSample(uint id, uint offset, FhSoundSampleHeader header, ReadOnlySpan<byte> data)
        {
            Id = id;
            Offset = offset;
            SampleRate = header.SampleRate;
            if (header.Format == 4)
            {
                Format = WaveFormat.ADPCM;
                Loop = header.LoopStart > 1;
                SampleStart = header.LoopStart;
                if (header.LoopEnd * 4 > data.Length)
                {
                    // a few FH sounds seem to have the length off by one
                    SampleLength = (header.LoopEnd - header.LoopStart - 1);
                }
                else
                {
                    SampleLength = (header.LoopEnd - header.LoopStart);
                }
            }
            else if (header.Format == 0)
            {
                Format = WaveFormat.PCM8;
                Loop = header.LoopStart > 0;
                SampleStart = header.LoopStart;
                SampleLength = header.LoopEnd - header.LoopStart;
            }
            else
            {
                throw new ProgramException($"Unexpected FH sound header value: {header.Format}");
            }
            _data = data.ToArray();
            WaveData = new Lazy<byte[]>(() => SoundRead.GetWaveData(this));
            if (Format == WaveFormat.ADPCM)
            {
                LoopStart = (int)((SampleStart * 4u - 4u) * 2u);
                LoopLength = (int)(SampleLength * 8u);
            }
            else
            {
                Debug.Assert(Format == WaveFormat.PCM8);
                // this is valid for FH's header format only
                LoopStart = (int)SampleStart;
                LoopLength = (int)SampleLength;
            }
        }

        private SoundSample(uint id)
        {
            Id = id;
            Format = WaveFormat.None;
            _data = Array.Empty<byte>();
            WaveData = new Lazy<byte[]>(() => _data);
        }

        public static SoundSample CreateNull(uint id)
        {
            return new SoundSample(id);
        }

        public ReadOnlySpan<byte> CreateSpan()
        {
            return new ReadOnlySpan<byte>(_data);
        }

        public ReadOnlySpan<byte> GetIntro()
        {
            if (LoopStart == 0)
            {
                return new ReadOnlySpan<byte>();
            }
            byte[] data = WaveData.Value;
            int factor = Format == WaveFormat.ADPCM ? 2 : 1;
            return new ReadOnlySpan<byte>(data, 0, LoopStart * factor);
        }

        public ReadOnlySpan<byte> GetLoop()
        {
            byte[] data = WaveData.Value;
            int factor = Format == WaveFormat.ADPCM ? 2 : 1;
            return new ReadOnlySpan<byte>(data, LoopStart * factor, LoopLength * factor);
        }

        public ReadOnlySpan<byte> GetOutro()
        {
            byte[] data = WaveData.Value;
            int factor = Format == WaveFormat.ADPCM ? 2 : 1;
            int start = (LoopStart + LoopLength) * factor;
            if (start >= data.Length)
            {
                return new ReadOnlySpan<byte>();
            }
            return new ReadOnlySpan<byte>(data, start, data.Length - start);
        }
    }

    // size: 16
    public readonly struct RawSoundSelectEntry
    {
        public readonly ushort Id;
        // BGM: 0 - SSEQ ID, 1 - track ID, 2 - STRM ID
        // SFX: 0 - SFX ID, 1 - DGN ID, 2 - script ID, 3 - STRM ID
        public readonly ushort Type;
        public readonly uint Field4;
        public readonly uint Field8;
        public readonly uint FieldC;
    }

    public class SoundSelectEntry
    {
        public string Name { get; }
        public ushort Id { get; }
        public ushort Type { get; }
        public uint Field4 { get; }
        public uint Field8 { get; }
        public uint FieldC { get; }

        public SoundSelectEntry(string name, RawSoundSelectEntry raw)
        {
            Name = name;
            Id = raw.Id;
            Type = raw.Type;
            Field4 = raw.Field4;
            Field8 = raw.Field8;
            FieldC = raw.FieldC;
        }
    }

    // size: 8
    public readonly struct Sound3dEntry
    {
        public readonly uint FalloffDistance;
        public readonly uint MaxDistance;
    }

    // size: 12
    public readonly struct RawSoundTableEntry
    {
        public readonly ushort Exists; // -1 - doesn't exist, 0 - exists
        public readonly byte CategoryId;
        public readonly byte SlotCount;
        public readonly byte InitialVolume;
        public readonly byte Priority;
        public readonly ushort Size;
        public readonly uint Data;
    }

    public class SoundTable
    {
        public IReadOnlyList<SoundTableEntry> Entries { get; }
        public IReadOnlyList<string> Categories { get; }

        public SoundTable(IReadOnlyList<SoundTableEntry> entries, IReadOnlyList<string> categories)
        {
            Entries = entries;
            Categories = categories;
        }
    }

    public class SoundTableEntry
    {
        public string Name { get; }
        public string Category { get; }
        public byte CategoryId;
        public byte SlotCount;
        public byte InitialVolume;
        public byte Priority;
        public ushort Size;
        public uint Data;

        public SoundTableEntry(string name, string category, RawSoundTableEntry raw)
        {
            Name = name;
            Category = category;
            CategoryId = raw.CategoryId;
            SlotCount = raw.SlotCount;
            InitialVolume = raw.InitialVolume;
            Priority = raw.Priority;
            Size = raw.Size;
            Data = raw.Data;
        }
    }

    // size: 8
    public readonly struct RoomMusic
    {
        public readonly ushort RoomId;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 3)]
        public readonly ushort[] TrackIds;
    }

    // size: 8
    public readonly struct RawMusicTrack
    {
        public readonly ushort SeqId;
        public readonly ushort Field2;
        public readonly ushort Tracks;
        public readonly ushort Field6;
    }

    public class MusicTrack
    {
        public uint Id { get; }
        public ushort SeqId { get; }
        public ushort Field2 { get; }
        public ushort Tracks { get; }
        public ushort Field6 { get; }

        public MusicTrack(uint id, RawMusicTrack raw)
        {
            Id = id;
            SeqId = raw.SeqId;
            Field2 = raw.Field2;
            Tracks = raw.Tracks;
            Field6 = raw.Field6;
        }
    }

    // size: 8
    public readonly struct SfxScriptHeader
    {
        public readonly uint Offset;
        public readonly ushort Size;
        public readonly byte InitialVolume;
        public readonly byte SlotCount;
    }

    // size: 12
    public readonly struct RawSfxScriptEntry
    {
        public readonly ushort SfxId;
        public readonly ushort Delay;
        public readonly byte Volume;
        public readonly byte Pan;
        public readonly ushort Pitch;
        public readonly uint Handle; // set at runtime
    }

    public class SfxScriptEntry
    {
        public int SfxData { get; }
        public float Delay { get; }
        public float Volume { get; }
        public float Pan { get; } = -1;
        public float Pitch { get; }
        public int Handle { get; set; } = -1;

        public SfxScriptEntry(RawSfxScriptEntry raw, SfxScriptHeader header)
        {
            SfxData = raw.SfxId;
            Delay = raw.Delay / 30f;
            Volume = raw.Volume / 127f * header.InitialVolume / 127f;
            if (raw.Pan != 255)
            {
                int rawPan = raw.Pan;
                if (rawPan == 127)
                {
                    rawPan = 128;
                }
                Pan = (rawPan - 64) / 64f / 2f;
            }
            Pitch = Sfx.CalculatePitchDiv(raw.Pitch);
        }
    }

    public class SfxScriptFile
    {
        public string Name { get; }
        public SfxScriptHeader Header { get; }
        public IReadOnlyList<SfxScriptEntry> Entries { get; }

        public SfxScriptFile(string name, SfxScriptHeader header, IReadOnlyList<RawSfxScriptEntry> entries)
        {
            Name = name;
            Header = header;
            Entries = entries.Select(e => new SfxScriptEntry(e, header)).ToList();
        }
    }

    // size: 8
    public readonly struct DgnHeader
    {
        public readonly uint Offset;
        public readonly ushort Size;
        public readonly byte InitialVolume;
        public readonly byte SlotCount;
    }

    // size: 36
    public readonly struct DgnEntry
    {
        public readonly ushort Unused0;
        public readonly ushort SfxId;
        public readonly uint Count1; // blocks
        public readonly uint Offset1;
        public readonly uint Count2;
        public readonly uint Offset2;
        public readonly uint Count3;
        public readonly uint Offset3;
        public readonly uint Count4;
        public readonly uint Offset4;
    }

    // size: 4
    public readonly struct DgnData
    {
        public readonly ushort Amount;
        public readonly ushort Value;
    }

    public class DgnFile
    {
        public string Name { get; }
        public DgnHeader Header { get; }
        public IReadOnlyList<DgnFileEntry> Entries { get; }

        public DgnFile(string name, DgnHeader header, IReadOnlyList<DgnFileEntry> entries)
        {
            Name = name;
            Header = header;
            Entries = entries;
        }
    }

    public class DgnFileEntry
    {
        public SfxId SfxId { get; }
        public IReadOnlyList<DgnData> Data1 { get; } // volume as percentage of max
        public IReadOnlyList<DgnData> Data2 { get; } // max volume
        public IReadOnlyList<DgnData> Data3 { get; } // pitch as percentage of max
        public IReadOnlyList<DgnData> Data4 { get; } // max pitch

        public DgnFileEntry(ushort sfxId, IReadOnlyList<DgnData> data1, IReadOnlyList<DgnData> data2,
            IReadOnlyList<DgnData> data3, IReadOnlyList<DgnData> data4)
        {
            SfxId = (SfxId)sfxId;
            Data1 = data1;
            Data2 = data2;
            Data3 = data3;
            Data4 = data4;
        }
    }

    public class WaveExportException : ProgramException
    {
        public WaveExportException(string message) : base(message) { }
    }

    public static class BinaryWriterExtensions
    {
        public static void WriteC(this BinaryWriter writer, string chars)
        {
            foreach (char character in chars)
            {
                writer.Write(character);
            }
        }

        public static void Write2(this BinaryWriter writer, int value)
        {
            writer.Write((ushort)(uint)value);
        }

        public static void Write2(this BinaryWriter writer, uint value)
        {
            writer.Write((ushort)value);
        }

        public static void Write2(this BinaryWriter writer, short value)
        {
            writer.Write(value);
        }

        public static void Write2(this BinaryWriter writer, ushort value)
        {
            writer.Write(value);
        }

        public static void Write4(this BinaryWriter writer, int value)
        {
            writer.Write(value);
        }

        public static void Write4(this BinaryWriter writer, uint value)
        {
            writer.Write(value);
        }

        public static void Write4(this BinaryWriter writer, short value)
        {
            writer.Write((uint)value);
        }

        public static void Write4(this BinaryWriter writer, ushort value)
        {
            writer.Write((uint)value);
        }
    }
}
