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
            string newPath;
            if (isFh)
            {
                newPath = Path.GetFullPath(Paths.Combine("files", rootName, "data"));
            }
            else
            {
                newPath = Path.GetFullPath(Paths.Combine("files", rootName));
            }
            Paths.SetPath(rootName, newPath);
            var lines = new List<string>();
            lines.Add(Program.Version.ToString());
            lines.Add($"{Ver.AMFE0}={Paths.AllPaths[Ver.AMFE0]}");
            lines.Add($"{Ver.AMFP0}={Paths.AllPaths[Ver.AMFP0]}");
            lines.Add($"{Ver.A76E0}={Paths.AllPaths[Ver.A76E0]}");
            lines.Add($"{Ver.AMHE0}={Paths.AllPaths[Ver.AMHE0]}");
            lines.Add($"{Ver.AMHE1}={Paths.AllPaths[Ver.AMHE1]}");
            lines.Add($"{Ver.AMHP0}={Paths.AllPaths[Ver.AMHP0]}");
            lines.Add($"{Ver.AMHP1}={Paths.AllPaths[Ver.AMHP1]}");
            lines.Add($"{Ver.AMHJ0}={Paths.AllPaths[Ver.AMHJ0]}");
            lines.Add($"{Ver.AMHJ1}={Paths.AllPaths[Ver.AMHJ1]}");
            lines.Add($"{Ver.AMHK0}={Paths.AllPaths[Ver.AMHK0]}");
            lines.Add($"Export={Paths.AllPaths["Export"]}");
            File.WriteAllText("paths.txt", String.Join(Environment.NewLine, lines));
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
            public RomDataValues FontModel { get; set; } = null!;
            public RomDataValues FontWidths { get; set; } = null!;
            public RomDataValues FontOffsets { get; set; } = null!;
            public RomDataValues FontCharData { get; set; } = null!;
            public RomDataValues TerrianSfx { get; set; } = null!;
            public RomDataValues BeamSfx { get; set; } = null!;
            public RomDataValues HunterSfx { get; set; } = null!;
            public RomDataValues EnemyDamageSfx { get; set; } = null!;
            public RomDataValues EnemyDeathSfx { get; set; } = null!;
            public RomDataValues PlatformSfx { get; set; } = null!;
        }

        private static void ExtractRomData(string rootName)
        {
            if (!_romData.TryGetValue(rootName, out RomData? data))
            {
                return;
            }
            byte[] bytes = File.ReadAllBytes(Paths.Combine("files", rootName, "_bin", data.FontModel.File));
            File.WriteAllBytes(Paths.Combine("files", rootName, @"models\hudfont_Model.bin"),
                bytes[data.FontModel.Offset..(data.FontModel.Offset + data.FontModel.Size)]);
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
                    File.WriteAllBytes(Paths.Combine(path, file.Name), bytes[start..end]);
                }
                foreach (DirInfo subdir in dir.Subdirectories)
                {
                    WriteFiles(subdir, Paths.Combine(path, subdir.Name));
                }
            }
            var root = new DirInfo(rootName, index: 0);
            PopulateDir(root);
            WriteFiles(root, Paths.Combine("files", root.Name));
            if (hasArchives)
            {
                foreach (string path in Directory.EnumerateFiles(Paths.Combine("files", root.Name, "archives")))
                {
                    if (Path.GetExtension(path).ToLower() == ".arc")
                    {
                        Read.ExtractArchive(path);
                    }
                }
            }
            string ftcDir = Paths.Combine("files", root.Name, "ftc");
            Directory.CreateDirectory(ftcDir);
            byte[] WriteFile(string name, int offset, int size)
            {
                byte[] fileBytes = bytes[offset..(offset + size)];
                File.WriteAllBytes(Paths.Combine(ftcDir, name), fileBytes);
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
                File.WriteAllBytes(Paths.Combine(ftcDir, $"overlay9_{overlayId}"), bytes[overlayStart..overlayEnd]);
            }
            string ftcDest = Paths.Combine("files", root.Name, "_bin");
            Directory.CreateDirectory(ftcDest);
            foreach (string path in Directory.EnumerateFiles(ftcDir))
            {
                string filename = Path.GetFileName(path);
                if (filename == "arm9.bin" || filename.StartsWith("overlay9_"))
                {
                    Console.WriteLine($"Decompressing {filename}...");
                    LZBackward.Decompress(path, Paths.Combine(ftcDest, filename));
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

        public static void LoadRuntimeData()
        {
            if (!_romData.TryGetValue(Paths.MphKey, out RomData? data))
            {
                return;
            }
            // arm9.bin
            byte[] bytes = File.ReadAllBytes(Paths.Combine(Paths.FileSystem, "_bin", data.FontWidths.File));
            byte[] widths = bytes[data.FontWidths.Offset..(data.FontWidths.Offset + data.FontWidths.Size)];
            byte[] offsets = bytes[data.FontOffsets.Offset..(data.FontOffsets.Offset + data.FontOffsets.Size)];
            byte[] chars = bytes[data.FontCharData.Offset..(data.FontCharData.Offset + data.FontCharData.Size)];
            byte[] enemyDamageSfx = bytes[data.EnemyDamageSfx.Offset..(data.EnemyDamageSfx.Offset + data.EnemyDamageSfx.Size)];
            byte[] enemyDeathSfx = bytes[data.EnemyDeathSfx.Offset..(data.EnemyDeathSfx.Offset + data.EnemyDeathSfx.Size)];
            Text.Font.SetData(widths, offsets, chars);
            // overlay9_2
            bytes = File.ReadAllBytes(Paths.Combine(Paths.FileSystem, "_bin", data.BeamSfx.File));
            byte[] terrainSfx = bytes[data.TerrianSfx.Offset..(data.TerrianSfx.Offset + data.TerrianSfx.Size)];
            byte[] beamSfx = bytes[data.BeamSfx.Offset..(data.BeamSfx.Offset + data.BeamSfx.Size)];
            byte[] hunterSfx = bytes[data.HunterSfx.Offset..(data.HunterSfx.Offset + data.HunterSfx.Size)];
            Metadata.SetTerrainSfxData(terrainSfx);
            Metadata.SetBeamSfxData(beamSfx);
            Metadata.SetHunterSfxData(hunterSfx);
            Metadata.SetEnemyDamageSfxData(enemyDamageSfx);
            Metadata.SetEnemyDeathSfxData(enemyDeathSfx);
            // overlay9_15 (or overlay9_12 for A76E0)
            bytes = File.ReadAllBytes(Paths.Combine(Paths.FileSystem, "_bin", data.PlatformSfx.File));
            byte[] platformSfx = bytes[data.PlatformSfx.Offset..(data.PlatformSfx.Offset + data.PlatformSfx.Size)];
            Metadata.SetPlatformSfxData(platformSfx);
        }

        private static readonly IReadOnlyDictionary<string, RomData> _romData = new Dictionary<string, RomData>()
        {
            {
                Ver.A76E0,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0x9D528, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0x95C68, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0x95A88, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0x96348, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1D828, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1D8B8, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1D96C, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0x9B574, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0x9B644, 208),
                    PlatformSfx = new RomDataValues("overlay9_12", 0x81E4, 360)
                }
            },
            {
                Ver.AMHE0,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC76D4, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xBF9B0, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xBFB90, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xC0270, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1DA08, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1DA98, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1DB4C, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xC54A8, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xC5578, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x8284, 360)
                }
            },
            {
                Ver.AMHE1,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC7F5C, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xC020C, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xC03EC, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xC0ACC, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1DA68, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1DAF8, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1DBAC, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xC5D30, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xC5E00, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x8284, 360)
                }
            },
            {
                Ver.AMHJ0,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC9510, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xC1754, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xC1934, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xC2014, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1DA68, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1DAF8, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1DBAC, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xC7278, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xC7348, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x8284, 360)
                }
            },
            {
                Ver.AMHJ1,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC94D0, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xC1714, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xC18F4, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xC1FD4, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1DA68, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1DAF8, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1DBAC, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xC7238, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xC7308, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x8284, 360)
                }
            },
            {
                Ver.AMHP0,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC7F7C, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xC022C, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xC040C, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xC0AEC, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1DA08, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1DA98, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1DB4C, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xC5D50, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xC5E20, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x8284, 360)
                }
            },
            {
                Ver.AMHP1,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC7FFC, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xC02AC, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xC048C, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xC0B6C, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1DA68, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1DAF8, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1DBAC, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xC5DD0, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xC5EA0, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x8284, 360)
                }
            },
            {
                Ver.AMHK0,
                new RomData()
                {
                    FontModel = new RomDataValues("arm9.bin", 0xC0D40, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0xBD580, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0xBD760, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0xB9560, 0x4000),
                    TerrianSfx = new RomDataValues("overlay9_2", 0x1BDBA, 144),
                    BeamSfx = new RomDataValues("overlay9_2", 0x1BE4A, 180),
                    HunterSfx = new RomDataValues("overlay9_2", 0x1BEFE, 272),
                    EnemyDamageSfx = new RomDataValues("arm9.bin", 0xBE4DC, 208),
                    EnemyDeathSfx = new RomDataValues("arm9.bin", 0xBE5AC, 208),
                    PlatformSfx = new RomDataValues("overlay9_15", 0x7CC0, 360)
                }
            },
            {
                Ver.NTRJ0,
                new RomData()
                {
                    // todo: values
                    FontModel = new RomDataValues("arm9.bin", 0xED610, 0x8284),
                    FontWidths = new RomDataValues("arm9.bin", 0x1FC07C, 480),
                    FontOffsets = new RomDataValues("arm9.bin", 0x1FC25C, 480),
                    FontCharData = new RomDataValues("arm9.bin", 0x1FC93C, 0x4000)
                }
            }
        };
    }
}
