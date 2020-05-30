using System.Runtime.InteropServices;

namespace MphRead
{
    public static class Sizes
    {
        public static readonly int Header = Marshal.SizeOf(typeof(Header));
        public static readonly int EntityHeader = Marshal.SizeOf(typeof(EntityHeader));
        public static readonly int EntityEntry = Marshal.SizeOf(typeof(EntityEntry));
        public static readonly int EntityDataHeader = Marshal.SizeOf(typeof(EntityDataHeader));
        public static readonly int JumpPadEntityData = Marshal.SizeOf(typeof(JumpPadEntityData));
        public static readonly int ItemEntityData = Marshal.SizeOf(typeof(ItemEntityData));
    }

    public readonly struct TextureData
    {
        public readonly uint Data;
        public readonly byte Alpha;

        public TextureData(uint data, byte alpha)
        {
            Data = data;
            Alpha = alpha;
        }
    }

    public readonly struct PaletteData
    {
        public readonly ushort Data;

        public PaletteData(ushort data)
        {
            Data = data;
        }
    }

    // size: 4
    public readonly struct Mesh
    {
        public readonly ushort MaterialId;
        public readonly ushort DlistId;
    }

    // size: 32
    public readonly struct DisplayList
    {
        public readonly uint Offset;
        public readonly uint Size;
        public readonly int XMinimum;
        public readonly int YMinimum;
        public readonly int ZMinimum;
        public readonly int XMaximum;
        public readonly int YMaximum;
        public readonly int ZMaximum;
    }

    // size: 132
    public readonly struct RawMaterial
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public readonly string Name;
        public readonly byte Light;
        public readonly CullingMode Culling;
        public readonly byte Alpha;
        public readonly byte Wireframe;
        public readonly ushort PaletteId;
        public readonly ushort TextureId;
        public readonly RepeatMode XRepeat;
        public readonly RepeatMode YRepeat;
        public readonly ColorRgb Diffuse;
        public readonly ColorRgb Ambient;
        public readonly ColorRgb Specular;
        public readonly byte Field53;
        public readonly PolygonMode PolygonMode;
        public readonly RenderMode RenderMode;
        public readonly byte AnimationFlags;
        public readonly ushort Field5A;
        public readonly uint TexcoordTransformMode;
        public readonly ushort TexcoordAnimationId;
        public readonly ushort Field62;
        public readonly uint MatrixId;
        public readonly Fixed ScaleS;
        public readonly Fixed ScaleT;
        public readonly ushort RotZ;
        public readonly ushort Field72;
        public readonly Fixed TranslateS;
        public readonly Fixed TranslateT;
        public readonly ushort MaterialAnimationId;
        public readonly ushort Field7E;
        public readonly byte PackedRepeatMode;
        public readonly byte Field81;
        public readonly ushort Field82;
    }

    // size: 40
    public readonly struct Texture
    {
        public readonly TextureFormat Format;
        public readonly ushort Width;
        public readonly ushort Height;
        public readonly ushort Padding;
        public readonly uint ImageOffset;
        public readonly uint ImageSize;
        public readonly uint Unknown7;
        public readonly uint Unknown8;
        public readonly uint VramOffset;
        public readonly uint Opaque;
        public readonly uint Unknown11;
        public readonly byte PackedSize;
        public readonly byte NativeTextureFormat;
        public readonly ushort TextureObjRef;
    }

    // size: 16
    public readonly struct Palette
    {
        public readonly uint Offset;
        public readonly uint Count;
        public readonly uint Unknown4;
        public readonly uint UnknownReference5;
    }

    // size: 100
    public readonly struct Header
    {
        public readonly uint ScaleFactor;
        public readonly Fixed ScaleBase;
        public readonly uint Unknown3;
        public readonly uint Unknown4;
        public readonly uint MaterialOffset;
        public readonly uint DlistOffset;
        public readonly uint NodeOffset;
        public readonly ushort NodeAnimationCount;
        public readonly byte Flags;
        public readonly byte Field1F;
        public readonly uint UnknownNodeId;
        public readonly uint MeshOffset;
        public readonly ushort TextureCount;
        public readonly ushort Field2A;
        public readonly uint TextureOffset;
        public readonly ushort PaletteCount;
        public readonly ushort Field32;
        public readonly uint PaletteOffset;
        public readonly uint UnknownAnimationCount;
        public readonly uint Unknown8;
        public readonly uint NodeInitialPosition;
        public readonly uint NodePosition;
        public readonly ushort MaterialCount;
        public readonly ushort NodeCount;
        public readonly uint TextureMatrices;
        public readonly uint NodeAnimation;
        public readonly uint TextureCoordinateAnimations;
        public readonly uint MaterialAnimations;
        public readonly uint TextureAnimations;
        public readonly ushort MeshCount;
        public readonly ushort MatrixCount;
    }

    // size: ?
    public readonly struct RawNode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public readonly string Name;
        public readonly ushort ParentId;
        public readonly ushort ChildId;
        public readonly ushort NextId;
        public readonly ushort Field46;
        public readonly uint Enabled;
        public readonly ushort MeshCount;
        public readonly ushort MeshId;
        public readonly Vector3Fx Scale;
        public readonly short AngleX;
        public readonly short AngleY;
        public readonly short AngleZ;
        public readonly ushort Field62;
        public readonly Vector3Fx Position;
        public readonly uint Field70;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly byte Type; // todo: enum or whatever
        public readonly byte Field8D;
        public readonly ushort Field8E;
        public readonly Matrix43Fx Transform; // scratch space
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
    }
}
