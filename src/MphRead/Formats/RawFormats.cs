using System.Runtime.InteropServices;
using MphRead.Effects;
using MphRead.Formats.Collision;

namespace MphRead
{
    public static class Sizes
    {
        public static readonly int Header = Marshal.SizeOf(typeof(Header));
        public static readonly int Texture = Marshal.SizeOf(typeof(Texture));
        public static readonly int Palette = Marshal.SizeOf(typeof(Palette));
        public static readonly int Material = Marshal.SizeOf(typeof(RawMaterial));
        public static readonly int Node = Marshal.SizeOf(typeof(RawNode));
        public static readonly int Mesh = Marshal.SizeOf(typeof(RawMesh));
        public static readonly int Dlist = Marshal.SizeOf(typeof(DisplayList));
        public static readonly int EntityHeader = Marshal.SizeOf(typeof(EntityHeader));
        public static readonly int EntityEntry = Marshal.SizeOf(typeof(EntityEntry));
        public static readonly int FhEntityEntry = Marshal.SizeOf(typeof(FhEntityEntry));
        public static readonly int EntityDataHeader = Marshal.SizeOf(typeof(EntityDataHeader));
        public static readonly int JumpPadEntityData = Marshal.SizeOf(typeof(JumpPadEntityData));
        public static readonly int AnimationHeader = Marshal.SizeOf(typeof(AnimationHeader));
        public static readonly int NodeAnimation = Marshal.SizeOf(typeof(NodeAnimation));
        public static readonly int CameraSequenceHeader = Marshal.SizeOf(typeof(CameraSequenceHeader));
        public static readonly int CameraSequenceKeyframe = Marshal.SizeOf(typeof(RawCameraSequenceKeyframe));
        public static readonly int CollisionHeader = Marshal.SizeOf(typeof(CollisionHeader));
        public static readonly int FhCollisionHeader = Marshal.SizeOf(typeof(FhCollisionHeader));
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
        public readonly Vector3Fx MinBounds;
        public readonly Vector3Fx MaxBounds;
    }

    // size: 132
    public readonly struct RawMaterial
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public readonly byte[] Name;
        public readonly byte Lighting;
        public readonly CullingMode Culling;
        public readonly byte Alpha;
        public readonly byte Wireframe;
        public readonly short PaletteId;
        public readonly short TextureId;
        public readonly RepeatMode XRepeat;
        public readonly RepeatMode YRepeat;
        public readonly ColorRgb Diffuse;
        public readonly ColorRgb Ambient;
        public readonly ColorRgb Specular;
        public readonly byte Padding53;
        public readonly PolygonMode PolygonMode;
        public readonly RenderMode RenderMode;
        public readonly byte AnimationFlags;
        public readonly ushort Padding5A;
        public readonly TexgenMode TexcoordTransformMode;
        public readonly ushort TexcoordAnimationId; // set at runtime
        public readonly ushort Padding62;
        public readonly uint MatrixId;
        public readonly Fixed ScaleS;
        public readonly Fixed ScaleT;
        public readonly ushort RotateZ;
        public readonly ushort Padding72;
        public readonly Fixed TranslateS;
        public readonly Fixed TranslateT;
        public readonly ushort MaterialAnimationId; // set at runtime
        public readonly ushort TextureAnimationId; // set at runtime
        public readonly byte PackedRepeatMode;
        public readonly byte Padding81;
        public readonly ushort Padding82;

        public string NameString => Name.MarshalString();
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
        public readonly ushort Padding16; // always 0 except for testlevel_Anim (FH), where it's 0xCCCC
    }

    // size: 20
    public readonly struct RawMaterialAnimationGroup
    {
        public readonly uint FrameCount;
        public readonly uint ColorLutOffset;
        public readonly uint AnimationCount;
        public readonly uint AnimationOffset;
        public readonly ushort AnimationFrame; // uint in FH
        public readonly ushort Unused12;
    }

    // size: 32
    public readonly struct RawTextureAnimationGroup
    {
        public readonly ushort FrameCount;
        public readonly ushort FrameIndexCount;
        public readonly ushort TextureIdCount;
        public readonly ushort PaletteIdCount;
        public readonly ushort AnimationCount;
        public readonly ushort UnusedA;
        public readonly uint FrameIndexOffset;
        public readonly uint TextureIdOffset;
        public readonly uint PaletteIdOffset;
        public readonly uint AnimationOffset;
        public readonly ushort AnimationFrame; // uint in FH
        public readonly ushort Unused1C;
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
        public readonly ushort AnimationFrame; // uint in FH
        public readonly ushort Unused1A;
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
        public readonly byte[] Name;
        public readonly uint Unused40; // always 0x01
        public readonly byte DiffuseBlendR;
        public readonly byte DiffuseBlendG;
        public readonly byte DiffuseBlendB;
        public readonly byte Unused47; // always 0x00
        public readonly ushort DiffuseLutLengthR;
        public readonly ushort DiffuseLutLengthG;
        public readonly ushort DiffuseLutLengthB;
        public readonly ushort DiffuseLutIndexR;
        public readonly ushort DiffuseLutIndexG;
        public readonly ushort DiffuseLutIndexB;
        public readonly byte AmbientBlendR;
        public readonly byte AmbientBlendG;
        public readonly byte AmbientBlendB;
        public readonly byte Unused57; // always 0xFF
        public readonly ushort AmbientLutLengthR;
        public readonly ushort AmbientLutLengthG;
        public readonly ushort AmbientLutLengthB;
        public readonly ushort AmbientLutIndexR;
        public readonly ushort AmbientLutIndexG;
        public readonly ushort AmbientLutIndexB;
        public readonly byte SpecularBlendR;
        public readonly byte SpecularBlendG;
        public readonly byte SpecularBlendB;
        public readonly byte Unused67; // always 0x00
        public readonly ushort SpecularLutLengthR;
        public readonly ushort SpecularLutLengthG;
        public readonly ushort SpecularLutLengthB;
        public readonly ushort SpecularLutIndexR;
        public readonly ushort SpecularLutIndexG;
        public readonly ushort SpecularLutIndexB;
        public readonly uint Unused74; // always 0x10101
        public readonly uint Unused78;
        public readonly uint Unused7C;
        public readonly uint Unused80;
        public readonly byte AlphaBlend;
        public readonly byte Unused85; // 0x01 in FH, 0xC1 in MPH
        public readonly ushort AlphaLutLength;
        public readonly ushort AlphaLutIndex;
        public readonly ushort MaterialId;

        public string NameString => Name.MarshalString();
    }

    // size: 44
    public readonly struct TextureAnimation
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly byte[] Name;
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
        public readonly byte[] Name;
        public readonly byte ScaleBlendS;
        public readonly byte ScaleBlendT;
        public readonly ushort ScaleLutLengthS;
        public readonly ushort ScaleLutLengthT;
        public readonly ushort ScaleLutIndexS;
        public readonly ushort ScaleLutIndexT;
        public readonly byte RotateBlendZ;
        public readonly byte Unused2B; // 0x00 in FH, 0xFF in MPH
        public readonly ushort RotateLutLengthZ;
        public readonly ushort RotateLutIndexZ;
        public readonly byte TranslateBlendS;
        public readonly byte TranslateBlendT;
        public readonly ushort TranslateLutLengthS;
        public readonly ushort TranslateLutLengthT;
        public readonly ushort TranslateLutIndexS;
        public readonly ushort TranslateLutIndexT;
        public readonly ushort Padding3A;

        public string NameString => Name.MarshalString();
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
        public readonly byte Padding13; // 0xCC in testLevel anim
        public readonly ushort RotateLutLengthX;
        public readonly ushort RotateLutLengthY;
        public readonly ushort RotateLutLengthZ;
        public readonly ushort RotateLutIndexX;
        public readonly ushort RotateLutIndexY;
        public readonly ushort RotateLutIndexZ;
        public readonly byte TranslateBlendX;
        public readonly byte TranslateBlendY;
        public readonly byte TranslateBlendZ;
        public readonly byte Padding23; // 0xCC in testLevel anim
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
        public readonly byte Padding1;
        public readonly ushort Width;
        public readonly ushort Height;
        public readonly ushort Padding6;
        public readonly uint ImageOffset;
        public readonly uint ImageSize;
        public readonly uint UnusedOffset; // offset into image data
        public readonly uint UnusedCount; // probably count for previous field
        public readonly uint VramOffset;
        public readonly uint Opaque;
        public readonly uint SkipVram;
        public readonly byte PackedSize;
        public readonly byte NativeTextureFormat;
        public readonly ushort ObjectRef;

        public Texture(TextureFormat format, ushort width, ushort height)
        {
            Format = format;
            Padding1 = 0;
            Width = width;
            Height = height;
            Padding6 = 0;
            ImageOffset = 0;
            ImageSize = 0;
            UnusedOffset = 0;
            UnusedCount = 0;
            VramOffset = 0;
            Opaque = 1;
            SkipVram = 0;
            PackedSize = 0;
            NativeTextureFormat = 0;
            ObjectRef = 0;
        }
    }

    // size: 16
    public readonly struct Palette
    {
        public readonly uint Offset;
        public readonly uint Size;
        public readonly uint VramOffset;
        public readonly uint ObjectRef;
    }

    // size: 100
    public readonly struct Header
    {
        public readonly uint ScaleFactor;
        public readonly Fixed ScaleBase;
        public readonly uint PrimitiveCount;
        public readonly uint VertexCount;
        public readonly uint MaterialOffset;
        public readonly uint DlistOffset;
        public readonly uint NodeOffset;
        public readonly ushort NodeWeightCount;
        public readonly byte Flags; // always 0 in the file
        public readonly byte Padding1F;
        public readonly uint NodeWeightOffset;
        public readonly uint MeshOffset;
        public readonly ushort TextureCount;
        public readonly ushort Padding2A;
        public readonly uint TextureOffset;
        public readonly ushort PaletteCount;
        public readonly ushort Padding32;
        public readonly uint PaletteOffset;
        public readonly uint NodePosCounts;
        public readonly uint NodePosScales;
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
        public readonly byte[] Name;
        public readonly short ParentId;
        public readonly short ChildId;
        public readonly short NextId;
        public readonly ushort Padding46;
        public readonly uint Enabled;
        public readonly ushort MeshCount;
        public readonly ushort MeshId;
        public readonly Vector3Fx Scale;
        public readonly short AngleX;
        public readonly short AngleY;
        public readonly short AngleZ;
        public readonly ushort Padding62;
        public readonly Vector3Fx Position;
        public readonly Fixed BoundingRadius;
        public readonly Vector3Fx MinBounds;
        public readonly Vector3Fx MaxBounds;
        public readonly BillboardMode BillboardMode;
        public readonly byte Padding8D;
        public readonly ushort Padding8E;
        public readonly Matrix43Fx Transform; // set at runtime
        public readonly uint BeforeTransform; // MtxFx43* set at runtime
        public readonly uint AfterTransform; // MtxFx43* set at runtime
        public readonly uint UnusedC8;
        public readonly uint UnusedCC;
        public readonly uint UnusedD0;
        public readonly uint UnusedD4;
        public readonly uint UnusedD8;
        public readonly uint UnusedDC;
        public readonly uint UnusedE0;
        public readonly uint UnusedE4;
        public readonly uint UnusedE8;
        public readonly uint UnusedEC;

        public string NameString => Name.MarshalString();
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
        public readonly byte Version;
        public readonly byte Padding3;
        public readonly uint Padding4;
    }

    // size: 100 (100 bytes are read from the file into a 112-byte struct)
    public readonly struct RawCameraSequenceKeyframe
    {
        public readonly Vector3Fx Position;
        public readonly Vector3Fx ToTarget;
        public readonly Fixed Roll;
        public readonly Fixed Fov;
        public readonly Fixed MoveTime;
        public readonly Fixed HoldTime;
        public readonly Fixed FadeInTime;
        public readonly Fixed FadeOutTime;
        public readonly FadeType FadeInType;
        public readonly FadeType FadeOutType;
        public readonly byte PrevFrameInfluence; // flag bits 0/1
        public readonly byte AfterFrameInfluence; // flag bits 0/1
        public readonly byte UseEntityTransform;
        public readonly byte Padding35;
        public readonly ushort Padding36;
        public readonly short PosEntityType;
        public readonly short PosEntityId;
        public readonly short TargetEntityType;
        public readonly short TargetEntityId;
        public readonly short MessageTargetType;
        public readonly short MessageTargetId;
        public readonly ushort MessageId;
        public readonly ushort MessageParam;
        public readonly Fixed Easing; // always 0, 4096, or 4120 (1.00585938)
        public readonly uint Unused4C;
        public readonly uint Unused50;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly byte[] NodeName;

        public string NodeNameString => NodeName.MarshalString();
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
    // note: sparksDown_PS.bin (unused) has an element struct of 112 bytes
    public readonly struct RawEffectElement
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly byte[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly byte[] ModelName;
        public readonly uint ParticleCount;
        public readonly uint ParticleOffset;
        public readonly EffElemFlags Flags;
        public readonly Vector3Fx Acceleration;
        public readonly uint ChildEffectId;
        public readonly Fixed Lifespan;
        public readonly Fixed DrainTime;
        public readonly Fixed BufferTime;
        public readonly int DrawType;
        public readonly uint FuncCount;
        public readonly uint FuncOffset;

        public string NameString => Name.MarshalString();
        public string ModelNameString => ModelName.MarshalString();
    }

    // size: 11
    public readonly struct RawStringTableEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly byte[] Id; // need all 4 characters (no terminator)
        public readonly uint Offset;
        public readonly ushort Length;
        public readonly byte Speed;
        public readonly char Category;

        public string IdString => Id.MarshalString();
    }

    //size: 12
    public readonly struct TextFileEntry
    {
        public readonly uint Offset1;
        public readonly uint Offset2;
        public readonly ushort Length1;
        public readonly ushort Length2;
    }
}
