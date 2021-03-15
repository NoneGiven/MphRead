using System;
using System.Runtime.InteropServices;
using MphRead.Entities;

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
    }

    // size: 588
    public readonly struct PlatformEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint NoPort; // used as boolean, but some of the beam spawners in Frost Labyrinth have a value of 2
        public readonly uint ModelId;
        public readonly short ParentId;
        public readonly byte Field2E;
        public readonly byte Field2F;
        public readonly ushort ScanData1;
        public readonly short ScanMsgTarget;
        public readonly Message ScanMessage;
        public readonly ushort ScanData2;
        public readonly ushort Field3A;
        public readonly Vector3FxArray10 Positions;
        public readonly Vector4FxArray10 Rotations;
        public readonly Vector3Fx PositionOffset;
        public readonly uint Field160;
        public readonly uint Field164;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] PortalName;
        public readonly uint Field178;
        public readonly uint Field17C;
        public readonly uint Field180;
        public readonly PlatformFlags Flags;
        public readonly uint ContactDamage;
        public readonly Vector3Fx BeamSpawnDir;
        public readonly Vector3Fx BeamSpawnPos;
        public readonly int BeamId;
        public readonly uint BeamInterval;
        public readonly uint BeamOnIntervals; // 16 bits are used
        public readonly ushort Unused1B0; // always UInt16.MaxValue
        public readonly ushort Unused1B2; // always 0
        public readonly int EffectId1;
        public readonly uint Health;
        public readonly uint Field1BC;
        public readonly int EffectId2;
        public readonly int EffectId3;
        public readonly byte ItemChance;
        public readonly byte Padding1C9;
        public readonly ushort Padding1CA;
        public readonly uint ItemModel;
        public readonly uint Field1D0;
        public readonly uint Field1D4;
        public readonly uint Message1Target;
        public readonly Message Message1;
        public readonly uint Message1Param1;
        public readonly uint Message1Param2;
        public readonly uint Message2Target;
        public readonly Message Message2;
        public readonly uint Message2Param1;
        public readonly uint Message2Param2;
        public readonly uint Message3Target;
        public readonly Message Message3;
        public readonly uint Message3Param1;
        public readonly uint Message3Param2;
        public readonly ushort Field208;
        public readonly short Msg32Target1;
        public readonly Message Msg32Message1;
        public readonly uint Msg32Param11;
        public readonly uint Msg32Param21;
        public readonly ushort Field218;
        public readonly short Msg32Target2;
        public readonly Message Msg32Message2;
        public readonly uint Msg32Param12;
        public readonly uint Msg32Param22;
        public readonly ushort Field228;
        public readonly short Msg32Target3;
        public readonly Message Msg32Message3;
        public readonly uint Msg32Param13;
        public readonly uint Msg32Param23;
        public readonly ushort Field238;
        public readonly short Msg32Target4;
        public readonly Message Msg32Message4;
        public readonly uint Msg32Param14;
        public readonly uint Msg32Param24;
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
        public readonly byte Flags;
        public readonly byte Padding25;
        public readonly ushort Padding26;
        public readonly uint EffectFlags;
        public readonly uint ModelId;
        public readonly short LinkedEntity;
        public readonly ushort ScanId;
        public readonly short ScanMsgTarget;
        public readonly ushort Padding36;
        public readonly Message ScanMessage;
        public readonly uint EffectId;
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
        public readonly uint ModelId;
        public readonly uint TargetRoomId;
        public readonly byte TargetLayerId;
        public readonly byte Flags; // bit 0 - locked
        public readonly byte Field42;
        public readonly byte Field43;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] EntityFilename;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] RoomName;
    }

    // size: 64
    public readonly struct FhDoorEntityData
    {
        public readonly EntityDataHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] RoomName;
        public readonly uint Flags;
        public readonly uint ModelId;
    }

    // size: 72
    public readonly struct ItemSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint ParentId;
        public readonly ItemType ItemType;
        public readonly byte Enabled; // boolean
        public readonly byte HasBase; // boolean
        public readonly byte AlwaysActive; // boolean -- set flags bit 0 based on Active boolean only and ignore room state
        public readonly byte Padding2F;
        public readonly ushort MaxSpawnCount;
        public readonly ushort SpawnInterval;
        public readonly ushort SpawnDelay;
        public readonly short SomeEntityId; // todo: parent? child?
        public readonly Message CollectedMessage;
        public readonly uint CollectedMsgParam1;
        public readonly uint CollectedMsgParam2;
    }

    // size: 50
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct FhItemSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly FhItemType ItemType;
        public readonly ushort SpawnLimit;
        public readonly ushort CooldownTime;
        public readonly ushort Field2C;
    }

    // size: 512
    public readonly struct EnemySpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly EnemyType Type;
        public readonly byte Padding25; // in-game, the type is 4 bytes on this struct (but is 1 byte on the class),
        public readonly ushort Padding26; // so this padding isn't actually there
        public readonly uint Subtype;
        public readonly uint TextureId;
        public readonly uint HunterWeapon;
        public readonly ushort Health;
        public readonly ushort HealthMax;
        public readonly ushort Field38;
        public readonly byte Field3A;
        public readonly byte Field3B;
        // union start
        public readonly uint Field3C;
        public readonly uint Field40;
        public readonly uint Field44;
        public readonly uint Field48;
        public readonly uint Field4C;
        public readonly uint Field50;
        public readonly uint Field54;
        public readonly uint Field58;
        public readonly uint Field5C;
        public readonly uint Field60;
        public readonly uint Field64;
        public readonly uint Field68;
        public readonly uint Field6C;
        public readonly uint Field70;
        public readonly uint Field74;
        public readonly uint Field78;
        public readonly uint Field7C;
        public readonly uint Field80;
        public readonly uint Field84;
        public readonly uint Field88;
        public readonly uint Field8C;
        public readonly uint Field90;
        public readonly uint Field94;
        public readonly uint Field98;
        public readonly uint Field9C;
        public readonly uint FieldA0;
        public readonly uint FieldA4;
        public readonly uint FieldA8;
        public readonly uint FieldAC;
        public readonly uint FieldB0;
        public readonly uint FieldB4;
        public readonly uint FieldB8;
        public readonly uint FieldBC;
        public readonly uint FieldC0;
        public readonly uint FieldC4;
        public readonly uint FieldC8;
        public readonly uint FieldCC;
        public readonly uint FieldD0;
        public readonly uint FieldD4;
        public readonly uint FieldD8;
        public readonly uint FieldDC;
        public readonly uint FieldE0;
        public readonly uint FieldE4;
        public readonly uint FieldE8;
        public readonly uint FieldEC;
        public readonly uint FieldF0;
        public readonly uint FieldF4;
        public readonly uint FieldF8;
        public readonly uint FieldFC;
        public readonly uint Field100;
        public readonly uint Field104;
        public readonly uint Field108;
        public readonly uint Field10C;
        public readonly uint Field110;
        public readonly uint Field114;
        public readonly uint Field118;
        public readonly uint Field11C;
        public readonly uint Field120;
        public readonly uint Field124;
        public readonly uint Field128;
        public readonly uint Field12C;
        public readonly uint Field130;
        public readonly uint Field134;
        public readonly uint Field138;
        public readonly uint Field13C;
        public readonly uint Field140;
        public readonly uint Field144;
        public readonly uint Field148;
        public readonly uint Field14C;
        public readonly uint Field150;
        public readonly uint Field154;
        public readonly uint Field158;
        public readonly uint Field15C;
        public readonly uint Field160;
        public readonly uint Field164;
        public readonly uint Field168;
        public readonly uint Field16C;
        public readonly uint Field170;
        public readonly uint Field174;
        public readonly uint Field178;
        public readonly uint Field17C;
        public readonly uint Field180;
        public readonly uint Field184;
        public readonly uint Field188;
        public readonly uint Field18C;
        public readonly uint Field190;
        public readonly uint Field194;
        public readonly uint Field198;
        public readonly uint Field19C;
        public readonly uint Field1A0;
        public readonly uint Field1A4;
        public readonly uint Field1A8;
        public readonly uint Field1AC;
        public readonly uint Field1B0;
        public readonly uint Field1B4;
        // union end
        public readonly ushort Field1B8;
        public readonly byte SomeLimit;
        public readonly byte Field1BB;
        public readonly byte SpawnCount;
        public readonly byte Active; // boolean
        public readonly byte AlwaysActive; // boolean
        public readonly byte ItemChance;
        public readonly ushort SpawnerModel;
        public readonly ushort CooldownTime;
        public readonly ushort InitialCooldown;
        public readonly ushort Padding1C6;
        public readonly Fixed ActiveDistance; // todo: display sphere
        public readonly uint Field1CC;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName;
        public readonly short EntityId1;
        public readonly ushort Field1E2; // todo: padding?
        public readonly Message Message1;
        public readonly short EntityId2;
        public readonly ushort Field1EA;
        public readonly Message Message2;
        public readonly short EntityId3;
        public readonly ushort Field1F2;
        public readonly Message Message3;
        public readonly uint ItemModel;
    }

    // size: 268
    public readonly struct FhEnemySpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly uint Field38;
        public readonly uint Field3C;
        public readonly uint Field40;
        public readonly uint Field44;
        public readonly uint Field48;
        public readonly uint Field4C;
        public readonly uint Field50;
        public readonly uint Field54;
        public readonly uint Field58;
        public readonly uint Field5C;
        public readonly uint Field60;
        public readonly uint Field64;
        public readonly uint Field68;
        public readonly uint Field6C;
        public readonly uint Field70;
        public readonly uint Field74;
        public readonly uint Field78;
        public readonly uint Field7C;
        public readonly uint Field80;
        public readonly uint Field84;
        public readonly uint Field88;
        public readonly uint Field8C;
        public readonly uint Field90;
        public readonly uint Field94;
        public readonly uint Field98;
        public readonly uint Field9C;
        public readonly uint FieldA0;
        public readonly uint FieldA4;
        public readonly uint FieldA8;
        public readonly uint FieldAC;
        public readonly uint FieldB0;
        public readonly uint FieldB4;
        public readonly uint FieldB8;
        public readonly uint FieldBC;
        public readonly uint FieldC0;
        public readonly uint FieldC4;
        public readonly uint FieldC8;
        public readonly uint FieldCC;
        public readonly uint FieldD0;
        public readonly uint FieldD4;
        public readonly uint FieldD8;
        public readonly uint FieldDC;
        public readonly uint FieldE0;
        public readonly uint EnemyType;
        public readonly byte SpawnTotal;
        public readonly byte SpawnLimit;
        public readonly byte SpawnCount;
        public readonly byte FieldEB;
        public readonly ushort Cooldown;
        public readonly ushort FieldEE;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName;
        public readonly short ParentId;
        public readonly ushort Field102;
        public readonly FhMessage EmptyMessage;
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
        public readonly ushort TriggerFlags; // in-game this is treated as uint, but the extra bits are never set/checked
        public readonly ushort Padding76;
        public readonly uint TriggerThreshold; // for subtype 1
        public readonly short ParentId;
        public readonly ushort Padding7E;
        public readonly Message ParentMessage;
        public readonly uint ParentMsgParam1;
        public readonly uint ParentMsgParam2;
        public readonly short ChildId;
        public readonly ushort Padding8E;
        public readonly Message ChildMessage;
        public readonly uint ChildMsgParam1;
        public readonly uint ChildMsgParam2;
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
        public readonly uint Flags;
        public readonly uint Threshold;
        public readonly short ParentId;
        public readonly ushort PaddingF6;
        public readonly FhMessage ParentMessage;
        public readonly uint ParentMsgParam1;
        public readonly short ChildId;
        public readonly ushort Padding102;
        public readonly FhMessage ChildMessage;
        public readonly uint ChildMsgParam1;

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
        public readonly uint InsideMsgParam1; // seconds for escape sequence, gravity/jump assist values, etc.
        public readonly uint InsideMsgParam2; // always 0 except for type 15, where it's always 2
        public readonly short ParentId;
        public readonly ushort Padding7A;
        public readonly Message ExitMessage;
        public readonly uint ExitMsgParam1; // always 0
        public readonly uint ExitMsgParam2; // always 0
        public readonly short ChildId; // always the same as ParentId
        public readonly ushort Cooldown;
        public readonly uint Priority; // always 0 or 1
        public readonly uint Flags; // 0x200 = affects biped, 0x400 = affects alt
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
        public readonly uint InsideMsgParam1;
        public readonly FhMessage ExitMessage;
        public readonly uint ExitMsgParam1;
        public readonly ushort Cooldown;
        public readonly ushort PaddingFA;
        public readonly uint Flags;

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
        public readonly uint ParentId;
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
        public readonly uint Flags;
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
        public readonly uint FieldFC;
        public readonly uint ModelId;
        public readonly uint BeamType;
        public readonly uint Flags;

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
        public readonly byte Field24;
        public readonly byte Field25;
        public readonly byte ArtifactId;
        public readonly byte Active; // boolean
        public readonly byte Invisible; // bolean
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public readonly char[] TargetRoom;
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
        public readonly uint Message1Id;
        public readonly short Message2Target;
        public readonly ushort Padding32;
        public readonly uint Message2Id;
        public readonly short Message3Target;
        public readonly ushort Padding3A;
        public readonly uint Message3Id;
        public readonly short LinkedEntityId; // always -1
    }

    // size: 64
    public readonly struct CameraSequenceEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly byte SequenceId;
        public readonly byte Field25;
        public readonly byte Loop; // boolean
        public readonly byte Field27;
        public readonly byte Field28;
        public readonly byte Field29;
        public readonly ushort DelayFrames;
        public readonly byte PlayerId1;
        public readonly byte PlayerId2;
        public readonly short Entity1;
        public readonly short Entity2;
        public readonly short MessageTargetId;
        public readonly Message Message;
        public readonly uint MessageParam;
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
