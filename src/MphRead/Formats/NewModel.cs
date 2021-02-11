using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ModelInstance
    {
        public NewModel Model { get; }
        public NewAnimationInfo AnimInfo { get; } = new NewAnimationInfo();
        public bool IsPlaceholder { get; set; }
        public bool Active { get; set; } = true;

        public ModelInstance(NewModel model)
        {
            Model = model;
            // todo: once we have proper animation selection, this can be removed
            if (Model.AnimationGroups.Node.Count > 0)
            {
                AnimInfo.Node.Index = 0;
                AnimInfo.Node.Group = Model.AnimationGroups.Node[0];
            }
            if (Model.AnimationGroups.Material.Count > 0)
            {
                AnimInfo.Material.Index = 0;
                AnimInfo.Material.Group = Model.AnimationGroups.Material[0];
            }
            if (Model.AnimationGroups.Texcoord.Count > 0)
            {
                AnimInfo.Texcoord.Index = 0;
                AnimInfo.Texcoord.Group = Model.AnimationGroups.Texcoord[0];
            }
            if (Model.AnimationGroups.Texture.Count > 0)
            {
                AnimInfo.Texture.Index = 0;
                AnimInfo.Texture.Group = Model.AnimationGroups.Texture[0];
            }
        }

        public void SetNodeAnim(int index)
        {
            if (index <= -1 || index >= Model.AnimationGroups.Node.Count)
            {
                AnimInfo.Node.Index = -1;
                AnimInfo.Node.Group = null;
            }
            else
            {
                AnimInfo.Node.Index = index;
                AnimInfo.Node.Group = Model.AnimationGroups.Node[index];
            }
            AnimInfo.Node.CurrentFrame = 0;
        }

        public void SetMaterialAnim(int index)
        {
            if (index <= -1 || index >= Model.AnimationGroups.Material.Count)
            {
                AnimInfo.Material.Index = -1;
                AnimInfo.Material.Group = null;
            }
            else
            {
                AnimInfo.Material.Index = index;
                AnimInfo.Material.Group = Model.AnimationGroups.Material[index];
            }
            AnimInfo.Material.CurrentFrame = 0;
        }

        public void SetTexcoordAnim(int index)
        {
            if (index <= -1 || index >= Model.AnimationGroups.Texcoord.Count)
            {
                AnimInfo.Texcoord.Index = -1;
                AnimInfo.Texcoord.Group = null;
            }
            else
            {
                AnimInfo.Texcoord.Index = index;
                AnimInfo.Texcoord.Group = Model.AnimationGroups.Texcoord[index];
            }
            AnimInfo.Texcoord.CurrentFrame = 0;
        }

        public void SeTextureAnim(int index)
        {
            if (index <= -1 || index >= Model.AnimationGroups.Texture.Count)
            {
                AnimInfo.Texture.Index = -1;
                AnimInfo.Texture.Group = null;
            }
            else
            {
                AnimInfo.Texture.Index = index;
                AnimInfo.Texture.Group = Model.AnimationGroups.Texture[index];
            }
            AnimInfo.Texture.CurrentFrame = 0;
        }

        public void UpdateAnimFrames()
        {
            if (AnimInfo.Node.Group != null)
            {
                AnimInfo.Node.CurrentFrame++;
                AnimInfo.Node.CurrentFrame %= AnimInfo.Node.Group.FrameCount;
            }
            if (AnimInfo.Material.Group != null)
            {
                AnimInfo.Material.CurrentFrame++;
                AnimInfo.Material.CurrentFrame %= AnimInfo.Material.Group.FrameCount;
            }
            if (AnimInfo.Texcoord.Group != null)
            {
                AnimInfo.Texcoord.CurrentFrame++;
                AnimInfo.Texcoord.CurrentFrame %= AnimInfo.Texcoord.Group.FrameCount;
            }
            if (AnimInfo.Texture.Group != null)
            {
                AnimInfo.Texture.CurrentFrame++;
                AnimInfo.Texture.CurrentFrame %= AnimInfo.Texture.Group.FrameCount;
            }
        }
    }

    public readonly struct AnimationGroups
    {
        public readonly IReadOnlyList<NodeAnimationGroup> Node;
        public readonly IReadOnlyList<MaterialAnimationGroup> Material;
        public readonly IReadOnlyList<TexcoordAnimationGroup> Texcoord;
        public readonly IReadOnlyList<TextureAnimationGroup> Texture;

        public AnimationGroups(IReadOnlyList<NodeAnimationGroup> nodes, IReadOnlyList<MaterialAnimationGroup> materials,
            IReadOnlyList<TexcoordAnimationGroup> texcoords, IReadOnlyList<TextureAnimationGroup> textures)
        {
            Node = nodes;
            Material = materials;
            Texcoord = texcoords;
            Texture = textures;
        }
    }

    public class NewAnimationInfo
    {
        public NodeAnimationInfo Node { get; } = new NodeAnimationInfo();
        public MaterialAnimationInfo Material { get; } = new MaterialAnimationInfo();
        public TexcoordAnimationInfo Texcoord { get; } = new TexcoordAnimationInfo();
        public TextureAnimationInfo Texture { get; } = new TextureAnimationInfo();
    }

    // none of these setters should be called outside of ModelInstance
    public class NodeAnimationInfo
    {
        public int Index { get; set; } = -1;
        public NodeAnimationGroup? Group { get; set; }
        public int CurrentFrame { get; set; }
    }

    public class MaterialAnimationInfo
    {
        public int Index { get; set; } = -1;
        public MaterialAnimationGroup? Group { get; set; }
        public int CurrentFrame { get; set; }
    }

    public class TexcoordAnimationInfo
    {
        public int Index { get; set; } = -1;
        public TexcoordAnimationGroup? Group { get; set; }
        public int CurrentFrame { get; set; }
    }

    public class TextureAnimationInfo
    {
        public int Index { get; set; } = -1;
        public TextureAnimationGroup? Group { get; set; }
        public int CurrentFrame { get; set; }
    }

    public class NewModel
    {
        private static int _nextId = 0;
        public int Id { get; } = _nextId++;

        public string Name { get; }
        public Header Header { get; }
        public IReadOnlyList<Node> Nodes { get; }
        public IReadOnlyList<Mesh> Meshes { get; }
        public IReadOnlyList<Material> Materials { get; }
        public IReadOnlyList<DisplayList> DisplayLists { get; }
        public IReadOnlyList<Matrix4> TextureMatrices { get; }
        public IReadOnlyList<IReadOnlyList<RenderInstruction>> RenderInstructionLists { get; } // count and order match dlists
        public IReadOnlyList<Recolor> Recolors { get; }
        public IReadOnlyList<int> NodeMatrixIds { get; }
        private readonly float[] _matrixStackValues;
        public IReadOnlyList<float> MatrixStackValues => _matrixStackValues;
        public AnimationGroups AnimationGroups { get; }

        public Vector3 Scale { get; }

        public NewModel(string name, Header header, IEnumerable<RawNode> nodes, IEnumerable<RawMesh> meshes,
            IEnumerable<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, IReadOnlyList<int> nodeWeights)
        {
            Name = name;
            Header = header;
            Nodes = nodes.Select(n => new Node(n)).ToList();
            Meshes = meshes.Select(m => new Mesh(m)).ToList();
            Materials = materials.Select(m => new Material(m)).ToList();
            DisplayLists = dlists;
            RenderInstructionLists = renderInstructions;
            TextureMatrices = textureMatrices;
            Recolors = recolors;
            Debug.Assert(header.NodeWeightCount == nodeWeights.Count || name == "doubleDamage_img");
            Debug.Assert(nodeWeights.Count <= 31);
            NodeMatrixIds = nodeWeights;
            if (header.NodeWeightCount > 0)
            {
                _matrixStackValues = new float[header.NodeWeightCount * 16];
                for (int i = 0; i < header.NodeWeightCount; i++)
                {
                    SetMatrixStackValues(i, Matrix4.Identity);
                }
            }
            else
            {
                _matrixStackValues = Array.Empty<float>();
            }
            AnimationGroups = new AnimationGroups(nodeGroups, materialGroups, texcoordGroups, textureGroups);
            float scale = Header.ScaleBase.FloatValue * (1 << (int)Header.ScaleFactor);
            Scale = new Vector3(scale, scale, scale);
        }

        public void ComputeNodeMatrices(int index)
        {
            if (Nodes.Count == 0 || index == UInt16.MaxValue)
            {
                return;
            }
            for (int i = index; i != UInt16.MaxValue;)
            {
                Node node = Nodes[i];
                // the scale division isn't done by the game, which is why transforms on room nodes don't work,
                // which is probably why they're disabled. they can be reenabled with a switch in the viewer
                var position = new Vector3(
                    node.Position.X / Scale.X,
                    node.Position.Y / Scale.Y,
                    node.Position.Z / Scale.Z
                );
                Matrix4 transform = ComputeNodeTransforms(node.Scale, node.Angle, position);
                if (node.ParentIndex == UInt16.MaxValue)
                {
                    node.Transform = transform;
                }
                else
                {
                    node.Transform = transform * Nodes[node.ParentIndex].Transform;
                }
                if (node.ChildIndex != UInt16.MaxValue)
                {
                    ComputeNodeMatrices(node.ChildIndex);
                }
                i = node.NextIndex;
            }
        }

        // todo: rename/relocate (and now also deduplicate)
        private Matrix4 ComputeNodeTransforms(Vector3 scale, Vector3 angle, Vector3 position)
        {
            float sinAx = MathF.Sin(angle.X);
            float sinAy = MathF.Sin(angle.Y);
            float sinAz = MathF.Sin(angle.Z);
            float cosAx = MathF.Cos(angle.X);
            float cosAy = MathF.Cos(angle.Y);
            float cosAz = MathF.Cos(angle.Z);

            float v18 = cosAx * cosAz;
            float v19 = cosAx * sinAz;
            float v20 = cosAx * cosAy;

            float v22 = sinAx * sinAy;

            float v17 = v19 * sinAy;

            Matrix4 transform = default;

            transform.M11 = scale.X * cosAy * cosAz;
            transform.M12 = scale.X * cosAy * sinAz;
            transform.M13 = scale.X * -sinAy;

            transform.M21 = scale.Y * ((v22 * cosAz) - v19);
            transform.M22 = scale.Y * ((v22 * sinAz) + v18);
            transform.M23 = scale.Y * sinAx * cosAy;

            transform.M31 = scale.Z * (v18 * sinAy + sinAx * sinAz);
            transform.M32 = scale.Z * (v17 + (v19 * sinAy) - (sinAx * cosAz));
            transform.M33 = scale.Z * v20;

            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;

            transform.M14 = 0;
            transform.M24 = 0;
            transform.M34 = 0;
            transform.M44 = 1;

            return transform;
        }

        public void AnimateNodes(int index, bool useNodeTransform, Matrix4 parentTansform, Vector3 scale, NodeAnimationInfo info)
        {
            for (int i = index; i != UInt16.MaxValue;)
            {
                Node node = Nodes[i];
                Matrix4 transform = useNodeTransform ? node.Transform : Matrix4.Identity;
                NodeAnimationGroup? group = info.Group;
                if (group != null && group.Animations.TryGetValue(node.Name, out NodeAnimation animation))
                {
                    transform = AnimateNode(group, animation, scale, info.CurrentFrame);
                    if (node.ParentIndex != UInt16.MaxValue)
                    {
                        transform *= Nodes[node.ParentIndex].Animation;
                    }
                }
                node.Animation = transform;
                if (node.ChildIndex != UInt16.MaxValue)
                {
                    AnimateNodes(node.ChildIndex, useNodeTransform, parentTansform, scale, info);
                }
                node.Animation *= parentTansform;
                i = node.NextIndex;
            }
        }

        private Matrix4 AnimateNode(NodeAnimationGroup group, NodeAnimation animation, Vector3 modelScale, int currentFrame)
        {
            float scaleX = InterpolateAnimation(group.Scales, animation.ScaleLutIndexX, currentFrame,
                animation.ScaleBlendX, animation.ScaleLutLengthX, group.FrameCount);
            float scaleY = InterpolateAnimation(group.Scales, animation.ScaleLutIndexY, currentFrame,
                animation.ScaleBlendY, animation.ScaleLutLengthY, group.FrameCount);
            float scaleZ = InterpolateAnimation(group.Scales, animation.ScaleLutIndexZ, currentFrame,
                animation.ScaleBlendZ, animation.ScaleLutLengthZ, group.FrameCount);
            float rotateX = InterpolateAnimation(group.Rotations, animation.RotateLutIndexX, currentFrame,
                animation.RotateBlendX, animation.RotateLutLengthX, group.FrameCount, isRotation: true);
            float rotateY = InterpolateAnimation(group.Rotations, animation.RotateLutIndexY, currentFrame,
                animation.RotateBlendY, animation.RotateLutLengthY, group.FrameCount, isRotation: true);
            float rotateZ = InterpolateAnimation(group.Rotations, animation.RotateLutIndexZ, currentFrame,
                animation.RotateBlendZ, animation.RotateLutLengthZ, group.FrameCount, isRotation: true);
            float translateX = InterpolateAnimation(group.Translations, animation.TranslateLutIndexX, currentFrame,
                animation.TranslateBlendX, animation.TranslateLutLengthX, group.FrameCount);
            float translateY = InterpolateAnimation(group.Translations, animation.TranslateLutIndexY, currentFrame,
                animation.TranslateBlendY, animation.TranslateLutLengthY, group.FrameCount);
            float translateZ = InterpolateAnimation(group.Translations, animation.TranslateLutIndexZ, currentFrame,
                animation.TranslateBlendZ, animation.TranslateLutLengthZ, group.FrameCount);
            var nodeMatrix = Matrix4.CreateTranslation(translateX / modelScale.X, translateY / modelScale.Y, translateZ / modelScale.Z);
            nodeMatrix = Matrix4.CreateRotationX(rotateX) * Matrix4.CreateRotationY(rotateY) * Matrix4.CreateRotationZ(rotateZ) * nodeMatrix;
            nodeMatrix = Matrix4.CreateScale(scaleX, scaleY, scaleZ) * nodeMatrix;
            return nodeMatrix;
        }

        public void AnimateMaterials(MaterialAnimationInfo info)
        {
            for (int i = 0; i < Materials.Count; i++)
            {
                Material material = Materials[i];
                material.CurrentDiffuse = material.Diffuse / 31.0f;
                material.CurrentAmbient = material.Ambient / 31.0f;
                material.CurrentSpecular = material.Specular / 31.0f;
                material.CurrentAlpha = material.Alpha / 31.0f;
                MaterialAnimationGroup? group = info.Group;
                if (group != null && group.Animations.TryGetValue(material.Name, out MaterialAnimation animation))
                {
                    int currentFrame = info.CurrentFrame;
                    if (!material.AnimationFlags.HasFlag(AnimationFlags.DisableColor))
                    {
                        float diffuseR = InterpolateAnimation(group.Colors, animation.DiffuseLutIndexR, currentFrame,
                            animation.DiffuseBlendR, animation.DiffuseLutLengthR, group.FrameCount);
                        float diffuseG = InterpolateAnimation(group.Colors, animation.DiffuseLutIndexG, currentFrame,
                            animation.DiffuseBlendG, animation.DiffuseLutLengthG, group.FrameCount);
                        float diffuseB = InterpolateAnimation(group.Colors, animation.DiffuseLutIndexB, currentFrame,
                            animation.DiffuseBlendB, animation.DiffuseLutLengthB, group.FrameCount);
                        float ambientR = InterpolateAnimation(group.Colors, animation.AmbientLutIndexR, currentFrame,
                            animation.AmbientBlendR, animation.AmbientLutLengthR, group.FrameCount);
                        float ambientG = InterpolateAnimation(group.Colors, animation.AmbientLutIndexG, currentFrame,
                            animation.AmbientBlendG, animation.AmbientLutLengthG, group.FrameCount);
                        float ambientB = InterpolateAnimation(group.Colors, animation.AmbientLutIndexB, currentFrame,
                            animation.AmbientBlendB, animation.AmbientLutLengthB, group.FrameCount);
                        float specularR = InterpolateAnimation(group.Colors, animation.SpecularLutIndexR, currentFrame,
                            animation.SpecularBlendR, animation.SpecularLutLengthR, group.FrameCount);
                        float specularG = InterpolateAnimation(group.Colors, animation.SpecularLutIndexG, currentFrame,
                            animation.SpecularBlendG, animation.SpecularLutLengthG, group.FrameCount);
                        float specularB = InterpolateAnimation(group.Colors, animation.SpecularLutIndexB, currentFrame,
                            animation.SpecularBlendB, animation.SpecularLutLengthB, group.FrameCount);
                        material.CurrentDiffuse = new Vector3(diffuseR / 31.0f, diffuseG / 31.0f, diffuseB / 31.0f);
                        material.CurrentAmbient = new Vector3(ambientR / 31.0f, ambientG / 31.0f, ambientB / 31.0f);
                        material.CurrentSpecular = new Vector3(specularR / 31.0f, specularG / 31.0f, specularB / 31.0f);
                    }
                    if (!material.AnimationFlags.HasFlag(AnimationFlags.DisableAlpha))
                    {
                        material.CurrentAlpha = InterpolateAnimation(group.Colors, animation.AlphaLutIndex, currentFrame,
                            animation.AlphaBlend, animation.AlphaLutLength, group.FrameCount) / 31.0f;
                    }
                }
            }
        }

        public Matrix4 AnimateTexcoords(TexcoordAnimationGroup group, TexcoordAnimation animation, int currentFrame)
        {
            float scaleS = InterpolateAnimation(group.Scales, animation.ScaleLutIndexS, currentFrame,
                animation.ScaleBlendS, animation.ScaleLutLengthS, group.FrameCount);
            float scaleT = InterpolateAnimation(group.Scales, animation.ScaleLutIndexT, currentFrame,
                animation.ScaleBlendT, animation.ScaleLutLengthT, group.FrameCount);
            float rotate = InterpolateAnimation(group.Rotations, animation.RotateLutIndexZ, currentFrame,
                animation.RotateBlendZ, animation.RotateLutLengthZ, group.FrameCount, isRotation: true);
            float translateS = InterpolateAnimation(group.Translations, animation.TranslateLutIndexS, currentFrame,
                animation.TranslateBlendS, animation.TranslateLutLengthS, group.FrameCount);
            float translateT = InterpolateAnimation(group.Translations, animation.TranslateLutIndexT, currentFrame,
                animation.TranslateBlendT, animation.TranslateLutLengthT, group.FrameCount);
            var textureMatrix = Matrix4.CreateTranslation(translateS, translateT, 0.0f);
            if (rotate != 0)
            {
                textureMatrix = Matrix4.CreateTranslation(0.5f, 0.5f, 0.0f) * textureMatrix;
                textureMatrix = Matrix4.CreateRotationZ(rotate) * textureMatrix;
                textureMatrix = Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f) * textureMatrix;
            }
            textureMatrix = Matrix4.CreateScale(scaleS, scaleT, 1) * textureMatrix;
            return textureMatrix;
        }

        public void AnimateTextures(TextureAnimationInfo info)
        {
            for (int i = 0; i < Materials.Count; i++)
            {
                Material material = Materials[i];
                material.CurrentTextureId = material.TextureId;
                material.CurrentPaletteId = material.PaletteId;
                TextureAnimationGroup? group = info.Group;
                if (group != null && group.Animations.TryGetValue(material.Name, out TextureAnimation animation))
                {
                    for (int j = animation.StartIndex; j < animation.StartIndex + animation.Count; j++)
                    {
                        if (group.FrameIndices[j] == info.CurrentFrame)
                        {
                            material.CurrentTextureId = group.TextureIds[j];
                            material.CurrentPaletteId = group.PaletteIds[j];
                            break;
                        }
                    }
                }
            }
        }

        private float InterpolateAnimation(IReadOnlyList<float> values, int start, int frame, int blend, int lutLength, int frameCount,
            bool isRotation = false)
        {
            if (lutLength == 1)
            {
                return values[start];
            }
            if (blend == 1)
            {
                return values[start + frame];
            }
            int limit = (frameCount - 1) >> (blend >> 1) << (blend >> 1);
            if (frame >= limit)
            {
                return values[start + lutLength - (frameCount - limit - (frame - limit))];
            }
            int index = Math.DivRem(frame, blend, out int remainder);
            if (remainder == 0)
            {
                return values[start + index];
            }
            float first = values[start + index];
            float second = values[start + index + 1];
            if (isRotation)
            {
                if (first - second > MathF.PI)
                {
                    second += MathF.PI * 2f;
                }
                else if (first - second < -MathF.PI)
                {
                    first += MathF.PI * 2f;
                }
            }
            float factor = 1.0f / blend * remainder;
            return first + (second - first) * factor;
        }

        public void UpdateMatrixStack(Matrix4 viewInvRot, Matrix4 viewInvRotY)
        {
            for (int i = 0; i < NodeMatrixIds.Count; i++)
            {
                Node node = Nodes[NodeMatrixIds[i]];
                Matrix4 transform = node.Animation;
                if (node.BillboardMode == BillboardMode.Sphere)
                {
                    transform = viewInvRot * transform.ClearRotation();
                }
                else if (node.BillboardMode == BillboardMode.Cylinder)
                {
                    transform = viewInvRotY * transform.ClearRotation();
                }
                SetMatrixStackValues(i, transform);
            }
        }

        private void SetMatrixStackValues(int index, Matrix4 matrix)
        {
            _matrixStackValues[16 * index] = matrix.M11;
            _matrixStackValues[16 * index + 1] = matrix.M12;
            _matrixStackValues[16 * index + 2] = matrix.M13;
            _matrixStackValues[16 * index + 3] = matrix.M14;
            _matrixStackValues[16 * index + 4] = matrix.M21;
            _matrixStackValues[16 * index + 5] = matrix.M22;
            _matrixStackValues[16 * index + 6] = matrix.M23;
            _matrixStackValues[16 * index + 7] = matrix.M24;
            _matrixStackValues[16 * index + 8] = matrix.M31;
            _matrixStackValues[16 * index + 9] = matrix.M32;
            _matrixStackValues[16 * index + 10] = matrix.M33;
            _matrixStackValues[16 * index + 11] = matrix.M34;
            _matrixStackValues[16 * index + 12] = matrix.M41;
            _matrixStackValues[16 * index + 13] = matrix.M42;
            _matrixStackValues[16 * index + 14] = matrix.M43;
            _matrixStackValues[16 * index + 15] = matrix.M44;
        }

        public IReadOnlyList<ColorRgba> GetPixels(int textureId, int paletteId, int recolorId)
        {
            Recolor recolor = Recolors[recolorId];
            if (textureId < 0 || textureId >= recolor.TextureData.Count)
            {
                throw new ArgumentException(nameof(textureId));
            }
            var pixels = new List<ColorRgba>();
            TextureFormat textureFormat = recolor.Textures[textureId].Format;
            if (textureFormat == TextureFormat.DirectRgb)
            {
                for (int i = 0; i < recolor.TextureData[textureId].Count; i++)
                {
                    uint color = recolor.TextureData[textureId][i].Data;
                    byte alpha = recolor.TextureData[textureId][i].Alpha;
                    pixels.Add(new ColorRgba(color, alpha));
                }
            }
            else
            {
                if (paletteId < 0 || paletteId >= recolor.PaletteData.Count)
                {
                    throw new ArgumentException(nameof(paletteId));
                }
                for (int i = 0; i < recolor.TextureData[textureId].Count; i++)
                {
                    int index = (int)recolor.TextureData[textureId][i].Data;
                    ushort color = recolor.PaletteData[paletteId][index].Data;
                    byte alpha = recolor.TextureData[textureId][i].Alpha;
                    pixels.Add(new ColorRgba(color, alpha));
                }
            }
            return pixels;
        }
    }
}
