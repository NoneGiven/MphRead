using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MphRead.Utility
{
    public static class Repack
    {
        public static void TestRepack()
        {
            foreach (ModelMetadata meta in Metadata.ModelMetadata.Values.Concat(Metadata.FirstHuntModels.Values))
            {
                int i = 0;
                foreach (RecolorMetadata recolor in meta.Recolors)
                {
                    // not real recolors -- data is from separate models which will get tested themselves
                    if ((meta.Name == "samus_hi_yellow" || meta.Name == "samus_low_yellow" || meta.Name == "morphBall") && i != 0)
                    {
                        break;
                    }
                    Model model = Read.GetModelInstance(meta.Name, meta.FirstHunt).Model;
                    string modelPath = meta.ModelPath;
                    // also not real recolors -- data is from separate models which won't get tested
                    if (meta.Name == "arcWelder1")
                    {
                        modelPath = modelPath.Replace("arcWelder1", $"arcWelder{i + 1}");
                    }
                    var options = new RepackOptions()
                    {
                        IsRoom = false,
                        Texture = RepackTexture.Inline
                    };
                    if (meta.ModelPath == recolor.TexturePath && recolor.TexturePath != recolor.PalettePath)
                    {
                        options.Texture = recolor.TexturePath.Contains("Share") || recolor.PalettePath.Contains("AlimbicPalettes")
                            ? RepackTexture.Shared
                            : RepackTexture.SeparatePal;
                    }
                    else if (meta.ModelPath != recolor.TexturePath || meta.ModelPath != recolor.PalettePath)
                    {
                        options.Texture = recolor.TexturePath.Contains("Share") || meta.ModelPath != recolor.PalettePath
                            ? RepackTexture.Shared
                            : RepackTexture.Separate;
                    }
                    TestRepack(model, recolor: i++, modelPath, meta.FirstHunt, options);
                }
            }
            foreach (RoomMetadata meta in Metadata.RoomMetadata.Values)
            {
                var options = new RepackOptions()
                {
                    IsRoom = true,
                    Texture = meta.TexturePath == null || meta.ModelPath == meta.TexturePath
                        ? RepackTexture.Inline
                        : RepackTexture.Separate
                };
                Model model = Read.GetRoomModelInstance(meta.Name).Model;
                TestRepack(model, recolor: 0, meta.ModelPath, meta.FirstHunt || meta.Hybrid, options);
            }
        }

        public static void TestRepack(string name, int recolor = 0, bool firstHunt = false)
        {
            var options = new RepackOptions()
            {
                IsRoom = false,
                WriteFile = true
            };
            ModelMetadata meta = firstHunt ? Metadata.FirstHuntModels[name] : Metadata.ModelMetadata[name];
            Model model = Read.GetModelInstance(meta.Name, meta.FirstHunt).Model;
            TestRepack(model, recolor, meta.ModelPath, meta.FirstHunt, options);
        }

        private static void TestRepack(Model model, int recolor, string modelPath, bool firstHunt, RepackOptions options)
        {
            // sktodo: share
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
            byte[] bytes = PackModel(model.Header.ScaleBase.FloatValue, model.Header.ScaleFactor, model.NodeMatrixIds, model.NodePosCounts,
                model.Materials, textureInfo, paletteInfo, model.Nodes, model.Meshes, model.RenderInstructionLists, model.DisplayLists, options);
            byte[] fileBytes = File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, modelPath));
            ComparePacks(model.Name, bytes, fileBytes, options);
            if (options.WriteFile)
            {
                File.WriteAllBytes(Path.Combine(Paths.Export, "_pack", $"out_{model.Name}_{model.Recolors[recolor].Name}.bin"), bytes);
            }
            Nop();
        }

        private static void ComparePacks(string name, byte[] bytes, byte[] otherBytes, RepackOptions options)
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
                // sktodo
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
                // sktodo
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

        public enum RepackTexture
        {
            Inline,
            Separate,
            SeparatePal,
            Shared
        }

        public class RepackOptions
        {
            public RepackTexture Texture { get; set; }
            public bool IsRoom { get; set; }
            public bool WriteFile { get; set; }
        }

        public static byte[] PackModel(float scaleBase, uint scaleFactor, IReadOnlyList<int> nodeMtxIds, IReadOnlyList<int> nodePosScaleCounts,
            IReadOnlyList<Material> materials, IReadOnlyList<TextureInfo> textures, IReadOnlyList<PaletteInfo> palettes, IReadOnlyList<Node> nodes,
            IReadOnlyList<Mesh> meshes, IReadOnlyList<IReadOnlyList<RenderInstruction>> renders, IReadOnlyList<DisplayList> dlists, RepackOptions options)
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
            Debug.Assert(renders.Count == dlists.Count);
            foreach (IReadOnlyList<RenderInstruction> render in renders)
            {
                (int primitives, int vertices) = GetDlistCounts(render);
                primitiveCount += primitives;
                vertexCount += vertices;
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
            // sktodo: handle tex/pal share
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
                (int off, int size) = dlistResults[i];
                // todo: (attmept to) calculate dlist bounds instead of relying on existing values
                writer.Write(off);
                writer.Write(size);
                writer.Write(dlist.MinBounds.X.Value);
                writer.Write(dlist.MinBounds.Y.Value);
                writer.Write(dlist.MinBounds.Z.Value);
                writer.Write(dlist.MaxBounds.X.Value);
                writer.Write(dlist.MaxBounds.Y.Value);
                writer.Write(dlist.MaxBounds.Z.Value);
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
            foreach (Node node in nodes)
            {
                WriteNode(node, writer);
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
            // sktodo: handle animation (file)
            int nodeAnimOffset = 0;
            int uvAnimOffset = 0;
            int matAnimOffset = 0;
            int texAnimOffset = 0;
            writer.Write(scaleFactor);
            writer.Write(Fixed.ToInt(scaleBase));
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
            return stream.ToArray();
        }

        private static void WriteString(string value, int length, BinaryWriter writer)
        {
            Debug.Assert(value.Length <= length);
            int i = 0;
            for (; i < value.Length; i++)
            {
                writer.Write(value[i]);
            }
            for (; i < length; i++)
            {
                writer.Write('\0');
            }
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
                // todo: if PaletteOpaque is true, index 0 is used for transparency -- so no actual color value can be accessed at that index
                // --> in that case, we should probably 
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
            int opacity = texture.Opaque ? 1 : 0;
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
            writer.Write(opacity);
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
            WriteString(material.Name, length: 64, writer);
            writer.Write(material.Lighting);
            writer.Write((byte)material.Culling);
            writer.Write(material.Alpha);
            writer.Write(material.Wireframe);
            writer.Write((ushort)material.PaletteId);
            writer.Write((ushort)material.TextureId);
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
            writer.Write(Fixed.ToInt(material.ScaleS));
            writer.Write(Fixed.ToInt(material.ScaleT));
            writer.Write((ushort)Math.Round(material.RotateZ / MathF.PI / 2f * 65536f));
            writer.Write(padShort);
            writer.Write(Fixed.ToInt(material.TranslateS));
            writer.Write(Fixed.ToInt(material.TranslateT));
            writer.Write(padShort); // MaterialAnimationId
            writer.Write(padShort); // TextureAnimationId
            writer.Write(padByte); // PackedRepeatMode
            writer.Write(padByte);
            writer.Write(padShort);
        }

        private static void WriteNode(Node node, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            WriteString(node.Name, length: 64, writer);
            writer.Write((ushort)node.ParentIndex);
            writer.Write((ushort)node.ChildIndex);
            writer.Write((ushort)node.NextIndex);
            writer.Write(padShort);
            writer.Write(node.Enabled ? 1 : 0);
            writer.Write((ushort)node.MeshCount);
            writer.Write((ushort)node.MeshId);
            writer.Write(Fixed.ToInt(node.Scale.X));
            writer.Write(Fixed.ToInt(node.Scale.Y));
            writer.Write(Fixed.ToInt(node.Scale.Z));
            writer.Write((ushort)Math.Round(node.Angle.X / MathF.PI / 2f * 65536f));
            writer.Write((ushort)Math.Round(node.Angle.Y / MathF.PI / 2f * 65536f));
            writer.Write((ushort)Math.Round(node.Angle.Z / MathF.PI / 2f * 65536f));
            writer.Write(padShort);
            writer.Write(Fixed.ToInt(node.Position.X));
            writer.Write(Fixed.ToInt(node.Position.Y));
            writer.Write(Fixed.ToInt(node.Position.Z));
            writer.Write(Fixed.ToInt(node.BoundingRadius));
            // todo: (attmept to) calculate node bounds instead of relying on existing values
            writer.Write(Fixed.ToInt(node.MinBounds.X));
            writer.Write(Fixed.ToInt(node.MinBounds.Y));
            writer.Write(Fixed.ToInt(node.MinBounds.Z));
            writer.Write(Fixed.ToInt(node.MaxBounds.X));
            writer.Write(Fixed.ToInt(node.MaxBounds.Y));
            writer.Write(Fixed.ToInt(node.MaxBounds.Z));
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
    }
}
