using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace MphRead.Utility
{
    public static class Repack
    {
        public static void TestRepack()
        {
            // todo: handle recolors
            Model model = Read.GetModelInstance("Crate01").Model;
            // todo: this approach for textures/palettes doesn't handle ordering or unused data
            var textureInfo = new List<TextureInfo>();
            for (int i = 0; i < model.Recolors[0].Textures.Count; i++)
            {
                Texture texture = model.Recolors[0].Textures[i];
                IReadOnlyList<TextureData> data = model.Recolors[0].TextureData[i];
                textureInfo.Add(ConvertData(texture, data));
            }
            var paletteInfo = new List<PaletteInfo>(); // sktodo
            foreach (IReadOnlyList<PaletteData> data in model.Recolors[0].PaletteData)
            {
                paletteInfo.Add(new PaletteInfo(data.Select(d => d.Data).ToList()));
            }
            byte[] bytes = PackModel(model.Header.ScaleBase.FloatValue, model.Header.ScaleFactor, model.NodeMatrixIds, model.NodePosCounts,
                model.Materials, textureInfo, paletteInfo, model.Nodes, model.Meshes, model.RenderInstructionLists, model.DisplayLists);
        }

        public static byte[] PackModel(float scaleBase, uint scaleFactor, IReadOnlyList<int> nodeMtxIds, IReadOnlyList<int> nodePosScaleCounts,
            IReadOnlyList<Material> materials, IReadOnlyList<TextureInfo> textures, IReadOnlyList<PaletteInfo> palettes, IReadOnlyList<Node> nodes,
            IReadOnlyList<Mesh> meshes, IReadOnlyList<IReadOnlyList<RenderInstruction>> renders, IReadOnlyList<DisplayList> dlists)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int primitiveCount = 0;
            int vertexCount = 0;
            Debug.Assert(renders.Count == dlists.Count);
            foreach (IReadOnlyList<RenderInstruction> render in renders)
            {
                (int primitives, int vertices) = GetDlistCounts(render);
                primitiveCount += primitives;
                vertexCount += vertexCount;
            }
            // header
            int offset = 0;
            offset += Sizes.Header;
            // node mtx IDs
            int nodeMtxIdOffset = offset;
            offset += nodeMtxIds.Count * sizeof(int);
            // node pos scale counts
            int nodePosCountOffset = offset;
            offset += nodePosScaleCounts.Count * sizeof(int);
            // texture data
            stream.Position = offset;
            var textureDataOffsets = new List<int>();
            foreach (TextureInfo texture in textures)
            {
                textureDataOffsets.Add(offset);
                foreach (byte data in texture.Data)
                {
                    writer.Write(data);
                    offset++;
                }
            }
            Debug.Assert(stream.Position == offset);
            // texture metadata
            int texturesOffset = offset;
            for (int i = 0; i < textures.Count; i++)
            {
                WriteTextureMeta(textures[i], textureDataOffsets[i], writer);
                offset += Sizes.Texture;
            }
            Debug.Assert(stream.Position == offset);
            // palette data
            var paletteDataOffsets = new List<int>();
            foreach (PaletteInfo palette in palettes)
            {
                paletteDataOffsets.Add(offset);
                foreach (ushort data in palette.Data)
                {
                    writer.Write(data);
                    offset += sizeof(ushort);
                }
            }
            Debug.Assert(stream.Position == offset);
            // palette metdata
            int paletteOffset = offset;
            for (int i = 0; i < palettes.Count; i++)
            {
                PaletteInfo palette = palettes[i];
                writer.Write(paletteDataOffsets[i]);
                writer.Write(palette.Data.Count * sizeof(ushort));
                writer.Write(padInt); // VramOffset
                writer.Write(padInt); // ObjectRef
                offset += Sizes.Palette;
            }
            Debug.Assert(stream.Position == offset);
            // dlist data
            var dlistResults = new List<(int, int)>();
            foreach (IReadOnlyList<RenderInstruction> render in renders)
            {
                int size = WriteRenderInstructions(render, writer);
                dlistResults.Add((offset, size));
                offset += size;
            }
            Debug.Assert(stream.Position == offset);
            // dlist metadata
            int dlistsOffset = offset;
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
                offset += Sizes.Dlist;
            }
            Debug.Assert(stream.Position == offset);
            // materials
            var matrixIds = new Dictionary<(int, int, ushort, int, int), int>();
            int materialsOffset = offset;
            foreach (Material material in materials)
            {
                int matrixId = GetTextureMatrixId(material, matrixIds);
                WriteMaterial(material, matrixId, writer);
                offset += Sizes.Material;
            }
            Debug.Assert(stream.Position == offset);
            // nodes
            int nodesOffset = offset;
            foreach (Node node in nodes)
            {
                WriteNode(node, writer);
                offset += Sizes.Node;
            }
            Debug.Assert(stream.Position == offset);
            // meshes
            int meshesOffset = offset;
            foreach (Mesh mesh in meshes)
            {
                writer.Write((ushort)mesh.MaterialId);
                writer.Write((ushort)mesh.DlistId);
                offset += Sizes.Mesh;
            }
            Debug.Assert(stream.Position == offset);

            stream.Position = 0;
            // header
            int flags = 0;
            int nodeScaleOffset = 0;
            int nodeInitPosOffset = 0;
            int nodePosOffset = 0;
            int texMtxOffset = 0;
            // sktodo: handle animation (file)
            // sktodo: handle separate texture file
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
            writer.Write(nodeMtxIds.Count);
            writer.Write(flags);
            writer.Write(padByte);
            writer.Write(nodeMtxIdOffset);
            writer.Write(meshesOffset);
            writer.Write(textures.Count);
            writer.Write(padShort);
            writer.Write(texturesOffset);
            writer.Write(palettes.Count);
            writer.Write(padShort);
            writer.Write(paletteOffset);
            writer.Write(nodePosCountOffset);
            writer.Write(nodeScaleOffset);
            writer.Write(nodeInitPosOffset);
            writer.Write(nodePosOffset);
            writer.Write(materials.Count);
            writer.Write(nodes.Count);
            writer.Write(texMtxOffset);
            writer.Write(nodeAnimOffset);
            writer.Write(uvAnimOffset);
            writer.Write(matAnimOffset);
            writer.Write(texAnimOffset);
            writer.Write(meshes.Count);
            writer.Write(matrixIds.Count);
            Debug.Assert(stream.Position == Sizes.Header);
            // node matrix IDs
            foreach (int value in nodeMtxIds)
            {
                writer.Write(value);
            }
            // node pos counts
            foreach (int value in nodePosScaleCounts)
            {
                writer.Write(value);
            }
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
            bool opaque = data.All(d => d.Alpha > 0);
            var imageData = new List<byte>();
            foreach (TextureData entry in data)
            {
                uint value = entry.Data;
                if (texture.Format == TextureFormat.DirectRgb)
                {
                    imageData.Add((byte)(value & 0xFF));
                    imageData.Add((byte)(value >> 8));
                }
                else
                {
                    imageData.Add((byte)value);
                }
            }
            return new TextureInfo(texture.Format, opaque, texture.Height, texture.Width, imageData);
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
                    ushort red = (ushort)(pixel.Red * 31 / 255);
                    ushort green = (ushort)(pixel.Green * 31 / 255);
                    ushort blue = (ushort)(pixel.Blue * 31 / 255);
                    value |= red;
                    value |= (ushort)(green << 5);
                    value |= (ushort)(blue << 10);
                    imageData.Add((byte)(value & 0xFF));
                    imageData.Add((byte)(value >> 8));
                    if (pixel.Alpha == 0)
                    {
                        opaque = false;
                    }
                }
            }
            else
            {
                // todo: confirm palette color ordering
                var colors = new Dictionary<(byte, byte, byte), int>();
                foreach (ColorRgba pixel in image.Pixels)
                {
                    (byte, byte, byte) color = (pixel.Red, pixel.Green, pixel.Blue);
                    if (!colors.ContainsKey(color))
                    {
                        colors.Add(color, colors.Count);
                    }
                }
                if (image.Format == TextureFormat.PaletteA3I5)
                {
                    Debug.Assert(colors.Count <= 32);
                    foreach (ColorRgba pixel in image.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        byte alpha = (byte)(pixel.Alpha * 7 / 255);
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
                        byte alpha = (byte)(pixel.Alpha * 31 / 255);
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
                    Debug.Assert(colors.Count <= 4);
                    for (int i = 0; i < image.Pixels.Count; i += 4)
                    {
                        byte value = 0;
                        for (int j = 0; j < 4 && i + j < image.Pixels.Count; j++)
                        {
                            ColorRgba pixel = image.Pixels[i + j];
                            byte index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                            value |= (byte)(index << (2 * j));
                        }
                        imageData.Add(value);
                    }
                }
                else if (image.Format == TextureFormat.Palette4Bit)
                {
                    Debug.Assert(colors.Count <= 16);
                    for (int i = 0; i < image.Pixels.Count; i += 2)
                    {
                        byte value = 0;
                        for (int j = 0; j < 2 && i + j < image.Pixels.Count; j++)
                        {
                            ColorRgba pixel = image.Pixels[i + j];
                            byte index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                            value |= (byte)(index << (4 * j));
                        }
                        imageData.Add(value);
                    }
                }
                else if (image.Format == TextureFormat.Palette8Bit)
                {
                    Debug.Assert(colors.Count <= 256);
                    foreach (ColorRgba pixel in image.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        imageData.Add(value);
                    }
                }
                foreach (KeyValuePair<(byte Red, byte Green, byte Blue), int> kvp in colors.OrderBy(c => c.Value))
                {
                    ushort value = 0;
                    ushort red = (ushort)(kvp.Key.Red * 31 / 255);
                    ushort green = (ushort)(kvp.Key.Green * 31 / 255);
                    ushort blue = (ushort)(kvp.Key.Blue * 31 / 255);
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

        private static int GetTextureMatrixId(Material material, Dictionary<(int, int, ushort, int, int), int> ids)
        {
            int scaleS = Fixed.ToInt(material.ScaleS);
            int scaleT = Fixed.ToInt(material.ScaleT);
            ushort rotZ = (ushort)(material.RotateZ / MathF.PI / 2f * 65536f);
            int translateS = Fixed.ToInt(material.TranslateS);
            int translateT = Fixed.ToInt(material.TranslateT);
            if (scaleS  == 4096 && scaleT == 4096 && rotZ == 0 && translateS == 0 && translateT == 0)
            {
                return -1;
            }
            if (ids.TryGetValue((scaleS, scaleT, rotZ, translateS, translateT), out int index))
            {
                return index;
            }
            index = ids.Count;
            ids.Add((scaleS, scaleT, rotZ, translateS, translateT), index);
            return index;
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
            writer.Write(padShort);
            writer.Write(matrixId == -1 ? 0 : matrixId);
            writer.Write(Fixed.ToInt(material.ScaleS));
            writer.Write(Fixed.ToInt(material.ScaleT));
            writer.Write((ushort)(material.RotateZ / MathF.PI / 2f * 65536f));
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
            writer.Write((ushort)(node.Angle.X / MathF.PI / 2f * 65536f));
            writer.Write((ushort)(node.Angle.Y / MathF.PI / 2f * 65536f));
            writer.Write((ushort)(node.Angle.Z / MathF.PI / 2f * 65536f));
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

        public class TextureInfo
        {
            public TextureFormat Format { get; set; }
            public bool Opaque { get; set; }
            public ushort Height { get; set; }
            public ushort Width { get; set; }
            public IReadOnlyList<byte> Data { get; set; }

            public TextureInfo(TextureFormat format,  bool opaque, ushort height, ushort width,
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
            public TextureFormat Format { get; set; }
            public ushort Height { get; set; }
            public ushort Width { get; set; }
            public IReadOnlyList<ColorRgba> Pixels { get; set; }

            public ImageInfo(TextureFormat format, ushort height, ushort width, IReadOnlyList<ColorRgba> pixels)
            {
                Format = format;
                Height = height;
                Width = width;
                Pixels = pixels;
            }
        }
    }
}
