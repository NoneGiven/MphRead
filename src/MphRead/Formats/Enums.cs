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
        Unknown24 = 24,
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
}
