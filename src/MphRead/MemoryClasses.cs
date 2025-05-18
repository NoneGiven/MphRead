using System;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Memory
{
    public abstract class MemoryClass
    {
        protected readonly Memory _memory;
        protected readonly int _offset;

        public IntPtr Address { get; }

        protected MemoryClass(Memory memory, IntPtr address)
        {
            _memory = memory;
            Address = address;
            _offset = address.ToInt32() - Memory.Offset;
        }

        protected MemoryClass(Memory memory, int address)
        {
            _memory = memory;
            Address = new IntPtr(address);
            _offset = address - Memory.Offset;
        }

        public override bool Equals(object? obj)
        {
            return this == (obj as MemoryClass);
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }

        public static bool operator ==(MemoryClass? lhs, MemoryClass? rhs)
        {
            if (lhs is null)
            {
                return rhs is null;
            }
            if (rhs is null)
            {
                return false;
            }
            return lhs.Address == rhs.Address;
        }

        public static bool operator !=(MemoryClass? lhs, MemoryClass? rhs)
        {
            return !(lhs == rhs);
        }

        protected sbyte ReadSByte(int offset)
        {
            return (sbyte)_memory.Buffer[_offset + offset];
        }

        protected byte ReadByte(int offset)
        {
            return _memory.Buffer[_offset + offset];
        }

        protected short ReadInt16(int offset)
        {
            return BitConverter.ToInt16(_memory.Buffer, _offset + offset);
        }

        protected ushort ReadUInt16(int offset)
        {
            return BitConverter.ToUInt16(_memory.Buffer, _offset + offset);
        }

        protected int ReadInt32(int offset)
        {
            return BitConverter.ToInt32(_memory.Buffer, _offset + offset);
        }

        protected uint ReadUInt32(int offset)
        {
            return BitConverter.ToUInt32(_memory.Buffer, _offset + offset);
        }

        protected void WriteSByte(int offset, sbyte value)
        {
            _memory.WriteMemory(Address + offset, new byte[] { (byte)value }, sizeof(sbyte));
        }

        protected void WriteByte(int offset, byte value)
        {
            _memory.WriteMemory(Address + offset, new byte[] { value }, sizeof(byte));
        }

        protected void WriteInt16(int offset, short value)
        {
            _memory.WriteMemory(Address + offset, BitConverter.GetBytes(value), sizeof(short));
        }

        protected void WriteUInt16(int offset, ushort value)
        {
            _memory.WriteMemory(Address + offset, BitConverter.GetBytes(value), sizeof(ushort));
        }

        protected void WriteInt32(int offset, int value)
        {
            _memory.WriteMemory(Address + offset, BitConverter.GetBytes(value), sizeof(int));
        }

        protected void WriteUInt32(int offset, uint value)
        {
            _memory.WriteMemory(Address + offset, BitConverter.GetBytes(value), sizeof(uint));
        }

        protected IntPtr ReadPointer(int offset)
        {
            return new IntPtr(ReadInt32(offset));
        }

        protected void WritePointer(int offset, IntPtr value)
        {
            WriteInt32(offset, value.ToInt32());
        }

        protected T ReadClass<T>(int offset, Func<Memory, int, T> create) where T : MemoryClass
        {
            return create.Invoke(_memory, ReadInt32(offset));
        }

        protected ColorRgb ReadColor3(int offset)
        {
            return new ColorRgb(
                ReadByte(offset),
                ReadByte(offset + 1),
                ReadByte(offset + 2)
            );
        }

        protected void WriteColor3(int offset, ColorRgb value)
        {
            byte[] bytes = new byte[3] { value.Red, value.Green, value.Blue };
            _memory.WriteMemory(Address + offset, bytes, 3);
        }

        protected Vector3 ReadVec3(int offset)
        {
            return new Vector3(
                ReadInt32(offset) / 4096f,
                ReadInt32(offset + 4) / 4096f,
                ReadInt32(offset + 8) / 4096f
            );
        }

        protected void WriteVec3(int offset, Vector3 value)
        {
            byte[] bytes = new byte[12];
            CopyFixed(value.X, bytes, 0);
            CopyFixed(value.Y, bytes, 1);
            CopyFixed(value.Z, bytes, 2);
            _memory.WriteMemory(Address + offset, bytes, 12);
        }

        protected Vector4 ReadVec4(int offset)
        {
            return new Vector4(
                ReadInt32(offset) / 4096f,
                ReadInt32(offset + 4) / 4096f,
                ReadInt32(offset + 8) / 4096f,
                ReadInt32(offset + 12) / 4096f
            );
        }

        protected void WriteVec4(int offset, Vector4 value)
        {
            byte[] bytes = new byte[16];
            CopyFixed(value.X, bytes, 0);
            CopyFixed(value.Y, bytes, 1);
            CopyFixed(value.Z, bytes, 2);
            CopyFixed(value.W, bytes, 3);
            _memory.WriteMemory(Address + offset, bytes, 16);
        }

        private void CopyFixed(float value, byte[] dest, int index)
        {
            CopyInt((int)(value * 4096), dest, index);
        }

        private void CopyInt(int value, byte[] dest, int index)
        {
            byte[] source = BitConverter.GetBytes(value);
            for (int i = 0; i < 4; i++)
            {
                dest[i + index * 4] = source[i];
            }
        }

        protected Matrix4x3 ReadMtx43(int offset)
        {
            return new Matrix4x3(
                ReadVec3(offset),
                ReadVec3(offset + 12),
                ReadVec3(offset + 24),
                ReadVec3(offset + 36)
            );
        }

        protected void WriteMtx43(int offset, Matrix4x3 value)
        {
            byte[] bytes = new byte[48];
            CopyFixed(value.M11, bytes, 0);
            CopyFixed(value.M12, bytes, 1);
            CopyFixed(value.M13, bytes, 2);
            CopyFixed(value.M21, bytes, 3);
            CopyFixed(value.M22, bytes, 4);
            CopyFixed(value.M23, bytes, 5);
            CopyFixed(value.M31, bytes, 6);
            CopyFixed(value.M32, bytes, 7);
            CopyFixed(value.M33, bytes, 8);
            CopyFixed(value.M41, bytes, 9);
            CopyFixed(value.M42, bytes, 10);
            CopyFixed(value.M43, bytes, 11);
            _memory.WriteMemory(Address + offset, bytes, 48);
        }
    }

    public class CEntity : MemoryClass
    {
        private const int _off0 = 0x0;
        public EntityType EntityType { get => (EntityType)ReadUInt16(_off0); set => WriteUInt16(_off0, (ushort)value); }

        private const int _off1 = 0x2;
        public ushort EntityId { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x4;
        public ushort ScanId { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x6;
        public ushort Padding6 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x8; // MtxFx43*
        public IntPtr MtxPtr { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0xC; // EntityClass*
        public IntPtr Funcs { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x10; // CEntity*
        public IntPtr Prev { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x14; // CEntity*
        public IntPtr Next { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        public CEntity(Memory memory, int address) : base(memory, address)
        {
        }

        public CEntity(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CEnemyBase : CEntity
    {
        private const int _off1 = 0x18;
        public ushort Flags { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x1A;
        public EnemyType Type { get => (EnemyType)ReadByte(_off2); set => WriteByte(_off2, (byte)value); }

        private const int _off3 = 0x1B;
        public byte State { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x1C;
        public byte NextSubId { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x1D;
        public byte HealthbarMsgId { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x1E;
        public byte TimeSinceDmg { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x1F;
        public byte HitPlayerBits { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x20;
        public uint Effectiveness { get => ReadUInt32(_off8); set => WriteUInt32(_off8, value); }

        private const int _off9 = 0x24; // CEntity*
        public IntPtr Owner { get => ReadPointer(_off9); set => WritePointer(_off9, value); }

        private const int _off10 = 0x28;
        public Vector3 LinkInvPos { get => ReadVec3(_off10); set => WriteVec3(_off10, value); }

        private const int _off11 = 0x34;
        public Vector3 LinkInvVec2 { get => ReadVec3(_off11); set => WriteVec3(_off11, value); }

        private const int _off12 = 0x40;
        public Vector3 LinkInvVec1 { get => ReadVec3(_off12); set => WriteVec3(_off12, value); }

        private const int _off13 = 0x4C;
        public Vector3 Pos { get => ReadVec3(_off13); set => WriteVec3(_off13, value); }

        private const int _off14 = 0x58;
        public Vector3 PrevPos { get => ReadVec3(_off14); set => WriteVec3(_off14, value); }

        private const int _off15 = 0x64;
        public Vector3 Speed { get => ReadVec3(_off15); set => WriteVec3(_off15, value); }

        private const int _off16 = 0x77;
        public Vector3 Vec2 { get => ReadVec3(_off16); set => WriteVec3(_off16, value); }

        private const int _off17 = 0x7C;
        public Vector3 Vec1 { get => ReadVec3(_off17); set => WriteVec3(_off17, value); }

        private const int _off18 = 0x88;
        public int BoundingRadius { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x8C;
        public CollisionVolume HurtVolUnxf { get; }

        private const int _off20 = 0xCC;
        public CollisionVolume HurtVol { get; }

        private const int _off21 = 0x10C;
        public int Scale { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x110;
        public ushort Health { get => ReadUInt16(_off22); set => WriteUInt16(_off22, value); }

        private const int _off23 = 0x112;
        public ushort HealthMax { get => ReadUInt16(_off23); set => WriteUInt16(_off23, value); }

        private const int _off24 = 0x114; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off24); set => WritePointer(_off24, value); }

        private const int _off25 = 0x118;
        public CModel Model { get; }

        private const int _off26 = 0x160;
        public SfxParameters SfxParameters { get; }

        private const int _off27 = 0x164; // EnemySubroutinePtr*
        public IntPtr Subroutine { get => ReadPointer(_off27); set => WritePointer(_off27, value); }

        private const int _off28 = 0x168;
        public ushort Unused168 { get => ReadUInt16(_off28); set => WriteUInt16(_off28, value); }

        private const int _off29 = 0x16A;
        public ushort Padding16A { get => ReadUInt16(_off29); set => WriteUInt16(_off29, value); }

        private const int _off30 = 0x16C;
        public int Unused16C { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        public CEnemyBase(Memory memory, int address) : base(memory, address)
        {
            HurtVolUnxf = new CollisionVolume(memory, address + _off19);
            HurtVol = new CollisionVolume(memory, address + _off20);
            Model = new CModel(memory, address + _off25);
            SfxParameters = new SfxParameters(memory, address + _off26);
        }

        public CEnemyBase(Memory memory, IntPtr address) : base(memory, address)
        {
            HurtVolUnxf = new CollisionVolume(memory, address + _off19);
            HurtVol = new CollisionVolume(memory, address + _off20);
            Model = new CModel(memory, address + _off25);
            SfxParameters = new SfxParameters(memory, address + _off26);
        }
    }

    public class CEnemy24 : CEnemyBase
    {
        private const int _off0 = 0x170; // Model*
        public IntPtr RegenMdl { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174; // Animation*
        public IntPtr RegenAnim { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x178;
        public CModel Regen { get; }

        private const int _off3 = 0x1C0; // Color3**
        public IntPtr Colors { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x1C4; // CEnemy25*
        public IntPtr Head { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x1C8; // CEnemy26*[2]
        public IntPtrArray Arms { get; }

        private const int _off6 = 0x1D0; // CEnemy27*[3]
        public IntPtrArray Legs { get; }

        private const int _off7 = 0x1DC; // CEnemy28*
        public IntPtr GoreaB { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        private const int _off8 = 0x1E0; // Node*
        public IntPtr SpineNode { get => ReadPointer(_off8); set => WritePointer(_off8, value); }

        private const int _off9 = 0x1E4;
        public int Field1E4 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x1E8;
        public CollisionVolume Volume { get; }

        private const int _off11 = 0x228;
        public int SpeedFactor { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x22C;
        public byte ArmBits { get => ReadByte(_off12); set => WriteByte(_off12, value); }

        private const int _off13 = 0x22D;
        public byte WeaponId { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x22E;
        public ushort Unused22E { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x230;
        public Vector3 TargetFacing { get => ReadVec3(_off15); set => WriteVec3(_off15, value); }

        private const int _off16 = 0x23C;
        public ushort Field23C { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x23E;
        public ushort Field23E { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x240;
        public ushort Field240 { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x242;
        public ushort Field242 { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x244;
        public ushort Field244 { get => ReadUInt16(_off20); set => WriteUInt16(_off20, value); }

        private const int _off21 = 0x246;
        public ushort Field246 { get => ReadUInt16(_off21); set => WriteUInt16(_off21, value); }

        private const int _off22 = 0x248;
        public int GoreaFlags { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x24C;
        public byte NextState { get => ReadByte(_off23); set => WriteByte(_off23, value); }

        private const int _off24 = 0x24D;
        public byte Padding24D { get => ReadByte(_off24); set => WriteByte(_off24, value); }

        private const int _off25 = 0x24E;
        public ushort Padding24E { get => ReadUInt16(_off25); set => WriteUInt16(_off25, value); }

        public CEnemy24(Memory memory, int address) : base(memory, address)
        {
            Regen = new CModel(memory, address + _off2);
            Arms = new IntPtrArray(memory, address + _off5, 2);
            Legs = new IntPtrArray(memory, address + _off6, 3);
            Volume = new CollisionVolume(memory, address + _off10);
        }

        public CEnemy24(Memory memory, IntPtr address) : base(memory, address)
        {
            Regen = new CModel(memory, address + _off2);
            Arms = new IntPtrArray(memory, address + _off5, 2);
            Legs = new IntPtrArray(memory, address + _off6, 3);
            Volume = new CollisionVolume(memory, address + _off10);
        }
    }

    public class CEnemy25 : CEnemyBase
    {
        private const int _off0 = 0x170; // Node*
        public IntPtr AttachNode { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174; // CEnemy24*
        public IntPtr GoreaOwner { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x178; // EffectEntry*
        public IntPtr FlashEffect { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0x17C;
        public ushort Damage { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x17E;
        public ushort Padding17E { get => ReadUInt16(_off4); set => WriteUInt16(_off4, value); }

        public CEnemy25(Memory memory, int address) : base(memory, address)
        {
        }

        public CEnemy25(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CEnemy26 : CEnemyBase
    {
        private const int _off0 = 0x170; // Node*
        public IntPtr ShoulderNode { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174; // Node*
        public IntPtr UpperArmNode { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x178; // Node*
        public IntPtr ElbowNode { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0x17C; // CEnemy24*
        public IntPtr GoreaOwner { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x180; // EffectEntry*
        public IntPtr ShotEffect { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x184; // EffectEntry*
        public IntPtr DmgEffect { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x188;
        public EquipInfo EquipInfo { get; }

        private const int _off7 = 0x19C;
        public ushort RegenTimer { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x19E;
        public ushort ColorInc { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x1A0;
        public ushort Ammo { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x1A2;
        public ushort Cooldown { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x1A4;
        public ushort DamageTo { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x1A6;
        public byte Index { get => ReadByte(_off12); set => WriteByte(_off12, value); }

        private const int _off13 = 0x1A7;
        public byte ArmFlags { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        public CEnemy26(Memory memory, int address) : base(memory, address)
        {
            EquipInfo = new EquipInfo(memory, address + _off6);
        }

        public CEnemy26(Memory memory, IntPtr address) : base(memory, address)
        {
            EquipInfo = new EquipInfo(memory, address + _off6);
        }
    }

    public class CEnemy27 : CEnemyBase
    {
        private const int _off0 = 0x170; // Node*
        public IntPtr KneeNode { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174; // CEnemyBase*
        public IntPtr GoreaOwner { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x178;
        public ushort Unused178 { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x17A;
        public byte Index { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x17B;
        public byte Padding17B { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        public CEnemy27(Memory memory, int address) : base(memory, address)
        {
        }

        public CEnemy27(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CEnemy28 : CEnemyBase
    {
        private const int _off0 = 0x170; // Node*
        public IntPtr SpineNode { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174; // CEnemy29*
        public IntPtr SealSphere { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x178; // CEnemy24*
        public IntPtr GoreaOwner { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0x17C;
        public CollisionVolume Volume { get; }

        private const int _off4 = 0x1BC;
        public Vector3 TargetFacing { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0x1C8;
        public ushort Field1C8 { get => ReadUInt16(_off5); set => WriteUInt16(_off5, value); }

        private const int _off6 = 0x1CA;
        public ushort Field1CA { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x1CC;
        public ushort Field1CC { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x1CE;
        public byte PhasesLeft { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0x1CF;
        public byte GoreaFlags { get => ReadByte(_off9); set => WriteByte(_off9, value); }

        private const int _off10 = 0x1D0; // CEnemy30*[9]
        public IntPtrArray Trocras { get; }

        public CEnemy28(Memory memory, int address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off3);
            Trocras = new IntPtrArray(memory, address + _off10, 9);
        }

        public CEnemy28(Memory memory, IntPtr address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off3);
            Trocras = new IntPtrArray(memory, address + _off10, 9);
        }
    }

    public class Enemy29Fields : MemoryClass
    {
        private const int _off0 = 0x0; // VecFx32*
        public IntPtr VecsReference { get => ReadPointer(_off0); set => WritePointer(_off0, value); }
        public StructArray<VecFx32> Vecs { get; init; }

        private const int _off1 = 0x4; // MtxFx43*
        public IntPtr MtxsReference { get => ReadPointer(_off1); set => WritePointer(_off1, value); }
        public StructArray<MtxFx43> Mtxs { get; init; }

        private const int _off2 = 0x8; // int*
        public IntPtr IntsReference { get => ReadPointer(_off2); set => WritePointer(_off2, value); }
        public Int32Array Ints { get; init; }

        private const int _off3 = 0xC; // __int16*
        public IntPtr ShortsReference { get => ReadPointer(_off3); set => WritePointer(_off3, value); }
        public Int16Array Shorts { get; init; }

        private const int _off4 = 0x10;
        public Vector3 Field10 { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0x1C;
        public int Count1 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x20;
        public int Count2 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x24;
        public int Field24 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x28;
        public int Field28 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x2C;
        public int Unused2C { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x30;
        public int Field30 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x34;
        public int Field34 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x38;
        public int Field38 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x3C;
        public int Unused3C { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x40;
        public int Unused40 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x44;
        public int Unused44 { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x48;
        public int Unused48 { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x4C;
        public int Unused4C { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x50;
        public int Unused50 { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x54;
        public int Unused54 { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        private const int _off20 = 0x58;
        public int Unused58 { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0x5C;
        public int Unused5C { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        public Enemy29Fields(Memory memory, int address) : base(memory, address)
        {
            Vecs = new StructArray<VecFx32>(memory, VecsReference, Count2, 12, (Memory m, int a) => new VecFx32(m, a));
            Mtxs = new StructArray<MtxFx43>(memory, MtxsReference, Count1, 48, (Memory m, int a) => new MtxFx43(m, a));
            Ints = new Int32Array(memory, IntsReference, Count1);
            Shorts = new Int16Array(memory, ShortsReference, Count1);
        }

        public Enemy29Fields(Memory memory, IntPtr address) : base(memory, address)
        {
            Vecs = new StructArray<VecFx32>(memory, VecsReference, Count2, 12, (Memory m, int a) => new VecFx32(m, a));
            Mtxs = new StructArray<MtxFx43>(memory, MtxsReference, Count1, 48, (Memory m, int a) => new MtxFx43(m, a));
            Ints = new Int32Array(memory, IntsReference, Count1);
            Shorts = new Int16Array(memory, ShortsReference, Count1);
        }
    }

    public class CEnemy29 : CEnemyBase
    {
        private const int _off0 = 0x170; // Model*
        public IntPtr MindTrickMdl { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174; // Animation*
        public IntPtr MindTrickAnim { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x178;
        public CModel MindTrick { get; }

        private const int _off3 = 0x1C0; // Model*
        public IntPtr GrappleMdl { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x1C4; // Animation*
        public IntPtr GrappleAnim { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x1C8;
        public CModel Grapple { get; }

        private const int _off6 = 0x210; // Node*
        public IntPtr AttachNode { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x214; // CEnemy28*
        public IntPtr GoreaOwner { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        private const int _off8 = 0x218; // Enemy29Fields*
        public IntPtr FieldsReference { get => ReadPointer(_off8); set => WritePointer(_off8, value); }
        public Enemy29Fields Fields { get; init; }

        private const int _off9 = 0x21C;
        public ushort Field21C { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x21E;
        public ushort Field21E { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x220;
        public int Unused220 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x224;
        public int Field224 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x228;
        public byte Grappling { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x229;
        public byte Padding229 { get => ReadByte(_off14); set => WriteByte(_off14, value); }

        private const int _off15 = 0x22A;
        public ushort Padding22A { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x22C;
        public ColorRgb Ambient { get => ReadColor3(_off16); set => WriteColor3(_off16, value); }

        private const int _off17 = 0x22F;
        public ColorRgb Diffuse { get => ReadColor3(_off17); set => WriteColor3(_off17, value); }

        private const int _off18 = 0x232;
        public ushort DamageTo { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x234;
        public ushort Field234 { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x236;
        public ushort DmgTimer { get => ReadUInt16(_off20); set => WriteUInt16(_off20, value); }

        private const int _off21 = 0x238;
        public byte Unused238 { get => ReadByte(_off21); set => WriteByte(_off21, value); }

        private const int _off22 = 0x239;
        public byte Padding239 { get => ReadByte(_off22); set => WriteByte(_off22, value); }

        private const int _off23 = 0x23A;
        public ushort Padding23A { get => ReadUInt16(_off23); set => WriteUInt16(_off23, value); }

        private const int _off24 = 0x23C; // EffectEntry*
        public IntPtr GrappleEffect { get => ReadPointer(_off24); set => WritePointer(_off24, value); }

        public CEnemy29(Memory memory, int address) : base(memory, address)
        {
            MindTrick = new CModel(memory, address + _off2);
            Grapple = new CModel(memory, address + _off5);
            Fields = new Enemy29Fields(memory, FieldsReference);
        }

        public CEnemy29(Memory memory, IntPtr address) : base(memory, address)
        {
            MindTrick = new CModel(memory, address + _off2);
            Grapple = new CModel(memory, address + _off5);
            Fields = new Enemy29Fields(memory, FieldsReference);
        }
    }

    public class CEnemy30 : CEnemyBase
    {
        private const int _off0 = 0x170; // CEnemy28*
        public IntPtr GoreaOwner { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x174;
        public Vector3 Field174 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x180;
        public int Index { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        private const int _off3 = 0x184;
        public ushort Field184 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x186;
        public ushort TrocraState { get => ReadUInt16(_off4); set => WriteUInt16(_off4, value); }

        public CEnemy30(Memory memory, int address) : base(memory, address)
        {
        }

        public CEnemy30(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CPlatform : CEntity
    {
        private const int _off0 = 0x18;
        public int NoPort { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x1C;
        public int ModelId { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x20; // CEntity*
        public IntPtr ScanEventTarget { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0x24;
        public int MovementType { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x28;
        public int ForCutscene { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x2C;
        public int ReverseType { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x30;
        public PlatformFlags Flags { get => (PlatformFlags)ReadInt32(_off6); set => WriteInt32(_off6, (int)value); }

        private const int _off7 = 0x34;
        public ushort CollisionDamage { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x36;
        public ushort Padding36 { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x38;
        public Vector3 BeamSpawnDir { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x44;
        public int BeamIndex { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x48;
        public ushort BeamInterval { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x4A;
        public ushort Padding4A { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x4C;
        public int BeamOnIntervals { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x50;
        public int ResistEffId { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x54;
        public int Effectiveness { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x58;
        public int DamageEffId { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x5C;
        public int DeadEffId { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x60;
        public int Unused60 { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x64;
        public int Unused64 { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        private const int _off20 = 0x68;
        public PlatStateFlags StateFlags { get => (PlatStateFlags)ReadUInt32(_off20); set => WriteUInt32(_off20, (uint)value); }

        private const int _off21 = 0x6C;
        public int CollisionBits { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x70;
        public ushort TimeSincePlayerCol { get => ReadUInt16(_off22); set => WriteUInt16(_off22, value); }

        private const int _off23 = 0x72;
        public PlatAnimFlags AnimFlags { get => (PlatAnimFlags)ReadUInt16(_off23); set => WriteUInt16(_off23, (ushort)value); }

        private const int _off24 = 0x74;
        public int CurrentAnimId { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        private const int _off25 = 0x78;
        public int CurrentAnim { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0x7C;
        public byte FromIndex { get => ReadByte(_off26); set => WriteByte(_off26, value); }

        private const int _off27 = 0x7D;
        public byte ToIndex { get => ReadByte(_off27); set => WriteByte(_off27, value); }

        private const int _off28 = 0x7E;
        public byte State { get => ReadByte(_off28); set => WriteByte(_off28, value); }

        private const int _off29 = 0x7F;
        public PlatformState PrevState { get => (PlatformState)ReadByte(_off29); set => WriteByte(_off29, (byte)value); }

        private const int _off30 = 0x80;
        public PlatformState PosCount { get => (PlatformState)ReadByte(_off30); set => WriteByte(_off30, (byte)value); }

        private const int _off31 = 0x81;
        public byte Padding81 { get => ReadByte(_off31); set => WriteByte(_off31, value); }

        private const int _off32 = 0x82;
        public ushort MoveTimer { get => ReadUInt16(_off32); set => WriteUInt16(_off32, value); }

        private const int _off33 = 0x84;
        public ushort RecoilTimer { get => ReadUInt16(_off33); set => WriteUInt16(_off33, value); }

        private const int _off34 = 0x86;
        public ushort Health { get => ReadUInt16(_off34); set => WriteUInt16(_off34, value); }

        private const int _off35 = 0x88;
        public ushort HealthMax { get => ReadUInt16(_off35); set => WriteUInt16(_off35, value); }

        private const int _off36 = 0x8A;
        public ushort HalfHealth { get => ReadUInt16(_off36); set => WriteUInt16(_off36, value); }

        private const int _off37 = 0x8C;
        public ushort ParentId { get => ReadUInt16(_off37); set => WriteUInt16(_off37, value); }

        private const int _off38 = 0x8E;
        public ushort Padding8E { get => ReadUInt16(_off38); set => WriteUInt16(_off38, value); }

        private const int _off39 = 0x90; // CEntity*
        public IntPtr Parent { get => ReadPointer(_off39); set => WritePointer(_off39, value); }

        private const int _off40 = 0x94;
        public EquipInfoPtr EquipInfo { get; }

        private const int _off41 = 0xA8;
        public ushort BeamAmmo { get => ReadUInt16(_off41); set => WriteUInt16(_off41, value); }

        private const int _off42 = 0xAA;
        public ushort PaddingAA { get => ReadUInt16(_off42); set => WriteUInt16(_off42, value); }

        private const int _off43 = 0xAC;
        public int UnusedAC { get => ReadInt32(_off43); set => WriteInt32(_off43, value); }

        private const int _off44 = 0xB0;
        public int UnusedB0 { get => ReadInt32(_off44); set => WriteInt32(_off44, value); }

        private const int _off45 = 0xB4;
        public int UnusedB4 { get => ReadInt32(_off45); set => WriteInt32(_off45, value); }

        private const int _off46 = 0xB8;
        public ushort BeamTimer { get => ReadUInt16(_off46); set => WriteUInt16(_off46, value); }

        private const int _off47 = 0xBA;
        public ushort BeamIntervalIndex { get => ReadUInt16(_off47); set => WriteUInt16(_off47, value); }

        private const int _off48 = 0xBC;
        public ushort DrawingBeam { get => ReadUInt16(_off48); set => WriteUInt16(_off48, value); }

        private const int _off49 = 0xBE;
        public ushort PaddingBE { get => ReadUInt16(_off49); set => WriteUInt16(_off49, value); }

        private const int _off50 = 0xC0; // EntityData*
        public IntPtr Data { get => ReadPointer(_off50); set => WritePointer(_off50, value); }

        private const int _off51 = 0xC4; // VecFx32*
        public IntPtr Positions { get => ReadPointer(_off51); set => WritePointer(_off51, value); }

        private const int _off52 = 0xC8; // Vec4*
        public IntPtr Rotations { get => ReadPointer(_off52); set => WritePointer(_off52, value); }

        private const int _off53 = 0xCC;
        public Vector3 PosOffset { get => ReadVec3(_off53); set => WriteVec3(_off53, value); }

        private const int _off54 = 0xD8;
        public Vector3 VisiblePos { get => ReadVec3(_off54); set => WriteVec3(_off54, value); }

        private const int _off55 = 0xE4;
        public Vector3 PrevVisiblePos { get => ReadVec3(_off55); set => WriteVec3(_off55, value); }

        private const int _off56 = 0xF0;
        public Vector3 Position { get => ReadVec3(_off56); set => WriteVec3(_off56, value); }

        private const int _off57 = 0xFC;
        public Vector3 PrevPosition { get => ReadVec3(_off57); set => WriteVec3(_off57, value); }

        private const int _off58 = 0x108;
        public Vector4 CurRotation { get => ReadVec4(_off58); set => WriteVec4(_off58, value); }

        private const int _off59 = 0x118;
        public Vector4 FromRotation { get => ReadVec4(_off59); set => WriteVec4(_off59, value); }

        private const int _off60 = 0x128;
        public Vector4 ToRotation { get => ReadVec4(_off60); set => WriteVec4(_off60, value); }

        private const int _off61 = 0x138;
        public int MovePct { get => ReadInt32(_off61); set => WriteInt32(_off61, value); }

        private const int _off62 = 0x13C;
        public int MoveInc { get => ReadInt32(_off62); set => WriteInt32(_off62, value); }

        private const int _off63 = 0x140;
        public int Unused140 { get => ReadInt32(_off63); set => WriteInt32(_off63, value); }

        private const int _off64 = 0x144;
        public int Unused144 { get => ReadInt32(_off64); set => WriteInt32(_off64, value); }

        private const int _off65 = 0x148;
        public Vector3 ColMin { get => ReadVec3(_off65); set => WriteVec3(_off65, value); }

        private const int _off66 = 0x154;
        public Vector3 ColW { get => ReadVec3(_off66); set => WriteVec3(_off66, value); }

        private const int _off67 = 0x160;
        public Vector3 ColMax { get => ReadVec3(_off67); set => WriteVec3(_off67, value); }

        private const int _off68 = 0x16C;
        public Vector3 Velocity { get => ReadVec3(_off68); set => WriteVec3(_off68, value); }

        private const int _off69 = 0x178;
        public int Unused178 { get => ReadInt32(_off69); set => WriteInt32(_off69, value); }

        private const int _off70 = 0x17C;
        public int Unused17C { get => ReadInt32(_off70); set => WriteInt32(_off70, value); }

        private const int _off71 = 0x180;
        public int Unused180 { get => ReadInt32(_off71); set => WriteInt32(_off71, value); }

        private const int _off72 = 0x184;
        public int ForwardSpeed { get => ReadInt32(_off72); set => WriteInt32(_off72, value); }

        private const int _off73 = 0x188;
        public int BackwardSpeed { get => ReadInt32(_off73); set => WriteInt32(_off73, value); }

        private const int _off74 = 0x18C;
        public int SfxVolume { get => ReadInt32(_off74); set => WriteInt32(_off74, value); }

        private const int _off75 = 0x190;
        public Vector3 Vec2 { get => ReadVec3(_off75); set => WriteVec3(_off75, value); }

        private const int _off76 = 0x19C;
        public Vector3 Vec1 { get => ReadVec3(_off76); set => WriteVec3(_off76, value); }

        private const int _off77 = 0x1A8;
        public EntityCollision EntityCollision { get; }

        private const int _off78 = 0x25C; // MaybeColLinkedStruct*
        public IntPtr MtxObj { get => ReadPointer(_off78); set => WritePointer(_off78, value); }

        private const int _off79 = 0x260;
        public ushort AttachNodeIndex { get => ReadUInt16(_off79); set => WriteUInt16(_off79, value); }

        private const int _off80 = 0x262; // ushort[4]
        public UInt16Array Turrets { get; }

        private const int _off81 = 0x26A;
        public ushort Padding26A { get => ReadUInt16(_off81); set => WriteUInt16(_off81, value); }

        private const int _off82 = 0x26C; // EffectEntry*[4]
        public IntPtrArray Effects { get; }

        private const int _off83 = 0x27C;
        public int Unused27C { get => ReadInt32(_off83); set => WriteInt32(_off83, value); }

        private const int _off84 = 0x280;
        public int Unused280 { get => ReadInt32(_off84); set => WriteInt32(_off84, value); }

        private const int _off85 = 0x284;
        public int Unused284 { get => ReadInt32(_off85); set => WriteInt32(_off85, value); }

        private const int _off86 = 0x288;
        public int Unused288 { get => ReadInt32(_off86); set => WriteInt32(_off86, value); }

        private const int _off87 = 0x28C;
        public int Unused28C { get => ReadInt32(_off87); set => WriteInt32(_off87, value); }

        private const int _off88 = 0x290;
        public int Unused290 { get => ReadInt32(_off88); set => WriteInt32(_off88, value); }

        private const int _off89 = 0x294; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off89); set => WritePointer(_off89, value); }

        private const int _off90 = 0x298;
        public CModel Model { get; }

        private const int _off91 = 0x2E0; // CollisionPort*
        public IntPtr Port { get => ReadPointer(_off91); set => WritePointer(_off91, value); }

        private const int _off92 = 0x2E4; // CEntity*
        public IntPtr HitEventTarget { get => ReadPointer(_off92); set => WritePointer(_off92, value); }

        private const int _off93 = 0x2E8; // CEntity*
        public IntPtr PlayerColEventTarget { get => ReadPointer(_off93); set => WritePointer(_off93, value); }

        private const int _off94 = 0x2EC; // CEntity*
        public IntPtr DeadEventTarget { get => ReadPointer(_off94); set => WritePointer(_off94, value); }

        private const int _off95 = 0x2F0; // byte[4]
        public ByteArray LifetimeEventIndices { get; }

        private const int _off99 = 0x2F4; // CEntity*[4]
        public IntPtrArray LifetimeEventTargets { get; }

        private const int _off100 = 0x304; // Message[4]
        public U32EnumArray<Message> LifetimeEventIds { get; }

        private const int _off101 = 0x314; // int[4]
        public Int32Array LifetimeEventParam1s { get; }

        private const int _off102 = 0x324; // int[4]
        public Int32Array LifetimeEventParam2s { get; }

        private const int _off103 = 0x334;
        public SfxParameters SfxParameters { get; }

        public CPlatform(Memory memory, int address) : base(memory, address)
        {
            EquipInfo = new EquipInfoPtr(memory, address + _off40);
            EntityCollision = new EntityCollision(memory, address + _off77);
            Turrets = new UInt16Array(memory, address + _off80, 4);
            Effects = new IntPtrArray(memory, address + _off82, 4);
            Model = new CModel(memory, address + _off90);
            LifetimeEventIndices = new ByteArray(memory, address + _off95, 4);
            LifetimeEventTargets = new IntPtrArray(memory, address + _off99, 4);
            LifetimeEventIds = new U32EnumArray<Message>(memory, address + _off100, 4);
            LifetimeEventParam1s = new Int32Array(memory, address + _off101, 4);
            LifetimeEventParam2s = new Int32Array(memory, address + _off102, 4);
            SfxParameters = new SfxParameters(memory, address + _off103);
        }

        public CPlatform(Memory memory, IntPtr address) : base(memory, address)
        {
            EquipInfo = new EquipInfoPtr(memory, address + _off40);
            EntityCollision = new EntityCollision(memory, address + _off77);
            Turrets = new UInt16Array(memory, address + _off80, 4);
            Effects = new IntPtrArray(memory, address + _off82, 4);
            Model = new CModel(memory, address + _off90);
            LifetimeEventIndices = new ByteArray(memory, address + _off95, 4);
            LifetimeEventTargets = new IntPtrArray(memory, address + _off99, 4);
            LifetimeEventIds = new U32EnumArray<Message>(memory, address + _off100, 4);
            LifetimeEventParam1s = new Int32Array(memory, address + _off101, 4);
            LifetimeEventParam2s = new Int32Array(memory, address + _off102, 4);
            SfxParameters = new SfxParameters(memory, address + _off103);
        }
    }

    public class CObject : CEntity
    {
        private const int _off0 = 0x18;
        public byte Flags { get => ReadByte(_off0); set => WriteByte(_off0, value); }

        private const int _off1 = 0x19;
        public byte Field19 { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x1A;
        public ushort Field1A { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x1C;
        public int EffectFlags { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x20;
        public ushort LinkedEntity { get => ReadUInt16(_off4); set => WriteUInt16(_off4, value); }

        private const int _off5 = 0x22;
        public ushort Field22 { get => ReadUInt16(_off5); set => WriteUInt16(_off5, value); }

        private const int _off6 = 0x24;
        public Vector3 TempPos { get => ReadVec3(_off6); set => WriteVec3(_off6, value); }

        private const int _off7 = 0x30;
        public Vector3 TempVec2 { get => ReadVec3(_off7); set => WriteVec3(_off7, value); }

        private const int _off8 = 0x3C;
        public Vector3 TempVec1 { get => ReadVec3(_off8); set => WriteVec3(_off8, value); }

        private const int _off9 = 0x48;
        public ushort AttachNodeIndex { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x4A;
        public ushort Field4A { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x4C;
        public Vector3 Pos { get => ReadVec3(_off11); set => WriteVec3(_off11, value); }

        private const int _off12 = 0x58;
        public Vector3 Vec2 { get => ReadVec3(_off12); set => WriteVec3(_off12, value); }

        private const int _off13 = 0x64;
        public Vector3 Vec1 { get => ReadVec3(_off13); set => WriteVec3(_off13, value); }

        private const int _off14 = 0x70;
        public Vector3 SomePos { get => ReadVec3(_off14); set => WriteVec3(_off14, value); }

        private const int _off15 = 0x7C; // EntityData*
        public IntPtr Data { get => ReadPointer(_off15); set => WritePointer(_off15, value); }

        private const int _off16 = 0x80; // CEntity*
        public IntPtr ScanEventTarget { get => ReadPointer(_off16); set => WritePointer(_off16, value); }

        private const int _off17 = 0x84; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off17); set => WritePointer(_off17, value); }

        private const int _off18 = 0x88; // EntityCollision[2]
        public StructArray<EntityCollision> ColStructs { get; }

        private const int _off19 = 0x1F0; // MaybeColLinkedStruct*[2]
        public IntPtrArray MtxObjs { get; }

        private const int _off20 = 0x1F8;
        public CModel Model { get; }

        private const int _off21 = 0x240;
        public int EffectProcessing { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x244;
        public int EffectId { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x248;
        public ushort EffectInterval { get => ReadUInt16(_off23); set => WriteUInt16(_off23, value); }

        private const int _off24 = 0x24A;
        public ushort EffectActive { get => ReadUInt16(_off24); set => WriteUInt16(_off24, value); }

        private const int _off25 = 0x24C;
        public int EffectOnIntervals { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0x250;
        public CollisionVolume Volume { get; }

        private const int _off27 = 0x290; // EffectEntry*
        public IntPtr Effect { get => ReadPointer(_off27); set => WritePointer(_off27, value); }

        private const int _off28 = 0x294;
        public ushort EffectTimer { get => ReadUInt16(_off28); set => WriteUInt16(_off28, value); }

        private const int _off29 = 0x296;
        public ushort EffectIntervalIndex { get => ReadUInt16(_off29); set => WriteUInt16(_off29, value); }

        private const int _off30 = 0x298;
        public SfxParameters Sfx { get; }

        public CObject(Memory memory, int address) : base(memory, address)
        {
            ColStructs = new StructArray<EntityCollision>(memory, address + _off18, 2, 180, (Memory m, int a) => new EntityCollision(m, a));
            MtxObjs = new IntPtrArray(memory, address + _off19, 2);
            Model = new CModel(memory, address + _off20);
            Volume = new CollisionVolume(memory, address + _off26);
            Sfx = new SfxParameters(memory, address + _off30);
        }

        public CObject(Memory memory, IntPtr address) : base(memory, address)
        {
            ColStructs = new StructArray<EntityCollision>(memory, address + _off18, 2, 180, (Memory m, int a) => new EntityCollision(m, a));
            MtxObjs = new IntPtrArray(memory, address + _off19, 2);
            Model = new CModel(memory, address + _off20);
            Volume = new CollisionVolume(memory, address + _off26);
            Sfx = new SfxParameters(memory, address + _off30);
        }
    }

    public class CPlayerSpawn : CEntity
    {
        private const int _off0 = 0x18;
        public ushort Cooldown { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x1A;
        public ushort Field1A { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x1C;
        public Vector3 Vec1 { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x28;
        public Vector3 Vec2 { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x34;
        public Vector3 Pos { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0x40;
        public byte Initial { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x41;
        public byte Active { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x42;
        public byte TeamIndex { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x43;
        public byte Field43 { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0x44; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off9); set => WritePointer(_off9, value); }

        public CPlayerSpawn(Memory memory, int address) : base(memory, address)
        {
        }

        public CPlayerSpawn(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CDoor : CEntity
    {
        private const int _off0 = 0x18;
        public ushort Flags { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x1A;
        public byte DoorPaletteId { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x1B;
        public byte Field1B { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x1C;
        public byte Field1C { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x1D;
        public byte Field1D { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x1E;
        public ushort Field1E { get => ReadUInt16(_off5); set => WriteUInt16(_off5, value); }

        private const int _off6 = 0x20;
        public int SomeRoomId { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x24;
        public Vector3 Vec1 { get => ReadVec3(_off7); set => WriteVec3(_off7, value); }

        private const int _off8 = 0x30;
        public Vector3 Vec2 { get => ReadVec3(_off8); set => WriteVec3(_off8, value); }

        private const int _off9 = 0x3C;
        public Vector3 Pos { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x48;
        public Vector3 LockPos { get => ReadVec3(_off10); set => WriteVec3(_off10, value); }

        private const int _off11 = 0x54;
        public int BoundingRadius { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x58;
        public int BoundingRadiusSquared { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x5C;
        public CModel DoorModel { get; }

        private const int _off14 = 0xA4;
        public CModel LockModel { get; }

        private const int _off15 = 0xEC;
        public DoorType DoorType { get => (DoorType)ReadUInt32(_off15); set => WriteUInt32(_off15, (uint)value); }

        private const int _off16 = 0xF0;
        public int TargetRoom { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0xF4; // CollisionPort*
        public IntPtr Port { get => ReadPointer(_off17); set => WritePointer(_off17, value); }

        private const int _off18 = 0xF8; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off18); set => WritePointer(_off18, value); }

        private const int _off19 = 0xFC; // NodeRef*
        public IntPtr DoorNodeRef { get => ReadPointer(_off19); set => WritePointer(_off19, value); }

        private const int _off20 = 0x100;
        public SfxParameters SfxParameters { get; }

        private const int _off21 = 0x104; // EntityData*
        public IntPtr Data { get => ReadPointer(_off21); set => WritePointer(_off21, value); }

        public CDoor(Memory memory, int address) : base(memory, address)
        {
            DoorModel = new CModel(memory, address + _off13);
            LockModel = new CModel(memory, address + _off14);
            SfxParameters = new SfxParameters(memory, address + _off20);
        }

        public CDoor(Memory memory, IntPtr address) : base(memory, address)
        {
            DoorModel = new CModel(memory, address + _off13);
            LockModel = new CModel(memory, address + _off14);
            SfxParameters = new SfxParameters(memory, address + _off20);
        }
    }

    public class CItemSpawn : CEntity
    {
        private const int _off0 = 0x18;
        public ushort ItemEntityId { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x1A;
        public ushort Field1A { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x1C;
        public Vector3 Field1C { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x28;
        public int Field28 { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x2C;
        public int Field2C { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x30;
        public int Field30 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x34;
        public int Field34 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x38;
        public int Field38 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x3C;
        public int Field3C { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x40;
        public Vector3 Pos { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x4C; // EntityData*
        public IntPtr Data { get => ReadPointer(_off10); set => WritePointer(_off10, value); }

        private const int _off11 = 0x50;
        public byte Flags { get => ReadByte(_off11); set => WriteByte(_off11, value); }

        private const int _off12 = 0x51;
        public byte Field51 { get => ReadByte(_off12); set => WriteByte(_off12, value); }

        private const int _off13 = 0x52;
        public ushort Field52 { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x54;
        public ItemType Type { get => (ItemType)ReadUInt16(_off14); set => WriteUInt16(_off14, (ushort)value); }

        private const int _off15 = 0x56;
        public ushort HasBase { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x58;
        public ushort MaxSpawnCount { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x5A;
        public ushort SpawnInterval { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x5C;
        public ushort SpawnDelay { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x5E;
        public ushort SpawnCount { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x60; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off20); set => WritePointer(_off20, value); }

        private const int _off21 = 0x64;
        public CModel BaseModel { get; }

        private const int _off22 = 0xAC; // CItem*
        public IntPtr ItemInstance { get => ReadPointer(_off22); set => WritePointer(_off22, value); }

        private const int _off23 = 0xB0; // CEntity*
        public IntPtr SomeEntity { get => ReadPointer(_off23); set => WritePointer(_off23, value); }

        private const int _off24 = 0xB4;
        public int FieldB4 { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        public CItemSpawn(Memory memory, int address) : base(memory, address)
        {
            BaseModel = new CModel(memory, address + _off21);
        }

        public CItemSpawn(Memory memory, IntPtr address) : base(memory, address)
        {
            BaseModel = new CModel(memory, address + _off21);
        }
    }

    public class CItemInstance : CEntity
    {
        private const int _off0 = 0x18;
        public ushort ParentId { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x1A;
        public ushort Field1A { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x1C;
        public Vector3 Field1C { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x28;
        public int Field28 { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x2C;
        public int Field2C { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x30;
        public int Field30 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x34;
        public int Field34 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x38;
        public int Field38 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x3C;
        public int Field3C { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x40;
        public Vector3 Pos { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x4C;
        public ItemType Type { get => (ItemType)ReadUInt16(_off10); set => WriteUInt16(_off10, (ushort)value); }

        private const int _off11 = 0x4E;
        public short DespawnTimer { get => ReadInt16(_off11); set => WriteInt16(_off11, value); }

        private const int _off12 = 0x50;
        public ushort RotationAngle { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x52;
        public ushort LinkDone { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x54; // EffectEntry*
        public IntPtr Effect { get => ReadPointer(_off14); set => WritePointer(_off14, value); }

        private const int _off15 = 0x58;
        public CModel Model { get; }

        private const int _off16 = 0xA0;
        public SfxParameters SfxParameters { get; }

        private const int _off17 = 0xA4; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off17); set => WritePointer(_off17, value); }

        private const int _off18 = 0xA8; // CItemSpawn*
        public IntPtr ItemBase { get => ReadPointer(_off18); set => WritePointer(_off18, value); }

        private const int _off19 = 0xAC;
        public int FieldAC { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        public CItemInstance(Memory memory, int address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off15);
            SfxParameters = new SfxParameters(memory, address + _off16);
        }

        public CItemInstance(Memory memory, IntPtr address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off15);
            SfxParameters = new SfxParameters(memory, address + _off16);
        }
    }

    public class CEnemySpawn : CEntity
    {
        private const int _off0 = 0x18;
        public EnemyType EnemyType { get => (EnemyType)ReadByte(_off0); set => WriteByte(_off0, (byte)value); }

        private const int _off1 = 0x19;
        public byte Padding19 { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x1A;
        public ushort Padding1A { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off4 = 0x1C; // NodeRef*
        public IntPtr RoomNodeRef { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x20; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x24;
        public ushort SomeEntId { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x26;
        public ushort Field26 { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x28;
        public Vector3 Field28 { get => ReadVec3(_off8); set => WriteVec3(_off8, value); }

        private const int _off9 = 0x34;
        public Vector3 Field34 { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x40;
        public Vector3 Field40 { get => ReadVec3(_off10); set => WriteVec3(_off10, value); }

        private const int _off11 = 0x4C;
        public Vector3 Pos { get => ReadVec3(_off11); set => WriteVec3(_off11, value); }

        private const int _off12 = 0x58;
        public Vector3 Vec2 { get => ReadVec3(_off12); set => WriteVec3(_off12, value); }

        private const int _off13 = 0x64;
        public Vector3 Vec1 { get => ReadVec3(_off13); set => WriteVec3(_off13, value); }

        private const int _off14 = 0x70;
        public byte Flags { get => ReadByte(_off14); set => WriteByte(_off14, value); }

        private const int _off15 = 0x71;
        public byte SomeCount { get => ReadByte(_off15); set => WriteByte(_off15, value); }

        private const int _off16 = 0x72;
        public byte Field72 { get => ReadByte(_off16); set => WriteByte(_off16, value); }

        private const int _off17 = 0x73;
        public byte Field73 { get => ReadByte(_off17); set => WriteByte(_off17, value); }

        private const int _off18 = 0x74;
        public ushort CooldownTimer { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x76;
        public ushort Field76 { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x78;
        public int ActiveDistSqr { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0x7C; // EntityIdOrRef
        public IntPtr Entity1 { get => ReadPointer(_off21); set => WritePointer(_off21, value); }

        private const int _off22 = 0x80; // EntityIdOrRef
        public IntPtr Entity2 { get => ReadPointer(_off22); set => WritePointer(_off22, value); }

        private const int _off23 = 0x84; // EntityIdOrRef
        public IntPtr Entity3 { get => ReadPointer(_off23); set => WritePointer(_off23, value); }

        private const int _off24 = 0x88; // EntityData*
        public IntPtr Data { get => ReadPointer(_off24); set => WritePointer(_off24, value); }

        public CEnemySpawn(Memory memory, int address) : base(memory, address)
        {
        }

        public CEnemySpawn(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CTriggerVolume : CEntity
    {
        private const int _off0 = 0x18;
        public byte Flags { get => ReadByte(_off0); set => WriteByte(_off0, value); }

        private const int _off1 = 0x19;
        public byte Type { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x1A;
        public ushort TriggerDelay { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x1C;
        public ushort RequiredStateBit { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x1E;
        public ushort Field1E { get => ReadUInt16(_off4); set => WriteUInt16(_off4, value); }

        private const int _off5 = 0x20;
        public int TriggerThreshold { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x24;
        public int TriggersNeeded { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x28;
        public TriggerFlags TriggerFlags { get => (TriggerFlags)ReadUInt32(_off7); set => WriteUInt32(_off7, (uint)value); }

        private const int _off8 = 0x2C; // EntityData*
        public IntPtr Data { get => ReadPointer(_off8); set => WritePointer(_off8, value); }

        private const int _off9 = 0x30; // EntityIdOrRef
        public IntPtr Parent { get => ReadPointer(_off9); set => WritePointer(_off9, value); }

        private const int _off10 = 0x34; // EntityIdOrRef
        public IntPtr Child { get => ReadPointer(_off10); set => WritePointer(_off10, value); }

        private const int _off11 = 0x38;
        public CollisionVolume Volume { get; }

        public CTriggerVolume(Memory memory, int address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off11);
        }

        public CTriggerVolume(Memory memory, IntPtr address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off11);
        }
    }

    public class CAreaVolume : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Pos { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec2 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Vec1 { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public byte Active { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x3D;
        public byte AllowMultiple { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x3E;
        public byte EventDelay { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x3F; // byte[4]
        public ByteArray TriggeredSlots { get; }

        private const int _off7 = 0x43; // byte[4]
        public ByteArray PrioritySlots { get; }

        private const int _off8 = 0x47;
        public byte Padding47 { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0x48;
        public Message InsideEventId { get => (Message)ReadUInt32(_off9); set => WriteUInt32(_off9, (uint)value); }

        private const int _off10 = 0x4C;
        public int InsideEventParam1 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x50;
        public int InsideEventParam2 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x54;
        public Message ExitEventId { get => (Message)ReadUInt32(_off12); set => WriteUInt32(_off12, (uint)value); }

        private const int _off13 = 0x58;
        public int ExitEventParam1 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x5C;
        public int ExitEventParam2 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x60;
        public TriggerFlags TriggerFlags { get => (TriggerFlags)ReadUInt32(_off15); set => WriteUInt32(_off15, (uint)value); }

        private const int _off16 = 0x64;
        public ushort Priority { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x66;
        public ushort Cooldown { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x68; // ushort[4]
        public UInt16Array CooldownSlots { get; }

        private const int _off19 = 0x70; // EntityIdOrRef
        public IntPtr Parent { get => ReadPointer(_off19); set => WritePointer(_off19, value); }

        private const int _off20 = 0x74; // EntityIdOrRef
        public IntPtr Child { get => ReadPointer(_off20); set => WritePointer(_off20, value); }

        private const int _off21 = 0x78; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off21); set => WritePointer(_off21, value); }

        private const int _off22 = 0x7C;
        public CollisionVolume Volume { get; }

        public CAreaVolume(Memory memory, int address) : base(memory, address)
        {
            TriggeredSlots = new ByteArray(memory, address + _off6, 4);
            PrioritySlots = new ByteArray(memory, address + _off7, 4);
            CooldownSlots = new UInt16Array(memory, address + _off18, 4);
            Volume = new CollisionVolume(memory, address + _off22);
        }

        public CAreaVolume(Memory memory, IntPtr address) : base(memory, address)
        {
            TriggeredSlots = new ByteArray(memory, address + _off6, 4);
            PrioritySlots = new ByteArray(memory, address + _off7, 4);
            CooldownSlots = new UInt16Array(memory, address + _off18, 4);
            Volume = new CollisionVolume(memory, address + _off22);
        }
    }

    public class CJumpPad : CEntity
    {
        private const int _off0 = 0x18;
        public ushort ParentId { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x1A;
        public ushort Field1A { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x1C;
        public Vector3 Field1C { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x28;
        public int Field28 { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x2C;
        public int Field2C { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x30;
        public int Field30 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x34;
        public int Field34 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x38;
        public int Field38 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x3C;
        public int Field3C { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x40;
        public Vector3 Pos { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x4C;
        public Vector3 BaseVec2 { get => ReadVec3(_off10); set => WriteVec3(_off10, value); }

        private const int _off11 = 0x58;
        public Vector3 BaseVec1 { get => ReadVec3(_off11); set => WriteVec3(_off11, value); }

        private const int _off12 = 0x64;
        public Matrix4x3 BaseMtx { get => ReadMtx43(_off12); set => WriteMtx43(_off12, value); }

        private const int _off13 = 0x94;
        public Matrix4x3 BeamMtx { get => ReadMtx43(_off13); set => WriteMtx43(_off13, value); }

        private const int _off14 = 0xC4; // EntityData*
        public IntPtr Data { get => ReadPointer(_off14); set => WritePointer(_off14, value); }

        private const int _off15 = 0xC8;
        public ushort CooldownTime { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0xCA;
        public ushort CooldownTimer { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0xCC;
        public byte UsedState { get => ReadByte(_off17); set => WriteByte(_off17, value); }

        private const int _off18 = 0xCD;
        public byte Flags1 { get => ReadByte(_off18); set => WriteByte(_off18, value); }

        private const int _off19 = 0xCE;
        public ushort FieldCE { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0xD0;
        public TriggerFlags TriggerFlags { get => (TriggerFlags)ReadUInt32(_off20); set => WriteUInt32(_off20, (uint)value); }

        private const int _off21 = 0xD4;
        public ushort FieldD4 { get => ReadUInt16(_off21); set => WriteUInt16(_off21, value); }

        private const int _off22 = 0xD6;
        public ushort Timer { get => ReadUInt16(_off22); set => WriteUInt16(_off22, value); }

        private const int _off23 = 0xD8;
        public Vector3 BeamVec { get => ReadVec3(_off23); set => WriteVec3(_off23, value); }

        private const int _off24 = 0xE4;
        public CollisionVolume FieldE4 { get; }

        private const int _off25 = 0x124;
        public CollisionVolume Volume { get; }

        private const int _off26 = 0x164; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off26); set => WritePointer(_off26, value); }

        private const int _off27 = 0x168;
        public CModel BaseModel { get; }

        private const int _off28 = 0x1B0;
        public CModel BeamModel { get; }

        private const int _off29 = 0x1F8;
        public int BaseId { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0x1FC;
        public int BeamId { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0x200; // NodeDataStruct3*
        public IntPtr NodedataRelated { get => ReadPointer(_off31); set => WritePointer(_off31, value); }

        public CJumpPad(Memory memory, int address) : base(memory, address)
        {
            FieldE4 = new CollisionVolume(memory, address + _off24);
            Volume = new CollisionVolume(memory, address + _off25);
            BaseModel = new CModel(memory, address + _off27);
            BeamModel = new CModel(memory, address + _off28);
        }

        public CJumpPad(Memory memory, IntPtr address) : base(memory, address)
        {
            FieldE4 = new CollisionVolume(memory, address + _off24);
            Volume = new CollisionVolume(memory, address + _off25);
            BaseModel = new CModel(memory, address + _off27);
            BeamModel = new CModel(memory, address + _off28);
        }
    }

    public class CPointModule : CEntity
    {
        private const int _off0 = 0x18;
        public int Field18 { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x1C;
        public int Field1C { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x20;
        public int Field20 { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        private const int _off3 = 0x24;
        public int Field24 { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x28; // WeaponInfo*
        public IntPtr Field28 { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x2C; // __int16*
        public IntPtr Field2C { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x30;
        public int Field30 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x34;
        public int Field34 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x38; // NodeRef*
        public IntPtr Field38 { get => ReadPointer(_off8); set => WritePointer(_off8, value); }

        private const int _off9 = 0x3C;
        public int Flags { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        public CPointModule(Memory memory, int address) : base(memory, address)
        {
        }

        public CPointModule(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CMorphCamera : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Pos { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public CollisionVolume Volume { get; }

        private const int _off2 = 0x64; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        public CMorphCamera(Memory memory, int address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off1);
        }

        public CMorphCamera(Memory memory, IntPtr address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off1);
        }
    }

    public class COctolithFlag : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Vec2 { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec1 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Pos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public Vector3 BasePos { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x48;
        public int HeightBob { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x4C;
        public byte TeamIndex { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x4D;
        public byte Flags { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x4E;
        public ushort Field4E { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x50;
        public int DespawnTimer { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x54; // CPlayer*
        public IntPtr Player { get => ReadPointer(_off9); set => WritePointer(_off9, value); }

        private const int _off10 = 0x58; // CPlayer*
        public IntPtr LastPlayer { get => ReadPointer(_off10); set => WritePointer(_off10, value); }

        private const int _off11 = 0x5C;
        public CModel BaseModel { get; }

        private const int _off12 = 0xA4;
        public CModel OctoModel { get; }

        private const int _off13 = 0xEC; // NodeDataStruct3*
        public IntPtr NodedataRelated { get => ReadPointer(_off13); set => WritePointer(_off13, value); }

        private const int _off14 = 0xF0;
        public int FieldF0 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        public COctolithFlag(Memory memory, int address) : base(memory, address)
        {
            BaseModel = new CModel(memory, address + _off11);
            OctoModel = new CModel(memory, address + _off12);
        }

        public COctolithFlag(Memory memory, IntPtr address) : base(memory, address)
        {
            BaseModel = new CModel(memory, address + _off11);
            OctoModel = new CModel(memory, address + _off12);
        }
    }

    public class CFlagBase : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Vec2 { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec1 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Pos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public CollisionVolume Volume { get; }

        private const int _off4 = 0x7C;
        public byte TeamId { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x7D;
        public byte Field7D { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x7E;
        public ushort Field7E { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x80;
        public CModel Model { get; }

        private const int _off8 = 0xC8; // NodeDataStruct3*
        public IntPtr NodedataRelated { get => ReadPointer(_off8); set => WritePointer(_off8, value); }

        public CFlagBase(Memory memory, int address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off3);
            Model = new CModel(memory, address + _off7);
        }

        public CFlagBase(Memory memory, IntPtr address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off3);
            Model = new CModel(memory, address + _off7);
        }
    }

    public class CTeleporter : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Vec2 { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec1 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Pos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public Vector3 TargetPos { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x48;
        public byte Flags { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x49;
        public byte Field49 { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x4A;
        public byte ArtifactId { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x4B;
        public byte Field4B { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x4C;
        public byte Field4C { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0x4D;
        public byte Field4D { get => ReadByte(_off9); set => WriteByte(_off9, value); }

        private const int _off10 = 0x4E;
        public ushort Field4E { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x50;
        public int ConnectorId { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x54; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off12); set => WritePointer(_off12, value); }

        private const int _off13 = 0x58; // NodeRef*
        public IntPtr Node { get => ReadPointer(_off13); set => WritePointer(_off13, value); }

        private const int _off14 = 0x5C;
        public CModel TeleModel { get; }

        private const int _off15 = 0xA4;
        public CModel ArtifactModel { get; }

        private const int _off16 = 0xEC;
        public SfxParameters SfxParameters { get; }

        public CTeleporter(Memory memory, int address) : base(memory, address)
        {
            TeleModel = new CModel(memory, address + _off14);
            ArtifactModel = new CModel(memory, address + _off15);
            SfxParameters = new SfxParameters(memory, address + _off16);
        }

        public CTeleporter(Memory memory, IntPtr address) : base(memory, address)
        {
            TeleModel = new CModel(memory, address + _off14);
            ArtifactModel = new CModel(memory, address + _off15);
            SfxParameters = new SfxParameters(memory, address + _off16);
        }
    }

    public class CNodeDefense : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Vec2 { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec1 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Pos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public int Rotation { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x40;
        public int RotSpeed { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x44;
        public CollisionVolume Volume { get; }

        private const int _off6 = 0x84;
        public byte TeamIndex { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x85;
        public byte OccupyingTeam { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x86;
        public byte OccupyFlags { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0x87;
        public byte Occupied { get => ReadByte(_off9); set => WriteByte(_off9, value); }

        private const int _off10 = 0x88;
        public byte Flags { get => ReadByte(_off10); set => WriteByte(_off10, value); }

        private const int _off11 = 0x89;
        public byte Field89 { get => ReadByte(_off11); set => WriteByte(_off11, value); }

        private const int _off12 = 0x8A;
        public ushort Field8A { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x8C;
        public int Progress { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x90;
        public int Field90 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x94; // CPlayer*
        public IntPtr SomePlayer { get => ReadPointer(_off15); set => WritePointer(_off15, value); }

        private const int _off16 = 0x98;
        public SfxParameters SfxParameters { get; }

        private const int _off17 = 0x9C;
        public CModel RingModel { get; }

        private const int _off18 = 0xE4;
        public CModel NodeModel { get; }

        private const int _off19 = 0x12C; // NodeDataStruct3*
        public IntPtr NodedataRelated { get => ReadPointer(_off19); set => WritePointer(_off19, value); }

        public CNodeDefense(Memory memory, int address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off5);
            SfxParameters = new SfxParameters(memory, address + _off16);
            RingModel = new CModel(memory, address + _off17);
            NodeModel = new CModel(memory, address + _off18);
        }

        public CNodeDefense(Memory memory, IntPtr address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off5);
            SfxParameters = new SfxParameters(memory, address + _off16);
            RingModel = new CModel(memory, address + _off17);
            NodeModel = new CModel(memory, address + _off18);
        }
    }

    public class CLightSource : CEntity
    {
        private const int _off0 = 0x18; // EntityData*
        public IntPtr Data { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x1C;
        public CollisionVolume Volume { get; }

        public CLightSource(Memory memory, int address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off1);
        }

        public CLightSource(Memory memory, IntPtr address) : base(memory, address)
        {
            Volume = new CollisionVolume(memory, address + _off1);
        }
    }

    public class CArtifact : CEntity
    {
        private const int _off0 = 0x18;
        public ushort LinkedEntityId { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x1A;
        public ushort Field1A { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x1C;
        public Vector3 Field1C { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x28;
        public int Field28 { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x2C;
        public int Field2C { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x30;
        public int Field30 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x34;
        public int Field34 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x38;
        public int Field38 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x3C;
        public int Field3C { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x40;
        public Vector3 Vec2 { get => ReadVec3(_off9); set => WriteVec3(_off9, value); }

        private const int _off10 = 0x4C;
        public Vector3 Vec1 { get => ReadVec3(_off10); set => WriteVec3(_off10, value); }

        private const int _off11 = 0x58;
        public Vector3 Pos { get => ReadVec3(_off11); set => WriteVec3(_off11, value); }

        private const int _off12 = 0x64;
        public byte Active { get => ReadByte(_off12); set => WriteByte(_off12, value); }

        private const int _off13 = 0x65;
        public byte ModelId { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x66;
        public byte ArtifactId { get => ReadByte(_off14); set => WriteByte(_off14, value); }

        private const int _off15 = 0x67;
        public byte Field67 { get => ReadByte(_off15); set => WriteByte(_off15, value); }

        private const int _off16 = 0x68;
        public ushort FoundLinked { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x6A;
        public ushort Field6A { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x6C; // EntityIdOrRef
        public IntPtr Entity1 { get => ReadPointer(_off18); set => WritePointer(_off18, value); }

        private const int _off19 = 0x70; // EntityIdOrRef
        public IntPtr Entity2 { get => ReadPointer(_off19); set => WritePointer(_off19, value); }

        private const int _off20 = 0x74; // EntityIdOrRef
        public IntPtr Entity3 { get => ReadPointer(_off20); set => WritePointer(_off20, value); }

        private const int _off21 = 0x78; // EntityData*
        public IntPtr Data { get => ReadPointer(_off21); set => WritePointer(_off21, value); }

        private const int _off22 = 0x7C;
        public CModel ArtifactModel { get; }

        private const int _off23 = 0xC4;
        public CModel BaseModel { get; }

        private const int _off24 = 0x10C; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off24); set => WritePointer(_off24, value); }

        private const int _off25 = 0x110;
        public SfxParameters SfxParameters { get; }

        private const int _off26 = 0x114;
        public int Field114 { get => ReadInt32(_off26); set => WriteInt32(_off26, value); }

        private const int _off27 = 0x118;
        public int Field118 { get => ReadInt32(_off27); set => WriteInt32(_off27, value); }

        private const int _off28 = 0x11C;
        public int Field11C { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0x120;
        public int Field120 { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0x124;
        public int Field124 { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        public CArtifact(Memory memory, int address) : base(memory, address)
        {
            ArtifactModel = new CModel(memory, address + _off22);
            BaseModel = new CModel(memory, address + _off23);
            SfxParameters = new SfxParameters(memory, address + _off25);
        }

        public CArtifact(Memory memory, IntPtr address) : base(memory, address)
        {
            ArtifactModel = new CModel(memory, address + _off22);
            BaseModel = new CModel(memory, address + _off23);
            SfxParameters = new SfxParameters(memory, address + _off25);
        }
    }

    public class CCameraSequence : CEntity
    {
        private const int _off0 = 0x18; // EntityData*
        public IntPtr Data { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x1C; // CEntity*
        public IntPtr Entity1 { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x20; // CEntity*
        public IntPtr Entity2 { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0x24; // CEntity*
        public IntPtr EventTarget { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x28;
        public byte Flags { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x29;
        public byte Padding29 { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x2A;
        public ushort DelayTimer { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        public CCameraSequence(Memory memory, int address) : base(memory, address)
        {
        }

        public CCameraSequence(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CForceField : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Normal { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public byte Flags { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x25;
        public byte Alpha { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x26;
        public ushort Field26 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x28; // EntityForceField*
        public IntPtr Data { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x2C; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x30; // CEnemy*
        public IntPtr Lock { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x34;
        public Vector3 Vector2 { get => ReadVec3(_off7); set => WriteVec3(_off7, value); }

        private const int _off8 = 0x40;
        public int Field40 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x44;
        public CModel Model { get; }

        public CForceField(Memory memory, int address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off9);
        }

        public CForceField(Memory memory, IntPtr address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off9);
        }
    }

    public class CBeamEffect : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Vec1 { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec2 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Pos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C; // VecFx32*
        public IntPtr DrawOffset { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x40;
        public Vector3 Speed { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0x4C;
        public byte Flags { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x4D;
        public byte BeamType { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x4E;
        public ushort Lifespan { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x50;
        public ushort Age { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x52;
        public ushort Field52 { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x54;
        public int Field54 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x58;
        public int Field58 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x5C;
        public int Field5C { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x60;
        public int Field60 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x64;
        public int Field64 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x68;
        public int ScaleX { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x6C;
        public int ScaleY { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x70;
        public int ScaleZ { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x74;
        public int DrawDist { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x78;
        public CModel Model { get; }

        private const int _off20 = 0xC0; // MtxFx43*
        public IntPtr EffMtxPtr { get => ReadPointer(_off20); set => WritePointer(_off20, value); }

        public CBeamEffect(Memory memory, int address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off19);
        }

        public CBeamEffect(Memory memory, IntPtr address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off19);
        }
    }

    public class CBomb : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Pos { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Vec1 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Vec2 { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public Vector3 SpeedMaybe { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x48;
        public byte BombType { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x49;
        public byte SiblingCount { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x4A;
        public byte Flags { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x4B;
        public byte Field4B { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x4C;
        public ushort Countdown { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x4E;
        public ushort Field4E { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x50;
        public ushort Field50 { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x52;
        public ushort Field52 { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x54;
        public int Field54 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x58;
        public int Field58 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x5C;
        public int Field5C { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x60; // CPlayer*
        public IntPtr Owner { get => ReadPointer(_off15); set => WritePointer(_off15, value); }

        private const int _off16 = 0x64; // CPlayer*
        public IntPtr OwnerSylux { get => ReadPointer(_off16); set => WritePointer(_off16, value); }

        private const int _off17 = 0x68;
        public CModel Model { get; }

        private const int _off18 = 0xB0; // NodeRef**
        public IntPtr RoomNodeRef { get => ReadPointer(_off18); set => WritePointer(_off18, value); }

        private const int _off19 = 0xB4;
        public SfxParameters SfxParameters { get; }

        public CBomb(Memory memory, int address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off17);
            SfxParameters = new SfxParameters(memory, address + _off19);
        }

        public CBomb(Memory memory, IntPtr address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off17);
            SfxParameters = new SfxParameters(memory, address + _off19);
        }
    }

    public class CHalfturret : CEntity
    {
        private const int _off0 = 0x18;
        public Vector3 Vec2 { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0x24;
        public Vector3 Field24 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x30;
        public Vector3 Pos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x3C;
        public int Field3C { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x40; // CPlayer*
        public IntPtr Owner { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x44; // CPlayer*
        public IntPtr Target { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x48;
        public LightInfo LightInfo { get; }

        private const int _off7 = 0x67;
        public byte Field67 { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x68;
        public EquipInfoPtr EquipInfo { get; }

        private const int _off9 = 0x7C; // EffectEntry*
        public IntPtr BurnEffect { get => ReadPointer(_off9); set => WritePointer(_off9, value); }

        private const int _off10 = 0x80;
        public CModel Model { get; }

        private const int _off11 = 0xC8; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off11); set => WritePointer(_off11, value); }

        private const int _off12 = 0xCC;
        public int FieldCC { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0xD0;
        public ushort Health { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0xD2;
        public ushort FieldD2 { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0xD4;
        public ushort BurnTimer { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0xD6;
        public ushort FieldD6 { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0xD8;
        public byte FieldD8 { get => ReadByte(_off17); set => WriteByte(_off17, value); }

        private const int _off18 = 0xD9;
        public byte Frozen { get => ReadByte(_off18); set => WriteByte(_off18, value); }

        private const int _off19 = 0xDA;
        public byte FieldDA { get => ReadByte(_off19); set => WriteByte(_off19, value); }

        private const int _off20 = 0xDB;
        public byte FieldDB { get => ReadByte(_off20); set => WriteByte(_off20, value); }

        private const int _off21 = 0xDC;
        public byte FieldDC { get => ReadByte(_off21); set => WriteByte(_off21, value); }

        private const int _off22 = 0xDD;
        public byte FieldDD { get => ReadByte(_off22); set => WriteByte(_off22, value); }

        private const int _off23 = 0xDE;
        public ushort FieldDE { get => ReadUInt16(_off23); set => WriteUInt16(_off23, value); }

        private const int _off24 = 0xE0;
        public int FieldE0 { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        public CHalfturret(Memory memory, int address) : base(memory, address)
        {
            LightInfo = new LightInfo(memory, address + _off6);
            EquipInfo = new EquipInfoPtr(memory, address + _off8);
            Model = new CModel(memory, address + _off10);
        }

        public CHalfturret(Memory memory, IntPtr address) : base(memory, address)
        {
            LightInfo = new LightInfo(memory, address + _off6);
            EquipInfo = new EquipInfoPtr(memory, address + _off8);
            Model = new CModel(memory, address + _off10);
        }
    }

    public class CPlayer : CEntity
    {
        private const int _off0 = 0x18;
        public int Field18 { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x1C;
        public Vector3 Pos { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x28;
        public Vector3 PrevPos { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x34;
        public Vector3 Speed { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x40;
        public Vector3 PrevSpeed { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0x4C;
        public Vector3 Vec2 { get => ReadVec3(_off5); set => WriteVec3(_off5, value); }

        private const int _off6 = 0x58;
        public Vector3 GunEffVec2 { get => ReadVec3(_off6); set => WriteVec3(_off6, value); }

        private const int _off7 = 0x64;
        public Vector3 Vec1 { get => ReadVec3(_off7); set => WriteVec3(_off7, value); }

        private const int _off8 = 0x70;
        public Vector3 Field70 { get => ReadVec3(_off8); set => WriteVec3(_off8, value); }

        private const int _off9 = 0x7C;
        public int Field7C { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x80;
        public int Field80 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x84;
        public int Field84 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x88;
        public int Field88 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x8C;
        public int Field8C { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x90;
        public int HSpeedMag { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x94;
        public int Field94 { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x98;
        public int GravityValue { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x9C;
        public Vector3 GunEffVec1 { get => ReadVec3(_off17); set => WriteVec3(_off17, value); }

        private const int _off18 = 0xA8;
        public Vector3 AimTargetPos { get => ReadVec3(_off18); set => WriteVec3(_off18, value); }

        private const int _off19 = 0xB4;
        public Vector3 FieldB4 { get => ReadVec3(_off19); set => WriteVec3(_off19, value); }

        private const int _off20 = 0xC0;
        public Vector3 FieldC0 { get => ReadVec3(_off20); set => WriteVec3(_off20, value); }

        private const int _off21 = 0xCC;
        public Vector3 SomeSpeedLoss { get => ReadVec3(_off21); set => WriteVec3(_off21, value); }

        private const int _off22 = 0xD8;
        public ushort SomeSpeedCounter { get => ReadUInt16(_off22); set => WriteUInt16(_off22, value); }

        private const int _off23 = 0xDA;
        public ushort Energy { get => ReadUInt16(_off23); set => WriteUInt16(_off23, value); }

        private const int _off24 = 0xDC;
        public ushort EnergyCap { get => ReadUInt16(_off24); set => WriteUInt16(_off24, value); }

        private const int _off25 = 0xDE;
        public ushort RecoveryTicksMaybe { get => ReadUInt16(_off25); set => WriteUInt16(_off25, value); }

        private const int _off26 = 0xE0;
        public byte SomeTimer1 { get => ReadByte(_off26); set => WriteByte(_off26, value); }

        private const int _off27 = 0xE1;
        public byte SomeTimer2 { get => ReadByte(_off27); set => WriteByte(_off27, value); }

        private const int _off28 = 0xE2;
        public byte DeathCountdownMaybe { get => ReadByte(_off28); set => WriteByte(_off28, value); }

        private const int _off29 = 0xE3;
        public byte FieldE3 { get => ReadByte(_off29); set => WriteByte(_off29, value); }

        private const int _off30 = 0xE4;
        public ushort FieldE4 { get => ReadUInt16(_off30); set => WriteUInt16(_off30, value); }

        private const int _off31 = 0xE6;
        public ushort FieldE6 { get => ReadUInt16(_off31); set => WriteUInt16(_off31, value); }

        private const int _off32 = 0xE8;
        public int FieldE8 { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0xEC;
        public Vector3 Dir2 { get => ReadVec3(_off33); set => WriteVec3(_off33, value); }

        private const int _off34 = 0xF8;
        public ushort JumpPadCountdown { get => ReadUInt16(_off34); set => WriteUInt16(_off34, value); }

        private const int _off35 = 0xFA;
        public byte JumpPadMin5s { get => ReadByte(_off35); set => WriteByte(_off35, value); }

        private const int _off36 = 0xFB;
        public byte TimeSinceJumpPadMaybe { get => ReadByte(_off36); set => WriteByte(_off36, value); }

        private const int _off37 = 0xFC;
        public int FieldFC { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0x100; // ushort[2]
        public UInt16Array Field100 { get; }

        private const int _off39 = 0x104;
        public byte Field104 { get => ReadByte(_off39); set => WriteByte(_off39, value); }

        private const int _off40 = 0x105;
        public byte Field105 { get => ReadByte(_off40); set => WriteByte(_off40, value); }

        private const int _off41 = 0x106;
        public ushort Field106 { get => ReadUInt16(_off41); set => WriteUInt16(_off41, value); }

        private const int _off42 = 0x108;
        public CollisionVolume Collision { get; }

        private const int _off43 = 0x148;
        public byte BoostBallCharge { get => ReadByte(_off43); set => WriteByte(_off43, value); }

        private const int _off44 = 0x149;
        public byte SomeDamage { get => ReadByte(_off44); set => WriteByte(_off44, value); }

        private const int _off45 = 0x14A;
        public byte BoostCooldown { get => ReadByte(_off45); set => WriteByte(_off45, value); }

        private const int _off46 = 0x14B;
        public byte Field14B { get => ReadByte(_off46); set => WriteByte(_off46, value); }

        private const int _off47 = 0x14C;
        public ushort UniversalAmmo { get => ReadUInt16(_off47); set => WriteUInt16(_off47, value); }

        private const int _off48 = 0x14E;
        public ushort Missiles { get => ReadUInt16(_off48); set => WriteUInt16(_off48, value); }

        private const int _off49 = 0x150;
        public ushort UniversalAmmoCap { get => ReadUInt16(_off49); set => WriteUInt16(_off49, value); }

        private const int _off50 = 0x152;
        public ushort MissilesCap { get => ReadUInt16(_off50); set => WriteUInt16(_off50, value); }

        private const int _off51 = 0x154;
        public byte Field154 { get => ReadByte(_off51); set => WriteByte(_off51, value); }

        private const int _off52 = 0x155;
        public byte Field155 { get => ReadByte(_off52); set => WriteByte(_off52, value); }

        private const int _off53 = 0x156;
        public ushort Field156 { get => ReadUInt16(_off53); set => WriteUInt16(_off53, value); }

        private const int _off54 = 0x158;
        public byte WeaponSlot0 { get => ReadByte(_off54); set => WriteByte(_off54, value); }

        private const int _off55 = 0x159;
        public byte WeaponSlot1 { get => ReadByte(_off55); set => WriteByte(_off55, value); }

        private const int _off56 = 0x15A;
        public byte WeaponSlot2 { get => ReadByte(_off56); set => WriteByte(_off56, value); }

        private const int _off57 = 0x15B;
        public byte GunAnimation { get => ReadByte(_off57); set => WriteByte(_off57, value); }

        private const int _off58 = 0x15C;
        public CModel GunModel { get; }

        private const int _off59 = 0x1A4;
        public CModel FrozenModel { get; }

        private const int _off60 = 0x1EC;
        public Vector3 Field1EC { get => ReadVec3(_off60); set => WriteVec3(_off60, value); }

        private const int _off61 = 0x1F8;
        public Vector3 MuzzlePos { get => ReadVec3(_off61); set => WriteVec3(_off61, value); }

        private const int _off62 = 0x204; // EffectEntry*
        public IntPtr FurlEffect { get => ReadPointer(_off62); set => WritePointer(_off62, value); }

        private const int _off63 = 0x208; // EffectEntry*
        public IntPtr EffectBoost { get => ReadPointer(_off63); set => WritePointer(_off63, value); }

        private const int _off64 = 0x20C; // EffectEntry*
        public IntPtr EffectMuzzle { get => ReadPointer(_off64); set => WritePointer(_off64, value); }

        private const int _off65 = 0x210; // EffectEntry*
        public IntPtr EffectCharge { get => ReadPointer(_off65); set => WritePointer(_off65, value); }

        private const int _off66 = 0x214; // EffectEntry*
        public IntPtr EffectDblDmg { get => ReadPointer(_off66); set => WritePointer(_off66, value); }

        private const int _off67 = 0x218; // EffectEntry*
        public IntPtr EffectDeathalt { get => ReadPointer(_off67); set => WritePointer(_off67, value); }

        private const int _off68 = 0x21C; // Node*[2]
        public IntPtrArray SpineNode { get; }

        private const int _off69 = 0x224; // Node*[2]
        public IntPtrArray ShootNode { get; }

        private const int _off70 = 0x22C;
        public CModel Biped1 { get; }

        private const int _off71 = 0x274;
        public CModel Biped2 { get; }

        private const int _off72 = 0x2BC;
        public ushort Field2BC { get => ReadUInt16(_off72); set => WriteUInt16(_off72, value); }

        private const int _off73 = 0x2BE;
        public ushort Field2BE { get => ReadUInt16(_off73); set => WriteUInt16(_off73, value); }

        private const int _off74 = 0x2C0;
        public CModel AltForm { get; }

        private const int _off75 = 0x308;
        public CModel GunSmoke { get; }

        private const int _off76 = 0x350;
        public byte SmokeAlphaMaybe { get => ReadByte(_off76); set => WriteByte(_off76, value); }

        private const int _off77 = 0x351;
        public byte Field351 { get => ReadByte(_off77); set => WriteByte(_off77, value); }

        private const int _off78 = 0x352;
        public ushort Field352 { get => ReadUInt16(_off78); set => WriteUInt16(_off78, value); }

        private const int _off79 = 0x354; // MtxFx43*
        public IntPtr Field354 { get => ReadPointer(_off79); set => WritePointer(_off79, value); }

        private const int _off80 = 0x358; // CEnemy*
        public IntPtr AttachedEnemy { get => ReadPointer(_off80); set => WritePointer(_off80, value); }

        private const int _off81 = 0x35C; // CEntity*
        public IntPtr Field35C { get => ReadPointer(_off81); set => WritePointer(_off81, value); }

        private const int _off82 = 0x360;
        public byte Field360 { get => ReadByte(_off82); set => WriteByte(_off82, value); }

        private const int _off83 = 0x361;
        public byte Field361 { get => ReadByte(_off83); set => WriteByte(_off83, value); }

        private const int _off84 = 0x362;
        public ushort Field362 { get => ReadUInt16(_off84); set => WriteUInt16(_off84, value); }

        private const int _off85 = 0x364;
        public PlayerControls Controls { get; }

        private const int _off86 = 0x400;
        public Hunter HunterId { get => (Hunter)ReadByte(_off86); set => WriteByte(_off86, (byte)value); }

        private const int _off87 = 0x401;
        public byte Field401 { get => ReadByte(_off87); set => WriteByte(_off87, value); }

        private const int _off88 = 0x402;
        public ushort Field402 { get => ReadUInt16(_off88); set => WriteUInt16(_off88, value); }

        private const int _off89 = 0x404; // Player404*
        public IntPtr Field404 { get => ReadPointer(_off89); set => WritePointer(_off89, value); }

        private const int _off90 = 0x408;
        public int Field408 { get => ReadInt32(_off90); set => WriteInt32(_off90, value); }

        private const int _off91 = 0x40C;
        public int Field40C { get => ReadInt32(_off91); set => WriteInt32(_off91, value); }

        private const int _off92 = 0x410;
        public int Field410 { get => ReadInt32(_off92); set => WriteInt32(_off92, value); }

        private const int _off93 = 0x414;
        public int Field414 { get => ReadInt32(_off93); set => WriteInt32(_off93, value); }

        private const int _off94 = 0x418;
        public int Field418 { get => ReadInt32(_off94); set => WriteInt32(_off94, value); }

        private const int _off95 = 0x41C;
        public int Field41C { get => ReadInt32(_off95); set => WriteInt32(_off95, value); }

        private const int _off96 = 0x420;
        public int Field420 { get => ReadInt32(_off96); set => WriteInt32(_off96, value); }

        private const int _off97 = 0x424;
        public int Field424 { get => ReadInt32(_off97); set => WriteInt32(_off97, value); }

        private const int _off98 = 0x428;
        public int Field428 { get => ReadInt32(_off98); set => WriteInt32(_off98, value); }

        private const int _off99 = 0x42C;
        public int Field42C { get => ReadInt32(_off99); set => WriteInt32(_off99, value); }

        private const int _off100 = 0x430;
        public int Field430 { get => ReadInt32(_off100); set => WriteInt32(_off100, value); }

        private const int _off101 = 0x434;
        public byte TimeSinceShot { get => ReadByte(_off101); set => WriteByte(_off101, value); }

        private const int _off102 = 0x435;
        public byte Field435 { get => ReadByte(_off102); set => WriteByte(_off102, value); }

        private const int _off103 = 0x436;
        public ushort Field436 { get => ReadUInt16(_off103); set => WriteUInt16(_off103, value); }

        private const int _off104 = 0x438;
        public ushort Field438 { get => ReadUInt16(_off104); set => WriteUInt16(_off104, value); }

        private const int _off105 = 0x43A;
        public ushort Field43A { get => ReadUInt16(_off105); set => WriteUInt16(_off105, value); }

        private const int _off106 = 0x43C;
        public ushort ShockCoilTimer { get => ReadUInt16(_off106); set => WriteUInt16(_off106, value); }

        private const int _off107 = 0x43E;
        public ushort Field43E { get => ReadUInt16(_off107); set => WriteUInt16(_off107, value); }

        private const int _off108 = 0x440; // CPlayer*
        public IntPtr ShockCoilTarget { get => ReadPointer(_off108); set => WritePointer(_off108, value); }

        private const int _off109 = 0x444;
        public byte TimeSinceDmg { get => ReadByte(_off109); set => WriteByte(_off109, value); }

        private const int _off110 = 0x445;
        public byte TimeSincePickup { get => ReadByte(_off110); set => WriteByte(_off110, value); }

        private const int _off111 = 0x446;
        public byte TimeSinceHeal { get => ReadByte(_off111); set => WriteByte(_off111, value); }

        private const int _off112 = 0x447;
        public byte Field447 { get => ReadByte(_off112); set => WriteByte(_off112, value); }

        private const int _off113 = 0x448;
        public byte Field448 { get => ReadByte(_off113); set => WriteByte(_off113, value); }

        private const int _off114 = 0x449;
        public byte Field449 { get => ReadByte(_off114); set => WriteByte(_off114, value); }

        private const int _off115 = 0x44A;
        public ushort Field44A { get => ReadUInt16(_off115); set => WriteUInt16(_off115, value); }

        private const int _off116 = 0x44C;
        public int Field44C { get => ReadInt32(_off116); set => WriteInt32(_off116, value); }

        private const int _off117 = 0x450;
        public int Field450 { get => ReadInt32(_off117); set => WriteInt32(_off117, value); }

        private const int _off118 = 0x454; // NodeRef*
        public IntPtr RoomNodeRef { get => ReadPointer(_off118); set => WritePointer(_off118, value); }

        private const int _off119 = 0x458;
        public ushort RespawnTimer { get => ReadUInt16(_off119); set => WriteUInt16(_off119, value); }

        private const int _off120 = 0x45A;
        public ushort Field45A { get => ReadUInt16(_off120); set => WriteUInt16(_off120, value); }

        private const int _off121 = 0x45C; // EntityCollision*
        public IntPtr Field45C { get => ReadPointer(_off121); set => WritePointer(_off121, value); }

        private const int _off122 = 0x460;
        public ushort Field460 { get => ReadUInt16(_off122); set => WriteUInt16(_off122, value); }

        private const int _off123 = 0x462;
        public ushort Field462 { get => ReadUInt16(_off123); set => WriteUInt16(_off123, value); }

        private const int _off124 = 0x464;
        public PlayerInput Input { get; }

        private const int _off125 = 0x4AC;
        public byte Field4AC { get => ReadByte(_off125); set => WriteByte(_off125, value); }

        private const int _off126 = 0x4AD;
        public byte TeamIndex { get => ReadByte(_off126); set => WriteByte(_off126, value); }

        private const int _off127 = 0x4AE;
        public byte Field4AE { get => ReadByte(_off127); set => WriteByte(_off127, value); }

        private const int _off128 = 0x4AF;
        public byte Field4AF { get => ReadByte(_off128); set => WriteByte(_off128, value); }

        private const int _off129 = 0x4B0;
        public ushort DoubleDamageTimer { get => ReadUInt16(_off129); set => WriteUInt16(_off129, value); }

        private const int _off130 = 0x4B2;
        public ushort CloakTimer { get => ReadUInt16(_off130); set => WriteUInt16(_off130, value); }

        private const int _off131 = 0x4B4;
        public ushort DeathaltTimer { get => ReadUInt16(_off131); set => WriteUInt16(_off131, value); }

        private const int _off132 = 0x4B6;
        public ushort DisruptTimer { get => ReadUInt16(_off132); set => WriteUInt16(_off132, value); }

        private const int _off133 = 0x4B8;
        public ushort FreezeTimer { get => ReadUInt16(_off133); set => WriteUInt16(_off133, value); }

        private const int _off134 = 0x4BA;
        public byte TimeSinceFrozen { get => ReadByte(_off134); set => WriteByte(_off134, value); }

        private const int _off135 = 0x4BB;
        public byte Frozen { get => ReadByte(_off135); set => WriteByte(_off135, value); }

        private const int _off136 = 0x4BC;
        public byte CurAlpha { get => ReadByte(_off136); set => WriteByte(_off136, value); }

        private const int _off137 = 0x4BD;
        public byte TargetAlpha { get => ReadByte(_off137); set => WriteByte(_off137, value); }

        private const int _off138 = 0x4BE;
        public byte ShotCooldownRelated { get => ReadByte(_off138); set => WriteByte(_off138, value); }

        private const int _off139 = 0x4BF;
        public byte Field4BF { get => ReadByte(_off139); set => WriteByte(_off139, value); }

        private const int _off140 = 0x4C0;
        public byte Field4C0 { get => ReadByte(_off140); set => WriteByte(_off140, value); }

        private const int _off141 = 0x4C1;
        public byte Field4C1 { get => ReadByte(_off141); set => WriteByte(_off141, value); }

        private const int _off142 = 0x4C2;
        public ushort Field4C2 { get => ReadUInt16(_off142); set => WriteUInt16(_off142, value); }

        private const int _off143 = 0x4C4;
        public uint SomeFlags { get => ReadUInt32(_off143); set => WriteUInt32(_off143, value); }

        private const int _off144 = 0x4C8;
        public uint MoreFlags { get => ReadUInt32(_off144); set => WriteUInt32(_off144, value); }

        private const int _off145 = 0x4CC;
        public ushort AbilityFlags { get => ReadUInt16(_off145); set => WriteUInt16(_off145, value); }

        private const int _off146 = 0x4CE;
        public byte CurrentWeapon { get => ReadByte(_off146); set => WriteByte(_off146, value); }

        private const int _off147 = 0x4CF;
        public byte WeaponSelection { get => ReadByte(_off147); set => WriteByte(_off147, value); }

        private const int _off148 = 0x4D0;
        public byte SomeWeapon { get => ReadByte(_off148); set => WriteByte(_off148, value); }

        private const int _off149 = 0x4D1;
        public byte Field4D1 { get => ReadByte(_off149); set => WriteByte(_off149, value); }

        private const int _off150 = 0x4D2;
        public byte AvailableWeapons { get => ReadByte(_off150); set => WriteByte(_off150, value); }

        private const int _off151 = 0x4D3;
        public byte OmegaCannon { get => ReadByte(_off151); set => WriteByte(_off151, value); }

        private const int _off152 = 0x4D4;
        public byte AvailableCharges { get => ReadByte(_off152); set => WriteByte(_off152, value); }

        private const int _off153 = 0x4D5;
        public byte Field4D5 { get => ReadByte(_off153); set => WriteByte(_off153, value); }

        private const int _off154 = 0x4D6;
        public byte ViewType { get => ReadByte(_off154); set => WriteByte(_off154, value); }

        private const int _off155 = 0x4D7;
        public byte ViewPlayer { get => ReadByte(_off155); set => WriteByte(_off155, value); }

        private const int _off156 = 0x4D8;
        public byte Field4D8 { get => ReadByte(_off156); set => WriteByte(_off156, value); }

        private const int _off157 = 0x4D9;
        public byte Field4D9 { get => ReadByte(_off157); set => WriteByte(_off157, value); }

        private const int _off158 = 0x4DA;
        public ushort Field4DA { get => ReadUInt16(_off158); set => WriteUInt16(_off158, value); }

        private const int _off159 = 0x4DC;
        public int Field4DC { get => ReadInt32(_off159); set => WriteInt32(_off159, value); }

        private const int _off160 = 0x4E0;
        public int Field4E0 { get => ReadInt32(_off160); set => WriteInt32(_off160, value); }

        private const int _off161 = 0x4E4;
        public int Field4E4 { get => ReadInt32(_off161); set => WriteInt32(_off161, value); }

        private const int _off162 = 0x4E8;
        public Vector3 Field4E8 { get => ReadVec3(_off162); set => WriteVec3(_off162, value); }

        private const int _off163 = 0x4F4;
        public Matrix4x3 Transform { get => ReadMtx43(_off163); set => WriteMtx43(_off163, value); }

        private const int _off164 = 0x524;
        public int Field524 { get => ReadInt32(_off164); set => WriteInt32(_off164, value); }

        private const int _off165 = 0x528;
        public int Field528 { get => ReadInt32(_off165); set => WriteInt32(_off165, value); }

        private const int _off166 = 0x52C;
        public int Field52C { get => ReadInt32(_off166); set => WriteInt32(_off166, value); }

        private const int _off167 = 0x530;
        public int Field530 { get => ReadInt32(_off167); set => WriteInt32(_off167, value); }

        private const int _off168 = 0x534;
        public int Field534 { get => ReadInt32(_off168); set => WriteInt32(_off168, value); }

        private const int _off169 = 0x538;
        public int Field538 { get => ReadInt32(_off169); set => WriteInt32(_off169, value); }

        private const int _off170 = 0x53C;
        public byte BombTimer { get => ReadByte(_off170); set => WriteByte(_off170, value); }

        private const int _off171 = 0x53D;
        public byte BombAmount { get => ReadByte(_off171); set => WriteByte(_off171, value); }

        private const int _off172 = 0x53E;
        public byte Field53E { get => ReadByte(_off172); set => WriteByte(_off172, value); }

        private const int _off173 = 0x53F;
        public byte Field53F { get => ReadByte(_off173); set => WriteByte(_off173, value); }

        private const int _off174 = 0x540;
        public byte Field540 { get => ReadByte(_off174); set => WriteByte(_off174, value); }

        private const int _off175 = 0x541;
        public byte Field541 { get => ReadByte(_off175); set => WriteByte(_off175, value); }

        private const int _off176 = 0x542;
        public ushort Field542 { get => ReadUInt16(_off176); set => WriteUInt16(_off176, value); }

        private const int _off177 = 0x544;
        public Vector3 Field544 { get => ReadVec3(_off177); set => WriteVec3(_off177, value); }

        private const int _off178 = 0x550;
        public byte GunIdleMaybe { get => ReadByte(_off178); set => WriteByte(_off178, value); }

        private const int _off179 = 0x551;
        public byte Field551 { get => ReadByte(_off179); set => WriteByte(_off179, value); }

        private const int _off180 = 0x552;
        public byte Field552 { get => ReadByte(_off180); set => WriteByte(_off180, value); }

        private const int _off181 = 0x553;
        public byte Field553 { get => ReadByte(_off181); set => WriteByte(_off181, value); }

        private const int _off182 = 0x554;
        public int Field554 { get => ReadInt32(_off182); set => WriteInt32(_off182, value); }

        private const int _off183 = 0x558;
        public int Field558 { get => ReadInt32(_off183); set => WriteInt32(_off183, value); }

        private const int _off184 = 0x55C;
        public CameraInfo CameraInfo { get; }

        private const int _off185 = 0x678;
        public Vector3 PrevCamPos { get => ReadVec3(_off185); set => WriteVec3(_off185, value); }

        private const int _off186 = 0x684;
        public int Field684 { get => ReadInt32(_off186); set => WriteInt32(_off186, value); }

        private const int _off187 = 0x688;
        public int Field688 { get => ReadInt32(_off187); set => WriteInt32(_off187, value); }

        private const int _off188 = 0x68C;
        public int Field68C { get => ReadInt32(_off188); set => WriteInt32(_off188, value); }

        private const int _off189 = 0x690;
        public int Field690 { get => ReadInt32(_off189); set => WriteInt32(_off189, value); }

        private const int _off190 = 0x694;
        public LightInfo LightInfo { get; }

        private const int _off191 = 0x6B3;
        public byte Field6B3 { get => ReadByte(_off191); set => WriteByte(_off191, value); }

        private const int _off192 = 0x6B4; // CollisionVolume*
        public IntPtr Field6B4 { get => ReadPointer(_off192); set => WritePointer(_off192, value); }

        private const int _off193 = 0x6B8; // CMorphCamera*
        public IntPtr LastCamPos { get => ReadPointer(_off193); set => WritePointer(_off193, value); }

        private const int _off194 = 0x6BC; // CPointModule*
        public IntPtr NextPointModule { get => ReadPointer(_off194); set => WritePointer(_off194, value); }

        private const int _off195 = 0x6C0; // CJumpPad*
        public IntPtr LastJumpPad { get => ReadPointer(_off195); set => WritePointer(_off195, value); }

        private const int _off196 = 0x6C4; // COctolithFlag*
        public IntPtr OctoFlag { get => ReadPointer(_off196); set => WritePointer(_off196, value); }

        private const int _off197 = 0x6C8; // CEnemySpawn*
        public IntPtr EnemySpawner { get => ReadPointer(_off197); set => WritePointer(_off197, value); }

        private const int _off198 = 0x6CC; // CEntity*
        public IntPtr LastTarget { get => ReadPointer(_off198); set => WritePointer(_off198, value); }

        private const int _off199 = 0x6D0;
        public int Field6D0 { get => ReadInt32(_off199); set => WriteInt32(_off199, value); }

        private const int _off200 = 0x6D4; // CEntity*
        public IntPtr BurnedBy { get => ReadPointer(_off200); set => WritePointer(_off200, value); }

        private const int _off201 = 0x6D8; // EffectEntry*
        public IntPtr EffectBurn { get => ReadPointer(_off201); set => WritePointer(_off201, value); }

        private const int _off202 = 0x6DC;
        public ushort BurnTimer { get => ReadUInt16(_off202); set => WriteUInt16(_off202, value); }

        private const int _off203 = 0x6DE;
        public ushort TargetOrDamageRelated { get => ReadUInt16(_off203); set => WriteUInt16(_off203, value); }

        private const int _off204 = 0x6E0;
        public ushort Field6E0 { get => ReadUInt16(_off204); set => WriteUInt16(_off204, value); }

        private const int _off205 = 0x6E2;
        public ushort Field6E2 { get => ReadUInt16(_off205); set => WriteUInt16(_off205, value); }

        private const int _off206 = 0x6E4;
        public int Field6E4 { get => ReadInt32(_off206); set => WriteInt32(_off206, value); }

        private const int _off207 = 0x6E8;
        public int Field6E8 { get => ReadInt32(_off207); set => WriteInt32(_off207, value); }

        private const int _off208 = 0x6EC;
        public int Field6EC { get => ReadInt32(_off208); set => WriteInt32(_off208, value); }

        private const int _off209 = 0x6F0;
        public uint Effectiveness { get => ReadUInt32(_off209); set => WriteUInt32(_off209, value); }

        private const int _off210 = 0x6F4;
        public int Field6F4 { get => ReadInt32(_off210); set => WriteInt32(_off210, value); }

        private const int _off211 = 0x6F8;
        public int Field6F8 { get => ReadInt32(_off211); set => WriteInt32(_off211, value); }

        private const int _off212 = 0x6FC;
        public int Field6FC { get => ReadInt32(_off212); set => WriteInt32(_off212, value); }

        private const int _off213 = 0x700;
        public int Field700 { get => ReadInt32(_off213); set => WriteInt32(_off213, value); }

        private const int _off214 = 0x704;
        public ushort AltFormAttackTime { get => ReadUInt16(_off214); set => WriteUInt16(_off214, value); }

        private const int _off215 = 0x706;
        public ushort Field706 { get => ReadUInt16(_off215); set => WriteUInt16(_off215, value); }

        private const int _off216 = 0x708;
        public int AltField708 { get => ReadInt32(_off216); set => WriteInt32(_off216, value); }

        private const int _off217 = 0x70C;
        public int AltField70C { get => ReadInt32(_off217); set => WriteInt32(_off217, value); }

        private const int _off218 = 0x710;
        public int AltField710 { get => ReadInt32(_off218); set => WriteInt32(_off218, value); }

        private const int _off219 = 0x714;
        public Vector3 Field714 { get => ReadVec3(_off219); set => WriteVec3(_off219, value); }

        private const int _off220 = 0x720;
        public Vector3 Field720 { get => ReadVec3(_off220); set => WriteVec3(_off220, value); }

        private const int _off221 = 0x72C;
        public Vector3 Field72C { get => ReadVec3(_off221); set => WriteVec3(_off221, value); }

        private const int _off222 = 0x738;
        public int Field738 { get => ReadInt32(_off222); set => WriteInt32(_off222, value); }

        private const int _off223 = 0x73C;
        public int Field73C { get => ReadInt32(_off223); set => WriteInt32(_off223, value); }

        private const int _off224 = 0x740;
        public int Field740 { get => ReadInt32(_off224); set => WriteInt32(_off224, value); }

        private const int _off225 = 0x744;
        public int Field744 { get => ReadInt32(_off225); set => WriteInt32(_off225, value); }

        private const int _off226 = 0x748;
        public int Field748 { get => ReadInt32(_off226); set => WriteInt32(_off226, value); }

        private const int _off227 = 0x74C;
        public int Field74C { get => ReadInt32(_off227); set => WriteInt32(_off227, value); }

        private const int _off228 = 0x750;
        public int Field750 { get => ReadInt32(_off228); set => WriteInt32(_off228, value); }

        private const int _off229 = 0x754;
        public int Field754 { get => ReadInt32(_off229); set => WriteInt32(_off229, value); }

        private const int _off230 = 0x758;
        public int Field758 { get => ReadInt32(_off230); set => WriteInt32(_off230, value); }

        private const int _off231 = 0x75C;
        public int Field75C { get => ReadInt32(_off231); set => WriteInt32(_off231, value); }

        private const int _off232 = 0x760;
        public int Field760 { get => ReadInt32(_off232); set => WriteInt32(_off232, value); }

        private const int _off233 = 0x764;
        public int Field764 { get => ReadInt32(_off233); set => WriteInt32(_off233, value); }

        private const int _off234 = 0x768;
        public int Field768 { get => ReadInt32(_off234); set => WriteInt32(_off234, value); }

        private const int _off235 = 0x76C;
        public int Field76C { get => ReadInt32(_off235); set => WriteInt32(_off235, value); }

        private const int _off236 = 0x770;
        public int Field770 { get => ReadInt32(_off236); set => WriteInt32(_off236, value); }

        private const int _off237 = 0x774;
        public int Field774 { get => ReadInt32(_off237); set => WriteInt32(_off237, value); }

        private const int _off238 = 0x778;
        public int Field778 { get => ReadInt32(_off238); set => WriteInt32(_off238, value); }

        private const int _off239 = 0x77C;
        public int Field77C { get => ReadInt32(_off239); set => WriteInt32(_off239, value); }

        private const int _off240 = 0x780;
        public int Field780 { get => ReadInt32(_off240); set => WriteInt32(_off240, value); }

        private const int _off241 = 0x784;
        public int Field784 { get => ReadInt32(_off241); set => WriteInt32(_off241, value); }

        private const int _off242 = 0x788;
        public int Field788 { get => ReadInt32(_off242); set => WriteInt32(_off242, value); }

        private const int _off243 = 0x78C;
        public int Field78C { get => ReadInt32(_off243); set => WriteInt32(_off243, value); }

        private const int _off244 = 0x790;
        public int Field790 { get => ReadInt32(_off244); set => WriteInt32(_off244, value); }

        private const int _off245 = 0x794;
        public int Field794 { get => ReadInt32(_off245); set => WriteInt32(_off245, value); }

        private const int _off246 = 0x798;
        public int Field798 { get => ReadInt32(_off246); set => WriteInt32(_off246, value); }

        private const int _off247 = 0x79C;
        public int Field79C { get => ReadInt32(_off247); set => WriteInt32(_off247, value); }

        private const int _off248 = 0x7A0;
        public int Field7A0 { get => ReadInt32(_off248); set => WriteInt32(_off248, value); }

        private const int _off249 = 0x7A4;
        public int Field7A4 { get => ReadInt32(_off249); set => WriteInt32(_off249, value); }

        private const int _off250 = 0x7A8;
        public int Field7A8 { get => ReadInt32(_off250); set => WriteInt32(_off250, value); }

        private const int _off251 = 0x7AC;
        public int Field7AC { get => ReadInt32(_off251); set => WriteInt32(_off251, value); }

        private const int _off252 = 0x7B0;
        public int Field7B0 { get => ReadInt32(_off252); set => WriteInt32(_off252, value); }

        private const int _off253 = 0x7B4;
        public int Field7B4 { get => ReadInt32(_off253); set => WriteInt32(_off253, value); }

        private const int _off254 = 0x7B8;
        public int Field7B8 { get => ReadInt32(_off254); set => WriteInt32(_off254, value); }

        private const int _off255 = 0x7BC;
        public int Field7BC { get => ReadInt32(_off255); set => WriteInt32(_off255, value); }

        private const int _off256 = 0x7C0;
        public int Field7C0 { get => ReadInt32(_off256); set => WriteInt32(_off256, value); }

        private const int _off257 = 0x7C4;
        public int Field7C4 { get => ReadInt32(_off257); set => WriteInt32(_off257, value); }

        private const int _off258 = 0x7C8;
        public int Field7C8 { get => ReadInt32(_off258); set => WriteInt32(_off258, value); }

        private const int _off259 = 0x7CC;
        public int Field7CC { get => ReadInt32(_off259); set => WriteInt32(_off259, value); }

        private const int _off260 = 0x7D0;
        public int Field7D0 { get => ReadInt32(_off260); set => WriteInt32(_off260, value); }

        private const int _off261 = 0x7D4;
        public Vector3 Field7D4 { get => ReadVec3(_off261); set => WriteVec3(_off261, value); }

        private const int _off262 = 0x7E0;
        public Vector3 Field7E0 { get => ReadVec3(_off262); set => WriteVec3(_off262, value); }

        private const int _off263 = 0x7EC;
        public int Field7EC { get => ReadInt32(_off263); set => WriteInt32(_off263, value); }

        private const int _off264 = 0x7F0;
        public int Field7F0 { get => ReadInt32(_off264); set => WriteInt32(_off264, value); }

        private const int _off265 = 0x7F4;
        public int Field7F4 { get => ReadInt32(_off265); set => WriteInt32(_off265, value); }

        private const int _off266 = 0x7F8;
        public int Field7F8 { get => ReadInt32(_off266); set => WriteInt32(_off266, value); }

        private const int _off267 = 0x7FC; // Node*
        public IntPtr Lrock01Node { get => ReadPointer(_off267); set => WritePointer(_off267, value); }

        private const int _off268 = 0x800; // Node*
        public IntPtr Rrock01Node { get => ReadPointer(_off268); set => WritePointer(_off268, value); }

        private const int _off269 = 0x804; // Node*
        public IntPtr RposrotNode { get => ReadPointer(_off269); set => WritePointer(_off269, value); }

        private const int _off270 = 0x808; // Node*
        public IntPtr Rposrot1Node { get => ReadPointer(_off270); set => WritePointer(_off270, value); }

        private const int _off271 = 0x80C;
        public int Field80C { get => ReadInt32(_off271); set => WriteInt32(_off271, value); }

        private const int _off272 = 0x810;
        public int Field810 { get => ReadInt32(_off272); set => WriteInt32(_off272, value); }

        private const int _off273 = 0x814;
        public int Field814 { get => ReadInt32(_off273); set => WriteInt32(_off273, value); }

        private const int _off274 = 0x818;
        public int Field818 { get => ReadInt32(_off274); set => WriteInt32(_off274, value); }

        private const int _off275 = 0x81C;
        public int Field81C { get => ReadInt32(_off275); set => WriteInt32(_off275, value); }

        private const int _off276 = 0x820;
        public int Field820 { get => ReadInt32(_off276); set => WriteInt32(_off276, value); }

        private const int _off277 = 0x824;
        public int Field824 { get => ReadInt32(_off277); set => WriteInt32(_off277, value); }

        private const int _off278 = 0x828;
        public Vector3 Field828 { get => ReadVec3(_off278); set => WriteVec3(_off278, value); }

        private const int _off279 = 0x834;
        public int Field834 { get => ReadInt32(_off279); set => WriteInt32(_off279, value); }

        private const int _off280 = 0x838;
        public int Field838 { get => ReadInt32(_off280); set => WriteInt32(_off280, value); }

        private const int _off281 = 0x83C;
        public int Field83C { get => ReadInt32(_off281); set => WriteInt32(_off281, value); }

        private const int _off282 = 0x840;
        public int Field840 { get => ReadInt32(_off282); set => WriteInt32(_off282, value); }

        private const int _off283 = 0x844;
        public int Field844 { get => ReadInt32(_off283); set => WriteInt32(_off283, value); }

        private const int _off284 = 0x848; // PlayerStruct1*
        public IntPtr Struct1 { get => ReadPointer(_off284); set => WritePointer(_off284, value); }

        private const int _off285 = 0x84C;
        public byte LoadFlags { get => ReadByte(_off285); set => WriteByte(_off285, value); }

        private const int _off286 = 0x84D;
        public byte SlotIndex { get => ReadByte(_off286); set => WriteByte(_off286, value); }

        private const int _off287 = 0x84E;
        public byte IsBot { get => ReadByte(_off287); set => WriteByte(_off287, value); }

        private const int _off288 = 0x84F;
        public byte Field84F { get => ReadByte(_off288); set => WriteByte(_off288, value); }

        private const int _off289 = 0x850;
        public EquipInfoPtr EquipInfo { get; }

        private const int _off290 = 0x864;
        public CBeamProjectile BeamHead { get; }

        private const int _off291 = 0x9BC;
        public int Field9BC { get => ReadInt32(_off291); set => WriteInt32(_off291, value); }

        private const int _off292 = 0x9C0;
        public int Field9C0 { get => ReadInt32(_off292); set => WriteInt32(_off292, value); }

        private const int _off293 = 0x9C4;
        public int Field9C4 { get => ReadInt32(_off293); set => WriteInt32(_off293, value); }

        private const int _off294 = 0x9C8;
        public int Field9C8 { get => ReadInt32(_off294); set => WriteInt32(_off294, value); }

        private const int _off295 = 0x9CC;
        public int Field9CC { get => ReadInt32(_off295); set => WriteInt32(_off295, value); }

        private const int _off296 = 0x9D0;
        public int Field9D0 { get => ReadInt32(_off296); set => WriteInt32(_off296, value); }

        private const int _off297 = 0x9D4;
        public int Field9D4 { get => ReadInt32(_off297); set => WriteInt32(_off297, value); }

        private const int _off298 = 0x9D8;
        public int Field9D8 { get => ReadInt32(_off298); set => WriteInt32(_off298, value); }

        private const int _off299 = 0x9DC;
        public int Field9DC { get => ReadInt32(_off299); set => WriteInt32(_off299, value); }

        private const int _off300 = 0x9E0;
        public int Field9E0 { get => ReadInt32(_off300); set => WriteInt32(_off300, value); }

        private const int _off301 = 0x9E4;
        public int Field9E4 { get => ReadInt32(_off301); set => WriteInt32(_off301, value); }

        private const int _off302 = 0x9E8;
        public int Field9E8 { get => ReadInt32(_off302); set => WriteInt32(_off302, value); }

        private const int _off303 = 0x9EC;
        public int Field9EC { get => ReadInt32(_off303); set => WriteInt32(_off303, value); }

        private const int _off304 = 0x9F0;
        public int Field9F0 { get => ReadInt32(_off304); set => WriteInt32(_off304, value); }

        private const int _off305 = 0x9F4;
        public int Field9F4 { get => ReadInt32(_off305); set => WriteInt32(_off305, value); }

        private const int _off306 = 0x9F8;
        public int Field9F8 { get => ReadInt32(_off306); set => WriteInt32(_off306, value); }

        private const int _off307 = 0x9FC;
        public int Field9FC { get => ReadInt32(_off307); set => WriteInt32(_off307, value); }

        private const int _off308 = 0xA00;
        public int FieldA00 { get => ReadInt32(_off308); set => WriteInt32(_off308, value); }

        private const int _off309 = 0xA04;
        public int FieldA04 { get => ReadInt32(_off309); set => WriteInt32(_off309, value); }

        private const int _off310 = 0xA08;
        public int FieldA08 { get => ReadInt32(_off310); set => WriteInt32(_off310, value); }

        private const int _off311 = 0xA0C;
        public int FieldA0C { get => ReadInt32(_off311); set => WriteInt32(_off311, value); }

        private const int _off312 = 0xA10;
        public int FieldA10 { get => ReadInt32(_off312); set => WriteInt32(_off312, value); }

        private const int _off313 = 0xA14;
        public int FieldA14 { get => ReadInt32(_off313); set => WriteInt32(_off313, value); }

        private const int _off314 = 0xA18;
        public int FieldA18 { get => ReadInt32(_off314); set => WriteInt32(_off314, value); }

        private const int _off315 = 0xA1C;
        public int FieldA1C { get => ReadInt32(_off315); set => WriteInt32(_off315, value); }

        private const int _off316 = 0xA20;
        public int FieldA20 { get => ReadInt32(_off316); set => WriteInt32(_off316, value); }

        private const int _off317 = 0xA24;
        public int FieldA24 { get => ReadInt32(_off317); set => WriteInt32(_off317, value); }

        private const int _off318 = 0xA28;
        public int FieldA28 { get => ReadInt32(_off318); set => WriteInt32(_off318, value); }

        private const int _off319 = 0xA2C;
        public int FieldA2C { get => ReadInt32(_off319); set => WriteInt32(_off319, value); }

        private const int _off320 = 0xA30;
        public int FieldA30 { get => ReadInt32(_off320); set => WriteInt32(_off320, value); }

        private const int _off321 = 0xA34;
        public int FieldA34 { get => ReadInt32(_off321); set => WriteInt32(_off321, value); }

        private const int _off322 = 0xA38;
        public int FieldA38 { get => ReadInt32(_off322); set => WriteInt32(_off322, value); }

        private const int _off323 = 0xA3C;
        public int FieldA3C { get => ReadInt32(_off323); set => WriteInt32(_off323, value); }

        private const int _off324 = 0xA40;
        public int FieldA40 { get => ReadInt32(_off324); set => WriteInt32(_off324, value); }

        private const int _off325 = 0xA44;
        public int FieldA44 { get => ReadInt32(_off325); set => WriteInt32(_off325, value); }

        private const int _off326 = 0xA48;
        public int FieldA48 { get => ReadInt32(_off326); set => WriteInt32(_off326, value); }

        private const int _off327 = 0xA4C;
        public int FieldA4C { get => ReadInt32(_off327); set => WriteInt32(_off327, value); }

        private const int _off328 = 0xA50;
        public int FieldA50 { get => ReadInt32(_off328); set => WriteInt32(_off328, value); }

        private const int _off329 = 0xA54;
        public int FieldA54 { get => ReadInt32(_off329); set => WriteInt32(_off329, value); }

        private const int _off330 = 0xA58;
        public int FieldA58 { get => ReadInt32(_off330); set => WriteInt32(_off330, value); }

        private const int _off331 = 0xA5C;
        public int FieldA5C { get => ReadInt32(_off331); set => WriteInt32(_off331, value); }

        private const int _off332 = 0xA60;
        public int FieldA60 { get => ReadInt32(_off332); set => WriteInt32(_off332, value); }

        private const int _off333 = 0xA64;
        public int FieldA64 { get => ReadInt32(_off333); set => WriteInt32(_off333, value); }

        private const int _off334 = 0xA68;
        public int FieldA68 { get => ReadInt32(_off334); set => WriteInt32(_off334, value); }

        private const int _off335 = 0xA6C;
        public int FieldA6C { get => ReadInt32(_off335); set => WriteInt32(_off335, value); }

        private const int _off336 = 0xA70;
        public int FieldA70 { get => ReadInt32(_off336); set => WriteInt32(_off336, value); }

        private const int _off337 = 0xA74;
        public int FieldA74 { get => ReadInt32(_off337); set => WriteInt32(_off337, value); }

        private const int _off338 = 0xA78;
        public int FieldA78 { get => ReadInt32(_off338); set => WriteInt32(_off338, value); }

        private const int _off339 = 0xA7C;
        public int FieldA7C { get => ReadInt32(_off339); set => WriteInt32(_off339, value); }

        private const int _off340 = 0xA80;
        public int FieldA80 { get => ReadInt32(_off340); set => WriteInt32(_off340, value); }

        private const int _off341 = 0xA84;
        public int FieldA84 { get => ReadInt32(_off341); set => WriteInt32(_off341, value); }

        private const int _off342 = 0xA88;
        public int FieldA88 { get => ReadInt32(_off342); set => WriteInt32(_off342, value); }

        private const int _off343 = 0xA8C;
        public int FieldA8C { get => ReadInt32(_off343); set => WriteInt32(_off343, value); }

        private const int _off344 = 0xA90;
        public int FieldA90 { get => ReadInt32(_off344); set => WriteInt32(_off344, value); }

        private const int _off345 = 0xA94;
        public int FieldA94 { get => ReadInt32(_off345); set => WriteInt32(_off345, value); }

        private const int _off346 = 0xA98;
        public int FieldA98 { get => ReadInt32(_off346); set => WriteInt32(_off346, value); }

        private const int _off347 = 0xA9C;
        public int FieldA9C { get => ReadInt32(_off347); set => WriteInt32(_off347, value); }

        private const int _off348 = 0xAA0;
        public int FieldAA0 { get => ReadInt32(_off348); set => WriteInt32(_off348, value); }

        private const int _off349 = 0xAA4;
        public int FieldAA4 { get => ReadInt32(_off349); set => WriteInt32(_off349, value); }

        private const int _off350 = 0xAA8;
        public int FieldAA8 { get => ReadInt32(_off350); set => WriteInt32(_off350, value); }

        private const int _off351 = 0xAAC;
        public int FieldAAC { get => ReadInt32(_off351); set => WriteInt32(_off351, value); }

        private const int _off352 = 0xAB0;
        public int FieldAB0 { get => ReadInt32(_off352); set => WriteInt32(_off352, value); }

        private const int _off353 = 0xAB4;
        public int FieldAB4 { get => ReadInt32(_off353); set => WriteInt32(_off353, value); }

        private const int _off354 = 0xAB8;
        public int FieldAB8 { get => ReadInt32(_off354); set => WriteInt32(_off354, value); }

        private const int _off355 = 0xABC;
        public int FieldABC { get => ReadInt32(_off355); set => WriteInt32(_off355, value); }

        private const int _off356 = 0xAC0;
        public int FieldAC0 { get => ReadInt32(_off356); set => WriteInt32(_off356, value); }

        private const int _off357 = 0xAC4;
        public int FieldAC4 { get => ReadInt32(_off357); set => WriteInt32(_off357, value); }

        private const int _off358 = 0xAC8;
        public int FieldAC8 { get => ReadInt32(_off358); set => WriteInt32(_off358, value); }

        private const int _off359 = 0xACC;
        public int FieldACC { get => ReadInt32(_off359); set => WriteInt32(_off359, value); }

        private const int _off360 = 0xAD0;
        public int FieldAD0 { get => ReadInt32(_off360); set => WriteInt32(_off360, value); }

        private const int _off361 = 0xAD4;
        public int FieldAD4 { get => ReadInt32(_off361); set => WriteInt32(_off361, value); }

        private const int _off362 = 0xAD8;
        public int FieldAD8 { get => ReadInt32(_off362); set => WriteInt32(_off362, value); }

        private const int _off363 = 0xADC;
        public int FieldADC { get => ReadInt32(_off363); set => WriteInt32(_off363, value); }

        private const int _off364 = 0xAE0;
        public int FieldAE0 { get => ReadInt32(_off364); set => WriteInt32(_off364, value); }

        private const int _off365 = 0xAE4;
        public int FieldAE4 { get => ReadInt32(_off365); set => WriteInt32(_off365, value); }

        private const int _off366 = 0xAE8;
        public int FieldAE8 { get => ReadInt32(_off366); set => WriteInt32(_off366, value); }

        private const int _off367 = 0xAEC;
        public int FieldAEC { get => ReadInt32(_off367); set => WriteInt32(_off367, value); }

        private const int _off368 = 0xAF0;
        public int FieldAF0 { get => ReadInt32(_off368); set => WriteInt32(_off368, value); }

        private const int _off369 = 0xAF4;
        public int FieldAF4 { get => ReadInt32(_off369); set => WriteInt32(_off369, value); }

        private const int _off370 = 0xAF8;
        public int FieldAF8 { get => ReadInt32(_off370); set => WriteInt32(_off370, value); }

        private const int _off371 = 0xAFC;
        public int FieldAFC { get => ReadInt32(_off371); set => WriteInt32(_off371, value); }

        private const int _off372 = 0xB00;
        public int FieldB00 { get => ReadInt32(_off372); set => WriteInt32(_off372, value); }

        private const int _off373 = 0xB04;
        public int FieldB04 { get => ReadInt32(_off373); set => WriteInt32(_off373, value); }

        private const int _off374 = 0xB08;
        public int FieldB08 { get => ReadInt32(_off374); set => WriteInt32(_off374, value); }

        private const int _off375 = 0xB0C;
        public int FieldB0C { get => ReadInt32(_off375); set => WriteInt32(_off375, value); }

        private const int _off376 = 0xB10;
        public int FieldB10 { get => ReadInt32(_off376); set => WriteInt32(_off376, value); }

        private const int _off377 = 0xB14;
        public int FieldB14 { get => ReadInt32(_off377); set => WriteInt32(_off377, value); }

        private const int _off378 = 0xB18;
        public int FieldB18 { get => ReadInt32(_off378); set => WriteInt32(_off378, value); }

        private const int _off379 = 0xB1C;
        public int FieldB1C { get => ReadInt32(_off379); set => WriteInt32(_off379, value); }

        private const int _off380 = 0xB20;
        public int FieldB20 { get => ReadInt32(_off380); set => WriteInt32(_off380, value); }

        private const int _off381 = 0xB24;
        public int FieldB24 { get => ReadInt32(_off381); set => WriteInt32(_off381, value); }

        private const int _off382 = 0xB28;
        public int FieldB28 { get => ReadInt32(_off382); set => WriteInt32(_off382, value); }

        private const int _off383 = 0xB2C;
        public int FieldB2C { get => ReadInt32(_off383); set => WriteInt32(_off383, value); }

        private const int _off384 = 0xB30;
        public int FieldB30 { get => ReadInt32(_off384); set => WriteInt32(_off384, value); }

        private const int _off385 = 0xB34;
        public int FieldB34 { get => ReadInt32(_off385); set => WriteInt32(_off385, value); }

        private const int _off386 = 0xB38;
        public int FieldB38 { get => ReadInt32(_off386); set => WriteInt32(_off386, value); }

        private const int _off387 = 0xB3C;
        public int FieldB3C { get => ReadInt32(_off387); set => WriteInt32(_off387, value); }

        private const int _off388 = 0xB40;
        public int FieldB40 { get => ReadInt32(_off388); set => WriteInt32(_off388, value); }

        private const int _off389 = 0xB44;
        public int FieldB44 { get => ReadInt32(_off389); set => WriteInt32(_off389, value); }

        private const int _off390 = 0xB48;
        public int FieldB48 { get => ReadInt32(_off390); set => WriteInt32(_off390, value); }

        private const int _off391 = 0xB4C;
        public int FieldB4C { get => ReadInt32(_off391); set => WriteInt32(_off391, value); }

        private const int _off392 = 0xB50;
        public int FieldB50 { get => ReadInt32(_off392); set => WriteInt32(_off392, value); }

        private const int _off393 = 0xB54;
        public int FieldB54 { get => ReadInt32(_off393); set => WriteInt32(_off393, value); }

        private const int _off394 = 0xB58;
        public int FieldB58 { get => ReadInt32(_off394); set => WriteInt32(_off394, value); }

        private const int _off395 = 0xB5C;
        public int FieldB5C { get => ReadInt32(_off395); set => WriteInt32(_off395, value); }

        private const int _off396 = 0xB60;
        public int FieldB60 { get => ReadInt32(_off396); set => WriteInt32(_off396, value); }

        private const int _off397 = 0xB64;
        public int FieldB64 { get => ReadInt32(_off397); set => WriteInt32(_off397, value); }

        private const int _off398 = 0xB68;
        public int FieldB68 { get => ReadInt32(_off398); set => WriteInt32(_off398, value); }

        private const int _off399 = 0xB6C;
        public int FieldB6C { get => ReadInt32(_off399); set => WriteInt32(_off399, value); }

        private const int _off400 = 0xB70;
        public int FieldB70 { get => ReadInt32(_off400); set => WriteInt32(_off400, value); }

        private const int _off401 = 0xB74;
        public int FieldB74 { get => ReadInt32(_off401); set => WriteInt32(_off401, value); }

        private const int _off402 = 0xB78;
        public int FieldB78 { get => ReadInt32(_off402); set => WriteInt32(_off402, value); }

        private const int _off403 = 0xB7C;
        public int FieldB7C { get => ReadInt32(_off403); set => WriteInt32(_off403, value); }

        private const int _off404 = 0xB80;
        public int FieldB80 { get => ReadInt32(_off404); set => WriteInt32(_off404, value); }

        private const int _off405 = 0xB84;
        public int FieldB84 { get => ReadInt32(_off405); set => WriteInt32(_off405, value); }

        private const int _off406 = 0xB88;
        public int FieldB88 { get => ReadInt32(_off406); set => WriteInt32(_off406, value); }

        private const int _off407 = 0xB8C;
        public int FieldB8C { get => ReadInt32(_off407); set => WriteInt32(_off407, value); }

        private const int _off408 = 0xB90;
        public int FieldB90 { get => ReadInt32(_off408); set => WriteInt32(_off408, value); }

        private const int _off409 = 0xB94;
        public int FieldB94 { get => ReadInt32(_off409); set => WriteInt32(_off409, value); }

        private const int _off410 = 0xB98;
        public int FieldB98 { get => ReadInt32(_off410); set => WriteInt32(_off410, value); }

        private const int _off411 = 0xB9C;
        public int FieldB9C { get => ReadInt32(_off411); set => WriteInt32(_off411, value); }

        private const int _off412 = 0xBA0;
        public int FieldBA0 { get => ReadInt32(_off412); set => WriteInt32(_off412, value); }

        private const int _off413 = 0xBA4;
        public int FieldBA4 { get => ReadInt32(_off413); set => WriteInt32(_off413, value); }

        private const int _off414 = 0xBA8;
        public int FieldBA8 { get => ReadInt32(_off414); set => WriteInt32(_off414, value); }

        private const int _off415 = 0xBAC;
        public int FieldBAC { get => ReadInt32(_off415); set => WriteInt32(_off415, value); }

        private const int _off416 = 0xBB0;
        public int FieldBB0 { get => ReadInt32(_off416); set => WriteInt32(_off416, value); }

        private const int _off417 = 0xBB4;
        public int FieldBB4 { get => ReadInt32(_off417); set => WriteInt32(_off417, value); }

        private const int _off418 = 0xBB8;
        public int FieldBB8 { get => ReadInt32(_off418); set => WriteInt32(_off418, value); }

        private const int _off419 = 0xBBC;
        public int FieldBBC { get => ReadInt32(_off419); set => WriteInt32(_off419, value); }

        private const int _off420 = 0xBC0;
        public int FieldBC0 { get => ReadInt32(_off420); set => WriteInt32(_off420, value); }

        private const int _off421 = 0xBC4;
        public int FieldBC4 { get => ReadInt32(_off421); set => WriteInt32(_off421, value); }

        private const int _off422 = 0xBC8;
        public int FieldBC8 { get => ReadInt32(_off422); set => WriteInt32(_off422, value); }

        private const int _off423 = 0xBCC;
        public int FieldBCC { get => ReadInt32(_off423); set => WriteInt32(_off423, value); }

        private const int _off424 = 0xBD0;
        public int FieldBD0 { get => ReadInt32(_off424); set => WriteInt32(_off424, value); }

        private const int _off425 = 0xBD4;
        public int FieldBD4 { get => ReadInt32(_off425); set => WriteInt32(_off425, value); }

        private const int _off426 = 0xBD8;
        public int FieldBD8 { get => ReadInt32(_off426); set => WriteInt32(_off426, value); }

        private const int _off427 = 0xBDC;
        public int FieldBDC { get => ReadInt32(_off427); set => WriteInt32(_off427, value); }

        private const int _off428 = 0xBE0;
        public int FieldBE0 { get => ReadInt32(_off428); set => WriteInt32(_off428, value); }

        private const int _off429 = 0xBE4;
        public int FieldBE4 { get => ReadInt32(_off429); set => WriteInt32(_off429, value); }

        private const int _off430 = 0xBE8;
        public int FieldBE8 { get => ReadInt32(_off430); set => WriteInt32(_off430, value); }

        private const int _off431 = 0xBEC;
        public int FieldBEC { get => ReadInt32(_off431); set => WriteInt32(_off431, value); }

        private const int _off432 = 0xBF0;
        public int FieldBF0 { get => ReadInt32(_off432); set => WriteInt32(_off432, value); }

        private const int _off433 = 0xBF4;
        public int FieldBF4 { get => ReadInt32(_off433); set => WriteInt32(_off433, value); }

        private const int _off434 = 0xBF8;
        public int FieldBF8 { get => ReadInt32(_off434); set => WriteInt32(_off434, value); }

        private const int _off435 = 0xBFC;
        public int FieldBFC { get => ReadInt32(_off435); set => WriteInt32(_off435, value); }

        private const int _off436 = 0xC00;
        public int FieldC00 { get => ReadInt32(_off436); set => WriteInt32(_off436, value); }

        private const int _off437 = 0xC04;
        public int FieldC04 { get => ReadInt32(_off437); set => WriteInt32(_off437, value); }

        private const int _off438 = 0xC08;
        public int FieldC08 { get => ReadInt32(_off438); set => WriteInt32(_off438, value); }

        private const int _off439 = 0xC0C;
        public int FieldC0C { get => ReadInt32(_off439); set => WriteInt32(_off439, value); }

        private const int _off440 = 0xC10;
        public int FieldC10 { get => ReadInt32(_off440); set => WriteInt32(_off440, value); }

        private const int _off441 = 0xC14;
        public int FieldC14 { get => ReadInt32(_off441); set => WriteInt32(_off441, value); }

        private const int _off442 = 0xC18;
        public int FieldC18 { get => ReadInt32(_off442); set => WriteInt32(_off442, value); }

        private const int _off443 = 0xC1C;
        public int FieldC1C { get => ReadInt32(_off443); set => WriteInt32(_off443, value); }

        private const int _off444 = 0xC20;
        public int FieldC20 { get => ReadInt32(_off444); set => WriteInt32(_off444, value); }

        private const int _off445 = 0xC24;
        public int FieldC24 { get => ReadInt32(_off445); set => WriteInt32(_off445, value); }

        private const int _off446 = 0xC28;
        public int FieldC28 { get => ReadInt32(_off446); set => WriteInt32(_off446, value); }

        private const int _off447 = 0xC2C;
        public int FieldC2C { get => ReadInt32(_off447); set => WriteInt32(_off447, value); }

        private const int _off448 = 0xC30;
        public int FieldC30 { get => ReadInt32(_off448); set => WriteInt32(_off448, value); }

        private const int _off449 = 0xC34;
        public int FieldC34 { get => ReadInt32(_off449); set => WriteInt32(_off449, value); }

        private const int _off450 = 0xC38;
        public int FieldC38 { get => ReadInt32(_off450); set => WriteInt32(_off450, value); }

        private const int _off451 = 0xC3C;
        public int FieldC3C { get => ReadInt32(_off451); set => WriteInt32(_off451, value); }

        private const int _off452 = 0xC40;
        public int FieldC40 { get => ReadInt32(_off452); set => WriteInt32(_off452, value); }

        private const int _off453 = 0xC44;
        public int FieldC44 { get => ReadInt32(_off453); set => WriteInt32(_off453, value); }

        private const int _off454 = 0xC48;
        public int FieldC48 { get => ReadInt32(_off454); set => WriteInt32(_off454, value); }

        private const int _off455 = 0xC4C;
        public int FieldC4C { get => ReadInt32(_off455); set => WriteInt32(_off455, value); }

        private const int _off456 = 0xC50;
        public int FieldC50 { get => ReadInt32(_off456); set => WriteInt32(_off456, value); }

        private const int _off457 = 0xC54;
        public int FieldC54 { get => ReadInt32(_off457); set => WriteInt32(_off457, value); }

        private const int _off458 = 0xC58;
        public int FieldC58 { get => ReadInt32(_off458); set => WriteInt32(_off458, value); }

        private const int _off459 = 0xC5C;
        public int FieldC5C { get => ReadInt32(_off459); set => WriteInt32(_off459, value); }

        private const int _off460 = 0xC60;
        public int FieldC60 { get => ReadInt32(_off460); set => WriteInt32(_off460, value); }

        private const int _off461 = 0xC64;
        public int FieldC64 { get => ReadInt32(_off461); set => WriteInt32(_off461, value); }

        private const int _off462 = 0xC68;
        public int FieldC68 { get => ReadInt32(_off462); set => WriteInt32(_off462, value); }

        private const int _off463 = 0xC6C;
        public int FieldC6C { get => ReadInt32(_off463); set => WriteInt32(_off463, value); }

        private const int _off464 = 0xC70;
        public int FieldC70 { get => ReadInt32(_off464); set => WriteInt32(_off464, value); }

        private const int _off465 = 0xC74;
        public int FieldC74 { get => ReadInt32(_off465); set => WriteInt32(_off465, value); }

        private const int _off466 = 0xC78;
        public int FieldC78 { get => ReadInt32(_off466); set => WriteInt32(_off466, value); }

        private const int _off467 = 0xC7C;
        public int FieldC7C { get => ReadInt32(_off467); set => WriteInt32(_off467, value); }

        private const int _off468 = 0xC80;
        public int FieldC80 { get => ReadInt32(_off468); set => WriteInt32(_off468, value); }

        private const int _off469 = 0xC84;
        public int FieldC84 { get => ReadInt32(_off469); set => WriteInt32(_off469, value); }

        private const int _off470 = 0xC88;
        public int FieldC88 { get => ReadInt32(_off470); set => WriteInt32(_off470, value); }

        private const int _off471 = 0xC8C;
        public int FieldC8C { get => ReadInt32(_off471); set => WriteInt32(_off471, value); }

        private const int _off472 = 0xC90;
        public int FieldC90 { get => ReadInt32(_off472); set => WriteInt32(_off472, value); }

        private const int _off473 = 0xC94;
        public int FieldC94 { get => ReadInt32(_off473); set => WriteInt32(_off473, value); }

        private const int _off474 = 0xC98;
        public int FieldC98 { get => ReadInt32(_off474); set => WriteInt32(_off474, value); }

        private const int _off475 = 0xC9C;
        public int FieldC9C { get => ReadInt32(_off475); set => WriteInt32(_off475, value); }

        private const int _off476 = 0xCA0;
        public int FieldCA0 { get => ReadInt32(_off476); set => WriteInt32(_off476, value); }

        private const int _off477 = 0xCA4;
        public int FieldCA4 { get => ReadInt32(_off477); set => WriteInt32(_off477, value); }

        private const int _off478 = 0xCA8;
        public int FieldCA8 { get => ReadInt32(_off478); set => WriteInt32(_off478, value); }

        private const int _off479 = 0xCAC;
        public int FieldCAC { get => ReadInt32(_off479); set => WriteInt32(_off479, value); }

        private const int _off480 = 0xCB0;
        public int FieldCB0 { get => ReadInt32(_off480); set => WriteInt32(_off480, value); }

        private const int _off481 = 0xCB4;
        public int FieldCB4 { get => ReadInt32(_off481); set => WriteInt32(_off481, value); }

        private const int _off482 = 0xCB8;
        public int FieldCB8 { get => ReadInt32(_off482); set => WriteInt32(_off482, value); }

        private const int _off483 = 0xCBC;
        public int FieldCBC { get => ReadInt32(_off483); set => WriteInt32(_off483, value); }

        private const int _off484 = 0xCC0;
        public int FieldCC0 { get => ReadInt32(_off484); set => WriteInt32(_off484, value); }

        private const int _off485 = 0xCC4;
        public int FieldCC4 { get => ReadInt32(_off485); set => WriteInt32(_off485, value); }

        private const int _off486 = 0xCC8;
        public int FieldCC8 { get => ReadInt32(_off486); set => WriteInt32(_off486, value); }

        private const int _off487 = 0xCCC;
        public int FieldCCC { get => ReadInt32(_off487); set => WriteInt32(_off487, value); }

        private const int _off488 = 0xCD0;
        public int FieldCD0 { get => ReadInt32(_off488); set => WriteInt32(_off488, value); }

        private const int _off489 = 0xCD4;
        public int FieldCD4 { get => ReadInt32(_off489); set => WriteInt32(_off489, value); }

        private const int _off490 = 0xCD8;
        public int FieldCD8 { get => ReadInt32(_off490); set => WriteInt32(_off490, value); }

        private const int _off491 = 0xCDC;
        public int FieldCDC { get => ReadInt32(_off491); set => WriteInt32(_off491, value); }

        private const int _off492 = 0xCE0;
        public int FieldCE0 { get => ReadInt32(_off492); set => WriteInt32(_off492, value); }

        private const int _off493 = 0xCE4;
        public int FieldCE4 { get => ReadInt32(_off493); set => WriteInt32(_off493, value); }

        private const int _off494 = 0xCE8;
        public int FieldCE8 { get => ReadInt32(_off494); set => WriteInt32(_off494, value); }

        private const int _off495 = 0xCEC;
        public int FieldCEC { get => ReadInt32(_off495); set => WriteInt32(_off495, value); }

        private const int _off496 = 0xCF0;
        public int FieldCF0 { get => ReadInt32(_off496); set => WriteInt32(_off496, value); }

        private const int _off497 = 0xCF4;
        public int FieldCF4 { get => ReadInt32(_off497); set => WriteInt32(_off497, value); }

        private const int _off498 = 0xCF8;
        public int FieldCF8 { get => ReadInt32(_off498); set => WriteInt32(_off498, value); }

        private const int _off499 = 0xCFC;
        public int FieldCFC { get => ReadInt32(_off499); set => WriteInt32(_off499, value); }

        private const int _off500 = 0xD00;
        public int FieldD00 { get => ReadInt32(_off500); set => WriteInt32(_off500, value); }

        private const int _off501 = 0xD04;
        public int FieldD04 { get => ReadInt32(_off501); set => WriteInt32(_off501, value); }

        private const int _off502 = 0xD08;
        public int FieldD08 { get => ReadInt32(_off502); set => WriteInt32(_off502, value); }

        private const int _off503 = 0xD0C;
        public int FieldD0C { get => ReadInt32(_off503); set => WriteInt32(_off503, value); }

        private const int _off504 = 0xD10;
        public int FieldD10 { get => ReadInt32(_off504); set => WriteInt32(_off504, value); }

        private const int _off505 = 0xD14;
        public int FieldD14 { get => ReadInt32(_off505); set => WriteInt32(_off505, value); }

        private const int _off506 = 0xD18;
        public int FieldD18 { get => ReadInt32(_off506); set => WriteInt32(_off506, value); }

        private const int _off507 = 0xD1C;
        public int FieldD1C { get => ReadInt32(_off507); set => WriteInt32(_off507, value); }

        private const int _off508 = 0xD20;
        public int FieldD20 { get => ReadInt32(_off508); set => WriteInt32(_off508, value); }

        private const int _off509 = 0xD24;
        public int FieldD24 { get => ReadInt32(_off509); set => WriteInt32(_off509, value); }

        private const int _off510 = 0xD28;
        public int FieldD28 { get => ReadInt32(_off510); set => WriteInt32(_off510, value); }

        private const int _off511 = 0xD2C;
        public int FieldD2C { get => ReadInt32(_off511); set => WriteInt32(_off511, value); }

        private const int _off512 = 0xD30;
        public int FieldD30 { get => ReadInt32(_off512); set => WriteInt32(_off512, value); }

        private const int _off513 = 0xD34;
        public int FieldD34 { get => ReadInt32(_off513); set => WriteInt32(_off513, value); }

        private const int _off514 = 0xD38;
        public int FieldD38 { get => ReadInt32(_off514); set => WriteInt32(_off514, value); }

        private const int _off515 = 0xD3C;
        public int FieldD3C { get => ReadInt32(_off515); set => WriteInt32(_off515, value); }

        private const int _off516 = 0xD40;
        public int FieldD40 { get => ReadInt32(_off516); set => WriteInt32(_off516, value); }

        private const int _off517 = 0xD44;
        public int FieldD44 { get => ReadInt32(_off517); set => WriteInt32(_off517, value); }

        private const int _off518 = 0xD48;
        public int FieldD48 { get => ReadInt32(_off518); set => WriteInt32(_off518, value); }

        private const int _off519 = 0xD4C;
        public int FieldD4C { get => ReadInt32(_off519); set => WriteInt32(_off519, value); }

        private const int _off520 = 0xD50;
        public int FieldD50 { get => ReadInt32(_off520); set => WriteInt32(_off520, value); }

        private const int _off521 = 0xD54;
        public int FieldD54 { get => ReadInt32(_off521); set => WriteInt32(_off521, value); }

        private const int _off522 = 0xD58;
        public int FieldD58 { get => ReadInt32(_off522); set => WriteInt32(_off522, value); }

        private const int _off523 = 0xD5C;
        public int FieldD5C { get => ReadInt32(_off523); set => WriteInt32(_off523, value); }

        private const int _off524 = 0xD60;
        public int FieldD60 { get => ReadInt32(_off524); set => WriteInt32(_off524, value); }

        private const int _off525 = 0xD64;
        public int FieldD64 { get => ReadInt32(_off525); set => WriteInt32(_off525, value); }

        private const int _off526 = 0xD68;
        public int FieldD68 { get => ReadInt32(_off526); set => WriteInt32(_off526, value); }

        private const int _off527 = 0xD6C;
        public int FieldD6C { get => ReadInt32(_off527); set => WriteInt32(_off527, value); }

        private const int _off528 = 0xD70;
        public int FieldD70 { get => ReadInt32(_off528); set => WriteInt32(_off528, value); }

        private const int _off529 = 0xD74;
        public int FieldD74 { get => ReadInt32(_off529); set => WriteInt32(_off529, value); }

        private const int _off530 = 0xD78;
        public int FieldD78 { get => ReadInt32(_off530); set => WriteInt32(_off530, value); }

        private const int _off531 = 0xD7C;
        public int FieldD7C { get => ReadInt32(_off531); set => WriteInt32(_off531, value); }

        private const int _off532 = 0xD80;
        public int FieldD80 { get => ReadInt32(_off532); set => WriteInt32(_off532, value); }

        private const int _off533 = 0xD84;
        public int FieldD84 { get => ReadInt32(_off533); set => WriteInt32(_off533, value); }

        private const int _off534 = 0xD88;
        public int FieldD88 { get => ReadInt32(_off534); set => WriteInt32(_off534, value); }

        private const int _off535 = 0xD8C;
        public int FieldD8C { get => ReadInt32(_off535); set => WriteInt32(_off535, value); }

        private const int _off536 = 0xD90;
        public int FieldD90 { get => ReadInt32(_off536); set => WriteInt32(_off536, value); }

        private const int _off537 = 0xD94;
        public int FieldD94 { get => ReadInt32(_off537); set => WriteInt32(_off537, value); }

        private const int _off538 = 0xD98;
        public int FieldD98 { get => ReadInt32(_off538); set => WriteInt32(_off538, value); }

        private const int _off539 = 0xD9C;
        public int FieldD9C { get => ReadInt32(_off539); set => WriteInt32(_off539, value); }

        private const int _off540 = 0xDA0;
        public int FieldDA0 { get => ReadInt32(_off540); set => WriteInt32(_off540, value); }

        private const int _off541 = 0xDA4;
        public int FieldDA4 { get => ReadInt32(_off541); set => WriteInt32(_off541, value); }

        private const int _off542 = 0xDA8;
        public int FieldDA8 { get => ReadInt32(_off542); set => WriteInt32(_off542, value); }

        private const int _off543 = 0xDAC;
        public int FieldDAC { get => ReadInt32(_off543); set => WriteInt32(_off543, value); }

        private const int _off544 = 0xDB0;
        public int FieldDB0 { get => ReadInt32(_off544); set => WriteInt32(_off544, value); }

        private const int _off545 = 0xDB4;
        public int FieldDB4 { get => ReadInt32(_off545); set => WriteInt32(_off545, value); }

        private const int _off546 = 0xDB8;
        public int FieldDB8 { get => ReadInt32(_off546); set => WriteInt32(_off546, value); }

        private const int _off547 = 0xDBC;
        public int FieldDBC { get => ReadInt32(_off547); set => WriteInt32(_off547, value); }

        private const int _off548 = 0xDC0;
        public int FieldDC0 { get => ReadInt32(_off548); set => WriteInt32(_off548, value); }

        private const int _off549 = 0xDC4;
        public int FieldDC4 { get => ReadInt32(_off549); set => WriteInt32(_off549, value); }

        private const int _off550 = 0xDC8;
        public int FieldDC8 { get => ReadInt32(_off550); set => WriteInt32(_off550, value); }

        private const int _off551 = 0xDCC;
        public int FieldDCC { get => ReadInt32(_off551); set => WriteInt32(_off551, value); }

        private const int _off552 = 0xDD0;
        public int FieldDD0 { get => ReadInt32(_off552); set => WriteInt32(_off552, value); }

        private const int _off553 = 0xDD4;
        public int FieldDD4 { get => ReadInt32(_off553); set => WriteInt32(_off553, value); }

        private const int _off554 = 0xDD8;
        public int FieldDD8 { get => ReadInt32(_off554); set => WriteInt32(_off554, value); }

        private const int _off555 = 0xDDC;
        public int FieldDDC { get => ReadInt32(_off555); set => WriteInt32(_off555, value); }

        private const int _off556 = 0xDE0;
        public int FieldDE0 { get => ReadInt32(_off556); set => WriteInt32(_off556, value); }

        private const int _off557 = 0xDE4;
        public int FieldDE4 { get => ReadInt32(_off557); set => WriteInt32(_off557, value); }

        private const int _off558 = 0xDE8;
        public int FieldDE8 { get => ReadInt32(_off558); set => WriteInt32(_off558, value); }

        private const int _off559 = 0xDEC;
        public int FieldDEC { get => ReadInt32(_off559); set => WriteInt32(_off559, value); }

        private const int _off560 = 0xDF0;
        public int FieldDF0 { get => ReadInt32(_off560); set => WriteInt32(_off560, value); }

        private const int _off561 = 0xDF4;
        public int FieldDF4 { get => ReadInt32(_off561); set => WriteInt32(_off561, value); }

        private const int _off562 = 0xDF8;
        public int FieldDF8 { get => ReadInt32(_off562); set => WriteInt32(_off562, value); }

        private const int _off563 = 0xDFC;
        public int FieldDFC { get => ReadInt32(_off563); set => WriteInt32(_off563, value); }

        private const int _off564 = 0xE00;
        public int FieldE00 { get => ReadInt32(_off564); set => WriteInt32(_off564, value); }

        private const int _off565 = 0xE04;
        public int FieldE04 { get => ReadInt32(_off565); set => WriteInt32(_off565, value); }

        private const int _off566 = 0xE08;
        public int FieldE08 { get => ReadInt32(_off566); set => WriteInt32(_off566, value); }

        private const int _off567 = 0xE0C;
        public int FieldE0C { get => ReadInt32(_off567); set => WriteInt32(_off567, value); }

        private const int _off568 = 0xE10;
        public int FieldE10 { get => ReadInt32(_off568); set => WriteInt32(_off568, value); }

        private const int _off569 = 0xE14;
        public int FieldE14 { get => ReadInt32(_off569); set => WriteInt32(_off569, value); }

        private const int _off570 = 0xE18;
        public int FieldE18 { get => ReadInt32(_off570); set => WriteInt32(_off570, value); }

        private const int _off571 = 0xE1C;
        public int FieldE1C { get => ReadInt32(_off571); set => WriteInt32(_off571, value); }

        private const int _off572 = 0xE20;
        public int FieldE20 { get => ReadInt32(_off572); set => WriteInt32(_off572, value); }

        private const int _off573 = 0xE24;
        public int FieldE24 { get => ReadInt32(_off573); set => WriteInt32(_off573, value); }

        private const int _off574 = 0xE28;
        public int FieldE28 { get => ReadInt32(_off574); set => WriteInt32(_off574, value); }

        private const int _off575 = 0xE2C;
        public int FieldE2C { get => ReadInt32(_off575); set => WriteInt32(_off575, value); }

        private const int _off576 = 0xE30;
        public int FieldE30 { get => ReadInt32(_off576); set => WriteInt32(_off576, value); }

        private const int _off577 = 0xE34;
        public int FieldE34 { get => ReadInt32(_off577); set => WriteInt32(_off577, value); }

        private const int _off578 = 0xE38;
        public int FieldE38 { get => ReadInt32(_off578); set => WriteInt32(_off578, value); }

        private const int _off579 = 0xE3C;
        public int FieldE3C { get => ReadInt32(_off579); set => WriteInt32(_off579, value); }

        private const int _off580 = 0xE40;
        public int FieldE40 { get => ReadInt32(_off580); set => WriteInt32(_off580, value); }

        private const int _off581 = 0xE44;
        public int FieldE44 { get => ReadInt32(_off581); set => WriteInt32(_off581, value); }

        private const int _off582 = 0xE48;
        public int FieldE48 { get => ReadInt32(_off582); set => WriteInt32(_off582, value); }

        private const int _off583 = 0xE4C;
        public int FieldE4C { get => ReadInt32(_off583); set => WriteInt32(_off583, value); }

        private const int _off584 = 0xE50;
        public int FieldE50 { get => ReadInt32(_off584); set => WriteInt32(_off584, value); }

        private const int _off585 = 0xE54;
        public int FieldE54 { get => ReadInt32(_off585); set => WriteInt32(_off585, value); }

        private const int _off586 = 0xE58;
        public int FieldE58 { get => ReadInt32(_off586); set => WriteInt32(_off586, value); }

        private const int _off587 = 0xE5C;
        public int FieldE5C { get => ReadInt32(_off587); set => WriteInt32(_off587, value); }

        private const int _off588 = 0xE60;
        public int FieldE60 { get => ReadInt32(_off588); set => WriteInt32(_off588, value); }

        private const int _off589 = 0xE64;
        public int FieldE64 { get => ReadInt32(_off589); set => WriteInt32(_off589, value); }

        private const int _off590 = 0xE68;
        public int FieldE68 { get => ReadInt32(_off590); set => WriteInt32(_off590, value); }

        private const int _off591 = 0xE6C;
        public int FieldE6C { get => ReadInt32(_off591); set => WriteInt32(_off591, value); }

        private const int _off592 = 0xE70;
        public int FieldE70 { get => ReadInt32(_off592); set => WriteInt32(_off592, value); }

        private const int _off593 = 0xE74;
        public int FieldE74 { get => ReadInt32(_off593); set => WriteInt32(_off593, value); }

        private const int _off594 = 0xE78;
        public int FieldE78 { get => ReadInt32(_off594); set => WriteInt32(_off594, value); }

        private const int _off595 = 0xE7C;
        public int FieldE7C { get => ReadInt32(_off595); set => WriteInt32(_off595, value); }

        private const int _off596 = 0xE80;
        public int FieldE80 { get => ReadInt32(_off596); set => WriteInt32(_off596, value); }

        private const int _off597 = 0xE84;
        public int FieldE84 { get => ReadInt32(_off597); set => WriteInt32(_off597, value); }

        private const int _off598 = 0xE88;
        public int FieldE88 { get => ReadInt32(_off598); set => WriteInt32(_off598, value); }

        private const int _off599 = 0xE8C;
        public int FieldE8C { get => ReadInt32(_off599); set => WriteInt32(_off599, value); }

        private const int _off600 = 0xE90;
        public int FieldE90 { get => ReadInt32(_off600); set => WriteInt32(_off600, value); }

        private const int _off601 = 0xE94;
        public int FieldE94 { get => ReadInt32(_off601); set => WriteInt32(_off601, value); }

        private const int _off602 = 0xE98;
        public int FieldE98 { get => ReadInt32(_off602); set => WriteInt32(_off602, value); }

        private const int _off603 = 0xE9C;
        public int FieldE9C { get => ReadInt32(_off603); set => WriteInt32(_off603, value); }

        private const int _off604 = 0xEA0;
        public int FieldEA0 { get => ReadInt32(_off604); set => WriteInt32(_off604, value); }

        private const int _off605 = 0xEA4;
        public int FieldEA4 { get => ReadInt32(_off605); set => WriteInt32(_off605, value); }

        private const int _off606 = 0xEA8;
        public int FieldEA8 { get => ReadInt32(_off606); set => WriteInt32(_off606, value); }

        private const int _off607 = 0xEAC;
        public int FieldEAC { get => ReadInt32(_off607); set => WriteInt32(_off607, value); }

        private const int _off608 = 0xEB0;
        public int FieldEB0 { get => ReadInt32(_off608); set => WriteInt32(_off608, value); }

        private const int _off609 = 0xEB4;
        public int FieldEB4 { get => ReadInt32(_off609); set => WriteInt32(_off609, value); }

        private const int _off610 = 0xEB8;
        public int FieldEB8 { get => ReadInt32(_off610); set => WriteInt32(_off610, value); }

        private const int _off611 = 0xEBC;
        public int FieldEBC { get => ReadInt32(_off611); set => WriteInt32(_off611, value); }

        private const int _off612 = 0xEC0;
        public int FieldEC0 { get => ReadInt32(_off612); set => WriteInt32(_off612, value); }

        private const int _off613 = 0xEC4;
        public int FieldEC4 { get => ReadInt32(_off613); set => WriteInt32(_off613, value); }

        private const int _off614 = 0xEC8;
        public int FieldEC8 { get => ReadInt32(_off614); set => WriteInt32(_off614, value); }

        private const int _off615 = 0xECC;
        public int FieldECC { get => ReadInt32(_off615); set => WriteInt32(_off615, value); }

        private const int _off616 = 0xED0;
        public int FieldED0 { get => ReadInt32(_off616); set => WriteInt32(_off616, value); }

        private const int _off617 = 0xED4;
        public int FieldED4 { get => ReadInt32(_off617); set => WriteInt32(_off617, value); }

        private const int _off618 = 0xED8;
        public int FieldED8 { get => ReadInt32(_off618); set => WriteInt32(_off618, value); }

        private const int _off619 = 0xEDC;
        public int FieldEDC { get => ReadInt32(_off619); set => WriteInt32(_off619, value); }

        private const int _off620 = 0xEE0;
        public int FieldEE0 { get => ReadInt32(_off620); set => WriteInt32(_off620, value); }

        private const int _off621 = 0xEE4;
        public int FieldEE4 { get => ReadInt32(_off621); set => WriteInt32(_off621, value); }

        private const int _off622 = 0xEE8;
        public int FieldEE8 { get => ReadInt32(_off622); set => WriteInt32(_off622, value); }

        private const int _off623 = 0xEEC;
        public int FieldEEC { get => ReadInt32(_off623); set => WriteInt32(_off623, value); }

        private const int _off624 = 0xEF0;
        public int FieldEF0 { get => ReadInt32(_off624); set => WriteInt32(_off624, value); }

        private const int _off625 = 0xEF4;
        public int FieldEF4 { get => ReadInt32(_off625); set => WriteInt32(_off625, value); }

        private const int _off626 = 0xEF8;
        public int FieldEF8 { get => ReadInt32(_off626); set => WriteInt32(_off626, value); }

        private const int _off627 = 0xEFC;
        public int FieldEFC { get => ReadInt32(_off627); set => WriteInt32(_off627, value); }

        private const int _off628 = 0xF00;
        public int FieldF00 { get => ReadInt32(_off628); set => WriteInt32(_off628, value); }

        private const int _off629 = 0xF04;
        public int FieldF04 { get => ReadInt32(_off629); set => WriteInt32(_off629, value); }

        private const int _off630 = 0xF08;
        public int FieldF08 { get => ReadInt32(_off630); set => WriteInt32(_off630, value); }

        private const int _off631 = 0xF0C;
        public int FieldF0C { get => ReadInt32(_off631); set => WriteInt32(_off631, value); }

        private const int _off632 = 0xF10;
        public int FieldF10 { get => ReadInt32(_off632); set => WriteInt32(_off632, value); }

        private const int _off633 = 0xF14;
        public int FieldF14 { get => ReadInt32(_off633); set => WriteInt32(_off633, value); }

        private const int _off634 = 0xF18;
        public int FieldF18 { get => ReadInt32(_off634); set => WriteInt32(_off634, value); }

        private const int _off635 = 0xF1C; // AIStruct*
        public IntPtr AiData { get => ReadPointer(_off635); set => WritePointer(_off635, value); }

        private const int _off636 = 0xF20;
        public int FieldF20 { get => ReadInt32(_off636); set => WriteInt32(_off636, value); }

        private const int _off637 = 0xF24; // CHalfturret*
        public IntPtr Halfturret { get => ReadPointer(_off637); set => WritePointer(_off637, value); }

        private const int _off638 = 0xF28;
        public SfxParameters SfxParameters { get; }

        private const int _off639 = 0xF2C;
        public int WeaponSfxHandle { get => ReadInt32(_off639); set => WriteInt32(_off639, value); }

        public StructArray<AIContext>? AIContext { get; }
        public uint AggroCount
        {
            get => ReadUInt32(AiData.ToInt32() - Memory.Offset - _offset + 0x1060);
            set => WriteUInt32(AiData.ToInt32() - Memory.Offset - _offset + 0x1060, value);
        }
        public StructArray<AIAggro>? AIAggro { get; }

        public CPlayer(Memory memory, int address) : base(memory, address)
        {
            Field100 = new UInt16Array(memory, address + _off38, 2);
            Collision = new CollisionVolume(memory, address + _off42);
            GunModel = new CModel(memory, address + _off58);
            FrozenModel = new CModel(memory, address + _off59);
            SpineNode = new IntPtrArray(memory, address + _off68, 2);
            ShootNode = new IntPtrArray(memory, address + _off69, 2);
            Biped1 = new CModel(memory, address + _off70);
            Biped2 = new CModel(memory, address + _off71);
            AltForm = new CModel(memory, address + _off74);
            GunSmoke = new CModel(memory, address + _off75);
            Controls = new PlayerControls(memory, address + _off85);
            Input = new PlayerInput(memory, address + _off124);
            CameraInfo = new CameraInfo(memory, address + _off184);
            LightInfo = new LightInfo(memory, address + _off190);
            EquipInfo = new EquipInfoPtr(memory, address + _off289);
            BeamHead = new CBeamProjectile(memory, address + _off290);
            SfxParameters = new SfxParameters(memory, address + _off638);
            if (AiData != 0)
            {
                var offset = AiData + new IntPtr(0x2FC);
                AIContext = new StructArray<AIContext>(memory, offset, 20, 0xA8, (Memory m, int a) => new AIContext(m, a));
                offset = AiData + new IntPtr(0x1064);
                AIAggro = new StructArray<AIAggro>(memory, offset, 25, 0x10, (Memory m, int a) => new AIAggro(m, a));
            }
        }

        public CPlayer(Memory memory, IntPtr address) : base(memory, address)
        {
            Field100 = new UInt16Array(memory, address + _off38, 2);
            Collision = new CollisionVolume(memory, address + _off42);
            GunModel = new CModel(memory, address + _off58);
            FrozenModel = new CModel(memory, address + _off59);
            SpineNode = new IntPtrArray(memory, address + _off68, 2);
            ShootNode = new IntPtrArray(memory, address + _off69, 2);
            Biped1 = new CModel(memory, address + _off70);
            Biped2 = new CModel(memory, address + _off71);
            AltForm = new CModel(memory, address + _off74);
            GunSmoke = new CModel(memory, address + _off75);
            Controls = new PlayerControls(memory, address + _off85);
            Input = new PlayerInput(memory, address + _off124);
            CameraInfo = new CameraInfo(memory, address + _off184);
            LightInfo = new LightInfo(memory, address + _off190);
            EquipInfo = new EquipInfoPtr(memory, address + _off289);
            BeamHead = new CBeamProjectile(memory, address + _off290);
            SfxParameters = new SfxParameters(memory, address + _off638);
            if (AiData != 0)
            {
                var offset = AiData + new IntPtr(0x2FC);
                AIContext = new StructArray<AIContext>(memory, offset, 20, 0xA8, (Memory m, int a) => new AIContext(m, a));
                offset = AiData + new IntPtr(0x1064);
                AIAggro = new StructArray<AIAggro>(memory, offset, 25, 0x10, (Memory m, int a) => new AIAggro(m, a));
            }
        }
    }

    public class AIContext : MemoryClass
    {
        private const int _off0 = 0x0;
        public int Func24Id { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public byte Field4 { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x5;
        public byte Field5 { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x6;
        public byte Field6 { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x7;
        public byte Field7 { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x8;
        public byte Field8 { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x9;
        public byte Field9 { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0xA;
        public byte FieldA { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0xB;
        public byte FieldB { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0xC;
        public byte FieldC { get => ReadByte(_off9); set => WriteByte(_off9, value); }

        private const int _off10 = 0xD;
        public byte FieldD { get => ReadByte(_off10); set => WriteByte(_off10, value); }

        private const int _off11 = 0xE;
        public byte FieldE { get => ReadByte(_off11); set => WriteByte(_off11, value); }

        private const int _off12 = 0xF;
        public byte FieldF { get => ReadByte(_off12); set => WriteByte(_off12, value); }

        private const int _off13 = 0x10;
        public byte Field10 { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x11;
        public byte Padding11 { get => ReadByte(_off14); set => WriteByte(_off14, value); }

        private const int _off15 = 0x12;
        public ushort Padding12 { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x14;
        public int Field14 { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x18;
        public int Field18 { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x1C;
        public int Field1C { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x20;
        public int Field20 { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        private const int _off20 = 0x24;
        public int Field24 { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0x28;
        public int Field28 { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x2C;
        public int Field2C { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x30;
        public int Field30 { get => ReadInt32(_off23); set => WriteInt32(_off23, value); }

        private const int _off24 = 0x34;
        public Vector3 Field34 { get => ReadVec3(_off24); set => WriteVec3(_off24, value); }

        private const int _off25 = 0x40;
        public int Field40 { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0x44;
        public int Field44 { get => ReadInt32(_off26); set => WriteInt32(_off26, value); }

        private const int _off27 = 0x48; // AIData1*
        public IntPtr CurData1Iter { get => ReadPointer(_off27); set => WritePointer(_off27, value); }

        private const int _off28 = 0x4C;
        public int CallCount { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0x50;
        public byte Depth { get => ReadByte(_off29); set => WriteByte(_off29, value); }

        private const int _off30 = 0x51;
        public byte Padding51 { get => ReadByte(_off30); set => WriteByte(_off30, value); }

        private const int _off31 = 0x52;
        public ushort Padding52 { get => ReadUInt16(_off31); set => WriteUInt16(_off31, value); }

        private const int _off32 = 0x54; // int[21]
        public Int32Array Weights { get; }

        private IntPtr _lastData1Ptr = IntPtr.Zero;
        private AIData1? _data1 = null;
        public AIData1? AIData1
        {
            get
            {
                if (CurData1Iter != _lastData1Ptr)
                {
                    if (CurData1Iter == 0)
                    {
                        _data1 = null;
                    }
                    else
                    {
                        _data1 = new AIData1(_memory, CurData1Iter);
                    }
                    _lastData1Ptr = CurData1Iter;
                }
                return _data1;
            }
        }

        public AIContext(Memory memory, int address) : base(memory, address)
        {
            Weights = new Int32Array(memory, address + _off32, 21);
        }

        public AIContext(Memory memory, IntPtr address) : base(memory, address)
        {
            Weights = new Int32Array(memory, address + _off32, 21);
        }
    }

    public class AIData1 : MemoryClass
    {
        private const int _off0 = 0x0;
        public int Func24Id { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public int Data1Count { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x8; // AIData1*
        public IntPtr Data1Ptr { get => ReadPointer(_off2); set => WritePointer(_off2, value); }
        public StructArray<AIData1>? Data1 { get; }

        private const int _off3 = 0xC;
        public int Data2Count { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x10; // AIData2*
        public IntPtr Data2Ptr { get => ReadPointer(_off4); set => WritePointer(_off4, value); }
        public StructArray<AIData2>? Data2 { get; }

        private const int _off5 = 0x14;
        public int Data3Count { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x18; // int*
        public IntPtr Data3a { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x1C;
        public int Data3bCount { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x20; // int*
        public IntPtr Data3b { get => ReadPointer(_off8); set => WritePointer(_off8, value); }

        public AIData1(Memory memory, int address) : base(memory, address)
        {
            if (Data1Ptr != 0 && Data1Count > 0)
            {
                Data1 = new StructArray<AIData1>(memory, Data1Ptr, Data1Count, 0x24, (Memory m, int a) => new AIData1(m, a));
            }
            if (Data2Ptr != 0 && Data2Count > 0)
            {
                Data2 = new StructArray<AIData2>(memory, Data2Ptr, Data2Count, 0x18, (Memory m, int a) => new AIData2(m, a));
            }
        }

        public AIData1(Memory memory, IntPtr address) : base(memory, address)
        {
            if (Data1Ptr != 0 && Data1Count > 0)
            {
                Data1 = new StructArray<AIData1>(memory, Data1Ptr, Data1Count, 0x24, (Memory m, int a) => new AIData1(m, a));
            }
            if (Data2Ptr != 0 && Data2Count > 0)
            {
                Data2 = new StructArray<AIData2>(memory, Data2Ptr, Data2Count, 0x18, (Memory m, int a) => new AIData2(m, a));
            }
        }
    }

    public class AIData2 : MemoryClass
    {
        private const int _off0 = 0x0;
        public int FuncIdx { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public int Data4Count { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x8; // AIData4*
        public IntPtr Data4 { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0xC;
        public int Data1SelectIdx { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x10;
        public int Weight { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x14; // AIData5*
        public IntPtr Data5 { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        public AIData2(Memory memory, int address) : base(memory, address)
        {
        }

        public AIData2(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class AIAggro : MemoryClass
    {
        private const int _off0 = 0x0;
        public ushort Flags { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x2;
        public ushort Field2 { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x4;
        public ushort Field4 { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x6;
        public ushort Field6 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x8; // CPlayer*
        public IntPtr Player1 { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0xC; // CPlayer*
        public IntPtr Player2 { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        public int Slot1 { get; set; }
        public int Slot2 { get; set; }

        public void UpdateSlots(CPlayer[] players)
        {
            Slot1 = -1;
            Slot2 = -1;
            for (int i = 0; i < 4; i++)
            {
                if (Player1 == players[i].Address)
                {
                    Slot1 = i;
                }
                if (Player2 == players[i].Address)
                {
                    Slot2 = i;
                }
            }
        }

        public byte Field0A
        {
            get
            {
                ushort value = Flags;
                return (byte)(value & 0xF);
            }
        }

        public byte Field0B
        {
            get
            {
                ushort value = Flags;
                return (byte)((value & 0xF0) >> 4);
            }
        }

        public byte Field0C
        {
            get
            {
                ushort value = Flags;
                return (byte)((value & 0xF00) >> 8);
            }
        }

        public byte Field0D
        {
            get
            {
                ushort value = Flags;
                return (byte)((value & 0xF000) >> 12);
            }
        }

        public byte Field2A
        {
            get
            {
                ushort value = Field2;
                return (byte)(value & 0xF);
            }
        }

        public ushort Field2B
        {
            get
            {
                ushort value = Field2;
                return (ushort)((value & 0xFFF0) >> 4);
            }
        }

        public AIAggro(Memory memory, int address) : base(memory, address)
        {
        }

        public AIAggro(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CBeamProjectile : CEntity
    {
        private const int _off0 = 0x18;
        public BeamType Beam { get => (BeamType)ReadByte(_off0); set => WriteByte(_off0, (byte)value); }

        private const int _off1 = 0x19;
        public BeamType BeamKind { get => (BeamType)ReadByte(_off1); set => WriteByte(_off1, (byte)value); }

        private const int _off2 = 0x1A;
        public byte DrawFuncId { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x1B;
        public byte ColEffect { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x1C;
        public byte SplashDmgType { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x1D;
        public byte DmgDirType { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x1E;
        public byte Field1E { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x1F;
        public byte SpeedInterpolation { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x20;
        public byte Afflictions { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0x21;
        public byte ListCount { get => ReadByte(_off9); set => WriteByte(_off9, value); }

        private const int _off10 = 0x22;
        public BeamFlags Flags { get => (BeamFlags)ReadUInt16(_off10); set => WriteUInt16(_off10, (ushort)value); }

        private const int _off11 = 0x24;
        public ushort Color { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x26;
        public ushort Damage { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x28;
        public ushort HeadshotDamage { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x2A;
        public ushort SplashDamage { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x2C;
        public ushort Lifespan { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x2E;
        public ushort Age { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x30;
        public ushort SpeedDecayTime { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x32;
        public ushort Field32 { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x34;
        public Vector3 PastPos0 { get => ReadVec3(_off19); set => WriteVec3(_off19, value); }

        private const int _off20 = 0x40;
        public Vector3 PastPos1 { get => ReadVec3(_off20); set => WriteVec3(_off20, value); }

        private const int _off21 = 0x4C;
        public Vector3 PastPos2 { get => ReadVec3(_off21); set => WriteVec3(_off21, value); }

        private const int _off22 = 0x58;
        public Vector3 PastPos3 { get => ReadVec3(_off22); set => WriteVec3(_off22, value); }

        private const int _off23 = 0x64;
        public Vector3 PastPos4 { get => ReadVec3(_off23); set => WriteVec3(_off23, value); }

        private const int _off24 = 0x70;
        public Vector3 Vec1 { get => ReadVec3(_off24); set => WriteVec3(_off24, value); }

        private const int _off25 = 0x7C;
        public Vector3 Field7C { get => ReadVec3(_off25); set => WriteVec3(_off25, value); }

        private const int _off26 = 0x88;
        public Vector3 Vec2 { get => ReadVec3(_off26); set => WriteVec3(_off26, value); }

        private const int _off27 = 0x94;
        public Vector3 CylBack { get => ReadVec3(_off27); set => WriteVec3(_off27, value); }

        private const int _off28 = 0xA0;
        public Vector3 CylFront { get => ReadVec3(_off28); set => WriteVec3(_off28, value); }

        private const int _off29 = 0xAC;
        public Vector3 SpawnPos { get => ReadVec3(_off29); set => WriteVec3(_off29, value); }

        private const int _off30 = 0xB8;
        public int Speed { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0xBC;
        public int InitialSpeed { get => ReadInt32(_off31); set => WriteInt32(_off31, value); }

        private const int _off32 = 0xC0;
        public int FinalSpeed { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0xC4;
        public Vector3 Velocity { get => ReadVec3(_off33); set => WriteVec3(_off33, value); }

        private const int _off34 = 0xD0;
        public Vector3 Acceleration { get => ReadVec3(_off34); set => WriteVec3(_off34, value); }

        private const int _off35 = 0xDC;
        public int Homing { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0xE0;
        public int FieldE0 { get => ReadInt32(_off36); set => WriteInt32(_off36, value); }

        private const int _off37 = 0xE4;
        public int MaxDist { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0xE8;
        public int FieldE8 { get => ReadInt32(_off38); set => WriteInt32(_off38, value); }

        private const int _off39 = 0xEC;
        public int FieldEC { get => ReadInt32(_off39); set => WriteInt32(_off39, value); }

        private const int _off40 = 0xF0;
        public int FieldF0 { get => ReadInt32(_off40); set => WriteInt32(_off40, value); }

        private const int _off41 = 0xF4;
        public int Scale { get => ReadInt32(_off41); set => WriteInt32(_off41, value); }

        private const int _off42 = 0xF8; // CEntity*
        public IntPtr Owner { get => ReadPointer(_off42); set => WritePointer(_off42, value); }

        private const int _off43 = 0xFC; // WeaponInfo*
        public IntPtr RicochetWeapon { get => ReadPointer(_off43); set => WritePointer(_off43, value); }

        private const int _off44 = 0x100; // CBeamProjectile*
        public IntPtr ListHead { get => ReadPointer(_off44); set => WritePointer(_off44, value); }

        private const int _off45 = 0x104; // CEntity*
        public IntPtr Target { get => ReadPointer(_off45); set => WritePointer(_off45, value); }

        private const int _off46 = 0x108;
        public CModel Model { get; }

        private const int _off47 = 0x150; // NodeRef**
        public IntPtr NodeRef { get => ReadPointer(_off47); set => WritePointer(_off47, value); }

        private const int _off48 = 0x154;
        public SfxParameters SfxParameters { get; }

        public CBeamProjectile(Memory memory, int address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off46);
            SfxParameters = new SfxParameters(memory, address + _off48);
        }

        public CBeamProjectile(Memory memory, IntPtr address) : base(memory, address)
        {
            Model = new CModel(memory, address + _off46);
            SfxParameters = new SfxParameters(memory, address + _off48);
        }
    }

    public class CModel : MemoryClass
    {
        private const int _off0 = 0x0; // Model* or EffectEntry*
        public IntPtr Union { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x4; // MaterialAnimations*
        public IntPtr MaterialAnimation { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x8; // TextureAnimations*
        public IntPtr TextureAnimation { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0xC; // TexcoordAnimations*
        public IntPtr TexcoordAnimation { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x10;
        public CNodeAnimation NodeAnimation { get; }

        private const int _off5 = 0x24; // Animation*
        public IntPtr Animation { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x28;
        public ushort Animid { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x2A;
        public ushort Field2A { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x2C;
        public ushort RoomAnimId { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x2E;
        public ushort Field2E { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x30;
        public ushort AnimFrame { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x32;
        public ushort Field32 { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x34;
        public ushort InitialFrame { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x36;
        public ushort Field36 { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x38;
        public ushort AnimationFlags { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x3A;
        public ushort Field3A { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x3C;
        public ushort Field3C { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x3E;
        public ushort Field3E { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x40;
        public ushort NodeAnimIgnoreRoot { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x42;
        public byte NodeAnimDelta { get => ReadByte(_off19); set => WriteByte(_off19, value); }

        private const int _off20 = 0x43;
        public byte MatAnimDelta { get => ReadByte(_off20); set => WriteByte(_off20, value); }

        private const int _off21 = 0x44;
        public byte TexAnimDelta { get => ReadByte(_off21); set => WriteByte(_off21, value); }

        private const int _off22 = 0x45;
        public byte UvAnimDelta { get => ReadByte(_off22); set => WriteByte(_off22, value); }

        private const int _off23 = 0x46;
        public ushort Field46 { get => ReadUInt16(_off23); set => WriteUInt16(_off23, value); }

        public CModel(Memory memory, int address) : base(memory, address)
        {
            NodeAnimation = new CNodeAnimation(memory, address + _off4);
        }

        public CModel(Memory memory, IntPtr address) : base(memory, address)
        {
            NodeAnimation = new CNodeAnimation(memory, address + _off4);
        }
    }

    public class CNodeAnimation : MemoryClass
    {
        private const int _off0 = 0x0; // NodeAnimations*
        public IntPtr NodeAnimation { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x4;
        public int NodeAnimFrame { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x8;
        public int NumNodes { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        private const int _off3 = 0xC; // Node*
        public IntPtr Nodes { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x10; // void*
        public IntPtr InitialMtx { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        public CNodeAnimation(Memory memory, int address) : base(memory, address)
        {
        }

        public CNodeAnimation(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class EntityCollision : MemoryClass
    {
        private const int _off0 = 0x0;
        public Matrix4x3 Matrix { get => ReadMtx43(_off0); set => WriteMtx43(_off0, value); }

        private const int _off1 = 0x30;
        public Matrix4x3 Inverse1 { get => ReadMtx43(_off1); set => WriteMtx43(_off1, value); }

        private const int _off2 = 0x60;
        public Matrix4x3 Inverse2 { get => ReadMtx43(_off2); set => WriteMtx43(_off2, value); }

        private const int _off3 = 0x90;
        public Vector3 Average { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x9C;
        public Vector3 Vec2 { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0xA8;
        public int MaxDist { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0xAC; // CEntity*
        public IntPtr EntPtr { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0xB0; // Collision*
        public IntPtr Collision { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        public EntityCollision(Memory memory, int address) : base(memory, address)
        {
        }

        public EntityCollision(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class EquipInfoPtr : MemoryClass
    {
        private const int _off0 = 0x0;
        public EquipFlags Flags { get => (EquipFlags)ReadByte(_off0); set => WriteByte(_off0, (byte)value); }

        private const int _off1 = 0x1;
        public byte Count { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x2;
        public ushort Padding2 { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x4; // CBeamProjectile*
        public IntPtr Beams { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x8; // WeaponInfo*
        public IntPtr WeaponInfo { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0xC; // unsigned__int16*
        public IntPtr AmmoPtr { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x10;
        public ushort ChargeLevel { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x12;
        public ushort SmokeLevel { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        public EquipInfoPtr(Memory memory, int address) : base(memory, address)
        {
        }

        public EquipInfoPtr(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class SfxParameters : MemoryClass
    {
        private const int _off0 = 0x0;
        public sbyte Volume { get => ReadSByte(_off0); set => WriteSByte(_off0, value); }

        private const int _off1 = 0x1;
        public byte PanX { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x2;
        public ushort PanZ { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        public SfxParameters(Memory memory, int address) : base(memory, address)
        {
        }

        public SfxParameters(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CollisionVolume : MemoryClass
    {
        private const int _off0 = 0x0;
        public VolumeType Type { get => (VolumeType)ReadUInt32(_off0); set => WriteUInt32(_off0, (uint)value); }

        private const int _box0 = 0x4;
        public Vector3 BoxVec1 { get => ReadVec3(_box0); set => WriteVec3(_box0, value); }

        private const int _box1 = 0x10;
        public Vector3 BoxVec2 { get => ReadVec3(_box1); set => WriteVec3(_box1, value); }

        private const int _box2 = 0x1C;
        public Vector3 BoxVec3 { get => ReadVec3(_box2); set => WriteVec3(_box2, value); }

        private const int _box3 = 0x28;
        public Vector3 BoxPos { get => ReadVec3(_box3); set => WriteVec3(_box3, value); }

        private const int _box4 = 0x34;
        public Vector3 BoxDot { get => ReadVec3(_box4); set => WriteVec3(_box4, value); }

        private const int _cyl0 = 0x4;
        public Vector3 CylinderVec { get => ReadVec3(_cyl0); set => WriteVec3(_cyl0, value); }

        private const int _cyl1 = 0x10;
        public Vector3 CylinderCenter { get => ReadVec3(_cyl1); set => WriteVec3(_cyl1, value); }

        private const int _cyl2 = 0x1C;
        public int CylinderRadius { get => ReadInt32(_cyl2); set => WriteInt32(_cyl2, value); }

        private const int _cyl3 = 0x20;
        public int CylinderDot { get => ReadInt32(_cyl3); set => WriteInt32(_cyl3, value); }

        private const int _sph0 = 0x4;
        public Vector3 SphereCenter { get => ReadVec3(_sph0); set => WriteVec3(_sph0, value); }

        private const int _sph1 = 0x10;
        public int SphereRadius { get => ReadInt32(_sph1); set => WriteInt32(_sph1, value); }

        public CollisionVolume(Memory memory, int address) : base(memory, address)
        {
        }

        public CollisionVolume(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class Light : MemoryClass
    {
        private const int _off0 = 0x0;
        public Vector3 Dir { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0xC;
        public ColorRgb Color { get => ReadColor3(_off1); set => WriteColor3(_off1, value); }

        public Light(Memory memory, int address) : base(memory, address)
        {
        }

        public Light(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class LightInfo : MemoryClass
    {
        private const int _off0 = 0x0;
        public Light Light1 { get; }

        private const int _off1 = 0xF;
        public byte PaddingF { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x10;
        public Light Light2 { get; }

        public LightInfo(Memory memory, int address) : base(memory, address)
        {
            Light1 = new Light(memory, address + _off0);
            Light2 = new Light(memory, address + _off2);
        }

        public LightInfo(Memory memory, IntPtr address) : base(memory, address)
        {
            Light1 = new Light(memory, address + _off0);
            Light2 = new Light(memory, address + _off2);
        }
    }

    public class CameraInfo : MemoryClass
    {
        private const int _off0 = 0x0;
        public Vector3 Pos { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0xC;
        public Vector3 Vec2 { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x18;
        public Vector3 Vec3 { get => ReadVec3(_off2); set => WriteVec3(_off2, value); }

        private const int _off3 = 0x24;
        public Vector3 Vec4 { get => ReadVec3(_off3); set => WriteVec3(_off3, value); }

        private const int _off4 = 0x30;
        public Vector3 Vec5 { get => ReadVec3(_off4); set => WriteVec3(_off4, value); }

        private const int _off5 = 0x3C;
        public Vector3 Vec6 { get => ReadVec3(_off5); set => WriteVec3(_off5, value); }

        private const int _off6 = 0x48;
        public int Field48 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x4C;
        public int Field4C { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x50;
        public int Field50 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x54;
        public int Field54 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x58;
        public Matrix4x3 Mtx1 { get => ReadMtx43(_off10); set => WriteMtx43(_off10, value); }

        private const int _off11 = 0x88;
        public int Field88 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x8C;
        public int Field8C { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x90;
        public int Field90 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x94;
        public int Field94 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x98;
        public int Field98 { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x9C;
        public int Field9C { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0xA0;
        public int FieldA0 { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0xA4;
        public int FieldA4 { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0xA8;
        public int FieldA8 { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        private const int _off20 = 0xAC;
        public int FieldAC { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0xB0;
        public int FieldB0 { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0xB4;
        public int FieldB4 { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0xB8;
        public Matrix4x3 Mtx2 { get => ReadMtx43(_off23); set => WriteMtx43(_off23, value); }

        private const int _off24 = 0xE8;
        public int Shake { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        private const int _off25 = 0xEC;
        public int Fov { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0xF0;
        public int FieldF0 { get => ReadInt32(_off26); set => WriteInt32(_off26, value); }

        private const int _off27 = 0xF4;
        public int FieldF4 { get => ReadInt32(_off27); set => WriteInt32(_off27, value); }

        private const int _off28 = 0xF8;
        public int NearLr { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0xFC;
        public int NearTb { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0x100;
        public int NearDist { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0x104;
        public int FarDist { get => ReadInt32(_off31); set => WriteInt32(_off31, value); }

        private const int _off32 = 0x108;
        public int ViewportY2 { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0x10C;
        public int ViewportY1 { get => ReadInt32(_off33); set => WriteInt32(_off33, value); }

        private const int _off34 = 0x110;
        public int ViewportX1 { get => ReadInt32(_off34); set => WriteInt32(_off34, value); }

        private const int _off35 = 0x114;
        public int ViewportX2 { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0x118; // NodeRef*
        public IntPtr CurNode { get => ReadPointer(_off36); set => WritePointer(_off36, value); }

        public CameraInfo(Memory memory, int address) : base(memory, address)
        {
        }

        public CameraInfo(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class PlayerControls : MemoryClass
    {
        private const int _off0 = 0x0;
        public int Field0 { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public ushort Field4 { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x6;
        public ushort Field6 { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x8;
        public ushort Field8 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0xA;
        public ushort FieldA { get => ReadUInt16(_off4); set => WriteUInt16(_off4, value); }

        private const int _off5 = 0xC;
        public ushort FieldC { get => ReadUInt16(_off5); set => WriteUInt16(_off5, value); }

        private const int _off6 = 0xE;
        public ushort FieldE { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x10;
        public ushort Field10 { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x12;
        public ushort Field12 { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x14;
        public int Field14 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x18;
        public int Field18 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x1C;
        public int Field1C { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x20;
        public int Field20 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x24;
        public ushort Field24 { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x26;
        public ushort Field26 { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x28;
        public ushort Field28 { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x2A;
        public ushort Field2A { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x2C;
        public ushort Field2C { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x2E;
        public ushort Field2E { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x30;
        public ushort Field30 { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x32;
        public ushort Field32 { get => ReadUInt16(_off20); set => WriteUInt16(_off20, value); }

        private const int _off21 = 0x34;
        public int Field34 { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x38;
        public int Field38 { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x3C;
        public int Field3C { get => ReadInt32(_off23); set => WriteInt32(_off23, value); }

        private const int _off24 = 0x40;
        public int Field40 { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        private const int _off25 = 0x44;
        public int Field44 { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0x48;
        public int Field48 { get => ReadInt32(_off26); set => WriteInt32(_off26, value); }

        private const int _off27 = 0x4C;
        public int Field4C { get => ReadInt32(_off27); set => WriteInt32(_off27, value); }

        private const int _off28 = 0x50;
        public int Field50 { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0x54;
        public int Field54 { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0x58;
        public int Field58 { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0x5C;
        public int Field5C { get => ReadInt32(_off31); set => WriteInt32(_off31, value); }

        private const int _off32 = 0x60;
        public int Field60 { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0x64;
        public int Field64 { get => ReadInt32(_off33); set => WriteInt32(_off33, value); }

        private const int _off34 = 0x68;
        public int Field68 { get => ReadInt32(_off34); set => WriteInt32(_off34, value); }

        private const int _off35 = 0x6C;
        public int Field6C { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0x70;
        public int Field70 { get => ReadInt32(_off36); set => WriteInt32(_off36, value); }

        private const int _off37 = 0x74;
        public int Field74 { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0x78;
        public int Field78 { get => ReadInt32(_off38); set => WriteInt32(_off38, value); }

        private const int _off39 = 0x7C;
        public int Field7C { get => ReadInt32(_off39); set => WriteInt32(_off39, value); }

        private const int _off40 = 0x80;
        public int Field80 { get => ReadInt32(_off40); set => WriteInt32(_off40, value); }

        private const int _off41 = 0x84;
        public int Field84 { get => ReadInt32(_off41); set => WriteInt32(_off41, value); }

        private const int _off42 = 0x88;
        public int Field88 { get => ReadInt32(_off42); set => WriteInt32(_off42, value); }

        private const int _off43 = 0x8C;
        public int Field8C { get => ReadInt32(_off43); set => WriteInt32(_off43, value); }

        private const int _off44 = 0x90;
        public int Field90 { get => ReadInt32(_off44); set => WriteInt32(_off44, value); }

        private const int _off45 = 0x94;
        public int Field94 { get => ReadInt32(_off45); set => WriteInt32(_off45, value); }

        private const int _off46 = 0x98;
        public int Field98 { get => ReadInt32(_off46); set => WriteInt32(_off46, value); }

        public PlayerControls(Memory memory, int address) : base(memory, address)
        {
        }

        public PlayerControls(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class PlayerInput : MemoryClass
    {
        private const int _off0 = 0x0;
        public ushort Field0 { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x2;
        public ushort Field2 { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x4;
        public ushort Field4 { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x6;
        public ushort Field6 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x8;
        public ushort Field8 { get => ReadUInt16(_off4); set => WriteUInt16(_off4, value); }

        private const int _off5 = 0xA;
        public ushort FieldA { get => ReadUInt16(_off5); set => WriteUInt16(_off5, value); }

        private const int _off6 = 0xC;
        public int FieldC { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x10;
        public int Field10 { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x14;
        public int Field14 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x18;
        public int Field18 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x1C;
        public int Field1C { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x20;
        public int Field20 { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x24;
        public ushort Field24 { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x26;
        public byte Field26 { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x27;
        public byte Field27 { get => ReadByte(_off14); set => WriteByte(_off14, value); }

        private const int _off15 = 0x28;
        public ushort Field28 { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x2A;
        public ushort Field2A { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x2C;
        public ushort Field2C { get => ReadUInt16(_off17); set => WriteUInt16(_off17, value); }

        private const int _off18 = 0x2E;
        public ushort Field2E { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x30;
        public ushort Field30 { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x32;
        public ushort Field32 { get => ReadUInt16(_off20); set => WriteUInt16(_off20, value); }

        private const int _off21 = 0x34;
        public int Field34 { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x38;
        public int Field38 { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x3C;
        public int Field3C { get => ReadInt32(_off23); set => WriteInt32(_off23, value); }

        private const int _off24 = 0x40;
        public int Field40 { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        private const int _off25 = 0x44;
        public int Field44 { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        public PlayerInput(Memory memory, int address) : base(memory, address)
        {
        }

        public PlayerInput(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class CameraSequence : MemoryClass
    {
        private const int _off0 = 0x0;
        public byte Flags { get => ReadByte(_off0); set => WriteByte(_off0, value); }

        private const int _off1 = 0x1;
        public byte Version { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x2;
        public byte Field2 { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x3;
        public byte Field3 { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x4;
        public int KeyframeElapsed { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x8; // CameraSequenceKeyframe*
        public IntPtr Keyframes { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0xC; // CameraSequenceKeyframe*
        public IntPtr NextKeyframe { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x10; // CameraInfo*
        public IntPtr CameraInfoPtr { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        private const int _off8 = 0x14;
        public CameraInfo CameraInfo { get; }

        public CameraSequence(Memory memory, int address) : base(memory, address)
        {
            CameraInfo = new CameraInfo(memory, address + _off8);
        }

        public CameraSequence(Memory memory, IntPtr address) : base(memory, address)
        {
            CameraInfo = new CameraInfo(memory, address + _off8);
        }
    }

    public class CameraSequenceKeyframe : MemoryClass
    {
        private const int _off0 = 0x0;
        public Vector3 Pos { get => ReadVec3(_off0); set => WriteVec3(_off0, value); }

        private const int _off1 = 0xC;
        public Vector3 ToTarget { get => ReadVec3(_off1); set => WriteVec3(_off1, value); }

        private const int _off2 = 0x18;
        public int Roll { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        private const int _off3 = 0x1C;
        public int Fov { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x20;
        public int MoveTime { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x24;
        public int HoldTime { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x28;
        public int FadeInTime { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x2C;
        public int FadeOutTime { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x30;
        public FadeType FadeInType { get => (FadeType)ReadByte(_off8); set => WriteByte(_off8, (byte)value); }

        private const int _off9 = 0x31;
        public FadeType FadeOutType { get => (FadeType)ReadByte(_off9); set => WriteByte(_off9, (byte)value); }

        private const int _off10 = 0x32;
        public byte PrevFrameInfluence { get => ReadByte(_off10); set => WriteByte(_off10, value); }

        private const int _off11 = 0x33;
        public byte AfterFrameInfluence { get => ReadByte(_off11); set => WriteByte(_off11, value); }

        private const int _off12 = 0x34;
        public byte UseEntityTransform { get => ReadByte(_off12); set => WriteByte(_off12, value); }

        private const int _off13 = 0x35;
        public byte Padding35 { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x36;
        public ushort Padding36 { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x38; // EntityIdOrRef
        public IntPtr Ent1Ref { get => ReadPointer(_off15); set => WritePointer(_off15, value); }

        private const int _off16 = 0x3C; // EntityIdOrRef
        public IntPtr Ent2Ref { get => ReadPointer(_off16); set => WritePointer(_off16, value); }

        private const int _off17 = 0x40; // EntityIdOrRef
        public IntPtr EventTarget { get => ReadPointer(_off17); set => WritePointer(_off17, value); }

        private const int _off18 = 0x44;
        public ushort EventId { get => ReadUInt16(_off18); set => WriteUInt16(_off18, value); }

        private const int _off19 = 0x46;
        public ushort EventParam { get => ReadUInt16(_off19); set => WriteUInt16(_off19, value); }

        private const int _off20 = 0x48;
        public int Easing { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0x4C;
        public int Unused4C { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x50;
        public int Unused50 { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x54; // NodeRef*
        public IntPtr NodeRef { get => ReadPointer(_off23); set => WritePointer(_off23, value); }

        private const int _off24 = 0x58; // byte[12]
        public ByteArray NodeNameRest { get; }

        private const int _off25 = 0x64; // CameraSequenceKeyframe*
        public IntPtr Next { get => ReadPointer(_off25); set => WritePointer(_off25, value); }

        private const int _off26 = 0x68; // CameraSequenceKeyframe*
        public IntPtr Prev { get => ReadPointer(_off26); set => WritePointer(_off26, value); }

        private const int _off27 = 0x6C;
        public byte Index { get => ReadByte(_off27); set => WriteByte(_off27, value); }

        private const int _off28 = 0x6D;
        public byte Padding6D { get => ReadByte(_off28); set => WriteByte(_off28, value); }

        private const int _off29 = 0x6E;
        public ushort Padding6E { get => ReadUInt16(_off29); set => WriteUInt16(_off29, value); }

        public CameraSequenceKeyframe(Memory memory, int address) : base(memory, address)
        {
            NodeNameRest = new ByteArray(memory, address + _off24, 12);
        }

        public CameraSequenceKeyframe(Memory memory, IntPtr address) : base(memory, address)
        {
            NodeNameRest = new ByteArray(memory, address + _off24, 12);
        }
    }

    public class GameState : MemoryClass
    {
        private const int _off0 = 0x0;
        public GameMode GameMode { get => (GameMode)ReadByte(_off0); set => WriteByte(_off0, (byte)value); }

        private const int _off1 = 0x1;
        public byte RoomId { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x2;
        public byte AreaId { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x3;
        public byte PlayerCount { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x4;
        public byte MaxPlayers { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x5;
        public byte CountOfBitsOfSomething { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x6;
        public byte BotCount { get => ReadByte(_off6); set => WriteByte(_off6, value); }

        private const int _off7 = 0x7;
        public byte Field7 { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0x8; // byte[4]
        public ByteArray Field8 { get; }

        private const int _off9 = 0xC;
        public byte DmgMultIdx { get => ReadByte(_off9); set => WriteByte(_off9, value); }

        private const int _off10 = 0xD;
        public byte FieldD { get => ReadByte(_off10); set => WriteByte(_off10, value); }

        private const int _off11 = 0xE;
        public ushort SomeFlags { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x10;
        public ushort Field10 { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x12;
        public ushort Field12 { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x14;
        public ushort PointLimit { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x16;
        public ushort Field16 { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x18;
        public int BattleTimeLimit { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x1C;
        public int EscapeState { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x20;
        public int TimeLimit { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x24; // int[4]
        public Int32Array Sensitivity { get; }

        private const int _off20 = 0x34; // byte[4]
        public ByteArray InvertSomething { get; }

        private const int _off21 = 0x38; // byte[4]
        public ByteArray Field38 { get; }

        private const int _off22 = 0x3C; // byte[4]
        public ByteArray Hunters { get; }

        private const int _off23 = 0x40; // byte[4]
        public ByteArray SuitColors { get; }

        private const int _off24 = 0x44; // byte[4]
        public ByteArray PlayerNames { get; }

        private const int _off25 = 0x48; // byte[4]
        public ByteArray BotEncounterState { get; }

        private const int _off26 = 0x4C; // byte[4]
        public ByteArray TeamIds { get; }

        private const int _off27 = 0x50; // byte[4]
        public ByteArray FieldA0 { get; }

        private const int _off28 = 0x54; // ushort[4]
        public UInt16Array BotSpawnerEntIds { get; }

        private const int _off29 = 0x5C;
        public byte PrimeHunterStats { get => ReadByte(_off29); set => WriteByte(_off29, value); }

        private const int _off30 = 0x5D;
        public byte PrimeHunter { get => ReadByte(_off30); set => WriteByte(_off30, value); }

        private const int _off31 = 0x5E;
        public byte RadShowPlayers { get => ReadByte(_off31); set => WriteByte(_off31, value); }

        private const int _off32 = 0x5F;
        public byte FieldAF { get => ReadByte(_off32); set => WriteByte(_off32, value); }

        private const int _off33 = 0x60; // int[4]
        public Int32Array PrimeTime { get; }

        private const int _off34 = 0x70; // int[4]
        public Int32Array FieldC0 { get; }

        private const int _off35 = 0x80;
        public int FieldD0 { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0x84;
        public int FieldD4 { get => ReadInt32(_off36); set => WriteInt32(_off36, value); }

        private const int _off37 = 0x88;
        public int FieldD8 { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0x8C;
        public int FieldDC { get => ReadInt32(_off38); set => WriteInt32(_off38, value); }

        private const int _off39 = 0x90;
        public int FieldE0 { get => ReadInt32(_off39); set => WriteInt32(_off39, value); }

        private const int _off40 = 0x94;
        public int FieldE4 { get => ReadInt32(_off40); set => WriteInt32(_off40, value); }

        private const int _off41 = 0x98;
        public int FieldE8 { get => ReadInt32(_off41); set => WriteInt32(_off41, value); }

        private const int _off42 = 0x9C;
        public int FieldEC { get => ReadInt32(_off42); set => WriteInt32(_off42, value); }

        private const int _off43 = 0xA0;
        public int FieldF0 { get => ReadInt32(_off43); set => WriteInt32(_off43, value); }

        private const int _off44 = 0xA4;
        public int FieldF4 { get => ReadInt32(_off44); set => WriteInt32(_off44, value); }

        private const int _off45 = 0xA8;
        public int FieldF8 { get => ReadInt32(_off45); set => WriteInt32(_off45, value); }

        private const int _off46 = 0xAC;
        public int FieldFC { get => ReadInt32(_off46); set => WriteInt32(_off46, value); }

        private const int _off47 = 0xB0;
        public int Field100 { get => ReadInt32(_off47); set => WriteInt32(_off47, value); }

        private const int _off48 = 0xB4;
        public int Field104 { get => ReadInt32(_off48); set => WriteInt32(_off48, value); }

        private const int _off49 = 0xB8;
        public int Field108 { get => ReadInt32(_off49); set => WriteInt32(_off49, value); }

        private const int _off50 = 0xBC;
        public int Field10C { get => ReadInt32(_off50); set => WriteInt32(_off50, value); }

        private const int _off51 = 0xC0;
        public int Field110 { get => ReadInt32(_off51); set => WriteInt32(_off51, value); }

        private const int _off52 = 0xC4;
        public int Field114 { get => ReadInt32(_off52); set => WriteInt32(_off52, value); }

        private const int _off53 = 0xC8;
        public int Field118 { get => ReadInt32(_off53); set => WriteInt32(_off53, value); }

        private const int _off54 = 0xCC;
        public int Field11C { get => ReadInt32(_off54); set => WriteInt32(_off54, value); }

        private const int _off55 = 0xD0;
        public int Field120 { get => ReadInt32(_off55); set => WriteInt32(_off55, value); }

        private const int _off56 = 0xD4;
        public int Field124 { get => ReadInt32(_off56); set => WriteInt32(_off56, value); }

        private const int _off57 = 0xD8;
        public int Field128 { get => ReadInt32(_off57); set => WriteInt32(_off57, value); }

        private const int _off58 = 0xDC;
        public int Field12C { get => ReadInt32(_off58); set => WriteInt32(_off58, value); }

        private const int _off59 = 0xE0;
        public int Field130 { get => ReadInt32(_off59); set => WriteInt32(_off59, value); }

        private const int _off60 = 0xE4;
        public int Field134 { get => ReadInt32(_off60); set => WriteInt32(_off60, value); }

        private const int _off61 = 0xE8;
        public int Field138 { get => ReadInt32(_off61); set => WriteInt32(_off61, value); }

        private const int _off62 = 0xEC;
        public int Field13C { get => ReadInt32(_off62); set => WriteInt32(_off62, value); }

        private const int _off63 = 0xF0;
        public int Field140 { get => ReadInt32(_off63); set => WriteInt32(_off63, value); }

        private const int _off64 = 0xF4;
        public int Field144 { get => ReadInt32(_off64); set => WriteInt32(_off64, value); }

        private const int _off65 = 0xF8;
        public int Field148 { get => ReadInt32(_off65); set => WriteInt32(_off65, value); }

        private const int _off66 = 0xFC;
        public int Field14C { get => ReadInt32(_off66); set => WriteInt32(_off66, value); }

        private const int _off67 = 0x100;
        public int Field150 { get => ReadInt32(_off67); set => WriteInt32(_off67, value); }

        private const int _off68 = 0x104;
        public int Field154 { get => ReadInt32(_off68); set => WriteInt32(_off68, value); }

        private const int _off69 = 0x108;
        public int Field158 { get => ReadInt32(_off69); set => WriteInt32(_off69, value); }

        private const int _off70 = 0x10C;
        public int Field15C { get => ReadInt32(_off70); set => WriteInt32(_off70, value); }

        private const int _off71 = 0x110; // int[4]
        public Int32Array Field160 { get; }

        private const int _off72 = 0x120;
        public int Field170 { get => ReadInt32(_off72); set => WriteInt32(_off72, value); }

        private const int _off73 = 0x124;
        public int Field174 { get => ReadInt32(_off73); set => WriteInt32(_off73, value); }

        private const int _off74 = 0x128;
        public int Field178 { get => ReadInt32(_off74); set => WriteInt32(_off74, value); }

        private const int _off75 = 0x12C;
        public int Field17C { get => ReadInt32(_off75); set => WriteInt32(_off75, value); }

        private const int _off76 = 0x130; // int[4]
        public Int32Array Field180 { get; }

        private const int _off77 = 0x140; // int[4]
        public Int32Array Deaths { get; }

        private const int _off78 = 0x150; // int[4]
        public Int32Array Field1A0 { get; }

        private const int _off79 = 0x160;
        public int Field1B0 { get => ReadInt32(_off79); set => WriteInt32(_off79, value); }

        private const int _off80 = 0x164;
        public int Field1B4 { get => ReadInt32(_off80); set => WriteInt32(_off80, value); }

        private const int _off81 = 0x168;
        public int Field1B8 { get => ReadInt32(_off81); set => WriteInt32(_off81, value); }

        private const int _off82 = 0x16C;
        public int Field1BC { get => ReadInt32(_off82); set => WriteInt32(_off82, value); }

        private const int _off83 = 0x170; // int[4]
        public Int32Array TeamkillsMaybe { get; }

        private const int _off84 = 0x180; // int[4]
        public Int32Array SuicidesMaybe { get; }

        private const int _off85 = 0x190; // int[4]
        public Int32Array Field1E0 { get; }

        private const int _off86 = 0x1A0; // int[4]
        public Int32Array HeadshotsMaybe { get; }

        private const int _off87 = 0x1B0; // int[4]
        public Int32Array Field200 { get; }

        private const int _off88 = 0x1C0; // int[4]
        public Int32Array DmgDealt { get; }

        private const int _off89 = 0x1D0; // int[4]
        public Int32Array DmgMax { get; }

        private const int _off90 = 0x1E0; // int[4]
        public Int32Array BattlePoints { get; }

        private const int _off91 = 0x1F0;
        public int Field240 { get => ReadInt32(_off91); set => WriteInt32(_off91, value); }

        private const int _off92 = 0x1F4;
        public int Field244 { get => ReadInt32(_off92); set => WriteInt32(_off92, value); }

        private const int _off93 = 0x1F8;
        public int Field248 { get => ReadInt32(_off93); set => WriteInt32(_off93, value); }

        private const int _off94 = 0x1FC;
        public int Field24C { get => ReadInt32(_off94); set => WriteInt32(_off94, value); }

        private const int _off95 = 0x200; // byte[4]
        public ByteArray Standings { get; }

        private const int _off96 = 0x204;
        public int Field254 { get => ReadInt32(_off96); set => WriteInt32(_off96, value); }

        private const int _off97 = 0x208;
        public byte Field258 { get => ReadByte(_off97); set => WriteByte(_off97, value); }

        private const int _off98 = 0x209;
        public byte Field259 { get => ReadByte(_off98); set => WriteByte(_off98, value); }

        private const int _off99 = 0x20A;
        public ushort Field25A { get => ReadUInt16(_off99); set => WriteUInt16(_off99, value); }

        private const int _off100 = 0x20C; // byte[4]
        public ByteArray KillStreaks { get; }

        private const int _off101 = 0x210; // ushort[4]
        public UInt16Array Field260 { get; }

        private const int _off102 = 0x218; // ushort[4]
        public UInt16Array Field268 { get; }

        private const int _off103 = 0x220; // ushort[4]
        public UInt16Array Field270 { get; }

        private const int _off104 = 0x228;
        public int Field278 { get => ReadInt32(_off104); set => WriteInt32(_off104, value); }

        private const int _off105 = 0x22C;
        public int Frames { get => ReadInt32(_off105); set => WriteInt32(_off105, value); }

        private const int _off106 = 0x230;
        public int LoadRoomId { get => ReadInt32(_off106); set => WriteInt32(_off106, value); }

        private const int _off107 = 0x234;
        public int Field284 { get => ReadInt32(_off107); set => WriteInt32(_off107, value); }

        private const int _off108 = 0x238;
        public int Field288 { get => ReadInt32(_off108); set => WriteInt32(_off108, value); }

        private const int _off109 = 0x23C;
        public byte LayerId { get => ReadByte(_off109); set => WriteByte(_off109, value); }

        private const int _off110 = 0x23D;
        public byte Field28D { get => ReadByte(_off110); set => WriteByte(_off110, value); }

        private const int _off111 = 0x23E;
        public ushort Field28E { get => ReadUInt16(_off111); set => WriteUInt16(_off111, value); }

        public GameState(Memory memory, int address) : base(memory, address)
        {
            Field8 = new ByteArray(memory, address + _off8, 4);
            Sensitivity = new Int32Array(memory, address + _off19, 4);
            InvertSomething = new ByteArray(memory, address + _off20, 4);
            Field38 = new ByteArray(memory, address + _off21, 4);
            Hunters = new ByteArray(memory, address + _off22, 4);
            SuitColors = new ByteArray(memory, address + _off23, 4);
            PlayerNames = new ByteArray(memory, address + _off24, 4);
            BotEncounterState = new ByteArray(memory, address + _off25, 4);
            TeamIds = new ByteArray(memory, address + _off26, 4);
            FieldA0 = new ByteArray(memory, address + _off27, 4);
            BotSpawnerEntIds = new UInt16Array(memory, address + _off28, 4);
            PrimeTime = new Int32Array(memory, address + _off33, 4);
            FieldC0 = new Int32Array(memory, address + _off34, 4);
            Field160 = new Int32Array(memory, address + _off71, 4);
            Field180 = new Int32Array(memory, address + _off76, 4);
            Deaths = new Int32Array(memory, address + _off77, 4);
            Field1A0 = new Int32Array(memory, address + _off78, 4);
            TeamkillsMaybe = new Int32Array(memory, address + _off83, 4);
            SuicidesMaybe = new Int32Array(memory, address + _off84, 4);
            Field1E0 = new Int32Array(memory, address + _off85, 4);
            HeadshotsMaybe = new Int32Array(memory, address + _off86, 4);
            Field200 = new Int32Array(memory, address + _off87, 4);
            DmgDealt = new Int32Array(memory, address + _off88, 4);
            DmgMax = new Int32Array(memory, address + _off89, 4);
            BattlePoints = new Int32Array(memory, address + _off90, 4);
            Standings = new ByteArray(memory, address + _off95, 4);
            KillStreaks = new ByteArray(memory, address + _off100, 4);
            Field260 = new UInt16Array(memory, address + _off101, 4);
            Field268 = new UInt16Array(memory, address + _off102, 4);
            Field270 = new UInt16Array(memory, address + _off103, 4);
        }

        public GameState(Memory memory, IntPtr address) : base(memory, address)
        {
            Field8 = new ByteArray(memory, address + _off8, 4);
            Sensitivity = new Int32Array(memory, address + _off19, 4);
            InvertSomething = new ByteArray(memory, address + _off20, 4);
            Field38 = new ByteArray(memory, address + _off21, 4);
            Hunters = new ByteArray(memory, address + _off22, 4);
            SuitColors = new ByteArray(memory, address + _off23, 4);
            PlayerNames = new ByteArray(memory, address + _off24, 4);
            BotEncounterState = new ByteArray(memory, address + _off25, 4);
            TeamIds = new ByteArray(memory, address + _off26, 4);
            FieldA0 = new ByteArray(memory, address + _off27, 4);
            BotSpawnerEntIds = new UInt16Array(memory, address + _off28, 4);
            PrimeTime = new Int32Array(memory, address + _off33, 4);
            FieldC0 = new Int32Array(memory, address + _off34, 4);
            Field160 = new Int32Array(memory, address + _off71, 4);
            Field180 = new Int32Array(memory, address + _off76, 4);
            Deaths = new Int32Array(memory, address + _off77, 4);
            Field1A0 = new Int32Array(memory, address + _off78, 4);
            TeamkillsMaybe = new Int32Array(memory, address + _off83, 4);
            SuicidesMaybe = new Int32Array(memory, address + _off84, 4);
            Field1E0 = new Int32Array(memory, address + _off85, 4);
            HeadshotsMaybe = new Int32Array(memory, address + _off86, 4);
            Field200 = new Int32Array(memory, address + _off87, 4);
            DmgDealt = new Int32Array(memory, address + _off88, 4);
            DmgMax = new Int32Array(memory, address + _off89, 4);
            BattlePoints = new Int32Array(memory, address + _off90, 4);
            Standings = new ByteArray(memory, address + _off95, 4);
            KillStreaks = new ByteArray(memory, address + _off100, 4);
            Field260 = new UInt16Array(memory, address + _off101, 4);
            Field268 = new UInt16Array(memory, address + _off102, 4);
            Field270 = new UInt16Array(memory, address + _off103, 4);
        }
    }

    public class KioskGameState : MemoryClass
    {
        private const int _off0 = 0x0;
        public GameMode GameMode { get => (GameMode)ReadByte(_off0); set => WriteByte(_off0, (byte)value); }

        private const int _off1 = 0x1;
        public byte RoomId { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x2;
        public byte AreaId { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x3;
        public byte PlayerCount { get => ReadByte(_off3); set => WriteByte(_off3, value); }

        private const int _off4 = 0x4;
        public byte MaxPlayers { get => ReadByte(_off4); set => WriteByte(_off4, value); }

        private const int _off5 = 0x5;
        public byte BotCount { get => ReadByte(_off5); set => WriteByte(_off5, value); }

        private const int _off6 = 0x6; // byte[4]
        public ByteArray Field6 { get; }

        private const int _off7 = 0xA;
        public byte DmgMultIdx { get => ReadByte(_off7); set => WriteByte(_off7, value); }

        private const int _off8 = 0xB;
        public byte FieldB { get => ReadByte(_off8); set => WriteByte(_off8, value); }

        private const int _off9 = 0xC;
        public ushort SomeFlags { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0xE;
        public ushort FieldE { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x10;
        public ushort Field10 { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x12;
        public ushort PointLimit { get => ReadUInt16(_off12); set => WriteUInt16(_off12, value); }

        private const int _off13 = 0x14;
        public int BattleTimeLimit { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x18;
        public int EscapeState { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x1C;
        public int TimeLimit { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x20; // int[4]
        public Int32Array Sensitivity { get; }

        private const int _off17 = 0x30; // byte[4]
        public ByteArray InvertSomething { get; }

        private const int _off18 = 0x34; // byte[4]
        public ByteArray Field34 { get; }

        private const int _off19 = 0x38; // byte[4]
        public ByteArray Hunters { get; }

        private const int _off20 = 0x3C; // byte[4]
        public ByteArray SuitColors { get; }

        private const int _off21 = 0x40; // byte[4]
        public ByteArray PlayerNames { get; }

        private const int _off22 = 0x44; // byte[4]
        public ByteArray BotEncounterState { get; }

        private const int _off23 = 0x48; // byte[4]
        public ByteArray TeamIds { get; }

        private const int _off24 = 0x4C; // byte[4]
        public ByteArray Field9C { get; }

        private const int _off25 = 0x50; // ushort[4]
        public UInt16Array BotSpawnerEntIds { get; }

        private const int _off26 = 0x58;
        public byte PrimeHunterStats { get => ReadByte(_off26); set => WriteByte(_off26, value); }

        private const int _off27 = 0x59;
        public byte PrimeHunter { get => ReadByte(_off27); set => WriteByte(_off27, value); }

        private const int _off28 = 0x5A;
        public byte RadShowPlayers { get => ReadByte(_off28); set => WriteByte(_off28, value); }

        private const int _off29 = 0x5B;
        public byte FieldAB { get => ReadByte(_off29); set => WriteByte(_off29, value); }

        private const int _off30 = 0x5C; // int[4]
        public Int32Array PrimeTime { get; }

        private const int _off31 = 0x6C; // int[4]
        public Int32Array FieldBC { get; }

        private const int _off32 = 0x7C;
        public int FieldCC { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0x80;
        public int FieldD0 { get => ReadInt32(_off33); set => WriteInt32(_off33, value); }

        private const int _off34 = 0x84;
        public int FieldD4 { get => ReadInt32(_off34); set => WriteInt32(_off34, value); }

        private const int _off35 = 0x88;
        public int FieldD8 { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0x8C;
        public int FieldDC { get => ReadInt32(_off36); set => WriteInt32(_off36, value); }

        private const int _off37 = 0x90;
        public int FieldE0 { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0x94;
        public int FieldE4 { get => ReadInt32(_off38); set => WriteInt32(_off38, value); }

        private const int _off39 = 0x98;
        public int FieldE9 { get => ReadInt32(_off39); set => WriteInt32(_off39, value); }

        private const int _off40 = 0x9C;
        public int FieldEC { get => ReadInt32(_off40); set => WriteInt32(_off40, value); }

        private const int _off41 = 0xA0;
        public int FieldF0 { get => ReadInt32(_off41); set => WriteInt32(_off41, value); }

        private const int _off42 = 0xA4;
        public int FieldF4 { get => ReadInt32(_off42); set => WriteInt32(_off42, value); }

        private const int _off43 = 0xA8;
        public int FieldF8 { get => ReadInt32(_off43); set => WriteInt32(_off43, value); }

        private const int _off44 = 0xAC;
        public int FieldFC { get => ReadInt32(_off44); set => WriteInt32(_off44, value); }

        private const int _off45 = 0xB0;
        public int Field100 { get => ReadInt32(_off45); set => WriteInt32(_off45, value); }

        private const int _off46 = 0xB4;
        public int Field104 { get => ReadInt32(_off46); set => WriteInt32(_off46, value); }

        private const int _off47 = 0xB8;
        public int Field108 { get => ReadInt32(_off47); set => WriteInt32(_off47, value); }

        private const int _off48 = 0xBC;
        public int Field10C { get => ReadInt32(_off48); set => WriteInt32(_off48, value); }

        private const int _off49 = 0xC0;
        public int Field110 { get => ReadInt32(_off49); set => WriteInt32(_off49, value); }

        private const int _off50 = 0xC4;
        public int Field114 { get => ReadInt32(_off50); set => WriteInt32(_off50, value); }

        private const int _off51 = 0xC8;
        public int Field118 { get => ReadInt32(_off51); set => WriteInt32(_off51, value); }

        private const int _off52 = 0xCC;
        public int Field11C { get => ReadInt32(_off52); set => WriteInt32(_off52, value); }

        private const int _off53 = 0xD0;
        public int Field120 { get => ReadInt32(_off53); set => WriteInt32(_off53, value); }

        private const int _off54 = 0xD4;
        public int Field124 { get => ReadInt32(_off54); set => WriteInt32(_off54, value); }

        private const int _off55 = 0xD8;
        public int Field128 { get => ReadInt32(_off55); set => WriteInt32(_off55, value); }

        private const int _off56 = 0xDC;
        public int Field12C { get => ReadInt32(_off56); set => WriteInt32(_off56, value); }

        private const int _off57 = 0xE0;
        public int Field130 { get => ReadInt32(_off57); set => WriteInt32(_off57, value); }

        private const int _off58 = 0xE4;
        public int Field134 { get => ReadInt32(_off58); set => WriteInt32(_off58, value); }

        private const int _off59 = 0xE8;
        public int Field138 { get => ReadInt32(_off59); set => WriteInt32(_off59, value); }

        private const int _off60 = 0xEC;
        public int Field13C { get => ReadInt32(_off60); set => WriteInt32(_off60, value); }

        private const int _off61 = 0xF0;
        public int Field140 { get => ReadInt32(_off61); set => WriteInt32(_off61, value); }

        private const int _off62 = 0xF4;
        public int Field144 { get => ReadInt32(_off62); set => WriteInt32(_off62, value); }

        private const int _off63 = 0xF8;
        public int Field148 { get => ReadInt32(_off63); set => WriteInt32(_off63, value); }

        private const int _off64 = 0xFC;
        public int Field14C { get => ReadInt32(_off64); set => WriteInt32(_off64, value); }

        private const int _off65 = 0x100;
        public int Field150 { get => ReadInt32(_off65); set => WriteInt32(_off65, value); }

        private const int _off66 = 0x104;
        public int Field154 { get => ReadInt32(_off66); set => WriteInt32(_off66, value); }

        private const int _off67 = 0x108;
        public int Field158 { get => ReadInt32(_off67); set => WriteInt32(_off67, value); }

        private const int _off68 = 0x10C;
        public int Field15C { get => ReadInt32(_off68); set => WriteInt32(_off68, value); }

        private const int _off69 = 0x110;
        public int Field160 { get => ReadInt32(_off69); set => WriteInt32(_off69, value); }

        private const int _off70 = 0x114;
        public int Field164 { get => ReadInt32(_off70); set => WriteInt32(_off70, value); }

        private const int _off71 = 0x118;
        public int Field168 { get => ReadInt32(_off71); set => WriteInt32(_off71, value); }

        private const int _off72 = 0x11C;
        public int Field16C { get => ReadInt32(_off72); set => WriteInt32(_off72, value); }

        private const int _off73 = 0x120;
        public int Field170 { get => ReadInt32(_off73); set => WriteInt32(_off73, value); }

        private const int _off74 = 0x124;
        public int Field174 { get => ReadInt32(_off74); set => WriteInt32(_off74, value); }

        private const int _off75 = 0x128;
        public int Field178 { get => ReadInt32(_off75); set => WriteInt32(_off75, value); }

        private const int _off76 = 0x12C; // int[4]
        public Int32Array Field17C { get; }

        private const int _off77 = 0x13C;
        public int Field18C { get => ReadInt32(_off77); set => WriteInt32(_off77, value); }

        private const int _off78 = 0x140;
        public int Field190 { get => ReadInt32(_off78); set => WriteInt32(_off78, value); }

        private const int _off79 = 0x144;
        public int Field194 { get => ReadInt32(_off79); set => WriteInt32(_off79, value); }

        private const int _off80 = 0x148;
        public int Field198 { get => ReadInt32(_off80); set => WriteInt32(_off80, value); }

        private const int _off81 = 0x14C; // int[4]
        public Int32Array Field19C { get; }

        private const int _off82 = 0x15C; // int[4]
        public Int32Array Deaths { get; }

        private const int _off83 = 0x16C; // int[4]
        public Int32Array Field1BC { get; }

        private const int _off84 = 0x17C; // int[4]
        public Int32Array Field1CC { get; }

        private const int _off85 = 0x18C; // int[4]
        public Int32Array SuicidesMaybe { get; }

        private const int _off86 = 0x19C;
        public int Field1EC { get => ReadInt32(_off86); set => WriteInt32(_off86, value); }

        private const int _off87 = 0x1A0;
        public int Field1F0 { get => ReadInt32(_off87); set => WriteInt32(_off87, value); }

        private const int _off88 = 0x1A4;
        public int Field1F4 { get => ReadInt32(_off88); set => WriteInt32(_off88, value); }

        private const int _off89 = 0x1A8;
        public int Field1F8 { get => ReadInt32(_off89); set => WriteInt32(_off89, value); }

        private const int _off90 = 0x1AC; // int[4]
        public Int32Array Field1FC { get; }

        private const int _off91 = 0x1BC; // int[4]
        public Int32Array HeadshotsMaybe { get; }

        private const int _off92 = 0x1CC; // int[4]
        public Int32Array Field21C { get; }

        private const int _off93 = 0x1DC; // int[4]
        public Int32Array DmgDealt { get; }

        private const int _off94 = 0x1EC; // int[4]
        public Int32Array DmgMax { get; }

        private const int _off95 = 0x1FC; // int[4]
        public Int32Array BattlePoints { get; }

        private const int _off96 = 0x20C;
        public int Field25C { get => ReadInt32(_off96); set => WriteInt32(_off96, value); }

        private const int _off97 = 0x210;
        public int Field260 { get => ReadInt32(_off97); set => WriteInt32(_off97, value); }

        private const int _off98 = 0x214;
        public int Field264 { get => ReadInt32(_off98); set => WriteInt32(_off98, value); }

        private const int _off99 = 0x218;
        public int Field268 { get => ReadInt32(_off99); set => WriteInt32(_off99, value); }

        private const int _off100 = 0x21C; // byte[4]
        public ByteArray Field26C { get; }

        private const int _off101 = 0x220;
        public int Field270 { get => ReadInt32(_off101); set => WriteInt32(_off101, value); }

        private const int _off102 = 0x224;
        public byte Field274 { get => ReadByte(_off102); set => WriteByte(_off102, value); }

        private const int _off103 = 0x225;
        public byte Field275 { get => ReadByte(_off103); set => WriteByte(_off103, value); }

        private const int _off104 = 0x226;
        public ushort Field276 { get => ReadUInt16(_off104); set => WriteUInt16(_off104, value); }

        private const int _off105 = 0x228; // byte[4]
        public ByteArray KillStreaks { get; }

        private const int _off106 = 0x22C; // ushort[4]
        public UInt16Array Field27C { get; }

        private const int _off107 = 0x234; // ushort[4]
        public UInt16Array Field284 { get; }

        private const int _off108 = 0x23C; // ushort[4]
        public UInt16Array Field28C { get; }

        private const int _off109 = 0x244;
        public int Field294 { get => ReadInt32(_off109); set => WriteInt32(_off109, value); }

        private const int _off110 = 0x248;
        public int Frames { get => ReadInt32(_off110); set => WriteInt32(_off110, value); }

        private const int _off111 = 0x24C;
        public int Field29C { get => ReadInt32(_off111); set => WriteInt32(_off111, value); }

        private const int _off112 = 0x250;
        public int Field2A0 { get => ReadInt32(_off112); set => WriteInt32(_off112, value); }

        private const int _off113 = 0x254;
        public int Field2A4 { get => ReadInt32(_off113); set => WriteInt32(_off113, value); }

        private const int _off114 = 0x258;
        public int Field2A8 { get => ReadInt32(_off114); set => WriteInt32(_off114, value); }

        private const int _off115 = 0x25C;
        public int Field2AC { get => ReadInt32(_off115); set => WriteInt32(_off115, value); }

        private const int _off116 = 0x260;
        public byte LayerId { get => ReadByte(_off116); set => WriteByte(_off116, value); }

        private const int _off117 = 0x261;
        public byte Field2B1 { get => ReadByte(_off117); set => WriteByte(_off117, value); }

        private const int _off118 = 0x262;
        public ushort Field2B2 { get => ReadUInt16(_off118); set => WriteUInt16(_off118, value); }

        public KioskGameState(Memory memory, int address) : base(memory, address)
        {
            Field6 = new ByteArray(memory, address + _off6, 4);
            Sensitivity = new Int32Array(memory, address + _off16, 4);
            InvertSomething = new ByteArray(memory, address + _off17, 4);
            Field34 = new ByteArray(memory, address + _off18, 4);
            Hunters = new ByteArray(memory, address + _off19, 4);
            SuitColors = new ByteArray(memory, address + _off20, 4);
            PlayerNames = new ByteArray(memory, address + _off21, 4);
            BotEncounterState = new ByteArray(memory, address + _off22, 4);
            TeamIds = new ByteArray(memory, address + _off23, 4);
            Field9C = new ByteArray(memory, address + _off24, 4);
            BotSpawnerEntIds = new UInt16Array(memory, address + _off25, 4);
            PrimeTime = new Int32Array(memory, address + _off30, 4);
            FieldBC = new Int32Array(memory, address + _off31, 4);
            Field17C = new Int32Array(memory, address + _off76, 4);
            Field19C = new Int32Array(memory, address + _off81, 4);
            Deaths = new Int32Array(memory, address + _off82, 4);
            Field1BC = new Int32Array(memory, address + _off83, 4);
            Field1CC = new Int32Array(memory, address + _off84, 4);
            SuicidesMaybe = new Int32Array(memory, address + _off85, 4);
            Field1FC = new Int32Array(memory, address + _off90, 4);
            HeadshotsMaybe = new Int32Array(memory, address + _off91, 4);
            Field21C = new Int32Array(memory, address + _off92, 4);
            DmgDealt = new Int32Array(memory, address + _off93, 4);
            DmgMax = new Int32Array(memory, address + _off94, 4);
            BattlePoints = new Int32Array(memory, address + _off95, 4);
            Field26C = new ByteArray(memory, address + _off100, 4);
            KillStreaks = new ByteArray(memory, address + _off105, 4);
            Field27C = new UInt16Array(memory, address + _off106, 4);
            Field284 = new UInt16Array(memory, address + _off107, 4);
            Field28C = new UInt16Array(memory, address + _off108, 4);
        }

        public KioskGameState(Memory memory, IntPtr address) : base(memory, address)
        {
            Field6 = new ByteArray(memory, address + _off6, 4);
            Sensitivity = new Int32Array(memory, address + _off16, 4);
            InvertSomething = new ByteArray(memory, address + _off17, 4);
            Field34 = new ByteArray(memory, address + _off18, 4);
            Hunters = new ByteArray(memory, address + _off19, 4);
            SuitColors = new ByteArray(memory, address + _off20, 4);
            PlayerNames = new ByteArray(memory, address + _off21, 4);
            BotEncounterState = new ByteArray(memory, address + _off22, 4);
            TeamIds = new ByteArray(memory, address + _off23, 4);
            Field9C = new ByteArray(memory, address + _off24, 4);
            BotSpawnerEntIds = new UInt16Array(memory, address + _off25, 4);
            PrimeTime = new Int32Array(memory, address + _off30, 4);
            FieldBC = new Int32Array(memory, address + _off31, 4);
            Field17C = new Int32Array(memory, address + _off76, 4);
            Field19C = new Int32Array(memory, address + _off81, 4);
            Deaths = new Int32Array(memory, address + _off82, 4);
            Field1BC = new Int32Array(memory, address + _off83, 4);
            Field1CC = new Int32Array(memory, address + _off84, 4);
            SuicidesMaybe = new Int32Array(memory, address + _off85, 4);
            Field1FC = new Int32Array(memory, address + _off90, 4);
            HeadshotsMaybe = new Int32Array(memory, address + _off91, 4);
            Field21C = new Int32Array(memory, address + _off92, 4);
            DmgDealt = new Int32Array(memory, address + _off93, 4);
            DmgMax = new Int32Array(memory, address + _off94, 4);
            BattlePoints = new Int32Array(memory, address + _off95, 4);
            Field26C = new ByteArray(memory, address + _off100, 4);
            KillStreaks = new ByteArray(memory, address + _off105, 4);
            Field27C = new UInt16Array(memory, address + _off106, 4);
            Field284 = new UInt16Array(memory, address + _off107, 4);
            Field28C = new UInt16Array(memory, address + _off108, 4);
        }
    }

    public class RoomState : MemoryClass
    {
        private const int _off0 = 0x0; // byte[60]
        public ByteArray Bits { get; }

        public RoomState(Memory memory, int address) : base(memory, address)
        {
            Bits = new ByteArray(memory, address + _off0, 60);
        }

        public RoomState(Memory memory, IntPtr address) : base(memory, address)
        {
            Bits = new ByteArray(memory, address + _off0, 60);
        }
    }

    public class StorySaveData : MemoryClass
    {
        private const int _off0 = 0x0;
        public ushort Weapons { get => ReadUInt16(_off0); set => WriteUInt16(_off0, value); }

        private const int _off1 = 0x2; // byte[3]
        public ByteArray WeaponSlots { get; }

        private const int _off2 = 0x5;
        public byte Padding5 { get => ReadByte(_off2); set => WriteByte(_off2, value); }

        private const int _off3 = 0x6; // ushort[2]
        public UInt16Array Ammo { get; }

        private const int _off4 = 0xA; // ushort[2]
        public UInt16Array AmmoCaps { get; }

        private const int _off5 = 0xE;
        public ushort Energy { get => ReadUInt16(_off5); set => WriteUInt16(_off5, value); }

        private const int _off6 = 0x10;
        public ushort EnergyCap { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x12;
        public ushort GameFlags { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        private const int _off8 = 0x14;
        public ushort Field14 { get => ReadUInt16(_off8); set => WriteUInt16(_off8, value); }

        private const int _off9 = 0x16;
        public ushort LastCheckpoint { get => ReadUInt16(_off9); set => WriteUInt16(_off9, value); }

        private const int _off10 = 0x18;
        public uint Bosses { get => ReadUInt32(_off10); set => WriteUInt32(_off10, value); }

        private const int _off11 = 0x1C;
        public uint Artifacts { get => ReadUInt32(_off11); set => WriteUInt32(_off11, value); }

        private const int _off12 = 0x20;
        public uint LostOctos { get => ReadUInt32(_off12); set => WriteUInt32(_off12, value); }

        private const int _off13 = 0x24;
        public byte CurOctos { get => ReadByte(_off13); set => WriteByte(_off13, value); }

        private const int _off14 = 0x25;
        public byte OwnOctos { get => ReadByte(_off14); set => WriteByte(_off14, value); }

        private const int _off15 = 0x26;
        public byte FoundOctos { get => ReadByte(_off15); set => WriteByte(_off15, value); }

        private const int _off16 = 0x27; // byte[9]
        public ByteArray VisitedRooms { get; }

        private const int _off17 = 0x30; // int[9]
        public Int32Array Field30 { get; }

        private const int _off18 = 0x54; // RoomState[66]
        public StructArray<RoomState> RoomState { get; }

        private const int _off19 = 0xFCC; // byte[8]
        public ByteArray FieldFCC { get; }

        private const int _off20 = 0xFD4;
        public int FieldFD4 { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0xFD8;
        public int FieldFD8 { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0xFDC;
        public int FieldFDC { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0xFE0;
        public int FieldFE0 { get => ReadInt32(_off23); set => WriteInt32(_off23, value); }

        private const int _off24 = 0xFE4;
        public int FieldFE4 { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        private const int _off25 = 0xFE8;
        public int FieldFE8 { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0xFEC;
        public int FieldFEC { get => ReadInt32(_off26); set => WriteInt32(_off26, value); }

        private const int _off27 = 0xFF0;
        public int FieldFF0 { get => ReadInt32(_off27); set => WriteInt32(_off27, value); }

        private const int _off28 = 0xFF4;
        public int FieldFF4 { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0xFF8;
        public int FieldFF8 { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0xFFC;
        public int FieldFFC { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0x1000;
        public int Field1000 { get => ReadInt32(_off31); set => WriteInt32(_off31, value); }

        private const int _off32 = 0x1004;
        public int Field1004 { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0x1008;
        public int Field1008 { get => ReadInt32(_off33); set => WriteInt32(_off33, value); }

        private const int _off34 = 0x100C;
        public int Field100C { get => ReadInt32(_off34); set => WriteInt32(_off34, value); }

        private const int _off35 = 0x1010;
        public int Field1010 { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0x1014; // byte[4]
        public ByteArray TriggerStateBits { get; }

        private const int _off37 = 0x1018;
        public int Field1018 { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0x101C; // byte[64]
        public ByteArray Logbook { get; }

        private const int _off39 = 0x105C;
        public int Field105C { get => ReadInt32(_off39); set => WriteInt32(_off39, value); }

        private const int _off40 = 0x1060;
        public int Field1060 { get => ReadInt32(_off40); set => WriteInt32(_off40, value); }

        private const int _off41 = 0x1064;
        public int Field1064 { get => ReadInt32(_off41); set => WriteInt32(_off41, value); }

        private const int _off42 = 0x1068;
        public int Field1068 { get => ReadInt32(_off42); set => WriteInt32(_off42, value); }

        private const int _off43 = 0x106C;
        public int Field106C { get => ReadInt32(_off43); set => WriteInt32(_off43, value); }

        private const int _off44 = 0x1070;
        public int Field1070 { get => ReadInt32(_off44); set => WriteInt32(_off44, value); }

        private const int _off45 = 0x1074;
        public int Field1074 { get => ReadInt32(_off45); set => WriteInt32(_off45, value); }

        private const int _off46 = 0x1078;
        public int Field1078 { get => ReadInt32(_off46); set => WriteInt32(_off46, value); }

        private const int _off47 = 0x107C;
        public int Field107C { get => ReadInt32(_off47); set => WriteInt32(_off47, value); }

        private const int _off48 = 0x1080; // byte[4]
        public ByteArray AreaHunters { get; }

        private const int _off49 = 0x1084;
        public byte Field1084 { get => ReadByte(_off49); set => WriteByte(_off49, value); }

        private const int _off50 = 0x1085;
        public byte RoomId { get => ReadByte(_off50); set => WriteByte(_off50, value); }

        private const int _off51 = 0x1086;
        public byte SlotHunterBits { get => ReadByte(_off51); set => WriteByte(_off51, value); }

        private const int _off52 = 0x1087;
        public byte DefeatedHunters { get => ReadByte(_off52); set => WriteByte(_off52, value); }

        private const int _off53 = 0x1088;
        public int HunterKills { get => ReadInt32(_off53); set => WriteInt32(_off53, value); }

        private const int _off54 = 0x108C;
        public int DeathsFromHunter { get => ReadInt32(_off54); set => WriteInt32(_off54, value); }

        private const int _off55 = 0x1090;
        public int DeathTotal { get => ReadInt32(_off55); set => WriteInt32(_off55, value); }

        private const int _off56 = 0x1094;
        public int Field1094 { get => ReadInt32(_off56); set => WriteInt32(_off56, value); }

        private const int _off57 = 0x1098;
        public int Field1098 { get => ReadInt32(_off57); set => WriteInt32(_off57, value); }

        private const int _off58 = 0x109C;
        public int Field109C { get => ReadInt32(_off58); set => WriteInt32(_off58, value); }

        private const int _off59 = 0x10A0;
        public int Field10A0 { get => ReadInt32(_off59); set => WriteInt32(_off59, value); }

        private const int _off60 = 0x10A4;
        public int ScanCount { get => ReadInt32(_off60); set => WriteInt32(_off60, value); }

        private const int _off61 = 0x10A8;
        public int EquipData { get => ReadInt32(_off61); set => WriteInt32(_off61, value); }

        private const int _off62 = 0x10AC;
        public uint MaxScanCount { get => ReadUInt32(_off62); set => WriteUInt32(_off62, value); }

        private const int _off63 = 0x10B0;
        public int MaxEquipData { get => ReadInt32(_off63); set => WriteInt32(_off63, value); }

        public StorySaveData(Memory memory, int address) : base(memory, address)
        {
            WeaponSlots = new ByteArray(memory, address + _off1, 3);
            Ammo = new UInt16Array(memory, address + _off3, 2);
            AmmoCaps = new UInt16Array(memory, address + _off4, 2);
            VisitedRooms = new ByteArray(memory, address + _off16, 9);
            Field30 = new Int32Array(memory, address + _off17, 9);
            RoomState = new StructArray<RoomState>(memory, address + _off18, 66,
                60, (Memory m, int a) => new RoomState(m, a));
            FieldFCC = new ByteArray(memory, address + _off19, 8);
            TriggerStateBits = new ByteArray(memory, address + _off36, 4);
            Logbook = new ByteArray(memory, address + _off38, 64);
            AreaHunters = new ByteArray(memory, address + _off48, 4);
        }

        public StorySaveData(Memory memory, IntPtr address) : base(memory, address)
        {
            WeaponSlots = new ByteArray(memory, address + _off1, 3);
            Ammo = new UInt16Array(memory, address + _off3, 2);
            AmmoCaps = new UInt16Array(memory, address + _off4, 2);
            VisitedRooms = new ByteArray(memory, address + _off16, 9);
            Field30 = new Int32Array(memory, address + _off17, 9);
            RoomState = new StructArray<RoomState>(memory, address + _off18, 66,
                60, (Memory m, int a) => new RoomState(m, a));
            FieldFCC = new ByteArray(memory, address + _off19, 8);
            TriggerStateBits = new ByteArray(memory, address + _off36, 4);
            Logbook = new ByteArray(memory, address + _off38, 64);
            AreaHunters = new ByteArray(memory, address + _off48, 4);
        }
    }

    public class SaveType3 : MemoryClass
    {
        private const int _off0 = 0x0;
        public int Field0 { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public int Field4 { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x8;
        public int Field8 { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        private const int _off3 = 0xC;
        public int FieldC { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x10;
        public int Field10 { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x14;
        public int Field14 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x18;
        public int Field18 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x1C;
        public int Field1C { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x20;
        public int Field20 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x24;
        public int Field24 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x28;
        public int Field28 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x2C;
        public int Field2C { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x30;
        public int Field30 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x34;
        public int Field34 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x38;
        public int Field38 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x3C;
        public int Field3C { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        public SaveType3(Memory memory, int address) : base(memory, address)
        {
        }

        public SaveType3(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class StatsAndSettings : MemoryClass
    {
        private const int _off0 = 0x0;
        public int Field0 { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public int AreaBits { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x8;
        public int MultiplayerCharacters { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        private const int _off3 = 0xC;
        public int FieldC { get => ReadInt32(_off3); set => WriteInt32(_off3, value); }

        private const int _off4 = 0x10;
        public int Field10 { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x14;
        public int TouchpadSensitivity { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x18;
        public int Field18 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x1C;
        public int Field1C { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x20;
        public int Field20 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x24;
        public int Field24 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x28;
        public int Field28 { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x2C;
        public int Field2C { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x30;
        public int Field30 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x34;
        public int Field34 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x38;
        public int Field38 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x3C;
        public int Field3C { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x40;
        public int Field40 { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x44;
        public int Field44 { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x48;
        public int Field48 { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x4C;
        public int Field4C { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        private const int _off20 = 0x50;
        public int Field50 { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0x54;
        public int Field54 { get => ReadInt32(_off21); set => WriteInt32(_off21, value); }

        private const int _off22 = 0x58;
        public int Field58 { get => ReadInt32(_off22); set => WriteInt32(_off22, value); }

        private const int _off23 = 0x5C;
        public int Field5C { get => ReadInt32(_off23); set => WriteInt32(_off23, value); }

        private const int _off24 = 0x60;
        public int Field60 { get => ReadInt32(_off24); set => WriteInt32(_off24, value); }

        private const int _off25 = 0x64;
        public int Field64 { get => ReadInt32(_off25); set => WriteInt32(_off25, value); }

        private const int _off26 = 0x68;
        public int Field68 { get => ReadInt32(_off26); set => WriteInt32(_off26, value); }

        private const int _off27 = 0x6C;
        public int Field6C { get => ReadInt32(_off27); set => WriteInt32(_off27, value); }

        private const int _off28 = 0x70;
        public int Field70 { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0x74;
        public int Field74 { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0x78;
        public int Field78 { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0x7C;
        public int Field7C { get => ReadInt32(_off31); set => WriteInt32(_off31, value); }

        private const int _off32 = 0x80;
        public int Field80 { get => ReadInt32(_off32); set => WriteInt32(_off32, value); }

        private const int _off33 = 0x84;
        public int Field84 { get => ReadInt32(_off33); set => WriteInt32(_off33, value); }

        private const int _off34 = 0x88;
        public int Field88 { get => ReadInt32(_off34); set => WriteInt32(_off34, value); }

        private const int _off35 = 0x8C;
        public int Field8C { get => ReadInt32(_off35); set => WriteInt32(_off35, value); }

        private const int _off36 = 0x90;
        public int Field90 { get => ReadInt32(_off36); set => WriteInt32(_off36, value); }

        private const int _off37 = 0x94;
        public int Field94 { get => ReadInt32(_off37); set => WriteInt32(_off37, value); }

        private const int _off38 = 0x98;
        public int Field98 { get => ReadInt32(_off38); set => WriteInt32(_off38, value); }

        private const int _off39 = 0x9C;
        public int Field9C { get => ReadInt32(_off39); set => WriteInt32(_off39, value); }

        private const int _off40 = 0xA0;
        public int EnemyKillsMaybe { get => ReadInt32(_off40); set => WriteInt32(_off40, value); }

        public StatsAndSettings(Memory memory, int address) : base(memory, address)
        {
        }

        public StatsAndSettings(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class LicenseInfo : MemoryClass
    {
        private const int _off0 = 0x0; // byte[24]
        public ByteArray Nickname { get; }

        private const int _off1 = 0x18;
        public int Field18 { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x1C;
        public ushort RankPoints { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x1E;
        public ushort Field1E { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x20;
        public int Field20 { get => ReadInt32(_off4); set => WriteInt32(_off4, value); }

        private const int _off5 = 0x24;
        public int Field24 { get => ReadInt32(_off5); set => WriteInt32(_off5, value); }

        private const int _off6 = 0x28;
        public int Field28 { get => ReadInt32(_off6); set => WriteInt32(_off6, value); }

        private const int _off7 = 0x2C;
        public int Field2C { get => ReadInt32(_off7); set => WriteInt32(_off7, value); }

        private const int _off8 = 0x30;
        public int Field30 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x34;
        public int Field34 { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x38;
        public int HeadshotCount { get => ReadInt32(_off10); set => WriteInt32(_off10, value); }

        private const int _off11 = 0x3C;
        public int Field3C { get => ReadInt32(_off11); set => WriteInt32(_off11, value); }

        private const int _off12 = 0x40;
        public int GamplayTime1 { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x44;
        public int GamplayTime2 { get => ReadInt32(_off13); set => WriteInt32(_off13, value); }

        private const int _off14 = 0x48;
        public int GamplayTime3 { get => ReadInt32(_off14); set => WriteInt32(_off14, value); }

        private const int _off15 = 0x4C;
        public int Field4C { get => ReadInt32(_off15); set => WriteInt32(_off15, value); }

        private const int _off16 = 0x50;
        public int Field50 { get => ReadInt32(_off16); set => WriteInt32(_off16, value); }

        private const int _off17 = 0x54;
        public int Field54 { get => ReadInt32(_off17); set => WriteInt32(_off17, value); }

        private const int _off18 = 0x58;
        public int Field58 { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x5C;
        public int Field5C { get => ReadInt32(_off19); set => WriteInt32(_off19, value); }

        private const int _off20 = 0x60;
        public int Field60 { get => ReadInt32(_off20); set => WriteInt32(_off20, value); }

        private const int _off21 = 0x64; // int[4]
        public Int32Array Field64 { get; }

        private const int _off22 = 0x74; // int[7]
        public Int32Array Field74 { get; }

        private const int _off23 = 0x90; // int[7]
        public Int32Array Field90 { get; }

        private const int _off24 = 0xAC; // int[9]
        public Int32Array FieldAC { get; }

        private const int _off25 = 0xD0; // int[29]
        public Int32Array FieldD0 { get; }

        private const int _off26 = 0x144; // int[29]
        public Int32Array Field144 { get; }

        private const int _off27 = 0x1B8; // int[7]
        public Int32Array Field1B8 { get; }

        private const int _off28 = 0x1D4;
        public int Field1D4 { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        private const int _off29 = 0x1D8;
        public int Field1D8 { get => ReadInt32(_off29); set => WriteInt32(_off29, value); }

        private const int _off30 = 0x1DC;
        public int Field1DC { get => ReadInt32(_off30); set => WriteInt32(_off30, value); }

        private const int _off31 = 0x1E0;
        public int Field1E0 { get => ReadInt32(_off31); set => WriteInt32(_off31, value); }

        private const int _off32 = 0x1E4; // byte[4]
        public ByteArray Field1E4 { get; }

        public LicenseInfo(Memory memory, int address) : base(memory, address)
        {
            Nickname = new ByteArray(memory, address + _off0, 24);
            Field64 = new Int32Array(memory, address + _off21, 4);
            Field74 = new Int32Array(memory, address + _off22, 7);
            Field90 = new Int32Array(memory, address + _off23, 7);
            FieldAC = new Int32Array(memory, address + _off24, 9);
            FieldD0 = new Int32Array(memory, address + _off25, 29);
            Field144 = new Int32Array(memory, address + _off26, 29);
            Field1B8 = new Int32Array(memory, address + _off27, 7);
            Field1E4 = new ByteArray(memory, address + _off32, 4);
        }

        public LicenseInfo(Memory memory, IntPtr address) : base(memory, address)
        {
            Nickname = new ByteArray(memory, address + _off0, 24);
            Field64 = new Int32Array(memory, address + _off21, 4);
            Field74 = new Int32Array(memory, address + _off22, 7);
            Field90 = new Int32Array(memory, address + _off23, 7);
            FieldAC = new Int32Array(memory, address + _off24, 9);
            FieldD0 = new Int32Array(memory, address + _off25, 29);
            Field144 = new Int32Array(memory, address + _off26, 29);
            Field1B8 = new Int32Array(memory, address + _off27, 7);
            Field1E4 = new ByteArray(memory, address + _off32, 4);
        }
    }

    public class FriendsRivals : MemoryClass
    {
        private const int _off0 = 0x0; // int[834]
        public Int32Array Fields { get; }

        public FriendsRivals(Memory memory, int address) : base(memory, address)
        {
            Fields = new Int32Array(memory, address + _off0, 834);
        }

        public FriendsRivals(Memory memory, IntPtr address) : base(memory, address)
        {
            Fields = new Int32Array(memory, address + _off0, 834);
        }
    }

    public class RoomDescription : MemoryClass
    {
        private const int _off0 = 0x0; // char*
        public IntPtr Name { get => ReadPointer(_off0); set => WritePointer(_off0, value); }

        private const int _off1 = 0x4; // char*
        public IntPtr Model { get => ReadPointer(_off1); set => WritePointer(_off1, value); }

        private const int _off2 = 0x8; // char*
        public IntPtr Anim { get => ReadPointer(_off2); set => WritePointer(_off2, value); }

        private const int _off3 = 0xC; // char*
        public IntPtr Tex { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x10; // char*
        public IntPtr Collision { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x14; // char*
        public IntPtr Ent { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x18; // char*
        public IntPtr Nodedata { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x1C; // char*
        public IntPtr RoomNodeName { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        private const int _off8 = 0x20;
        public int BattleTimeLimit { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        private const int _off9 = 0x24;
        public int TimeLimit { get => ReadInt32(_off9); set => WriteInt32(_off9, value); }

        private const int _off10 = 0x28;
        public ushort PointLimit { get => ReadUInt16(_off10); set => WriteUInt16(_off10, value); }

        private const int _off11 = 0x2A;
        public ushort LayerId { get => ReadUInt16(_off11); set => WriteUInt16(_off11, value); }

        private const int _off12 = 0x2C;
        public int FarClipDist { get => ReadInt32(_off12); set => WriteInt32(_off12, value); }

        private const int _off13 = 0x30;
        public ushort FogEnable { get => ReadUInt16(_off13); set => WriteUInt16(_off13, value); }

        private const int _off14 = 0x32;
        public ushort ClearFog { get => ReadUInt16(_off14); set => WriteUInt16(_off14, value); }

        private const int _off15 = 0x34;
        public ushort FogColor { get => ReadUInt16(_off15); set => WriteUInt16(_off15, value); }

        private const int _off16 = 0x36;
        public ushort Padding36 { get => ReadUInt16(_off16); set => WriteUInt16(_off16, value); }

        private const int _off17 = 0x38;
        public uint FogSlope { get => ReadUInt32(_off17); set => WriteUInt32(_off17, value); }

        private const int _off18 = 0x3C;
        public int FogOffset { get => ReadInt32(_off18); set => WriteInt32(_off18, value); }

        private const int _off19 = 0x40;
        public ColorRgb Light1Color { get => ReadColor3(_off19); set => WriteColor3(_off19, value); }

        private const int _off20 = 0x43;
        public byte Padding43 { get => ReadByte(_off20); set => WriteByte(_off20, value); }

        private const int _off21 = 0x44;
        public Vector3 Light1Vec { get => ReadVec3(_off21); set => WriteVec3(_off21, value); }

        private const int _off22 = 0x50;
        public ColorRgb Light2Color { get => ReadColor3(_off22); set => WriteColor3(_off22, value); }

        private const int _off23 = 0x53;
        public byte Padding53 { get => ReadByte(_off23); set => WriteByte(_off23, value); }

        private const int _off24 = 0x54;
        public Vector3 Light2Vec { get => ReadVec3(_off24); set => WriteVec3(_off24, value); }

        private const int _off25 = 0x60; // char*
        public IntPtr InternalName { get => ReadPointer(_off25); set => WritePointer(_off25, value); }

        private const int _off26 = 0x64; // char*
        public IntPtr Archive { get => ReadPointer(_off26); set => WritePointer(_off26, value); }

        private const int _off27 = 0x68;
        public int KillHeight { get => ReadInt32(_off27); set => WriteInt32(_off27, value); }

        private const int _off28 = 0x6C;
        public int Size { get => ReadInt32(_off28); set => WriteInt32(_off28, value); }

        public RoomDescription(Memory memory, int address) : base(memory, address)
        {
        }

        public RoomDescription(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class EquipInfo : MemoryClass
    {
        private const int _off0 = 0x0;
        public byte Flags { get => ReadByte(_off0); set => WriteByte(_off0, value); }

        private const int _off1 = 0x1;
        public byte Count { get => ReadByte(_off1); set => WriteByte(_off1, value); }

        private const int _off2 = 0x2;
        public ushort Padding2 { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x4; // CBeamProjectile*
        public IntPtr Beams { get => ReadPointer(_off3); set => WritePointer(_off3, value); }

        private const int _off4 = 0x8; // WeaponInfo*
        public IntPtr WeaponInfo { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0xC; // unsigned__int16*
        public IntPtr AmmoPtr { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x10;
        public ushort ChargeLevel { get => ReadUInt16(_off6); set => WriteUInt16(_off6, value); }

        private const int _off7 = 0x12;
        public ushort SmokeLevel { get => ReadUInt16(_off7); set => WriteUInt16(_off7, value); }

        public EquipInfo(Memory memory, int address) : base(memory, address)
        {
        }

        public EquipInfo(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class VecFx32 : MemoryClass
    {
        private const int _off0 = 0x0;
        public int X { get => ReadInt32(_off0); set => WriteInt32(_off0, value); }

        private const int _off1 = 0x4;
        public int Y { get => ReadInt32(_off1); set => WriteInt32(_off1, value); }

        private const int _off2 = 0x8;
        public int Z { get => ReadInt32(_off2); set => WriteInt32(_off2, value); }

        public VecFx32(Memory memory, int address) : base(memory, address)
        {
        }

        public VecFx32(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }

    public class MtxFx43 : MemoryClass
    {
        private const int _off0 = 0x0;
        public Int32Array M { get; init; }
        public VecFx32 Row0 { get; init; }

        private const int _off1 = 0xC;
        public VecFx32 Row1 { get; init; }

        private const int _off2 = 0x18;
        public VecFx32 Row2 { get; init; }

        private const int _off3 = 0x24;
        public VecFx32 Row3 { get; init; }

        public MtxFx43(Memory memory, int address) : base(memory, address)
        {
            M = new Int32Array(memory, address + _off0, 12);
            Row0 = new VecFx32(memory, address + _off0);
            Row1 = new VecFx32(memory, address + _off1);
            Row2 = new VecFx32(memory, address + _off2);
            Row3 = new VecFx32(memory, address + _off3);
        }

        public MtxFx43(Memory memory, IntPtr address) : base(memory, address)
        {
            M = new Int32Array(memory, address + _off0, 12);
            Row0 = new VecFx32(memory, address + _off0);
            Row1 = new VecFx32(memory, address + _off1);
            Row2 = new VecFx32(memory, address + _off2);
            Row3 = new VecFx32(memory, address + _off3);
        }
    }
}
