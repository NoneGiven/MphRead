using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MphRead.Formats
{
    // size: 20
    public readonly struct FrontendHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // MARM
        public readonly ushort Field4;
        public readonly byte Field6;
        public readonly byte Field7;
        public readonly uint Offfset1; // null-terminated list of MenuStruct1*
        public readonly uint Offfset2; // ntl of MenuStruct2*
        public readonly uint Offfset3;
    }

    // size: 32
    public readonly struct MenuStruct1
    {
        // 021E87F8 - offset 4 in use
        public readonly uint Field0;
        public readonly uint Offset1; // unused in metroidhunters.bin
        public readonly uint Offset2; // ntl of MenuStruct1A*
        public readonly uint Offset3; // ntl of MenuStruct1A1*
        public readonly uint Offset4; // ntl of MenuStruct1B*
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly byte Field1C;
        public readonly byte Field1D;
        public readonly byte Field1E;
        public readonly byte Field1F;
    }

    // size: 60
    public readonly struct MenuStruct1A
    {
        public readonly uint Offset1; // unused in metroidhunters.bin
        public readonly uint Offset2; // ntl of MenuStruct1A1*
        public readonly uint Offset3; // ntl of pointers to single(?) ints
        public readonly uint Offset4; // ntl of MenuStruct1A2*
        public readonly uint Field10; // (?) runtime single pointer to a MenuStruct1A2?
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C; // (?) runtime single pointer, maybe?
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Offset5; // pointer to list of ints -- only set for one MenuStruct1A in metroidhunters.bin (22140BC)
        public readonly uint Offset6; // pointer to list of ints, can be external
        public readonly byte Field34;
        public readonly byte Field35;
        public readonly ushort Field36;
        public readonly byte Flags;
        public readonly byte Field39;
        public readonly ushort Field3A;
    }

    // size: 24
    public readonly struct MenuStruct1A1
    {
        public readonly uint Field0;
        public readonly uint Field4;
        public readonly uint Field8;
        public readonly uint FieldC;
        public readonly uint Offset1; // ntl of pointers to int pairs
        public readonly uint Field14;
    }

    // size: 8
    public readonly struct MenuStruct1A2
    {
        public readonly byte Field0; // if 1, Offset1 is converted to MenuStruct1A3*
        public readonly byte Field1;
        public readonly ushort Field2;
        public readonly uint Offset1;
    }

    // size: 4
    public readonly struct MenuStruct1A3
    {
        public readonly uint Field0;
        public readonly uint Offset1; // pointer to list of ints, can be external
    }

    // size: 32
    public readonly struct MenuStruct1B
    {
        public readonly uint Field0;
        public readonly MenuStruct1A1 Struct1A1;
        public readonly uint Field1C;
    }

    // size: 12
    public readonly struct MenuStruct2
    {
        public readonly uint Field0;
        public readonly uint Field4;
        public readonly uint Offset1; // ntl of MenuStruct2A*
    }

    // size: 12
    public readonly struct MenuStruct2A
    {
        public readonly byte Field0;
        public readonly byte FilenameLength; // includes terminator and 0xBB padding to multiple of 4 bytes
        public readonly ushort Field2;
        public readonly uint Field4;
        public readonly uint FilenameOffset; // char*
    }

    public static class Frontend
    {
        public static void Parse()
        {
            // todo: parse DP version (different header)
            //string path = @"D:\Cdrv\MPH\_FS\amhe1\frontend\single_metroidhunters.bin";
            string path = @"D:\Cdrv\MPH\_FS\amhe1\frontend\metroidhunters.bin";
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            FrontendHeader header = Read.ReadStruct<FrontendHeader>(bytes);
            Debug.Assert(header.Type.MarshalString() == "MARM");
            var list1 = new List<MenuStruct1>();
            foreach (uint offset in Read.DoListNullEnd(bytes, header.Offfset1))
            {
                MenuStruct1 item = Read.DoOffset<MenuStruct1>(bytes, offset);
                Debug.Assert(item.Offset1 == 0);
                list1.Add(item);
                foreach (uint subOffset in Read.DoListNullEnd(bytes, item.Offset2))
                {
                    Console.WriteLine(subOffset);
                    MenuStruct1A subItem = Read.DoOffset<MenuStruct1A>(bytes, subOffset);
                    Debug.Assert(subItem.Offset1 == 0);
                }
            }
            Nop();
        }

        private static void Nop()
        {
        }
    }
}
