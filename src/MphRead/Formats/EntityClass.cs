using System.Collections.Generic;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Editor
{
    // todo:
    // - message enum
    public abstract class EntityDataBase
    {
        public string NodeName { get; set; } = "";
        public ushort LayerMask { get; set; }
        public EntityType Type { get; set; }
        public ushort Id { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Up { get; set; }
        public Vector3 Facing { get; set; }
    }

    public class PlatformEntityData : EntityDataBase
    {
        public EntityDataHeader Header { get; set; }
        public bool NoPort { get; set; }
        public uint ModelId { get; set; }
        public ushort ParentId { get; set; }
        public byte Field2E { get; set; }
        public byte Field2F { get; set; }
        public ushort ScanData1 { get; set; }
        public ushort ScanEventTarget { get; set; }
        public uint ScanEventId { get; set; }
        public ushort ScanData2 { get; set; }
        public ushort Field3A { get; set; }
        public List<Vector3> Positions { get; set; } = new List<Vector3>();
        public List<Vector4> Rotations { get; set; } = new List<Vector4>();
        public Vector3Fx PositionOffset { get; set; }
        public uint Field160 { get; set; }
        public uint Field164 { get; set; }
        public string PortalName { get; set; } = "";
        public uint Field178 { get; set; }
        public uint Field17C { get; set; }
        public uint Field180 { get; set; }
        public PlatformFlags Flags { get; set; }
        public uint ContactDamage { get; set; }
        public Vector3Fx BeamSpawnDir { get; set; }
        public Vector3Fx BeamSpawnPos { get; set; }
        public int BeamId { get; set; }
        public uint BeamInterval { get; set; }
        public uint BeamOnIntervals { get; set; } // 16 bits are used
        public uint Unused1B0 { get; set; } // always UInt16.MaxValue
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
        public uint Message1Id { get; set; }
        public uint Message1Param1 { get; set; }
        public uint Message1Param2 { get; set; }
        public uint Message2Target { get; set; }
        public uint Message2Id { get; set; }
        public uint Message2Param1 { get; set; }
        public uint Message2Param2 { get; set; }
        public uint Message3Target { get; set; }
        public uint Message3Id { get; set; }
        public uint Message3Param1 { get; set; }
        public uint Message3Param2 { get; set; }
        public ushort Field208 { get; set; }
        public ushort Msg32Target1 { get; set; }
        public uint Msg32Message1 { get; set; }
        public uint Msg32Param11 { get; set; }
        public uint Msg32Param21 { get; set; }
        public ushort Field218 { get; set; }
        public ushort Msg32Target2 { get; set; }
        public uint Msg32Message2 { get; set; }
        public uint Msg32Param12 { get; set; }
        public uint Msg32Param22 { get; set; }
        public ushort Field228 { get; set; }
        public ushort Msg32Target3 { get; set; }
        public uint Msg32Message3 { get; set; }
        public uint Msg32Param13 { get; set; }
        public uint Msg32Param23 { get; set; }
        public ushort Field238 { get; set; }
        public ushort Msg32Target4 { get; set; }
        public uint Msg32Message4 { get; set; }
        public uint Msg32Param14 { get; set; }
        public uint Msg32Param24 { get; set; }
    }

    public class FhPlatformEntityData : EntityDataBase
    {
        public bool NoPortal { get; set; }
        public uint Field28 { get; set; }
        public uint Field2C { get; set; }
        public byte Field30 { get; set; }
        public byte Field31 { get; set; }
        public CollisionVolume Volume { get; set; } // unused
        public List<Vector3> Vectors { get; set; } = new List<Vector3>();
        public uint FieldD4 { get; set; }
        public string PortalName { get; set; } = "";
    }

    public class ObjectEntityData : EntityDataBase
    {
        public byte Flags { get; set; }
        public uint EffectFlags { get; set; }
        public uint ModelId { get; set; }
        public ushort LinkedEntity { get; set; }
        public ushort ScanId { get; set; }
        public ushort ScanEventTargetId { get; set; }
        public uint ScanEventId { get; set; }
        public uint EffectId { get; set; }
        public uint EffectInterval { get; set; }
        public uint EffectOnIntervals { get; set; } // 16 bits are used
        public Vector3 EffectPositionOffset { get; set; } // maximum value for random offset
        public CollisionVolume Volume { get; set; }
    }

    public class PlayerSpawnEntityData : EntityDataBase
    {
        public bool Initial { get; set; } // whether this is available to spawn at when frame count is 0
        public bool Active { get; set; }
        public sbyte TeamIndex { get; set; } // 0, 1, or -1
    }

    public class DoorEntityData : EntityDataBase
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
    }

    public class FhDoorEntityData : EntityDataBase
    {
        public string RoomName { get; set; } = "";
        public uint Flags { get; set; }
        public uint ModelId { get; set; }
    }

    public class ItemSpawnEntityData : EntityDataBase
    {
        public uint ParentId { get; set; }
        public ItemType ItemType { get; set; }
        public bool Enabled { get; set; }
        public bool HasBase { get; set; }
        public bool AlwaysActive { get; set; } // set flags bit 0 based on Active boolean only and ignore room state
        public ushort MaxSpawnCount { get; set; }
        public ushort SpawnInterval { get; set; }
        public ushort SpawnDelay { get; set; }
        public ushort SomeEntityId { get; set; } // todo: parent? child?
        public uint CollectedMessageId { get; set; }
        public uint CollectedMessageParam1 { get; set; }
        public uint CollectedMessageParam2 { get; set; }
    }

    public class FhItemSpawnEntityData : EntityDataBase
    {
        public FhItemType ItemType { get; set; }
        public ushort SpawnLimit { get; set; }
        public ushort CooldownTime { get; set; }
        public ushort Field2C { get; set; }
    }

    public class EnemySpawnEntityData : EntityDataBase
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
        public CollisionVolume Volume { get; set; }
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
        public byte Active { get; set; }
        public byte AlwaysActive { get; set; }
        public byte ItemChance { get; set; }
        public ushort SpawnerModel { get; set; }
        public ushort CooldownTime { get; set; }
        public ushort InitialCooldown { get; set; }
        public uint ActiveDistance { get; set; } // todo: display sphere
        public uint Field1CC { get; set; }
        public string SpawnNodeName { get; set; } = "";
        public ushort EntityId1 { get; set; }
        public ushort Field1E2 { get; set; }
        public uint MessageId1 { get; set; }
        public ushort EntityId2 { get; set; }
        public ushort Field1EA { get; set; }
        public uint MessageId2 { get; set; }
        public ushort EntityId3 { get; set; }
        public ushort Field1F2 { get; set; }
        public uint MessageId3 { get; set; }
        public uint ItemModel { get; set; }
    }

    public class FhEnemySpawnEntityData : EntityDataBase
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
        public byte FieldE8 { get; set; }
        public byte SpawnLimit { get; set; }
        public byte SpawnCount { get; set; }
        public byte FieldEB { get; set; }
        public ushort Cooldown { get; set; }
        public ushort FieldEE { get; set; }
        public string SpawnNodeName { get; set; } = "";
        public ushort ParentId { get; set; }
        public ushort Field102 { get; set; }
        public uint Field104 { get; set; }
    }

    public class TriggerVolumeEntityData : EntityDataBase
    {
        public TriggerType Subtype { get; set; }
        public CollisionVolume Volume { get; set; }
        public ushort Unused68 { get; set; } // always UInt16.MaxValue
        public bool Active { get; set; }
        public bool AlwaysActive { get; set; } // set flags bit 0 based on Active boolean only and ignore room state
        public bool DeactivateAfterUse { get; set; } // set flags bit 1
        public ushort RepeatDelay { get; set; }
        public ushort CheckDelay { get; set; }
        public ushort RequiredStateBit { get; set; } // for subtype 4
        public ushort TriggerFlags { get; set; } // in-game this is treated as uint, but the extra bits are never set/checked
        public uint TriggerThreshold { get; set; } // for subtype 1
        public ushort ParentId { get; set; }
        public Message ParentEvent { get; set; }
        public uint ParentEventParam1 { get; set; }
        public uint ParentEventParam2 { get; set; }
        public ushort ChildId { get; set; }
        public Message ChildEvent { get; set; }
        public uint ChildEventParam1 { get; set; }
        public uint ChildEventParam2 { get; set; }
    }

    public class FhTriggerVolumeEntityData : EntityDataBase
    {
        public FhTriggerType Subtype { get; set; } // 0/1/2 - sphere/box/cylinder, 3 - threshold
        public CollisionVolume Box { get; set; }
        public CollisionVolume Sphere { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public ushort OneUse { get; set; }
        public ushort Cooldown { get; set; }
        public uint Flags { get; set; }
        public uint Threshold { get; set; }
        public ushort ParentId { get; set; }
        public FhMessage ParentEvent { get; set; }
        public uint ParentParam1 { get; set; }
        public ushort ChildId { get; set; }
        public FhMessage ChildEvent { get; set; }
        public uint ChildParam1 { get; set; }
    }

    public class AreaVolumeEntityData : EntityDataBase
    {
        public CollisionVolume Volume { get; set; }
        public ushort Unused64 { get; set; } // always UInt16.MaxValue
        public bool Active { get; set; } // in 1P, may be controlled by room state bits
        public bool AlwaysActive { get; set; } // ignore 1P state bits
        public bool AllowMultiple { get; set; }
        public byte EventDelay { get; set; } // always 0 or 1
        public ushort Unused6A { get; set; } // always 0 or 1
        public Message InsideEvent { get; set; }
        public uint InsideEventParam1 { get; set; } // seconds for escape sequence, gravity/jump assist values, etc.
        public uint InsideEventParam2 { get; set; } // always 0 except for type 15, where it's always 2
        public ushort ParentId { get; set; }
        public Message ExitEvent { get; set; }
        public uint ExitEventParam1 { get; set; } // always 0
        public uint ExitEventParam2 { get; set; } // always 0
        public ushort ChildId { get; set; } // always the same as ParentId
        public ushort Cooldown { get; set; }
        public uint Priority { get; set; } // always 0 or 1
        public uint Flags { get; set; } // 0x200 = affects biped, 0x400 = affects alt
    }

    public class FhAreaVolumeEntityData : EntityDataBase
    {
        public FhTriggerType Subtype { get; set; } // 0/1 - sphere/box
        public CollisionVolume Box { get; set; }
        public CollisionVolume Sphere { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public FhMessage InsideEvent { get; set; }
        public uint InsideParam1 { get; set; }
        public FhMessage ExitEvent { get; set; }
        public uint ExitParam1 { get; set; }
        public ushort Cooldown { get; set; }
        public uint Flags { get; set; }
    }

    public class JumpPadEntityData : EntityDataBase
    {
        public uint ParentId { get; set; }
        public uint Unused28 { get; set; } // usually 0, occasionally 2
        public CollisionVolume Volume { get; set; }
        public Vector3 BeamVector { get; set; }
        public Fixed Speed { get; set; }
        public ushort ControlLockTime { get; set; }
        public ushort CooldownTime { get; set; }
        public bool Active { get; set; }
        public uint ModelId { get; set; }
        public uint BeamType { get; set; }
        public uint Flags { get; set; }
    }

    public class FhJumpPadEntityData : EntityDataBase
    {
        public uint VolumeId { get; set; }
        public CollisionVolume Box { get; set; }
        public CollisionVolume Sphere { get; set; }
        public CollisionVolume Cylinder { get; set; }
        public uint CooldownTime { get; set; }
        public Vector3 BeamVector { get; set; }
        public Fixed Speed { get; set; }
        public uint FieldFC { get; set; }
        public uint ModelId { get; set; }
        public uint BeamType { get; set; }
        public uint Flags { get; set; }
    }

    public class PointModuleEntityData : EntityDataBase
    {
        public ushort NextId { get; set; }
        public ushort PrevId { get; set; }
        public bool Active { get; set; }
    }

    public class MorphCameraEntityData : EntityDataBase
    {
        public CollisionVolume Volume { get; set; }
    }

    public class OctolithFlagEntityData : EntityDataBase
    {
        public byte TeamId { get; set; }
    }

    public class FlagBaseEntityData : EntityDataBase
    {
        public uint TeamId { get; set; }
        public CollisionVolume Volume { get; set; }
    }

    public class TeleporterEntityData : EntityDataBase
    {
        public byte Field24 { get; set; }
        public byte Field25 { get; set; }
        public byte ArtifactId { get; set; }
        public byte Active { get; set; }
        public byte Invisible { get; set; }
        public string TargetRoom { get; set; } = "";
        public ushort Unused38 { get; set; } // always 0
        public ushort Unused3A { get; set; } // always UInt16.MaxValue
        public Vector3 TargetPosition { get; set; }
        public string TeleporterNodeName { get; set; } = "";
    }

    public class NodeDefenseEntityData : EntityDataBase
    {
        public CollisionVolume Volume { get; set; }
    }

    public class LightSourceEntityData : EntityDataBase
    {
        public CollisionVolume Volume { get; set; }
        public bool Light1Enabled { get; set; }
        public ColorRgb Light1Color { get; set; } // 8-bit color values
        public Vector3 Light1Vector { get; set; }
        public bool Light2Enabled { get; set; }
        public ColorRgb Light2Color { get; set; }
        public Vector3 Light2Vector { get; set; }
    }

    public class ArtifactEntityData : EntityDataBase
    {
        public byte ModelId { get; set; }
        public byte ArtifactId { get; set; }
        public byte Active { get; set; }
        public byte HasBase { get; set; }
        public ushort Message1Target { get; set; }
        public uint Message1Id { get; set; }
        public ushort Message2Target { get; set; }
        public uint Message2Id { get; set; }
        public ushort Message3Target { get; set; }
        public uint Message3Id { get; set; }
        public ushort LinkedEntityId { get; set; } // always UInt16.MaxValue
    }

    public class CameraSequenceEntityData : EntityDataBase
    {
        public byte SequenceId { get; set; }
        public byte Field25 { get; set; }
        public byte Loop { get; set; }
        public byte Field27 { get; set; }
        public byte Field28 { get; set; }
        public byte Field29 { get; set; }
        public ushort DelayFrames { get; set; }
        public byte PlayerId1 { get; set; }
        public byte PlayerId2 { get; set; }
        public ushort Entity1 { get; set; }
        public ushort Entity2 { get; set; }
        public ushort MessageTargetId { get; set; }
        public uint MessageId { get; set; }
        public uint MessageParam { get; set; }
    }

    public class ForceFieldEntityData : EntityDataBase
    {
        public uint ForceFieldType { get; set; } // 0-8 beam lock, 9 no lock
        public Fixed Width { get; set; }
        public Fixed Height { get; set; }
        public bool Active { get; set; }
    }
}
