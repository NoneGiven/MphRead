using System;
using System.Runtime.InteropServices;

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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string NodeName; // todo: probably used for partial room visibility
        public readonly short LayerMask;
        public readonly ushort Length;
        public readonly uint DataOffset;
    }

    // size: 20
    public readonly struct FhEntityEntry
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string NodeName; // todo: same as above
        public readonly uint DataOffset;
    }

    // size: 4
    public readonly struct EntityDataHeader
    {
        public readonly ushort Type;
        public readonly ushort EntityId; // counts up
    }

    // size: 588
    public readonly struct PlatformEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly uint Field24;
        public readonly uint ModelId;
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
        public readonly uint Field1B8;
        public readonly uint Field1BC;
        public readonly uint Field1C0;
        public readonly uint Field1C4;
        public readonly uint Field1C8;
        public readonly uint Field1CC;
        public readonly uint Field1D0;
        public readonly uint Field1D4;
        public readonly uint Field1D8;
        public readonly uint Field1DC;
        public readonly uint Field1E0;
        public readonly uint Field1E4;
        public readonly uint Field1E8;
        public readonly uint Field1EC;
        public readonly uint Field1F0;
        public readonly uint Field1F4;
        public readonly uint Field1F8;
        public readonly uint Field1FC;
        public readonly uint Field200;
        public readonly uint Field204;
        public readonly uint Field208;
        public readonly uint Field20C;
        public readonly uint Field210;
        public readonly uint Field214;
        public readonly uint Field218;
        public readonly uint Field21C;
        public readonly uint Field220;
        public readonly uint Field224;
        public readonly uint Field228;
        public readonly uint Field22C;
        public readonly uint Field230;
        public readonly uint Field234;
        public readonly uint Field238;
        public readonly uint Field23C;
        public readonly uint Field240;
        public readonly uint Field244;
    }

    // size: 236
    public readonly struct FhPlatformEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
        public readonly uint FieldE4;
    }

    // size: 152
    public readonly struct ObjectEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly uint Flags;
        public readonly uint FxFlags;
        public readonly uint ModelId;
        public readonly ushort LinkedEntity;
        public readonly ushort ScanId;
        public readonly ushort Field34;
        public readonly ushort Field36;
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
    }

    // size: 43
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct PlayerSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly ushort Field24;
        public readonly byte Field26;
    }

    // size: 44
    public readonly struct FhPlayerSpawnEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly ushort Field24;
        public readonly ushort Field26;
    }

    // size: 104
    public readonly struct DoorEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx Vector2;
        public readonly Vector3Fx Rotation;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string NodeName;
        public readonly uint PaletteId;
        public readonly uint ModelId;
        public readonly uint Field3C;
        public readonly byte TargetLayerId;
        public readonly byte Flags; // sktodo: use these for some stuff
        public readonly byte Field42;
        public readonly byte Field43;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string EntityFilename;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string RoomName;
        //public readonly uint Field64; // sktodo: does this exist?
    }

    // size: 64
    public readonly struct FhDoorEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx Vector2;
        public readonly Vector3Fx Rotation;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly uint Field38;
    }

    // size: 72
    public readonly struct ItemEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint FieldC;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint ItemId;
        public readonly uint ModelId;
        public readonly byte Enabled; // boolean
        public readonly byte HasBase; // boolean
        public readonly byte Field2E;
        public readonly byte Field2F;
        public readonly ushort MaxSpawnCount;
        public readonly ushort SpawnInterval;
        public readonly ushort SpawnDelay;
        public readonly ushort EntityId;
        public readonly uint Field38;
        public readonly uint Field3C;
        public readonly uint Field40;
    }

    // size: 50
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct FhItemEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint ModelId;
        public readonly uint Field28;
        public readonly ushort Field2C;
    }

    // size: 512
    public readonly struct EnemyEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
        public readonly uint Field1B8;
        public readonly uint Field1BC;
        public readonly uint Field1C0;
        public readonly uint Field1C4;
        public readonly uint Field1C8;
        public readonly uint Field1CC;
        public readonly uint Field1D0;
        public readonly uint Field1D4;
        public readonly uint Field1D8;
        public readonly uint Field1DC;
        public readonly uint Field1E0;
        public readonly uint Field1E4;
        public readonly uint Field1E8;
        public readonly uint Field1EC;
        public readonly uint Field1F0;
        public readonly uint Field1F4;
        public readonly uint Field1F8;
    }

    // size: 268
    public readonly struct FhEnemyEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
        public readonly uint FieldE4;
        public readonly uint FieldE8;
        public readonly uint FieldEC;
        public readonly uint FieldF0;
        public readonly uint FieldF4;
        public readonly uint FieldF8;
        public readonly uint FieldFC;
        public readonly uint Field100;
        public readonly uint Field104;
    }

    // size: 160
    public readonly struct Unknown7EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 272
    public readonly struct FhUnknown9EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 152
    public readonly struct Unknown8EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 260
    public readonly struct FhUnknown10EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
        public readonly uint FieldE4;
        public readonly uint FieldE8;
        public readonly uint FieldEC;
        public readonly uint FieldF0;
        public readonly uint FieldF4;
        public readonly uint FieldF8;
        public readonly uint FieldFC;
    }

    // size: 148
    public readonly struct JumpPadEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx BaseVector1;
        public readonly Vector3Fx BaseVector2;
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
        public readonly Vector3Fx BeamVector;
        public readonly Fixed Speed;
        public readonly short Field7C;
        public readonly ushort Field7E;
        public readonly byte Field80;
        public readonly byte Field81;
        public readonly short Field82;
        public readonly uint ModelId;
        public readonly uint BeamType;
        public readonly uint Field8C;
    }

    // size: 272
    public readonly struct FhJumpPadEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx BaseVector1;
        public readonly Vector3Fx BaseVector2;
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
        public readonly uint FieldE4;
        public readonly uint FieldE8;
        public readonly Vector3Fx BeamVector;
        public readonly Fixed Speed;
        public readonly uint FieldFC;
        public readonly uint ModelId;
        public readonly uint BeamType;
        public readonly uint Field108;
    }

    // size: 45
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct PointModuleEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly byte Field28;
    }

    // todo: might be interchangeable with the MPH version
    // size: 45
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct FhPointModuleEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly byte Field28;
    }

    // size: 104
    public readonly struct CameraPosEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // todo: might be interchangeable with the MPH version
    // size: 104
    public readonly struct FhCameraPosEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 41
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Unknown12EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly byte Field24;
    }

    // size: 108
    public readonly struct Unknown13EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 92
    public readonly struct TeleporterEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly byte Field24;
        public readonly byte Field25;
        public readonly byte ArtifactId;
        public readonly byte Field27;
        public readonly byte Invisible;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public readonly string TargetEntity;
        public readonly uint Field38;
        public readonly Vector3Fx Field3C;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string NodeName;
        //public readonly uint Field58; // sktodo: does this exist?
    }

    // size: 104
    public readonly struct Unknown15EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 136
    public readonly struct Unknown16EntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
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
    }

    // size: 70
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly struct ArtifactEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly uint Field38;
        public readonly uint Field3C;
        public readonly ushort Field40;
    }

    // size: 64
    public readonly struct CameraSeqEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly uint Field38;
    }

    // size: 53
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ForceFieldEntityData
    {
        public readonly EntityDataHeader Header;
        public readonly Vector3Fx Position;
        public readonly uint Field0C;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly byte Field30;
    }

    // size: 32
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
