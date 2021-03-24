using System;
using System.Collections.Generic;
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
            byte[] mphBytes = File.ReadAllBytes(mphPath);
            RomHeader mphHeader = Read.ReadStruct<RomHeader>(mphBytes);
            var mphCodes = new List<string>() { "AMHE", "AMHP", "AMHJ", "AMHK", "A76E" };
            if (!mphCodes.Contains(mphHeader.GameCode.MarshalString()))
            {
                PrintExit("The file does not appear to be an MPH ROM. Please try again.");
                return;
            }
            ExtractRomFs(mphHeader, mphBytes);
            if (!String.IsNullOrWhiteSpace(fhPath))
            {
                byte[] fhBytes = File.ReadAllBytes(fhPath);
                RomHeader fhHeader = Read.ReadStruct<RomHeader>(mphBytes);
                var fhCodes = new List<string>() { "AMFE", "AMFP" };
                if (!fhCodes.Contains(fhHeader.GameCode.MarshalString()))
                {
                    PrintExit("The file does not appear to be an FH ROM. Please try again.");
                    return;
                }
                ExtractRomFs(fhHeader, fhBytes);
            }
            Nop();
        }

        private static void ExtractRomFs(RomHeader header, byte[] bytes)
        {
            DirTableEntry dirStart = Read.DoOffset<DirTableEntry>(bytes, header.FntOffset);
            IReadOnlyList<DirTableEntry> entries = Read.DoOffsets<DirTableEntry>(bytes, header.FntOffset, dirStart.DirNum);

            void PopulateDir(DirInfo dir)
            {
                DirTableEntry entry = entries[(int)dir.Index];
                uint offset = header.FntOffset + entry.Offset;
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
                        dir.Files.Add(name);
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
            var dirInfo = new DirInfo("root", index: 0);
            PopulateDir(dirInfo);
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
            public List<string> Files { get; set; } = new List<string>();

            public DirInfo(string name, uint index)
            {
                Name = name;
                Index = index;
            }
        }

        public readonly struct DirTableEntry
        {
            public readonly uint Offset;
            public readonly ushort FirstFileId;
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
