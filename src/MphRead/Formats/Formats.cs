using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenToolkit.Mathematics;

namespace MphRead
{
    public class Model
    {
        public bool Visible { get; set; } = true;
        public bool ScanVisorOnly { get; set; }
        public bool UseLightSources { get; }
        public ModelType Type { get; set; }
        public EntityType EntityType { get; set; } // currently only used when ModelType is Placeholder

        public string Name { get; }
        public Header Header { get; }
        public IReadOnlyList<Node> Nodes { get; }
        public IReadOnlyList<Mesh> Meshes { get; }
        public IReadOnlyList<Material> Materials { get; }
        public IReadOnlyList<DisplayList> DisplayLists { get; }
        public IReadOnlyList<Matrix4> TextureMatrices { get; }

        // count and order match Dlists
        public IReadOnlyList<IReadOnlyList<RenderInstruction>> RenderInstructionLists { get; }

        public IReadOnlyList<Texture> Textures => Recolors[CurrentRecolor].Textures;
        public IReadOnlyList<Palette> Palettes => Recolors[CurrentRecolor].Palettes;
        public IReadOnlyList<IReadOnlyList<TextureData>> TextureData => Recolors[CurrentRecolor].TextureData;
        public IReadOnlyList<IReadOnlyList<PaletteData>> PaletteData => Recolors[CurrentRecolor].PaletteData;

        private int _currentRecolor = 0;
        public int CurrentRecolor
        {
            get
            {
                return _currentRecolor;
            }
            set
            {
                if (_currentRecolor < 0 || _currentRecolor >= Recolors.Count)
                {
                    throw new ArgumentException(nameof(CurrentRecolor));
                }
                _currentRecolor = value;
            }
        }

        private Matrix4 _transform = Matrix4.Identity;
        public Vector3 _scale = new Vector3(1, 1, 1);
        public Vector3 _position = Vector3.Zero;
        public Vector3 _rotation = Vector3.Zero;

        public Matrix4 Transform
        {
            get
            {
                return _transform;
            }
            set
            {
                _position = value.ExtractTranslation();
                _scale = value.ExtractScale();
                value.ExtractRotation().ToEulerAngles(out Vector3 rotation);
                _rotation = new Vector3(
                    MathHelper.RadiansToDegrees(rotation.X),
                    MathHelper.RadiansToDegrees(rotation.Y),
                    MathHelper.RadiansToDegrees(rotation.Z)
                );
                _transform = value;
            }
        }

        public Vector3 Position
        {
            get
            {
                return _position;
            }
            set
            {
                _transform.M41 = value.X;
                _transform.M42 = value.Y;
                _transform.M43 = value.Z;
                _position = value;
            }
        }

        public Vector3 Scale
        {
            get
            {
                return _scale;
            }
            set
            {
                _transform = SceneSetup.ComputeNodeTransforms(value, new Vector3(
                    MathHelper.DegreesToRadians(Rotation.X),
                    MathHelper.DegreesToRadians(Rotation.Y),
                    MathHelper.DegreesToRadians(Rotation.Z)),
                    Position);
                _scale = value;
            }
        }

        public Vector3 Rotation
        {
            get
            {
                return _rotation;
            }
            set
            {
                _transform = SceneSetup.ComputeNodeTransforms(Scale, new Vector3(
                    MathHelper.DegreesToRadians(value.X),
                    MathHelper.DegreesToRadians(value.Y),
                    MathHelper.DegreesToRadians(value.Z)),
                    Position);
                _rotation = value;
            }
        }

        // used to rotate items (and FH jump pad beams) about the Y axis,
        // after all their other transforms are done
        public bool Rotating { get; set; }
        public bool Floating { get; set; }
        public float Spin { get; set; }
        // refers to the untransformed model's axis
        public Vector3 SpinAxis { get; set; } = Vector3.UnitY;

        public int AnimationCount { get; set; }
        public IReadOnlyList<NodeAnimationGroup> NodeAnimationGroups { get; }
        public IReadOnlyList<MaterialAnimationGroup> MaterialAnimationGroups { get; }
        public IReadOnlyList<TexcoordAnimationGroup> TexcoordAnimationGroups { get; }
        public IReadOnlyList<TextureAnimationGroup> TextureAnimationGroups { get; }

        public IReadOnlyList<Recolor> Recolors { get; }

        // todo: refactor model vs. entity abstraction
        public Entity? Entity { get; set; }

        private static int _nextSceneId = 0;
        public int SceneId { get; } = _nextSceneId++;

        public Model(string name, Header header, IReadOnlyList<RawNode> nodes, IReadOnlyList<RawMesh> meshes,
            IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix44Fx> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources)
        {
            ThrowIfInvalidEnums(materials);
            Name = name;
            Header = header;
            Nodes = nodes.Select(n => new Node(n)).ToList();
            Meshes = meshes.Select(m => new Mesh(m)).ToList();
            Materials = materials.Select(m => new Material(m)).ToList();
            DisplayLists = dlists;
            RenderInstructionLists = renderInstructions;
            NodeAnimationGroups = nodeGroups;
            MaterialAnimationGroups = materialGroups;
            TexcoordAnimationGroups = texcoordGroups;
            TextureAnimationGroups = textureGroups;
            TextureMatrices = textureMatrices.Select(m => m.ToFloatMatrix()).ToList();
            Recolors = recolors;
            CurrentRecolor = defaultRecolor;
            float scale = Header.ScaleBase.FloatValue * (1 << (int)Header.ScaleFactor);
            Scale = new Vector3(scale, scale, scale);
            UseLightSources = useLightSources;
        }

        public IEnumerable<ColorRgba> GetPixels(int textureId, int paletteId)
        {
            return Recolors[CurrentRecolor].GetPixels(textureId, paletteId);
        }

        public IEnumerable<Mesh> GetNodeMeshes(int nodeId)
        {
            return GetNodeMeshes(Nodes[nodeId]);
        }

        public IEnumerable<Mesh> GetNodeMeshes(Node node)
        {
            foreach (int meshId in node.GetMeshIds())
            {
                yield return Meshes[meshId];
            }
        }

        public bool NodeParentsEnabled(Node node)
        {
            int parentIndex = node.ParentIndex;
            while (parentIndex != UInt16.MaxValue)
            {
                Node parent = Nodes[parentIndex];
                if (!parent.Enabled)
                {
                    return false;
                }
                parentIndex = parent.ParentIndex;
            }
            return true;
        }

        public int GetNextRoomNodeId(int nodeId)
        {
            int i = nodeId + 1;
            while (true)
            {
                if (i > Nodes.Count - 1)
                {
                    i = 0;
                }
                if (i == nodeId)
                {
                    break;
                }
                if (Nodes[i].IsRoomNode)
                {
                    return i;
                }
                i++;
            }
            return nodeId;
        }

        public int GetPreviousRoomNodeId(int nodeId)
        {
            int i = nodeId - 1;
            while (true)
            {
                if (i < 0)
                {
                    i = Nodes.Count - 1;
                }
                if (i == nodeId)
                {
                    break;
                }
                if (Nodes[i].IsRoomNode)
                {
                    return i;
                }
                i--;
            }
            return nodeId;
        }

        public void PrintImages(int recolor = 0)
        {
            if (recolor >= 0 && recolor < Recolors.Count)
            {
                Console.WriteLine($"-- {Recolors[recolor].Name} --");
                Console.WriteLine();
                foreach (Material material in Materials.OrderBy(m => m.TextureId).ThenBy(m => m.PaletteId))
                {
                    Console.WriteLine($"tex {material.TextureId} - pal {material.PaletteId}");
                    Console.WriteLine();
                    Texture texture = Recolors[recolor].Textures[material.TextureId];
                    IReadOnlyList<ColorRgba> pixels = Recolors[recolor].GetPixels(material.TextureId, material.PaletteId);
                    for (int i = 0; i < pixels.Count; i++)
                    {
                        ColorRgba pixel = ColorOnBlackBackground(pixels[i]);
                        for (int j = 0; j < 2; j++)
                        {
                            Console.Write($"\u001b[38;2;{pixel.Red};{pixel.Green};{pixel.Blue}m█");
                        }
                        if (i == pixels.Count - 1)
                        {
                            Console.Write("\u001b[0m");
                        }
                        if ((i + 1) % texture.Width == 0)
                        {
                            Console.WriteLine();
                        }
                    }
                    Console.WriteLine();
                }
            }
        }

        public void PrintRenderInstructions()
        {
            for (int i = 0; i < RenderInstructionLists.Count; i++)
            {
                Console.WriteLine();
                PrintRenderInstructions(i);
            }
        }

        public void PrintRenderInstructions(int dlistId)
        {
            Console.WriteLine($"Dlist ID {dlistId}:");
            foreach (RenderInstruction instruction in RenderInstructionLists[dlistId])
            {
                Console.WriteLine($"{instruction.Code,-12}\t" +
                    $"{String.Join(", ", instruction.Arguments)}".Trim());
            }
        }

        private ColorRgba ColorOnBlackBackground(ColorRgba color)
        {
            float alpha = color.Alpha / 255f;
            return new ColorRgba((byte)(color.Red * alpha), (byte)(color.Green * alpha), (byte)(color.Blue * alpha), 255);
        }

        private static void ThrowIfInvalidEnums(IEnumerable<RawMaterial> materials)
        {
            foreach (RawMaterial material in materials)
            {
                if (!Enum.IsDefined(typeof(RenderMode), material.RenderMode))
                {
                    throw new ProgramException($"Invalid render mode {material.RenderMode}.");
                }
                if (!Enum.IsDefined(typeof(RepeatMode), material.PackedRepeatMode))
                {
                    throw new ProgramException($"Invalid repeat mode {material.PackedRepeatMode}.");
                }
                if (!Enum.IsDefined(typeof(PolygonMode), material.PolygonMode))
                {
                    throw new ProgramException($"Invalid polygon mode {material.PolygonMode}.");
                }
            }
        }
    }

    public class Recolor
    {
        public string Name { get; }
        public IReadOnlyList<Texture> Textures { get; }
        public IReadOnlyList<Palette> Palettes { get; }
        public IReadOnlyList<IReadOnlyList<TextureData>> TextureData { get; }
        public IReadOnlyList<IReadOnlyList<PaletteData>> PaletteData { get; }

        public Recolor(string name, IReadOnlyList<Texture> textures, IReadOnlyList<Palette> palettes,
            IReadOnlyList<IReadOnlyList<TextureData>> textureData, IReadOnlyList<IReadOnlyList<PaletteData>> paletteData)
        {
            ThrowIfInvalidEnums(textures);
            Name = name;
            Textures = textures;
            Palettes = palettes;
            TextureData = textureData;
            PaletteData = paletteData;
            Debug.Assert(Textures.Count == TextureData.Count);
            Debug.Assert(Palettes.Count == PaletteData.Count);
        }

        public IReadOnlyList<ColorRgba> GetPixels(int textureId, int palettteId)
        {
            if (textureId < 0 || textureId >= TextureData.Count)
            {
                throw new ArgumentException(nameof(textureId));
            }
            var pixels = new List<ColorRgba>();
            TextureFormat textureFormat = Textures[textureId].Format;
            if (textureFormat == TextureFormat.DirectRgb)
            {
                for (int i = 0; i < TextureData[textureId].Count; i++)
                {
                    uint color = TextureData[textureId][i].Data;
                    byte alpha = TextureData[textureId][i].Alpha;
                    pixels.Add(ColorFromShort(color, alpha));
                }
            }
            else
            {
                if (palettteId < 0 || palettteId >= PaletteData.Count)
                {
                    throw new ArgumentException(nameof(palettteId));
                }
                for (int i = 0; i < TextureData[textureId].Count; i++)
                {
                    int index = (int)TextureData[textureId][i].Data;
                    ushort color = PaletteData[palettteId][index].Data;
                    byte alpha = TextureData[textureId][i].Alpha;
                    pixels.Add(ColorFromShort(color, alpha));
                }
            }
            return pixels;
        }

        public IReadOnlyList<ColorRgba> GetPalettePixels(int palettteId)
        {
            if (palettteId < 0 || palettteId >= PaletteData.Count)
            {
                throw new ArgumentException(nameof(palettteId));
            }
            var pixels = new List<ColorRgba>();
            foreach (PaletteData paletteData in PaletteData[palettteId])
            {
                pixels.Add(ColorFromShort(paletteData.Data, 255));
            }
            return pixels;
        }

        private ColorRgba ColorFromShort(uint value, byte alpha)
        {
            byte red = (byte)(((value >> 0) & 0x1F) << 3);
            byte green = (byte)(((value >> 5) & 0x1F) << 3);
            byte blue = (byte)(((value >> 10) & 0x1F) << 3);
            return new ColorRgba(red, green, blue, alpha);
        }

        private static void ThrowIfInvalidEnums(IEnumerable<Texture> textures)
        {
            foreach (Texture texture in textures)
            {
                if (!Enum.IsDefined(typeof(TextureFormat), texture.Format))
                {
                    throw new ProgramException($"Invalid texture format {texture.Format}.");
                }
            }
        }
    }

    // todo: look at and use more fields from the raw struct (same for Material)
    public class Node
    {
        public string Name { get; }
        public int ParentIndex { get; }
        public int ChildIndex { get; }
        public int NextIndex { get; }
        public bool Enabled { get; set; }
        public int MeshCount { get; }
        public int MeshId { get; }
        public Vector3 Scale { get; set; }
        public Vector3 Angle { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Vector1 { get; }
        public Vector3 Vector2 { get; }
        public bool Billboard { get; }
        public Matrix4 Transform { get; set; } = Matrix4.Identity;

        public IEnumerable<int> GetMeshIds()
        {
            int start = MeshId / 2;
            for (int i = 0; i < MeshCount; i++)
            {
                yield return start + i;
            }
        }

        public bool IsRoomNode { get; private set; }

        public Node(RawNode raw)
        {
            Name = raw.Name;
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
            Vector1 = raw.Vector1.ToFloatVector();
            Vector2 = raw.Vector2.ToFloatVector();
            // todo: implement billboard = 2 (cylindrical), also "fix" spherical
            Billboard = raw.Billboard == 1;
            IsRoomNode = Name.StartsWith("rm");
        }
    }

    public class Mesh
    {
        public int MaterialId { get; }
        public int DlistId { get; }

        public bool Visible { get; set; } = true;
        public Vector4? PlaceholderColor { get; set; }
        public Vector4? OverrideColor { get; set; }

        public Mesh(RawMesh raw)
        {
            MaterialId = raw.MaterialId;
            DlistId = raw.DlistId;
        }
    }

    public class Material
    {
        public string Name { get; }
        public byte Lighting { get; set; } // todo: what do lighting values 3 and 5 mean?
        public CullingMode Culling { get; }
        public byte Alpha { get; }
        public float CurrentAlpha { get; set; }
        public int TextureId { get; }
        public int PaletteId { get; }
        public int TextureBindingId { get; set; }
        public int CurrentTextureId { get; set; }
        public int CurrentPaletteId { get; set; }
        public RepeatMode XRepeat { get; }
        public RepeatMode YRepeat { get; }
        public ColorRgb Diffuse { get; }
        public ColorRgb Ambient { get; }
        public ColorRgb Specular { get; }
        public PolygonMode PolygonMode { get; set; }
        public RenderMode RenderMode { get; set; }
        public byte AnimationFlags { get; set; } // todo: this probably has more uses
        public TexgenMode TexgenMode { get; set; }
        public int TexcoordAnimationId { get; set; }
        public int MatrixId { get; set; }
        public float ScaleS { get; }
        public float ScaleT { get; }
        public float TranslateS { get; }
        public float TranslateT { get; }
        public float RotateZ { get; }

        public RenderMode GetEffectiveRenderMode(Mesh mesh)
        {
            return mesh.OverrideColor == null ? RenderMode : RenderMode.Translucent;
        }

        public Material(RawMaterial raw)
        {
            Name = raw.Name;
            Lighting = raw.Lighting;
            Culling = raw.Culling;
            Alpha = raw.Alpha;
            CurrentAlpha = Alpha / 31.0f;
            CurrentTextureId = TextureId = raw.TextureId;
            CurrentPaletteId = PaletteId = raw.PaletteId;
            XRepeat = raw.XRepeat;
            YRepeat = raw.YRepeat;
            Diffuse = raw.Diffuse;
            Ambient = raw.Ambient;
            Specular = raw.Specular;
            PolygonMode = raw.PolygonMode;
            RenderMode = raw.RenderMode;
            AnimationFlags = raw.AnimationFlags;
            TexgenMode = raw.TexcoordTransformMode;
            TexcoordAnimationId = raw.TexcoordAnimationId;
            MatrixId = (int)raw.MatrixId;
            ScaleS = raw.ScaleS.FloatValue;
            ScaleT = raw.ScaleT.FloatValue;
            TranslateS = raw.TranslateS.FloatValue;
            TranslateT = raw.TranslateT.FloatValue;
            // todo: doing rad to deg here is inconsistent with other things, but more efficient
            RotateZ = MathHelper.RadiansToDegrees(raw.RotateZ / 65536.0f * 2.0f * MathF.PI);
        }
    }

    public class NodeAnimationGroup
    {
        public uint FrameCount { get; }
        public uint Fixed32Pointer { get; }
        public uint UInt16Pointer { get; }
        public uint Int32Pointer { get; }
        public uint AnimationOffset { get; }
        public IReadOnlyDictionary<string, NodeAnimation> Animations { get; }

        public NodeAnimationGroup(RawNodeAnimationGroup raw, IReadOnlyDictionary<string, NodeAnimation> animations)
        {
            FrameCount = raw.FrameCount;
            Fixed32Pointer = raw.Fixed32Pointer;
            UInt16Pointer = raw.UInt16Pointer;
            Int32Pointer = raw.Int32Pointer;
            AnimationOffset = raw.AnimationOffset;
            Animations = animations;
        }
    }

    public class TexcoordAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int Count { get; }
        public IReadOnlyList<float> Scales { get; }
        public IReadOnlyList<float> Rotations { get; }
        public IReadOnlyList<float> Translations { get; }
        public IReadOnlyDictionary<string, TexcoordAnimation> Animations { get; }

        public TexcoordAnimationGroup(RawTexcoordAnimationGroup raw, IReadOnlyList<float> scales, IReadOnlyList<float> rotations,
            IReadOnlyList<float> translations, IReadOnlyDictionary<string, TexcoordAnimation> animations)
        {
            FrameCount = (int)raw.FrameCount;
            CurrentFrame = raw.AnimationFrame;
            Count = (int)raw.AnimationCount;
            Scales = scales;
            Rotations = rotations;
            Translations = translations;
            Animations = animations;
        }
    }

    public class TextureAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int Count { get; }
        public IReadOnlyList<ushort> FrameIndices { get; }
        public IReadOnlyList<ushort> TextureIds { get; }
        public IReadOnlyList<ushort> PaletteIds { get; }
        public IReadOnlyDictionary<string, TextureAnimation> Animations { get; }

        public TextureAnimationGroup(RawTextureAnimationGroup raw, IReadOnlyList<ushort> frameIndices, IReadOnlyList<ushort> textureIds,
            IReadOnlyList<ushort> paletteIds, IReadOnlyDictionary<string, TextureAnimation> animations)
        {
            FrameCount = raw.FrameCount;
            CurrentFrame = raw.AnimationFrame;
            Count = raw.AnimationCount;
            FrameIndices = frameIndices;
            TextureIds = textureIds;
            PaletteIds = paletteIds;
            Animations = animations;
        }
    }

    public class MaterialAnimationGroup
    {
        public int FrameCount { get; }
        public int CurrentFrame { get; set; }
        public int Count { get; }
        public IReadOnlyList<float> Colors { get; }
        public IReadOnlyDictionary<string, MaterialAnimation> Animations { get; }

        public MaterialAnimationGroup(RawMaterialAnimationGroup raw, IReadOnlyList<float> colors,
            IReadOnlyDictionary<string, MaterialAnimation> animations)
        {
            FrameCount = (int)raw.FrameCount;
            CurrentFrame = raw.AnimationFrame;
            Count = (int)raw.AnimationCount;
            Colors = colors;
            Animations = animations;
        }
    }

    public class Entity
    {
        public string NodeName { get; }
        public short LayerMask { get; }
        public ushort Length { get; }
        public EntityType Type { get; }
        public ushort EntityId { get; }
        public bool FirstHunt { get; }

        public IEnumerable<int> LayerIds
        {
            get
            {
                if (FirstHunt)
                {
                    yield return -1;
                }
                else
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if ((LayerMask & (1 << i)) != 0)
                        {
                            yield return i;
                        }
                    }
                }
            }
        }

        public Entity(EntityEntry entry, EntityType type, ushort entityId)
        {
            NodeName = entry.NodeName;
            LayerMask = entry.LayerMask;
            Length = entry.Length;
            if (!Enum.IsDefined(typeof(EntityType), type))
            {
                throw new ProgramException($"Invalid entity type {type}");
            }
            Type = type;
            EntityId = entityId;
            FirstHunt = false;
        }

        public Entity(FhEntityEntry entry, EntityType type, ushort entityId)
        {
            NodeName = entry.NodeName;
            if (!Enum.IsDefined(typeof(EntityType), type))
            {
                throw new ProgramException($"Invalid entity type {type}");
            }
            Type = type;
            EntityId = entityId;
            FirstHunt = true;
        }
    }

    public class Entity<T> : Entity where T : struct
    {
        public T Data { get; }

        public Entity(EntityEntry entry, EntityType type, ushort someId, T data)
            : base(entry, type, someId)
        {
            Data = data;
        }

        public Entity(FhEntityEntry entry, EntityType type, ushort someId, T data)
            : base(entry, type, someId)
        {
            Data = data;
        }
    }

    public readonly struct CollisionVolume
    {
        public readonly VolumeType Type;
        public readonly Vector3 BoxVector1;
        public readonly Vector3 BoxVector2;
        public readonly Vector3 BoxVector3;
        public readonly Vector3 BoxPosition;
        public readonly float BoxDot1;
        public readonly float BoxDot2;
        public readonly float BoxDot3;
        public readonly Vector3 CylinderVector;
        public readonly Vector3 CylinderPosition;
        public readonly float CylinderRadius;
        public readonly float CylinderDot;
        public readonly Vector3 SpherePosition;
        public readonly float SphereRadius;

        public CollisionVolume(RawCollisionVolume raw, VolumeType type)
        {
            Type = type;
            BoxVector1 = raw.BoxVector1.ToFloatVector();
            BoxVector2 = raw.BoxVector2.ToFloatVector();
            BoxVector3 = raw.BoxVector3.ToFloatVector();
            BoxPosition = raw.BoxPosition.ToFloatVector();
            BoxDot1 = raw.BoxDot1.FloatValue;
            BoxDot2 = raw.BoxDot2.FloatValue;
            BoxDot3 = raw.BoxDot3.FloatValue;
            CylinderVector = raw.CylinderVector.ToFloatVector();
            CylinderPosition = raw.CylinderPosition.ToFloatVector();
            CylinderRadius = raw.CylinderRadius.FloatValue;
            CylinderDot = raw.CylinderDot.FloatValue;
            SpherePosition = raw.SpherePosition.ToFloatVector();
            SphereRadius = raw.SphereRadius.FloatValue;
        }
    }

    public class LightSource
    {
        public Entity<LightSourceEntityData> Entity { get; }
        public Vector3 Position { get; }
        public CollisionVolume Volume { get; }
        public bool Light1Enabled { get; }
        public Vector3 Light1Color { get; }
        public Vector3 Light1Vector { get; }
        public bool Light2Enabled { get; }
        public Vector3 Light2Color { get; }
        public Vector3 Light2Vector { get; }

        public LightSource(Entity<LightSourceEntityData> entity)
        {
            Entity = entity;
            Position = entity.Data.Position.ToFloatVector();
            Volume = new CollisionVolume(entity.Data.Volume, entity.Data.VolumeType);
            Light1Enabled = entity.Data.Light1Enabled != 0;
            Light1Color = entity.Data.Light1Color.AsVector3();
            Light1Vector = entity.Data.Light1Vector.ToFloatVector();
            Light2Enabled = entity.Data.Light2Enabled != 0;
            Light2Color = entity.Data.Light2Color.AsVector3();
            Light2Vector = entity.Data.Light2Vector.ToFloatVector();
        }

        // sktodo: return for sphere
        public bool TestPoint(Vector3 point)
        {
            if (Volume.Type == VolumeType.Box)
            {
                Vector3 difference = point - (Volume.BoxPosition + Position);
                float dot1 = Vector3.Dot(Volume.BoxVector1, difference);
                if (dot1 >= 0 && dot1 <= Volume.BoxDot1)
                {
                    float dot2 = Vector3.Dot(Volume.BoxVector2, difference);
                    if (dot2 >= 0 && dot2 <= Volume.BoxDot2)
                    {
                        float dot3 = Vector3.Dot(Volume.BoxVector3, difference);
                        return dot3 >= 0 && dot3 <= Volume.BoxDot3;
                    }
                }
            }
            else if (Volume.Type == VolumeType.Cylinder)
            {
                Vector3 bottom = Volume.CylinderPosition + Position;
                Vector3 top = bottom + Volume.CylinderVector * Volume.CylinderDot;
                if (Vector3.Dot(point - bottom, top - bottom) >= 0)
                {
                    if (Vector3.Dot(point - top, top - bottom) <= 0)
                    {
                        return Vector3.Cross(point - bottom, top - bottom).Length / (top - bottom).Length <= Volume.CylinderRadius;
                    }
                } 
            }
            else if (Volume.Type == VolumeType.Sphere)
            {

            }
            return false;
        }
    }

    // todo: FH game modes
    // todo: use these to set up all the appropriate layer stuff
    public enum GameMode
    {
        SinglePlayer,
        Battle,
        Survival,
        PrimeHunter,
        Capture,
        Bounty,
        Nodes,
        Defender
    }

    [Flags]
    public enum NodeLayer
    {
        None = 0x0000, // todo: um
        Multiplayer0 = 0x0008,
        Multiplayer1 = 0x0010,
        MultiplayerU = 0x0020,
        CaptureTheFlag = 0x4000
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
        public static string Export => _paths.Value.Export;

        private static readonly Lazy<(string FileSystem, string Export)> _paths
            = new Lazy<(string, string)>(() =>
        {
            if (File.Exists("paths.txt"))
            {
                string[] lines = File.ReadAllLines("paths.txt");
                return (lines[0], lines[1]);
            }
            return ("", "");
        });
    }

    public static class CollectionExtensions
    {
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
    }
}
