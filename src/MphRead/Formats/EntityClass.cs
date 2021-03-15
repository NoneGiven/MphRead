using System.Collections.Generic;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Editor
{
    public abstract class EntityEditorBase
    {
        public EntityType Type { get; set; }
        public short Id { get; set; }
        public ushort LayerMask { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Up { get; set; }
        public Vector3 Facing { get; set; }
        public string NodeName { get; set; } = "";

        public EntityEditorBase()
        {
        }

        public EntityEditorBase(Entity header)
        {
            Type = header.Type;
            Id = header.EntityId;
            LayerMask = header.LayerMask;
            Position = header.Position;
            Up = header.UpVector;
            Facing = header.FacingVector;
            NodeName = header.NodeName;
        }
    }

    public class PlatformEntityEditor : EntityEditorBase
    {
        public uint NoPort { get; set; }
        public uint ModelId { get; set; }
        public short ParentId { get; set; }
        public byte Field2E { get; set; }
        public byte Field2F { get; set; }
        public ushort ScanData1 { get; set; }
        public short ScanMsgTarget { get; set; }
        public Message ScanMessage { get; set; }
        public ushort ScanData2 { get; set; }
        public ushort Field3A { get; set; }
        public List<Vector3> Positions { get; set; } = new List<Vector3>();
        public List<Vector4> Rotations { get; set; } = new List<Vector4>();
        public Vector3 PositionOffset { get; set; }
        public uint Field160 { get; set; }
        public uint Field164 { get; set; }
        public string PortalName { get; set; } = "";
        public uint Field178 { get; set; }
        public uint Field17C { get; set; }
        public uint Field180 { get; set; }
        public PlatformFlags Flags { get; set; }
        public uint ContactDamage { get; set; }
        public Vector3 BeamSpawnDir { get; set; }
        public Vector3 BeamSpawnPos { get; set; }
        public int BeamId { get; set; }
        public uint BeamInterval { get; set; }
        public uint BeamOnIntervals { get; set; } // 16 bits are used
        public int EffectId1 { get; set; }
        public uint Health { get; set; }
        public uint Field1BC { get; set; }
        public int EffectId2 { get; set; }
        public int EffectId3 { get; set; }
        public byte ItemChance { get; set; }
        public uint ItemModel { get; set; }
        public uint Field1D0 { get; set; }
        public uint Field1D4 { get; set; }
        public uint Message1Target { get; set; }
        public Message Message1 { get; set; }
        public uint Message1Param1 { get; set; }
        public uint Message1Param2 { get; set; }
        public uint Message2Target { get; set; }
        public Message Message2 { get; set; }
        public uint Message2Param1 { get; set; }
        public uint Message2Param2 { get; set; }
        public uint Message3Target { get; set; }
        public Message Message3 { get; set; }
        public uint Message3Param1 { get; set; }
        public uint Message3Param2 { get; set; }
        public ushort Field208 { get; set; }
        public short Msg32Target1 { get; set; }
        public Message Msg32Message1 { get; set; }
        public uint Msg32Param11 { get; set; }
        public uint Msg32Param21 { get; set; }
        public ushort Field218 { get; set; }
        public short Msg32Target2 { get; set; }
        public Message Msg32Message2 { get; set; }
        public uint Msg32Param12 { get; set; }
        public uint Msg32Param22 { get; set; }
        public ushort Field228 { get; set; }
        public short Msg32Target3 { get; set; }
        public Message Msg32Message3 { get; set; }
        public uint Msg32Param13 { get; set; }
        public uint Msg32Param23 { get; set; }
        public ushort Field238 { get; set; }
        public short Msg32Target4 { get; set; }
        public Message Msg32Message4 { get; set; }
        public uint Msg32Param14 { get; set; }
        public uint Msg32Param24 { get; set; }

        public PlatformEntityEditor(Entity header, PlatformEntityData raw) : base(header)
        {
            NoPort = raw.NoPort;
            ModelId = raw.ModelId;
            ParentId = raw.ParentId;
            Field2E = raw.Field2E;
            Field2F = raw.Field2F;
            ScanData1 = raw.ScanData1;
            ScanMsgTarget = raw.ScanMsgTarget;
            ScanMessage = raw.ScanMessage;
            ScanData2 = raw.ScanData2;
            Field3A = raw.Field3A;
            for (int i = 0; i < 10; i++)
            {
                Positions.Add(raw.Positions[i].ToFloatVector());
            }
            for (int i = 0; i < 10; i++)
            {
                Rotations.Add(raw.Rotations[i].ToFloatVector());
            }
            PositionOffset = raw.PositionOffset.ToFloatVector();
            Field160 = raw.Field160;
            Field164 = raw.Field164;
            PortalName = raw.PortalName.MarshalString();
            Field178 = raw.Field178;
            Field17C = raw.Field17C;
            Field180 = raw.Field180;
            Flags = raw.Flags;
            ContactDamage = raw.ContactDamage;
            BeamSpawnDir = raw.BeamSpawnDir.ToFloatVector();
            BeamSpawnPos = raw.BeamSpawnPos.ToFloatVector();
            BeamId = raw.BeamId;
            BeamInterval = raw.BeamInterval;
            BeamOnIntervals = raw.BeamOnIntervals;
            EffectId1 = raw.EffectId1;
            Health = raw.Health;
            Field1BC = raw.Field1BC;
            EffectId2 = raw.EffectId2;
            EffectId3 = raw.EffectId3;
            ItemChance = raw.ItemChance;
            ItemModel = raw.ItemModel;
            Field1D0 = raw.Field1D0;
            Field1D4 = raw.Field1D4;
            Message1Target = raw.Message1Target;
            Message1 = raw.Message1;
            Message1Param1 = raw.Message1Param1;
            Message1Param2 = raw.Message1Param2;
            Message2Target = raw.Message2Target;
            Message2 = raw.Message2;
            Message2Param1 = raw.Message2Param1;
            Message2Param2 = raw.Message2Param2;
            Message3Target = raw.Message3Target;
            Message3 = raw.Message3;
            Message3Param1 = raw.Message3Param1;
            Message3Param2 = raw.Message3Param2;
            Field208 = raw.Field208;
            Msg32Target1 = raw.Msg32Target1;
            Msg32Message1 = raw.Msg32Message1;
            Msg32Param11 = raw.Msg32Param11;
            Msg32Param21 = raw.Msg32Param21;
            Field218 = raw.Field218;
            Msg32Target2 = raw.Msg32Target2;
            Msg32Message2 = raw.Msg32Message2;
            Msg32Param12 = raw.Msg32Param12;
            Msg32Param22 = raw.Msg32Param22;
            Field228 = raw.Field228;
            Msg32Target3 = raw.Msg32Target3;
            Msg32Message3 = raw.Msg32Message3;
            Msg32Param13 = raw.Msg32Param13;
            Msg32Param23 = raw.Msg32Param23;
            Field238 = raw.Field238;
            Msg32Target4 = raw.Msg32Target4;
            Msg32Message4 = raw.Msg32Message4;
            Msg32Param14 = raw.Msg32Param14;
            Msg32Param24 = raw.Msg32Param24;
        }
    }

    public class FhPlatformEntityEditor : EntityEditorBase
    {
        public uint NoPortal { get; set; }
        public uint GroupId { get; set; }
        public uint Unused2C { get; set; }
        public byte Field30 { get; set; }
        public byte Field31 { get; set; }
        public CollisionVolume Volume { get; set; } // unused
        public List<Vector3> Positions { get; set; } = new List<Vector3>();
        public uint FieldD4 { get; set; }
        public string PortalName { get; set; } = "";

        public FhPlatformEntityEditor(Entity header, FhPlatformEntityData raw) : base(header)
        {
            NoPortal = raw.NoPortal;
            GroupId = raw.GroupId;
            Unused2C = raw.Unused2C;
            Field30 = raw.Field30;
            Field31 = raw.Field31;
            Volume = new CollisionVolume(raw.Volume);
            for (int i = 0; i < 8; i++)
            {
                Positions.Add(raw.Positions[i].ToFloatVector());
            }
            FieldD4 = raw.FieldD4;
            PortalName = raw.PortalName.MarshalString();
        }
    }

    public class ObjectEntityEditor : EntityEditorBase
    {
        public byte Flags { get; set; }
        public uint EffectFlags { get; set; }
        public uint ModelId { get; set; }
        public short LinkedEntity { get; set; }
        public ushort ScanId { get; set; }
        public short ScanMsgTarget { get; set; }
        public Message ScanMessage { get; set; }
        public uint EffectId { get; set; }
        public uint EffectInterval { get; set; }
        public uint EffectOnIntervals { get; set; } // 16 bits are used
        public Vector3 EffectPositionOffset { get; set; } // maximum value for random offset
        public CollisionVolume Volume { get; set; }

        public ObjectEntityEditor(Entity header, ObjectEntityData raw) : base(header)
        {
            Flags = raw.Flags;
            EffectFlags = raw.EffectFlags;
            ModelId = raw.ModelId;
            LinkedEntity = raw.LinkedEntity;
            ScanId = raw.ScanId;
            ScanMsgTarget = raw.ScanMsgTarget;
            ScanMessage = raw.ScanMessage;
            EffectId = raw.EffectId;
            EffectInterval = raw.EffectInterval;
            EffectOnIntervals = raw.EffectOnIntervals;
            EffectPositionOffset = raw.EffectPositionOffset.ToFloatVector();
            Volume = new CollisionVolume(raw.Volume);
        }
    }

    public class PlayerSpawnEntityEditor : EntityEditorBase
    {
        public byte Availability { get; set; } // 0 - any time, 1 - no first frame, 2 - bot only (FH)
        public bool Active { get; set; }
        public sbyte TeamIndex { get; set; } // 0, 1, or -1

        public PlayerSpawnEntityEditor(Entity header, PlayerSpawnEntityData raw) : base(header)
        {
            Availability = raw.Availability;
            Active = raw.Active != 0;
            TeamIndex = raw.TeamIndex;
        }
    }

    public class DoorEntityEditor : EntityEditorBase
    {
        public string DoorNodeName { get; set; } = "";
        public uint PaletteId { get; set; }
        public uint ModelId { get; set; }
        public uint TargetRoomId { get; set; }
        public byte TargetLayerId { get; set; }
        public byte Flags { get; set; } // bit 0 - locked
        public byte Field42 { get; set; }
        public byte Field43 { get; set; }
        public string EntityFilename { get; set; } = "";
        public string RoomName { get; set; } = "";

        public DoorEntityEditor(Entity header, DoorEntityData raw) : base(header)
        {
            DoorNodeName = raw.NodeName.MarshalString();
            PaletteId = raw.PaletteId;
            ModelId = raw.ModelId;
            TargetRoomId = raw.TargetRoomId;
            TargetLayerId = raw.TargetLayerId;
            Flags = raw.Flags;
            Field42 = raw.Field42;
            Field43 = raw.Field43;
            EntityFilename = raw.EntityFilename.MarshalString();
            RoomName = raw.RoomName.MarshalString();
        }
    }

    public class FhDoorEntityEditor : EntityEditorBase
    {
        public string RoomName { get; set; } = "";
        public uint Flags { get; set; }
        public uint ModelId { get; set; }

        public FhDoorEntityEditor(Entity header, FhDoorEntityData raw) : base(header)
        {
            RoomName = raw.RoomName.MarshalString();
            Flags = raw.Flags;
            ModelId = raw.ModelId;
        }
    }

    public class ItemSpawnEntityEditor : EntityEditorBase
    {
        public uint ParentId { get; set; }
        public ItemType ItemType { get; set; }
        public bool Enabled { get; set; }
        public bool HasBase { get; set; }
        public bool AlwaysActive { get; set; } // set flags bit 0 based on Active boolean only and ignore room state
        public ushort MaxSpawnCount { get; set; }
        public ushort SpawnInterval { get; set; }
        public ushort SpawnDelay { get; set; }
        public short SomeEntityId { get; set; } // todo: parent? child?
        public Message CollectedMessage { get; set; }
        public uint CollectedMsgParam1 { get; set; }
        public uint CollectedMsgParam2 { get; set; }

        public ItemSpawnEntityEditor(Entity header, ItemSpawnEntityData raw) : base(header)
        {
            ParentId = raw.ParentId;
            ItemType = raw.ItemType;
            Enabled = raw.Enabled != 0;
            HasBase = raw.HasBase != 0;
            AlwaysActive = raw.AlwaysActive != 0;
            MaxSpawnCount = raw.MaxSpawnCount;
            SpawnInterval = raw.SpawnInterval;
            SpawnDelay = raw.SpawnDelay;
            SomeEntityId = raw.SomeEntityId;
            CollectedMessage = raw.CollectedMessage;
            CollectedMsgParam1 = raw.CollectedMsgParam1;
            CollectedMsgParam2 = raw.CollectedMsgParam2;
        }
    }

    public class FhItemSpawnEntityEditor : EntityEditorBase
    {
        public FhItemType ItemType { get; set; }
        public ushort SpawnLimit { get; set; }
        public ushort CooldownTime { get; set; }
        public ushort Field2C { get; set; }

        public FhItemSpawnEntityEditor(Entity header, FhItemSpawnEntityData raw) : base(header)
        {
            ItemType = raw.ItemType;
            SpawnLimit = raw.SpawnLimit;
            CooldownTime = raw.CooldownTime;
            Field2C = raw.Field2C;
        }
    }

    public class EnemySpawnEntityEditor : EntityEditorBase
    {
        public EnemyType EnemyType { get; set; }
        public uint Subtype { get; set; }
        public uint TextureId { get; set; }
        public uint HunterWeapon { get; set; }
        public ushort Health { get; set; }
        public ushort HealthMax { get; set; }
        public ushort Field38 { get; set; }
        public byte Field3A { get; set; }
        public byte Field3B { get; set; }
        public uint Field3C { get; set; }
        public uint Field40 { get; set; }
        public uint Field44 { get; set; }
        public uint Field48 { get; set; }
        public uint Field4C { get; set; }
        public uint Field50 { get; set; }
        public uint Field54 { get; set; }
        public uint Field58 { get; set; }
        public uint Field5C { get; set; }
        public uint Field60 { get; set; }
        public uint Field64 { get; set; }
        public uint Field68 { get; set; }
        public uint Field6C { get; set; }
        public uint Field70 { get; set; }
        public uint Field74 { get; set; }
        public uint Field78 { get; set; }
        public uint Field7C { get; set; }
        public uint Field80 { get; set; }
        public uint Field84 { get; set; }
        public uint Field88 { get; set; }
        public uint Field8C { get; set; }
        public uint Field90 { get; set; }
        public uint Field94 { get; set; }
        public uint Field98 { get; set; }
        public uint Field9C { get; set; }
        public uint FieldA0 { get; set; }
        public uint FieldA4 { get; set; }
        public uint FieldA8 { get; set; }
        public uint FieldAC { get; set; }
        public uint FieldB0 { get; set; }
        public uint FieldB4 { get; set; }
        public uint FieldB8 { get; set; }
        public uint FieldBC { get; set; }
        public uint FieldC0 { get; set; }
        public uint FieldC4 { get; set; }
        public uint FieldC8 { get; set; }
        public uint FieldCC { get; set; }
        public uint FieldD0 { get; set; }
        public uint FieldD4 { get; set; }
        public uint FieldD8 { get; set; }
        public uint FieldDC { get; set; }
        public uint FieldE0 { get; set; }
        public uint FieldE4 { get; set; }
        public uint FieldE8 { get; set; }
        public uint FieldEC { get; set; }
        public uint FieldF0 { get; set; }
        public uint FieldF4 { get; set; }
        public uint FieldF8 { get; set; }
        public uint FieldFC { get; set; }
        public uint Field100 { get; set; }
        public uint Field104 { get; set; }
        public uint Field108 { get; set; }
        public uint Field10C { get; set; }
        public uint Field110 { get; set; }
        public uint Field114 { get; set; }
        public uint Field118 { get; set; }
        public uint Field11C { get; set; }
        public uint Field120 { get; set; }
        public uint Field124 { get; set; }
        public uint Field128 { get; set; }
        public uint Field12C { get; set; }
        public uint Field130 { get; set; }
        public uint Field134 { get; set; }
        public uint Field138 { get; set; }
        public uint Field13C { get; set; }
        public uint Field140 { get; set; }
        public uint Field144 { get; set; }
        public uint Field148 { get; set; }
        public uint Field14C { get; set; }
        public uint Field150 { get; set; }
        public uint Field154 { get; set; }
        public uint Field158 { get; set; }
        public uint Field15C { get; set; }
        public uint Field160 { get; set; }
        public uint Field164 { get; set; }
        public uint Field168 { get; set; }
        public uint Field16C { get; set; }
        public uint Field170 { get; set; }
        public uint Field174 { get; set; }
        public uint Field178 { get; set; }
        public uint Field17C { get; set; }
        public uint Field180 { get; set; }
        public uint Field184 { get; set; }
        public uint Field188 { get; set; }
        public uint Field18C { get; set; }
        public uint Field190 { get; set; }
        public uint Field194 { get; set; }
        public uint Field198 { get; set; }
        public uint Field19C { get; set; }
        public uint Field1A0 { get; set; }
        public uint Field1A4 { get; set; }
        public uint Field1A8 { get; set; }
        public uint Field1AC { get; set; }
        public uint Field1B0 { get; set; }
        public uint Field1B4 { get; set; }
        public ushort Field1B8 { get; set; }
        public byte SomeLimit { get; set; }
        public byte Field1BB { get; set; }
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
        public ushort Field1E2 { get; set; }
        public Message Message1 { get; set; }
        public short EntityId2 { get; set; }
        public ushort Field1EA { get; set; }
        public Message Message2 { get; set; }
        public short EntityId3 { get; set; }
        public ushort Field1F2 { get; set; }
        public Message Message3 { get; set; }
        public uint ItemModel { get; set; }

        public EnemySpawnEntityEditor(Entity header, EnemySpawnEntityData raw) : base(header)
        {
            EnemyType = raw.Type;
            Subtype = raw.Subtype;
            TextureId = raw.TextureId;
            HunterWeapon = raw.HunterWeapon;
            Health = raw.Health;
            HealthMax = raw.HealthMax;
            Field38 = raw.Field38;
            Field3A = raw.Field3A;
            Field3B = raw.Field3B;
            Field3C = raw.Field3C;
            Field40 = raw.Field40;
            Field44 = raw.Field44;
            Field48 = raw.Field48;
            Field4C = raw.Field4C;
            Field50 = raw.Field50;
            Field54 = raw.Field54;
            Field58 = raw.Field58;
            Field5C = raw.Field5C;
            Field60 = raw.Field60;
            Field64 = raw.Field64;
            Field68 = raw.Field68;
            Field6C = raw.Field6C;
            Field70 = raw.Field70;
            Field74 = raw.Field74;
            Field78 = raw.Field78;
            Field7C = raw.Field7C;
            Field80 = raw.Field80;
            Field84 = raw.Field84;
            Field88 = raw.Field88;
            Field8C = raw.Field8C;
            Field90 = raw.Field90;
            Field94 = raw.Field94;
            Field98 = raw.Field98;
            Field9C = raw.Field9C;
            FieldA0 = raw.FieldA0;
            FieldA4 = raw.FieldA4;
            FieldA8 = raw.FieldA8;
            FieldAC = raw.FieldAC;
            FieldB0 = raw.FieldB0;
            FieldB4 = raw.FieldB4;
            FieldB8 = raw.FieldB8;
            FieldBC = raw.FieldBC;
            FieldC0 = raw.FieldC0;
            FieldC4 = raw.FieldC4;
            FieldC8 = raw.FieldC8;
            FieldCC = raw.FieldCC;
            FieldD0 = raw.FieldD0;
            FieldD4 = raw.FieldD4;
            FieldD8 = raw.FieldD8;
            FieldDC = raw.FieldDC;
            FieldE0 = raw.FieldE0;
            FieldE4 = raw.FieldE4;
            FieldE8 = raw.FieldE8;
            FieldEC = raw.FieldEC;
            FieldF0 = raw.FieldF0;
            FieldF4 = raw.FieldF4;
            FieldF8 = raw.FieldF8;
            FieldFC = raw.FieldFC;
            Field100 = raw.Field100;
            Field104 = raw.Field104;
            Field108 = raw.Field108;
            Field10C = raw.Field10C;
            Field110 = raw.Field110;
            Field114 = raw.Field114;
            Field118 = raw.Field118;
            Field11C = raw.Field11C;
            Field120 = raw.Field120;
            Field124 = raw.Field124;
            Field128 = raw.Field128;
            Field12C = raw.Field12C;
            Field130 = raw.Field130;
            Field134 = raw.Field134;
            Field138 = raw.Field138;
            Field13C = raw.Field13C;
            Field140 = raw.Field140;
            Field144 = raw.Field144;
            Field148 = raw.Field148;
            Field14C = raw.Field14C;
            Field150 = raw.Field150;
            Field154 = raw.Field154;
            Field158 = raw.Field158;
            Field15C = raw.Field15C;
            Field160 = raw.Field160;
            Field164 = raw.Field164;
            Field168 = raw.Field168;
            Field16C = raw.Field16C;
            Field170 = raw.Field170;
            Field174 = raw.Field174;
            Field178 = raw.Field178;
            Field17C = raw.Field17C;
            Field180 = raw.Field180;
            Field184 = raw.Field184;
            Field188 = raw.Field188;
            Field18C = raw.Field18C;
            Field190 = raw.Field190;
            Field194 = raw.Field194;
            Field198 = raw.Field198;
            Field19C = raw.Field19C;
            Field1A0 = raw.Field1A0;
            Field1A4 = raw.Field1A4;
            Field1A8 = raw.Field1A8;
            Field1AC = raw.Field1AC;
            Field1B0 = raw.Field1B0;
            Field1B4 = raw.Field1B4;
            Field1B8 = raw.Field1B8;
            SomeLimit = raw.SomeLimit;
            Field1BB = raw.Field1BB;
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
            Field1E2 = raw.Field1E2;
            Message1 = raw.Message1;
            EntityId2 = raw.EntityId2;
            Field1EA = raw.Field1EA;
            Message2 = raw.Message2;
            EntityId3 = raw.EntityId3;
            Field1F2 = raw.Field1F2;
            Message3 = raw.Message3;
            ItemModel = raw.ItemModel;
        }
    }

    public class FhEnemySpawnEntityEditor : EntityEditorBase
    {
        public uint Field24 { get; set; }
        public uint Field28 { get; set; }
        public uint Field2C { get; set; }
        public uint Field30 { get; set; }
        public uint Field34 { get; set; }
        public uint Field38 { get; set; }
        public uint Field3C { get; set; }
        public uint Field40 { get; set; }
        public uint Field44 { get; set; }
        public uint Field48 { get; set; }
        public uint Field4C { get; set; }
        public uint Field50 { get; set; }
        public uint Field54 { get; set; }
        public uint Field58 { get; set; }
        public uint Field5C { get; set; }
        public uint Field60 { get; set; }
        public uint Field64 { get; set; }
        public uint Field68 { get; set; }
        public uint Field6C { get; set; }
        public uint Field70 { get; set; }
        public uint Field74 { get; set; }
        public uint Field78 { get; set; }
        public uint Field7C { get; set; }
        public uint Field80 { get; set; }
        public uint Field84 { get; set; }
        public uint Field88 { get; set; }
        public uint Field8C { get; set; }
        public uint Field90 { get; set; }
        public uint Field94 { get; set; }
        public uint Field98 { get; set; }
        public uint Field9C { get; set; }
        public uint FieldA0 { get; set; }
        public uint FieldA4 { get; set; }
        public uint FieldA8 { get; set; }
        public uint FieldAC { get; set; }
        public uint FieldB0 { get; set; }
        public uint FieldB4 { get; set; }
        public uint FieldB8 { get; set; }
        public uint FieldBC { get; set; }
        public uint FieldC0 { get; set; }
        public uint FieldC4 { get; set; }
        public uint FieldC8 { get; set; }
        public uint FieldCC { get; set; }
        public uint FieldD0 { get; set; }
        public uint FieldD4 { get; set; }
        public uint FieldD8 { get; set; }
        public uint FieldDC { get; set; }
        public uint FieldE0 { get; set; }
        public uint EnemyType { get; set; }
        public byte SpawnTotal { get; set; }
        public byte SpawnLimit { get; set; }
        public byte SpawnCount { get; set; }
        public byte FieldEB { get; set; }
        public ushort Cooldown { get; set; }
        public ushort FieldEE { get; set; }
        public string SpawnNodeName { get; set; } = "";
        public short ParentId { get; set; }
        public ushort Field102 { get; set; }
        public FhMessage EmptyMessage { get; set; }

        public FhEnemySpawnEntityEditor(Entity header, FhEnemySpawnEntityData raw) : base(header)
        {
            Field24 = raw.Field24;
            Field28 = raw.Field28;
            Field2C = raw.Field2C;
            Field30 = raw.Field30;
            Field34 = raw.Field34;
            Field38 = raw.Field38;
            Field3C = raw.Field3C;
            Field40 = raw.Field40;
            Field44 = raw.Field44;
            Field48 = raw.Field48;
            Field4C = raw.Field4C;
            Field50 = raw.Field50;
            Field54 = raw.Field54;
            Field58 = raw.Field58;
            Field5C = raw.Field5C;
            Field60 = raw.Field60;
            Field64 = raw.Field64;
            Field68 = raw.Field68;
            Field6C = raw.Field6C;
            Field70 = raw.Field70;
            Field74 = raw.Field74;
            Field78 = raw.Field78;
            Field7C = raw.Field7C;
            Field80 = raw.Field80;
            Field84 = raw.Field84;
            Field88 = raw.Field88;
            Field8C = raw.Field8C;
            Field90 = raw.Field90;
            Field94 = raw.Field94;
            Field98 = raw.Field98;
            Field9C = raw.Field9C;
            FieldA0 = raw.FieldA0;
            FieldA4 = raw.FieldA4;
            FieldA8 = raw.FieldA8;
            FieldAC = raw.FieldAC;
            FieldB0 = raw.FieldB0;
            FieldB4 = raw.FieldB4;
            FieldB8 = raw.FieldB8;
            FieldBC = raw.FieldBC;
            FieldC0 = raw.FieldC0;
            FieldC4 = raw.FieldC4;
            FieldC8 = raw.FieldC8;
            FieldCC = raw.FieldCC;
            FieldD0 = raw.FieldD0;
            FieldD4 = raw.FieldD4;
            FieldD8 = raw.FieldD8;
            FieldDC = raw.FieldDC;
            FieldE0 = raw.FieldE0;
            EnemyType = raw.EnemyType;
            SpawnTotal = raw.SpawnTotal;
            SpawnLimit = raw.SpawnLimit;
            SpawnCount = raw.SpawnCount;
            FieldEB = raw.FieldEB;
            Cooldown = raw.Cooldown;
            FieldEE = raw.FieldEE;
            SpawnNodeName = raw.NodeName.MarshalString();
            ParentId = raw.ParentId;
            Field102 = raw.Field102;
            EmptyMessage = raw.EmptyMessage;
        }
    }

    public class TriggerVolumeEntityEditor : EntityEditorBase
    {
        public TriggerType Subtype { get; set; }
        public CollisionVolume Volume { get; set; }
        public bool Active { get; set; }
        public bool AlwaysActive { get; set; } // set flags bit 0 based on Active boolean only and ignore room state
        public bool DeactivateAfterUse { get; set; } // set flags bit 1
        public ushort RepeatDelay { get; set; }
        public ushort CheckDelay { get; set; }
        public ushort RequiredStateBit { get; set; } // for subtype 4
        public ushort TriggerFlags { get; set; } // in-game this is treated as uint, but the extra bits are never set/checked
        public uint TriggerThreshold { get; set; } // for subtype 1
        public short ParentId { get; set; }
        public Message ParentMessage { get; set; }
        public uint ParentMsgParam1 { get; set; }
        public uint ParentMsgParam2 { get; set; }
        public short ChildId { get; set; }
        public Message ChildMessage { get; set; }
        public uint ChildMsgParam1 { get; set; }
        public uint ChildMsgParam2 { get; set; }

        public TriggerVolumeEntityEditor(Entity header, TriggerVolumeEntityData raw) : base(header)
        {
            Subtype = raw.Subtype;
            Volume = new CollisionVolume(raw.Volume);
            Active = raw.Active != 0;
            AlwaysActive = raw.AlwaysActive != 0;
            DeactivateAfterUse = raw.DeactivateAfterUse != 0;
            RepeatDelay = raw.RepeatDelay;
            CheckDelay = raw.CheckDelay;
            RequiredStateBit = raw.RequiredStateBit;
            TriggerFlags = raw.TriggerFlags;
            TriggerThreshold = raw.TriggerThreshold;
            ParentId = raw.ParentId;
            ParentMessage = raw.ParentMessage;
            ParentMsgParam1 = raw.ParentMsgParam1;
            ParentMsgParam2 = raw.ParentMsgParam2;
            ChildId = raw.ChildId;
            ChildMessage = raw.ChildMessage;
            ChildMsgParam1 = raw.ChildMsgParam1;
            ChildMsgParam2 = raw.ChildMsgParam2;
        }
    }

    public class FhTriggerVolumeEntityEditor : EntityEditorBase
    {
        public FhTriggerType Subtype { get; set; } // 0/1/2 - sphere/box/cylinder, 3 - threshold
        public CollisionVolume Box { get; set; }
        public CollisionVolume Sphere { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public ushort OneUse { get; set; }
        public ushort Cooldown { get; set; }
        public uint Flags { get; set; }
        public uint Threshold { get; set; }
        public short ParentId { get; set; }
        public FhMessage ParentMessage { get; set; }
        public uint ParentMsgParam1 { get; set; }
        public short ChildId { get; set; }
        public FhMessage ChildMessage { get; set; }
        public uint ChildMsgParam1 { get; set; }

        public FhTriggerVolumeEntityEditor(Entity header, FhTriggerVolumeEntityData raw) : base(header)
        {
            Subtype = raw.Subtype;
            Box = new CollisionVolume(raw.Box);
            Sphere = new CollisionVolume(raw.Sphere);
            Cylinder = new CollisionVolume(raw.Cylinder);
            OneUse = raw.OneUse;
            Cooldown = raw.Cooldown;
            Flags = raw.Flags;
            Threshold = raw.Threshold;
            ParentId = raw.ParentId;
            ParentMessage = raw.ParentMessage;
            ParentMsgParam1 = raw.ParentMsgParam1;
            ChildId = raw.ChildId;
            ChildMessage = raw.ChildMessage;
            ChildMsgParam1 = raw.ChildMsgParam1;
        }
    }

    public class AreaVolumeEntityEditor : EntityEditorBase
    {
        public CollisionVolume Volume { get; set; }
        public bool Active { get; set; } // in 1P, may be controlled by room state bits
        public bool AlwaysActive { get; set; } // ignore 1P state bits
        public bool AllowMultiple { get; set; }
        public byte MessageDelay { get; set; } // always 0 or 1
        public ushort Unused6A { get; set; } // always 0 or 1
        public Message InsideMessage { get; set; }
        public uint InsideMsgParam1 { get; set; } // seconds for escape sequence, gravity/jump assist values, etc.
        public uint InsideMsgParam2 { get; set; } // always 0 except for type 15, where it's always 2
        public short ParentId { get; set; }
        public Message ExitMessage { get; set; }
        public uint ExitMsgParam1 { get; set; } // always 0
        public uint ExitMsgParam2 { get; set; } // always 0
        public short ChildId { get; set; } // always the same as ParentId
        public ushort Cooldown { get; set; }
        public uint Priority { get; set; } // always 0 or 1
        public uint Flags { get; set; } // 0x200 = affects biped, 0x400 = affects alt

        public AreaVolumeEntityEditor(Entity header, AreaVolumeEntityData raw) : base(header)
        {
            Volume = new CollisionVolume(raw.Volume);
            Active = raw.Active != 0;
            AlwaysActive = raw.AlwaysActive != 0;
            AllowMultiple = raw.AllowMultiple != 0;
            MessageDelay = raw.MessageDelay;
            Unused6A = raw.Unused6A;
            InsideMessage = raw.InsideMessage;
            InsideMsgParam1 = raw.InsideMsgParam1;
            InsideMsgParam2 = raw.InsideMsgParam2;
            ParentId = raw.ParentId;
            ExitMessage = raw.ExitMessage;
            ExitMsgParam1 = raw.ExitMsgParam1;
            ExitMsgParam2 = raw.ExitMsgParam2;
            ChildId = raw.ChildId;
            Cooldown = raw.Cooldown;
            Priority = raw.Priority;
            Flags = raw.Flags;
        }
    }

    public class FhAreaVolumeEntityEditor : EntityEditorBase
    {
        public FhTriggerType Subtype { get; set; } // 0/1 - sphere/box
        public CollisionVolume Box { get; set; }
        public CollisionVolume Sphere { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public FhMessage InsideMessage { get; set; }
        public uint InsideMsgParam1 { get; set; }
        public FhMessage ExitMessage { get; set; }
        public uint ExitMsgParam1 { get; set; }
        public ushort Cooldown { get; set; }
        public uint Flags { get; set; }

        public FhAreaVolumeEntityEditor(Entity header, FhAreaVolumeEntityData raw) : base(header)
        {
            Subtype = raw.Subtype;
            Box = new CollisionVolume(raw.Box);
            Sphere = new CollisionVolume(raw.Sphere);
            Cylinder = new CollisionVolume(raw.Cylinder);
            InsideMessage = raw.InsideMessage;
            InsideMsgParam1 = raw.InsideMsgParam1;
            ExitMessage = raw.ExitMessage;
            ExitMsgParam1 = raw.ExitMsgParam1;
            Cooldown = raw.Cooldown;
            Flags = raw.Flags;
        }
    }

    public class JumpPadEntityEditor : EntityEditorBase
    {
        public uint ParentId { get; set; }
        public uint Unused28 { get; set; } // usually 0, occasionally 2
        public CollisionVolume Volume { get; set; }
        public Vector3 BeamVector { get; set; }
        public float Speed { get; set; }
        public ushort ControlLockTime { get; set; }
        public ushort CooldownTime { get; set; }
        public bool Active { get; set; }
        public uint ModelId { get; set; }
        public uint Flags { get; set; }

        public JumpPadEntityEditor(Entity header, JumpPadEntityData raw) : base(header)
        {
            ParentId = raw.ParentId;
            Unused28 = raw.Unused28;
            Volume = new CollisionVolume(raw.Volume);
            BeamVector = raw.BeamVector.ToFloatVector();
            Speed = raw.Speed.FloatValue;
            ControlLockTime = raw.ControlLockTime;
            CooldownTime = raw.CooldownTime;
            Active = raw.Active != 0;
            ModelId = raw.ModelId;
            Flags = raw.Flags;
        }
    }

    public class FhJumpPadEntityEditor : EntityEditorBase
    {
        public FhTriggerType VolumeType { get; set; }
        public CollisionVolume Box { get; set; }
        public CollisionVolume Sphere { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public uint CooldownTime { get; set; }
        public Vector3 BeamVector { get; set; }
        public float Speed { get; set; }
        public uint FieldFC { get; set; }
        public uint ModelId { get; set; }
        public uint BeamType { get; set; }
        public uint Flags { get; set; }

        public FhJumpPadEntityEditor(Entity header, FhJumpPadEntityData raw) : base(header)
        {
            VolumeType = raw.VolumeType;
            Box = new CollisionVolume(raw.Box);
            Cylinder = new CollisionVolume(raw.Cylinder);
            Sphere = new CollisionVolume(raw.Sphere);
            CooldownTime = raw.CooldownTime;
            BeamVector = raw.BeamVector.ToFloatVector();
            Speed = raw.Speed.FloatValue;
            FieldFC = raw.FieldFC;
            ModelId = raw.ModelId;
            BeamType = raw.BeamType;
            Flags = raw.Flags;
        }
    }

    public class PointModuleEntityEditor : EntityEditorBase
    {
        public short NextId { get; set; }
        public short PrevId { get; set; }
        public bool Active { get; set; }

        public PointModuleEntityEditor(Entity header, PointModuleEntityData raw) : base(header)
        {
            NextId = raw.NextId;
            PrevId = raw.PrevId;
            Active = raw.Active != 0;
        }
    }

    public class MorphCameraEntityEditor : EntityEditorBase
    {
        public CollisionVolume Volume { get; set; }

        public MorphCameraEntityEditor(Entity header, MorphCameraEntityData raw) : base(header)
        {
            Volume = new CollisionVolume(raw.Volume);
        }

        public MorphCameraEntityEditor(Entity header, FhMorphCameraEntityData raw) : base(header)
        {
            Volume = new CollisionVolume(raw.Volume);
        }
    }

    public class OctolithFlagEntityEditor : EntityEditorBase
    {
        public byte TeamId { get; set; }

        public OctolithFlagEntityEditor(Entity header, OctolithFlagEntityData raw) : base(header)
        {
            TeamId = raw.TeamId;
        }
    }

    public class FlagBaseEntityEditor : EntityEditorBase
    {
        public uint TeamId { get; set; }
        public CollisionVolume Volume { get; set; }

        public FlagBaseEntityEditor(Entity header, FlagBaseEntityData raw) : base(header)
        {
            TeamId = raw.TeamId;
            Volume = new CollisionVolume(raw.Volume);
        }
    }

    public class TeleporterEntityEditor : EntityEditorBase
    {
        public byte Field24 { get; set; }
        public byte Field25 { get; set; }
        public byte ArtifactId { get; set; }
        public bool Active { get; set; }
        public bool Invisible { get; set; }
        public string TargetRoom { get; set; } = "";
        public Vector3 TargetPosition { get; set; }
        public string TeleporterNodeName { get; set; } = "";

        public TeleporterEntityEditor(Entity header, TeleporterEntityData raw) : base(header)
        {
            Field24 = raw.Field24;
            Field25 = raw.Field25;
            ArtifactId = raw.ArtifactId;
            Active = raw.Active != 0;
            Invisible = raw.Invisible != 0;
            TargetRoom = raw.TargetRoom.MarshalString();
            TargetPosition = raw.TargetPosition.ToFloatVector();
            TeleporterNodeName = raw.NodeName.MarshalString();
        }
    }

    public class NodeDefenseEntityEditor : EntityEditorBase
    {
        public CollisionVolume Volume { get; set; }

        public NodeDefenseEntityEditor(Entity header, NodeDefenseEntityData raw) : base(header)
        {
            Volume = new CollisionVolume(raw.Volume);
        }
    }

    public class LightSourceEntityEditor : EntityEditorBase
    {
        public CollisionVolume Volume { get; set; }
        public bool Light1Enabled { get; set; }
        public ColorRgb Light1Color { get; set; } // 8-bit color values
        public Vector3 Light1Vector { get; set; }
        public bool Light2Enabled { get; set; }
        public ColorRgb Light2Color { get; set; }
        public Vector3 Light2Vector { get; set; }

        public LightSourceEntityEditor(Entity header, LightSourceEntityData raw) : base(header)
        {
            Volume = new CollisionVolume(raw.Volume);
            Light1Enabled = raw.Light1Enabled != 0;
            Light1Color = raw.Light1Color;
            Light1Vector = raw.Light1Vector.ToFloatVector();
            Light2Enabled = raw.Light2Enabled != 0;
            Light2Color = raw.Light2Color;
            Light2Vector = raw.Light2Vector.ToFloatVector();
        }
    }

    public class ArtifactEntityEditor : EntityEditorBase
    {
        public byte ModelId { get; set; }
        public byte ArtifactId { get; set; }
        public bool Active { get; set; }
        public bool HasBase { get; set; }
        public short Message1Target { get; set; }
        public uint Message1Id { get; set; }
        public short Message2Target { get; set; }
        public uint Message2Id { get; set; }
        public short Message3Target { get; set; }
        public uint Message3Id { get; set; }
        public short LinkedEntityId { get; set; } // always -1

        public ArtifactEntityEditor(Entity header, ArtifactEntityData raw) : base(header)
        {
            ModelId = raw.ModelId;
            ArtifactId = raw.ArtifactId;
            Active = raw.Active != 0;
            HasBase = raw.HasBase != 0;
            Message1Target = raw.Message1Target;
            Message1Id = raw.Message1Id;
            Message2Target = raw.Message2Target;
            Message2Id = raw.Message2Id;
            Message3Target = raw.Message3Target;
            Message3Id = raw.Message3Id;
            LinkedEntityId = raw.LinkedEntityId;
        }
    }

    public class CameraSequenceEntityEditor : EntityEditorBase
    {
        public byte SequenceId { get; set; }
        public byte Field25 { get; set; }
        public bool Loop { get; set; }
        public byte Field27 { get; set; }
        public byte Field28 { get; set; }
        public byte Field29 { get; set; }
        public ushort DelayFrames { get; set; }
        public byte PlayerId1 { get; set; }
        public byte PlayerId2 { get; set; }
        public short Entity1 { get; set; }
        public short Entity2 { get; set; }
        public short MessageTargetId { get; set; }
        public Message Message { get; set; }
        public uint MessageParam { get; set; }

        public CameraSequenceEntityEditor(Entity header, CameraSequenceEntityData raw) : base(header)
        {
            SequenceId = raw.SequenceId;
            Field25 = raw.Field25;
            Loop = raw.Loop != 0;
            Field27 = raw.Field27;
            Field28 = raw.Field28;
            Field29 = raw.Field29;
            DelayFrames = raw.DelayFrames;
            PlayerId1 = raw.PlayerId1;
            PlayerId2 = raw.PlayerId2;
            Entity1 = raw.Entity1;
            Entity2 = raw.Entity2;
            MessageTargetId = raw.MessageTargetId;
            Message = raw.Message;
            MessageParam = raw.MessageParam;
        }
    }

    public class ForceFieldEntityEditor : EntityEditorBase
    {
        public uint ForceFieldType { get; set; } // 0-8 beam lock, 9 no lock
        public float Width { get; set; }
        public float Height { get; set; }
        public bool Active { get; set; }

        public ForceFieldEntityEditor(Entity header, ForceFieldEntityData raw) : base(header)
        {
            ForceFieldType = raw.Type;
            Width = raw.Width.FloatValue;
            Height = raw.Height.FloatValue;
            Active = raw.Active != 0;
        }
    }
}
