using System;

namespace MphRead.Entities
{
    public class AiData
    {
        // todo: all field names (and also implementations)
        public AiFlags1 Flags1 { get; set; } // todo?: not currently convinced these need to be bitfields
        public AiFlags2 Flags2 { get; set; }
        public ushort HealthThreshold { get; set; }
        public uint Field110 { get; set; }
    }

    public partial class PlayerEntity
    {
        public AiData AiData { get; init; } = new AiData();
    }

    [Flags]
    public enum AiFlags1 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        Bit23 = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }

    [Flags]
    public enum AiFlags2 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        Bit23 = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }
}
