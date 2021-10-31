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
            new Memory(Process.GetProcessById(53320)).Run();
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
            _baseAddress = new IntPtr(0x995E100);
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
                RefreshMemory();
                var players = new CPlayer[]
                {
                    new CPlayer(this, Addresses.Players),
                    new CPlayer(this, Addresses.Players + 0xF30),
                    new CPlayer(this, Addresses.Players + 0xF30 * 2),
                    new CPlayer(this, Addresses.Players + 0xF30 * 3)
                };
                //var states = new List<uint>();
                //var gameState = new GameState(this, Addresses.GameState);
                string[] levels = new string[] { "*", "**", "***", "****" };
                string[] dmgs = new string[] { "Low", "Med", "High", "ERROR" };
                while (true)
                {
                    sb.Clear();
                    RefreshMemory();
                    //for (int i = 0; i < 4; i++)
                    //{
                    //    byte flags = players[i].LoadFlags;
                    //    sb.AppendLine(" 7   6   5   4   3   2   1   0");
                    //    //             [ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ]
                    //    for (int b = 7; b >= 0; b--)
                    //    {
                    //        sb.Append($"[{((flags & (1 << b)) != 0 ? "*" : " ")}] ");
                    //    }
                    //    sb.AppendLine();
                    //    sb.AppendLine();
                    //}
                    uint state1 = _buffer[0xCBEA0]
                        | ((uint)_buffer[0xCBEA1] << 8)
                        | ((uint)_buffer[0xCBEA2] << 16)
                        | ((uint)_buffer[0xCBEA3] << 24);
                    uint state2 = _buffer[0xCBEA4]
                        | ((uint)_buffer[0xCBEA5] << 8)
                        | ((uint)_buffer[0xCBEA6] << 16)
                        | ((uint)_buffer[0xCBEA7] << 24);
                    uint state3 = _buffer[0xCBEA8]
                        | ((uint)_buffer[0xCBEA9] << 8)
                        | ((uint)_buffer[0xCBEAA] << 16)
                        | ((uint)_buffer[0xCBEAB] << 24);

                    //sb.AppendLine($"     1P Mode: {((state1 & 1) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"    Affinity: {((state1 & 2) != 0 ? "On" : "Off")}");
                    //sb.AppendLine($"  Auto reset: {((state1 & 4) != 0 ? "On" : "Off")}");
                    //uint botLevels = (state1 & 0x1F8) >> 3;
                    //sb.AppendLine($" Bot 2 level: {levels[botLevels & 3]}");
                    //sb.AppendLine($" Bot 3 level: {levels[(botLevels & 0xC) >> 2]}");
                    //sb.AppendLine($" Bot 4 level: {levels[(botLevels & 0x30) >> 4]}");
                    //sb.AppendLine($"Damage level: {dmgs[(state1 & 0x600) >> 9]}");
                    //sb.AppendLine($" Team damage: {((state1 & 0x800) != 0 ? "On" : "Off")}");
                    //sb.AppendLine($"    MP Match: {((state1 & 0x1000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"Player radar: {((state1 & 0x2000) != 0 ? "On" : "Off")}");
                    //sb.AppendLine($"  Room index: {(state1 & 0x7C000) >> 14}");
                    //sb.AppendLine($"  Room count: {(state1 & 0xF80000) >> 19}");
                    //sb.AppendLine($"Player count: {(state1 & 0x7000000) >> 24}");
                    //sb.AppendLine($"  Bot 1 flag: {((state1 & 0x8000000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"  Bot 2 flag: {((state1 & 0x10000000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"  Bot 3 flag: {((state1 & 0x20000000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"  Bot 4 flag: {((state1 & 0x40000000) != 0 ? "Yes" : "No")}");

                    sb.AppendLine($"       Bit 0: {((state2 & 1) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"       Bit 1: {((state2 & 2) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"       Bit 2: {((state2 & 4) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine();
                    sb.AppendLine($"    Bits 0-2: {state2 & 7}");
                    sb.AppendLine();
                    sb.AppendLine($"       Bit 3: {((state2 & 8) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"       Bit 4: {((state2 & 0x10) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"       Bit 5: {((state2 & 0x20) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($" Main player: {(state2 & 0xC0) >> 6}");
                    sb.AppendLine($"       Bit 8: {((state2 & 0x100) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"       Bit 9: {((state2 & 0x200) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 10: {((state2 & 0x400) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 11: {((state2 & 0x800) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 12: {((state2 & 0x1000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 13: {((state2 & 0x2000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 14: {((state2 & 0x4000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 15: {((state2 & 0x8000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 16: {((state2 & 0x10000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 17: {((state2 & 0x20000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 18: {((state2 & 0x40000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 19: {((state2 & 0x80000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"  Story file: {(state2 & 0x300000) >> 20}");
                    sb.AppendLine($"  Point goal: {(state2 & 0x3C00000) >> 22}");
                    sb.AppendLine($"Random arena: {((state2 & 0x4000000) != 0 ? "Yes" : "No")}");
                    sb.AppendLine($"      Bit 27: {((state2 & 0x8000000) != 0 ? "Set" : "Cleared")}");
                    sb.AppendLine($"      Bit 28: {((state2 & 0x10000000) != 0 ? "Set" : "Cleared")}");

                    //sb.AppendLine($" Slot 1 team: {((state3 & 1) != 0 ? "1" : "0")}");
                    //sb.AppendLine($" Slot 2 team: {((state3 & 2) != 0 ? "1" : "0")}");
                    //sb.AppendLine($" Slot 3 team: {((state3 & 4) != 0 ? "1" : "0")}");
                    //sb.AppendLine($" Slot 4 team: {((state3 & 8) != 0 ? "1" : "0")}");
                    //sb.AppendLine($"       Teams: {((state3 & 0x10) != 0 ? "On" : "Off")}");
                    //sb.AppendLine($"   Time goal: {(state3 & 0x1E0) >> 5}");
                    //sb.AppendLine($"  Time limit: {(state3 & 0x1E00) >> 9}");
                    //sb.AppendLine($"  Wi-Fi mode: {((state3 & 0x2000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"   Worldwide: {((state3 & 0x4000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"  Match rank: {((state3 & 0x8000) != 0 ? "Yes" : "No")}");

                    //ushort flags = gameState.SomeFlags;
                    //sb.AppendLine($"Team dmg: {((flags & 1) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"   Teams: {((flags & 2) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"Affinity: {((flags & 4) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"   Radar: {((flags & 8) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine();
                    //sb.AppendLine($"   Bit 4: {((flags & 0x10) != 0 ? "Set" : "Cleared")}");
                    //sb.AppendLine($"   Bit 5: {((flags & 0x20) != 0 ? "Set" : "Cleared")}");
                    //sb.AppendLine($"  Bit 12: {((flags & 0x1000) != 0 ? "Set" : "Cleared")}");
                    //sb.AppendLine($"  Bit 15: {((flags & 0x8000) != 0 ? "Set" : "Cleared")}");
                    //sb.AppendLine();
                    //uint state = ((uint)flags << 24) >> 30;
                    //sb.AppendLine($"Bits 6/7: {state}");
                    //sb.AppendLine();
                    //sb.AppendLine($"Bits 8/9: {((uint)flags << 22) >> 30}");
                    //sb.AppendLine();
                    //sb.AppendLine($"Tele alt: {((flags & 0x400) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"Clean st: {((flags & 0x800) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"Portal S: {((flags & 0x2000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine($"Portal D: {((flags & 0x4000) != 0 ? "Yes" : "No")}");
                    //sb.AppendLine();
                    //if (states.Count == 0 || states[^1] != state)
                    //{
                    //    states.Add(state);
                    //}
                    //sb.AppendLine(String.Join(", ", states));
                    //sb.AppendLine();
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
