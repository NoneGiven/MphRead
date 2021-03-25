using System;
using System.Collections;
using System.Collections.Generic;

namespace MphRead.Memory
{
    public abstract class MemoryArray<T> : MemoryClass, IEnumerable, IEnumerator
    {
        public int Length { get; }

        protected MemoryArray(Memory memory, int address, int length) : base(memory, address)
        {
            Length = length;
        }

        protected MemoryArray(Memory memory, IntPtr address, int length) : base(memory, address)
        {
            Length = length;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                {
                    throw new IndexOutOfRangeException();
                }
                return Get(index);
            }
            set
            {
                if (index < 0 || index >= Length)
                {
                    throw new IndexOutOfRangeException();
                }
                Set(index, value);
            }
        }

        protected abstract T Get(int index);
        protected abstract void Set(int index, T value);

        private int _currentIndex = -1;

        public IEnumerator GetEnumerator()
        {
            return this;
        }

        object? IEnumerator.Current => Get(_currentIndex);

        bool IEnumerator.MoveNext()
        {
            _currentIndex++;
            return _currentIndex < Length;
        }

        void IEnumerator.Reset()
        {
            _currentIndex = 0;
        }
    }

    public class SByteArray : MemoryArray<sbyte>
    {
        public SByteArray(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public SByteArray(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override sbyte Get(int index)
        {
            return ReadSByte(index * sizeof(sbyte));
        }

        protected override void Set(int index, sbyte value)
        {
            WriteSByte(index * sizeof(sbyte), value);
        }
    }

    public class ByteArray : MemoryArray<byte>
    {
        public ByteArray(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public ByteArray(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override byte Get(int index)
        {
            return ReadByte(index * sizeof(byte));
        }

        protected override void Set(int index, byte value)
        {
            WriteByte(index * sizeof(byte), value);
        }
    }

    public class Int16Array : MemoryArray<short>
    {
        public Int16Array(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public Int16Array(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override short Get(int index)
        {
            return ReadInt16(index * sizeof(short));
        }

        protected override void Set(int index, short value)
        {
            WriteInt16(index * sizeof(short), value);
        }
    }

    public class UInt16Array : MemoryArray<ushort>
    {
        public UInt16Array(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public UInt16Array(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override ushort Get(int index)
        {
            return ReadUInt16(index * sizeof(ushort));
        }

        protected override void Set(int index, ushort value)
        {
            WriteUInt16(index * sizeof(ushort), value);
        }
    }

    public class Int32Array : MemoryArray<int>
    {
        public Int32Array(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public Int32Array(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override int Get(int index)
        {
            return ReadInt32(index * sizeof(int));
        }

        protected override void Set(int index, int value)
        {
            WriteInt32(index * sizeof(int), value);
        }
    }

    public class UInt32Array : MemoryArray<uint>
    {
        public UInt32Array(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public UInt32Array(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override uint Get(int index)
        {
            return ReadUInt32(index * sizeof(uint));
        }

        protected override void Set(int index, uint value)
        {
            WriteUInt32(index * sizeof(uint), value);
        }
    }

    public class IntPtrArray : MemoryArray<IntPtr>
    {
        public IntPtrArray(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public IntPtrArray(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override IntPtr Get(int index)
        {
            return ReadPointer(index * sizeof(int));
        }

        protected override void Set(int index, IntPtr value)
        {
            WritePointer(index * sizeof(int), value);
        }
    }

    public class U8EnumArray<T> : MemoryArray<T> where T : Enum
    {
        public U8EnumArray(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public U8EnumArray(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override T Get(int index)
        {
            return (T)(object)ReadByte(index * sizeof(byte));
        }

        protected override void Set(int index, T value)
        {
            WriteByte(index * sizeof(byte), (byte)(object)value);
        }
    }

    public class U16EnumArray<T> : MemoryArray<T> where T : Enum
    {
        public U16EnumArray(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public U16EnumArray(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override T Get(int index)
        {
            return (T)(object)ReadUInt16(index * sizeof(ushort));
        }

        protected override void Set(int index, T value)
        {
            WriteUInt16(index * sizeof(ushort), (ushort)(object)value);
        }
    }

    public class U32EnumArray<T> : MemoryArray<T> where T : Enum
    {
        public U32EnumArray(Memory memory, int address, int length) : base(memory, address, length)
        {
        }

        public U32EnumArray(Memory memory, IntPtr address, int length) : base(memory, address, length)
        {
        }

        protected override T Get(int index)
        {
            return (T)(object)ReadUInt32(index * sizeof(uint));
        }

        protected override void Set(int index, T value)
        {
            WriteUInt32(index * sizeof(uint), (uint)(object)value);
        }
    }

    public class StructArray<T> : MemoryArray<T> where T : class
    {
        private readonly IReadOnlyList<T> _items;

        public StructArray(Memory memory, int address, int length, int size, Func<Memory, int, T> create) : base(memory, address, length)
        {
            var items = new List<T>();
            for (int i = 0; i < length; i++)
            {
                items.Add(create.Invoke(memory, address + i * size));
            }
            _items = items;
        }

        public StructArray(Memory memory, IntPtr address, int length, int size, Func<Memory, int, T> create) : base(memory, address, length)
        {
            var items = new List<T>();
            for (int i = 0; i < length; i++)
            {
                items.Add(create.Invoke(memory, address.ToInt32() + i * size));
            }
            _items = items;
        }

        protected override T Get(int index)
        {
            return _items[index];
        }

        protected override void Set(int index, T value)
        {
            // todo: easiest would be to just copy each property, but that would cause multiple writes;
            // to avoid that we would need to keep a size parameter with each class, allocate a buffer of that size,
            // fill it with the bytes from each property, and then write that memory all at once
            throw new NotImplementedException("Writing embedded struct properties is not supported.");
        }
    }
}
