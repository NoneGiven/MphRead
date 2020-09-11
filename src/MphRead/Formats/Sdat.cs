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
        public static IReadOnlyList<DngFile> ReadDng()
        {
            string path = Path.Combine(Paths.FileSystem, "data", "sound", "DGNFILES.DAT");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            var files = new List<DngFile>();
            uint fileCount = Read.SpanReadUint(bytes, 0);
            Debug.Assert(fileCount > 0);
            IReadOnlyList<DngHeader> headers = Read.DoOffsets<DngHeader>(bytes, 4, fileCount);
            IReadOnlyList<string> names = Read.ReadStrings(bytes, headers.Last().Offset + headers.Last().Size, fileCount);
            for (int i = 0; i < headers.Count; i++)
            {
                DngHeader header = headers[i];
                var entries = new List<DngFileEntry>();
                uint entryCount = Read.SpanReadUint(bytes, header.Offset);
                foreach (DngEntry entry in Read.DoOffsets<DngEntry>(bytes, header.Offset + 4, entryCount))
                {
                    entries.Add(new DngFileEntry(
                        entry.Field0,
                        entry.Field2,
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset1, entry.Count1),
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset2, entry.Count2),
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset3, entry.Count3),
                        Read.DoOffsets<uint>(bytes, header.Offset + entry.Offset4, entry.Count4)
                    ));
                }
                files.Add(new DngFile(names[i], header, entries));
            }
            return files;
        }
    }
    
    // size: 8
    public readonly struct DngHeader
    {
        public readonly uint Offset;
        public readonly ushort Size;
        public readonly byte Field6;
        public readonly byte Field7;
    }

    // size: 36
    public readonly struct DngEntry
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

    public class DngFile
    {
        public string Name { get; }
        public DngHeader Header { get; }
        public IReadOnlyList<DngFileEntry> Entries { get; }

        public DngFile(string name, DngHeader header, IReadOnlyList<DngFileEntry> entries)
        {
            Name = name;
            Header = header;
            Entries = entries;
        }
    }

    public class DngFileEntry
    {
        public ushort Field0 { get; }
        public ushort Field2 { get; }
        public IReadOnlyList<uint> Data1 { get; }
        public IReadOnlyList<uint> Data2 { get; }
        public IReadOnlyList<uint> Data3 { get; }
        public IReadOnlyList<uint> Data4 { get; }

        public DngFileEntry(ushort field0, ushort field2, IReadOnlyList<uint> data1,
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
