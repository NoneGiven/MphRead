using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead
{
    public class Model
    {
        public bool Visible { get; set; } = true;
        public bool ScanVisorOnly { get; set; }
        public bool UseLightSources { get; }
        public bool UseLightOverride { get; set; } // for Octoliths
        public Vector3 Light1Color { get; set; }
        public Vector3 Light1Vector { get; set; }
        public Vector3 Light2Color { get; set; }
        public Vector3 Light2Vector { get; set; }
        public ModelType Type { get; set; }
        public EntityType EntityType { get; set; }
        public ushort EntityLayer { get; set; } = UInt16.MaxValue;
        public byte Flags { get; set; } // todo: enum for model flags

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

        public Vector4? PaletteOverride { get; set; }

        private uint _damageState = 0;
        public uint DamageState
        {
            get
            {
                return _damageState;
            }
            set
            {
                _damageState = value;
                if (value == 1)
                {
                    PaletteOverride = Metadata.RedPalette;
                }
                else if (value == 2)
                {
                    PaletteOverride = Metadata.WhitePalette;
                }
                else
                {
                    PaletteOverride = null;
                }
            }
        }

        // todo: update these as with the other transform properties
        public Vector3 Vector1 = Vector3.UnitY;
        public Vector3 Vector2 = Vector3.UnitZ;

        // todo: handle this better
        public Vector3 InitialPosition { get; set; }

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
        public float SpinSpeed { get; set; }
        // refers to the untransformed model's axis
        public Vector3 SpinAxis { get; set; } = Vector3.UnitY;

        public AnimationInfo Animations { get; }

        public IReadOnlyList<Recolor> Recolors { get; }

        public IReadOnlyList<int> NodeMatrixIds { get; }
        public float[] MatrixStackValues { get; }

        // todo: refactor model vs. entity abstraction
        private Entity? _entity = null;
        public Entity? Entity
        {
            get
            {
                return _entity;
            }
            set
            {
                _entity = value;
                if (_entity?.Type == EntityType.TriggerVolume)
                {
                    ParentId = ((Entity<TriggerVolumeEntityData>)_entity).Data.ParentId;
                    ChildId = ((Entity<TriggerVolumeEntityData>)_entity).Data.ChildId;
                }
                else if (_entity?.Type == EntityType.AreaVolume)
                {
                    ParentId = ((Entity<AreaVolumeEntityData>)_entity).Data.ParentId;
                    ChildId = ((Entity<AreaVolumeEntityData>)_entity).Data.ChildId;
                }
            }
        }

        public ushort ParentId { get; private set; } = UInt16.MaxValue;
        public ushort ChildId { get; private set; } = UInt16.MaxValue;

        private static int _nextSceneId = 0;
        public int SceneId { get; } = _nextSceneId++;

        public Model(string name, Header header, IReadOnlyList<RawNode> nodes, IReadOnlyList<RawMesh> meshes,
            IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources,
            IReadOnlyList<int> nodeMatrixIds)
        {
            ThrowIfInvalidEnums(materials);
            Name = name;
            Header = header;
            Nodes = nodes.Select(n => new Node(n)).ToList();
            Meshes = meshes.Select(m => new Mesh(m)).ToList();
            Materials = materials.Select(m => new Material(m)).ToList();
            DisplayLists = dlists;
            RenderInstructionLists = renderInstructions;
            Animations = new AnimationInfo(nodeGroups, materialGroups, texcoordGroups, textureGroups);
            TextureMatrices = textureMatrices;
            Recolors = recolors;
            CurrentRecolor = defaultRecolor;
            float scale = Header.ScaleBase.FloatValue * (1 << (int)Header.ScaleFactor);
            Scale = new Vector3(scale, scale, scale);
            UseLightSources = useLightSources;
            // sktodo: field and property naming
            // todo: could assert more about any MTX_RESTORE calls in the dlists (see test method)
            Debug.Assert(header.NodeAnimationCount == nodeMatrixIds.Count);
            NodeMatrixIds = nodeMatrixIds;
            if (header.NodeAnimationCount > 0)
            {
                var values = new List<float>();
                Matrix4 identity = Matrix4.Identity;
                for (int i = 0; i < header.NodeAnimationCount; i++)
                {
                    values.Add(identity.M11);
                    values.Add(identity.M12);
                    values.Add(identity.M13);
                    values.Add(identity.M14);
                    values.Add(identity.M21);
                    values.Add(identity.M22);
                    values.Add(identity.M23);
                    values.Add(identity.M24);
                    values.Add(identity.M31);
                    values.Add(identity.M32);
                    values.Add(identity.M33);
                    values.Add(identity.M34);
                    values.Add(identity.M41);
                    values.Add(identity.M42);
                    values.Add(identity.M43);
                    values.Add(identity.M44);
                }
                MatrixStackValues = values.ToArray();
            }
            else
            {
                MatrixStackValues = new float[] { };
            }
            Flags = header.Flags;
            if (materials.Any(m => m.Lighting > 0))
            {
                Flags |= 1;
            }
            // unlike Morph Ball, Dialanche applies its rotation to the root node transform
            if (Name == "SpireAlt_lod0")
            {
                ExtraTexgenTransform = true;
            }
        }

        public Model(Model other)
        {
            Name = other.Name;
            Header = other.Header;
            Nodes = other.Nodes;
            Meshes = other.Meshes;
            Materials = other.Materials;
            DisplayLists = other.DisplayLists;
            RenderInstructionLists = other.RenderInstructionLists;
            Animations = other.Animations;
            TextureMatrices = other.TextureMatrices;
            Recolors = other.Recolors;
            CurrentRecolor = other.CurrentRecolor;
            Scale = other.Scale;
            UseLightSources = other.UseLightSources;
            NodeMatrixIds = other.NodeMatrixIds;
            MatrixStackValues = other.MatrixStackValues;
            Flags = other.Flags;
            ExtraTexgenTransform = other.ExtraTexgenTransform;
        }

        public IEnumerable<ColorRgba> GetPixels(int textureId, int paletteId)
        {
            return Recolors[CurrentRecolor].GetPixels(textureId, paletteId);
        }

        public virtual IEnumerable<NodeInfo> GetDrawNodes(bool includeForceFields)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                yield return new NodeInfo(Nodes[i]);
            }
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

        // todo: these belong on the subclass
        public int GetNextRoomPartId(int nodeId)
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
                if (Nodes[i].IsRoomPartNode)
                {
                    return i;
                }
                i++;
            }
            return nodeId;
        }

        public int GetPrevRoomPartId(int nodeId)
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
                if (Nodes[i].IsRoomPartNode)
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

        public Matrix4 ExtraTransform { get; private set; } = Matrix4.Identity;
        public bool ExtraTexgenTransform { get; }

        public virtual void Process(double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            // todo: FPS stuff
            if (frameCount != 0 && frameCount % 2 == 0)
            {
                UpdateAnimationFrames();
            }
            // for Morph Ball/Dialanche, the extra transform holds model-level rotation
            ExtraTransform = Transform;
            // for items, the extra transform holds the rotation and position for spinning and floating
            if (Rotating)
            {
                Spin = (float)(Spin + elapsedTime * 360 * SpinSpeed) % 360;
                Matrix4 transform = Matrix4.Identity;
                if (Animations.NodeGroupId == -1)
                {
                    transform = SceneSetup.ComputeNodeTransforms(Vector3.One, new Vector3(
                        MathHelper.DegreesToRadians(SpinAxis.X * Spin),
                        MathHelper.DegreesToRadians(SpinAxis.Y * Spin),
                        MathHelper.DegreesToRadians(SpinAxis.Z * Spin)),
                        Vector3.Zero);
                }
                if (Floating)
                {
                    transform.M42 += (MathF.Sin(Spin / 180 * MathF.PI) + 1) / 8f;
                }
                ExtraTransform = transform * ExtraTransform;
            }
            if (Nodes.Count > 0)
            {
                AnimateNodes(0, viewInvRot, viewInvRotY, useTransform);
                for (int i = 0; i < NodeMatrixIds.Count; i++)
                {
                    Node node = Nodes[NodeMatrixIds[i]];
                    MatrixStackValues[0 + 16 * i] = node.Animation.M11;
                    MatrixStackValues[1 + 16 * i] = node.Animation.M12;
                    MatrixStackValues[2 + 16 * i] = node.Animation.M13;
                    MatrixStackValues[3 + 16 * i] = node.Animation.M14;
                    MatrixStackValues[4 + 16 * i] = node.Animation.M21;
                    MatrixStackValues[5 + 16 * i] = node.Animation.M22;
                    MatrixStackValues[6 + 16 * i] = node.Animation.M23;
                    MatrixStackValues[7 + 16 * i] = node.Animation.M24;
                    MatrixStackValues[8 + 16 * i] = node.Animation.M31;
                    MatrixStackValues[9 + 16 * i] = node.Animation.M32;
                    MatrixStackValues[10 + 16 * i] = node.Animation.M33;
                    MatrixStackValues[11 + 16 * i] = node.Animation.M34;
                    MatrixStackValues[12 + 16 * i] = node.Animation.M41;
                    MatrixStackValues[13 + 16 * i] = node.Animation.M42;
                    MatrixStackValues[14 + 16 * i] = node.Animation.M43;
                    MatrixStackValues[15 + 16 * i] = node.Animation.M44;
                }
            }
        }

        private void AnimateNodes(int index, Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            for (int i = index; i != UInt16.MaxValue;)
            {
                Node node = Nodes[i];
                NodeAnimationGroup? group = Animations.NodeGroup;
                Matrix4 transform = useTransform ? node.Transform : Matrix4.Identity;
                if (group != null && group.Animations.TryGetValue(node.Name, out NodeAnimation animation))
                {
                    // todo: move this and other stuff
                    transform = RenderWindow.AnimateNode(group, animation, Scale);
                    if (node.ParentIndex != UInt16.MaxValue)
                    {
                        transform *= Nodes[node.ParentIndex].Animation;
                    }
                }
                node.Animation = transform;
                if (node.ChildIndex != UInt16.MaxValue)
                {
                    AnimateNodes(node.ChildIndex, viewInvRot, viewInvRotY, useTransform);
                }
                node.Animation *= ExtraTransform;
                if (node.BillboardMode == BillboardMode.Sphere)
                {
                    node.Animation = viewInvRot * node.Animation.ClearRotation();
                }
                else if (node.BillboardMode == BillboardMode.Cylinder)
                {
                    node.Animation = viewInvRotY * node.Animation.ClearRotation();
                }
                i = node.NextIndex;
            }
        }

        private void UpdateAnimationFrames()
        {
            if (Animations.MaterialGroupId != -1)
            {
                Animations.MaterialGroup!.CurrentFrame++;
                Animations.MaterialGroup.CurrentFrame %= Animations.MaterialGroup.FrameCount;
            }
            if (Animations.TexcoordGroupId != -1)
            {
                Animations.TexcoordGroup!.CurrentFrame++;
                Animations.TexcoordGroup.CurrentFrame %= Animations.TexcoordGroup.FrameCount;
            }
            if (Animations.TextureGroupId != -1)
            {
                Animations.TextureGroup!.CurrentFrame++;
                Animations.TextureGroup.CurrentFrame %= Animations.TextureGroup.FrameCount;
            }
            if (Animations.NodeGroupId != -1)
            {
                Animations.NodeGroup!.CurrentFrame++;
                Animations.NodeGroup.CurrentFrame %= Animations.NodeGroup.FrameCount;
            }
        }
    }

    public readonly struct NodeInfo
    {
        public readonly Node Node;
        public readonly CollisionPortal? Portal;

        public NodeInfo(Node node)
        {
            Node = node;
            Portal = null;
        }

        public NodeInfo(Node node, CollisionPortal portal)
        {
            Node = node;
            Portal = portal;
        }
    }

    public class AnimationInfo
    {
        public IReadOnlyList<NodeAnimationGroup> NodeGroups { get; }
        public IReadOnlyList<MaterialAnimationGroup> MaterialGroups { get; }
        public IReadOnlyList<TexcoordAnimationGroup> TexcoordGroups { get; }
        public IReadOnlyList<TextureAnimationGroup> TextureGroups { get; }

        public NodeAnimationGroup? NodeGroup { get; private set; }
        public MaterialAnimationGroup? MaterialGroup { get; private set; }
        public TexcoordAnimationGroup? TexcoordGroup { get; private set; }
        public TextureAnimationGroup? TextureGroup { get; private set; }

        private int _nodeGroupId = -1;
        private int _materialGroupId = -1;
        private int _texcoordGroupId = -1;
        private int _textureGroupId = -1;

        public int NodeGroupId
        {
            get
            {
                return _nodeGroupId;
            }
            set
            {
                _nodeGroupId = value;
                if (_nodeGroupId == -1)
                {
                    NodeGroup = null;
                }
                else
                {
                    NodeGroup = NodeGroups[_nodeGroupId];
                    NodeGroup.CurrentFrame = 0;
                }
            }
        }

        public int MaterialGroupId
        {
            get
            {
                return _materialGroupId;
            }
            set
            {
                _materialGroupId = value;
                if (_materialGroupId == -1)
                {
                    MaterialGroup = null;
                }
                else
                {
                    MaterialGroup = MaterialGroups[_materialGroupId];
                    MaterialGroup.CurrentFrame = 0;
                }
            }
        }

        public int TexcoordGroupId
        {
            get
            {
                return _texcoordGroupId;
            }
            set
            {
                _texcoordGroupId = value;
                if (_texcoordGroupId == -1)
                {
                    TexcoordGroup = null;
                }
                else
                {
                    TexcoordGroup = TexcoordGroups[_texcoordGroupId];
                    TexcoordGroup.CurrentFrame = 0;
                }
            }
        }

        public int TextureGroupId
        {
            get
            {
                return _textureGroupId;
            }
            set
            {
                _textureGroupId = value;
                if (_textureGroupId == -1)
                {
                    TextureGroup = null;
                }
                else
                {
                    TextureGroup = TextureGroups[_textureGroupId];
                    TextureGroup.CurrentFrame = 0;
                }
            }
        }

        // in-game, animations other than node are all set together
        public int TexUvMatId
        {
            set
            {
                MaterialGroupId = value;
                TexcoordGroupId = value;
                TextureGroupId = value;
            }
        }

        public AnimationInfo(IReadOnlyList<NodeAnimationGroup> nodes, IReadOnlyList<MaterialAnimationGroup> materials,
            IReadOnlyList<TexcoordAnimationGroup> texcoords, IReadOnlyList<TextureAnimationGroup> textures)
        {
            NodeGroups = nodes;
            MaterialGroups = materials;
            TexcoordGroups = texcoords;
            TextureGroups = textures;
            // todo: proper animation selection
            if (NodeGroups.Count > 0)
            {
                NodeGroupId = 0;
            }
            if (MaterialGroups.Count > 0)
            {
                MaterialGroupId = 0;
            }
            if (TexcoordGroups.Count > 0)
            {
                TexcoordGroupId = 0;
            }
            if (TextureGroups.Count > 0)
            {
                TextureGroupId = 0;
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

        // todo: just return float color early
        private ColorRgba ColorFromShort(uint value, byte alpha)
        {
            byte red = (byte)MathF.Round(((value >> 0) & 0x1F) / 31f * 255f);
            byte green = (byte)MathF.Round(((value >> 5) & 0x1F) / 31f * 255f);
            byte blue = (byte)MathF.Round(((value >> 10) & 0x1F) / 31f * 255f);
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
}
