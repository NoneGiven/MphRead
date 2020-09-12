using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead.Formats.Sound
{
    public static class SoundRead
    {
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
        public readonly uint Handle;
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
