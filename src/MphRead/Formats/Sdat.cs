using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead.Formats.Sound
{
    // todo: are any bytes not being read in SFXSCRIPTFILES or DGNFILES?
    // (we know there are in sound_data, and we know there aren't in the rest of the current ones)
    public static class SoundRead
    {
        public static void ExportSamples(bool adpcmRoundingError = false)
        {
            IReadOnlyList<SoundSample> samples = ReadWfsSoundSamples();
            foreach (SoundSample sample in samples)
            {
                try
                {
                    ExportSample(sample, adpcmRoundingError);
                }
                catch (WaveExportException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public static void ExportSample(int id, bool adpcmRoundingError = false)
        {
            IReadOnlyList<SoundSample> samples = ReadSoundSamples();
            ExportSample(samples[id], adpcmRoundingError);
        }

        // todo: support PCM8 and PCM16, even though MPH doesn't use them
        public static void ExportSample(SoundSample sample, bool adpcmRoundingError = false)
        {
            string id = sample.Id.ToString().PadLeft(3, '0');
            if (sample.Data.Count == 0)
            {
                throw new WaveExportException($"Sample {id} contains no data.");
            }
            if (sample.Format != WaveFormat.ADPCM)
            {
                throw new WaveExportException($"Format {sample.Format} is unsupported.");
            }
            uint bps = 16;
            uint headerSize = 0x2C;
            uint loopStart = (sample.LoopStart * 4u - 4u) * 2u;
            uint loopLength = sample.LoopLength * 8u;
            uint sampleCount = loopStart + loopLength;
            uint waveSize = sampleCount * bps / 8 + headerSize;
            uint decodedSize = sampleCount * (bps / 8);
            string path = Path.Combine(Paths.Export, "_SFX");
            Directory.CreateDirectory(path);
            path = Path.Combine(path, $"{id}.wav");
            using FileStream file = File.OpenWrite(path);
            using var writer = new BinaryWriter(file);
            writer.WriteC("RIFF");
            writer.Write4(waveSize - 8);
            writer.WriteC("WAVE");
            writer.WriteC("fmt ");
            writer.Write4(16);
            writer.Write2(1);
            writer.Write2(1);
            writer.Write4(sample.SampleRate);
            writer.Write4(sample.SampleRate * (bps / 8));
            writer.Write2(bps / 8);
            writer.Write2(bps);
            writer.WriteC("data");
            writer.Write4(decodedSize);
            ReadOnlySpan<byte> data = sample.CreateSpan();
            int transferred = 0;
            bool low = false;
            int sampleValue = 0;
            int stepIndex = 0;
            if (sample.Format == WaveFormat.ADPCM)
            {
                low = true;
                sampleValue = BitConverter.ToInt16(data.Slice(0, 2));
                stepIndex = BitConverter.ToInt16(data.Slice(2, 2));
                transferred += 4;
            }
            for (int i = 0; i < sampleCount; i++)
            {
                //int index = i * ((int)bps / 8);
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
            string path = Path.Combine(Paths.FileSystem, "data", "sound", filename);
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
                    SoundSampleHeader header = Read.DoOffset<SoundSampleHeader>(bytes, offset);
                    long start = offset + Marshal.SizeOf<SoundSampleHeader>();
                    // todo: what are these? and what are the bytes?
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
            string path = Path.Combine(Paths.FileSystem, "data", "sound", filename);
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
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "SND3DLIST.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            return Read.DoOffsets<Sound3dEntry>(bytes, 4, count);
        }

        public static SoundTable ReadSoundTables()
        {
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "SNDTBLS.DAT");
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
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "ASSIGNMUSIC.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            Debug.Assert(count > 0);
            return Read.DoOffsets<RoomMusic>(bytes, 4, count);
        }

        public static IReadOnlyList<MusicTrack> ReadInterMusicInfo()
        {
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "INTERMUSICINFO.DAT");
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
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "SFXSCRIPTFILES.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint fileCount = Read.SpanReadUint(bytes, 0);
            Debug.Assert(fileCount > 0);
            var files = new List<SfxScriptFile>();
            IReadOnlyList<SfxScriptHeader> headers = Read.DoOffsets<SfxScriptHeader>(bytes, 4, fileCount);
            IReadOnlyList<string> names = Read.ReadStrings(bytes, headers.Last().Offset + headers.Last().Size, fileCount);
            for (int i = 0; i < headers.Count; i++)
            {
                SfxScriptHeader header = headers[i];
                uint entryCount = Read.SpanReadUint(bytes, header.Offset);
                files.Add(new SfxScriptFile(names[i], header, Read.DoOffsets<SfxScriptEntry>(bytes, header.Offset + 4, entryCount)));
            }
            return files;
        }

        public static IReadOnlyList<DgnFile> ReadDgnFiles()
        {
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "DGNFILES.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint fileCount = Read.SpanReadUint(bytes, 0);
            Debug.Assert(fileCount > 0);
            var files = new List<DgnFile>();
            IReadOnlyList<DgnHeader> headers = Read.DoOffsets<DgnHeader>(bytes, 4, fileCount);
            IReadOnlyList<string> names = Read.ReadStrings(bytes, headers.Last().Offset + headers.Last().Size, fileCount);
            for (int i = 0; i < headers.Count; i++)
            {
                DgnHeader header = headers[i];
                var entries = new List<DgnFileEntry>();
                uint entryCount = Read.SpanReadUint(bytes, header.Offset);
                foreach (DgnEntry entry in Read.DoOffsets<DgnEntry>(bytes, header.Offset + 4, entryCount))
                {
                    entries.Add(new DgnFileEntry(
                        entry.Field0,
                        entry.Field2,
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset1, entry.Count1),
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset2, entry.Count2),
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset3, entry.Count3),
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset4, entry.Count4)
                    ));
                }
                files.Add(new DgnFile(names[i], header, entries));
            }
            return files;
        }

        public static void Nop() { }
    }

    // size: 12
    public readonly struct SoundSampleHeader
    {
        public readonly byte Format;
        public readonly byte LoopFlag; // boolean
        public readonly ushort SampleRate;
        public readonly ushort Timer; // SND_TIMER_CLOCK / SampleRate
        public readonly ushort LoopStart; // number of 32-bit words
        public readonly uint LoopLength; // number of 32-bit words
    }

    public class SoundSample
    {
        public uint Id { get; }
        public uint Offset { get; }
        public WaveFormat Format { get; }
        public bool Loop { get; }
        public ushort SampleRate { get; }
        public ushort Timer { get; }
        public ushort LoopStart { get; }
        public uint LoopLength { get; }

        private readonly byte[] _data;
        public IReadOnlyList<byte> Data => _data;

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
            Timer = header.Timer;
            LoopStart = header.LoopStart;
            LoopLength = header.LoopLength;
            _data = data.ToArray();
        }

        private SoundSample(uint id)
        {
            Id = id;
            Format = WaveFormat.None;
            _data = new byte[0];
        }

        public static SoundSample CreateNull(uint id)
        {
            return new SoundSample(id);
        }

        public ReadOnlySpan<byte> CreateSpan()
        {
            return new ReadOnlySpan<byte>(_data);
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
        public readonly uint Field0;
        public readonly uint Field4;
    }

    // size: 12
    public readonly struct RawSoundTableEntry
    {
        // sktodo: SFX fields
        public readonly ushort Field0;
        public readonly byte CategoryId;
        public readonly byte Field3;
        public readonly byte Field4;
        public readonly byte Field5;
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
        public ushort Field0;
        public byte CategoryId;
        public byte Field3;
        public byte Field4;
        public byte Field5;
        public ushort Size;
        public uint Data;

        public SoundTableEntry(string name, string category, RawSoundTableEntry raw)
        {
            Name = name;
            Category = category;
            Field0 = raw.Field0;
            CategoryId = raw.CategoryId;
            Field3 = raw.Field3;
            Field4 = raw.Field4;
            Field5 = raw.Field5;
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
        public readonly byte Field6;
        public readonly byte Field7;
    }

    // size: 12
    public readonly struct SfxScriptEntry
    {
        public readonly ushort SfxId;
        public readonly ushort Delay;
        public readonly byte Param1; // todo: these are passed to play_sfx_2; what are they?
        public readonly byte Param2;
        public readonly ushort Param3;
        public readonly uint Handle; // set at runtime
    }

    public class SfxScriptFile
    {
        public string Name { get; }
        public SfxScriptHeader Header { get; }
        public IReadOnlyList<SfxScriptEntry> Entries { get; }

        public SfxScriptFile(string name, SfxScriptHeader header, IReadOnlyList<SfxScriptEntry> entries)
        {
            Name = name;
            Header = header;
            Entries = entries;
        }
    }

    // size: 8
    public readonly struct DgnHeader
    {
        public readonly uint Offset;
        public readonly ushort Size;
        public readonly byte Field6;
        public readonly byte Field7;
    }

    // size: 36
    public readonly struct DgnEntry
    {
        public readonly ushort Field0;
        public readonly ushort Field2;
        public readonly uint Count1;
        public readonly uint Offset1;
        public readonly uint Count2;
        public readonly uint Offset2;
        public readonly uint Count3;
        public readonly uint Offset3;
        public readonly uint Count4;
        public readonly uint Offset4;
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
        public ushort Field0 { get; }
        public ushort Field2 { get; }
        public IReadOnlyList<uint> Data1 { get; }
        public IReadOnlyList<uint> Data2 { get; }
        public IReadOnlyList<uint> Data3 { get; }
        public IReadOnlyList<uint> Data4 { get; }

        public DgnFileEntry(ushort field0, ushort field2, IReadOnlyList<uint> data1,
            IReadOnlyList<uint> data2, IReadOnlyList<uint> data3, IReadOnlyList<uint> data4)
        {
            Field0 = field0;
            Field2 = field2;
            Data1 = data1;
            Data2 = data2;
            Data3 = data3;
            Data4 = data4;
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

    public readonly struct SymbolBlockHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // SYMB - no terminator
        public readonly uint HeaderSize;
        public readonly uint SeqOffset; // relative offsets
        public readonly uint SeqarcOffset;
        public readonly uint BankOffset;
        public readonly uint WavearcOffset;
        public readonly uint PlayerOffset;
        public readonly uint GroupOffset;
        public readonly uint Player2Offset;
        public readonly uint StrmOffset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public readonly byte[] Reserved;
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
