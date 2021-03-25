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
            var mphCodes = new List<string>() { "AMHE", "AMHP", "AMHJ", "AMHK", "A76E" };
            bool isFh = false;
            if (!mphCodes.Contains(header.GameCode.MarshalString()))
            {
                var fhCodes = new List<string>() { "AMFE", "AMFP" };
                isFh = fhCodes.Contains(header.GameCode.MarshalString());
                if (!isFh)
                {
                    PrintExit("The specified file does not appear to be a valid ROM.");
                    return;
                }
            }
            if (File.Exists("paths.txt"))
            {
                if ((!isFh && !String.IsNullOrWhiteSpace(Paths.FileSystem)) || (isFh && !String.IsNullOrWhiteSpace(Paths.FhFileSystem)))
                {
                    Console.Write($"A path has already been specified for {(isFh ? "FH" : "MPH")} files. Do you want to update it? (y/n) ");
                    string input = Console.ReadLine().Trim().ToLower();
                    if (input != "y" && input != "yes")
                    {
                        return;
                    }
                }
            }
            ExtractRomFs(header, bytes, rootName: isFh ? "fh" : "mph");
            if (isFh)
            {
                File.WriteAllText("paths.txt", String.Join(Environment.NewLine, Paths.FileSystem, Path.Combine("files", "fh"), Paths.Export));
            }
            else
            {
                File.WriteAllText("paths.txt", String.Join(Environment.NewLine, Path.Combine("files", "mph"), Paths.FhFileSystem, Paths.Export));
            }
            Nop();
        }

        private static void ExtractRomFs(RomHeader header, byte[] bytes, string rootName)
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
            foreach (string path in Directory.EnumerateFiles(Path.Combine("files", root.Name, "archives")))
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
