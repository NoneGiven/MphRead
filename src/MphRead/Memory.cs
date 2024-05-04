using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Memory
{
    public class Memory
    {
        private void DoProcess()
        {
            CEnemy24? gorea1A = null;
            GetEntities();
            foreach (CEntity entity in _entities)
            {
                if (entity.EntityType == EntityType.EnemyInstance && entity is CEnemy24 enemy)
                {
                    gorea1A = enemy;
                    break;
                }
            }
            Debug.Assert(gorea1A != null);
            _sb.AppendLine($"state {gorea1A.State}");
        }

        private class AddressInfo
        {
            public int EntityListHead { get; }
            public int FrameCount { get; }
            public int PlayerUA { get; }
            public int Players { get; }
            public int CamSeqData { get; }
            public int GameState { get; }
            public int RoomDesc { get; }

            public SaveAddressInfo Save { get; }

            public class SaveAddressInfo
            {
                public int Story { get; }
                public int Type3 { get; }
                public int Settings { get; }
                public int License { get; }
                public int Friends { get; }

                public SaveAddressInfo(int story, int type3, int settings, int license, int friends)
                {
                    Story = story;
                    Type3 = type3;
                    Settings = settings;
                    License = license;
                    Friends = friends;
                }
            }

            public AddressInfo(int gameState, int entityListHead, int frameCount, int players,
                int playerUa, int camSeqData, int roomDesc, SaveAddressInfo save)
            {
                GameState = gameState;
                EntityListHead = entityListHead;
                FrameCount = frameCount;
                Players = players;
                PlayerUA = playerUa;
                CamSeqData = camSeqData;
                RoomDesc = roomDesc;
                Save = save;
            }
        }

        private static AddressInfo Addresses { get; set; } = null!;

        private static readonly IReadOnlyDictionary<string, AddressInfo> AllAddresses = new Dictionary<string, AddressInfo>()
        {
            ["a76e"] = new AddressInfo(
                gameState: 0x20BC420, // todo: class
                entityListHead: 0x20B85F8,
                frameCount: 0x20AE514,
                players: 0x20B00D4, // todo
                playerUa: 0x20B00D4,
                camSeqData: 0x2103760,
                roomDesc: 0x20B84C4, // todo
                new AddressInfo.SaveAddressInfo(
                    story: 0x20BD798,
                    type3: 0x20D958C, // todo
                    settings: 0x20BC364,
                    license: 0x20EB948, // todo
                    friends: 0x20ECEE0 // todo
                )
            ),
            ["amhp1"] = new AddressInfo(
                gameState: 0x20E845C,
                entityListHead: 0x20E3EE0,
                frameCount: 0x20D94FC,
                players: 0x20DB034,
                playerUa: 0x20DB180,
                camSeqData: 0x21335E0,
                roomDesc: 0x20B84C4,
                new AddressInfo.SaveAddressInfo(
                    story: 0x20E97B0,
                    type3: 0x20D958C,
                    settings: 0x20E83B8,
                    license: 0x20EB948,
                    friends: 0x20ECEE0
                )
            )
        };

        public struct SystemInfo
        {
            public ushort ProcessorArchitecture;
            public ushort Reserved;
            public uint PageSize;
            public IntPtr MinimumApplicationAddress;
            public IntPtr MaximumApplicationAddress;
            public IntPtr ActiveProcessorMask;
            public uint NumberOfProcessors;
            public uint ProcessorType;
            public uint AllocationGranularity;
            public ushort ProcessorLevel;
            public ushort ProcessorRevision;
        }

        public struct MemoryInfo64
        {
            public long BaseAddress;
            public long AllocationBase;
            public int AllocationProtect;
            public int Padding1;
            public long RegionSize;
            public int State;
            public int Protect;
            public int lType;
            public int Padding2;
        }

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(out SystemInfo lpSystemInfo);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
            out MemoryInfo64 lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

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
            // FF DE FF E7 FF DE FF E7 FF DE FF E7 @ 0x2004000
            //new Memory(Process.GetProcessById(17608)).Run();
            Process? foundProcess = null;
            DateTime startTime = DateTime.MinValue;
            Process[] procs = Process.GetProcessesByName("NO$GBA");
            foreach (Process process in procs)
            {
                if (process.StartTime > startTime)
                {
                    foundProcess = process;
                    startTime = process.StartTime;
                }
            }
            if (foundProcess == null)
            {
                throw new ProgramException("Could not find process.");
            }
            new Memory(foundProcess).Run();
        }

        private readonly List<CEntity> _entities = new List<CEntity>();
        private readonly Dictionary<IntPtr, CEntity> _temp = new Dictionary<IntPtr, CEntity>();

        private void SetBaseAddress()
        {
            if (!File.Exists("memory.txt"))
            {
                File.WriteAllText("memory.txt", "");
            }
            long startTime = new DateTimeOffset(_process.StartTime).ToUnixTimeMilliseconds();
            string[] lines = File.ReadAllLines("memory.txt");
            if (lines.Length >= 2 && Int64.TryParse(lines[0], out long timestamp)
                && startTime == timestamp
                && Int64.TryParse(lines[1].Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out long saved))
            {
                _baseAddress = new IntPtr(saved);
                return;
            }
            Console.WriteLine("Scanning memory...");
            // todo: no idea if this works for non-AMHP1
            byte[] search = new byte[] { 0xFF, 0xDE, 0xFF, 0xE7, 0xFF, 0xDE, 0xFF, 0xE7, 0xFF, 0xDE, 0xFF, 0xE7 };
            GetSystemInfo(out SystemInfo systemInfo);
            IntPtr minAddr = systemInfo.MinimumApplicationAddress;
            IntPtr maxAddr = systemInfo.MaximumApplicationAddress;
            IntPtr processHandle = OpenProcess(0x10 | 0x400, false, _process.Id);
            var memoryInfo = new MemoryInfo64();
            while (minAddr.ToInt64() < maxAddr.ToInt64())
            {
                VirtualQueryEx(processHandle, minAddr, out memoryInfo, 48);
                if (memoryInfo.Protect == 4 && memoryInfo.State == 0x1000)
                {
                    byte[] buffer = new byte[memoryInfo.RegionSize];
                    var baseAddr = new IntPtr(memoryInfo.BaseAddress);
                    bool result = ReadProcessMemory(processHandle, baseAddr, buffer, (int)memoryInfo.RegionSize, out IntPtr count);
                    Debug.Assert(result);
                    Debug.Assert(count.ToInt64() == memoryInfo.RegionSize);
                    for (int i = 0; i <= buffer.Length - search.Length; i++)
                    {
                        // the search sequence appears twice at an offset of 0x4000. at the start of the one
                        // we don't want is "MP HUNTERS", and at the start of the one we do want are zeroes.
                        if (buffer.Skip(i).Take(search.Length).SequenceEqual(search)
                            && buffer[i - 0x4000] == 0 && buffer[i - 0x4000 + 1] == 0)
                        {
                            _baseAddress = new IntPtr(memoryInfo.BaseAddress + i - 0x4000);
                            File.WriteAllLines("memory.txt", new string[2]
                            {
                                startTime.ToString(),
                                $"0x{_baseAddress:X2}"
                            });
                            return;
                        }
                    }
                }
                if (memoryInfo.RegionSize == 0)
                {
                    throw new ProgramException("Failed to scan memory.");
                }
                minAddr = new IntPtr(minAddr.ToInt64() + memoryInfo.RegionSize);
            }
            throw new ProgramException("Failed to find search sequence.");
        }

        private readonly CPlayer[] _players = new CPlayer[4];
        private readonly StringBuilder _sb = new StringBuilder();

        private void Run()
        {
            // todo: we should just detect the version automatically
            Addresses = AllAddresses["amhp1"];
            _baseAddress = new IntPtr(0x19E9A100);
            SetBaseAddress();
            Task.Run(async () =>
            {
                // 0x137A9C Cretaphid 1 crystal
                // 0x137B8C Cretaphid 1 laser
                // 0x137C7C Cretaphid 2 plasma
                // 0x13846C Slench 1 tear
                // 0x13855C Slench beams x4
                //var results = new List<(int, int)>();
                //string last = "";
                string output = "";
                RefreshMemory();
                _players[0] = new CPlayer(this, Addresses.Players);
                _players[1] = new CPlayer(this, Addresses.Players + 0xF30);
                _players[2] = new CPlayer(this, Addresses.Players + 0xF30 * 2);
                _players[3] = new CPlayer(this, Addresses.Players + 0xF30 * 3);
                while (true)
                {
                    _sb.Clear();
                    RefreshMemory();
                    DoProcess();
                    string newOutput = _sb.ToString();
                    if (newOutput != output)
                    {
                        output = newOutput;
                        Console.Clear();
                        Console.Write(output);
                    }
                    await Task.Delay(15);
                }
            }).GetAwaiter().GetResult();
        }

        private void RefreshMemory()
        {
            bool result = ReadProcessMemory(_process.Handle, _baseAddress, _buffer, _size, out IntPtr count);
            Debug.Assert(result);
            Debug.Assert(count.ToInt64() == _size);
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
                var enemy = new CEnemyBase(this, address);
                return enemy.Type switch
                {
                    EnemyType.Gorea1A => new CEnemy24(this, address),
                    EnemyType.GoreaHead => new CEnemy25(this, address),
                    EnemyType.GoreaArm => new CEnemy26(this, address),
                    EnemyType.GoreaLeg => new CEnemy27(this, address),
                    EnemyType.Gorea1B => new CEnemy28(this, address),
                    EnemyType.GoreaSealSphere1 => new CEnemy29(this, address),
                    EnemyType.Trocra => new CEnemy30(this, address),
                    _ => enemy
                };
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
    }
}
