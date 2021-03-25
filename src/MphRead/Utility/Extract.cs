using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MphRead
{
    public static class Extract
    {
        public static void Setup()
        {
            //string mphPath = GetString("Enter the file path to your Metroid Prime Hunters ROM:", optional: false, checkPath: true);
            //string fhPath = GetString("Enter the file path to your First Hunt ROM (optional):", optional: true, checkPath: true);
            string mphPath = @"D:\Cdrv\MPH\Roms\AMHE_00_7fe4554a.nds";
            string fhPath = @"D:\Cdrv\MPH\Roms\AMFE_00_c2fb5233.nds";
            fhPath = "";
            byte[] mphBytes = File.ReadAllBytes(mphPath);
            Console.WriteLine("Reading file...");
            RomHeader mphHeader = Read.ReadStruct<RomHeader>(mphBytes);
            var mphCodes = new List<string>() { "AMHE", "AMHP", "AMHJ", "AMHK", "A76E" };
            if (!mphCodes.Contains(mphHeader.GameCode.MarshalString()))
            {
                PrintExit("The file does not appear to be an MPH ROM. Please try again.");
                return;
            }
            byte[] fhBytes = default!;
            RomHeader fhHeader = default;
            if (!String.IsNullOrWhiteSpace(fhPath))
            {
                Console.WriteLine("Reading file...");
                fhBytes = File.ReadAllBytes(fhPath);
                fhHeader = Read.ReadStruct<RomHeader>(mphBytes);
                var fhCodes = new List<string>() { "AMFE", "AMFP" };
                if (!fhCodes.Contains(fhHeader.GameCode.MarshalString()))
                {
                    PrintExit("The file does not appear to be an FH ROM. Please try again.");
                    return;
                }
            }
            ExtractRomFs(mphHeader, mphBytes);
            if (!String.IsNullOrWhiteSpace(fhPath))
            {
                ExtractRomFs(fhHeader, fhBytes);
            }
            Nop();
        }

        private static void ExtractRomFs(RomHeader header, byte[] bytes)
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
            var root = new DirInfo("root", index: 0);
            PopulateDir(root);
            WriteFiles(root, root.Name);
            foreach (string path in Directory.EnumerateFiles(Path.Combine(root.Name, "archives")))
            {
                if (Path.GetExtension(path).ToLower() == ".arc")
                {
                    Read.ExtractArchive(path);
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

        private static string GetString(string prompt, bool optional, bool checkPath)
        {
            string input;
            while (true)
            {
                Console.WriteLine(prompt);
                input = Console.ReadLine();
                if (!String.IsNullOrWhiteSpace(input))
                {
                    if (checkPath)
                    {
                        if (File.Exists(input))
                        {
                            break;
                        }
                        Console.WriteLine("Could not find the file.");
                    }
                }
                else if (optional)
                {
                    break;
                }
                Console.WriteLine();
            }
            Console.WriteLine();
            return input;
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

        public readonly struct RomHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public readonly char[] Title;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public readonly char[] GameCode;
            public readonly int Fields10;
            public readonly int Fields14;
            public readonly int Fields18;
            public readonly int Fields1C;
            public readonly int Fields20;
            public readonly int Fields24;
            public readonly int Fields28;
            public readonly int Fields2C;
            public readonly int Fields30;
            public readonly int Fields34;
            public readonly int Fields38;
            public readonly int Fields3C;
            public readonly uint FntOffset;
            public readonly uint FntSize;
            public readonly uint FatOffset;
            public readonly uint FatSize;
        }
    }
}
