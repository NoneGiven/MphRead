namespace MphRead
{
    // seems like 5 and 10 are unused
    // -- not sure if/where 26 (EnergyBeam) is used
    public enum EntityType : ushort
    {
        Platform = 0,
        Object = 1,
        PlayerSpawn = 2,
        Door = 3,
        Item = 4,
        ItemInstance = 5,
        Enemy = 6,
        Unknown7 = 7,
        Unknown8 = 8,
        JumpPad = 9,
        CameraPos = 11,
        Unknown12 = 12,
        Unknown13 = 13,
        Teleporter = 14,
        Unknown15 = 15,
        Unknown16 = 16,
        Artifact = 17,
        CameraSeq = 18,
        ForceField = 19,
        EnemyInstance = 23,
        EnergyBeam = 26,
        EntityListHead = 27
    }

    public enum ModelType
    {
        Generic,
        Room,
        Item,
        Object,
        Placeholder
    }

    public enum PolygonMode : uint
    {
        Modulate = 0,
        Decal = 1,
        Toon = 2,
        Shadow = 3
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
        Unknown4 = 4,
        AlphaTest = 5 // viewer only
    }

    public enum TexgenMode : uint
    {
        None = 0,
        Texcoord = 1,
        Normal = 2,
        Vertex = 3
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
        DirectRgba = 3,  // RGBA
        PaletteA5I3 = 4, // A5I3 
        DirectRgb = 5,   // RGB
        PaletteA3I5 = 6  // A3I5
    }
}
