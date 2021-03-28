using System;

namespace MphRead
{
    public enum EntityType : ushort
    {
        Platform = 0,
        Object = 1,
        PlayerSpawn = 2,
        Door = 3,
        ItemSpawn = 4,
        ItemInstance = 5,
        EnemySpawn = 6,
        TriggerVolume = 7,
        AreaVolume = 8,
        JumpPad = 9,
        PointModule = 10, // FH leftovers only
        MorphCamera = 11,
        OctolithFlag = 12,
        FlagBase = 13,
        Teleporter = 14,
        NodeDefense = 15,
        LightSource = 16,
        Artifact = 17,
        CameraSequence = 18,
        ForceField = 19,
        // 20 = missing?
        BeamEffect = 21,
        Bomb = 22,
        EnemyInstance = 23,
        Halfturret = 24,
        Player = 25,
        BeamProjectile = 26,
        ListHead = 27,
        // First Hunt
        FhUnknown0 = 100,
        FhPlayerSpawn = 101,
        FhUnknown2 = 102,
        FhDoor = 103,
        FhItemSpawn = 104,
        FhItemInstance = 105,
        FhEnemySpawn = 106,
        FhEffectInstance = 107,
        FhBomb = 108,
        FhTriggerVolume = 109,
        FhAreaVolume = 110,
        FhPlatform = 111,
        FhJumpPad = 112,
        FhPointModule = 113,
        FhMorphCamera = 114,
        FhEnemyInstance = 115,
        FhPlayer = 116,
        FhBeamProjectile = 117,
        // 118 = entity list header
        // viewer only
        Room = 200,
        Model = 201,
        All = 255
    }

    public enum VolumeType : uint
    {
        Box = 0,
        Cylinder = 1,
        Sphere = 2
    }

    public enum FhVolumeType : uint
    {
        Sphere = 0,
        Box = 1,
        Cylinder = 2
    }

    public enum DoorType : uint
    {
        Standard = 0,
        MorphBall = 1,
        Boss = 2,
        Thin = 3
    }

    public enum ItemType : int
    {
        None = -1,
        HealthMedium = 0,
        HealthSmall = 1,
        HealthBig = 2,
        DoubleDamage = 3,
        EnergyTank = 4,
        VoltDriver = 5,
        MissileExpansion = 6,
        Battlehammer = 7,
        Imperialist = 8,
        Judicator = 9,
        Magmaul = 10,
        ShockCoil = 11,
        OmegaCannon = 12,
        UASmall = 13,
        UABig = 14,
        MissileSmall = 15,
        MissileBig = 16,
        Cloak = 17,
        UAExpansion = 18,
        ArtifactKey = 19,
        Deathalt = 20,
        AffinityWeapon = 21,
        PickWpnMissile = 22
    }

    public enum FhItemType : uint
    {
        AmmoSmall = 0,
        AmmoBig = 1,
        HealthSmall = 2,
        HealthBig = 3,
        DoubleDamage = 4,
        PickMorphBall = 5,
        ElectroLob = 6,
        Missile = 7
    }

    public enum BeamType : byte
    {
        PowerBeam = 0,
        VoltDriver = 1,
        Missile = 2,
        Battlehamer = 3,
        Imperialist = 4,
        Judicator = 5,
        Magmaul = 6,
        ShockCoil = 7,
        OmegaCannon = 8,
        Platform = 9,
        Enemy = 10
    }

    public enum BombType : byte
    {
        MorphBall = 0,
        Stinglarva = 1,
        Lockjaw = 2
    }

    [Flags]
    public enum Affliction : byte
    {
        None = 0,
        Freeze = 1,
        Disrupt = 2,
        Burn = 4
    }

    public enum FadeType : byte
    {
        None = 0,
        FadeInBlack = 1,
        FadeOutBlack = 2,
        FadeInWhite = 3,
        FadeOutWhite = 4,
        FadeOutInBlack = 5,
        FadeOutInWhite = 6
    }

    public enum Terrain
    {
        Metal = 0,
        OrangeHolo = 1,
        GreenHolo = 2,
        BlueHolo = 3,
        Ice = 4,
        Snow = 5,
        Sand = 6,
        Rock = 7,
        Lava = 8,
        Acid = 9,
        Gorea = 10,
        Unknown11 = 11, // unused
        All = 12 // viewer only
    }

    public enum ModelType
    {
        Generic,
        Room,
        Item,
        Object,
        Placeholder,
        JumpPad,
        JumpPadBeam,
        Enemy,
        Player,
        Platform
    }

    public enum Team
    {
        None,
        Orange,
        Green
    }

    public enum BillboardMode : byte
    {
        None = 0,
        Sphere = 1,
        Cylinder = 2
    }

    public enum PolygonMode : uint
    {
        Modulate = 0,
        Decal = 1,
        Toon = 2, // MPH only uses this in "highlight" shading mode
        Shadow = 3 // unused
    }

    public enum RepeatMode : byte
    {
        Clamp = 0,
        Repeat = 1,
        Mirror = 2
    }

    public enum RenderMode : byte
    {
        Normal = 0,
        Decal = 1,
        Translucent = 2,
        Unknown3 = 3,
        Unknown4 = 4
    }

    public enum TexgenMode : uint
    {
        None = 0,
        Texcoord = 1,
        Normal = 2,
        Vertex = 3 // unused
    }

    public enum CullingMode : byte
    {
        Neither = 0,
        Front = 1,
        Back = 2
    }

    public enum TextureFormat : byte
    {
        Palette2Bit = 0, // RGB4
        Palette4Bit = 1, // RGB16
        Palette8Bit = 2, // RGB256
        PaletteA5I3 = 4, // A5I3 
        DirectRgb = 5,   // RGB
        PaletteA3I5 = 6  // A3I5
    }

    public enum TriggerType : uint
    {
        Normal = 0,
        Threshold = 1,
        Relay = 2,
        Automatic = 3,
        StateBits = 4
    }

    public enum FhTriggerType : uint
    {
        Sphere = 0,
        Box = 1,
        Cylinder = 2, // not used for area volumes -- game code mistakenly copies data from box volume
        Threshold = 3 // not used for area volumes
    }

    public enum Message : uint
    {
        None = 0,
        SetActive = 5,
        Destroyed = 6,
        Damage = 7,
        Trigger = 9,
        Unknown12 = 12,
        Gravity = 15,
        Unknown16 = 16, // unlock?
        Unknown17 = 17, // lock?
        Activate = 18,
        Unknown19 = 19, // match over?
        Impact = 20,
        Death = 21,
        Unknown22 = 22,
        SavePoint = 23,
        Unknown24 = 24,
        Unknown25 = 25,
        ShowPrompt = 26,
        ShowWarning = 27,
        ShowOverlay = 28,
        Unknown29 = 29, // spawn item here?
        Unknown30 = 30,
        Unknown31 = 31, // platform collision?
        Unknown32 = 32,
        Unknown33 = 33, // unlock all?
        Unknown34 = 34, // lock all?
        Unknown35 = 35,
        Unknown36 = 36,
        Unknown41 = 41,
        Unknown42 = 42,
        Unknown43 = 43,
        Unknown44 = 44, // platform wakeup?
        Unknown45 = 45, // platform sleep? also checked by enemies
        DripMoatPlatform = 46,
        Unknown48 = 48,
        Unknown49 = 49,
        Unknown50 = 50,
        Unknown51 = 51,
        SetBeamReflection = 52, // only checked by platforms, probably never sent, and not relevant either way
        Unknown53 = 53,
        Unknown54 = 54,
        Unknown55 = 55,
        UnlockOubliette = 56,
        Checkpoint = 57,
        EscapeStart = 58,
        Unknown59 = 59,
        Unknown60 = 60,
        EscapeCancel = 61
    }

    public enum FhMessage : uint
    {
        None = 0,
        Activate = 5,
        Destroyed = 6,
        Damage = 7,
        Unknown9 = 9,
        Unknown15 = 15,
        Unlock = 16,
        SetActive = 17,
        Complete = 18,
        Unknown19 = 19,
        Death = 20,
        Unknown21 = 21
    }

    public enum EnemyType : byte // see note on enemy spawn entity struct
    {
        WarWasp = 0,
        Zoomer = 1,
        Temroid = 2,
        Petrasyl1 = 3,
        Petrasyl2 = 4,
        Petrasyl3 = 5,
        Petrasyl4 = 6,
        Unknown7 = 7, // unused
        Unknown8 = 8, // unused
        Unknown9 = 9, // unused
        BarbedWarWasp = 10,
        Shriekbat = 11,
        Geemer = 12,
        Unknown13 = 13, // unused
        Unknown14 = 14, // unused
        Unknown15 = 15, // unused
        Blastcap = 16,
        Unknown17 = 17, // unused
        AlimbicTurret = 18,
        Cretaphid = 19,
        CretaphidEye = 20,
        CretaphidCrystal = 21,
        Unknown22 = 22, // unused (Cretaphid-related)
        PsychoBit1 = 23,
        Gorea1A = 24,
        GoreaHead = 25,
        GoreaArm = 26,
        GoreaLeg = 27,
        Gorea1B = 28,
        GoreaSealSphere1 = 29,
        Trocra = 30,
        Gorea2 = 31,
        GoreaSealSphere2 = 32,
        GoreaMeteor = 33,
        PsychoBit2 = 34,
        Voldrum2 = 35,
        Voldrum1 = 36,
        Quadtroid = 37,
        CrashPillar = 38,
        FireSpawn = 39,
        Spawner = 40,
        Slench = 41,
        SlenchShield = 42,
        SlenchNest = 43,
        SlenchSynapse = 44,
        SlenchTurret = 45,
        LesserIthrak = 46,
        GreaterIthrak = 47,
        Hunter = 48,
        ForceFieldLock = 49,
        WeakSpot = 50, // used by 39/46/47
        CarnivorousPlant = 51
    }

    public enum Hunter : byte
    {
        Samus = 0,
        Kanden = 1,
        Trace = 2,
        Sylux = 3,
        Noxus = 4,
        Spire = 5,
        Weavel = 6,
        Guardian = 7,
        Random = 8
    }

    public enum Language
    {
        English,
        French,
        German,
        Italian,
        Japanese,
        Spanish
    }

    public enum WaveFormat
    {
        None = -1,
        PCM8 = 0,
        PCM16 = 1,
        ADPCM = 2,
    }

    public enum SingleType
    {
        Death,
        Fuzzball
    }
}
