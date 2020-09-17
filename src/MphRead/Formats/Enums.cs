namespace MphRead
{
    public enum EntityType : ushort
    {
        Platform = 0,
        Object = 1,
        PlayerSpawn = 2,
        Door = 3,
        Item = 4,
        ItemInstance = 5,
        Enemy = 6,
        TriggerVolume = 7,
        AreaVolume = 8,
        JumpPad = 9,
        PointModule = 10, // FH leftovers only
        CameraPosition = 11,
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
        // 27 = entity list header
        // First Hunt
        FhPlayerSpawn = 101,
        FhDoor = 103,
        FhItem = 104,
        FhEnemy = 106,
        FhUnknown9 = 109,
        FhUnknown10 = 110,
        FhPlatform = 111,
        FhJumpPad = 112,
        FhPointModule = 113,
        FhCameraPosition = 114
    }

    public enum VolumeType : uint
    {
        Box = 0,
        Cylinder = 1,
        Sphere = 2
    }

    public enum FhVolumeType : uint
    {
        Unknown0 = 0,
        Box = 1,
        Unknown2 = 2,
        Unknown3 = 3
    }

    public enum ModelType
    {
        Generic,
        Room,
        Item,
        Object,
        Placeholder,
        JumpPad,
        JumpPadBeam
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

    public enum TextureFormat : ushort
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
        Splitter = 2,
        Automatic = 3,
        Unknown4 = 4
    }

    public enum Message : uint
    {
        None = 0,
        SetActive = 5,
        Destroyed = 6,
        Damage = 7,
        Unknown9 = 9,
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
        Unknown25 = 25,
        Unknown26 = 26, // UI message-related?
        Unknown27 = 27, // UI message-related?
        Unknown28 = 28, // UI message-related?
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
        Unknown45 = 45, // platform sleep?
        Unknown46 = 46,
        Unknown50 = 50,
        Unknown52 = 52,
        Unknown53 = 53,
        Unknown54 = 54,
        Unknown55 = 55,
        Unknown56 = 56,
        Checkpoint = 57,
        EscapeStart = 58,
        Unknown59 = 59,
        Unknown60 = 60,
        Unknown61 = 61 // also starts escape sequence?
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
}
