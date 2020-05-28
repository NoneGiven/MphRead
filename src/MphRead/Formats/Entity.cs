using System;
using System.Runtime.InteropServices;

namespace MphRead
{
    // size: 36
    public readonly struct EntityHeader
    {
        public readonly uint Version;
        public readonly EntityLengthArray Lengths;
    }

    // size: 24
    public readonly struct EntityEntry
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string NodeName;
        public readonly short LayerMask;
        public readonly ushort Length;
        public readonly uint DataOffset;
    }

    // size: 4
    public readonly struct EntityDataHeader
    {
        public readonly ushort Type;
        public readonly ushort SomeId;
    }

    // size: 152
    public readonly struct JumpPadEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx Vector2;
        public readonly Vector3Fx Vector3;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly uint Field38;
        public readonly uint Field3C;
        public readonly uint Field40;
        public readonly uint Field44;
        public readonly uint Field48;
        public readonly uint Field4C;
        public readonly uint Field50;
        public readonly uint Field54;
        public readonly uint Field58;
        public readonly uint Field5C;
        public readonly uint Field60;
        public readonly uint Field64;
        public readonly uint Field68;
        public readonly Vector3Fx Vector4;
        public readonly uint Field78;
        public readonly short Field7C;
        public readonly ushort Field7E;
        public readonly byte Field80;
        public readonly byte Field81;
        public readonly short Field82;
        public readonly uint ModelId;
        public readonly uint BeamType;
        public readonly uint Field8C;
    }

    // size: 76
    public readonly struct ItemEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint FieldC;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint ItemId;
        public readonly uint ModelId;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly uint Field38;
        public readonly uint Field3C;
        public readonly uint Field40;
    }

    // size: 32
    public readonly struct EntityLengthArray
    {
        public readonly ushort Length00;
        public readonly ushort Length01;
        public readonly ushort Length02;
        public readonly ushort Length03;
        public readonly ushort Length04;
        public readonly ushort Length05;
        public readonly ushort Length06;
        public readonly ushort Length07;
        public readonly ushort Length08;
        public readonly ushort Length09;
        public readonly ushort Length10;
        public readonly ushort Length11;
        public readonly ushort Length12;
        public readonly ushort Length13;
        public readonly ushort Length14;
        public readonly ushort Length15;

        public ushort this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return Length00;
                }
                else if (index == 1)
                {
                    return Length01;
                }
                else if (index == 2)
                {
                    return Length02;
                }
                else if (index == 3)
                {
                    return Length03;
                }
                else if (index == 4)
                {
                    return Length04;
                }
                else if (index == 5)
                {
                    return Length05;
                }
                else if (index == 6)
                {
                    return Length06;
                }
                else if (index == 7)
                {
                    return Length07;
                }
                else if (index == 8)
                {
                    return Length08;
                }
                else if (index == 9)
                {
                    return Length09;
                }
                else if (index == 10)
                {
                    return Length10;
                }
                else if (index == 11)
                {
                    return Length11;
                }
                else if (index == 12)
                {
                    return Length12;
                }
                else if (index == 13)
                {
                    return Length13;
                }
                else if (index == 14)
                {
                    return Length14;
                }
                else if (index == 15)
                {
                    return Length15;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }
}
