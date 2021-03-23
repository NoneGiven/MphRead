using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MphRead.Testing;

namespace MphRead.Memory
{
    public class Memory
    {
        private static class Addresses
        {
            public static readonly int EntityListHead = 0x20E3EE0;
            public static readonly int FrameCount = 0x20D94FC;
            public static readonly int PlayerUA = 0x20DB180;
            public static readonly int CamSeqData = 0x21335E0;

            public static class Save
            {
                public static readonly int Story = 0x20E97B0;
                public static readonly int Type3 = 0x20D958C;
                public static readonly int Settings = 0x20E83B8;
                public static readonly int License = 0x20EB948;
                public static readonly int Friends = 0x20ECEE0;
            }
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
            // FF DE FF E7 FF DE FF E7 FF DE FF E7 @ 0x2004000
            new Memory(Process.GetProcessById(3984)).Run();
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
            _baseAddress = new IntPtr(0x998E100);
            Task.Run(async () =>
            {
                // 0x137A9C Cretaphid 1 crystal
                // 0x137B8C Cretaphid 1 laser
                // 0x137C7C Cretaphid 2 plasma
                // 0x13846C Slench 1 tear
                // 0x13855C Slench beams x4
                //var results = new List<(int, int)>();
                //string last = "";
                RefreshMemory();
                var story = new StorySaveData(this, Addresses.Save.Story);
                var type3 = new SaveType3(this, Addresses.Save.Type3);
                var settings = new StatsAndSettings(this, Addresses.Save.Settings);
                var license = new StorySaveData(this, Addresses.Save.License);
                var friends = new StorySaveData(this, Addresses.Save.Friends);
                IReadOnlyList<StringTableEntry> scans = Strings.ReadStringTable(StringTables.ScanLog);
                while (true)
                {
                    RefreshMemory();
                    //GetEntities();
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
                    TestLogic.CompletionValues pcts = TestLogic.GetCompletionValues(story);
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
