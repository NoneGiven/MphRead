using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MphRead.Memory
{
    public class Memory
    {
        private static class Addresses
        {
            public static readonly int EntityListHead = 0x20E3EE0;
        }

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

        private readonly Process _process;
        private readonly byte[] _buffer;
        private IntPtr _baseAddress;

        public byte[] Buffer => _buffer;

        private const int _size = 0x400000;
        public const int Offset = 0x2000000;

        private Memory(Process process)
        {
            _process = process;
            _buffer = new byte[_size];
        }

        public static void Start()
        {
            new Memory(Process.GetProcessById(34248)).Run();
            /*var procs = Process.GetProcessesByName("NO$GBA").ToList();
            foreach (Process process in procs)
            {
                if (!process.MainWindowTitle.Contains("Debugger"))
                {
                    new Memory(process).Run();
                    return;
                }
            }
            throw new ProgramException("Could not find process.");*/
        }

        private readonly List<CEntity> _entities = new List<CEntity>();

        private void Run()
        {
            _baseAddress = new IntPtr(0x91A8100);
            Task.Run(async () =>
            {
                while (true)
                {
                    RefreshMemory();
                    GetEntities();
                    await Task.Delay(15);
                }
            }).GetAwaiter().GetResult();
        }

        private void RefreshMemory()
        {
            bool result = ReadProcessMemory(_process.Handle, _baseAddress, _buffer, _size, out IntPtr count);
            Debug.Assert(result);
            Debug.Assert(count.ToInt32() == _size);
        }

        private void GetEntities()
        {
            _entities.Clear();
            CEntity head = GetEntity(Addresses.EntityListHead);
            Debug.Assert(head.Type == EntityType.ListHead);
            _entities.Add(head);
            CEntity next = GetEntity(head.Next);
            while (next != head)
            {
                _entities.Add(next);
                next = GetEntity(next.Next);
            }
        }

        public void WriteMemory(int address, byte[] value, int size)
        {
            int offset = address - Offset;
            var pointer = new IntPtr(_baseAddress.ToInt32() + offset);
            bool result = WriteProcessMemory(_process.Handle, pointer, value, size, out IntPtr count);
            Debug.Assert(result);
            Debug.Assert(count.ToInt32() == size);
            for (int i = 0; i < size; i++)
            {
                _buffer[offset + i] = value[i];
            }
        }

        private CEntity GetEntity(IntPtr address)
        {
            return GetEntity(address.ToInt32());
        }

        private CEntity GetEntity(int address)
        {
            int offset = address - Offset;
            var type = (EntityType)BitConverter.ToUInt16(_buffer, offset);
            if (type == EntityType.ForceField)
            {
                return new CForceField(this, address);
            }
            return new CEntity(this, address);
        }
    }

    public abstract class MemoryClass
    {
        private readonly Memory _memory;
        private readonly int _offset;

        public int Address { get; }

        protected MemoryClass(Memory memory, IntPtr address)
        {
            _memory = memory;
            Address = address.ToInt32();
            _offset = Address - Memory.Offset;
        }

        protected MemoryClass(Memory memory, int address)
        {
            _memory = memory;
            Address = address;
            _offset = Address - Memory.Offset;
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
            _memory.WriteMemory(Address + offset, BitConverter.GetBytes(value), sizeof(sbyte));
        }

        protected void WriteByte(int offset, byte value)
        {
            _memory.WriteMemory(Address + offset, BitConverter.GetBytes(value), sizeof(byte));
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
            byte[] vector = new byte[12];
            byte[] component = BitConverter.GetBytes((int)(value.X * 4096));
            for (int i = 0; i < 4; i++)
            {
                vector[i] = component[i];
            }
            component = BitConverter.GetBytes((int)(value.Y * 4096));
            for (int i = 0; i < 4; i++)
            {
                vector[i + 4] = component[i];
            }
            component = BitConverter.GetBytes((int)(value.Z * 4096));
            for (int i = 0; i < 4; i++)
            {
                vector[i + 8] = component[i];
            }
            _memory.WriteMemory(Address + offset, component, 12);
        }
    }

    public class CEntity : MemoryClass
    {
        private const int _off0 = 0x0;
        public EntityType Type { get => (EntityType)ReadUInt16(_off0); set => WriteUInt16(_off0, (ushort)value); }

        private const int _off1 = 0x2;
        public ushort EntityId { get => ReadUInt16(_off1); set => WriteUInt16(_off1, value); }

        private const int _off2 = 0x4;
        public ushort ScanId { get => ReadUInt16(_off2); set => WriteUInt16(_off2, value); }

        private const int _off3 = 0x6;
        public ushort Field6 { get => ReadUInt16(_off3); set => WriteUInt16(_off3, value); }

        private const int _off4 = 0x8;
        public IntPtr MtxPtr { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0xC;
        public IntPtr Funcs { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x10;
        public IntPtr Prev { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x14;
        public IntPtr Next { get => ReadPointer(_off7); set => WritePointer(_off7, value); }

        /*
            MtxFx43 *mtxptr;
            EntityClass *funcs;
            CEntity *prev;
            CEntity *next;
        */

        public CEntity(Memory memory, int address) : base(memory, address)
        {
        }

        public CEntity(Memory memory, IntPtr address) : base(memory, address)
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

        private const int _off4 = 0x28;
        public IntPtr Data { get => ReadPointer(_off4); set => WritePointer(_off4, value); }

        private const int _off5 = 0x2C;
        public IntPtr NodeRef { get => ReadPointer(_off5); set => WritePointer(_off5, value); }

        private const int _off6 = 0x30;
        public IntPtr Lock { get => ReadPointer(_off6); set => WritePointer(_off6, value); }

        private const int _off7 = 0x34;
        public Vector3 Vector2 { get => ReadVec3(_off7); set => WriteVec3(_off7, value); }

        private const int _off8 = 0x44;
        public int Field40 { get => ReadInt32(_off8); set => WriteInt32(_off8, value); }

        /*
            EntityData *data;
            NodeRef *node_ref;
            CEnemy *lock;
            CModel model;
        */

        public CForceField(Memory memory, int address) : base(memory, address)
        {
        }

        public CForceField(Memory memory, IntPtr address) : base(memory, address)
        {
        }
    }
}
