using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MphRead.Effects;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead
{
    public class Node
    {
        public string Name { get; }
        public int ParentIndex { get; }
        public int ChildIndex { get; }
        public int NextIndex { get; }
        public bool Enabled { get; set; }
        public bool AnimIgnoreParent { get; set; }
        public bool AnimIgnoreChild { get; set; }
        public int MeshCount { get; }
        public int MeshId { get; }
        public Vector3 Scale { get; set; }
        public Vector3 Angle { get; set; }
        public Vector3 Position { get; set; }
        public float BoundingRadius { get; }
        public Vector3 MinBounds { get; }
        public Vector3 MaxBounds { get; }
        public BillboardMode BillboardMode { get; }
        public Matrix4 Transform { get; set; } = Matrix4.Identity;
        public Matrix4? BeforeTransform { get; set; }
        public Matrix4? AfterTransform { get; set; }
        public Matrix4 Animation { get; set; } = Matrix4.Identity;

        public int RoomPartId { get; set; } = -1;
        public bool RoomPartActive { get; set; } = true; // sktodo: remove

        public readonly float[] Bounds = new float[6];

        public Node(RawNode raw)
        {
            Name = raw.Name.MarshalString();
            ParentIndex = raw.ParentId;
            ChildIndex = raw.ChildId;
            NextIndex = raw.NextId;
            Enabled = raw.Enabled != 0;
            MeshCount = raw.MeshCount;
            MeshId = raw.MeshId;
            Scale = raw.Scale.ToFloatVector();
            Angle = new Vector3(
                raw.AngleX / 65536.0f * 2.0f * MathF.PI,
                raw.AngleY / 65536.0f * 2.0f * MathF.PI,
                raw.AngleZ / 65536.0f * 2.0f * MathF.PI
            );
            Position = raw.Position.ToFloatVector();
            BoundingRadius = raw.BoundingRadius.FloatValue;
            MinBounds = raw.MinBounds.ToFloatVector();
            MaxBounds = raw.MaxBounds.ToFloatVector();
            BillboardMode = raw.BillboardMode;
            Bounds[0] = MinBounds.X;
            Bounds[1] = MinBounds.Y;
            Bounds[2] = MinBounds.Z;
            Bounds[3] = MaxBounds.X;
            Bounds[4] = MaxBounds.Y;
            Bounds[5] = MaxBounds.Z;
        }

        public IEnumerable<int> GetMeshIds()
        {
            int start = MeshId / 2;
            for (int i = 0; i < MeshCount; i++)
            {
                yield return start + i;
            }
        }

        public IEnumerable<int> GetAllMeshIds(IReadOnlyList<Node> nodes, bool root)
        {
            static IEnumerable<int> Yield(Node node)
            {
                int start = node.MeshId / 2;
                for (int i = 0; i < node.MeshCount; i++)
                {
                    yield return start + i;
                }
            }
            foreach (int value in Yield(this))
            {
                yield return value;
            }
            if (!root && NextIndex != -1)
            {
                foreach (int value in nodes[NextIndex].GetAllMeshIds(nodes, root: false))
                {
                    yield return value;
                }
            }
            if (ChildIndex != -1)
            {
                foreach (int value in nodes[ChildIndex].GetAllMeshIds(nodes, root: false))
                {
                    yield return value;
                }
            }
        }
    }

    public class Mesh
    {
        public int MaterialId { get; }
        public int DlistId { get; }

        public int ListId { get; set; }
        public bool Visible { get; set; } = true;
        public Vector4? PlaceholderColor { get; set; }

        public SelectionType Selection { get; set; } = SelectionType.None;

        public Vector4? OverrideColor
        {
            get
            {
                static float GetFactor()
                {
                    long ms = Environment.TickCount64;
                    float percentage = (ms % 1000) / 1000f;
                    if (ms / 1000 % 10 % 2 == 0)
                    {
                        percentage = 1 - percentage;
                    }
                    return percentage;
                }
                if (Selection == SelectionType.Selected)
                {
                    float factor = GetFactor();
                    return new Vector4(factor, factor, factor, 1);
                }
                if (Selection == SelectionType.Parent)
                {
                    float factor = GetFactor();
                    return new Vector4(factor, 0, 0, 1);
                }
                if (Selection == SelectionType.Child)
                {
                    float factor = GetFactor();
                    return new Vector4(0, 0, factor, 1);
                }
                return null;
            }
        }

        public Mesh(RawMesh raw)
        {
            MaterialId = raw.MaterialId;
            DlistId = raw.DlistId;
        }
    }

    public enum SelectionType
    {
        None,
        Selected,
        Parent,
        Child
    }

    public class Material
    {
        public string Name { get; }
        public byte Lighting { get; set; }
        public byte InitLighting { get; }
        public CullingMode Culling { get; }
        public byte Alpha { get; }
        public float CurrentAlpha { get; set; }
        public byte Wireframe { get; }
        public int TextureId { get; }
        public int PaletteId { get; }
        public int TextureBindingId { get; set; }
        public int CurrentTextureId { get; set; }
        public int CurrentPaletteId { get; set; }
        public RepeatMode XRepeat { get; }
        public RepeatMode YRepeat { get; }
        public ColorRgb Diffuse { get; set; }
        public ColorRgb Ambient { get; set; } // todo: yep
        public ColorRgb Specular { get; }
        public Vector3 CurrentDiffuse { get; set; }
        public Vector3 CurrentAmbient { get; set; }
        public Vector3 CurrentSpecular { get; set; }
        public PolygonMode PolygonMode { get; set; }
        public RenderMode RenderMode { get; set; } // todo: revisit the use as a polygon ID
        public MatAnimFlags AnimationFlags { get; set; }
        public TexgenMode TexgenMode { get; set; }
        public int TexcoordAnimationId { get; set; }
        public int MatrixId { get; set; }
        public float ScaleS { get; }
        public float ScaleT { get; }
        public float TranslateS { get; }
        public float TranslateT { get; }
        public float RotateZ { get; }

        public Material(RawMaterial raw)
        {
            Name = raw.Name.MarshalString();
            Lighting = raw.Lighting;
            InitLighting = raw.Lighting;
            Culling = raw.Culling;
            Alpha = raw.Alpha;
            Wireframe = raw.Wireframe;
            CurrentAlpha = Alpha / 31.0f;
            CurrentTextureId = TextureId = raw.TextureId;
            CurrentPaletteId = PaletteId = raw.PaletteId;
            XRepeat = raw.XRepeat;
            YRepeat = raw.YRepeat;
            Diffuse = raw.Diffuse;
            Ambient = raw.Ambient;
            Specular = raw.Specular;
            CurrentDiffuse = raw.Diffuse / 31.0f;
            CurrentAmbient = raw.Ambient / 31.0f;
            CurrentSpecular = raw.Specular / 31.0f;
            PolygonMode = raw.PolygonMode;
            RenderMode = raw.RenderMode;
            AnimationFlags = (MatAnimFlags)raw.AnimationFlags;
            TexgenMode = raw.TexcoordTransformMode;
            TexcoordAnimationId = raw.TexcoordAnimationId;
            MatrixId = (int)raw.MatrixId;
            ScaleS = raw.ScaleS.FloatValue;
            ScaleT = raw.ScaleT.FloatValue;
            TranslateS = raw.TranslateS.FloatValue;
            TranslateT = raw.TranslateT.FloatValue;
            RotateZ = raw.RotateZ / 65536.0f * 2.0f * MathF.PI;
        }
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

    public class NodeAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int Count { get; }
        public IReadOnlyList<float> Scales { get; }
        public IReadOnlyList<float> Rotations { get; }
        public IReadOnlyList<float> Translations { get; }
        public IReadOnlyDictionary<string, NodeAnimation> Animations { get; }

        private NodeAnimationGroup()
        {
            Translations = Rotations = Scales = Enumerable.Empty<float>().ToList();
            Animations = new Dictionary<string, NodeAnimation>();
        }

        public NodeAnimationGroup(RawNodeAnimationGroup raw, IReadOnlyList<float> scales, IReadOnlyList<float> rotations,
            IReadOnlyList<float> translations, IReadOnlyDictionary<string, NodeAnimation> animations)
        {
            FrameCount = (int)raw.FrameCount;
            Scales = scales;
            Rotations = rotations;
            Translations = translations;
            Animations = animations;
            Count = Animations.Count;
        }

        public static NodeAnimationGroup Empty()
        {
            return new NodeAnimationGroup();
        }
    }

    public class TexcoordAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int UnusedFrame { get; }
        public int Count { get; }
        public IReadOnlyList<float> Scales { get; }
        public IReadOnlyList<float> Rotations { get; }
        public IReadOnlyList<float> Translations { get; }
        public IReadOnlyDictionary<string, TexcoordAnimation> Animations { get; }

        private TexcoordAnimationGroup()
        {
            Translations = Rotations = Scales = Enumerable.Empty<float>().ToList();
            Animations = new Dictionary<string, TexcoordAnimation>();
        }

        public TexcoordAnimationGroup(RawTexcoordAnimationGroup raw, IReadOnlyList<float> scales, IReadOnlyList<float> rotations,
            IReadOnlyList<float> translations, IReadOnlyDictionary<string, TexcoordAnimation> animations)
        {
            FrameCount = (int)raw.FrameCount;
            CurrentFrame = raw.AnimationFrame;
            UnusedFrame = raw.Unused1A;
            Count = (int)raw.AnimationCount;
            Scales = scales;
            Rotations = rotations;
            Translations = translations;
            Animations = animations;
            Debug.Assert(Count == Animations.Count);
        }

        public static TexcoordAnimationGroup Empty()
        {
            return new TexcoordAnimationGroup();
        }
    }

    public class TextureAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int UnusedFrame { get; }
        public int Count { get; }
        public IReadOnlyList<ushort> FrameIndices { get; }
        public IReadOnlyList<ushort> TextureIds { get; }
        public IReadOnlyList<ushort> PaletteIds { get; }
        public IReadOnlyDictionary<string, TextureAnimation> Animations { get; }
        public ushort UnusedA { get; }

        private TextureAnimationGroup()
        {
            PaletteIds = TextureIds = FrameIndices = Enumerable.Empty<ushort>().ToList();
            Animations = new Dictionary<string, TextureAnimation>();
        }

        public TextureAnimationGroup(RawTextureAnimationGroup raw, IReadOnlyList<ushort> frameIndices, IReadOnlyList<ushort> textureIds,
            IReadOnlyList<ushort> paletteIds, IReadOnlyDictionary<string, TextureAnimation> animations)
        {
            FrameCount = raw.FrameCount;
            CurrentFrame = raw.AnimationFrame;
            UnusedFrame = raw.Unused1C;
            Count = raw.AnimationCount;
            FrameIndices = frameIndices;
            TextureIds = textureIds;
            PaletteIds = paletteIds;
            Animations = animations;
            Debug.Assert(Count == Animations.Count);
            UnusedA = raw.UnusedA;
        }

        public static TextureAnimationGroup Empty()
        {
            return new TextureAnimationGroup();
        }
    }

    public class MaterialAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int UnusedFrame { get; }
        public int Count { get; }
        public IReadOnlyList<float> Colors { get; }
        public IReadOnlyDictionary<string, MaterialAnimation> Animations { get; }

        private MaterialAnimationGroup()
        {
            Colors = Enumerable.Empty<float>().ToList();
            Animations = new Dictionary<string, MaterialAnimation>();
        }

        public MaterialAnimationGroup(RawMaterialAnimationGroup raw, IReadOnlyList<float> colors,
            IReadOnlyDictionary<string, MaterialAnimation> animations)
        {
            FrameCount = (int)raw.FrameCount;
            CurrentFrame = raw.AnimationFrame;
            UnusedFrame = raw.Unused12;
            Count = (int)raw.AnimationCount;
            Colors = colors;
            Animations = animations;
            Debug.Assert(Count == Animations.Count);
        }

        public static MaterialAnimationGroup Empty()
        {
            return new MaterialAnimationGroup();
        }
    }

    public class FxFuncInfo
    {
        public uint FuncId { get; }
        public IReadOnlyList<int> Parameters { get; }

        public FxFuncInfo(uint funcId, IReadOnlyList<int> parameters)
        {
            Debug.Assert(funcId > 0);
            FuncId = funcId;
            Parameters = parameters;
        }
    }

    public class Effect
    {
        public string Name { get; }
        public uint Field0 { get; }
        // the key is the file offset, which we need to keep around because e.g. fx15's parameters are themselves function pointers
        public IReadOnlyDictionary<uint, FxFuncInfo> Funcs { get; }
        public IReadOnlyList<uint> List2 { get; }
        public IReadOnlyList<EffectElement> Elements { get; }

        public Effect(RawEffect raw, IReadOnlyDictionary<uint, FxFuncInfo> funcs, IReadOnlyList<uint> list2,
            IReadOnlyList<EffectElement> elements, string name)
        {
            Name = Path.GetFileNameWithoutExtension(name).Replace("_PS", "");
            Field0 = raw.Field0;
            Funcs = funcs;
            List2 = list2;
            Elements = elements;
        }
    }

    // todo: this class is basically duplicated, should just go from raw struct to entry? or also make this a readonly struct?
    public class EffectElement
    {
        public string Name { get; }
        public string ModelName { get; }
        public IReadOnlyList<Particle> Particles { get; }
        public EffElemFlags Flags { get; }
        public Vector3 Acceleration { get; }
        public uint ChildEffectId { get; }
        public float Lifespan { get; }
        public float DrainTime { get; }
        public float BufferTime { get; }
        public int DrawType { get; }
        public IReadOnlyDictionary<FuncAction, FxFuncInfo> Actions { get; }
        public IReadOnlyDictionary<uint, FxFuncInfo> Funcs { get; }

        public EffectElement(RawEffectElement raw, IReadOnlyList<Particle> particles,
            IReadOnlyDictionary<uint, FxFuncInfo> funcs, IReadOnlyDictionary<FuncAction, FxFuncInfo> actions)
        {
            Name = raw.Name.MarshalString();
            ModelName = raw.ModelName.MarshalString();
            Flags = raw.Flags;
            Acceleration = raw.Acceleration.ToFloatVector();
            ChildEffectId = raw.ChildEffectId;
            Lifespan = raw.Lifespan.FloatValue;
            DrainTime = raw.DrainTime.FloatValue;
            BufferTime = raw.BufferTime.FloatValue;
            DrawType = raw.DrawType;
            Particles = particles;
            Funcs = funcs;
            Actions = actions;
        }
    }

    public class Particle
    {
        public string Name { get; }
        public Model Model { get; }
        public Node Node { get; }
        public int MaterialId { get; }

        public Particle(string name, Model model, Node node, int materialId)
        {
            Name = name;
            Model = model;
            Node = node;
            MaterialId = materialId;
        }
    }

    public enum FuncAction
    {
        SetParticleId = 9,
        IncreaseParticleAmount = 14,
        SetNewParticleSpeed = 15,
        SetNewParticlePosition = 16,
        SetNewParticleLifespan = 17,
        UpdateParticleSpeed = 18,
        SetParticleAlpha = 19,
        SetParticleRed = 20,
        SetParticleGreen = 21,
        SetParticleBlue = 22,
        SetParticleScale = 23,
        SetParticleRotation = 24,
        SetParticleRoField1 = 25,
        SetParticleRoField2 = 26,
        SetParticleRoField3 = 27,
        SetParticleRoField4 = 28,
        SetParticleRwField1 = 29,
        SetParticleRwField2 = 30,
        SetParticleRwField3 = 31,
        SetParticleRwField4 = 32
    }

    public class StringTableEntry
    {
        public string Id { get; }
        public string Value { get; }
        public byte Speed { get; }
        public char Category { get; }

        public StringTableEntry(RawStringTableEntry raw, string value)
        {
            Id = raw.Id.Reverse().ToArray().MarshalString();
            Speed = raw.Speed;
            Category = raw.Category;
            Value = value;
        }
    }

    // todo: review whether we still need this class after the entity refactor (entity classes aren't using it)
    public class Entity
    {
        public string NodeName { get; }
        public ushort LayerMask { get; }
        public ushort Length { get; }
        public EntityType Type { get; }
        public short EntityId { get; }
        public bool FirstHunt { get; }

        public Vector3 Position { get; }
        public Vector3 UpVector { get; }
        public Vector3 FacingVector { get; }

        public Entity(EntityEntry entry, EntityType type, short entityId, EntityDataHeader header)
        {
            NodeName = entry.NodeName.MarshalString();
            LayerMask = entry.LayerMask;
            Length = entry.Length;
            if (!Enum.IsDefined(typeof(EntityType), type))
            {
                throw new ProgramException($"Invalid entity type {type}");
            }
            Type = type;
            EntityId = entityId;
            FirstHunt = false;
            Position = header.Position.ToFloatVector();
            UpVector = header.UpVector.ToFloatVector();
            FacingVector = header.FacingVector.ToFloatVector();
        }

        public Entity(FhEntityEntry entry, EntityType type, short entityId, EntityDataHeader header)
        {
            NodeName = entry.NodeName.MarshalString();
            if (!Enum.IsDefined(typeof(EntityType), type))
            {
                throw new ProgramException($"Invalid entity type {type}");
            }
            Type = type;
            EntityId = entityId;
            FirstHunt = true;
            Position = header.Position.ToFloatVector();
            UpVector = header.UpVector.ToFloatVector();
            FacingVector = header.FacingVector.ToFloatVector();
        }

        public virtual short GetParentId()
        {
            return -1;
        }

        public virtual short GetChildId()
        {
            return -1;
        }
    }

    public class Entity<T> : Entity where T : struct
    {
        public T Data { get; }

        public Entity(EntityEntry entry, EntityType type, short entityId, T data, EntityDataHeader header)
            : base(entry, type, entityId, header)
        {
            Data = data;
        }

        public Entity(FhEntityEntry entry, EntityType type, short entityId, T data, EntityDataHeader header)
            : base(entry, type, entityId, header)
        {
            Data = data;
        }

        public override short GetParentId()
        {
            if (Data is TriggerVolumeEntityData triggerData)
            {
                return triggerData.ParentId;
            }
            if (Data is AreaVolumeEntityData areaData)
            {
                return areaData.ParentId;
            }
            if (Data is FhTriggerVolumeEntityData fhTiggerData)
            {
                return fhTiggerData.ParentId;
            }
            if (Data is PointModuleEntityData pointModule)
            {
                if (pointModule.PrevId == pointModule.Header.EntityId)
                {
                    return -1;
                }
                return pointModule.PrevId;
            }
            return base.GetParentId();
        }

        public override short GetChildId()
        {
            if (Data is TriggerVolumeEntityData triggerData)
            {
                return triggerData.ChildId;
            }
            if (Data is AreaVolumeEntityData areaData)
            {
                return areaData.ChildId;
            }
            if (Data is FhTriggerVolumeEntityData fhTiggerData)
            {
                return fhTiggerData.ChildId;
            }
            if (Data is PointModuleEntityData pointModule)
            {
                if (pointModule.NextId == pointModule.Header.EntityId)
                {
                    return -1;
                }
                return pointModule.NextId;
            }
            return base.GetChildId();
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CollisionVolume
    {
        [FieldOffset(0)]
        public VolumeType Type;
        // box
        [FieldOffset(4)]
        public Vector3 BoxVector1;
        [FieldOffset(16)]
        public Vector3 BoxVector2;
        [FieldOffset(28)]
        public Vector3 BoxVector3;
        [FieldOffset(40)]
        public Vector3 BoxPosition;
        [FieldOffset(52)]
        public float BoxDot1;
        [FieldOffset(56)]
        public float BoxDot2;
        [FieldOffset(60)]
        public float BoxDot3;
        // cylinder
        [FieldOffset(4)]
        public Vector3 CylinderVector;
        [FieldOffset(16)]
        public Vector3 CylinderPosition;
        [FieldOffset(28)]
        public float CylinderRadius;
        [FieldOffset(32)]
        public float CylinderDot;
        // sphere
        [FieldOffset(4)]
        public Vector3 SpherePosition;
        [FieldOffset(16)]
        public float SphereRadius;

        public CollisionVolume(RawCollisionVolume raw)
        {
            Type = raw.Type;
            BoxVector1 = Vector3.Zero;
            BoxVector2 = Vector3.Zero;
            BoxVector3 = Vector3.Zero;
            BoxPosition = Vector3.Zero;
            BoxDot1 = 0;
            BoxDot2 = 0;
            BoxDot3 = 0;
            CylinderVector = Vector3.Zero;
            CylinderPosition = Vector3.Zero;
            CylinderRadius = 0;
            CylinderDot = 0;
            SpherePosition = Vector3.Zero;
            SphereRadius = 0;
            if (Type == VolumeType.Box)
            {
                BoxVector1 = raw.BoxVector1.ToFloatVector();
                BoxVector2 = raw.BoxVector2.ToFloatVector();
                BoxVector3 = raw.BoxVector3.ToFloatVector();
                BoxPosition = raw.BoxPosition.ToFloatVector();
                BoxDot1 = raw.BoxDot1.FloatValue;
                BoxDot2 = raw.BoxDot2.FloatValue;
                BoxDot3 = raw.BoxDot3.FloatValue;
            }
            else if (Type == VolumeType.Cylinder)
            {
                CylinderVector = raw.CylinderVector.ToFloatVector();
                CylinderPosition = raw.CylinderPosition.ToFloatVector();
                CylinderRadius = raw.CylinderRadius.FloatValue;
                CylinderDot = raw.CylinderDot.FloatValue;
            }
            else if (Type == VolumeType.Sphere)
            {
                SpherePosition = raw.SpherePosition.ToFloatVector();
                SphereRadius = raw.SphereRadius.FloatValue;
            }
            else
            {
                throw new ProgramException($"Invalid volume type {raw.Type}.");
            }
        }

        public CollisionVolume(FhRawCollisionVolume raw)
        {
            BoxVector1 = Vector3.Zero;
            BoxVector2 = Vector3.Zero;
            BoxVector3 = Vector3.Zero;
            BoxPosition = Vector3.Zero;
            BoxDot1 = 0;
            BoxDot2 = 0;
            BoxDot3 = 0;
            CylinderVector = Vector3.Zero;
            CylinderPosition = Vector3.Zero;
            CylinderRadius = 0;
            CylinderDot = 0;
            SpherePosition = Vector3.Zero;
            SphereRadius = 0;
            if (raw.Type == FhVolumeType.Box)
            {
                Type = VolumeType.Box;
                BoxVector1 = raw.BoxVector1.ToFloatVector();
                BoxVector2 = raw.BoxVector2.ToFloatVector();
                BoxVector3 = raw.BoxVector3.ToFloatVector();
                BoxPosition = raw.BoxPosition.ToFloatVector();
                BoxDot1 = raw.BoxDot1.FloatValue;
                BoxDot2 = raw.BoxDot2.FloatValue;
                BoxDot3 = raw.BoxDot3.FloatValue;
            }
            else if (raw.Type == FhVolumeType.Cylinder)
            {
                Type = VolumeType.Cylinder;
                CylinderVector = raw.CylinderVector.ToFloatVector();
                CylinderPosition = raw.CylinderPosition.ToFloatVector();
                CylinderRadius = raw.CylinderRadius.FloatValue;
                CylinderDot = raw.CylinderDot.FloatValue;
            }
            else if (raw.Type == FhVolumeType.Sphere)
            {
                Type = VolumeType.Sphere;
                SpherePosition = raw.SpherePosition.ToFloatVector();
                SphereRadius = raw.SphereRadius.FloatValue;
            }
            else
            {
                throw new ProgramException($"Invalid volume type {raw.Type}.");
            }
        }

        public CollisionVolume(Vector3 vec1, Vector3 vec2, Vector3 vec3, Vector3 pos, float dot1, float dot2, float dot3)
        {
            CylinderVector = Vector3.Zero;
            CylinderPosition = Vector3.Zero;
            CylinderRadius = 0;
            CylinderDot = 0;
            SpherePosition = Vector3.Zero;
            SphereRadius = 0;
            Type = VolumeType.Box;
            BoxVector1 = vec1;
            BoxVector2 = vec2;
            BoxVector3 = vec3;
            BoxPosition = pos;
            BoxDot1 = dot1;
            BoxDot2 = dot2;
            BoxDot3 = dot3;
        }

        public CollisionVolume(Vector3 vec, Vector3 pos, float rad, float dot)
        {
            BoxVector1 = Vector3.Zero;
            BoxVector2 = Vector3.Zero;
            BoxVector3 = Vector3.Zero;
            BoxPosition = Vector3.Zero;
            BoxDot1 = 0;
            BoxDot2 = 0;
            BoxDot3 = 0;
            SpherePosition = Vector3.Zero;
            SphereRadius = 0;
            Type = VolumeType.Cylinder;
            CylinderVector = vec;
            CylinderPosition = pos;
            CylinderRadius = rad;
            CylinderDot = dot;
        }

        public CollisionVolume(Vector3 pos, float rad)
        {
            BoxVector1 = Vector3.Zero;
            BoxVector2 = Vector3.Zero;
            BoxVector3 = Vector3.Zero;
            BoxPosition = Vector3.Zero;
            BoxDot1 = 0;
            BoxDot2 = 0;
            BoxDot3 = 0;
            CylinderVector = Vector3.Zero;
            CylinderPosition = Vector3.Zero;
            CylinderRadius = 0;
            CylinderDot = 0;
            Type = VolumeType.Sphere;
            SpherePosition = pos;
            SphereRadius = rad;
        }

        public static CollisionVolume Transform(CollisionVolume vol, Matrix4 transform)
        {
            if (vol.Type == VolumeType.Box)
            {
                return new CollisionVolume(
                    Matrix.Vec3MultMtx3(vol.BoxVector1, transform),
                    Matrix.Vec3MultMtx3(vol.BoxVector2, transform),
                    Matrix.Vec3MultMtx3(vol.BoxVector3, transform),
                    Matrix.Vec3MultMtx4(vol.BoxPosition, transform),
                    vol.BoxDot1,
                    vol.BoxDot2,
                    vol.BoxDot3
                );
            }
            if (vol.Type == VolumeType.Cylinder)
            {
                return new CollisionVolume(
                    Matrix.Vec3MultMtx3(vol.CylinderVector, transform),
                    Matrix.Vec3MultMtx4(vol.CylinderPosition, transform),
                    vol.CylinderRadius,
                    vol.CylinderDot
                );
            }
            if (vol.Type == VolumeType.Sphere)
            {
                return new CollisionVolume(
                    Matrix.Vec3MultMtx4(vol.SpherePosition, transform),
                    vol.SphereRadius
                );
            }
            throw new ProgramException($"Invalid volume type {vol.Type}.");
        }

        public static CollisionVolume Transform(RawCollisionVolume vol, Matrix4 transform)
        {
            return Transform(new CollisionVolume(vol), transform);
        }

        public static CollisionVolume Transform(FhRawCollisionVolume vol, Matrix4 transform)
        {
            return Transform(new CollisionVolume(vol), transform);
        }

        public static CollisionVolume Move(CollisionVolume vol, Vector3 position)
        {
            if (vol.Type == VolumeType.Box)
            {
                return new CollisionVolume(
                    vol.BoxVector1, vol.BoxVector2, vol.BoxVector3, vol.BoxPosition + position, vol.BoxDot1, vol.BoxDot2, vol.BoxDot3);
            }
            if (vol.Type == VolumeType.Cylinder)
            {
                return new CollisionVolume(vol.CylinderVector, vol.CylinderPosition + position, vol.CylinderRadius, vol.CylinderDot);
            }
            if (vol.Type == VolumeType.Sphere)
            {
                return new CollisionVolume(vol.SpherePosition + position, vol.SphereRadius);
            }
            throw new ProgramException($"Invalid volume type {vol.Type}.");
        }

        public static CollisionVolume Move(RawCollisionVolume vol, Vector3 position)
        {
            return Move(new CollisionVolume(vol), position);
        }

        public static CollisionVolume Move(FhRawCollisionVolume vol, Vector3 position)
        {
            return Move(new CollisionVolume(vol), position);
        }

        public bool TestPoint(Vector3 point)
        {
            if (Type == VolumeType.Box)
            {
                Vector3 difference = point - BoxPosition;
                float dot1 = Vector3.Dot(BoxVector1, difference);
                if (dot1 >= 0 && dot1 <= BoxDot1)
                {
                    float dot2 = Vector3.Dot(BoxVector2, difference);
                    if (dot2 >= 0 && dot2 <= BoxDot2)
                    {
                        float dot3 = Vector3.Dot(BoxVector3, difference);
                        return dot3 >= 0 && dot3 <= BoxDot3;
                    }
                }
            }
            else if (Type == VolumeType.Cylinder)
            {
                Vector3 bottom = CylinderPosition;
                Vector3 top = bottom + CylinderVector * CylinderDot;
                if (Vector3.Dot(point - bottom, top - bottom) >= 0)
                {
                    if (Vector3.Dot(point - top, top - bottom) <= 0)
                    {
                        return Vector3.Cross(point - bottom, top - bottom).Length / (top - bottom).Length <= CylinderRadius;
                    }
                }
            }
            else if (Type == VolumeType.Sphere)
            {
                return Vector3.Distance(SpherePosition, point) <= SphereRadius;
            }
            return false;
        }

        public Vector3 GetCenter()
        {
            if (Type == VolumeType.Box)
            {
                Vector3 pos = BoxDot2 * BoxVector2 + BoxPosition;
                pos = BoxDot3 * BoxVector3 + pos;
                return BoxDot1 * BoxVector1 + pos;
            }
            if (Type == VolumeType.Cylinder)
            {
                return CylinderDot * CylinderVector + CylinderPosition;
            }
            if (Type == VolumeType.Sphere)
            {
                return SpherePosition;
            }
            return Vector3.Zero;
        }
    }

    public abstract class DisplayVolume
    {
        public CollisionVolume Volume { get; }
        public Vector3 Color1 { get; protected set; } = Vector3.Zero;
        public Vector3 Color2 { get; protected set; } = Vector3.Zero;

        public DisplayVolume(RawCollisionVolume volume, Matrix4 transform)
        {
            Volume = CollisionVolume.Transform(volume, transform);
        }

        public DisplayVolume(FhRawCollisionVolume volume, Matrix4 transform)
        {
            Volume = CollisionVolume.Transform(volume, transform);
        }

        public DisplayVolume(CollisionVolume volume, Matrix4 transform)
        {
            Volume = CollisionVolume.Transform(volume, transform);
        }

        public DisplayVolume(RawCollisionVolume volume, Vector3 position)
        {
            Volume = CollisionVolume.Move(volume, position);
        }

        public DisplayVolume(FhRawCollisionVolume volume, Vector3 position)
        {
            Volume = CollisionVolume.Move(volume, position);
        }

        public DisplayVolume(CollisionVolume volume, Vector3 position)
        {
            Volume = CollisionVolume.Move(volume, position);
        }

        public abstract Vector3? GetColor(int index);
    }

    public class MorphCameraDisplay : DisplayVolume
    {
        public MorphCameraDisplay(Entity<MorphCameraEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Color1 = new Vector3(1, 1, 0);
        }

        public MorphCameraDisplay(Entity<FhMorphCameraEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Color1 = new Vector3(1, 1, 0);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 7)
            {
                return Color1;
            }
            return null;
        }
    }

    public class JumpPadDisplay : DisplayVolume
    {
        public Vector3 Vector { get; }
        public float Speed { get; }
        public bool Active { get; }

        public JumpPadDisplay(Entity<JumpPadEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Vector = entity.Data.BeamVector.ToFloatVector();
            Speed = entity.Data.Speed.FloatValue;
            Active = entity.Data.Active != 0;
            Color1 = new Vector3(0, 1, 0);
        }

        public JumpPadDisplay(Entity<FhJumpPadEntityData> entity, Vector3 position)
            : base(entity.Data.ActiveVolume, position)
        {
            Vector = entity.Data.BeamVector.ToFloatVector();
            Speed = entity.Data.Speed.FloatValue;
            Active = true;
            Color1 = new Vector3(0, 1, 0);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 8)
            {
                return Color1;
            }
            return null;
        }
    }

    public class ObjectDisplay : DisplayVolume
    {
        public ObjectDisplay(Entity<ObjectEntityData> entity, Matrix4 transform)
            : base(entity.Data.Volume, transform)
        {
            Color1 = new Vector3(1, 0, 0);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 9)
            {
                return Color1;
            }
            return null;
        }
    }

    public class FlagBaseDisplay : DisplayVolume
    {
        public FlagBaseDisplay(Entity<FlagBaseEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Color1 = new Vector3(1, 1, 1);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 10)
            {
                return Color1;
            }
            return null;
        }
    }

    public class NodeDefenseDisplay : DisplayVolume
    {
        public NodeDefenseDisplay(Entity<NodeDefenseEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Color1 = new Vector3(1, 1, 1);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 11)
            {
                return Color1;
            }
            return null;
        }
    }

    // todo: some subtypes might not use their volume? if so, don't render them (confirm that all AreaVolumes do, also)
    public class TriggerVolumeDisplay : DisplayVolume
    {
        public TriggerVolumeDisplay(Entity<TriggerVolumeEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Color1 = Metadata.GetEventColor(entity.Data.ParentMessage);
            Color2 = Metadata.GetEventColor(entity.Data.ChildMessage);
        }

        public TriggerVolumeDisplay(Entity<FhTriggerVolumeEntityData> entity, Vector3 position)
            : base(entity.Data.ActiveVolume, position)
        {
            Color1 = Metadata.GetEventColor(entity.Data.ParentMessage);
            Color2 = Metadata.GetEventColor(entity.Data.ChildMessage);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 3)
            {
                return Color1;
            }
            if (index == 4)
            {
                return Color2;
            }
            return null;
        }
    }

    public class AreaVolumeDisplay : DisplayVolume
    {
        public AreaVolumeDisplay(Entity<AreaVolumeEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Color1 = Metadata.GetEventColor(entity.Data.InsideMessage);
            Color2 = Metadata.GetEventColor(entity.Data.ExitMessage);
        }

        public AreaVolumeDisplay(Entity<FhAreaVolumeEntityData> entity, Vector3 position)
            : base(entity.Data.ActiveVolume, position)
        {
            Color1 = Metadata.GetEventColor(entity.Data.InsideMessage);
            Color2 = Metadata.GetEventColor(entity.Data.ExitMessage);
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 5)
            {
                return Color1;
            }
            if (index == 6)
            {
                return Color2;
            }
            return null;
        }
    }

    public class LightSource : DisplayVolume
    {
        public bool Light1Enabled { get; }
        public Vector3 Light1Vector { get; }
        public bool Light2Enabled { get; }
        public Vector3 Light2Vector { get; }

        public LightSource(Entity<LightSourceEntityData> entity, Vector3 position)
            : base(entity.Data.Volume, position)
        {
            Light1Enabled = entity.Data.Light1Enabled != 0;
            Color1 = entity.Data.Light1Color.AsVector3();
            Light1Vector = entity.Data.Light1Vector.ToFloatVector();
            Light2Enabled = entity.Data.Light2Enabled != 0;
            Color2 = entity.Data.Light2Color.AsVector3();
            Light2Vector = entity.Data.Light2Vector.ToFloatVector();
        }

        public override Vector3? GetColor(int index)
        {
            if (index == 1)
            {
                return Light1Enabled ? Color1 : Vector3.Zero;
            }
            if (index == 2)
            {
                return Light2Enabled ? Color2 : Vector3.Zero;
            }
            return null;
        }
    }

    public class CameraSequenceKeyframe
    {
        public Vector3 Position { get; }
        public Vector3 ToTarget { get; }
        public float Roll { get; }
        public float Fov { get; }
        public float MoveTime { get; }
        public float HoldTime { get; }
        public float FadeInTime { get; }
        public float FadeOutTime { get; }
        public FadeType FadeInType { get; }
        public FadeType FadeOutType { get; }
        public byte PrevFrameInfluence { get; } // flag bits 0/1
        public byte AfterFrameInfluence { get; } // flag bits 0/1
        public bool UseEntityTransform { get; }
        public short PosEntityType { get; }
        public short PosEntityId { get; }
        public short TargetEntityType { get; }
        public short TargetEntityId { get; }
        public short MessageTargetType { get; }
        public short MessageTargetId { get; }
        public ushort MessageId { get; }
        public ushort MessageParam { get; }
        public float Easing { get; } // always 0, 4096, or 4120 (1.00585938)
        public string NodeName { get; }

        public EntityBase? PositionEntity { get; set; }
        public EntityBase? TargetEntity { get; set; }
        public EntityBase? MessageTarget { get; set; }

        public CameraSequenceKeyframe(RawCameraSequenceKeyframe raw)
        {
            Position = raw.Position.ToFloatVector();
            ToTarget = raw.ToTarget.ToFloatVector();
            Roll = raw.Roll.FloatValue;
            Fov = raw.Fov.FloatValue;
            MoveTime = raw.MoveTime.FloatValue;
            HoldTime = raw.HoldTime.FloatValue;
            FadeInTime = raw.FadeInTime.FloatValue;
            FadeOutTime = raw.FadeOutTime.FloatValue;
            FadeInType = raw.FadeInType;
            FadeOutType = raw.FadeOutType;
            PrevFrameInfluence = raw.PrevFrameInfluence;
            AfterFrameInfluence = raw.AfterFrameInfluence;
            UseEntityTransform = raw.UseEntityTransform != 0;
            PosEntityType = raw.PosEntityType;
            PosEntityId = raw.PosEntityId;
            TargetEntityType = raw.TargetEntityType;
            TargetEntityId = raw.TargetEntityId;
            MessageTargetType = raw.MessageTargetType;
            MessageTargetId = raw.MessageTargetId;
            MessageId = raw.MessageId;
            MessageParam = raw.MessageParam;
            Easing = raw.Easing.FloatValue;
            NodeName = raw.NodeName.MarshalString();
        }
    }

    // todo: FH game modes
    public enum GameMode : byte
    {
        None = 0,
        SinglePlayer = 2,
        Battle = 3,
        BattleTeams = 4,
        Survival = 5,
        SurvivalTeams = 6,
        Capture = 7,
        Bounty = 8,
        BountyTeams = 9,
        Nodes = 10,
        NodesTeams = 11,
        Defender = 12,
        DefenderTeams = 13,
        PrimeHunter = 14,
        Unknown15 = 15 // seems to be unused
    }

    [Flags]
    public enum MatAnimFlags : byte
    {
        None = 0x0,
        DisableColor = 0x1,
        DisableAlpha = 0x2
    }

    [Flags]
    public enum NodeLayer : ushort
    {
        None = 0x0,
        MultiplayerLod0 = 0x8,
        MultiplayerLod1 = 0x10,
        MultiplayerU = 0x20,
        Unknown40 = 0x40, // todo?: 0x1048 shows up in menus, including inside the ship
        Unknown1000 = 0x1000,
        CaptureTheFlag = 0x4000
    }

    [Flags]
    public enum BossFlags
    {
        None = 0x0,
        Unit1B1 = 0x1,
        Unit1B2 = 0x4,
        Unit2B1 = 0x10,
        Unit2B2 = 0x40,
        Unit3B1 = 0x100,
        Unit3B2 = 0x400,
        Unit4B1 = 0x1000,
        Unit4B2 = 0x4000,
        Gorea1 = 0x10000,
        All = 0x55555
    }

    public enum InstructionCode : uint
    {
        NOP = 0x400,
        MTX_RESTORE = 0x450,
        COLOR = 0x480,
        NORMAL = 0x484,
        TEXCOORD = 0x488,
        VTX_16 = 0x48C,
        VTX_10 = 0x490,
        VTX_XY = 0x494,
        VTX_XZ = 0x498,
        VTX_YZ = 0x49C,
        VTX_DIFF = 0x4A0,
        DIF_AMB = 0x4C0,
        BEGIN_VTXS = 0x500,
        END_VTXS = 0x504
    }

    public class RenderInstruction
    {
        public RenderInstruction(InstructionCode code, params uint[] arguments)
        {
            if (arguments.Length != GetArity(code))
            {
                throw new ProgramException($"Incorrect number of arguments for code {code}.");
            }
            Code = code;
            Arguments = arguments.ToList();
        }

        public InstructionCode Code { get; }
        public IReadOnlyList<uint> Arguments { get; }

        private static readonly IReadOnlyDictionary<InstructionCode, int> _arityMap =
            new Dictionary<InstructionCode, int>()
            {
                { InstructionCode.NOP, 0 },
                { InstructionCode.MTX_RESTORE, 1 },
                { InstructionCode.COLOR, 1 },
                { InstructionCode.NORMAL, 1 },
                { InstructionCode.TEXCOORD, 1 },
                { InstructionCode.VTX_16, 2 },
                { InstructionCode.VTX_10, 1 },
                { InstructionCode.VTX_XY, 1 },
                { InstructionCode.VTX_XZ, 1 },
                { InstructionCode.VTX_YZ, 1 },
                { InstructionCode.VTX_DIFF, 1 },
                { InstructionCode.DIF_AMB, 1 },
                { InstructionCode.BEGIN_VTXS, 1 },
                { InstructionCode.END_VTXS, 0 }
            };

        public static int GetArity(InstructionCode code)
        {
            if (!Enum.IsDefined(typeof(InstructionCode), code))
            {
                throw new ProgramException($"Invalid code arity {code}");
            }
            Debug.Assert(_arityMap.ContainsKey(code));
            return _arityMap[code];
        }
    }

    public static class Paths
    {
        public static string FileSystem => _paths.Value.FileSystem;
        public static string FhFileSystem => _paths.Value.FhFileSystem;
        public static string Export => _paths.Value.Export;

        private static readonly Lazy<(string FileSystem, string FhFileSystem, string Export)> _paths
            = new Lazy<(string, string, string)>(() =>
        {
            if (File.Exists("paths.txt"))
            {
                string[] lines = File.ReadAllLines("paths.txt");
                return (
                    lines.Length > 0 ? lines[0] : "",
                    lines.Length > 1 ? lines[1] : "",
                    lines.Length > 2 ? lines[2] : "");
            }
            return ("", "", "");
        });
    }

    public static class CollectionExtensions
    {
        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, uint start)
        {
            return source[(int)start..];
        }

        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, uint start, uint length)
        {
            return source.Slice((int)start, (int)length);
        }

        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, int start, uint length)
        {
            return source.Slice(start, (int)length);
        }

        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, uint start, int length)
        {
            return source.Slice((int)start, length);
        }

        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, long start, int length)
        {
            return source.Slice((int)start, length);
        }

        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, long start, uint length)
        {
            return source.Slice((int)start, (int)length);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            int index = 0;
            foreach (TSource item in source)
            {
                if (predicate.Invoke(item))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public static bool TryFind<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate,
            [NotNullWhen(true)] out TSource? result) where TSource : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            result = source.FirstOrDefault(s => predicate.Invoke(s));
            return result != null;
        }
    }
}
