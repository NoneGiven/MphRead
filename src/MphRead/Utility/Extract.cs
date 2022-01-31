using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MphRead
{
    public static class Extract
    {
        public static void Setup(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            RomHeader header = Read.ReadStruct<RomHeader>(bytes);
            var mphCodes = new Dictionary<string, List<byte>>()
            {
                { "AMHE", new List<byte>() { 0, 1 } },
                { "AMHP", new List<byte>() { 0, 1 } },
                { "AMHJ", new List<byte>() { 0, 1 } },
                { "AMHK", new List<byte>() { 0 } },
                { "A76E", new List<byte>() { 0 } }
            };
            bool isFh = false;
            string gameCode = header.GameCode.MarshalString();
            if (!mphCodes.TryGetValue(gameCode, out List<byte>? mphVersions))
            {
                var fhCodes = new Dictionary<string, List<byte>>()
                {
                    { "AMFE", new List<byte>() { 0 } },
                    { "AMFP", new List<byte>() { 0 } }
                };
                if (!fhCodes.TryGetValue(gameCode, out List<byte>? fhVersions))
                {
                    PrintExit($"The specified ROM file has invalid game code {gameCode}.");
                    return;
                }
                if (!fhVersions.Contains(header.Version))
                {
                    PrintExit($"The specified {gameCode} ROM has unexpected version {header.Version}.");
                    return;
                }
                isFh = true;
            }
            else if (!mphVersions.Contains(header.Version))
            {
                PrintExit($"The specified {gameCode} ROM has unexpected version {header.Version}.");
                return;
            }
            if (File.Exists("paths.txt"))
            {
                if ((!isFh && !String.IsNullOrWhiteSpace(Paths.FileSystem))
                    || (isFh && !String.IsNullOrWhiteSpace(Paths.FhFileSystem)))
                {
                    Console.Write($"A path has already been specified for {(isFh ? "FH" : "MPH")} files. " +
                        $"Do you want to update it? (y/n) ");
                    string input = (Console.ReadLine() ?? "").Trim().ToLower();
                    if (input != "y" && input != "yes")
                    {
                        return;
                    }
                }
            }
            string rootName = $"{header.GameCode.MarshalString()}{header.Version}";
            ExtractRomFs(header, bytes, rootName, hasArchives: !isFh);
            ExtractRomData(rootName);
            if (isFh)
            {
                File.WriteAllText("paths.txt", String.Join(Environment.NewLine,
                    Paths.FileSystem,
                    Path.GetFullPath(Path.Combine("files", rootName, "data")),
                    Paths.Export));
            }
            else
            {
                File.WriteAllText("paths.txt", String.Join(Environment.NewLine,
                    Path.GetFullPath(Path.Combine("files", rootName)),
                    Paths.FhFileSystem,
                    Paths.Export));
            }
            Nop();
        }

        private class RomDataValues
        {
            public string File { get; set; }
            public int Offset { get; set; }
            public int Size { get; set; }

            public RomDataValues(string file, int offset, int size)
            {
                File = file;
                Offset = offset;
                Size = size;
            }
        }

        private class RomData
        {
            public RomDataValues HudFontModel { get; set; } = null!;
        }

        private static void ExtractRomData(string rootName)
        {
            if (!_romData.TryGetValue(rootName, out RomData? data))
            {
                return;
            }
            byte[] bytes = File.ReadAllBytes(Path.Combine("files", rootName, "_bin", data.HudFontModel.File));
            File.WriteAllBytes(Path.Combine("files", rootName, @"models\hudfont_Model.bin"),
                bytes[data.HudFontModel.Offset..(data.HudFontModel.Offset + data.HudFontModel.Size)]);
        }

        private static void ExtractRomFs(RomHeader header, byte[] bytes, string rootName, bool hasArchives)
        {
            Debug.Assert(header.FntOffset > 0 && header.FatSize > 0);
            Debug.Assert(header.FatOffset > 0 && header.FatSize > 0 && header.FatSize % 8 == 0);
            DirTableEntry dirStart = Read.DoOffset<DirTableEntry>(bytes, header.FntOffset);
            IReadOnlyList<DirTableEntry> entries = Read.DoOffsets<DirTableEntry>(bytes, header.FntOffset, dirStart.DirNum);
            var fileOffsets = new List<(int, int)>();
            IReadOnlyList<uint> addresses = Read.DoOffsets<uint>(bytes, header.FatOffset, header.FatSize / 4);
            for (int i = 0; i < addresses.Count; i += 2)
            {
                fileOffsets.Add(((int)addresses[i], (int)addresses[i + 1]));
            }
            void PopulateDir(DirInfo dir)
            {
                DirTableEntry entry = entries[(int)dir.Index];
                uint offset = header.FntOffset + entry.Offset;
                ushort fileIndex = entry.FirstFileIndex;
                byte type = 1;
                while (type != 0)
                {
                    type = bytes[offset];
                    offset++;
                    if (type >= 1 && type <= 127)
                    {
                        int length = type;
                        string name = Read.ReadString(bytes, offset, length);
                        offset += (uint)length;
                        dir.Files.Add(new FileInfo(name, fileIndex++));
                    }
                    else if (type >= 129 && type <= 255)
                    {
                        int length = type - 128;
                        string name = Read.ReadString(bytes, offset, length);
                        offset += (uint)length;
                        ushort id = Read.SpanReadUshort(bytes, offset);
                        offset += sizeof(ushort);
                        dir.Subdirectories.Add(new DirInfo(name, id - 0xF000u));
                    }
                }
                foreach (DirInfo subdir in dir.Subdirectories)
                {
                    PopulateDir(subdir);
                }
            }
            void WriteFiles(DirInfo dir, string path)
            {
                Console.WriteLine($"Writing {path}...");
                Directory.CreateDirectory(path);
                foreach (FileInfo file in dir.Files)
                {
                    (int start, int end) = fileOffsets[(int)file.Index];
                    Debug.Assert(start > 0 && end > start);
                    File.WriteAllBytes(Path.Combine(path, file.Name), bytes[start..end]);
                }
                foreach (DirInfo subdir in dir.Subdirectories)
                {
                    WriteFiles(subdir, Path.Combine(path, subdir.Name));
                }
            }
            var root = new DirInfo(rootName, index: 0);
            PopulateDir(root);
            WriteFiles(root, Path.Combine("files", root.Name));
            if (hasArchives)
            {
                foreach (string path in Directory.EnumerateFiles(Path.Combine("files", root.Name, "archives")))
                {
                    if (Path.GetExtension(path).ToLower() == ".arc")
                    {
                        Read.ExtractArchive(path);
                    }
                }
            }
            string ftcDir = Path.Combine("files", root.Name, "ftc");
            Directory.CreateDirectory(ftcDir);
            byte[] WriteFile(string name, int offset, int size)
            {
                byte[] fileBytes = bytes[offset..(offset + size)];
                File.WriteAllBytes(Path.Combine(ftcDir, name), fileBytes);
                return fileBytes;
            }
            WriteFile("arm9.bin", header.ARM9Offset, header.ARM9Size);
            WriteFile("arm7.bin", header.ARM7Offset, header.ARM7Size);
            WriteFile("fat.bin", (int)header.FatOffset, (int)header.FatSize);
            WriteFile("fnt.bin", (int)header.FntOffset, (int)header.FntSize);
            WriteFile("banner.bin", header.BannerOffset, 0x840);
            byte[] overlayInfo = WriteFile("y9.bin", header.Overlay9Offset, header.Overlay9Size);
            Debug.Assert(overlayInfo.Length % 32 == 0);
            for (int i = 0; i < overlayInfo.Length / 32; i++)
            {
                var items = new List<int>();
                for (int j = 0; j < 8; j++)
                {
                    int start = i * 32 + j * 4;
                    byte[] value = overlayInfo[start..(start + 4)];
                    items.Add(BitConverter.ToInt32(value));
                }
                int overlayId = items[0];
                int fileId = items[6];
                (int overlayStart, int overlayEnd) = fileOffsets[fileId];
                Debug.Assert(overlayStart > 0 && overlayEnd > overlayStart);
                File.WriteAllBytes(Path.Combine(ftcDir, $"overlay9_{overlayId}"), bytes[overlayStart..overlayEnd]);
            }
            string ftcDest = Path.Combine("files", root.Name, "_bin");
            Directory.CreateDirectory(ftcDest);
            foreach (string path in Directory.EnumerateFiles(ftcDir))
            {
                string filename = Path.GetFileName(path);
                if (filename == "arm9.bin" || filename.StartsWith("overlay9_"))
                {
                    Console.WriteLine($"Decompressing {filename}...");
                    LZBackward.Decompress(path, Path.Combine(ftcDest, filename));
                }
            }
            Nop();
        }

        private static void PrintExit(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void Nop()
        {
        }

        public class DirInfo
        {
            public string Name { get; }
            public uint Index { get; }
            public List<DirInfo> Subdirectories { get; set; } = new List<DirInfo>();
            public List<FileInfo> Files { get; set; } = new List<FileInfo>();

            public DirInfo(string name, uint index)
            {
                Name = name;
                Index = index;
            }
        }

        public class FileInfo
        {
            public string Name { get; }
            public uint Index { get; }

            public FileInfo(string name, uint index)
            {
                Name = name;
                Index = index;
            }
        }

        public readonly struct DirTableEntry
        {
            public readonly uint Offset;
            public readonly ushort FirstFileIndex;
            public readonly ushort DirNum;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public readonly struct RomHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public readonly char[] Title;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public readonly char[] GameCode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public readonly char[] MakerCode;
            public readonly byte UnitCode;
            public readonly byte Seed;
            public readonly byte Capacity;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
            public readonly char[] Reserved15;
            public readonly byte Reserved16;
            public readonly byte Region;
            public readonly byte Version;
            public readonly byte AutoStart;
            public readonly int ARM9Offset;
            public readonly int ARM9EntryAddress;
            public readonly int ARM9RamAddress;
            public readonly int ARM9Size;
            public readonly int ARM7Offset;
            public readonly int ARM7EntryAddress;
            public readonly int ARM7RamAddress;
            public readonly int ARM7Size;
            public readonly uint FntOffset;
            public readonly uint FntSize;
            public readonly uint FatOffset;
            public readonly uint FatSize;
            public readonly int Overlay9Offset;
            public readonly int Overlay9Size;
            public readonly int Overlay7Offset;
            public readonly int Overlay7Size;
            public readonly uint ReadFlags;
            public readonly uint InitFlags;
            public readonly int BannerOffset;
        }

        private static readonly IReadOnlyDictionary<string, RomData> _romData = new Dictionary<string, RomData>()
        {
            {
                "A76E0",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0x9D528, 0x8284)
                }
            },
            {
                "AMHE0",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC76D4, 0x8284)
                }
            },
            {
                "AMHE1",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC7F5C, 0x8284)
                }
            },
            {
                "AMHJ0",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC9510, 0x8284)
                }
            },
            {
                "AMHJ1",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC94D0, 0x8284)
                }
            },
            {
                "AMHP0",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC7F7C, 0x8284)
                }
            },
            {
                "AMHP1",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC7FFC, 0x8284)
                }
            },
            {
                "AMHK0",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xC0D40, 0x8284)
                }
            },
            {
                "NTRJ0",
                new RomData()
                {
                    HudFontModel = new RomDataValues("arm9.bin", 0xED610, 0x8284)
                }
            }
        };
    }
}
