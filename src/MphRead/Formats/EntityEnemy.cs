using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace MphRead
{
    // size: 400 (256 + 36 * 4)
    public readonly struct EnemySpawnFields00
    {
        public readonly RawCollisionVolume Volume0;
        public readonly RawCollisionVolume Volume1;
        public readonly RawCollisionVolume Volume2;
        public readonly RawCollisionVolume Volume3;
    }

    // size: 400
    public readonly struct EnemySpawnFields01
    {
        public readonly EnemySpawnFieldsWW WarWasp;
        public readonly uint Padding1B0;
        public readonly uint Padding1B4;
    }

    // size: 400 (204 + 49 * 4)
    public readonly struct EnemySpawnFields02
    {
        public readonly RawCollisionVolume Volume0;
        public readonly Vector3Fx PathVector;
        public readonly RawCollisionVolume Volume1;
        public readonly RawCollisionVolume Volume2;
    }

    // size: 400 (128 + 68 * 4)
    public readonly struct EnemySpawnFields03
    {
        public readonly RawCollisionVolume Volume0;
        public readonly uint Unused68;
        public readonly uint Unused6C;
        public readonly uint Unused70;
        public readonly uint Unused74;
        public readonly uint Unused78;
        public readonly uint Unused7C;
        public readonly uint Unused80;
        public readonly Vector3Fx Facing;
        public readonly Vector3Fx Position;
        public readonly uint Field9C;
        public readonly uint UnusedA0;
        public readonly uint FieldA4;
    }

    // size: 400 (100 + 75 * 4)
    public readonly struct EnemySpawnFields04
    {
        public readonly RawCollisionVolume Volume0;
        public readonly uint Unused68;
        public readonly uint Unused6C;
        public readonly uint Unused70;
        public readonly uint Unused74;
        public readonly Vector3Fx Position;
        public readonly uint Field84;
        public readonly uint Field88;
    }

    // size: 400 (260 + 35 * 4)
    public readonly struct EnemySpawnFields05
    {
        public readonly uint EnemySubtype;
        public readonly RawCollisionVolume Volume0;
        public readonly RawCollisionVolume Volume1;
        public readonly RawCollisionVolume Volume2;
        public readonly RawCollisionVolume Volume3;
    }

    // size: 400 (264 + 34 * 4)
    public readonly struct EnemySpawnFields06
    {
        public readonly uint EnemySubtype;
        public readonly uint EnemyVersion;
        public readonly RawCollisionVolume Volume0;
        public readonly RawCollisionVolume Volume1;
        public readonly RawCollisionVolume Volume2;
        public readonly RawCollisionVolume Volume3;
    }

    // size: 400 (72 + 82 * 4)
    public readonly struct EnemySpawnFields07
    {
        public readonly ushort EnemyHealth;
        public readonly ushort EnemyDamage;
        public readonly uint EnemySubtype;
        public readonly RawCollisionVolume Volume0; // unused
    }

    // size: 400
    public readonly struct EnemySpawnFields08
    {
        public readonly uint EnemySubtype;
        public readonly uint EnemyVersion;
        public readonly EnemySpawnFieldsWW WarWasp;
    }

    // size: 400 (20 + 95 * 4)
    public readonly struct EnemySpawnFields09
    {
        public readonly uint HunterId;
        public readonly uint EncounterType;
        public readonly uint HunterWeapon;
        public readonly ushort HunterHealth;
        public readonly ushort HunterHealthMax;
        public readonly ushort Field38; // set in AI data
        public readonly byte HunterColor;
        public readonly byte HunterChance;
    }

    // size: 400 (140 + 65 * 4)
    public readonly struct EnemySpawnFields10
    {
        public readonly uint EnemySubtype;
        public readonly uint EnemyVersion;
        public readonly RawCollisionVolume Volume0;
        public readonly RawCollisionVolume Volume1;
        public readonly uint FieldB0;
    }

    // size: 400 (32 + 92 * 4)
    public readonly struct EnemySpawnFields11
    {
        public readonly Vector3Fx Sphere1Position;
        public readonly Fixed Sphere1Radius;
        public readonly Vector3Fx Sphere2Position;
        public readonly Fixed Sphere2Radius;
    }

    // size: 400 (20 + 95 * 4)
    public readonly struct EnemySpawnFields12
    {
        public readonly Vector3Fx Field28;
        public readonly uint Field34;
        public readonly uint Field38;
    }

    // size: 400
    public readonly struct EnemySpawnFieldsWW
    {
        public readonly RawCollisionVolume Volume0;
        public readonly RawCollisionVolume Volume1;
        public readonly RawCollisionVolume Volume2;
        public readonly Vector3FxArray16 MovementVectors;
        public readonly byte Field1A8;
        public readonly byte Padding1A9;
        public readonly ushort Padding1AA;
        public readonly uint MovementType;
    }

    // size: 400
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct EnumSpawnUnion
    {
        [FieldOffset(0)]
        public readonly EnemySpawnFields00 S00;
        [FieldOffset(0)]
        public readonly EnemySpawnFields01 S01;
        [FieldOffset(0)]
        public readonly EnemySpawnFields02 S02;
        [FieldOffset(0)]
        public readonly EnemySpawnFields03 S03;
        [FieldOffset(0)]
        public readonly EnemySpawnFields04 S04;
        [FieldOffset(0)]
        public readonly EnemySpawnFields05 S05;
        [FieldOffset(0)]
        public readonly EnemySpawnFields06 S06;
        [FieldOffset(0)]
        public readonly EnemySpawnFields07 S07;
        [FieldOffset(0)]
        public readonly EnemySpawnFields08 S08;
        [FieldOffset(0)]
        public readonly EnemySpawnFields09 S09;
        [FieldOffset(0)]
        public readonly EnemySpawnFields10 S10;
        [FieldOffset(0)]
        public readonly EnemySpawnFields11 S11;
        [FieldOffset(0)]
        public readonly EnemySpawnFields12 S12;
    }

    // size: 512
    public readonly struct EnemySpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly EnemyType EnemyType;
        public readonly byte Padding25; // in-game, the type is 4 bytes on this struct (but is 1 byte on the class),
        public readonly ushort Padding26; // so this padding isn't actually there
        public readonly EnumSpawnUnion Fields;
        public readonly short LinkedEntityId; // always -1 except for Cretaphid 4
        public readonly byte SpawnLimit;
        public readonly byte SpawnTotal;
        public readonly byte SpawnCount;
        public readonly byte Active; // boolean
        public readonly byte AlwaysActive; // boolean
        public readonly byte ItemChance;
        public readonly ushort SpawnerModel;
        public readonly ushort CooldownTime;
        public readonly ushort InitialCooldown;
        public readonly ushort Padding1C6;
        public readonly Fixed ActiveDistance; // todo: display sphere
        public readonly uint Field1CC; // unused?
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName;
        public readonly short EntityId1;
        public readonly ushort Padding1E2;
        public readonly Message Message1;
        public readonly short EntityId2;
        public readonly ushort Padding1EA;
        public readonly Message Message2;
        public readonly short EntityId3;
        public readonly ushort Padding1F2;
        public readonly Message Message3;
        public readonly ItemType ItemType;
    }

    // size: 268
    public readonly struct FhEnemySpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhRawCollisionVolume Box; // used by Mochtroid1 and Metroid
        public readonly FhRawCollisionVolume Cylinder; // used by Mochtroid2/3/4
        public readonly FhRawCollisionVolume Sphere; // used by Zoomer
        public readonly FhEnemyType EnemyType;
        public readonly byte SpawnTotal;
        public readonly byte SpawnLimit;
        public readonly byte SpawnCount;
        public readonly byte PaddingEB;
        public readonly ushort Cooldown;
        public readonly ushort EndFrame;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName;
        public readonly short ParentId;
        public readonly ushort Padding102;
        public readonly FhMessage EmptyMessage;
    }
}

namespace MphRead.Editor
{
    public class EnemySpawnEntityEditor : EntityEditorBase
    {
        public EnemyType EnemyType { get; set; }
        public short LinkedEntityId { get; set; } // always -1 except for Cretaphid 4
        public byte SpawnLimit { get; set; }
        public byte SpawnTotal { get; set; }
        public byte SpawnCount { get; set; }
        public bool Active { get; set; }
        public bool AlwaysActive { get; set; }
        public byte ItemChance { get; set; }
        public ushort SpawnerModel { get; set; }
        public ushort CooldownTime { get; set; }
        public ushort InitialCooldown { get; set; }
        public float ActiveDistance { get; set; } // todo: display sphere
        public uint Field1CC { get; set; }
        public string SpawnNodeName { get; set; } = "";
        public short EntityId1 { get; set; }
        public Message Message1 { get; set; }
        public short EntityId2 { get; set; }
        public Message Message2 { get; set; }
        public short EntityId3 { get; set; }
        public Message Message3 { get; set; }
        public ItemType ItemType { get; set; }

        // common
        public uint EnemySubtype { get; set; }
        public uint EnemyVersion { get; set; }
        public CollisionVolume Volume0 { get; set; }
        public CollisionVolume Volume1 { get; set; }
        public CollisionVolume Volume2 { get; set; }
        public CollisionVolume Volume3 { get; set; }

        // shriekbat
        public Vector3 PathVector { get; set; }

        // temroid, petrasyl
        public Vector3 EnemyFacing { get; set; }
        public Vector3 EnemyPosition { get; set; }
        public uint Unknown00 { get; set; }
        public uint Unknown01 { get; set; }
        public uint Unused68 { get; set; }
        public uint Unused6C { get; set; }
        public uint Unused70 { get; set; }
        public uint Unused74 { get; set; }
        public uint Unused78 { get; set; }
        public uint Unused7C { get; set; }
        public uint Unused80 { get; set; }
        public uint UnusedA0 { get; set; }

        // carnivorous plant
        public ushort EnemyHealth { get; set; }
        public ushort EnemyDamage { get; set; }

        // war wasp
        public List<Vector3> MovementVectors { get; set; } = new List<Vector3>();
        public byte Unknown02 { get; set; }
        public uint MovementType { get; set; }

        // enemy hunter
        public Hunter Hunter { get; set; }
        public uint EncounterType { get; set; }
        public uint HunterWeapon { get; set; }
        public ushort HunterHealth { get; set; }
        public ushort HunterHealthMax { get; set; }
        public ushort Unknown03 { get; set; }
        public byte HunterColor { get; set; }
        public byte HunterChance { get; set; }

        // slench turret
        public uint Unknown04 { get; set; }

        // gorea 2
        public Vector3 Unknown05 { get; set; }
        public uint Unknown06 { get; set; }
        public uint Unknown07 { get; set; }

        public int SpawnerType => GetSpawnerType(EnemyType);

        public static int GetSpawnerType(EnemyType type)
        {
            if (type == EnemyType.Zoomer || type == EnemyType.Geemer || type == EnemyType.Blastcap
                || type == EnemyType.Voldrum2 || type == EnemyType.Quadtroid || type == EnemyType.CrashPillar
                || type == EnemyType.Slench || type == EnemyType.LesserIthrak || type == EnemyType.Trocra)
            {
                return 0;
            }
            if (type == EnemyType.WarWasp)
            {
                return 1;
            }
            if (type == EnemyType.Shriekbat)
            {
                return 2;
            }
            if (type == EnemyType.Temroid || type == EnemyType.Petrasyl1)
            {
                return 3;
            }
            if (type == EnemyType.Petrasyl2 || type == EnemyType.Petrasyl3 || type == EnemyType.Petrasyl4)
            {
                return 4;
            }
            if (type == EnemyType.Cretaphid || type == EnemyType.GreaterIthrak)
            {
                return 5;
            }
            if (type == EnemyType.AlimbicTurret || type == EnemyType.PsychoBit1 || type == EnemyType.PsychoBit1
                || type == EnemyType.Voldrum1 || type == EnemyType.FireSpawn)
            {
                return 6;
            }
            if (type == EnemyType.CarnivorousPlant)
            {
                return 7;
            }
            if (type == EnemyType.BarbedWarWasp)
            {
                return 8;
            }
            if (type == EnemyType.Hunter)
            {
                return 9;
            }
            if (type == EnemyType.SlenchTurret)
            {
                return 10;
            }
            if (type == EnemyType.Gorea1A)
            {
                return 11;
            }
            if (type == EnemyType.Gorea2)
            {
                return 12;
            }
            Debug.Assert(false, $"Unexpected enemy type {type} for spawner entity.");
            return 0;
        }

        public EnemySpawnEntityEditor() : base(EntityType.EnemySpawn)
        {
        }

        public EnemySpawnEntityEditor(Entity header, EnemySpawnEntityData raw) : base(header)
        {
            EnemyType = raw.EnemyType;
            LinkedEntityId = raw.LinkedEntityId;
            SpawnLimit = raw.SpawnLimit;
            SpawnTotal = raw.SpawnTotal;
            SpawnCount = raw.SpawnCount;
            Active = raw.Active != 0;
            AlwaysActive = raw.AlwaysActive != 0;
            ItemChance = raw.ItemChance;
            SpawnerModel = raw.SpawnerModel;
            CooldownTime = raw.CooldownTime;
            InitialCooldown = raw.InitialCooldown;
            ActiveDistance = raw.ActiveDistance.FloatValue;
            Field1CC = raw.Field1CC;
            SpawnNodeName = raw.NodeName.MarshalString();
            EntityId1 = raw.EntityId1;
            Message1 = raw.Message1;
            EntityId2 = raw.EntityId2;
            Message2 = raw.Message2;
            EntityId3 = raw.EntityId3;
            Message3 = raw.Message3;
            ItemType = raw.ItemType;
            int spawnerType = GetSpawnerType(EnemyType);
            if (spawnerType == 0)
            {
                Volume0 = new CollisionVolume(raw.Fields.S00.Volume0);
                Volume1 = new CollisionVolume(raw.Fields.S00.Volume1);
                Volume2 = new CollisionVolume(raw.Fields.S00.Volume2);
                Volume3 = new CollisionVolume(raw.Fields.S00.Volume3);
            }
            else if (spawnerType == 2)
            {
                Volume0 = new CollisionVolume(raw.Fields.S02.Volume0);
                Volume1 = new CollisionVolume(raw.Fields.S02.Volume1);
                Volume2 = new CollisionVolume(raw.Fields.S02.Volume2);
                PathVector = raw.Fields.S02.PathVector.ToFloatVector();
            }
            else if (spawnerType == 3)
            {
                // sktodo: set and write unused fields
                Volume0 = new CollisionVolume(raw.Fields.S03.Volume0);
                EnemyPosition = raw.Fields.S03.Position.ToFloatVector();
                EnemyFacing = raw.Fields.S03.Facing.ToFloatVector();
                Unknown00 = raw.Fields.S03.Field9C;
                Unknown01 = raw.Fields.S03.FieldA4;
                Unused68 = raw.Fields.S03.Unused68;
                Unused6C = raw.Fields.S03.Unused6C;
                Unused70 = raw.Fields.S03.Unused70;
                Unused74 = raw.Fields.S03.Unused74;
                Unused78 = raw.Fields.S03.Unused78;
                Unused7C = raw.Fields.S03.Unused7C;
                Unused80 = raw.Fields.S03.Unused80;
                UnusedA0 = raw.Fields.S03.UnusedA0;
            }
            else if (spawnerType == 4)
            {
                Volume0 = new CollisionVolume(raw.Fields.S04.Volume0);
                EnemyPosition = raw.Fields.S04.Position.ToFloatVector();
                Unknown00 = raw.Fields.S04.Field84;
                Unknown01 = raw.Fields.S04.Field88;
                Unused68 = raw.Fields.S04.Unused68;
                Unused6C = raw.Fields.S04.Unused6C;
                Unused70 = raw.Fields.S04.Unused70;
                Unused74 = raw.Fields.S04.Unused74;
            }
            else if (spawnerType == 1 || spawnerType == 8)
            {
                void SetWarWaspFields(EnemySpawnFieldsWW fields)
                {
                    Volume0 = new CollisionVolume(fields.Volume0);
                    Volume1 = new CollisionVolume(fields.Volume1);
                    Volume2 = new CollisionVolume(fields.Volume2);
                    for (int i = 0; i < 16; i++)
                    {
                        MovementVectors.Add(fields.MovementVectors[i].ToFloatVector());
                    }
                    Unknown02 = fields.Field1A8;
                    MovementType = fields.MovementType;
                }
                if (EnemyType == EnemyType.WarWasp)
                {
                    SetWarWaspFields(raw.Fields.S01.WarWasp);
                }
                else
                {
                    EnemySubtype = raw.Fields.S08.EnemySubtype;
                    EnemyVersion = raw.Fields.S08.EnemyVersion;
                    SetWarWaspFields(raw.Fields.S08.WarWasp);
                }
            }
            else if (spawnerType == 5)
            {
                EnemySubtype = raw.Fields.S05.EnemySubtype;
                Volume0 = new CollisionVolume(raw.Fields.S05.Volume0);
                Volume1 = new CollisionVolume(raw.Fields.S05.Volume1);
                Volume2 = new CollisionVolume(raw.Fields.S05.Volume2);
                Volume3 = new CollisionVolume(raw.Fields.S05.Volume3);
            }
            else if (spawnerType == 6)
            {
                EnemySubtype = raw.Fields.S06.EnemySubtype;
                EnemyVersion = raw.Fields.S06.EnemyVersion;
                Volume0 = new CollisionVolume(raw.Fields.S06.Volume0);
                Volume1 = new CollisionVolume(raw.Fields.S06.Volume1);
                Volume2 = new CollisionVolume(raw.Fields.S06.Volume2);
                Volume3 = new CollisionVolume(raw.Fields.S06.Volume3);
            }
            else if (spawnerType == 7)
            {
                EnemySubtype = raw.Fields.S07.EnemySubtype;
                EnemyHealth = raw.Fields.S07.EnemyHealth;
                EnemyDamage = raw.Fields.S07.EnemyDamage;
                Volume0 = new CollisionVolume(raw.Fields.S07.Volume0);
            }
            else if (spawnerType == 9)
            {
                Hunter = (Hunter)raw.Fields.S09.HunterId;
                EncounterType = raw.Fields.S09.EncounterType;
                HunterWeapon = raw.Fields.S09.HunterWeapon;
                HunterHealth = raw.Fields.S09.HunterHealth;
                HunterHealthMax = raw.Fields.S09.HunterHealthMax;
                Unknown03 = raw.Fields.S09.Field38; // set in AI data
                HunterColor = raw.Fields.S09.HunterColor;
                HunterChance = raw.Fields.S09.HunterChance;
            }
            else if (spawnerType == 10)
            {
                EnemySubtype = raw.Fields.S10.EnemySubtype;
                EnemyVersion = raw.Fields.S10.EnemyVersion;
                Volume0 = new CollisionVolume(raw.Fields.S10.Volume0);
                Volume1 = new CollisionVolume(raw.Fields.S10.Volume1);
                Unknown04 = raw.Fields.S10.FieldB0;
            }
            else if (spawnerType == 11)
            {
                Volume0 = new CollisionVolume(raw.Fields.S11.Sphere1Position.ToFloatVector(), raw.Fields.S11.Sphere1Radius.FloatValue);
                Volume1 = new CollisionVolume(raw.Fields.S11.Sphere2Position.ToFloatVector(), raw.Fields.S11.Sphere2Radius.FloatValue);
            }
            else if (spawnerType == 12)
            {
                Unknown05 = raw.Fields.S12.Field28.ToFloatVector();
                Unknown06 = raw.Fields.S12.Field34;
                Unknown07 = raw.Fields.S12.Field38;
            }
        }
    }

    public class FhEnemySpawnEntityEditor : EntityEditorBase
    {
        public CollisionVolume Box { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public CollisionVolume Sphere { get; set; }
        public FhEnemyType EnemyType { get; set; }
        public byte SpawnTotal { get; set; }
        public byte SpawnLimit { get; set; }
        public byte SpawnCount { get; set; }
        public ushort Cooldown { get; set; }
        public ushort EndFrame { get; set; }
        public string SpawnNodeName { get; set; } = "";
        public short ParentId { get; set; }
        public FhMessage EmptyMessage { get; set; }

        public FhEnemySpawnEntityEditor() : base(EntityType.FhEnemySpawn)
        {
        }

        public FhEnemySpawnEntityEditor(Entity header, FhEnemySpawnEntityData raw) : base(header)
        {
            Box = new CollisionVolume(raw.Box);
            Cylinder = new CollisionVolume(raw.Cylinder);
            Sphere = new CollisionVolume(raw.Sphere);
            EnemyType = raw.EnemyType;
            SpawnTotal = raw.SpawnTotal;
            SpawnLimit = raw.SpawnLimit;
            SpawnCount = raw.SpawnCount;
            Cooldown = raw.Cooldown;
            EndFrame = raw.EndFrame;
            SpawnNodeName = raw.NodeName.MarshalString();
            ParentId = raw.ParentId;
            EmptyMessage = raw.EmptyMessage;
        }
    }
}
