using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Memory
{
    public class Memory
    {
        private class AddressInfo
        {
            public int EntityListHead { get; }
            public int FrameCount { get; }
            public int PlayerUA { get; }
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

            public AddressInfo(int gameState, int entityListHead, int frameCount, int playerUa,
                int camSeqData, int roomDesc, SaveAddressInfo save)
            {
                GameState = gameState;
                EntityListHead = entityListHead;
                FrameCount = frameCount;
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
            // FF DE FF E7 FF DE FF E7 FF DE FF E7 @ 0x2004000
            new Memory(Process.GetProcessById(43444)).Run();
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
            Addresses = AllAddresses["amhp1"];
            _baseAddress = new IntPtr(0x98C5100);
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
                var sb = new StringBuilder();
                var ents = new List<int>();
                var nd3s = new List<int>();
                var matches = new Dictionary<int, CEntity>();
                RefreshMemory();
                //var story = new StorySaveData(this, Addresses.Save.Story);
                //var type3 = new SaveType3(this, Addresses.Save.Type3);
                //var settings = new StatsAndSettings(this, Addresses.Save.Settings);
                //var license = new StorySaveData(this, Addresses.Save.License);
                //var friends = new StorySaveData(this, Addresses.Save.Friends);
                //var state = new GameState(this, Addresses.GameState);
                var state = new KioskGameState(this, Addresses.GameState);
                IReadOnlyList<StringTableEntry> scans = Strings.ReadStringTable(StringTables.ScanLog);
                while (true)
                {
                    sb.Clear();
                    RefreshMemory();
                    GetEntities();
                    //byte[] weapon = new byte[0xF0];
                    //for (int i = 0; i < 0xF0; i++)
                    //{
                    //    weapon[i] = _buffer[0x137C7C + i];
                    //}
                    //Test.DumpWeaponInfo(Test.ParseWeaponInfo(1, weapon)[0]);
                    //var camSeqs = new IntPtrArray(this, Addresses.CamSeqData, 175);
                    //var scanIntro = new CameraSequence(this, camSeqs[5]);
                    //var keyframe0 = new CameraSequenceKeyframe(this, scanIntro.Keyframes);
                    //var keyframe1 = new CameraSequenceKeyframe(this, keyframe0.Next);
                    //var keyframe2 = new CameraSequenceKeyframe(this, keyframe1.Next);
                    //var beams = _entities.Where(e => e.EntityType == EntityType.BeamProjectile).ToList();
                    //var player = _entities.FirstOrDefault(e => e.EntityType == EntityType.Player) as CPlayer;
                    //if (player != null)
                    //{
                    //    player.AvailableWeapons = 0xFF;
                    //    player.AvailableCharges = 0xFF;
                    //    player.Energy += 99;
                    //}
                    //if (player != null)
                    //{
                    //    int bit0 = player.SomeFlags & 0x10;
                    //    int bit1 = player.SomeFlags & 0x40;
                    //    string output = $"{(bit0 == 0 ? "Cleared" : "Set")} // {(bit1 == 0 ? "Cleared" : "Set")}";
                    //    if (output != last)
                    //    {
                    //        Console.Write($"\r{output}                     ");
                    //        last = output;
                    //    }
                    //}
                    //Console.Clear();
                    //foreach (CPlatform plat in _entities.Where(e => e.EntityType == EntityType.Platform).Select(e => (CPlatform)e))
                    //{
                    //    Console.WriteLine($"{plat.ModelId}: {plat.State}");
                    //}
                    //TestLogic.CompletionValues pcts = TestLogic.GetCompletionValues(story);
                    //state.AreaId = 8;
                    //state.BattleTimeLimit = 25200;
                    //state.TimeLimit = 25200;
                    //state.BotCount = 1; // todo: work around the allocation thing
                    //state.Field6[1] = 0;
                    //state.GameMode = GameMode.Battle;
                    //state.Hunters[1] = 1;
                    //state.SuitColors[1] = 1;
                    //state.MaxPlayers = 2;
                    //state.PlayerCount = 2;
                    //state.PointLimit = 7;
                    //state.RoomId = 105;
                    //state.SomeFlags = 6160;
                    //state.LayerId = 255;

                    var ptr1s = new List<uint>() { 0x22AEB28 };

                    int str3 = 0x2AD8E0;
                    ushort prev = 0;
                    ushort id = BitConverter.ToUInt16(_buffer, str3 + 2);
                    Debug.Assert(id == 0);
                    do
                    {
                        str3 += 0x24;
                        prev = id;
                        id = BitConverter.ToUInt16(_buffer, str3 + 2);
                        if (id != 0)
                        {
                            ptr1s.Add(BitConverter.ToUInt32(_buffer, str3 + 0x18));
                        }
                    }
                    while (id == prev + 1);

                    for (int i = 0; i < ptr1s.Count; i++)
                    {
                        Console.Write($"{i,3}: 0x{ptr1s[i]:X1}");
                        if (i < ptr1s.Count - 1)
                        {
                            uint diff = ptr1s[i + 1] - ptr1s[i];
                            Debug.Assert(diff >= 0xC);
                            Debug.Assert(diff % 0xC == 0);
                            Console.Write($" + {diff:X1}");
                        }
                        Console.WriteLine();
                    }

                    ents.Clear();
                    nd3s.Clear();
                    matches.Clear();
                    int none = 0;
                    foreach (CPlayer player in _entities.Where(e => e.EntityType == EntityType.Player))
                    {
                        if (player.IsBot != 0 && player.SlotIndex == 1)
                        {
                            int aiAddr = player.AiData.ToInt32();
                            Debug.Assert(aiAddr != 0);
                            for (int i = 0; i < 78; i++)
                            {
                                int addr = aiAddr + 0x120 + i * 4;
                                int ptr = BitConverter.ToInt32(_buffer, addr - Offset);
                                if (ptr == 0)
                                {
                                    none++;
                                }
                                else if (_entities.Any(e => e.Address.ToInt32() == ptr))
                                {
                                    ents.Add(ptr);
                                }
                                else
                                {
                                    nd3s.Add(ptr);
                                }
                            }
                        }
                    }
                    foreach (int addr in nd3s)
                    {
                        foreach (CEntity entity in _entities)
                        {
                            if (entity.EntityType == EntityType.JumpPad && entity is CJumpPad jumpPad)
                            {
                                if (jumpPad.NodedataRelated.ToInt32() == addr)
                                {
                                    matches.Add(addr, jumpPad);
                                }
                            }
                            else if (entity.EntityType == EntityType.OctolithFlag && entity is COctolithFlag octo)
                            {
                                if (octo.NodedataRelated.ToInt32() == addr)
                                {
                                    matches.Add(addr, octo);
                                }
                            }
                            else if (entity.EntityType == EntityType.FlagBase && entity is CFlagBase flag)
                            {
                                if (flag.NodedataRelated.ToInt32() == addr)
                                {
                                    matches.Add(addr, flag);
                                }
                            }
                            else if (entity.EntityType == EntityType.NodeDefense && entity is CNodeDefense def)
                            {
                                if (def.NodedataRelated.ToInt32() == addr)
                                {
                                    matches.Add(addr, def);
                                }
                            }
                        }
                    }
                    sb.AppendLine($"ents: {ents.Count}");
                    sb.AppendLine($"nd3s: {nd3s.Count}");
                    sb.AppendLine($"none: {none}");
                    if (matches.Count > 0)
                    {
                        Debugger.Break();
                    }
                    string newOutput = sb.ToString();
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
    }
}
