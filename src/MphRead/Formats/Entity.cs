using System;
using System.Runtime.InteropServices;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead
{
    // size: 36
    public readonly struct EntityHeader
    {
        public readonly uint Version;
        // putting lengths on a separate struct so we can index e.g. header.Lengths[0]
        public readonly EntityLengthArray Lengths;
    }

    // size: 24
    public readonly struct EntityEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName; // todo: use this for partial room visibility
        public readonly ushort LayerMask;
        public readonly ushort Length;
        public readonly uint DataOffset;
    }

    // size: 20
    public readonly struct FhEntityEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName; // todo: same as above
        public readonly uint DataOffset;
    }

    // size: 40
    public readonly struct EntityDataHeader
    {
        public readonly ushort Type;
        public readonly short EntityId; // counts up
        public readonly Vector3Fx Position;
        public readonly Vector3Fx UpVector;
        public readonly Vector3Fx FacingVector;

        public EntityDataHeader(ushort type, short entityId, Vector3 position,
            Vector3 upVector, Vector3 facingVector)
        {
            Type = type;
            EntityId = entityId;
            Position = position.ToVector3Fx();
            UpVector = upVector.ToVector3Fx();
            FacingVector = facingVector.ToVector3Fx();
        }
    }

    // size: 588
    public readonly struct PlatformEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint NoPort; // used as boolean, but some of the beam spawners in Frost Labyrinth have a value of 2
        public readonly uint ModelId;
        public readonly short ParentId;
        public readonly byte Active; // boolean
        public readonly byte Delay;
        public readonly ushort ScanData1;
        public readonly short ScanMsgTarget;
        public readonly Message ScanMessage;
        public readonly ushort ScanData2;
        public readonly ushort PositionCount;
        public readonly Vector3FxArray10 Positions;
        public readonly Vector4FxArray10 Rotations;
        public readonly Vector3Fx PositionOffset;
        public readonly Fixed ForwardSpeed;
        public readonly Fixed BackwardSpeed;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] PortalName;
        public readonly uint MovementType;
        public readonly uint ForCutscene;
        public readonly uint ReverseType;
        public readonly PlatformFlags Flags;
        public readonly uint ContactDamage;
        public readonly Vector3Fx BeamSpawnDir;
        public readonly Vector3Fx BeamSpawnPos;
        public readonly int BeamId;
        public readonly uint BeamInterval;
        public readonly uint BeamOnIntervals; // 16 bits are used
        public readonly ushort Unused1B0; // always UInt16.MaxValue
        public readonly ushort Unused1B2; // always 0
        public readonly int ResistEffectId;
        public readonly uint Health;
        public readonly uint Effectiveness;
        public readonly int DamageEffectId;
        public readonly int DeadEffectId;
        public readonly byte ItemChance;
        public readonly byte Padding1C9;
        public readonly ushort Padding1CA;
        public readonly ItemType ItemType;
        public readonly uint Unused1D0; // always 0
        public readonly uint Unused1D4; // always UInt32.MaxValue
        public readonly int BeamHitMsgTarget;
        public readonly Message BeamHitMessage;
        public readonly int BeamHitMsgParam1;
        public readonly int BeamHitMsgParam2;
        public readonly int PlayerColMsgTarget;
        public readonly Message PlayerColMessage;
        public readonly int PlayerColMsgParam1;
        public readonly int PlayerColMsgParam2;
        public readonly int DeadMsgTarget;
        public readonly Message DeadMessage;
        public readonly int DeadMsgParam1;
        public readonly int DeadMsgParam2;
        public readonly ushort LifetimeMsg1Index;
        public readonly short LifetimeMsg1Target;
        public readonly Message LifetimeMessage1;
        public readonly int LifetimeMsg1Param1;
        public readonly int LifetimeMsg1Param2;
        public readonly ushort LifetimeMsg2Index;
        public readonly short LifetimeMsg2Target;
        public readonly Message LifetimeMessage2;
        public readonly int LifetimeMsg2Param1;
        public readonly int LifetimeMsg2Param2;
        public readonly ushort LifetimeMsg3Index;
        public readonly short LifetimeMsg3Target;
        public readonly Message LifetimeMessage3;
        public readonly int LifetimeMsg3Param1;
        public readonly int LifetimeMsg3Param2;
        public readonly ushort LifetimeMsg4Index;
        public readonly short LifetimeMsg4Target;
        public readonly Message LifetimeMessage4;
        public readonly int LifetimeMsg4Param1;
        public readonly int LifetimeMsg4Param2;
    }

    // size: 236
    public readonly struct FhPlatformEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint NoPortal;
        public readonly uint GroupId;
        public readonly uint Unused2C;
        public readonly byte Delay;
        public readonly byte PositionCount;
        public readonly ushort Padding32;
        public readonly FhRawCollisionVolume Volume; // unused
        public readonly Vector3FxArray8 Positions;
        public readonly Fixed Speed;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] PortalName;
    }

    // size: 152
    public readonly struct ObjectEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly ObjectFlags Flags;
        public readonly byte Padding25;
        public readonly ushort Padding26;
        public readonly ObjEffFlags EffectFlags;
        public readonly int ModelId;
        public readonly short LinkedEntity;
        public readonly ushort ScanId;
        public readonly short ScanMsgTarget;
        public readonly ushort Padding36;
        public readonly Message ScanMessage;
        public readonly int EffectId;
        public readonly uint EffectInterval;
        public readonly uint EffectOnIntervals; // 16 bits are used
        public readonly Vector3Fx EffectPositionOffset; // maximum value for random offset
        public readonly RawCollisionVolume Volume;
    }

    // size: 43
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct PlayerSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly byte Availability; // 0 - any time, 1 - no first frame, 2 - bot only (FH)
        public readonly byte Active; // boolean
        public readonly sbyte TeamIndex; // 0, 1, or -1
    }

    // size: 104
    public readonly struct DoorEntityData
    {
        public readonly EntityDataHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName;
        public readonly uint PaletteId;
        public readonly DoorType DoorType;
        public readonly uint ConnectorId;
        public readonly byte TargetLayerId;
        public readonly byte Locked; // boolean
        public readonly byte OutConnectorId;
        public readonly byte OutLoaderId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] EntityFilename;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] RoomName;

        public DoorEntityData(EntityDataHeader header, string? nodeName, uint paletteId, DoorType doorType, uint connectorId,
            byte targetLayerId, byte locked, byte outConnectorId, byte outLoaderId, string? entityFilename, string? roomName)
        {
            Header = header;
            NodeName = new char[16];
            if (nodeName != null)
            {
                nodeName.CopyTo(NodeName);
            }
            PaletteId = paletteId;
            DoorType = doorType;
            ConnectorId = connectorId;
            TargetLayerId = targetLayerId;
            Locked = locked;
            OutConnectorId = outConnectorId;
            OutLoaderId = outLoaderId;
            EntityFilename = new char[16];
            if (entityFilename != null)
            {
                entityFilename.CopyTo(EntityFilename);
            }
            RoomName = new char[16];
            if (roomName != null)
            {
                roomName.CopyTo(RoomName);
            }
        }
    }

    // size: 64
    public readonly struct FhDoorEntityData
    {
        public readonly EntityDataHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] RoomName;
        public readonly uint Locked; // boolean
        public readonly uint ModelId;
    }

    // size: 72
    public readonly struct ItemSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly int ParentId;
        public readonly ItemType ItemType;
        public readonly byte Enabled; // boolean
        public readonly byte HasBase; // boolean
        public readonly byte AlwaysActive; // boolean -- set flags bit 0 based on Active boolean only and ignore room state
        public readonly byte Padding2F;
        public readonly ushort MaxSpawnCount;
        public readonly ushort SpawnInterval;
        public readonly ushort SpawnDelay;
        public readonly short NotifyEntityId; // todo: parent? child?
        public readonly Message CollectedMessage;
        public readonly int CollectedMsgParam1;
        public readonly int CollectedMsgParam2;
    }

    // size: 50
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct FhItemSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhItemType ItemType;
        public readonly ushort SpawnLimit;
        public readonly ushort CooldownTime;
        public readonly ushort Unused2C;
    }

    // size: 160
    public readonly struct TriggerVolumeEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly TriggerType Subtype;
        public readonly RawCollisionVolume Volume;
        public readonly ushort Unused68; // always UInt16.MaxValue
        public readonly byte Active; // boolean
        public readonly byte AlwaysActive; // boolean -- set flags bit 0 based on Active boolean only and ignore room state
        public readonly byte DeactivateAfterUse; // boolean -- set flags bit 1
        public readonly byte Padding6D;
        public readonly ushort RepeatDelay;
        public readonly ushort CheckDelay;
        public readonly ushort RequiredStateBit; // for subtype 4
        public readonly TriggerFlags TriggerFlags;
        public readonly uint TriggerThreshold; // for subtype 1
        public readonly short ParentId;
        public readonly ushort Padding7E;
        public readonly Message ParentMessage;
        public readonly int ParentMsgParam1;
        public readonly int ParentMsgParam2;
        public readonly short ChildId;
        public readonly ushort Padding8E;
        public readonly Message ChildMessage;
        public readonly int ChildMsgParam1;
        public readonly int ChildMsgParam2;
    }

    // size: 272
    public readonly struct FhTriggerVolumeEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhTriggerType Subtype; // 0/1/2 - sphere/box/cylinder, 3 - threshold
        public readonly FhRawCollisionVolume Box;
        public readonly FhRawCollisionVolume Sphere;
        public readonly FhRawCollisionVolume Cylinder;
        public readonly ushort OneUse;
        public readonly ushort Cooldown;
        public readonly FhTriggerFlags TriggerFlags;
        public readonly uint Threshold;
        public readonly short ParentId;
        public readonly ushort PaddingF6;
        public readonly FhMessage ParentMessage;
        public readonly int ParentMsgParam1;
        public readonly short ChildId;
        public readonly ushort Padding102;
        public readonly FhMessage ChildMessage;
        public readonly int ChildMsgParam1;

        public FhRawCollisionVolume ActiveVolume
        {
            get
            {
                if (Subtype == FhTriggerType.Cylinder)
                {
                    return Cylinder;
                }
                if (Subtype == FhTriggerType.Box)
                {
                    return Box;
                }
                return Sphere;
            }
        }
    }

    // size: 152
    public readonly struct AreaVolumeEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly RawCollisionVolume Volume;
        public readonly ushort Unused64; // always UInt16.MaxValue
        public readonly byte Active; // boolean -- in 1P, may be controlled by room state bits
        public readonly byte AlwaysActive; // boolean -- ignore 1P state bits
        public readonly byte AllowMultiple; // boolean
        public readonly byte MessageDelay; // always 0 or 1
        public readonly ushort Unused6A; // always 0 or 1
        public readonly Message InsideMessage;
        public readonly int InsideMsgParam1; // seconds for escape sequence, gravity/jump assist values, etc.
        public readonly int InsideMsgParam2; // always 0 except for type 15, where it's always 2
        public readonly short ParentId;
        public readonly ushort Padding7A;
        public readonly Message ExitMessage;
        public readonly int ExitMsgParam1; // always 0
        public readonly int ExitMsgParam2; // always 0
        public readonly short ChildId; // always the same as ParentId
        public readonly ushort Cooldown;
        public readonly uint Priority; // always 0 or 1
        public readonly TriggerFlags TriggerFlags;
    }

    // size: 260
    public readonly struct FhAreaVolumeEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhTriggerType Subtype; // 0/1 - sphere/box
        public readonly FhRawCollisionVolume Box;
        public readonly FhRawCollisionVolume Sphere;
        public readonly FhRawCollisionVolume Cylinder;
        public readonly FhMessage InsideMessage;
        public readonly int InsideMsgParam1;
        public readonly FhMessage ExitMessage;
        public readonly int ExitMsgParam1;
        public readonly ushort Cooldown;
        public readonly ushort PaddingFA;
        public readonly FhTriggerFlags TriggerFlags;

        public FhRawCollisionVolume ActiveVolume
        {
            get
            {
                if (Subtype == FhTriggerType.Cylinder)
                {
                    return Cylinder;
                }
                if (Subtype == FhTriggerType.Box)
                {
                    return Box;
                }
                return Sphere;
            }
        }
    }

    // size: 148
    public readonly struct JumpPadEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly int ParentId;
        public readonly uint Unused28; // usually 0, occasionally 2
        public readonly RawCollisionVolume Volume;
        public readonly Vector3Fx BeamVector;
        public readonly Fixed Speed;
        public readonly ushort ControlLockTime;
        public readonly ushort CooldownTime;
        public readonly byte Active; // boolean
        public readonly byte Padding81;
        public readonly ushort Padding82;
        public readonly uint ModelId;
        public readonly uint BeamType; // always 0, has no imapct
        public readonly TriggerFlags TriggerFlags;

        public JumpPadEntityData(EntityDataHeader header, int parentId, RawCollisionVolume volume, Vector3Fx beamVector,
            Fixed speed, ushort controlLockTime, ushort cooldownTime, byte active, uint modelId, uint beamType, TriggerFlags triggerFlags)
        {
            Header = header;
            ParentId = parentId;
            Volume = volume;
            BeamVector = beamVector;
            Speed = speed;
            ControlLockTime = controlLockTime;
            CooldownTime = cooldownTime;
            Active = active;
            ModelId = modelId;
            BeamType = beamType;
            TriggerFlags = triggerFlags;
        }
    }

    // size: 272
    public readonly struct FhJumpPadEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhTriggerType VolumeType; // 0/1/2 - sphere/box/cylinder
        public readonly FhRawCollisionVolume Box;
        public readonly FhRawCollisionVolume Sphere;
        public readonly FhRawCollisionVolume Cylinder;
        public readonly uint CooldownTime;
        public readonly Vector3Fx BeamVector;
        public readonly Fixed Speed;
        public readonly uint ControlLockTime;
        public readonly uint ModelId;
        public readonly uint BeamType;
        public readonly FhTriggerFlags TriggerFlags;

        public FhRawCollisionVolume ActiveVolume
        {
            get
            {
                if (VolumeType == FhTriggerType.Sphere)
                {
                    return Sphere;
                }
                if (VolumeType == FhTriggerType.Box)
                {
                    return Box;
                }
                if (VolumeType == FhTriggerType.Cylinder)
                {
                    return Cylinder;
                }
                return default;
            }
        }
    }

    // size: 45
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct PointModuleEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly short NextId;
        public readonly short PrevId;
        public readonly byte Active; // boolean
    }

    // size: 104
    public readonly struct MorphCameraEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly RawCollisionVolume Volume;
    }

    // size: 104
    public readonly struct FhMorphCameraEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhRawCollisionVolume Volume;
    }

    // size: 41
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct OctolithFlagEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly byte TeamId;
    }

    // size: 108
    public readonly struct FlagBaseEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint TeamId;
        public readonly RawCollisionVolume Volume;
    }

    // size: 92
    public readonly struct TeleporterEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly byte LoadIndex;
        public readonly byte TargetIndex;
        public readonly byte ArtifactId;
        public readonly byte Active; // boolean
        public readonly byte Invisible; // bolean
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public readonly char[] EntityFilename;
        public readonly ushort Unused38; // always 0
        public readonly ushort Unused3A; // always UInt16.MaxValue
        public readonly Vector3Fx TargetPosition;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName;
    }

    // size: 104
    public readonly struct NodeDefenseEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly RawCollisionVolume Volume;
    }

    // size: 136
    public readonly struct LightSourceEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly RawCollisionVolume Volume;
        public readonly byte Light1Enabled; // boolean
        public readonly ColorRgb Light1Color; // 8-bit color values
        public readonly Vector3Fx Light1Vector;
        public readonly byte Light2Enabled;
        public readonly ColorRgb Light2Color;
        public readonly Vector3Fx Light2Vector;
    }

    // size: 70
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly struct ArtifactEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly byte ModelId;
        public readonly byte ArtifactId;
        public readonly byte Active; // boolean
        public readonly byte HasBase; // boolean
        public readonly short Message1Target;
        public readonly ushort Padding2A;
        public readonly Message Message1;
        public readonly short Message2Target;
        public readonly ushort Padding32;
        public readonly Message Message2;
        public readonly short Message3Target;
        public readonly ushort Padding3A;
        public readonly Message Message3;
        public readonly short LinkedEntityId; // always -1

        public ArtifactEntityData(EntityDataHeader header, byte modelId, byte artifactId, byte active,
            byte hasBase, short message1Target, Message message1, short message2Target, Message message2,
            short message3Target, Message message3, short linkedEntityId)
        {
            Header = header;
            ModelId = modelId;
            ArtifactId = artifactId;
            Active = active;
            HasBase = hasBase;
            Message1Target = message1Target;
            Message1 = message1;
            Message2Target = message2Target;
            Message2 = message2;
            Message3Target = message3Target;
            Message3 = message3;
            LinkedEntityId = linkedEntityId;
        }
    }

    // size: 64
    public readonly struct CameraSequenceEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly byte SequenceId;
        public readonly byte Handoff; // boolean
        public readonly byte Loop; // boolean
        public readonly byte BlockInput; // boolean
        public readonly byte ForceAltForm; // boolean
        public readonly byte ForceBipedForm; // boolean
        public readonly ushort DelayFrames;
        public readonly byte PlayerId1;
        public readonly byte PlayerId2;
        public readonly short Entity1;
        public readonly short Entity2;
        public readonly short EndMessageTargetId;
        public readonly Message EndMessage;
        public readonly int EndMessageParam;
    }

    // size: 53
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ForceFieldEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint Type; // 0-8 beam lock, 9 no lock
        public readonly Fixed Width;
        public readonly Fixed Height;
        public readonly byte Active; // boolean
    }

    // size: 96 (12 x 8)
    public readonly struct Vector3FxArray8
    {
        public readonly Vector3Fx Vector0;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly Vector3Fx Vector3;
        public readonly Vector3Fx Vector4;
        public readonly Vector3Fx Vector5;
        public readonly Vector3Fx Vector6;
        public readonly Vector3Fx Vector7;

        public Vector3Fx this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return Vector0;
                }
                else if (index == 1)
                {
                    return Vector1;
                }
                else if (index == 2)
                {
                    return Vector2;
                }
                else if (index == 3)
                {
                    return Vector3;
                }
                else if (index == 4)
                {
                    return Vector4;
                }
                else if (index == 5)
                {
                    return Vector5;
                }
                else if (index == 6)
                {
                    return Vector6;
                }
                else if (index == 7)
                {
                    return Vector7;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }

    // size: 120 (12 x 10)
    public readonly struct Vector3FxArray10
    {
        public readonly Vector3Fx Vector0;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly Vector3Fx Vector3;
        public readonly Vector3Fx Vector4;
        public readonly Vector3Fx Vector5;
        public readonly Vector3Fx Vector6;
        public readonly Vector3Fx Vector7;
        public readonly Vector3Fx Vector8;
        public readonly Vector3Fx Vector9;

        public Vector3Fx this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return Vector0;
                }
                else if (index == 1)
                {
                    return Vector1;
                }
                else if (index == 2)
                {
                    return Vector2;
                }
                else if (index == 3)
                {
                    return Vector3;
                }
                else if (index == 4)
                {
                    return Vector4;
                }
                else if (index == 5)
                {
                    return Vector5;
                }
                else if (index == 6)
                {
                    return Vector6;
                }
                else if (index == 7)
                {
                    return Vector7;
                }
                else if (index == 8)
                {
                    return Vector8;
                }
                else if (index == 9)
                {
                    return Vector9;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }

    // size: 160 (16 x 10)
    public readonly struct Vector4FxArray10
    {
        public readonly Vector4Fx Vector0;
        public readonly Vector4Fx Vector1;
        public readonly Vector4Fx Vector2;
        public readonly Vector4Fx Vector3;
        public readonly Vector4Fx Vector4;
        public readonly Vector4Fx Vector5;
        public readonly Vector4Fx Vector6;
        public readonly Vector4Fx Vector7;
        public readonly Vector4Fx Vector8;
        public readonly Vector4Fx Vector9;

        public Vector4Fx this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return Vector0;
                }
                else if (index == 1)
                {
                    return Vector1;
                }
                else if (index == 2)
                {
                    return Vector2;
                }
                else if (index == 3)
                {
                    return Vector3;
                }
                else if (index == 4)
                {
                    return Vector4;
                }
                else if (index == 5)
                {
                    return Vector5;
                }
                else if (index == 6)
                {
                    return Vector6;
                }
                else if (index == 7)
                {
                    return Vector7;
                }
                else if (index == 8)
                {
                    return Vector8;
                }
                else if (index == 9)
                {
                    return Vector9;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }

    // size: 160 (12 x 16)
    public readonly struct Vector3FxArray16
    {
        public readonly Vector3Fx Vector00;
        public readonly Vector3Fx Vector01;
        public readonly Vector3Fx Vector02;
        public readonly Vector3Fx Vector03;
        public readonly Vector3Fx Vector04;
        public readonly Vector3Fx Vector05;
        public readonly Vector3Fx Vector06;
        public readonly Vector3Fx Vector07;
        public readonly Vector3Fx Vector08;
        public readonly Vector3Fx Vector09;
        public readonly Vector3Fx Vector10;
        public readonly Vector3Fx Vector11;
        public readonly Vector3Fx Vector12;
        public readonly Vector3Fx Vector13;
        public readonly Vector3Fx Vector14;
        public readonly Vector3Fx Vector15;

        public Vector3Fx this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return Vector00;
                }
                else if (index == 1)
                {
                    return Vector01;
                }
                else if (index == 2)
                {
                    return Vector02;
                }
                else if (index == 3)
                {
                    return Vector03;
                }
                else if (index == 4)
                {
                    return Vector04;
                }
                else if (index == 5)
                {
                    return Vector05;
                }
                else if (index == 6)
                {
                    return Vector06;
                }
                else if (index == 7)
                {
                    return Vector07;
                }
                else if (index == 8)
                {
                    return Vector08;
                }
                else if (index == 9)
                {
                    return Vector09;
                }
                else if (index == 10)
                {
                    return Vector10;
                }
                else if (index == 11)
                {
                    return Vector11;
                }
                else if (index == 12)
                {
                    return Vector12;
                }
                else if (index == 13)
                {
                    return Vector13;
                }
                else if (index == 14)
                {
                    return Vector14;
                }
                else if (index == 15)
                {
                    return Vector15;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }

    // size: 32 (2 x 16)
    public readonly struct EntityLengthArray
    {
        public readonly ushort Length00;
        public readonly ushort Length01;
        public readonly ushort Length02;
        public readonly ushort Length03;
        public readonly ushort Length04;
        public readonly ushort Length05;
        public readonly ushort Length06;
        public readonly ushort Length07;
        public readonly ushort Length08;
        public readonly ushort Length09;
        public readonly ushort Length10;
        public readonly ushort Length11;
        public readonly ushort Length12;
        public readonly ushort Length13;
        public readonly ushort Length14;
        public readonly ushort Length15;

        public ushort this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return Length00;
                }
                else if (index == 1)
                {
                    return Length01;
                }
                else if (index == 2)
                {
                    return Length02;
                }
                else if (index == 3)
                {
                    return Length03;
                }
                else if (index == 4)
                {
                    return Length04;
                }
                else if (index == 5)
                {
                    return Length05;
                }
                else if (index == 6)
                {
                    return Length06;
                }
                else if (index == 7)
                {
                    return Length07;
                }
                else if (index == 8)
                {
                    return Length08;
                }
                else if (index == 9)
                {
                    return Length09;
                }
                else if (index == 10)
                {
                    return Length10;
                }
                else if (index == 11)
                {
                    return Length11;
                }
                else if (index == 12)
                {
                    return Length12;
                }
                else if (index == 13)
                {
                    return Length13;
                }
                else if (index == 14)
                {
                    return Length14;
                }
                else if (index == 15)
                {
                    return Length15;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }
}
