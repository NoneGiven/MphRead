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
            return Read.DoOffsets<MusicTrack>(bytes, 4, count);
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
    public readonly struct MusicTrack
    {
        public readonly ushort SeqId;
        public readonly ushort Field2;
        public readonly ushort Field4;
        public readonly ushort Field6;
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
}
