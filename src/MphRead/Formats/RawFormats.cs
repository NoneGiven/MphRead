using System.Runtime.InteropServices;

namespace MphRead
{
    public static class Sizes
    {
        public static readonly int Header = Marshal.SizeOf(typeof(Header));
        public static readonly int EntityHeader = Marshal.SizeOf(typeof(EntityHeader));
        public static readonly int EntityEntry = Marshal.SizeOf(typeof(EntityEntry));
        public static readonly int FhEntityEntry = Marshal.SizeOf(typeof(FhEntityEntry));
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
    public readonly struct RawMesh
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
        public readonly byte Lighting;
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
        public readonly TexgenMode TexcoordTransformMode;
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

    // size: 24
    public readonly struct AnimationHeader
    {
        public readonly uint NodeGroupOffset;
        public readonly uint UnusedGroupOffset; // always points to Count zeroes
        public readonly uint MaterialGroupOffset;
        public readonly uint TexcoordGroupOffset;
        public readonly uint TextureGroupOffset;
        public readonly ushort Count; // todo?: always 1?
        public readonly ushort Field16; // todo?: always 0?
    }

    // size: 20
    public readonly struct RawMaterialAnimationGroup
    {
        public readonly uint FrameCount;
        public readonly uint ColorLutOffset;
        public readonly uint AnimationCount;
        public readonly uint AnimationOffset;
        public readonly ushort AnimationFrame;
        public readonly ushort Field12;
    }

    // size: 44
    public readonly struct RawTextureAnimationGroup
    {
        public readonly uint FrameCount;
        public readonly uint Field4;
        public readonly ushort AnimationCount;
        public readonly ushort FieldA;
        public readonly uint FrameDataOffset;
        public readonly uint TextureIdOffset;
        public readonly uint PaletteOffset;
        public readonly uint AnimationOffset;
        public readonly ushort AnimationFrame;
        public readonly ushort Field1E;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly uint Field28;
    }

    // size: 28
    public readonly struct RawTexcoordAnimationGroup
    {
        public readonly uint FrameCount;
        public readonly uint ScaleLutOffset;
        public readonly uint RotateLutOffset;
        public readonly uint TranslateLutOffset;
        public readonly uint AnimationCount;
        public readonly uint AnimationOffset;
        public readonly ushort AnimationFrame;
        public readonly ushort Field1A;
    }

    // size: 20
    public readonly struct RawNodeAnimationGroup
    {
        public readonly uint Data;
        public readonly uint Fixed32Pointer;
        public readonly uint UInt16Pointer;
        public readonly uint Int32Pointer;
        public readonly uint AnimationOffset;
    }

    // size: 140
    public readonly struct MaterialAnimation
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public readonly string Name;
        public readonly uint Field40;
        public readonly byte DiffuseBlendFactorR;
        public readonly byte DiffuseBlendFactorG;
        public readonly byte DiffuseBlendFactorB;
        public readonly byte Field47;
        public readonly ushort DiffuseLutLengthR;
        public readonly ushort DiffuseLutLengthG;
        public readonly ushort DiffuseLutLengthB;
        public readonly ushort DiffuseLutStartIndexR;
        public readonly ushort DiffuseLutStartIndexG;
        public readonly ushort DiffuseLutStartIndexB;
        public readonly byte AmbientBlendFactorR;
        public readonly byte AmbientBlendFactorG;
        public readonly byte AmbientBlendFactorB;
        public readonly byte Field57;
        public readonly ushort AmbientLutLengthR;
        public readonly ushort AmbientLutLengthG;
        public readonly ushort AmbientLutLengthB;
        public readonly ushort AmbientLutStartIndexR;
        public readonly ushort AmbientLutStartIndexG;
        public readonly ushort AmbientLutStartIndexB;
        public readonly byte SpecularBlendFactorR;
        public readonly byte SpecularBlendFactorG;
        public readonly byte SpecularBlendFactorB;
        public readonly byte Field67;
        public readonly ushort SpecularLutLengthR;
        public readonly ushort SpecularLutLengthG;
        public readonly ushort SpecularLutLengthB;
        public readonly ushort SpecularLutStartIndexR;
        public readonly ushort SpecularLutStartIndexG;
        public readonly ushort SpecularLutStartIndexB;
        public readonly uint Field74;
        public readonly uint Field78;
        public readonly uint Field7C;
        public readonly uint Field80;
        public readonly byte AlphaBlendFactor;
        public readonly byte Field85;
        public readonly ushort AlphaLutLength;
        public readonly ushort AlphaLutStartIndex;
        public readonly ushort MaterialId;
    }

    // size: 44
    public readonly struct TextureAnimation
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public readonly string Name;
        public readonly ushort Count;
        public readonly ushort StartIndex;
        public readonly ushort MinimumPaletteId;
        public readonly ushort MaterialId;
        public readonly ushort MinimumTextureId;
        public readonly ushort Field2A;
    }

    // size: 60
    public readonly struct TexcoordAnimation
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public readonly string Name;
        public readonly byte ScaleBlendS;
        public readonly byte ScaleBlendT;
        public readonly ushort ScaleLutLengthS;
        public readonly ushort ScaleLutLengthT;
        public readonly ushort ScaleLutIndexS;
        public readonly ushort ScaleLutIndexT;
        public readonly byte RotateBlendZ;
        public readonly byte Field2B;
        public readonly ushort RotateLutLengthZ;
        public readonly ushort RotateLutIndexZ;
        public readonly byte TranslateBlendS;
        public readonly byte TranslateBlendT;
        public readonly ushort TranslateLutLengthS;
        public readonly ushort TranslateLutLengthT;
        public readonly ushort TranslateLutIndexS;
        public readonly ushort TranslateLutIndexT;
        public readonly ushort Field3A;
    }

    // size: 48
    public readonly struct NodeAnimation
    {
        public readonly byte Field0;
        public readonly byte Field1;
        public readonly byte Field2;
        public readonly byte Flags;
        public readonly ushort Field4;
        public readonly ushort Field6;
        public readonly ushort Field8;
        public readonly ushort FieldA;
        public readonly ushort FieldC;
        public readonly ushort FieldE;
        public readonly byte Field10;
        public readonly byte Field11;
        public readonly byte Field12;
        public readonly byte Field13;
        public readonly ushort Field14;
        public readonly ushort Field16;
        public readonly ushort Field18;
        public readonly ushort Field1A;
        public readonly ushort Field1C;
        public readonly ushort Field1E;
        public readonly byte Field20;
        public readonly byte Field21;
        public readonly byte Field22;
        public readonly byte Field23;
        public readonly ushort Field24;
        public readonly ushort Field26;
        public readonly ushort Field28;
        public readonly ushort Field2A;
        public readonly ushort Field2C;
        public readonly ushort Field2E;
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
        public readonly uint TextureMatrixOffset;
        public readonly uint NodeAnimationOffset;
        public readonly uint TextureCoordinateAnimations;
        public readonly uint MaterialAnimations;
        public readonly uint TextureAnimations;
        public readonly ushort MeshCount;
        public readonly ushort MatrixCount;
    }

    // size: 240
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
        public readonly byte Billboard;
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
