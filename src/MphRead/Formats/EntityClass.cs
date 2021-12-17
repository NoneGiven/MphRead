using System;
using System.Collections.Generic;
using System.Linq;
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

        protected void PrintValue(string value1, string value2, string name)
        {
            if (value1 != value2)
            {
                Console.WriteLine($"{name}: {value1} / {value2}");
            }
        }

        protected void PrintValue<T>(T value1, T value2, string name) where T : struct
        {
            if (!value1.Equals(value2))
            {
                Console.WriteLine($"{name}: {value1} / {value2}");
            }
        }

        protected void PrintValues<T>(List<T> value1, List<T> value2, string name) where T : struct
        {
            if (value1.Count != value2.Count)
            {
                Console.WriteLine($"{name}: {value1.Count} / {value2.Count}");
                for (int i = 0; i < Math.Max(value1.Count, value2.Count); i++)
                {
                    Console.WriteLine($"{(i < value1.Count ? value1[i].ToString() : "N/A")} / {(i < value2.Count ? value2[i].ToString() : "N/A")}");
                }
            }
            else if (!Enumerable.SequenceEqual(value1, value2))
            {
                for (int i = 0; i < value1.Count; i++)
                {
                    Console.WriteLine($"{value1[i]} / {value2[i]}");
                }
            }
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

        public void CompareTo(PlatformEntityEditor other)
        {
            PrintValue(NoPort, other.NoPort, nameof(NoPort));
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
            PrintValue(ParentId, other.ParentId, nameof(ParentId));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(Delay, other.Delay, nameof(Delay));
            PrintValue(ScanData1, other.ScanData1, nameof(ScanData1));
            PrintValue(ScanMsgTarget, other.ScanMsgTarget, nameof(ScanMsgTarget));
            PrintValue(ScanMessage, other.ScanMessage, nameof(ScanMessage));
            PrintValue(ScanData2, other.ScanData2, nameof(ScanData2));
            PrintValue(PositionCount, other.PositionCount, nameof(PositionCount));
            PrintValues(Positions, other.Positions, nameof(Positions));
            PrintValues(Rotations, other.Rotations, nameof(Rotations));
            PrintValue(PositionOffset, other.PositionOffset, nameof(PositionOffset));
            PrintValue(ForwardSpeed, other.ForwardSpeed, nameof(ForwardSpeed));
            PrintValue(BackwardSpeed, other.BackwardSpeed, nameof(BackwardSpeed));
            PrintValue(PortalName, other.PortalName, nameof(PortalName));
            PrintValue(MovementType, other.MovementType, nameof(MovementType));
            PrintValue(ForCutscene, other.ForCutscene, nameof(ForCutscene));
            PrintValue(ReverseType, other.ReverseType, nameof(ReverseType));
            PrintValue(Flags, other.Flags, nameof(Flags));
            PrintValue(ContactDamage, other.ContactDamage, nameof(ContactDamage));
            PrintValue(BeamSpawnDir, other.BeamSpawnDir, nameof(BeamSpawnDir));
            PrintValue(BeamSpawnPos, other.BeamSpawnPos, nameof(BeamSpawnPos));
            PrintValue(BeamId, other.BeamId, nameof(BeamId));
            PrintValue(BeamInterval, other.BeamInterval, nameof(BeamInterval));
            PrintValue(BeamOnIntervals, other.BeamOnIntervals, nameof(BeamOnIntervals));
            PrintValue(ResistEffectId, other.ResistEffectId, nameof(ResistEffectId));
            PrintValue(Health, other.Health, nameof(Health));
            PrintValue(Effectiveness, other.Effectiveness, nameof(Effectiveness));
            PrintValue(DamageEffectId, other.DamageEffectId, nameof(DamageEffectId));
            PrintValue(DeadEffectId, other.DeadEffectId, nameof(DeadEffectId));
            PrintValue(ItemChance, other.ItemChance, nameof(ItemChance));
            PrintValue(ItemType, other.ItemType, nameof(ItemType));
            PrintValue(Unused1D0, other.Unused1D0, nameof(Unused1D0));
            PrintValue(Unused1D4, other.Unused1D4, nameof(Unused1D4));
            PrintValue(BeamHitMsgTarget, other.BeamHitMsgTarget, nameof(BeamHitMsgTarget));
            PrintValue(BeamHitMessage, other.BeamHitMessage, nameof(BeamHitMessage));
            PrintValue(BeamHitMsgParam1, other.BeamHitMsgParam1, nameof(BeamHitMsgParam1));
            PrintValue(BeamHitMsgParam2, other.BeamHitMsgParam2, nameof(BeamHitMsgParam2));
            PrintValue(PlayerColMsgTarget, other.PlayerColMsgTarget, nameof(PlayerColMsgTarget));
            PrintValue(PlayerColMessage, other.PlayerColMessage, nameof(PlayerColMessage));
            PrintValue(PlayerColMsgParam1, other.PlayerColMsgParam1, nameof(PlayerColMsgParam1));
            PrintValue(PlayerColMsgParam2, other.PlayerColMsgParam2, nameof(PlayerColMsgParam2));
            PrintValue(DeadMsgTarget, other.DeadMsgTarget, nameof(DeadMsgTarget));
            PrintValue(DeadMessage, other.DeadMessage, nameof(DeadMessage));
            PrintValue(DeadMsgParam1, other.DeadMsgParam1, nameof(DeadMsgParam1));
            PrintValue(DeadMsgParam2, other.DeadMsgParam2, nameof(DeadMsgParam2));
            PrintValue(LifetimeMsg1Index, other.LifetimeMsg1Index, nameof(LifetimeMsg1Index));
            PrintValue(LifetimeMsg1Target, other.LifetimeMsg1Target, nameof(LifetimeMsg1Target));
            PrintValue(LifetimeMessage1, other.LifetimeMessage1, nameof(LifetimeMessage1));
            PrintValue(LifetimeMsg1Param1, other.LifetimeMsg1Param1, nameof(LifetimeMsg1Param1));
            PrintValue(LifetimeMsg1Param2, other.LifetimeMsg1Param2, nameof(LifetimeMsg1Param2));
            PrintValue(LifetimeMsg2Index, other.LifetimeMsg2Index, nameof(LifetimeMsg2Index));
            PrintValue(LifetimeMsg2Target, other.LifetimeMsg2Target, nameof(LifetimeMsg2Target));
            PrintValue(LifetimeMessage2, other.LifetimeMessage2, nameof(LifetimeMessage2));
            PrintValue(LifetimeMsg2Param1, other.LifetimeMsg2Param1, nameof(LifetimeMsg2Param1));
            PrintValue(LifetimeMsg2Param2, other.LifetimeMsg2Param2, nameof(LifetimeMsg2Param2));
            PrintValue(LifetimeMsg3Index, other.LifetimeMsg3Index, nameof(LifetimeMsg3Index));
            PrintValue(LifetimeMsg3Target, other.LifetimeMsg3Target, nameof(LifetimeMsg3Target));
            PrintValue(LifetimeMessage3, other.LifetimeMessage3, nameof(LifetimeMessage3));
            PrintValue(LifetimeMsg3Param1, other.LifetimeMsg3Param1, nameof(LifetimeMsg3Param1));
            PrintValue(LifetimeMsg3Param2, other.LifetimeMsg3Param2, nameof(LifetimeMsg3Param2));
            PrintValue(LifetimeMsg4Index, other.LifetimeMsg4Index, nameof(LifetimeMsg4Index));
            PrintValue(LifetimeMsg4Target, other.LifetimeMsg4Target, nameof(LifetimeMsg4Target));
            PrintValue(LifetimeMessage4, other.LifetimeMessage4, nameof(LifetimeMessage4));
            PrintValue(LifetimeMsg4Param1, other.LifetimeMsg4Param1, nameof(LifetimeMsg4Param1));
            PrintValue(LifetimeMsg4Param2, other.LifetimeMsg4Param2, nameof(LifetimeMsg4Param2));
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

        public void CompareTo(FhPlatformEntityEditor other)
        {
            PrintValue(NoPortal, other.NoPortal, nameof(NoPortal));
            PrintValue(GroupId, other.GroupId, nameof(GroupId));
            PrintValue(Unused2C, other.Unused2C, nameof(Unused2C));
            PrintValue(Delay, other.Delay, nameof(Delay));
            PrintValue(PositionCount, other.PositionCount, nameof(PositionCount));
            PrintValue(Volume, other.Volume, nameof(Volume));
            PrintValues(Positions, other.Positions, nameof(Positions));
            PrintValue(Speed, other.Speed, nameof(Speed));
            PrintValue(PortalName, other.PortalName, nameof(PortalName));
        }
    }

    public class ObjectEntityEditor : EntityEditorBase
    {
        public ObjectFlags Flags { get; set; }
        public ObjEffFlags EffectFlags { get; set; }
        public int ModelId { get; set; }
        public short LinkedEntity { get; set; }
        public ushort ScanId { get; set; }
        public short ScanMsgTarget { get; set; }
        public Message ScanMessage { get; set; }
        public int EffectId { get; set; }
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

        public void CompareTo(ObjectEntityEditor other)
        {
            PrintValue(Flags, other.Flags, nameof(Flags));
            PrintValue(EffectFlags, other.EffectFlags, nameof(EffectFlags));
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
            PrintValue(LinkedEntity, other.LinkedEntity, nameof(LinkedEntity));
            PrintValue(ScanId, other.ScanId, nameof(ScanId));
            PrintValue(ScanMsgTarget, other.ScanMsgTarget, nameof(ScanMsgTarget));
            PrintValue(ScanMessage, other.ScanMessage, nameof(ScanMessage));
            PrintValue(EffectId, other.EffectId, nameof(EffectId));
            PrintValue(EffectInterval, other.EffectInterval, nameof(EffectInterval));
            PrintValue(EffectOnIntervals, other.EffectOnIntervals, nameof(EffectOnIntervals));
            PrintValue(EffectPositionOffset, other.EffectPositionOffset, nameof(EffectPositionOffset));
            PrintValue(Volume, other.Volume, nameof(Volume));
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

        public void CompareTo(PlayerSpawnEntityEditor other)
        {
            PrintValue(Availability, other.Availability, nameof(Availability));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(TeamIndex, other.TeamIndex, nameof(TeamIndex));
        }
    }

    public class DoorEntityEditor : EntityEditorBase
    {
        public string DoorNodeName { get; set; } = "";
        public uint PaletteId { get; set; }
        public uint ModelId { get; set; }
        public uint ConnectorId { get; set; }
        public byte TargetLayerId { get; set; }
        public bool Locked { get; set; }
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
            Locked = raw.Locked != 0;
            Field42 = raw.OutConnectorId;
            Field43 = raw.OutLoaderId;
            EntityFilename = raw.EntityFilename.MarshalString();
            RoomName = raw.RoomName.MarshalString();
        }

        public void CompareTo(DoorEntityEditor other)
        {
            PrintValue(DoorNodeName, other.DoorNodeName, nameof(DoorNodeName));
            PrintValue(PaletteId, other.PaletteId, nameof(PaletteId));
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
            PrintValue(ConnectorId, other.ConnectorId, nameof(ConnectorId));
            PrintValue(TargetLayerId, other.TargetLayerId, nameof(TargetLayerId));
            PrintValue(Locked, other.Locked, nameof(Locked));
            PrintValue(Field42, other.Field42, nameof(Field42));
            PrintValue(Field43, other.Field43, nameof(Field43));
            PrintValue(EntityFilename, other.EntityFilename, nameof(EntityFilename));
            PrintValue(RoomName, other.RoomName, nameof(RoomName));
        }
    }

    public class FhDoorEntityEditor : EntityEditorBase
    {
        public string RoomName { get; set; } = "";
        public bool Locked { get; set; }
        public uint ModelId { get; set; }

        public FhDoorEntityEditor() : base(EntityType.FhDoor)
        {
        }

        public FhDoorEntityEditor(Entity header, FhDoorEntityData raw) : base(header)
        {
            RoomName = raw.RoomName.MarshalString();
            Locked = raw.Locked != 0;
            ModelId = raw.ModelId;
        }

        public void CompareTo(FhDoorEntityEditor other)
        {
            PrintValue(RoomName, other.RoomName, nameof(RoomName));
            PrintValue(Locked, other.Locked, nameof(Locked));
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
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
        public short NotifyEntityId { get; set; } // todo: parent? child?
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
            NotifyEntityId = raw.NotifyEntityId;
            CollectedMessage = raw.CollectedMessage;
            CollectedMsgParam1 = raw.CollectedMsgParam1;
            CollectedMsgParam2 = raw.CollectedMsgParam2;
        }

        public void CompareTo(ItemSpawnEntityEditor other)
        {
            PrintValue(ParentId, other.ParentId, nameof(ParentId));
            PrintValue(ItemType, other.ItemType, nameof(ItemType));
            PrintValue(Enabled, other.Enabled, nameof(Enabled));
            PrintValue(HasBase, other.HasBase, nameof(HasBase));
            PrintValue(AlwaysActive, other.AlwaysActive, nameof(AlwaysActive));
            PrintValue(MaxSpawnCount, other.MaxSpawnCount, nameof(MaxSpawnCount));
            PrintValue(SpawnInterval, other.SpawnInterval, nameof(SpawnInterval));
            PrintValue(SpawnDelay, other.SpawnDelay, nameof(SpawnDelay));
            PrintValue(NotifyEntityId, other.NotifyEntityId, nameof(NotifyEntityId));
            PrintValue(CollectedMessage, other.CollectedMessage, nameof(CollectedMessage));
            PrintValue(CollectedMsgParam1, other.CollectedMsgParam1, nameof(CollectedMsgParam1));
            PrintValue(CollectedMsgParam2, other.CollectedMsgParam2, nameof(CollectedMsgParam2));
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

        public void CompareTo(FhItemSpawnEntityEditor other)
        {
            PrintValue(ItemType, other.ItemType, nameof(ItemType));
            PrintValue(SpawnLimit, other.SpawnLimit, nameof(SpawnLimit));
            PrintValue(CooldownTime, other.CooldownTime, nameof(CooldownTime));
            PrintValue(Unused2C, other.Unused2C, nameof(Unused2C));
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

        public void CompareTo(TriggerVolumeEntityEditor other)
        {
            PrintValue(Subtype, other.Subtype, nameof(Subtype));
            PrintValue(Volume, other.Volume, nameof(Volume));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(AlwaysActive, other.AlwaysActive, nameof(AlwaysActive));
            PrintValue(DeactivateAfterUse, other.DeactivateAfterUse, nameof(DeactivateAfterUse));
            PrintValue(RepeatDelay, other.RepeatDelay, nameof(RepeatDelay));
            PrintValue(CheckDelay, other.CheckDelay, nameof(CheckDelay));
            PrintValue(RequiredStateBit, other.RequiredStateBit, nameof(RequiredStateBit));
            PrintValue(TriggerFlags, other.TriggerFlags, nameof(TriggerFlags));
            PrintValue(TriggerThreshold, other.TriggerThreshold, nameof(TriggerThreshold));
            PrintValue(ParentId, other.ParentId, nameof(ParentId));
            PrintValue(ParentMessage, other.ParentMessage, nameof(ParentMessage));
            PrintValue(ParentMsgParam1, other.ParentMsgParam1, nameof(ParentMsgParam1));
            PrintValue(ParentMsgParam2, other.ParentMsgParam2, nameof(ParentMsgParam2));
            PrintValue(ChildId, other.ChildId, nameof(ChildId));
            PrintValue(ChildMessage, other.ChildMessage, nameof(ChildMessage));
            PrintValue(ChildMsgParam1, other.ChildMsgParam1, nameof(ChildMsgParam1));
            PrintValue(ChildMsgParam2, other.ChildMsgParam2, nameof(ChildMsgParam2));
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

        public void CompareTo(FhTriggerVolumeEntityEditor other)
        {
            PrintValue(Subtype, other.Subtype, nameof(Subtype));
            PrintValue(Box, other.Box, nameof(Box));
            PrintValue(Sphere, other.Sphere, nameof(Sphere));
            PrintValue(Cylinder, other.Cylinder, nameof(Cylinder));
            PrintValue(OneUse, other.OneUse, nameof(OneUse));
            PrintValue(Cooldown, other.Cooldown, nameof(Cooldown));
            PrintValue(TriggerFlags, other.TriggerFlags, nameof(TriggerFlags));
            PrintValue(Threshold, other.Threshold, nameof(Threshold));
            PrintValue(ParentId, other.ParentId, nameof(ParentId));
            PrintValue(ParentMessage, other.ParentMessage, nameof(ParentMessage));
            PrintValue(ParentMsgParam1, other.ParentMsgParam1, nameof(ParentMsgParam1));
            PrintValue(ChildId, other.ChildId, nameof(ChildId));
            PrintValue(ChildMessage, other.ChildMessage, nameof(ChildMessage));
            PrintValue(ChildMsgParam1, other.ChildMsgParam1, nameof(ChildMsgParam1));
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

        public void CompareTo(AreaVolumeEntityEditor other)
        {
            PrintValue(Volume, other.Volume, nameof(Volume));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(AlwaysActive, other.AlwaysActive, nameof(AlwaysActive));
            PrintValue(AllowMultiple, other.AllowMultiple, nameof(AllowMultiple));
            PrintValue(MessageDelay, other.MessageDelay, nameof(MessageDelay));
            PrintValue(Unused6A, other.Unused6A, nameof(Unused6A));
            PrintValue(InsideMessage, other.InsideMessage, nameof(InsideMessage));
            PrintValue(InsideMsgParam1, other.InsideMsgParam1, nameof(InsideMsgParam1));
            PrintValue(InsideMsgParam2, other.InsideMsgParam2, nameof(InsideMsgParam2));
            PrintValue(ParentId, other.ParentId, nameof(ParentId));
            PrintValue(ExitMessage, other.ExitMessage, nameof(ExitMessage));
            PrintValue(ExitMsgParam1, other.ExitMsgParam1, nameof(ExitMsgParam1));
            PrintValue(ExitMsgParam2, other.ExitMsgParam2, nameof(ExitMsgParam2));
            PrintValue(ChildId, other.ChildId, nameof(ChildId));
            PrintValue(Cooldown, other.Cooldown, nameof(Cooldown));
            PrintValue(Priority, other.Priority, nameof(Priority));
            PrintValue(TriggerFlags, other.TriggerFlags, nameof(TriggerFlags));
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

        public void CompareTo(FhAreaVolumeEntityEditor other)
        {
            PrintValue(Subtype, other.Subtype, nameof(Subtype));
            PrintValue(Box, other.Box, nameof(Box));
            PrintValue(Sphere, other.Sphere, nameof(Sphere));
            PrintValue(Cylinder, other.Cylinder, nameof(Cylinder));
            PrintValue(InsideMessage, other.InsideMessage, nameof(InsideMessage));
            PrintValue(InsideMsgParam1, other.InsideMsgParam1, nameof(InsideMsgParam1));
            PrintValue(ExitMessage, other.ExitMessage, nameof(ExitMessage));
            PrintValue(ExitMsgParam1, other.ExitMsgParam1, nameof(ExitMsgParam1));
            PrintValue(Cooldown, other.Cooldown, nameof(Cooldown));
            PrintValue(TriggerFlags, other.TriggerFlags, nameof(TriggerFlags));
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

        public void CompareTo(JumpPadEntityEditor other)
        {
            PrintValue(ParentId, other.ParentId, nameof(ParentId));
            PrintValue(Unused28, other.Unused28, nameof(Unused28));
            PrintValue(Volume, other.Volume, nameof(Volume));
            PrintValue(BeamVector, other.BeamVector, nameof(BeamVector));
            PrintValue(Speed, other.Speed, nameof(Speed));
            PrintValue(ControlLockTime, other.ControlLockTime, nameof(ControlLockTime));
            PrintValue(CooldownTime, other.CooldownTime, nameof(CooldownTime));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
            PrintValue(BeamType, other.BeamType, nameof(BeamType));
            PrintValue(TriggerFlags, other.TriggerFlags, nameof(TriggerFlags));
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

        public void CompareTo(FhJumpPadEntityEditor other)
        {
            PrintValue(VolumeType, other.VolumeType, nameof(VolumeType));
            PrintValue(Box, other.Box, nameof(Box));
            PrintValue(Sphere, other.Sphere, nameof(Sphere));
            PrintValue(Cylinder, other.Cylinder, nameof(Cylinder));
            PrintValue(CooldownTime, other.CooldownTime, nameof(CooldownTime));
            PrintValue(BeamVector, other.BeamVector, nameof(BeamVector));
            PrintValue(Speed, other.Speed, nameof(Speed));
            PrintValue(ControlLockTime, other.ControlLockTime, nameof(ControlLockTime));
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
            PrintValue(BeamType, other.BeamType, nameof(BeamType));
            PrintValue(TriggerFlags, other.TriggerFlags, nameof(TriggerFlags));
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

        public void CompareTo(PointModuleEntityEditor other)
        {
            PrintValue(NextId, other.NextId, nameof(NextId));
            PrintValue(PrevId, other.PrevId, nameof(PrevId));
            PrintValue(Active, other.Active, nameof(Active));
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

        public void CompareTo(MorphCameraEntityEditor other)
        {
            PrintValue(Volume, other.Volume, nameof(Volume));
        }
    }

    public class OctolithFlagEntityEditor : EntityEditorBase
    {
        public byte TeamId { get; set; }

        public OctolithFlagEntityEditor(Entity header, OctolithFlagEntityData raw) : base(header)
        {
            TeamId = raw.TeamId;
        }

        public void CompareTo(OctolithFlagEntityEditor other)
        {
            PrintValue(TeamId, other.TeamId, nameof(TeamId));
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

        public void CompareTo(FlagBaseEntityEditor other)
        {
            PrintValue(TeamId, other.TeamId, nameof(TeamId));
            PrintValue(Volume, other.Volume, nameof(Volume));
        }
    }

    public class TeleporterEntityEditor : EntityEditorBase
    {
        public byte LoadIndex { get; set; }
        public byte TargetIndex { get; set; }
        public byte ArtifactId { get; set; }
        public bool Active { get; set; }
        public bool Invisible { get; set; }
        public string TargetRoom { get; set; } = "";
        public Vector3 TargetPosition { get; set; }
        public string TeleporterNodeName { get; set; } = "";

        public TeleporterEntityEditor(Entity header, TeleporterEntityData raw) : base(header)
        {
            LoadIndex = raw.LoadIndex;
            TargetIndex = raw.TargetIndex;
            ArtifactId = raw.ArtifactId;
            Active = raw.Active != 0;
            Invisible = raw.Invisible != 0;
            TargetRoom = raw.TargetRoom.MarshalString();
            TargetPosition = raw.TargetPosition.ToFloatVector();
            TeleporterNodeName = raw.NodeName.MarshalString();
        }

        public void CompareTo(TeleporterEntityEditor other)
        {
            PrintValue(LoadIndex, other.LoadIndex, nameof(LoadIndex));
            PrintValue(TargetIndex, other.TargetIndex, nameof(TargetIndex));
            PrintValue(ArtifactId, other.ArtifactId, nameof(ArtifactId));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(Invisible, other.Invisible, nameof(Invisible));
            PrintValue(TargetRoom, other.TargetRoom, nameof(TargetRoom));
            PrintValue(TargetPosition, other.TargetPosition, nameof(TargetPosition));
            PrintValue(TeleporterNodeName, other.TeleporterNodeName, nameof(TeleporterNodeName));
        }
    }

    public class NodeDefenseEntityEditor : EntityEditorBase
    {
        public CollisionVolume Volume { get; set; }

        public NodeDefenseEntityEditor(Entity header, NodeDefenseEntityData raw) : base(header)
        {
            Volume = new CollisionVolume(raw.Volume);
        }

        public void CompareTo(NodeDefenseEntityEditor other)
        {
            PrintValue(Volume, other.Volume, nameof(Volume));
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

        public void CompareTo(LightSourceEntityEditor other)
        {
            PrintValue(Volume, other.Volume, nameof(Volume));
            PrintValue(Light1Enabled, other.Light1Enabled, nameof(Light1Enabled));
            PrintValue(Light1Color, other.Light1Color, nameof(Light1Color));
            PrintValue(Light1Vector, other.Light1Vector, nameof(Light1Vector));
            PrintValue(Light2Enabled, other.Light2Enabled, nameof(Light2Enabled));
            PrintValue(Light2Color, other.Light2Color, nameof(Light2Color));
            PrintValue(Light2Vector, other.Light2Vector, nameof(Light2Vector));
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

        public void CompareTo(ArtifactEntityEditor other)
        {
            PrintValue(ModelId, other.ModelId, nameof(ModelId));
            PrintValue(ArtifactId, other.ArtifactId, nameof(ArtifactId));
            PrintValue(Active, other.Active, nameof(Active));
            PrintValue(HasBase, other.HasBase, nameof(HasBase));
            PrintValue(Message1Target, other.Message1Target, nameof(Message1Target));
            PrintValue(Message1, other.Message1, nameof(Message1));
            PrintValue(Message2Target, other.Message2Target, nameof(Message2Target));
            PrintValue(Message2, other.Message2, nameof(Message2));
            PrintValue(Message3Target, other.Message3Target, nameof(Message3Target));
            PrintValue(Message3, other.Message3, nameof(Message3));
            PrintValue(LinkedEntityId, other.LinkedEntityId, nameof(LinkedEntityId));
        }
    }

    public class CameraSequenceEntityEditor : EntityEditorBase
    {
        public byte SequenceId { get; set; }
        public bool Handoff { get; set; }
        public bool Loop { get; set; }
        public bool BlockInput { get; set; }
        public bool ForceAltForm { get; set; }
        public bool ForceBipedForm { get; set; }
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
            Handoff = raw.Handoff != 0;
            Loop = raw.Loop != 0;
            BlockInput = raw.BlockInput != 0;
            ForceAltForm = raw.ForceAltForm != 0;
            ForceBipedForm = raw.ForceBipedForm != 0;
            DelayFrames = raw.DelayFrames;
            PlayerId1 = raw.PlayerId1;
            PlayerId2 = raw.PlayerId2;
            Entity1 = raw.Entity1;
            Entity2 = raw.Entity2;
            MessageTargetId = raw.MessageTargetId;
            Message = raw.Message;
            MessageParam = raw.MessageParam;
        }

        public void CompareTo(CameraSequenceEntityEditor other)
        {
            PrintValue(SequenceId, other.SequenceId, nameof(SequenceId));
            PrintValue(Handoff, other.Handoff, nameof(Handoff));
            PrintValue(Loop, other.Loop, nameof(Loop));
            PrintValue(BlockInput, other.BlockInput, nameof(BlockInput));
            PrintValue(ForceAltForm, other.ForceAltForm, nameof(ForceAltForm));
            PrintValue(ForceBipedForm, other.ForceBipedForm, nameof(ForceBipedForm));
            PrintValue(DelayFrames, other.DelayFrames, nameof(DelayFrames));
            PrintValue(PlayerId1, other.PlayerId1, nameof(PlayerId1));
            PrintValue(PlayerId2, other.PlayerId2, nameof(PlayerId2));
            PrintValue(Entity1, other.Entity1, nameof(Entity1));
            PrintValue(Entity2, other.Entity2, nameof(Entity2));
            PrintValue(MessageTargetId, other.MessageTargetId, nameof(MessageTargetId));
            PrintValue(Message, other.Message, nameof(Message));
            PrintValue(MessageParam, other.MessageParam, nameof(MessageParam));
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

        public void CompareTo(ForceFieldEntityEditor other)
        {
            PrintValue(ForceFieldType, other.ForceFieldType, nameof(ForceFieldType));
            PrintValue(Width, other.Width, nameof(Width));
            PrintValue(Height, other.Height, nameof(Height));
            PrintValue(Active, other.Active, nameof(Active));
        }
    }
}
