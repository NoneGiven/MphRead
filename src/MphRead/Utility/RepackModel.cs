using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Utility
{
    public enum RepackFilter
    {
        All,
        SinglePlayer,
        Multiplayer
    }

    public static partial class Repack
    {
        public static (byte[], byte[]) RepackRoomModel(string room, bool separateTextures, RepackFilter filter = RepackFilter.All)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            if (separateTextures && meta.TexturePath != null)
            {
                throw new ProgramException($"Room {room} already has a separate texture file.");
            }
            Model model = Read.GetRoomModelInstance(meta.Name).Model;
            Debug.Assert(model.Scale.X == model.Scale.Y && model.Scale.Y == model.Scale.Z);
            Debug.Assert(model.Scale.X == (int)model.Scale.X);
            var textureInfo = new List<TextureInfo>();
            for (int i = 0; i < model.Recolors[0].Textures.Count; i++)
            {
                Texture texture = model.Recolors[0].Textures[i];
                IReadOnlyList<TextureData> data = model.Recolors[0].TextureData[i];
                textureInfo.Add(ConvertData(texture, data));
            }
            var paletteInfo = new List<PaletteInfo>();
            foreach (IReadOnlyList<PaletteData> data in model.Recolors[0].PaletteData)
            {
                paletteInfo.Add(new PaletteInfo(data.Select(d => d.Data).ToList()));
            }
            if (filter != RepackFilter.All)
            {
                int layerMask = filter == RepackFilter.Multiplayer
                    ? SceneSetup.GetNodeLayer(GameMode.Battle, roomLayer: 0, playerCount: 2)
                    : SceneSetup.GetNodeLayer(GameMode.SinglePlayer, meta.NodeLayer, playerCount: 1);
                model.FilterNodes(layerMask);
            }
            var options = new RepackOptions()
            {
                IsRoom = true,
                Texture = separateTextures ? RepackTexture.Separate : RepackTexture.Inline,
                ComputeBounds = ComputeBounds.None
            };
            return PackModel((int)model.Scale.X, model.NodeMatrixIds, model.NodePosCounts, model.Materials,
                textureInfo, paletteInfo, model.Nodes, model.Meshes, model.RenderInstructionLists, model.DisplayLists, options);
        }

        public static void TestRepack()
        {
            Read.ApplyFixes = false;
            foreach (ModelMetadata meta in Metadata.ModelMetadata.Values.Concat(Metadata.FirstHuntModels.Values))
            {
                // todo: support texture shares
                Model model = Read.GetModelInstance(meta.Name, meta.FirstHunt).Model;
                int i = 0;
                foreach (RecolorMetadata recolor in meta.Recolors)
                {
                    // data is from separate models which will get tested themselves
                    if ((meta.Name == "samus_hi_yellow" || meta.Name == "samus_low_yellow" || meta.Name == "morphBall") && i != 0)
                    {
                        break;
                    }
                    string modelPath = meta.ModelPath;
                    // data is from separate models which won't get tested
                    if (meta.Name == "arcWelder1")
                    {
                        modelPath = modelPath.Replace("arcWelder1", $"arcWelder{i + 1}");
                    }
                    var options = new RepackOptions()
                    {
                        IsRoom = false,
                        Texture = RepackTexture.Inline,
                        ComputeBounds = ComputeBounds.None
                    };
                    if (meta.ModelPath != recolor.TexturePath || meta.ModelPath != recolor.PalettePath)
                    {
                        options.Texture = recolor.TexturePath.ToLower().Contains("share") || meta.ModelPath != recolor.PalettePath
                            ? RepackTexture.Shared
                            : RepackTexture.Separate;
                    }
                    TestModelRepack(model, recolor: i++, modelPath, recolor.TexturePath, meta.FirstHunt, options);
                }
                // todo: support animation shares, I guess
                if (meta.AnimationPath != null && meta.AnimationShare == null)
                {
                    TestAnimRepack(model, meta.AnimationPath, meta.FirstHunt, writeFile: false);
                }
            }
            foreach (RoomMetadata meta in Metadata.RoomMetadata.Values)
            {
                var options = new RepackOptions()
                {
                    IsRoom = true,
                    Texture = meta.TexturePath == null || meta.ModelPath == meta.TexturePath
                        ? RepackTexture.Inline
                        : RepackTexture.Separate,
                    ComputeBounds = ComputeBounds.None
                };
                //options.Texture = RepackTexture.Separate;
                //options.WriteFile = true;
                Model model = Read.GetRoomModelInstance(meta.Name).Model;
                TestModelRepack(model, recolor: 0, meta.ModelPath, meta.TexturePath, meta.FirstHunt || meta.Hybrid, options);
                if (meta.AnimationPath != null)
                {
                    TestAnimRepack(model, meta.AnimationPath, meta.FirstHunt || meta.Hybrid, options.WriteFile);
                }
            }
            Read.ApplyFixes = true;
        }

        public static void TestRepack(string name, int recolor = 0, bool firstHunt = false)
        {
            Read.ApplyFixes = false;
            var options = new RepackOptions()
            {
                IsRoom = false,
                WriteFile = true,
                ComputeBounds = ComputeBounds.None
            };
            ModelMetadata meta = firstHunt ? Metadata.FirstHuntModels[name] : Metadata.ModelMetadata[name];
            Model model = Read.GetModelInstance(meta.Name, meta.FirstHunt).Model;
            TestModelRepack(model, recolor, meta.ModelPath, meta.Recolors[recolor].TexturePath, meta.FirstHunt, options);
            if (meta.AnimationPath != null && meta.AnimationShare == null)
            {
                TestAnimRepack(model, meta.AnimationPath, meta.FirstHunt, options.WriteFile);
            }
            Read.ApplyFixes = true;
        }

        private static void TestAnimRepack(Model model, string animPath, bool firstHunt, bool writeFile)
        {
            var node = new List<NodeAnimationGroup?>();
            var mat = new List<MaterialAnimationGroup?>();
            var uv = new List<TexcoordAnimationGroup?>();
            var tex = new List<TextureAnimationGroup?>();
            int index = 0;
            foreach (int offset in model.AnimationGroups.Offsets.Node)
            {
                node.Add(offset == 0 ? null : model.AnimationGroups.Node[index++]);
            }
            index = 0;
            foreach (int offset in model.AnimationGroups.Offsets.Material)
            {
                mat.Add(offset == 0 ? null : model.AnimationGroups.Material[index++]);
            }
            index = 0;
            foreach (int offset in model.AnimationGroups.Offsets.Texcoord)
            {
                uv.Add(offset == 0 ? null : model.AnimationGroups.Texcoord[index++]);
            }
            index = 0;
            foreach (int offset in model.AnimationGroups.Offsets.Texture)
            {
                tex.Add(offset == 0 ? null : model.AnimationGroups.Texture[index++]);
            }
            bool fhPad = animPath.EndsWith("testlevel_Anim.bin");
            byte[] bytes = PackAnim(node, mat, uv, tex, fhPad);
            byte[] fileBytes = File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, animPath));
            CompareAnims(model.Name, bytes, fileBytes);
            if (writeFile)
            {
                File.WriteAllBytes(Path.Combine(Paths.Export, "_pack", $"out_{model.Name}_Anim.bin"), bytes);
            }
            Nop();
        }

        private static void TestModelRepack(Model model, int recolor, string modelPath, string? texPath, bool firstHunt, RepackOptions options)
        {
            if (options.Texture == RepackTexture.Shared)
            {
                return;
            }
            var textureInfo = new List<TextureInfo>();
            for (int i = 0; i < model.Recolors[recolor].Textures.Count; i++)
            {
                Texture texture = model.Recolors[recolor].Textures[i];
                IReadOnlyList<TextureData> data = model.Recolors[recolor].TextureData[i];
                textureInfo.Add(ConvertData(texture, data));
            }
            var paletteInfo = new List<PaletteInfo>();
            foreach (IReadOnlyList<PaletteData> data in model.Recolors[recolor].PaletteData)
            {
                paletteInfo.Add(new PaletteInfo(data.Select(d => d.Data).ToList()));
            }
            Debug.Assert(model.Scale.X == model.Scale.Y && model.Scale.Y == model.Scale.Z);
            Debug.Assert(model.Scale.X == (int)model.Scale.X);
            (byte[] bytes, byte[] tex) = PackModel((int)model.Scale.X, model.NodeMatrixIds, model.NodePosCounts, model.Materials,
                textureInfo, paletteInfo, model.Nodes, model.Meshes, model.RenderInstructionLists, model.DisplayLists, options);
            byte[] fileBytes = File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, modelPath));
            if (options.Compare)
            {
                CompareModels(model.Name, bytes, fileBytes, options);
                if (options.Texture == RepackTexture.Separate)
                {
                    Debug.Assert(texPath != null);
                    byte[] texFile = File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, texPath));
                    Debug.Assert(tex.Length == texFile.Length);
                    Debug.Assert(Enumerable.SequenceEqual(tex, texFile));
                }
            }
            if (options.WriteFile)
            {
                File.WriteAllBytes(Path.Combine(Paths.Export, "_pack", $"out_{model.Name}_{model.Recolors[recolor].Name}.bin"), bytes);
                if (options.Texture == RepackTexture.Separate)
                {
                    File.WriteAllBytes(Path.Combine(Paths.Export, "_pack", $"out_{model.Name}_Tex.bin"), tex);
                }
            }
            Nop();
        }

        public static void CompareModels(string model1, string model2, string game1 = "amhe1", string game2 = "amhe1")
        {
            ModelMetadata meta1 = Metadata.ModelMetadata[model1];
            ModelMetadata meta2 = Metadata.ModelMetadata[model2];
            string path1 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game1, meta1.ModelPath);
            string path2 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game2, meta2.ModelPath);
            CompareModels(model1, File.ReadAllBytes(path1), File.ReadAllBytes(path2), new RepackOptions()
            {
                Texture = RepackTexture.Separate
            });
            Nop();
        }

        public static void CompareAnims(string model1, string model2, string game1 = "amhe1", string game2 = "amhe1")
        {
            ModelMetadata meta1 = Metadata.ModelMetadata[model1];
            ModelMetadata meta2 = Metadata.ModelMetadata[model2];
            if (meta1.AnimationPath != null && meta2.AnimationPath != null)
            {
                string path1 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game1, meta1.AnimationPath);
                string path2 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game2, meta2.AnimationPath);
                CompareAnims(model1, File.ReadAllBytes(path1), File.ReadAllBytes(path2));
            }
            Nop();
        }

        private static void CompareModels(string name, byte[] bytes, byte[] otherBytes, RepackOptions options)
        {
            string temp = name;
            Debug.Assert(bytes.Length == otherBytes.Length);
            Header header = Read.ReadStruct<Header>(bytes);
            Header other = Read.ReadStruct<Header>(otherBytes);
            Debug.Assert(header.ScaleFactor == other.ScaleFactor);
            Debug.Assert(header.ScaleBase.Value == other.ScaleBase.Value);
            Debug.Assert(header.PrimitiveCount == other.PrimitiveCount);
            Debug.Assert(header.VertexCount == other.VertexCount);
            Debug.Assert(header.MaterialOffset == other.MaterialOffset);
            Debug.Assert(header.DlistOffset == other.DlistOffset);
            Debug.Assert(header.NodeOffset == other.NodeOffset);
            Debug.Assert(header.NodeWeightCount == other.NodeWeightCount);
            Debug.Assert(header.NodeWeightOffset == other.NodeWeightOffset);
            Debug.Assert(header.MeshOffset == other.MeshOffset);
            Debug.Assert(header.TextureCount == other.TextureCount);
            Debug.Assert(header.TextureOffset == other.TextureOffset);
            Debug.Assert(header.PaletteCount == other.PaletteCount);
            Debug.Assert(header.PaletteOffset == other.PaletteOffset);
            Debug.Assert(header.NodePosCounts == other.NodePosCounts);
            Debug.Assert(header.NodePosScales == other.NodePosScales);
            Debug.Assert(header.NodeInitialPosition == other.NodeInitialPosition);
            Debug.Assert(header.NodePosition == other.NodePosition);
            Debug.Assert(header.MaterialCount == other.MaterialCount);
            Debug.Assert(header.NodeCount == other.NodeCount);
            Debug.Assert(header.NodeAnimationOffset == other.NodeAnimationOffset);
            Debug.Assert(header.TextureCoordinateAnimations == other.TextureCoordinateAnimations);
            Debug.Assert(header.MaterialAnimations == other.MaterialAnimations);
            Debug.Assert(header.TextureAnimations == other.TextureAnimations);
            Debug.Assert(header.MeshCount == other.MeshCount);
            Debug.Assert(header.TextureMatrixCount == other.TextureMatrixCount);
            IReadOnlyList<Texture> texes = Read.DoOffsets<Texture>(bytes, header.TextureOffset, header.TextureCount);
            IReadOnlyList<Texture> otherTexes = Read.DoOffsets<Texture>(otherBytes, other.TextureOffset, other.TextureCount);
            for (int i = 0; i < texes.Count; i++)
            {
                Texture tex = texes[i];
                Texture otherTex = otherTexes[i];
                Debug.Assert(tex.Format == otherTex.Format);
                Debug.Assert(tex.Width == otherTex.Width);
                Debug.Assert(tex.Height == otherTex.Height);
                Debug.Assert(tex.ImageOffset == otherTex.ImageOffset);
                Debug.Assert(tex.ImageSize == otherTex.ImageSize);
                Debug.Assert(tex.Opaque == otherTex.Opaque);
                if (options.Texture == RepackTexture.Inline)
                {
                    IReadOnlyList<byte> texData = Read.DoOffsets<byte>(bytes, tex.ImageOffset, tex.ImageSize);
                    IReadOnlyList<byte> otherTexData = Read.DoOffsets<byte>(otherBytes, otherTex.ImageOffset, otherTex.ImageSize);
                    Debug.Assert(Enumerable.SequenceEqual(texData, otherTexData));
                }
            }
            IReadOnlyList<Palette> pals = Read.DoOffsets<Palette>(bytes, header.PaletteOffset, header.PaletteCount);
            IReadOnlyList<Palette> otherPals = Read.DoOffsets<Palette>(otherBytes, other.PaletteOffset, other.PaletteCount);
            for (int i = 0; i < pals.Count; i++)
            {
                Palette pal = pals[i];
                Palette otherPal = otherPals[i];
                Debug.Assert(pal.Offset == otherPal.Offset);
                Debug.Assert(pal.Size == otherPal.Size);
                if (options.Texture == RepackTexture.Inline)
                {
                    IReadOnlyList<byte> palData = Read.DoOffsets<byte>(bytes, pal.Offset, pal.Size);
                    IReadOnlyList<byte> otherPalData = Read.DoOffsets<byte>(otherBytes, otherPal.Offset, otherPal.Size);
                    Debug.Assert(Enumerable.SequenceEqual(palData, otherPalData));
                }
            }
            IReadOnlyList<RawMaterial> mats = Read.DoOffsets<RawMaterial>(bytes, header.MaterialOffset, header.MaterialCount);
            IReadOnlyList<RawMaterial> otherMats = Read.DoOffsets<RawMaterial>(otherBytes, other.MaterialOffset, other.MaterialCount);
            for (int i = 0; i < mats.Count; i++)
            {
                RawMaterial mat = mats[i];
                RawMaterial otherMat = otherMats[i];
                Debug.Assert(Enumerable.SequenceEqual(mat.Name, otherMat.Name));
                Debug.Assert(mat.Alpha == otherMat.Alpha);
                Debug.Assert(mat.Diffuse.Red == otherMat.Diffuse.Red);
                Debug.Assert(mat.Diffuse.Green == otherMat.Diffuse.Green);
                Debug.Assert(mat.Diffuse.Blue == otherMat.Diffuse.Blue);
                Debug.Assert(mat.Ambient.Red == otherMat.Ambient.Red);
                Debug.Assert(mat.Ambient.Green == otherMat.Ambient.Green);
                Debug.Assert(mat.Ambient.Blue == otherMat.Ambient.Blue);
                Debug.Assert(mat.Specular.Red == otherMat.Specular.Red);
                Debug.Assert(mat.Specular.Green == otherMat.Specular.Green);
                Debug.Assert(mat.Specular.Blue == otherMat.Specular.Blue);
                Debug.Assert(mat.AnimationFlags == otherMat.AnimationFlags);
                Debug.Assert(mat.Culling == otherMat.Culling);
                Debug.Assert(mat.Lighting == otherMat.Lighting);
                Debug.Assert(mat.MatrixId == otherMat.MatrixId);
                Debug.Assert(mat.PaletteId == otherMat.PaletteId);
                Debug.Assert(mat.PolygonMode == otherMat.PolygonMode);
                Debug.Assert(mat.RenderMode == otherMat.RenderMode);
                Debug.Assert(mat.RotateZ == otherMat.RotateZ);
                Debug.Assert(mat.ScaleS.Value == otherMat.ScaleS.Value);
                Debug.Assert(mat.ScaleT.Value == otherMat.ScaleT.Value);
                Debug.Assert(mat.TranslateS.Value == otherMat.TranslateS.Value);
                Debug.Assert(mat.TranslateT.Value == otherMat.TranslateT.Value);
                Debug.Assert(mat.TexcoordTransformMode == otherMat.TexcoordTransformMode);
                Debug.Assert(mat.TextureId == otherMat.TextureId);
                Debug.Assert(mat.Wireframe == otherMat.Wireframe);
                Debug.Assert(mat.XRepeat == otherMat.XRepeat);
                Debug.Assert(mat.YRepeat == otherMat.YRepeat);
            }
            IReadOnlyList<RawNode> nodes = Read.DoOffsets<RawNode>(bytes, header.NodeOffset, header.NodeCount);
            IReadOnlyList<RawNode> otherNodes = Read.DoOffsets<RawNode>(otherBytes, other.NodeOffset, other.NodeCount);
            for (int i = 0; i < nodes.Count; i++)
            {
                RawNode node = nodes[i];
                RawNode otherNode = otherNodes[i];
                Debug.Assert(Enumerable.SequenceEqual(node.Name, otherNode.Name));
                Debug.Assert(node.AngleX == otherNode.AngleX);
                Debug.Assert(node.AngleY == otherNode.AngleY);
                Debug.Assert(node.AngleZ == otherNode.AngleZ);
                Debug.Assert(node.BillboardMode == otherNode.BillboardMode);
                Debug.Assert(node.BoundingRadius.Value == otherNode.BoundingRadius.Value);
                Debug.Assert(node.ChildId == otherNode.ChildId);
                Debug.Assert(node.NextId == otherNode.NextId);
                Debug.Assert(node.ParentId == otherNode.ParentId);
                Debug.Assert(node.Enabled == otherNode.Enabled);
                Debug.Assert(node.MinBounds.X.Value == otherNode.MinBounds.X.Value);
                Debug.Assert(node.MinBounds.Y.Value == otherNode.MinBounds.Y.Value);
                Debug.Assert(node.MinBounds.Z.Value == otherNode.MinBounds.Z.Value);
                Debug.Assert(node.MaxBounds.X.Value == otherNode.MaxBounds.X.Value);
                Debug.Assert(node.MaxBounds.Y.Value == otherNode.MaxBounds.Y.Value);
                Debug.Assert(node.MaxBounds.Z.Value == otherNode.MaxBounds.Z.Value);
                Debug.Assert(node.MeshCount == otherNode.MeshCount);
                Debug.Assert(node.MeshId == otherNode.MeshId);
                Debug.Assert(node.Position.X.Value == otherNode.Position.X.Value);
                Debug.Assert(node.Position.Y.Value == otherNode.Position.Y.Value);
                Debug.Assert(node.Position.Z.Value == otherNode.Position.Z.Value);
                Debug.Assert(node.Scale.X.Value == otherNode.Scale.X.Value);
                Debug.Assert(node.Scale.Y.Value == otherNode.Scale.Y.Value);
                Debug.Assert(node.Scale.Z.Value == otherNode.Scale.Z.Value);
            }
            IReadOnlyList<RawMesh> meshes = Read.DoOffsets<RawMesh>(bytes, header.MeshOffset, header.MeshCount);
            IReadOnlyList<RawMesh> otherMeshes = Read.DoOffsets<RawMesh>(otherBytes, other.MeshOffset, other.MeshCount);
            for (int i = 0; i < meshes.Count; i++)
            {
                RawMesh mesh = meshes[i];
                RawMesh otherMesh = otherMeshes[i];
                Debug.Assert(mesh.MaterialId == otherMesh.MaterialId);
                Debug.Assert(mesh.DlistId == otherMesh.DlistId);
            }
            IReadOnlyList<DisplayList> dlists = Read.DoOffsets<DisplayList>(bytes, header.DlistOffset, header.MeshCount);
            IReadOnlyList<DisplayList> otherDlists = Read.DoOffsets<DisplayList>(otherBytes, other.DlistOffset, other.MeshCount);
            for (int i = 0; i < dlists.Count; i++)
            {
                DisplayList dlist = dlists[i];
                DisplayList otherDlist = otherDlists[i];
                Debug.Assert(dlist.Offset == otherDlist.Offset);
                Debug.Assert(dlist.Size == otherDlist.Size);
                Debug.Assert(dlist.MinBounds.X.Value == otherDlist.MinBounds.X.Value);
                Debug.Assert(dlist.MinBounds.Y.Value == otherDlist.MinBounds.Y.Value);
                Debug.Assert(dlist.MinBounds.Z.Value == otherDlist.MinBounds.Z.Value);
                Debug.Assert(dlist.MaxBounds.X.Value == otherDlist.MaxBounds.X.Value);
                Debug.Assert(dlist.MaxBounds.Y.Value == otherDlist.MaxBounds.Y.Value);
                Debug.Assert(dlist.MaxBounds.Z.Value == otherDlist.MaxBounds.Z.Value);
                IReadOnlyList<byte> dlistData = Read.DoOffsets<byte>(bytes, dlist.Offset, dlist.Size);
                IReadOnlyList<byte> otherDlistData = Read.DoOffsets<byte>(otherBytes, otherDlist.Offset, otherDlist.Size);
                Debug.Assert(Enumerable.SequenceEqual(dlistData, otherDlistData));
            }
            Debug.Assert(Enumerable.SequenceEqual(bytes, otherBytes));
            Nop();
        }

        private static void CompareAnims(string name, byte[] bytes, byte[] otherBytes)
        {
            string temp = name;
            Debug.Assert(bytes.Length == otherBytes.Length);
            AnimationHeader header = Read.ReadStruct<AnimationHeader>(bytes);
            AnimationHeader other = Read.ReadStruct<AnimationHeader>(otherBytes);
            Debug.Assert(header.Count == other.Count);
            Debug.Assert(header.NodeGroupOffset == other.NodeGroupOffset);
            Debug.Assert(header.UnusedGroupOffset == other.UnusedGroupOffset);
            Debug.Assert(header.MaterialGroupOffset == other.MaterialGroupOffset);
            Debug.Assert(header.TexcoordGroupOffset == other.TexcoordGroupOffset);
            Debug.Assert(header.TextureGroupOffset == other.TextureGroupOffset);
            IReadOnlyList<uint> nodes = Read.DoOffsets<uint>(bytes, header.NodeGroupOffset, header.Count);
            IReadOnlyList<uint> otherNodes = Read.DoOffsets<uint>(otherBytes, other.NodeGroupOffset, other.Count);
            IReadOnlyList<uint> mats = Read.DoOffsets<uint>(bytes, header.MaterialGroupOffset, header.Count);
            IReadOnlyList<uint> otherMats = Read.DoOffsets<uint>(otherBytes, other.MaterialGroupOffset, other.Count);
            IReadOnlyList<uint> uvs = Read.DoOffsets<uint>(bytes, header.TexcoordGroupOffset, header.Count);
            IReadOnlyList<uint> otherUvs = Read.DoOffsets<uint>(otherBytes, other.TexcoordGroupOffset, other.Count);
            IReadOnlyList<uint> texes = Read.DoOffsets<uint>(bytes, header.TextureGroupOffset, header.Count);
            IReadOnlyList<uint> otherTexes = Read.DoOffsets<uint>(otherBytes, other.TextureGroupOffset, other.Count);
            for (int i = 0; i < header.Count; i++)
            {
                uint node = nodes[i];
                uint otherNode = otherNodes[i];
                Debug.Assert(node == otherNode);
                if (node != 0)
                {
                    RawNodeAnimationGroup nodeGroup = Read.DoOffset<RawNodeAnimationGroup>(bytes, node);
                    RawNodeAnimationGroup otherGroup = Read.DoOffset<RawNodeAnimationGroup>(otherBytes, otherNode);
                    Debug.Assert(nodeGroup.FrameCount == otherGroup.FrameCount);
                    Debug.Assert(nodeGroup.ScaleLutOffset == otherGroup.ScaleLutOffset);
                    Debug.Assert(nodeGroup.RotateLutOffset == otherGroup.RotateLutOffset);
                    Debug.Assert(nodeGroup.TranslateLutOffset == otherGroup.TranslateLutOffset);
                    Debug.Assert(nodeGroup.AnimationOffset == otherGroup.AnimationOffset);
                    int scaleCount = (int)(nodeGroup.RotateLutOffset - nodeGroup.ScaleLutOffset) / 4;
                    int rotCount = (int)(nodeGroup.TranslateLutOffset - nodeGroup.RotateLutOffset) / 2;
                    int transCount = (int)(nodeGroup.AnimationOffset - nodeGroup.TranslateLutOffset) / 4;
                    IReadOnlyList<uint> scales = Read.DoOffsets<uint>(bytes, nodeGroup.ScaleLutOffset, scaleCount);
                    IReadOnlyList<uint> otherScales = Read.DoOffsets<uint>(otherBytes, otherGroup.ScaleLutOffset, scaleCount);
                    Debug.Assert(Enumerable.SequenceEqual(scales, otherScales));
                    IReadOnlyList<ushort> rots = Read.DoOffsets<ushort>(bytes, nodeGroup.RotateLutOffset, rotCount);
                    IReadOnlyList<ushort> otherRots = Read.DoOffsets<ushort>(otherBytes, otherGroup.RotateLutOffset, rotCount);
                    Debug.Assert(Enumerable.SequenceEqual(rots, otherRots));
                    IReadOnlyList<uint> poses = Read.DoOffsets<uint>(bytes, nodeGroup.TranslateLutOffset, transCount);
                    IReadOnlyList<uint> otherPoses = Read.DoOffsets<uint>(otherBytes, otherGroup.TranslateLutOffset, transCount);
                    Debug.Assert(Enumerable.SequenceEqual(poses, otherPoses));
                    if (nodeGroup.AnimationOffset != node)
                    {
                        Debug.Assert(node > nodeGroup.AnimationOffset);
                        Debug.Assert((node - nodeGroup.AnimationOffset) % Sizes.NodeAnimation == 0);
                        int animCount = (int)(node - nodeGroup.AnimationOffset) / Sizes.NodeAnimation;
                        IReadOnlyList<NodeAnimation> anims = Read.DoOffsets<NodeAnimation>(bytes, nodeGroup.AnimationOffset, animCount);
                        IReadOnlyList<NodeAnimation> otherAnims = Read.DoOffsets<NodeAnimation>(otherBytes, otherGroup.AnimationOffset, animCount);
                        for (int j = 0; j < anims.Count; j++)
                        {
                            NodeAnimation anim = anims[j];
                            NodeAnimation otherAnim = otherAnims[j];
                            Debug.Assert(anim.ScaleBlendX == otherAnim.ScaleBlendX);
                            Debug.Assert(anim.ScaleBlendY == otherAnim.ScaleBlendY);
                            Debug.Assert(anim.ScaleBlendZ == otherAnim.ScaleBlendZ);
                            Debug.Assert(anim.Flags == otherAnim.Flags);
                            Debug.Assert(anim.ScaleLutLengthX == otherAnim.ScaleLutLengthX);
                            Debug.Assert(anim.ScaleLutLengthY == otherAnim.ScaleLutLengthY);
                            Debug.Assert(anim.ScaleLutLengthZ == otherAnim.ScaleLutLengthZ);
                            Debug.Assert(anim.ScaleLutIndexX == otherAnim.ScaleLutIndexX);
                            Debug.Assert(anim.ScaleLutIndexY == otherAnim.ScaleLutIndexY);
                            Debug.Assert(anim.ScaleLutIndexZ == otherAnim.ScaleLutIndexZ);
                            Debug.Assert(anim.RotateBlendX == otherAnim.RotateBlendX);
                            Debug.Assert(anim.RotateBlendY == otherAnim.RotateBlendY);
                            Debug.Assert(anim.RotateBlendZ == otherAnim.RotateBlendZ);
                            Debug.Assert(anim.RotateLutLengthX == otherAnim.RotateLutLengthX);
                            Debug.Assert(anim.RotateLutLengthY == otherAnim.RotateLutLengthY);
                            Debug.Assert(anim.RotateLutLengthZ == otherAnim.RotateLutLengthZ);
                            Debug.Assert(anim.RotateLutIndexX == otherAnim.RotateLutIndexX);
                            Debug.Assert(anim.RotateLutIndexY == otherAnim.RotateLutIndexY);
                            Debug.Assert(anim.RotateLutIndexZ == otherAnim.RotateLutIndexZ);
                            Debug.Assert(anim.TranslateBlendX == otherAnim.TranslateBlendX);
                            Debug.Assert(anim.TranslateBlendY == otherAnim.TranslateBlendY);
                            Debug.Assert(anim.TranslateBlendZ == otherAnim.TranslateBlendZ);
                            Debug.Assert(anim.TranslateLutLengthX == otherAnim.TranslateLutLengthX);
                            Debug.Assert(anim.TranslateLutLengthY == otherAnim.TranslateLutLengthY);
                            Debug.Assert(anim.TranslateLutLengthZ == otherAnim.TranslateLutLengthZ);
                            Debug.Assert(anim.TranslateLutIndexX == otherAnim.TranslateLutIndexX);
                            Debug.Assert(anim.TranslateLutIndexY == otherAnim.TranslateLutIndexY);
                            Debug.Assert(anim.TranslateLutIndexZ == otherAnim.TranslateLutIndexZ);
                        }
                    }
                }
                uint mat = mats[i];
                uint otherMat = otherMats[i];
                Debug.Assert(mat == otherMat);
                if (mat != 0)
                {
                    RawMaterialAnimationGroup matGroup = Read.DoOffset<RawMaterialAnimationGroup>(bytes, mat);
                    RawMaterialAnimationGroup otherGroup = Read.DoOffset<RawMaterialAnimationGroup>(otherBytes, otherMat);
                    Debug.Assert(matGroup.FrameCount == otherGroup.FrameCount);
                    Debug.Assert(matGroup.ColorLutOffset == otherGroup.ColorLutOffset);
                    Debug.Assert(matGroup.AnimationCount == otherGroup.AnimationCount);
                    Debug.Assert(matGroup.AnimationOffset == otherGroup.AnimationOffset);
                    Debug.Assert(matGroup.AnimationFrame == otherGroup.AnimationFrame);
                    Debug.Assert(matGroup.Unused12 == otherGroup.Unused12);
                    int colorCount = (int)(matGroup.AnimationOffset - matGroup.ColorLutOffset);
                    IReadOnlyList<byte> colors = Read.DoOffsets<byte>(bytes, matGroup.ColorLutOffset, colorCount);
                    IReadOnlyList<byte> otherColors = Read.DoOffsets<byte>(otherBytes, otherGroup.ColorLutOffset, colorCount);
                    Debug.Assert(Enumerable.SequenceEqual(colors, otherColors));
                    IReadOnlyList<MaterialAnimation> anims
                        = Read.DoOffsets<MaterialAnimation>(bytes, matGroup.AnimationOffset, matGroup.AnimationCount);
                    IReadOnlyList<MaterialAnimation> otherAnims
                        = Read.DoOffsets<MaterialAnimation>(otherBytes, otherGroup.AnimationOffset, otherGroup.AnimationCount);
                    for (int j = 0; j < anims.Count; j++)
                    {
                        MaterialAnimation anim = anims[j];
                        MaterialAnimation otherAnim = otherAnims[j];
                        Debug.Assert(Enumerable.SequenceEqual(anim.Name, otherAnim.Name));
                        Debug.Assert(anim.Unused40 == otherAnim.Unused40);
                        Debug.Assert(anim.DiffuseBlendR == otherAnim.DiffuseBlendR);
                        Debug.Assert(anim.DiffuseBlendG == otherAnim.DiffuseBlendG);
                        Debug.Assert(anim.DiffuseBlendB == otherAnim.DiffuseBlendB);
                        Debug.Assert(anim.Unused47 == otherAnim.Unused47);
                        Debug.Assert(anim.DiffuseLutLengthR == otherAnim.DiffuseLutLengthR);
                        Debug.Assert(anim.DiffuseLutLengthG == otherAnim.DiffuseLutLengthG);
                        Debug.Assert(anim.DiffuseLutLengthB == otherAnim.DiffuseLutLengthB);
                        Debug.Assert(anim.DiffuseLutIndexR == otherAnim.DiffuseLutIndexR);
                        Debug.Assert(anim.DiffuseLutIndexG == otherAnim.DiffuseLutIndexG);
                        Debug.Assert(anim.DiffuseLutIndexB == otherAnim.DiffuseLutIndexB);
                        Debug.Assert(anim.AmbientBlendR == otherAnim.AmbientBlendR);
                        Debug.Assert(anim.AmbientBlendG == otherAnim.AmbientBlendG);
                        Debug.Assert(anim.AmbientBlendB == otherAnim.AmbientBlendB);
                        Debug.Assert(anim.Unused57 == otherAnim.Unused57);
                        Debug.Assert(anim.AmbientLutLengthR == otherAnim.AmbientLutLengthR);
                        Debug.Assert(anim.AmbientLutLengthG == otherAnim.AmbientLutLengthG);
                        Debug.Assert(anim.AmbientLutLengthB == otherAnim.AmbientLutLengthB);
                        Debug.Assert(anim.AmbientLutIndexR == otherAnim.AmbientLutIndexR);
                        Debug.Assert(anim.AmbientLutIndexG == otherAnim.AmbientLutIndexG);
                        Debug.Assert(anim.AmbientLutIndexB == otherAnim.AmbientLutIndexB);
                        Debug.Assert(anim.SpecularBlendR == otherAnim.SpecularBlendR);
                        Debug.Assert(anim.SpecularBlendG == otherAnim.SpecularBlendG);
                        Debug.Assert(anim.SpecularBlendB == otherAnim.SpecularBlendB);
                        Debug.Assert(anim.Unused67 == otherAnim.Unused67);
                        Debug.Assert(anim.SpecularLutLengthR == otherAnim.SpecularLutLengthR);
                        Debug.Assert(anim.SpecularLutLengthG == otherAnim.SpecularLutLengthG);
                        Debug.Assert(anim.SpecularLutLengthB == otherAnim.SpecularLutLengthB);
                        Debug.Assert(anim.SpecularLutIndexR == otherAnim.SpecularLutIndexR);
                        Debug.Assert(anim.SpecularLutIndexG == otherAnim.SpecularLutIndexG);
                        Debug.Assert(anim.SpecularLutIndexB == otherAnim.SpecularLutIndexB);
                        Debug.Assert(anim.Unused74 == otherAnim.Unused74);
                        Debug.Assert(anim.Unused78 == otherAnim.Unused78);
                        Debug.Assert(anim.Unused7C == otherAnim.Unused7C);
                        Debug.Assert(anim.Unused80 == otherAnim.Unused80);
                        Debug.Assert(anim.AlphaBlend == otherAnim.AlphaBlend);
                        Debug.Assert(anim.Unused85 == otherAnim.Unused85);
                        Debug.Assert(anim.AlphaLutLength == otherAnim.AlphaLutLength);
                        Debug.Assert(anim.AlphaLutIndex == otherAnim.AlphaLutIndex);
                        Debug.Assert(anim.MaterialId == otherAnim.MaterialId);
                    }
                }
                uint uv = uvs[i];
                uint otherUv = otherUvs[i];
                Debug.Assert(uv == otherUv);
                if (uv != 0)
                {
                    RawTexcoordAnimationGroup uvGroup = Read.DoOffset<RawTexcoordAnimationGroup>(bytes, uv);
                    RawTexcoordAnimationGroup otherGroup = Read.DoOffset<RawTexcoordAnimationGroup>(otherBytes, otherUv);
                    Debug.Assert(uvGroup.FrameCount == otherGroup.FrameCount);
                    Debug.Assert(uvGroup.ScaleLutOffset == otherGroup.ScaleLutOffset);
                    Debug.Assert(uvGroup.RotateLutOffset == otherGroup.RotateLutOffset);
                    Debug.Assert(uvGroup.TranslateLutOffset == otherGroup.TranslateLutOffset);
                    Debug.Assert(uvGroup.AnimationCount == otherGroup.AnimationCount);
                    Debug.Assert(uvGroup.AnimationOffset == otherGroup.AnimationOffset);
                    Debug.Assert(uvGroup.AnimationFrame == otherGroup.AnimationFrame);
                    Debug.Assert(uvGroup.Unused1A == otherGroup.Unused1A);
                    int scaleCount = (int)(uvGroup.RotateLutOffset - uvGroup.ScaleLutOffset) / 4;
                    int rotCount = (int)(uvGroup.TranslateLutOffset - uvGroup.RotateLutOffset) / 2;
                    int transCount = (int)(uvGroup.AnimationOffset - uvGroup.TranslateLutOffset) / 4;
                    IReadOnlyList<uint> scales = Read.DoOffsets<uint>(bytes, uvGroup.ScaleLutOffset, scaleCount);
                    IReadOnlyList<uint> otherScales = Read.DoOffsets<uint>(otherBytes, otherGroup.ScaleLutOffset, scaleCount);
                    Debug.Assert(Enumerable.SequenceEqual(scales, otherScales));
                    IReadOnlyList<ushort> rots = Read.DoOffsets<ushort>(bytes, uvGroup.RotateLutOffset, rotCount);
                    IReadOnlyList<ushort> otherRots = Read.DoOffsets<ushort>(otherBytes, otherGroup.RotateLutOffset, rotCount);
                    Debug.Assert(Enumerable.SequenceEqual(rots, otherRots));
                    IReadOnlyList<uint> poses = Read.DoOffsets<uint>(bytes, uvGroup.TranslateLutOffset, transCount);
                    IReadOnlyList<uint> otherPoses = Read.DoOffsets<uint>(otherBytes, otherGroup.TranslateLutOffset, transCount);
                    Debug.Assert(Enumerable.SequenceEqual(poses, otherPoses));
                    IReadOnlyList<TexcoordAnimation> anims
                        = Read.DoOffsets<TexcoordAnimation>(bytes, uvGroup.AnimationOffset, uvGroup.AnimationCount);
                    IReadOnlyList<TexcoordAnimation> otherAnims
                        = Read.DoOffsets<TexcoordAnimation>(otherBytes, otherGroup.AnimationOffset, otherGroup.AnimationCount);
                    for (int j = 0; j < anims.Count; j++)
                    {
                        TexcoordAnimation anim = anims[j];
                        TexcoordAnimation otherAnim = otherAnims[j];
                        Debug.Assert(Enumerable.SequenceEqual(anim.Name, otherAnim.Name));
                        Debug.Assert(anim.ScaleBlendS == otherAnim.ScaleBlendS);
                        Debug.Assert(anim.ScaleBlendT == otherAnim.ScaleBlendT);
                        Debug.Assert(anim.ScaleLutLengthS == otherAnim.ScaleLutLengthS);
                        Debug.Assert(anim.ScaleLutLengthT == otherAnim.ScaleLutLengthT);
                        Debug.Assert(anim.ScaleLutIndexS == otherAnim.ScaleLutIndexS);
                        Debug.Assert(anim.ScaleLutIndexT == otherAnim.ScaleLutIndexT);
                        Debug.Assert(anim.RotateBlendZ == otherAnim.RotateBlendZ);
                        Debug.Assert(anim.Unused2B == otherAnim.Unused2B);
                        Debug.Assert(anim.RotateLutLengthZ == otherAnim.RotateLutLengthZ);
                        Debug.Assert(anim.RotateLutIndexZ == otherAnim.RotateLutIndexZ);
                        Debug.Assert(anim.TranslateBlendS == otherAnim.TranslateBlendS);
                        Debug.Assert(anim.TranslateBlendT == otherAnim.TranslateBlendT);
                        Debug.Assert(anim.TranslateLutLengthS == otherAnim.TranslateLutLengthS);
                        Debug.Assert(anim.TranslateLutLengthT == otherAnim.TranslateLutLengthT);
                        Debug.Assert(anim.TranslateLutIndexS == otherAnim.TranslateLutIndexS);
                        Debug.Assert(anim.TranslateLutIndexT == otherAnim.TranslateLutIndexT);
                    }
                }
                uint tex = texes[i];
                uint otherTex = otherTexes[i];
                Debug.Assert(tex == otherTex);
                if (tex != 0)
                {
                    RawTextureAnimationGroup texGroup = Read.DoOffset<RawTextureAnimationGroup>(bytes, tex);
                    RawTextureAnimationGroup otherGroup = Read.DoOffset<RawTextureAnimationGroup>(otherBytes, otherTex);
                    Debug.Assert(texGroup.FrameCount == otherGroup.FrameCount);
                    Debug.Assert(texGroup.FrameIndexCount == otherGroup.FrameIndexCount);
                    Debug.Assert(texGroup.TextureIdCount == otherGroup.TextureIdCount);
                    Debug.Assert(texGroup.PaletteIdCount == otherGroup.PaletteIdCount);
                    Debug.Assert(texGroup.AnimationCount == otherGroup.AnimationCount);
                    Debug.Assert(texGroup.UnusedA == otherGroup.UnusedA);
                    Debug.Assert(texGroup.FrameIndexOffset == otherGroup.FrameIndexOffset);
                    Debug.Assert(texGroup.TextureIdOffset == otherGroup.TextureIdOffset);
                    Debug.Assert(texGroup.PaletteIdOffset == otherGroup.PaletteIdOffset);
                    Debug.Assert(texGroup.AnimationOffset == otherGroup.AnimationOffset);
                    Debug.Assert(texGroup.AnimationFrame == otherGroup.AnimationFrame);
                    Debug.Assert(texGroup.Unused1C == otherGroup.Unused1C);
                    IReadOnlyList<ushort> frameIds = Read.DoOffsets<ushort>(bytes, texGroup.FrameIndexOffset, texGroup.FrameIndexCount);
                    IReadOnlyList<ushort> otherFrameIds = Read.DoOffsets<ushort>(otherBytes, otherGroup.FrameIndexOffset, otherGroup.FrameIndexCount);
                    Debug.Assert(Enumerable.SequenceEqual(frameIds, otherFrameIds));
                    IReadOnlyList<ushort> texIds = Read.DoOffsets<ushort>(bytes, texGroup.TextureIdOffset, texGroup.TextureIdCount);
                    IReadOnlyList<ushort> otherTexIds = Read.DoOffsets<ushort>(otherBytes, otherGroup.TextureIdOffset, otherGroup.TextureIdCount);
                    Debug.Assert(Enumerable.SequenceEqual(texIds, otherTexIds));
                    IReadOnlyList<ushort> palIds = Read.DoOffsets<ushort>(bytes, texGroup.PaletteIdOffset, texGroup.PaletteIdCount);
                    IReadOnlyList<ushort> otherPalIds = Read.DoOffsets<ushort>(otherBytes, otherGroup.PaletteIdOffset, otherGroup.PaletteIdCount);
                    Debug.Assert(Enumerable.SequenceEqual(palIds, otherPalIds));
                    IReadOnlyList<TextureAnimation> anims
                        = Read.DoOffsets<TextureAnimation>(bytes, texGroup.AnimationOffset, texGroup.AnimationCount);
                    IReadOnlyList<TextureAnimation> otherAnims
                        = Read.DoOffsets<TextureAnimation>(otherBytes, otherGroup.AnimationOffset, otherGroup.AnimationCount);
                    for (int j = 0; j < anims.Count; j++)
                    {
                        TextureAnimation anim = anims[j];
                        TextureAnimation otherAnim = otherAnims[j];
                        Debug.Assert(Enumerable.SequenceEqual(anim.Name, otherAnim.Name));
                        Debug.Assert(anim.Count == otherAnim.Count);
                        Debug.Assert(anim.StartIndex == otherAnim.StartIndex);
                        Debug.Assert(anim.MinimumPaletteId == otherAnim.MinimumPaletteId);
                        Debug.Assert(anim.MaterialId == otherAnim.MaterialId);
                        Debug.Assert(anim.MinimumTextureId == otherAnim.MinimumTextureId);
                    }
                }
            }
            Debug.Assert(Enumerable.SequenceEqual(bytes, otherBytes));
            Nop();
        }

        public enum RepackTexture
        {
            Inline,
            Separate,
            Shared
        }

        public enum ComputeBounds
        {
            None,
            Capped,
            Uncapped
        }

        public class RepackOptions
        {
            public RepackTexture Texture { get; set; }
            public bool IsRoom { get; set; }
            public ComputeBounds ComputeBounds { get; set; }
            public bool WriteFile { get; set; }
            public bool Compare { get; set; }
        }

        public static byte[] PackAnim(IReadOnlyList<NodeAnimationGroup?> nodeGroups, IReadOnlyList<MaterialAnimationGroup?> matGroups,
            IReadOnlyList<TexcoordAnimationGroup?> uvGroups, IReadOnlyList<TextureAnimationGroup?> texGroups, bool fhPad)
        {
            ushort padShort = fhPad ? (ushort)0xCCCC : (ushort)0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            Debug.Assert(nodeGroups.Count == matGroups.Count && matGroups.Count == uvGroups.Count && uvGroups.Count == texGroups.Count);
            int count = nodeGroups.Count;
            var nodeGroupOffsets = new List<int>();
            var matGroupOffsets = new List<int>();
            var uvGroupOffsets = new List<int>();
            var unusedGroupOffsets = new List<int>();
            var texGroupOffsets = new List<int>();
            // data, animation, groups
            stream.Position = Sizes.AnimationHeader;
            for (int i = 0; i < count; i++)
            {
                NodeAnimationGroup? nodeGroup = nodeGroups[i];
                nodeGroupOffsets.Add(nodeGroup == null ? 0 : WriteNodeGroup(nodeGroup, fhPad, writer));
                MaterialAnimationGroup? matGroup = matGroups[i];
                matGroupOffsets.Add(matGroup == null ? 0 : WriteMatGroup(matGroup, fhPad, writer));
                TextureAnimationGroup? texGroup = texGroups[i];
                texGroupOffsets.Add(texGroup == null ? 0 : WriteTexGroup(texGroup, fhPad, writer));
                TexcoordAnimationGroup? uvGroup = uvGroups[i];
                uvGroupOffsets.Add(uvGroup == null ? 0 : WriteUvGroup(uvGroup, fhPad, writer));
                unusedGroupOffsets.Add(0);
            }
            // offset lists
            int nodeGroupList = (int)stream.Position;
            foreach (int offset in nodeGroupOffsets)
            {
                writer.Write(offset);
            }
            int unusedGroupList = (int)stream.Position;
            foreach (int offset in unusedGroupOffsets)
            {
                writer.Write(offset);
            }
            int matGroupList = (int)stream.Position;
            foreach (int offset in matGroupOffsets)
            {
                writer.Write(offset);
            }
            int uvGroupList = (int)stream.Position;
            foreach (int offset in uvGroupOffsets)
            {
                writer.Write(offset);
            }
            int texGroupList = (int)stream.Position;
            foreach (int offset in texGroupOffsets)
            {
                writer.Write(offset);
            }
            // header
            stream.Position = 0;
            writer.Write(nodeGroupList);
            writer.Write(unusedGroupList);
            writer.Write(matGroupList);
            writer.Write(uvGroupList);
            writer.Write(texGroupList);
            writer.Write((ushort)count);
            writer.Write(padShort);
            Debug.Assert(stream.Position == Sizes.AnimationHeader);
            return stream.ToArray();
        }

        private static int WriteNodeGroup(NodeAnimationGroup group, bool fhPad, BinaryWriter writer)
        {
            ushort padShort = fhPad ? (ushort)0xCCCC : (ushort)0;
            // scale LUT
            int scaleOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Scales)
            {
                writer.WriteFloat(value);
            }
            // rotation LUT
            int rotOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Rotations)
            {
                writer.WriteAngle(value);
            }
            while (writer.BaseStream.Position % 4 != 0)
            {
                writer.Write(padShort);
            }
            // translation LUT
            int transOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Translations)
            {
                writer.WriteFloat(value);
            }
            // animations
            int animOffset = (int)writer.BaseStream.Position;
            foreach (NodeAnimation anim in group.Animations.Values)
            {
                writer.Write(anim.ScaleBlendX);
                writer.Write(anim.ScaleBlendY);
                writer.Write(anim.ScaleBlendZ);
                writer.Write(anim.Flags);
                writer.Write(anim.ScaleLutLengthX);
                writer.Write(anim.ScaleLutLengthY);
                writer.Write(anim.ScaleLutLengthZ);
                writer.Write(anim.ScaleLutIndexX);
                writer.Write(anim.ScaleLutIndexY);
                writer.Write(anim.ScaleLutIndexZ);
                writer.Write(anim.RotateBlendX);
                writer.Write(anim.RotateBlendY);
                writer.Write(anim.RotateBlendZ);
                writer.Write(anim.Padding13);
                writer.Write(anim.RotateLutLengthX);
                writer.Write(anim.RotateLutLengthY);
                writer.Write(anim.RotateLutLengthZ);
                writer.Write(anim.RotateLutIndexX);
                writer.Write(anim.RotateLutIndexY);
                writer.Write(anim.RotateLutIndexZ);
                writer.Write(anim.TranslateBlendX);
                writer.Write(anim.TranslateBlendY);
                writer.Write(anim.TranslateBlendZ);
                writer.Write(anim.Padding23);
                writer.Write(anim.TranslateLutLengthX);
                writer.Write(anim.TranslateLutLengthY);
                writer.Write(anim.TranslateLutLengthZ);
                writer.Write(anim.TranslateLutIndexX);
                writer.Write(anim.TranslateLutIndexY);
                writer.Write(anim.TranslateLutIndexZ);
            }
            // group
            int groupOffset = (int)writer.BaseStream.Position;
            writer.Write(group.FrameCount);
            writer.Write(scaleOffset);
            writer.Write(rotOffset);
            writer.Write(transOffset);
            writer.Write(animOffset);
            return groupOffset;
        }

        private static int WriteMatGroup(MaterialAnimationGroup group, bool fhPad, BinaryWriter writer)
        {
            byte padByte = fhPad ? (byte)0xCC : (byte)0;
            // color LUT
            int colorOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Colors)
            {
                writer.Write((byte)value);
            }
            while (writer.BaseStream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // animations
            int animOffset = (int)writer.BaseStream.Position;
            foreach (MaterialAnimation anim in group.Animations.Values)
            {
                for (int i = 0; i < anim.Name.Length; i++)
                {
                    writer.Write((byte)anim.Name[i]);
                }
                Debug.Assert(writer.BaseStream.Position % 4 == 0);
                writer.Write(anim.Unused40);
                writer.Write(anim.DiffuseBlendR);
                writer.Write(anim.DiffuseBlendG);
                writer.Write(anim.DiffuseBlendB);
                writer.Write(anim.Unused47);
                writer.Write(anim.DiffuseLutLengthR);
                writer.Write(anim.DiffuseLutLengthG);
                writer.Write(anim.DiffuseLutLengthB);
                writer.Write(anim.DiffuseLutIndexR);
                writer.Write(anim.DiffuseLutIndexG);
                writer.Write(anim.DiffuseLutIndexB);
                writer.Write(anim.AmbientBlendR);
                writer.Write(anim.AmbientBlendG);
                writer.Write(anim.AmbientBlendB);
                writer.Write(anim.Unused57);
                writer.Write(anim.AmbientLutLengthR);
                writer.Write(anim.AmbientLutLengthG);
                writer.Write(anim.AmbientLutLengthB);
                writer.Write(anim.AmbientLutIndexR);
                writer.Write(anim.AmbientLutIndexG);
                writer.Write(anim.AmbientLutIndexB);
                writer.Write(anim.SpecularBlendR);
                writer.Write(anim.SpecularBlendG);
                writer.Write(anim.SpecularBlendB);
                writer.Write(anim.Unused67);
                writer.Write(anim.SpecularLutLengthR);
                writer.Write(anim.SpecularLutLengthG);
                writer.Write(anim.SpecularLutLengthB);
                writer.Write(anim.SpecularLutIndexR);
                writer.Write(anim.SpecularLutIndexG);
                writer.Write(anim.SpecularLutIndexB);
                writer.Write(anim.Unused74);
                writer.Write(anim.Unused78);
                writer.Write(anim.Unused7C);
                writer.Write(anim.Unused80);
                writer.Write(anim.AlphaBlend);
                writer.Write(anim.Unused85);
                writer.Write(anim.AlphaLutLength);
                writer.Write(anim.AlphaLutIndex);
                writer.Write(anim.MaterialId);
            }
            // group
            int groupOffset = (int)writer.BaseStream.Position;
            writer.Write(group.FrameCount);
            writer.Write(colorOffset);
            writer.Write(group.Animations.Count);
            writer.Write(animOffset);
            writer.Write((ushort)group.CurrentFrame);
            writer.Write((ushort)group.UnusedFrame);
            return groupOffset;
        }

        private static int WriteUvGroup(TexcoordAnimationGroup group, bool fhPad, BinaryWriter writer)
        {
            ushort padShort = fhPad ? (ushort)0xCCCC : (ushort)0;
            // scale LUT
            int scaleOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Scales)
            {
                writer.WriteFloat(value);
            }
            // rotation LUT
            int rotOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Rotations)
            {
                writer.WriteAngle(value);
            }
            while (writer.BaseStream.Position % 4 != 0)
            {
                writer.Write(padShort);
            }
            // translation LUT
            int transOffset = (int)writer.BaseStream.Position;
            foreach (float value in group.Translations)
            {
                writer.WriteFloat(value);
            }
            // animations
            int animOffset = (int)writer.BaseStream.Position;
            foreach (TexcoordAnimation anim in group.Animations.Values)
            {
                for (int i = 0; i < anim.Name.Length; i++)
                {
                    writer.Write((byte)anim.Name[i]);
                }
                Debug.Assert(writer.BaseStream.Position % 4 == 0);
                writer.Write(anim.ScaleBlendS);
                writer.Write(anim.ScaleBlendT);
                writer.Write(anim.ScaleLutLengthS);
                writer.Write(anim.ScaleLutLengthT);
                writer.Write(anim.ScaleLutIndexS);
                writer.Write(anim.ScaleLutIndexT);
                writer.Write(anim.RotateBlendZ);
                writer.Write(anim.Unused2B);
                writer.Write(anim.RotateLutLengthZ);
                writer.Write(anim.RotateLutIndexZ);
                writer.Write(anim.TranslateBlendS);
                writer.Write(anim.TranslateBlendT);
                writer.Write(anim.TranslateLutLengthS);
                writer.Write(anim.TranslateLutLengthT);
                writer.Write(anim.TranslateLutIndexS);
                writer.Write(anim.TranslateLutIndexT);
                writer.Write(padShort);
            }
            // group
            int groupOffset = (int)writer.BaseStream.Position;
            writer.Write(group.FrameCount);
            writer.Write(scaleOffset);
            writer.Write(rotOffset);
            writer.Write(transOffset);
            writer.Write(group.Animations.Count);
            writer.Write(animOffset);
            writer.Write((ushort)group.CurrentFrame);
            writer.Write((ushort)group.UnusedFrame);
            return groupOffset;
        }

        private static int WriteTexGroup(TextureAnimationGroup group, bool fhPad, BinaryWriter writer)
        {
            ushort padShort = fhPad ? (ushort)0xCCCC : (ushort)0;
            // frame list
            int frameOffset = (int)writer.BaseStream.Position;
            foreach (ushort value in group.FrameIndices)
            {
                writer.Write(value);
            }
            // texid list
            int texOffset = (int)writer.BaseStream.Position;
            foreach (ushort value in group.TextureIds)
            {
                writer.Write(value);
            }
            // palid list
            int palOffset = (int)writer.BaseStream.Position;
            foreach (ushort value in group.PaletteIds)
            {
                writer.Write(value);
            }
            while (writer.BaseStream.Position % 4 != 0)
            {
                writer.Write(padShort);
            }
            // animations
            int animOffset = (int)writer.BaseStream.Position;
            foreach (TextureAnimation anim in group.Animations.Values)
            {
                for (int i = 0; i < anim.Name.Length; i++)
                {
                    writer.Write((byte)anim.Name[i]);
                }
                Debug.Assert(writer.BaseStream.Position % 4 == 0);
                writer.Write(anim.Count);
                writer.Write(anim.StartIndex);
                writer.Write(anim.MinimumPaletteId);
                writer.Write(anim.MaterialId);
                writer.Write(anim.MinimumTextureId);
                writer.Write(padShort);
            }
            // group
            int groupOffset = (int)writer.BaseStream.Position;
            writer.Write((ushort)group.FrameCount);
            writer.Write((ushort)group.FrameIndices.Count);
            writer.Write((ushort)group.TextureIds.Count);
            writer.Write((ushort)group.PaletteIds.Count);
            writer.Write((ushort)group.Animations.Count);
            writer.Write(group.UnusedA);
            writer.Write(frameOffset);
            writer.Write(texOffset);
            writer.Write(palOffset);
            writer.Write(animOffset);
            writer.Write((ushort)group.CurrentFrame);
            writer.Write((ushort)group.UnusedFrame);
            return groupOffset;
        }

        private static (Vector3i, Vector3i) CalculateBounds(IReadOnlyList<RenderInstruction> insts)
        {
            var verts = new List<Vector3i>();
            int vtxX = 0;
            int vtxY = 0;
            int vtxZ = 0;
            void Update()
            {
                verts.Add(new Vector3i(vtxX, vtxY, vtxZ));
            }
            foreach (RenderInstruction instruction in insts)
            {
                switch (instruction.Code)
                {
                case InstructionCode.VTX_16:
                    {
                        uint xy = instruction.Arguments[0];
                        int x = (int)((xy >> 0) & 0xFFFF);
                        if ((x & 0x8000) > 0)
                        {
                            x = (int)(x | 0xFFFF0000);
                        }
                        int y = (int)((xy >> 16) & 0xFFFF);
                        if ((y & 0x8000) > 0)
                        {
                            y = (int)(y | 0xFFFF0000);
                        }
                        int z = (int)(instruction.Arguments[1] & 0xFFFF);
                        if ((z & 0x8000) > 0)
                        {
                            z = (int)(z | 0xFFFF0000);
                        }
                        vtxX = x;
                        vtxY = y;
                        vtxZ = z;
                        Update();
                    }
                    break;
                case InstructionCode.VTX_10:
                    {
                        uint xyz = instruction.Arguments[0];
                        int x = (int)((xyz >> 0) & 0x3FF);
                        if ((x & 0x200) > 0)
                        {
                            x = (int)(x | 0xFFFFFC00);
                        }
                        int y = (int)((xyz >> 10) & 0x3FF);
                        if ((y & 0x200) > 0)
                        {
                            y = (int)(y | 0xFFFFFC00);
                        }
                        int z = (int)((xyz >> 20) & 0x3FF);
                        if ((z & 0x200) > 0)
                        {
                            z = (int)(z | 0xFFFFFC00);
                        }
                        vtxX = x << 6;
                        vtxY = y << 6;
                        vtxZ = z << 6;
                        Update();
                    }
                    break;
                case InstructionCode.VTX_XY:
                    {
                        uint xy = instruction.Arguments[0];
                        int x = (int)((xy >> 0) & 0xFFFF);
                        if ((x & 0x8000) > 0)
                        {
                            x = (int)(x | 0xFFFF0000);
                        }
                        int y = (int)((xy >> 16) & 0xFFFF);
                        if ((y & 0x8000) > 0)
                        {
                            y = (int)(y | 0xFFFF0000);
                        }
                        vtxX = x;
                        vtxY = y;
                        Update();
                    }
                    break;
                case InstructionCode.VTX_XZ:
                    {
                        uint xz = instruction.Arguments[0];
                        int x = (int)((xz >> 0) & 0xFFFF);
                        if ((x & 0x8000) > 0)
                        {
                            x = (int)(x | 0xFFFF0000);
                        }
                        int z = (int)((xz >> 16) & 0xFFFF);
                        if ((z & 0x8000) > 0)
                        {
                            z = (int)(z | 0xFFFF0000);
                        }
                        vtxX = x;
                        vtxZ = z;
                        Update();
                    }
                    break;
                case InstructionCode.VTX_YZ:
                    {
                        uint yz = instruction.Arguments[0];
                        int y = (int)((yz >> 0) & 0xFFFF);
                        if ((y & 0x8000) > 0)
                        {
                            y = (int)(y | 0xFFFF0000);
                        }
                        int z = (int)((yz >> 16) & 0xFFFF);
                        if ((z & 0x8000) > 0)
                        {
                            z = (int)(z | 0xFFFF0000);
                        }
                        vtxY = y;
                        vtxZ = z;
                        Update();
                    }
                    break;
                case InstructionCode.VTX_DIFF:
                    {
                        uint xyz = instruction.Arguments[0];
                        int x = (int)((xyz >> 0) & 0x3FF);
                        if ((x & 0x200) > 0)
                        {
                            x = (int)(x | 0xFFFFFC00);
                        }
                        int y = (int)((xyz >> 10) & 0x3FF);
                        if ((y & 0x200) > 0)
                        {
                            y = (int)(y | 0xFFFFFC00);
                        }
                        int z = (int)((xyz >> 20) & 0x3FF);
                        if ((z & 0x200) > 0)
                        {
                            z = (int)(z | 0xFFFFFC00);
                        }
                        vtxX += x;
                        vtxY += y;
                        vtxZ += z;
                        Update();
                    }
                    break;
                }
            }
            int minX = Int32.MaxValue;
            int maxX = Int32.MinValue;
            int minY = Int32.MaxValue;
            int maxY = Int32.MinValue;
            int minZ = Int32.MaxValue;
            int maxZ = Int32.MinValue;
            foreach (Vector3i vert in verts)
            {
                minX = Math.Min(minX, vert.X);
                maxX = Math.Max(maxX, vert.X);
                minY = Math.Min(minY, vert.Y);
                maxY = Math.Max(maxY, vert.Y);
                minZ = Math.Min(minZ, vert.Z);
                maxZ = Math.Max(maxZ, vert.Z);
            }
            return (new Vector3i(minX, minY, minZ), new Vector3i(maxX, maxY, maxZ));
        }

        public static (byte[], byte[]) PackModel(Model model)
        {
            int recolor = 0;
            var textureInfo = new List<TextureInfo>();
            for (int i = 0; i < model.Recolors[recolor].Textures.Count; i++)
            {
                Texture texture = model.Recolors[recolor].Textures[i];
                IReadOnlyList<TextureData> data = model.Recolors[recolor].TextureData[i];
                textureInfo.Add(ConvertData(texture, data));
            }
            var paletteInfo = new List<PaletteInfo>();
            foreach (IReadOnlyList<PaletteData> data in model.Recolors[recolor].PaletteData)
            {
                paletteInfo.Add(new PaletteInfo(data.Select(d => d.Data).ToList()));
            }
            var options = new RepackOptions()
            {
                Compare = false,
                ComputeBounds = ComputeBounds.None,
                IsRoom = false,
                Texture = RepackTexture.Inline,
                WriteFile = false
            };
            return PackModel((int)model.Scale.X, model.NodeMatrixIds, model.NodePosCounts, model.Materials,
                textureInfo, paletteInfo, model.Nodes, model.Meshes, model.RenderInstructionLists, model.DisplayLists, options);
        }

        public static (byte[], byte[]) PackModel(int scale, IReadOnlyList<int> nodeMtxIds, IReadOnlyList<int> nodePosScaleCounts,
            IReadOnlyList<Material> materials, IReadOnlyList<TextureInfo> textures, IReadOnlyList<PaletteInfo> palettes,
            IReadOnlyList<Node> nodes, IReadOnlyList<Mesh> meshes, IReadOnlyList<IReadOnlyList<RenderInstruction>> renders,
            IReadOnlyList<DisplayList> dlists, RepackOptions options)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            using MemoryStream texStream = options.Texture == RepackTexture.Inline ? stream : new MemoryStream();
            using BinaryWriter texWriter = options.Texture == RepackTexture.Inline ? writer : new BinaryWriter(texStream);
            int primitiveCount = 0;
            int vertexCount = 0;
            var dlistMin = new List<Vector3i>();
            var dlistMax = new List<Vector3i>();
            var nodeMin = new List<Vector3i>();
            var nodeMax = new List<Vector3i>();
            Debug.Assert(scale > 0);
            Debug.Assert(renders.Count == dlists.Count);
            foreach (IReadOnlyList<RenderInstruction> render in renders)
            {
                (int primitives, int vertices) = GetDlistCounts(render);
                primitiveCount += primitives;
                vertexCount += vertices;
            }
            // todo: support bounds calculation for models with weighted transforms
            if (nodeMtxIds.Count > 0)
            {
                options.ComputeBounds = ComputeBounds.None;
            }
            if (options.ComputeBounds == ComputeBounds.None)
            {
                dlistMin.AddRange(dlists.Select(d => d.MinBounds.ToIntVector()));
                dlistMax.AddRange(dlists.Select(d => d.MaxBounds.ToIntVector()));
                nodeMin.AddRange(nodes.Select(n => n.MinBounds.ToFixedVector()));
                nodeMax.AddRange(nodes.Select(n => n.MaxBounds.ToFixedVector()));
            }
            else
            {
                var allMin = new List<Vector3i>();
                var allMax = new List<Vector3i>();
                foreach (IReadOnlyList<RenderInstruction> insts in renders)
                {
                    (Vector3i min, Vector3i max) = CalculateBounds(insts);
                    allMin.Add(min);
                    allMax.Add(max);
                }
                foreach (Node node in nodes)
                {
                    IEnumerable<int> ids = node.MeshCount == 0 ? node.GetAllMeshIds(nodes, root: true) : node.GetMeshIds();
                    if (!ids.Any())
                    {
                        nodeMin.Add(new Vector3i(0, 0, 0));
                        nodeMax.Add(new Vector3i(0, 0, 0));
                    }
                    else
                    {
                        var min = new Vector3i(Int32.MaxValue, Int32.MaxValue, Int32.MaxValue);
                        var max = new Vector3i(Int32.MinValue, Int32.MinValue, Int32.MinValue);
                        foreach (int id in ids)
                        {
                            int dlistId = meshes[id].DlistId;
                            Vector3i meshMin = allMin[dlistId];
                            Vector3i meshMax = allMax[dlistId];
                            min.X = Math.Min(min.X, meshMin.X);
                            min.Y = Math.Min(min.Y, meshMin.Y);
                            min.Z = Math.Min(min.Z, meshMin.Z);
                            max.X = Math.Max(max.X, meshMax.X);
                            max.Y = Math.Max(max.Y, meshMax.Y);
                            max.Z = Math.Max(max.Z, meshMax.Z);
                        }
                        nodeMin.Add(min * scale);
                        nodeMax.Add(max * scale);
                    }
                }
                int clampMin = Int16.MinValue * scale;
                int clampMax = Int16.MaxValue * scale;
                for (int i = 0; i < allMin.Count; i++)
                {
                    Vector3i min = allMin[i];
                    Vector3i max = allMax[i];
                    if (options.ComputeBounds == ComputeBounds.Capped)
                    {
                        dlistMin.Add(new Vector3i(
                            Math.Clamp(min.X * scale, clampMin, clampMax),
                            Math.Clamp(min.Y * scale, clampMin, clampMax),
                            Math.Clamp(min.Z * scale, clampMin, clampMax)
                        ));
                        dlistMax.Add(new Vector3i(
                            Math.Clamp(max.X * scale, clampMin, clampMax),
                            Math.Clamp(max.Y * scale, clampMin, clampMax),
                            Math.Clamp(max.Z * scale, clampMin, clampMax)
                        ));
                    }
                    else
                    {
                        dlistMin.Add(min);
                        dlistMax.Add(max);
                    }
                }
            }
            // header is written last
            stream.Position = Sizes.Header;
            // node mtx IDs, node pos scale counts
            int nodeMtxIdOffset;
            int actualOffset = Sizes.Header;
            if (nodeMtxIds.Count == 0)
            {
                nodeMtxIdOffset = 0;
                // sometimes the header has no offset for the matrix IDs, but the values are actually there after the header
                // in that case, we can infer their existence and count from the pos scale count's offset
                if (nodePosScaleCounts.Count == 0)
                {
                    actualOffset += sizeof(uint);
                }
                else
                {
                    actualOffset += nodePosScaleCounts.Count * sizeof(uint);
                }
            }
            else
            {
                nodeMtxIdOffset = actualOffset;
                actualOffset += nodeMtxIds.Count * sizeof(uint);
            }
            int nodePosCountOffset;
            if (nodePosScaleCounts.Count == 0)
            {
                nodePosCountOffset = 0;
            }
            else
            {
                // in the situation described above, Read is returning the pos scale counts while ignoring the matrix IDs,
                // to avoid the matrix IDs messing with anything in the model transform code, so we can rely on the former here
                nodePosCountOffset = actualOffset;
            }
            // node matrix IDs
            if (nodeMtxIds.Count == 0)
            {
                if (options.IsRoom)
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].MeshCount > 0)
                        {
                            writer.Write(i);
                        }
                    }
                }
                else
                {
                    int padCount = nodePosScaleCounts.Count == 0 ? 1 : nodePosScaleCounts.Count;
                    for (int i = 0; i < padCount; i++)
                    {
                        // basically just a hack to differentiate between goreaLaser and arcWelder
                        writer.Write(nodes.Count <= 1 ? 0 : i + 1);
                    }
                }
            }
            else
            {
                foreach (int value in nodeMtxIds)
                {
                    writer.Write(value);
                }
            }
            // node pos counts
            foreach (int value in nodePosScaleCounts)
            {
                writer.Write(value);
            }
            // texture data
            var textureDataOffsets = new List<int>();
            foreach (TextureInfo texture in textures)
            {
                textureDataOffsets.Add((int)texStream.Position);
                foreach (byte data in texture.Data)
                {
                    texWriter.Write(data);
                }
            }
            // texture metadata
            int texturesOffset = textures.Count == 0 ? 0 : (int)stream.Position;
            for (int i = 0; i < textures.Count; i++)
            {
                WriteTextureMeta(textures[i], textureDataOffsets[i], writer);
            }
            // palette data
            var paletteDataOffsets = new List<int>();
            foreach (PaletteInfo palette in palettes)
            {
                paletteDataOffsets.Add((int)texStream.Position);
                foreach (ushort data in palette.Data)
                {
                    texWriter.Write(data);
                }
            }
            // palette metdata
            int paletteOffset = palettes.Count == 0 ? 0 : (int)stream.Position;
            for (int i = 0; i < palettes.Count; i++)
            {
                PaletteInfo palette = palettes[i];
                writer.Write(paletteDataOffsets[i]);
                writer.Write(palette.Data.Count * sizeof(ushort));
                writer.Write(padInt); // VramOffset
                writer.Write(padInt); // ObjectRef
            }
            // dlist data
            var dlistResults = new List<(int, int)>();
            foreach (IReadOnlyList<RenderInstruction> render in renders)
            {
                int offset = (int)stream.Position;
                int size = WriteRenderInstructions(render, writer);
                dlistResults.Add((offset, size));
            }
            // dlist metadata
            int dlistsOffset = (int)stream.Position;
            for (int i = 0; i < dlists.Count; i++)
            {
                DisplayList dlist = dlists[i];
                Vector3i minBounds = dlistMin[i];
                Vector3i maxBounds = dlistMax[i];
                (int off, int size) = dlistResults[i];
                writer.Write(off);
                writer.Write(size);
                writer.Write(minBounds.X);
                writer.Write(minBounds.Y);
                writer.Write(minBounds.Z);
                writer.Write(maxBounds.X);
                writer.Write(maxBounds.Y);
                writer.Write(maxBounds.Z);
            }
            // materials
            int matrixIdCount = 0;
            int materialsOffset = (int)stream.Position;
            foreach (Material material in materials)
            {
                int matrixId = GetTextureMatrixId(material, ref matrixIdCount);
                WriteMaterial(material, matrixId, writer);
            }
            // nodes
            int nodesOffset = (int)stream.Position;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                Vector3i minBounds = nodeMin[i];
                Vector3i maxBounds = nodeMax[i];
                WriteNode(node, minBounds, maxBounds, writer);
            }
            // meshes
            int meshesOffset = (int)stream.Position;
            foreach (Mesh mesh in meshes)
            {
                writer.Write((ushort)mesh.MaterialId);
                writer.Write((ushort)mesh.DlistId);
            }
            stream.Position = 0;
            // header
            int scaleFactor = (int)Math.Log2(scale);
            Debug.Assert(Math.Pow(2, scaleFactor) == scale);
            int scaleBase = 4096;
            int nodeAnimOffset = 0;
            int uvAnimOffset = 0;
            int matAnimOffset = 0;
            int texAnimOffset = 0;
            writer.Write(scaleFactor);
            writer.Write(scaleBase);
            writer.Write(primitiveCount);
            writer.Write(vertexCount);
            writer.Write(materialsOffset);
            writer.Write(dlistsOffset);
            writer.Write(nodesOffset);
            writer.Write((ushort)nodeMtxIds.Count);
            writer.Write(padByte); // Flags
            writer.Write(padByte);
            writer.Write(nodeMtxIdOffset);
            writer.Write(meshesOffset);
            writer.Write((ushort)textures.Count);
            writer.Write(padShort);
            writer.Write(texturesOffset);
            writer.Write((ushort)palettes.Count);
            writer.Write(padShort);
            writer.Write(paletteOffset);
            writer.Write(nodePosCountOffset);
            writer.Write(padInt); // NodePosScales
            writer.Write(padInt); // NodeInitialPosition
            writer.Write(padInt); // NodePosition
            writer.Write((ushort)materials.Count);
            writer.Write((ushort)nodes.Count);
            writer.Write(padInt); // TextureMatrixOffset
            writer.Write(nodeAnimOffset);
            writer.Write(uvAnimOffset);
            writer.Write(matAnimOffset);
            writer.Write(texAnimOffset);
            writer.Write((ushort)meshes.Count);
            writer.Write((ushort)matrixIdCount);
            Debug.Assert(stream.Position == Sizes.Header);
            return (stream.ToArray(), texStream == stream ? Array.Empty<byte>() : texStream.ToArray());
        }

        private static (int primitives, int vertices) GetDlistCounts(IReadOnlyList<RenderInstruction> dlist)
        {
            int primitiveCount = 0;
            int vertexCount = 0;
            int vertexType = -1;
            int currentVertexCount = 0;
            foreach (RenderInstruction instruction in dlist)
            {
                switch (instruction.Code)
                {
                case InstructionCode.BEGIN_VTXS:
                    Debug.Assert(vertexType == -1 && currentVertexCount == 0);
                    vertexType = (int)instruction.Arguments[0];
                    break;
                case InstructionCode.VTX_16:
                case InstructionCode.VTX_10:
                case InstructionCode.VTX_XY:
                case InstructionCode.VTX_XZ:
                case InstructionCode.VTX_YZ:
                case InstructionCode.VTX_DIFF:
                    vertexCount++;
                    currentVertexCount++;
                    break;
                case InstructionCode.END_VTXS:
                    if (vertexType == 0)
                    {
                        Debug.Assert(currentVertexCount >= 3 && currentVertexCount % 3 == 0);
                        primitiveCount += currentVertexCount / 3;
                    }
                    else if (vertexType == 1)
                    {
                        Debug.Assert(currentVertexCount >= 4 && currentVertexCount % 4 == 0);
                        primitiveCount += currentVertexCount / 4;
                    }
                    else if (vertexType == 2)
                    {
                        Debug.Assert(currentVertexCount >= 3);
                        primitiveCount += 1 + currentVertexCount - 3;
                    }
                    else if (vertexType == 3)
                    {
                        Debug.Assert(currentVertexCount >= 4 && currentVertexCount % 2 == 0);
                        primitiveCount += 1 + (currentVertexCount - 4) / 2;
                    }
                    vertexType = -1;
                    currentVertexCount = 0;
                    break;
                }
            }
            return (primitiveCount, vertexCount);
        }

        public static TextureInfo ConvertData(Texture texture, IReadOnlyList<TextureData> data)
        {
            var imageData = new List<byte>();

            if (texture.Format == TextureFormat.DirectRgb)
            {
                foreach (TextureData entry in data)
                {
                    // alpha bit is already present in the ushort value
                    imageData.Add((byte)(entry.Data & 0xFF));
                    imageData.Add((byte)(entry.Data >> 8));
                }
            }
            else if (texture.Format == TextureFormat.PaletteA3I5)
            {
                foreach (TextureData entry in data)
                {
                    byte value = (byte)entry.Data;
                    byte alpha = (byte)Math.Round(entry.Alpha * 7f / 255f);
                    value |= (byte)(alpha << 5);
                    imageData.Add(value);
                }
            }
            else if (texture.Format == TextureFormat.PaletteA5I3)
            {
                foreach (TextureData entry in data)
                {
                    byte value = (byte)entry.Data;
                    byte alpha = (byte)Math.Round(entry.Alpha * 31f / 255f);
                    value |= (byte)(alpha << 3);
                    imageData.Add(value);
                }
            }
            else if (texture.Format == TextureFormat.Palette2Bit)
            {
                for (int i = 0; i < data.Count; i += 4)
                {
                    byte value = 0;
                    for (int j = 0; j < 4 && i + j < data.Count; j++)
                    {
                        uint index = data[i + j].Data;
                        value |= (byte)(index << (2 * j));
                    }
                    imageData.Add(value);
                }
            }
            else if (texture.Format == TextureFormat.Palette4Bit)
            {
                for (int i = 0; i < data.Count; i += 2)
                {
                    byte value = 0;
                    for (int j = 0; j < 2 && i + j < data.Count; j++)
                    {
                        uint index = data[i + j].Data;
                        value |= (byte)(index << (4 * j));
                    }
                    imageData.Add(value);
                }
            }
            else if (texture.Format == TextureFormat.Palette8Bit)
            {
                foreach (TextureData entry in data)
                {
                    imageData.Add((byte)entry.Data);
                }
            }
            return new TextureInfo(texture.Format, texture.Opaque != 0, texture.Height, texture.Width, imageData);
        }

        public static (TextureInfo, PaletteInfo) ConvertImage(ImageInfo image)
        {
            bool opaque = true;
            var imageData = new List<byte>();
            var paletteData = new List<ushort>();
            Debug.Assert(image.Pixels.Count == image.Width * image.Height);
            if (image.Format == TextureFormat.DirectRgb)
            {
                foreach (ColorRgba pixel in image.Pixels)
                {
                    ushort value = pixel.Alpha == 0 ? (ushort)0 : (ushort)0x8000;
                    ushort red = (ushort)Math.Round(pixel.Red * 31f / 255f);
                    ushort green = (ushort)Math.Round(pixel.Green * 31f / 255f);
                    ushort blue = (ushort)Math.Round(pixel.Blue * 31f / 255f);
                    value |= red;
                    value |= (ushort)(green << 5);
                    value |= (ushort)(blue << 10);
                    imageData.Add((byte)(value & 0xFF));
                    imageData.Add((byte)(value >> 8));
                    if (pixel.Alpha != 255)
                    {
                        opaque = false;
                    }
                }
            }
            else
            {
                var colors = new Dictionary<(byte, byte, byte), int>();
                foreach (ColorRgba pixel in image.Pixels)
                {
                    if (image.PaletteOpaque || pixel.Alpha > 0)
                    {
                        (byte, byte, byte) color = (pixel.Red, pixel.Green, pixel.Blue);
                        if (!colors.ContainsKey(color))
                        {
                            colors.Add(color, colors.Count);
                        }
                    }
                }
                if (image.Format == TextureFormat.PaletteA3I5)
                {
                    Debug.Assert(colors.Count <= 32);
                    foreach (ColorRgba pixel in image.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        byte alpha = (byte)Math.Round(pixel.Alpha * 7f / 255f);
                        value |= (byte)(alpha << 5);
                        imageData.Add(value);
                        if (alpha < 7)
                        {
                            opaque = false;
                        }
                    }
                }
                else if (image.Format == TextureFormat.PaletteA5I3)
                {
                    Debug.Assert(colors.Count <= 8);
                    foreach (ColorRgba pixel in image.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        byte alpha = (byte)Math.Round(pixel.Alpha * 31f / 255f);
                        value |= (byte)(alpha << 3);
                        imageData.Add(value);
                        if (alpha < 31)
                        {
                            opaque = false;
                        }
                    }
                }
                else if (image.Format == TextureFormat.Palette2Bit)
                {
                    if (image.PaletteOpaque || image.Pixels.All(i => i.Alpha > 0))
                    {
                        Debug.Assert(colors.Count <= 4);
                    }
                    else
                    {
                        Debug.Assert(colors.Count <= 3);
                    }
                    for (int i = 0; i < image.Pixels.Count; i += 4)
                    {
                        byte value = 0;
                        for (int j = 0; j < 4 && i + j < image.Pixels.Count; j++)
                        {
                            ColorRgba pixel = image.Pixels[i + j];
                            byte index;
                            if (image.PaletteOpaque || pixel.Alpha > 0)
                            {
                                index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                                if (!image.PaletteOpaque)
                                {
                                    index++;
                                }
                            }
                            else
                            {
                                index = 0;
                            }
                            value |= (byte)(index << (2 * j));
                        }
                        imageData.Add(value);
                    }
                    opaque = image.PaletteOpaque;
                }
                else if (image.Format == TextureFormat.Palette4Bit)
                {
                    if (image.PaletteOpaque || image.Pixels.All(i => i.Alpha > 0))
                    {
                        Debug.Assert(colors.Count <= 16);
                    }
                    else
                    {
                        Debug.Assert(colors.Count <= 15);
                    }
                    for (int i = 0; i < image.Pixels.Count; i += 2)
                    {
                        byte value = 0;
                        for (int j = 0; j < 2 && i + j < image.Pixels.Count; j++)
                        {
                            ColorRgba pixel = image.Pixels[i + j];
                            byte index;
                            if (image.PaletteOpaque || pixel.Alpha > 0)
                            {
                                index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                                if (!image.PaletteOpaque)
                                {
                                    index++;
                                }
                            }
                            else
                            {
                                index = 0;
                            }
                            value |= (byte)(index << (4 * j));
                        }
                        imageData.Add(value);
                    }
                    opaque = image.PaletteOpaque;
                }
                else if (image.Format == TextureFormat.Palette8Bit)
                {
                    if (image.PaletteOpaque || image.Pixels.All(i => i.Alpha > 0))
                    {
                        Debug.Assert(colors.Count <= 256);
                    }
                    else
                    {
                        Debug.Assert(colors.Count <= 255);
                    }
                    foreach (ColorRgba pixel in image.Pixels)
                    {
                        if (image.PaletteOpaque || pixel.Alpha > 0)
                        {
                            byte index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                            if (!image.PaletteOpaque)
                            {
                                index++;
                            }
                            imageData.Add(index);
                        }
                        else
                        {
                            imageData.Add(0);
                        }
                    }
                    opaque = image.PaletteOpaque;
                }
                foreach (KeyValuePair<(byte Red, byte Green, byte Blue), int> kvp in colors.OrderBy(c => c.Value))
                {
                    ushort value = 0;
                    ushort red = (ushort)Math.Round(kvp.Key.Red * 31f / 255f);
                    ushort green = (ushort)Math.Round(kvp.Key.Green * 31f / 255f);
                    ushort blue = (ushort)Math.Round(kvp.Key.Blue * 31f / 255f);
                    value |= red;
                    value |= (ushort)(green << 5);
                    value |= (ushort)(blue << 10);
                    paletteData.Add(value);
                }
            }
            return (new TextureInfo(image.Format, opaque, image.Height, image.Width, imageData), new PaletteInfo(paletteData));
        }

        private static void WriteTextureMeta(TextureInfo texture, int offset, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            writer.Write((byte)texture.Format);
            writer.Write(padByte);
            writer.Write(texture.Width);
            writer.Write(texture.Height);
            writer.Write(padShort);
            writer.Write(offset);
            writer.Write(texture.Data.Count);
            writer.Write(padInt); // UnusedOffset
            writer.Write(padInt); // UnusedCount
            writer.Write(padInt); // VramOffset
            writer.WriteInt(texture.Opaque);
            writer.Write(padInt); // SkipVram
            writer.Write(padByte); // PackedSize
            writer.Write(padByte); // NativeTextureFormat
            writer.Write(padShort); // ObjectRef
        }

        private static int WriteRenderInstructions(IReadOnlyList<RenderInstruction> list, BinaryWriter writer)
        {
            int bytesWritten = 0;
            var arguments = new List<uint>();
            Debug.Assert(list.Count % 4 == 0);
            for (int i = 0; i < list.Count; i += 4)
            {
                arguments.Clear();
                uint packedCommands = 0;
                for (int j = 0; j < 4; j++)
                {
                    RenderInstruction inst = list[i + j];
                    uint code = (((uint)inst.Code) - 0x400) >> 2;
                    packedCommands |= code << (8 * j);
                    arguments.AddRange(inst.Arguments);
                }
                writer.Write(packedCommands);
                bytesWritten += sizeof(uint);
                foreach (uint argument in arguments)
                {
                    writer.Write(argument);
                    bytesWritten += sizeof(uint);
                }
            }
            return bytesWritten;
        }

        private static int GetTextureMatrixId(Material material, ref int indexCount)
        {
            int scaleS = Fixed.ToInt(material.ScaleS);
            int scaleT = Fixed.ToInt(material.ScaleT);
            ushort rotZ = (ushort)Math.Round(material.RotateZ / MathF.PI / 2f * 65536f);
            int transS = Fixed.ToInt(material.TranslateS);
            int transT = Fixed.ToInt(material.TranslateT);
            // materials with no texgen mode and default transform values get a matrix ID of 0 and don't contribute to the matrix count.
            // if the values are non-default, a matrix index is assigned even though the values won't be used due to the lack of texgen mode.
            // also, if the texgen mode is set, then even materials with default transform values will have a matrix index assigned, with no sharing.
            if (scaleS == 4096 && scaleT == 4096 && rotZ == 0 && transS == 0 && transT == 0 && material.TexgenMode == TexgenMode.None)
            {
                return -1;
            }
            return indexCount++;
        }

        private static void WriteMaterial(Material material, int matrixId, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            writer.WriteString(material.Name, length: 64);
            writer.Write(material.Lighting);
            writer.Write((byte)material.Culling);
            writer.Write(material.Alpha);
            writer.Write(material.Wireframe);
            writer.Write((short)material.PaletteId);
            writer.Write((short)material.TextureId);
            writer.Write((byte)material.XRepeat);
            writer.Write((byte)material.YRepeat);
            writer.Write(material.Diffuse.Red);
            writer.Write(material.Diffuse.Green);
            writer.Write(material.Diffuse.Blue);
            writer.Write(material.Ambient.Red);
            writer.Write(material.Ambient.Green);
            writer.Write(material.Ambient.Blue);
            writer.Write(material.Specular.Red);
            writer.Write(material.Specular.Green);
            writer.Write(material.Specular.Blue);
            writer.Write(padByte);
            writer.Write((uint)material.PolygonMode);
            writer.Write((byte)material.RenderMode);
            writer.Write((byte)material.AnimationFlags);
            writer.Write(padShort);
            writer.Write((uint)material.TexgenMode);
            writer.Write(padShort); // TexcoordAnimationId
            writer.Write(padShort);
            writer.Write(matrixId == -1 ? 0 : matrixId);
            writer.WriteFloat(material.ScaleS);
            writer.WriteFloat(material.ScaleT);
            writer.WriteAngle(material.RotateZ);
            writer.Write(padShort);
            writer.WriteFloat(material.TranslateS);
            writer.WriteFloat(material.TranslateT);
            writer.Write(padShort); // MaterialAnimationId
            writer.Write(padShort); // TextureAnimationId
            writer.Write(padByte); // PackedRepeatMode
            writer.Write(padByte);
            writer.Write(padShort);
        }

        private static void WriteNode(Node node, Vector3i minBounds, Vector3i maxBounds, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            writer.WriteString(node.Name, length: 64);
            writer.Write((short)node.ParentIndex);
            writer.Write((short)node.ChildIndex);
            writer.Write((short)node.NextIndex);
            writer.Write(padShort);
            writer.WriteInt(node.Enabled);
            writer.Write((short)node.MeshCount);
            writer.Write((short)node.MeshId);
            writer.WriteVector3(node.Scale);
            writer.WriteAngles(node.Angle);
            writer.Write(padShort);
            writer.WriteVector3(node.Position);
            writer.WriteFloat(node.BoundingRadius);
            writer.Write(minBounds.X);
            writer.Write(minBounds.Y);
            writer.Write(minBounds.Z);
            writer.Write(maxBounds.X);
            writer.Write(maxBounds.Y);
            writer.Write(maxBounds.Z);
            writer.Write((byte)node.BillboardMode);
            writer.Write(padByte);
            writer.Write(padShort);
            for (int i = 0; i < 12; i++)
            {
                writer.Write(padInt); // transform MtxFx43
            }
            writer.Write(padInt); // BeforeTransform
            writer.Write(padInt); // AfterTransform
            writer.Write(padInt); // UnusedC8
            writer.Write(padInt); // UnusedCC
            writer.Write(padInt); // UnusedD0
            writer.Write(padInt); // UnusedD4
            writer.Write(padInt); // UnusedD8
            writer.Write(padInt); // UnusedDC
            writer.Write(padInt); // UnusedE0
            writer.Write(padInt); // UnusedE4
            writer.Write(padInt); // UnusedE8
            writer.Write(padInt); // UnusedEC
        }

        private static void Nop()
        {
        }

        public class TextureInfo
        {
            public TextureFormat Format { get; }
            public bool Opaque { get; }
            public ushort Height { get; }
            public ushort Width { get; }
            public IReadOnlyList<byte> Data { get; }

            public TextureInfo(TextureFormat format, bool opaque, ushort height, ushort width,
                IReadOnlyList<byte> data)
            {
                Format = format;
                Opaque = opaque;
                Height = height;
                Width = width;
                Data = data;
            }
        }

        public class PaletteInfo
        {
            public IReadOnlyList<ushort> Data { get; set; }

            public PaletteInfo(IReadOnlyList<ushort> data)
            {
                Data = data;
            }
        }

        public class ImageInfo
        {
            public TextureFormat Format { get; }
            public ushort Height { get; }
            public ushort Width { get; }
            public IReadOnlyList<ColorRgba> Pixels { get; }
            public bool PaletteOpaque { get; }

            public ImageInfo(TextureFormat format, ushort height, ushort width, IReadOnlyList<ColorRgba> pixels, bool paletteOpaque)
            {
                Format = format;
                Height = height;
                Width = width;
                Pixels = pixels;
                if (format == TextureFormat.Palette2Bit || format == TextureFormat.Palette4Bit || format == TextureFormat.Palette8Bit)
                {
                    PaletteOpaque = paletteOpaque;
                }
            }
        }

        private class TexMtxMap
        {
            private readonly Dictionary<(ushort, ushort, int, int, ushort, int, int), int> _dict
                = new Dictionary<(ushort, ushort, int, int, ushort, int, int), int>();

            public int Count => _dict.Count;

            public bool TryGetValue(ushort width, ushort height, int scaleS, int scaleT, ushort rotZ, int transS, int transT, out int index)
            {
                return _dict.TryGetValue((width, height, scaleS, scaleT, rotZ, transS, transT), out index);
            }

            public void Add(ushort width, ushort height, int scaleS, int scaleT, ushort rotZ, int transS, int transT, int index)
            {
                _dict.Add((width, height, scaleS, scaleT, rotZ, transS, transT), index);
            }
        }

        public static void WriteString(this BinaryWriter writer, string value, int length)
        {
            Debug.Assert(value.Length <= length);
            int i = 0;
            for (; i < value.Length; i++)
            {
                writer.Write((byte)value[i]);
            }
            for (; i < length; i++)
            {
                writer.Write('\0');
            }
        }

        public static void WriteFloat(this BinaryWriter writer, float value)
        {
            writer.Write(Fixed.ToInt(value));
        }

        public static void WriteVector3(this BinaryWriter writer, Vector3 vector)
        {
            writer.WriteFloat(vector.X);
            writer.WriteFloat(vector.Y);
            writer.WriteFloat(vector.Z);
        }

        public static void WriteVector4(this BinaryWriter writer, Vector4 vector)
        {
            writer.WriteFloat(vector.X);
            writer.WriteFloat(vector.Y);
            writer.WriteFloat(vector.Z);
            writer.WriteFloat(vector.W);
        }

        public static void WriteColorRgb(this BinaryWriter writer, ColorRgb color)
        {
            writer.Write(color.Red);
            writer.Write(color.Green);
            writer.Write(color.Blue);
        }

        public static void WriteAngle(this BinaryWriter writer, float angle)
        {
            writer.Write((ushort)Math.Round(angle / MathF.PI / 2f * 65536f));
        }

        public static void WriteAngles(this BinaryWriter writer, Vector3 angles)
        {
            writer.WriteAngle(angles.X);
            writer.WriteAngle(angles.Y);
            writer.WriteAngle(angles.Z);
        }

        public static void WriteByte(this BinaryWriter writer, bool value)
        {
            byte yes = 1;
            byte no = 0;
            writer.Write(value ? yes : no);
        }

        public static void WriteInt(this BinaryWriter writer, bool value)
        {
            uint yes = 1;
            uint no = 0;
            writer.Write(value ? yes : no);
        }
    }
}
