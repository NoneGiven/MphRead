using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MphRead.Memory
{
    public class Memory
    {
        private static class Addresses
        {
            public static readonly int EntityListHead = 0x20E3EE0;
            public static readonly int FrameCount = 0x20D94FC;
            public static readonly int PlayerUA = 0x20DB180;
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
        private readonly Dictionary<IntPtr, CEntity> _temp = new Dictionary<IntPtr, CEntity>();

        private void Run()
        {
            _baseAddress = new IntPtr(0x91A8100);
            Task.Run(async () =>
            {
                var results = new List<(int, int)>();
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
            _temp.Clear();
            foreach (CEntity entity in _entities)
            {
                _temp.Add(entity.Address, entity);
            }
            _entities.Clear();
            CEntity head = GetEntity(Addresses.EntityListHead);
            Debug.Assert(head.EntityType == EntityType.ListHead);
            _entities.Add(head);
            IntPtr nextAddr = head.Next;
            while (nextAddr != head.Address)
            {
                CEntity entity;
                if (_temp.TryGetValue(nextAddr, out entity!)
                    && entity.EntityType == (EntityType)BitConverter.ToUInt16(_buffer, nextAddr.ToInt32() - Offset))
                {
                    _entities.Add(entity);
                }
                else
                {
                    entity = GetEntity(nextAddr);
                    _entities.Add(entity);
                }
                nextAddr = entity.Next;
            }
        }

        public void WriteMemory(IntPtr address, byte[] value, int size)
        {
            WriteMemory(address.ToInt32(), value, size);
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
            if (type == EntityType.Platform)
            {
                return new CPlatform(this, address);
            }
            if (type == EntityType.Object)
            {
                return new CObject(this, address);
            }
            if (type == EntityType.PlayerSpawn)
            {
                return new CPlayerSpawn(this, address);
            }
            if (type == EntityType.Door)
            {
                return new CDoor(this, address);
            }
            if (type == EntityType.ItemSpawn)
            {
                return new CItemSpawn(this, address);
            }
            if (type == EntityType.ItemInstance)
            {
                return new CItemInstance(this, address);
            }
            if (type == EntityType.EnemySpawn)
            {
                return new CEnemySpawn(this, address);
            }
            if (type == EntityType.TriggerVolume)
            {
                return new CTriggerVolume(this, address);
            }
            if (type == EntityType.AreaVolume)
            {
                return new CAreaVolume(this, address);
            }
            if (type == EntityType.JumpPad)
            {
                return new CJumpPad(this, address);
            }
            if (type == EntityType.PointModule)
            {
                return new CPointModule(this, address);
            }
            if (type == EntityType.MorphCamera)
            {
                return new CMorphCamera(this, address);
            }
            if (type == EntityType.OctolithFlag)
            {
                return new COctolithFlag(this, address);
            }
            if (type == EntityType.FlagBase)
            {
                return new CFlagBase(this, address);
            }
            if (type == EntityType.Teleporter)
            {
                return new CTeleporter(this, address);
            }
            if (type == EntityType.NodeDefense)
            {
                return new CNodeDefense(this, address);
            }
            if (type == EntityType.LightSource)
            {
                return new CLightSource(this, address);
            }
            if (type == EntityType.Artifact)
            {
                return new CArtifact(this, address);
            }
            if (type == EntityType.CameraSequence)
            {
                return new CCameraSequence(this, address);
            }
            if (type == EntityType.ForceField)
            {
                return new CForceField(this, address);
            }
            if (type == EntityType.BeamEffect)
            {
                return new CBeamEffect(this, address);
            }
            if (type == EntityType.Bomb)
            {
                return new CBomb(this, address);
            }
            if (type == EntityType.EnemyInstance)
            {
                // nxtodo: enemy instance
            }
            if (type == EntityType.Halfturret)
            {
                return new CHalfturret(this, address);
            }
            if (type == EntityType.Player)
            {
                return new CPlayer(this, address);
            }
            if (type == EntityType.BeamProjectile)
            {
                return new CBeamProjectile(this, address);
            }
            return new CEntity(this, address);
        }

        public static void ParseStruct(string className, bool entity, string data)
        {
            if (String.IsNullOrWhiteSpace(data))
            {
                return;
            }
            int index = 0;
            int offset = entity ? 0x18 : 0;
            var byteEnums = new Dictionary<string, string>()
            {
                { "ENEMY_TYPE", "EnemyType" },
                { "HUNTER", "Hunter" }
            };
            var ushortEnums = new Dictionary<string, string>()
            {
                { "ITEM_TYPE", "ItemType" }
            };
            var uintEnums = new Dictionary<string, string>()
            {
                { "EVENT_TYPE", "Message" },
                { "DOOR_TYPE", "DoorType" },
                { "COLLISION_VOLUME_TYPE", "VolumeType" }
            };
            if (entity)
            {
                Console.WriteLine($"public class {className} : CEntity");
            }
            else
            {
                Console.WriteLine($"public class {className} : MemoryClass");
            }
            Console.WriteLine("    {");
            var news = new List<string>();
            foreach (string line in data.Split(Environment.NewLine))
            {
                string[] split = line.Trim().Replace("signed ", "signed").Replace(" *", "* ").Replace(";", "").Split(' ');
                Debug.Assert(split.Length == 2);
                string name = "";
                foreach (string part in split[1].Split('_'))
                {
                    name += part[0].ToString().ToUpperInvariant() + part[1..];
                }
                string comment = "";
                string type;
                string getter;
                string setter;
                int size;
                bool enums = false;
                bool embed = false;
                string cast = "";
                if (split[0].Contains("*"))
                {
                    type = "IntPtr";
                    getter = "ReadPointer";
                    setter = "WritePointer";
                    size = 4;
                    comment = $" // {split[0]}";
                }
                else if (split[0] == "EntityPtrUnion")
                {
                    type = "IntPtr";
                    getter = "ReadPointer";
                    setter = "WritePointer";
                    size = 4;
                    comment = $" // CEntity*";
                }
                else if (split[0] == "EntityIdOrRef")
                {
                    type = "IntPtr";
                    getter = "ReadPointer";
                    setter = "WritePointer";
                    size = 4;
                    comment = $" // EntityIdOrRef";
                }
                else if (split[0] == "int")
                {
                    type = "int";
                    getter = "ReadInt32";
                    setter = "WriteInt32";
                    size = 4;
                }
                else if (split[0] == "unsignedint")
                {
                    type = "uint";
                    getter = "ReadUInt32";
                    setter = "WriteUInt32";
                    size = 4;
                }
                else if (split[0] == "signed__int16")
                {
                    type = "short";
                    getter = "ReadInt16";
                    setter = "WriteInt16";
                    size = 2;
                }
                else if (split[0] == "__int16" || split[0] == "unsigned__int16")
                {
                    type = "ushort";
                    getter = "ReadUInt16";
                    setter = "WriteUInt16";
                    size = 2;
                }
                else if (split[0] == "char" || split[0] == "__int8" || split[0] == "unsigned__int8")
                {
                    type = "byte";
                    getter = "ReadByte";
                    setter = "WriteByte";
                    size = 1;
                }
                else if (split[0] == "signed__int8")
                {
                    type = "sbyte";
                    getter = "ReadSByte";
                    setter = "WriteSByte";
                    size = 1;
                }
                else if (split[0] == "Color3")
                {
                    type = "ColorRgb";
                    getter = "ReadColor3";
                    setter = "WriteColor3";
                    size = 3;
                }
                else if (split[0] == "VecFx32")
                {
                    type = "Vector3";
                    getter = "ReadVec3";
                    setter = "WriteVec3";
                    size = 12;
                }
                else if (split[0] == "Vec4")
                {
                    type = "Vector4";
                    getter = "ReadVec4";
                    setter = "WriteVec4";
                    size = 16;
                }
                else if (split[0] == "MtxFx43")
                {
                    type = "Matrix4x3";
                    getter = "ReadMtx43";
                    setter = "WriteMtx43";
                    size = 48;
                }
                else if (split[0] == "CModel")
                {
                    type = "CModel";
                    getter = "";
                    setter = "";
                    size = 0x48;
                    embed = true;
                }
                else if (split[0] == "BeamInfo")
                {
                    type = "BeamInfo";
                    getter = "";
                    setter = "";
                    size = 0x14;
                    embed = true;
                }
                else if (split[0] == "EntityCollision")
                {
                    type = "EntityCollision";
                    getter = "";
                    setter = "";
                    size = 0xB4;
                    embed = true;
                }
                else if (split[0] == "SmallSfxStruct")
                {
                    type = "SmallSfxStruct";
                    getter = "";
                    setter = "";
                    size = 4;
                    embed = true;
                }
                else if (split[0] == "CollisionVolume")
                {
                    type = "CollisionVolume";
                    getter = "";
                    setter = "";
                    size = 0x40;
                    embed = true;
                }
                else if (split[0] == "Light")
                {
                    type = "Light";
                    getter = "";
                    setter = "";
                    size = 0xF;
                    embed = true;
                }
                else if (split[0] == "LightInfo")
                {
                    type = "LightInfo";
                    getter = "";
                    setter = "";
                    size = 0x1F;
                    embed = true;
                }
                else if (split[0] == "CameraInfo")
                {
                    type = "CameraInfo";
                    getter = "";
                    setter = "";
                    size = 0x11C;
                    embed = true;
                }
                else if (split[0] == "PlayerControlsMaybe")
                {
                    type = "PlayerControls";
                    getter = "";
                    setter = "";
                    size = 0x9C;
                    embed = true;
                }
                else if (split[0] == "PlayerInputProbably")
                {
                    type = "PlayerInput";
                    getter = "";
                    setter = "";
                    size = 0x48;
                    embed = true;
                }
                else if (split[0] == "CBeamProjectile")
                {
                    type = "CBeamProjectile";
                    getter = "";
                    setter = "";
                    size = 0x158;
                    embed = true;
                }
                else if (byteEnums.TryGetValue(split[0], out string? value))
                {
                    type = value;
                    getter = "ReadByte";
                    setter = "WriteByte";
                    size = 1;
                    enums = true;
                    cast = "byte";
                }
                else if (ushortEnums.TryGetValue(split[0], out value))
                {
                    type = value;
                    getter = "ReadUInt16";
                    setter = "WriteUInt16";
                    size = 2;
                    enums = true;
                    cast = "ushort";
                }
                else if (uintEnums.TryGetValue(split[0], out value))
                {
                    type = value;
                    getter = "ReadUInt32";
                    setter = "WriteUInt32";
                    size = 4;
                    enums = true;
                    cast = "uint";
                }
                else
                {
                    type = split[0];
                    getter = "Read";
                    setter = "Write";
                    size = 4;
                    embed = true;
                    Debugger.Break();
                }
                int number = 0;
                bool array = name.Contains('[');
                string param = "";
                if (array)
                {
                    split = name.Split('[');
                    number = Int32.Parse(split[1].Split(']')[0]);
                    Debug.Assert(number > 1);
                    name = split[0];
                    if (comment == "")
                    {
                        comment = $" // {type}";
                    }
                    comment += $"[{number}]";
                    if (embed)
                    {
                        param = $",{Environment.NewLine}                {size}, (Memory m, int a) => new {type}(m, a)";
                        type = $"StructArray<{type}>";
                    }
                    else if (enums && size == 1)
                    {
                        type = $"U8EnumArray<{type}>";
                    }
                    else if (enums && size == 2)
                    {
                        type = $"U16EnumArray<{type}>";
                    }
                    else if (enums && size == 4)
                    {
                        type = $"U32EnumArray<{type}>";
                    }
                    else
                    {
                        type = getter.Replace("Read", "").Replace("Pointer", "IntPtr") + "Array";
                    }
                    size *= number;
                }
                Console.WriteLine($"        private const int _off{index} = 0x{offset:X1};{comment}");
                if (array)
                {
                    Console.WriteLine($"        public {type} {name} {{ get; }}");
                    news.Add($"            {name} = new {type}(memory, address + _off{index}, {number}{param});");
                }
                else if (embed)
                {
                    Console.WriteLine($"        public {type} {name} {{ get; }}");
                    news.Add($"            {name} = new {type}(memory, address + _off{index});");
                }
                else if (enums)
                {
                    Console.WriteLine($"        public {type} {name} {{ get => ({type}){getter}(_off{index}); " +
                        $"set => {setter}(_off{index}, ({cast})value); }}");
                }
                else
                {
                    Console.WriteLine($"        public {type} {name} {{ get => {getter}(_off{index}); " +
                        $"set => {setter}(_off{index}, value); }}");
                }
                Console.WriteLine();
                index++;
                offset += size;
            }
            Console.WriteLine($"        public {className}(Memory memory, int address) : base(memory, address)");
            Console.WriteLine("        {");
            foreach (string line in news)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine("        }");
            Console.WriteLine();
            Console.WriteLine($"        public {className}(Memory memory, IntPtr address) : base(memory, address)");
            Console.WriteLine("        {");
            foreach (string line in news)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine("        }");
            Console.WriteLine("    }");
            Debugger.Break();
        }
    }
}
