using System.Runtime.InteropServices;

namespace MphRead.Formats
{
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
