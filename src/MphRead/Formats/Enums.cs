namespace MphRead
{
    public enum ModelType
    {
        Generic,
        Room,
        Item,
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
        DirectRgb = 3,   // RGB -- not entirely sure if this is RGB or RGBA; the alpha bit is always 1 for format 5
        PaletteA5I3 = 4, // A5I3 
        DirectRgba = 5,  // RGBA
        PaletteA3I5 = 6  // A3I5
    }
}
