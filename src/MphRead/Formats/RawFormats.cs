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
        public static readonly int NodeAnimation = Marshal.SizeOf(typeof(NodeAnimation));
        public static readonly int CameraSequenceHeader = Marshal.SizeOf(typeof(CameraSequenceHeader));
        public static readonly int CameraSequenceFrame = Marshal.SizeOf(typeof(CameraSequenceFrame));
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
        public readonly Vector3Fx MinCoordinates;
        public readonly Vector3Fx MaxCoordinates;
    }

    // size: 132
    public readonly struct RawMaterial
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public readonly char[] Name;
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
        public readonly ushort RotateZ;
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
        public readonly ushort Count;
        public readonly ushort Field16; // always 0 except for testlevel_Anim (FH), where it's 52428
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

    // size: 32
    public readonly struct RawTextureAnimationGroup
    {
        public readonly ushort FrameCount;
        public readonly ushort FrameIndexCount;
        public readonly ushort TextureIdCount;
        public readonly ushort PaletteIdCount;
        public readonly ushort AnimationCount;
        public readonly ushort FieldA;
        public readonly uint FrameIndexOffset;
        public readonly uint TextureIdOffset;
        public readonly uint PaletteIdOffset;
        public readonly uint AnimationOffset;
        public readonly ushort AnimationFrame;
        public readonly ushort Field1E;
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
        public readonly uint FrameCount;
        public readonly uint ScaleLutOffset;
        public readonly uint RotateLutOffset;
        public readonly uint TranslateLutOffset;
        public readonly uint AnimationOffset;
    }

    // size: 140
    public readonly struct MaterialAnimation
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public readonly char[] Name;
        public readonly uint Field40;
        public readonly byte DiffuseBlendR;
        public readonly byte DiffuseBlendG;
        public readonly byte DiffuseBlendB;
        public readonly byte Field47; // todo: use more properties (this one is always 0 or 255)
        public readonly ushort DiffuseLutLengthR;
        public readonly ushort DiffuseLutLengthG;
        public readonly ushort DiffuseLutLengthB;
        public readonly ushort DiffuseLutIndexR;
        public readonly ushort DiffuseLutIndexG;
        public readonly ushort DiffuseLutIndexB;
        public readonly byte AmbientBlendR;
        public readonly byte AmbientBlendG;
        public readonly byte AmbientBlendB;
        public readonly byte Field57; // same as 47
        public readonly ushort AmbientLutLengthR;
        public readonly ushort AmbientLutLengthG;
        public readonly ushort AmbientLutLengthB;
        public readonly ushort AmbientLutIndexR;
        public readonly ushort AmbientLutIndexG;
        public readonly ushort AmbientLutIndexB;
        public readonly byte SpecularBlendR;
        public readonly byte SpecularBlendG;
        public readonly byte SpecularBlendB;
        public readonly byte Field67; // same as 47
        public readonly ushort SpecularLutLengthR;
        public readonly ushort SpecularLutLengthG;
        public readonly ushort SpecularLutLengthB;
        public readonly ushort SpecularLutIndexR;
        public readonly ushort SpecularLutIndexG;
        public readonly ushort SpecularLutIndexB;
        public readonly uint Field74;
        public readonly uint Field78;
        public readonly uint Field7C;
        public readonly uint Field80;
        public readonly byte AlphaBlend;
        public readonly byte Field85;
        public readonly ushort AlphaLutLength;
        public readonly ushort AlphaLutIndex;
        public readonly ushort MaterialId;
    }

    // size: 44
    public readonly struct TextureAnimation
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] Name;
        public readonly ushort Count;
        public readonly ushort StartIndex;
        public readonly ushort MinimumPaletteId; // todo: do these need to be used?
        public readonly ushort MaterialId;
        public readonly ushort MinimumTextureId;
        public readonly ushort Field2A;
    }

    // size: 60
    public readonly struct TexcoordAnimation
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] Name;
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
        public readonly byte ScaleBlendX;
        public readonly byte ScaleBlendY;
        public readonly byte ScaleBlendZ;
        public readonly byte Flags;
        public readonly ushort ScaleLutLengthX;
        public readonly ushort ScaleLutLengthY;
        public readonly ushort ScaleLutLengthZ;
        public readonly ushort ScaleLutIndexX;
        public readonly ushort ScaleLutIndexY;
        public readonly ushort ScaleLutIndexZ;
        public readonly byte RotateBlendX;
        public readonly byte RotateBlendY;
        public readonly byte RotateBlendZ;
        public readonly byte Field13; // padding?
        public readonly ushort RotateLutLengthX;
        public readonly ushort RotateLutLengthY;
        public readonly ushort RotateLutLengthZ;
        public readonly ushort RotateLutIndexX;
        public readonly ushort RotateLutIndexY;
        public readonly ushort RotateLutIndexZ;
        public readonly byte TranslateBlendX;
        public readonly byte TranslateBlendY;
        public readonly byte TranslateBlendZ;
        public readonly byte Field23; // padding?
        public readonly ushort TranslateLutLengthX;
        public readonly ushort TranslateLutLengthY;
        public readonly ushort TranslateLutLengthZ;
        public readonly ushort TranslateLutIndexX;
        public readonly ushort TranslateLutIndexY;
        public readonly ushort TranslateLutIndexZ;
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
        public readonly uint Size;
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
        public readonly ushort NodeWeightCount;
        public readonly byte Flags; // always 0 in the file
        public readonly byte Field1F;
        public readonly uint NodeWeightOffset;
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
        public readonly uint TextureMatrixOffset; // only set at runtime
        public readonly uint NodeAnimationOffset;
        public readonly uint TextureCoordinateAnimations;
        public readonly uint MaterialAnimations;
        public readonly uint TextureAnimations;
        public readonly ushort MeshCount;
        public readonly ushort TextureMatrixCount;
    }

    // size: 240
    public readonly struct RawNode
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public readonly char[] Name;
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
        public readonly Fixed CullRadius;
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly BillboardMode BillboardMode;
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

    // size: 64
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct RawCollisionVolume
    {
        [FieldOffset(0)]
        public readonly VolumeType Type;
        // box
        [FieldOffset(4)]
        public readonly Vector3Fx BoxVector1;
        [FieldOffset(16)]
        public readonly Vector3Fx BoxVector2;
        [FieldOffset(28)]
        public readonly Vector3Fx BoxVector3;
        [FieldOffset(40)]
        public readonly Vector3Fx BoxPosition;
        [FieldOffset(52)]
        public readonly Fixed BoxDot1;
        [FieldOffset(56)]
        public readonly Fixed BoxDot2;
        [FieldOffset(60)]
        public readonly Fixed BoxDot3;
        // cylinder
        [FieldOffset(4)]
        public readonly Vector3Fx CylinderVector;
        [FieldOffset(16)]
        public readonly Vector3Fx CylinderPosition;
        [FieldOffset(28)]
        public readonly Fixed CylinderRadius;
        [FieldOffset(32)]
        public readonly Fixed CylinderDot;
        // sphere
        [FieldOffset(4)]
        public readonly Vector3Fx SpherePosition;
        [FieldOffset(16)]
        public readonly Fixed SphereRadius;
    }

    // size: 64
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct FhRawCollisionVolume
    {
        [FieldOffset(0)]
        public readonly FhVolumeType Type;
        // box
        [FieldOffset(4)]
        public readonly Vector3Fx BoxPosition;
        [FieldOffset(16)]
        public readonly Vector3Fx BoxVector1;
        [FieldOffset(28)]
        public readonly Vector3Fx BoxVector2;
        [FieldOffset(40)]
        public readonly Vector3Fx BoxVector3;
        [FieldOffset(52)]
        public readonly Fixed BoxDot1;
        [FieldOffset(56)]
        public readonly Fixed BoxDot2;
        [FieldOffset(60)]
        public readonly Fixed BoxDot3;
        // cylinder
        [FieldOffset(4)]
        public readonly Vector3Fx CylinderPosition;
        [FieldOffset(16)]
        public readonly Vector3Fx CylinderVector;
        [FieldOffset(28)]
        public readonly Fixed CylinderDot;
        [FieldOffset(32)]
        public readonly Fixed CylinderRadius;
        // sphere
        [FieldOffset(4)]
        public readonly Vector3Fx SpherePosition;
        [FieldOffset(16)]
        public readonly Fixed SphereRadius;
    }

    // size: 8
    public readonly struct CameraSequenceHeader
    {
        public readonly ushort Count;
        public readonly byte Flags;
        // these fields aren't used in ReadCamSeqData, which is probably the only place this struct is read
        public readonly byte Padding1;
        public readonly uint Padding2;
    }

    // size: 100 (100 bytes are read from the file into a 112-byte struct)
    public readonly struct CameraSequenceFrame
    {
        public readonly uint Field0;
        public readonly uint Field4;
        public readonly uint Field8;
        public readonly uint FieldC;
        public readonly uint Field10;
        public readonly uint Field14;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        public readonly uint Field28;
        public readonly uint Field2C;
        public readonly byte Field30;
        public readonly byte Field31;
        public readonly byte Field32;
        public readonly byte Field33;
        public readonly byte Field34;
        public readonly byte Field35;
        public readonly ushort Field36;
        public readonly uint Entity1; // runtime pointer
        public readonly uint Entity2; // runtime pointer
        public readonly uint Field40;
        public readonly ushort Field44;
        public readonly ushort Field46;
        public readonly Fixed Field48; // might be a Vector3Fx around this
        public readonly uint Field4C;
        public readonly uint Field50;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName; // actually "NodeNameOrRef" union
    }

    // size: 28
    public readonly struct RawEffect
    {
        public readonly uint Field0; // always 0xCCCCCCCC in the file
        public readonly uint FuncCount;
        public readonly uint FuncOffset;
        public readonly uint Count2;
        public readonly uint Offset2;
        public readonly uint ElementCount;
        public readonly uint ElementOffset;
    }

    // size: 116
    public readonly struct RawEffectElement
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] ModelName;
        public readonly uint ParticleCount;
        public readonly uint ParticleOffset;
        // note: sparksDown_PS.bin (unused) has an element struct of 112 bytes
        // which is probably missing Flags, Field4C, Field50, or Field54
        public readonly uint Flags;
        public readonly uint Field4C;
        public readonly uint Field50;
        public readonly uint Field54;
        public readonly uint ChildEffectId;
        public readonly uint Field5C;
        public readonly uint Field60;
        public readonly uint Field64;
        public readonly uint Field68;
        public readonly uint SomeCount;
        public readonly uint SomeOffset;
    }

    // size: 11
    public readonly struct RawStringTableEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Id; // need all 4 characters (no terminator)
        public readonly uint Offset;
        public readonly ushort Length;
        public readonly byte Speed;
        public readonly char Category;
    }
}
