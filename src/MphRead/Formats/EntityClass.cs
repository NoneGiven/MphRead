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

        public EntityEditorBase(EntityType type)
        {
            Type = type;
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
        public bool Active { get; set; }
        public byte Delay { get; set; }
        public ushort ScanData1 { get; set; }
        public short ScanMsgTarget { get; set; }
        public Message ScanMessage { get; set; }
        public ushort ScanData2 { get; set; }
        public ushort PositionCount { get; set; }
        public List<Vector3> Positions { get; set; } = new List<Vector3>();
        public List<Vector4> Rotations { get; set; } = new List<Vector4>();
        public Vector3 PositionOffset { get; set; }
        public float ForwardSpeed { get; set; }
        public float BackwardSpeed { get; set; }
        public string PortalName { get; set; } = "";
        public uint MovementType { get; set; } // always 0
        public bool ForCutscene { get; set; }
        public uint ReverseType { get; set; }
        public PlatformFlags Flags { get; set; }
        public uint ContactDamage { get; set; }
        public Vector3 BeamSpawnDir { get; set; }
        public Vector3 BeamSpawnPos { get; set; }
        public int BeamId { get; set; }
        public uint BeamInterval { get; set; }
        public uint BeamOnIntervals { get; set; } // 16 bits are used
        public int ResistEffectId { get; set; }
        public uint Health { get; set; }
        public uint Effectiveness { get; set; }
        public int DamageEffectId { get; set; }
        public int DeadEffectId { get; set; }
        public byte ItemChance { get; set; }
        public ItemType ItemType { get; set; }
        public uint Unused1D0 { get; set; } // always 0
        public uint Unused1D4 { get; set; } // always UInt32.MaxValue
        public uint BeamHitMsgTarget { get; set; }
        public Message BeamHitMessage { get; set; }
        public uint BeamHitMsgParam1 { get; set; }
        public uint BeamHitMsgParam2 { get; set; }
        public uint PlayerColMsgTarget { get; set; }
        public Message PlayerColMessage { get; set; }
        public uint PlayerColMsgParam1 { get; set; }
        public uint PlayerColMsgParam2 { get; set; }
        public uint DeadMsgTarget { get; set; }
        public Message DeadMessage { get; set; }
        public uint DeadMsgParam1 { get; set; }
        public uint DeadMsgParam2 { get; set; }
        public ushort LifetimeMsg1Index { get; set; }
        public short LifetimeMsg1Target { get; set; }
        public Message LifetimeMessage1 { get; set; }
        public uint LifetimeMsg1Param1 { get; set; }
        public uint LifetimeMsg1Param2 { get; set; }
        public ushort LifetimeMsg2Index { get; set; }
        public short LifetimeMsg2Target { get; set; }
        public Message LifetimeMessage2 { get; set; }
        public uint LifetimeMsg2Param1 { get; set; }
        public uint LifetimeMsg2Param2 { get; set; }
        public ushort LifetimeMsg3Index { get; set; }
        public short LifetimeMsg3Target { get; set; }
        public Message LifetimeMessage3 { get; set; }
        public uint LifetimeMsg3Param1 { get; set; }
        public uint LifetimeMsg3Param2 { get; set; }
        public ushort LifetimeMsg4Index { get; set; }
        public short LifetimeMsg4Target { get; set; }
        public Message LifetimeMessage4 { get; set; }
        public uint LifetimeMsg4Param1 { get; set; }
        public uint LifetimeMsg4Param2 { get; set; }

        public PlatformEntityEditor() : base(EntityType.Platform)
        {
        }

        public PlatformEntityEditor(Entity header, PlatformEntityData raw) : base(header)
        {
            NoPort = raw.NoPort;
            ModelId = raw.ModelId;
            ParentId = raw.ParentId;
            Active = raw.Active != 0;
            Delay = raw.Delay;
            ScanData1 = raw.ScanData1;
            ScanMsgTarget = raw.ScanMsgTarget;
            ScanMessage = raw.ScanMessage;
            ScanData2 = raw.ScanData2;
            PositionCount = raw.PositionCount;
            for (int i = 0; i < 10; i++)
            {
                Positions.Add(raw.Positions[i].ToFloatVector());
            }
            for (int i = 0; i < 10; i++)
            {
                Rotations.Add(raw.Rotations[i].ToFloatVector());
            }
            PositionOffset = raw.PositionOffset.ToFloatVector();
            ForwardSpeed = raw.ForwardSpeed.FloatValue;
            BackwardSpeed = raw.BackwardSpeed.FloatValue;
            PortalName = raw.PortalName.MarshalString();
            MovementType = raw.MovementType;
            ForCutscene = raw.ForCutscene != 0;
            ReverseType = raw.ReverseType;
            Flags = raw.Flags;
            ContactDamage = raw.ContactDamage;
            BeamSpawnDir = raw.BeamSpawnDir.ToFloatVector();
            BeamSpawnPos = raw.BeamSpawnPos.ToFloatVector();
            BeamId = raw.BeamId;
            BeamInterval = raw.BeamInterval;
            BeamOnIntervals = raw.BeamOnIntervals;
            ResistEffectId = raw.ResistEffectId;
            Health = raw.Health;
            Effectiveness = raw.Effectiveness;
            DamageEffectId = raw.DamageEffectId;
            DeadEffectId = raw.DeadEffectId;
            ItemChance = raw.ItemChance;
            ItemType = raw.ItemType;
            Unused1D0 = raw.Unused1D0;
            Unused1D4 = raw.Unused1D4;
            BeamHitMsgTarget = raw.BeamHitMsgTarget;
            BeamHitMessage = raw.BeamHitMessage;
            BeamHitMsgParam1 = raw.BeamHitMsgParam1;
            BeamHitMsgParam2 = raw.BeamHitMsgParam2;
            PlayerColMsgTarget = raw.PlayerColMsgTarget;
            PlayerColMessage = raw.PlayerColMessage;
            PlayerColMsgParam1 = raw.PlayerColMsgParam1;
            PlayerColMsgParam2 = raw.PlayerColMsgParam2;
            DeadMsgTarget = raw.DeadMsgTarget;
            DeadMessage = raw.DeadMessage;
            DeadMsgParam1 = raw.DeadMsgParam1;
            DeadMsgParam2 = raw.DeadMsgParam2;
            LifetimeMsg1Index = raw.LifetimeMsg1Index;
            LifetimeMsg1Target = raw.LifetimeMsg1Target;
            LifetimeMessage1 = raw.LifetimeMessage1;
            LifetimeMsg1Param1 = raw.LifetimeMsg1Param1;
            LifetimeMsg1Param2 = raw.LifetimeMsg1Param2;
            LifetimeMsg2Index = raw.LifetimeMsg2Index;
            LifetimeMsg2Target = raw.LifetimeMsg2Target;
            LifetimeMessage2 = raw.LifetimeMessage2;
            LifetimeMsg2Param1 = raw.LifetimeMsg2Param1;
            LifetimeMsg2Param2 = raw.LifetimeMsg2Param2;
            LifetimeMsg3Index = raw.LifetimeMsg3Index;
            LifetimeMsg3Target = raw.LifetimeMsg3Target;
            LifetimeMessage3 = raw.LifetimeMessage3;
            LifetimeMsg3Param1 = raw.LifetimeMsg3Param1;
            LifetimeMsg3Param2 = raw.LifetimeMsg3Param2;
            LifetimeMsg4Index = raw.LifetimeMsg4Index;
            LifetimeMsg4Target = raw.LifetimeMsg4Target;
            LifetimeMessage4 = raw.LifetimeMessage4;
            LifetimeMsg4Param1 = raw.LifetimeMsg4Param1;
            LifetimeMsg4Param2 = raw.LifetimeMsg4Param2;
        }
    }

    public class FhPlatformEntityEditor : EntityEditorBase
    {
        public uint NoPortal { get; set; }
        public uint GroupId { get; set; }
        public uint Unused2C { get; set; }
        public byte Delay { get; set; }
        public byte PositionCount { get; set; }
        public CollisionVolume Volume { get; set; } // unused
        public List<Vector3> Positions { get; set; } = new List<Vector3>();
        public float Speed { get; set; }
        public string PortalName { get; set; } = "";

        public FhPlatformEntityEditor() : base(EntityType.FhPlatform)
        {
        }

        public FhPlatformEntityEditor(Entity header, FhPlatformEntityData raw) : base(header)
        {
            NoPortal = raw.NoPortal;
            GroupId = raw.GroupId;
            Unused2C = raw.Unused2C;
            Delay = raw.Delay;
            PositionCount = raw.PositionCount;
            Volume = new CollisionVolume(raw.Volume);
            for (int i = 0; i < 8; i++)
            {
                Positions.Add(raw.Positions[i].ToFloatVector());
            }
            Speed = raw.Speed.FloatValue;
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
        public bool Active { get; set; } = true;
        public sbyte TeamIndex { get; set; } = -1; // 0, 1, or -1 (only used in CTF)

        public PlayerSpawnEntityEditor() : base(EntityType.PlayerSpawn)
        {
        }

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
        public uint ConnectorId { get; set; }
        public byte TargetLayerId { get; set; }
        public byte Flags { get; set; } // bit 0 - locked
        public byte Field42 { get; set; }
        public byte Field43 { get; set; }
        public string EntityFilename { get; set; } = "";
        public string RoomName { get; set; } = "";

        public DoorEntityEditor() : base(EntityType.Door)
        {
        }

        public DoorEntityEditor(Entity header, DoorEntityData raw) : base(header)
        {
            DoorNodeName = raw.NodeName.MarshalString();
            PaletteId = raw.PaletteId;
            ModelId = raw.ModelId;
            ConnectorId = raw.ConnectorId;
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

        public FhDoorEntityEditor() : base(EntityType.FhDoor)
        {
        }

        public FhDoorEntityEditor(Entity header, FhDoorEntityData raw) : base(header)
        {
            RoomName = raw.RoomName.MarshalString();
            Flags = raw.Flags;
            ModelId = raw.ModelId;
        }
    }

    public class ItemSpawnEntityEditor : EntityEditorBase
    {
        public int ParentId { get; set; }
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

        public ItemSpawnEntityEditor() : base(EntityType.ItemSpawn)
        {
        }

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
        public ushort Unused2C { get; set; }

        public FhItemSpawnEntityEditor() : base(EntityType.FhItemSpawn)
        {
        }

        public FhItemSpawnEntityEditor(Entity header, FhItemSpawnEntityData raw) : base(header)
        {
            ItemType = raw.ItemType;
            SpawnLimit = raw.SpawnLimit;
            CooldownTime = raw.CooldownTime;
            Unused2C = raw.Unused2C;
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
        public TriggerFlags TriggerFlags { get; set; }
        public uint TriggerThreshold { get; set; } // for subtype 1
        public short ParentId { get; set; }
        public Message ParentMessage { get; set; }
        public uint ParentMsgParam1 { get; set; }
        public uint ParentMsgParam2 { get; set; }
        public short ChildId { get; set; }
        public Message ChildMessage { get; set; }
        public uint ChildMsgParam1 { get; set; }
        public uint ChildMsgParam2 { get; set; }

        public TriggerVolumeEntityEditor() : base(EntityType.TriggerVolume)
        {
        }

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
        public FhTriggerFlags TriggerFlags { get; set; }
        public uint Threshold { get; set; }
        public short ParentId { get; set; }
        public FhMessage ParentMessage { get; set; }
        public uint ParentMsgParam1 { get; set; }
        public short ChildId { get; set; }
        public FhMessage ChildMessage { get; set; }
        public uint ChildMsgParam1 { get; set; }

        public FhTriggerVolumeEntityEditor() : base(EntityType.FhTriggerVolume)
        {
        }

        public FhTriggerVolumeEntityEditor(Entity header, FhTriggerVolumeEntityData raw) : base(header)
        {
            Subtype = raw.Subtype;
            Box = new CollisionVolume(raw.Box);
            Sphere = new CollisionVolume(raw.Sphere);
            Cylinder = new CollisionVolume(raw.Cylinder);
            OneUse = raw.OneUse;
            Cooldown = raw.Cooldown;
            TriggerFlags = raw.TriggerFlags;
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
        public TriggerFlags TriggerFlags { get; set; }

        public AreaVolumeEntityEditor() : base(EntityType.AreaVolume)
        {
        }

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
            TriggerFlags = raw.TriggerFlags;
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
        public FhTriggerFlags TriggerFlags { get; set; }

        public FhAreaVolumeEntityEditor() : base(EntityType.FhAreaVolume)
        {
        }

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
            TriggerFlags = raw.TriggerFlags;
        }
    }

    public class JumpPadEntityEditor : EntityEditorBase
    {
        public int ParentId { get; set; }
        public uint Unused28 { get; set; } // usually 0, occasionally 2
        public CollisionVolume Volume { get; set; }
        public Vector3 BeamVector { get; set; }
        public float Speed { get; set; }
        public ushort ControlLockTime { get; set; }
        public ushort CooldownTime { get; set; }
        public bool Active { get; set; }
        public uint ModelId { get; set; }
        public uint BeamType { get; set; }
        public TriggerFlags TriggerFlags { get; set; }

        public JumpPadEntityEditor() : base(EntityType.JumpPad)
        {
        }

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
            BeamType = raw.BeamType;
            TriggerFlags = raw.TriggerFlags;
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
        public uint ControlLockTime { get; set; }
        public uint ModelId { get; set; }
        public uint BeamType { get; set; }
        public FhTriggerFlags TriggerFlags { get; set; }

        public FhJumpPadEntityEditor() : base(EntityType.FhJumpPad)
        {
        }

        public FhJumpPadEntityEditor(Entity header, FhJumpPadEntityData raw) : base(header)
        {
            VolumeType = raw.VolumeType;
            Box = new CollisionVolume(raw.Box);
            Cylinder = new CollisionVolume(raw.Cylinder);
            Sphere = new CollisionVolume(raw.Sphere);
            CooldownTime = raw.CooldownTime;
            BeamVector = raw.BeamVector.ToFloatVector();
            Speed = raw.Speed.FloatValue;
            ControlLockTime = raw.ControlLockTime;
            ModelId = raw.ModelId;
            BeamType = raw.BeamType;
            TriggerFlags = raw.TriggerFlags;
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
        public Message Message1 { get; set; }
        public short Message2Target { get; set; }
        public Message Message2 { get; set; }
        public short Message3Target { get; set; }
        public Message Message3 { get; set; }
        public short LinkedEntityId { get; set; } // always -1

        public ArtifactEntityEditor(Entity header, ArtifactEntityData raw) : base(header)
        {
            ModelId = raw.ModelId;
            ArtifactId = raw.ArtifactId;
            Active = raw.Active != 0;
            HasBase = raw.HasBase != 0;
            Message1Target = raw.Message1Target;
            Message1 = raw.Message1;
            Message2Target = raw.Message2Target;
            Message2 = raw.Message2;
            Message3Target = raw.Message3Target;
            Message3 = raw.Message3;
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
