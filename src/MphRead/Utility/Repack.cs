using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MphRead.Utility
{
    public static class Repack
    {
        public class TextureInfo
        {
            public TextureFormat Format { get; set; }
            public ushort Height { get; set; }
            public ushort Width { get; set; }
            public IReadOnlyList<ColorRgba> Pixels { get; set; }

            public TextureInfo(TextureFormat format, ushort height, ushort width, IReadOnlyList<ColorRgba> pixels)
            {
                Format = format;
                Height = height;
                Width = width;
                Pixels = pixels;
            }
        }

        public static void PackModel(float scaleBase, int scaleFactor, IReadOnlyList<int> nodeMtxIds, IReadOnlyList<int> nodePosScaleCounts,
            IReadOnlyList<Material> materials, IReadOnlyList<TextureInfo> textures, IReadOnlyList<Node> nodes, IReadOnlyList<Mesh> meshes,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renders, IReadOnlyList<DisplayList> dlists)
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
            var palettes = new List<List<ushort>>();
            var textureResults = new List<(int Offset, int Size, bool Opaque)>();
            foreach (TextureInfo texture in textures)
            {
                (int size, bool opaque) = WriteTextureData(texture, palettes, writer);
                textureResults.Add((offset, size, opaque));
                offset += size;
            }
            // texture metadata
            int texturesOffset = offset;
            for (int i = 0; i < textures.Count; i++)
            {
                (int off, int size, bool opaque) = textureResults[i];
                WriteTextureMeta(textures[i], off, size, opaque, writer);
                offset += Sizes.Texture;
            }
            Debug.Assert(stream.Position == offset);
            // palette data
            var paletteResults = new List<(int, int)>();
            foreach (List<ushort> palette in palettes)
            {
                int size = 0;
                foreach (ushort value in palette)
                {
                    writer.Write(value);
                    size += sizeof(ushort);
                }
                paletteResults.Add((offset, size));
                offset += size;
            }
            Debug.Assert(stream.Position == offset);
            // palette metdata
            int paletteOffset = offset;
            foreach ((int off, int size) in paletteResults)
            {
                writer.Write(off);
                writer.Write(size);
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

        private static (int, bool) WriteTextureData(TextureInfo texture, List<List<ushort>> palettes, BinaryWriter writer)
        {
            int bytesWritten = 0;
            bool opaque = true;
            Debug.Assert(texture.Pixels.Count == texture.Width * texture.Height);
            if (texture.Format == TextureFormat.DirectRgb)
            {
                foreach (ColorRgba pixel in texture.Pixels)
                {
                    ushort value = pixel.Alpha == 0 ? (ushort)0 : (ushort)0x8000;
                    ushort red = (ushort)(pixel.Red * 31 / 255);
                    ushort green = (ushort)(pixel.Green * 31 / 255);
                    ushort blue = (ushort)(pixel.Blue * 31 / 255);
                    value |= red;
                    value |= (ushort)(green << 5);
                    value |= (ushort)(blue << 10);
                    writer.Write(value);
                    bytesWritten += 2;
                    if (pixel.Alpha == 0)
                    {
                        opaque = false;
                    }
                }
            }
            else
            {
                var colors = new Dictionary<(byte, byte, byte), int>();
                foreach (ColorRgba pixel in texture.Pixels)
                {
                    (byte, byte, byte) color = (pixel.Red, pixel.Green, pixel.Blue);
                    if (!colors.ContainsKey(color))
                    {
                        colors.Add(color, colors.Count);
                    }
                }
                if (texture.Format == TextureFormat.PaletteA3I5)
                {
                    Debug.Assert(colors.Count <= 32);
                    foreach (ColorRgba pixel in texture.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        byte alpha = (byte)(pixel.Alpha * 7 / 255);
                        value |= (byte)(alpha << 5);
                        writer.Write(value);
                        bytesWritten++;
                        if (alpha < 7)
                        {
                            opaque = false;
                        }
                    }
                }
                else if (texture.Format == TextureFormat.PaletteA5I3)
                {
                    Debug.Assert(colors.Count <= 8);
                    foreach (ColorRgba pixel in texture.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        byte alpha = (byte)(pixel.Alpha * 31 / 255);
                        value |= (byte)(alpha << 3);
                        writer.Write(value);
                        bytesWritten++;
                        if (alpha < 31)
                        {
                            opaque = false;
                        }
                    }
                }
                else if (texture.Format == TextureFormat.Palette2Bit)
                {
                    Debug.Assert(colors.Count <= 4);
                    for (int i = 0; i < texture.Pixels.Count; i += 4)
                    {
                        byte value = 0;
                        for (int j = 0; j < 4 && i + j < texture.Pixels.Count; j++)
                        {
                            ColorRgba pixel = texture.Pixels[i + j];
                            byte index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                            value |= (byte)(index << (2 * j));
                        }
                        writer.Write(value);
                        bytesWritten++;
                    }
                }
                else if (texture.Format == TextureFormat.Palette4Bit)
                {
                    Debug.Assert(colors.Count <= 16);
                    for (int i = 0; i < texture.Pixels.Count; i += 2)
                    {
                        byte value = 0;
                        for (int j = 0; j < 2 && i + j < texture.Pixels.Count; j++)
                        {
                            ColorRgba pixel = texture.Pixels[i + j];
                            byte index = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                            value |= (byte)(index << (4 * j));
                        }
                        writer.Write(value);
                        bytesWritten++;
                    }
                }
                else if (texture.Format == TextureFormat.Palette8Bit)
                {
                    Debug.Assert(colors.Count <= 256);
                    foreach (ColorRgba pixel in texture.Pixels)
                    {
                        byte value = (byte)colors[(pixel.Red, pixel.Green, pixel.Blue)];
                        writer.Write(value);
                        bytesWritten++;
                    }
                }
                var palette = new List<ushort>();
                foreach (KeyValuePair<(byte Red, byte Green, byte Blue), int> kvp in colors.OrderBy(c => c.Value))
                {
                    ushort value = 0;
                    ushort red = (ushort)(kvp.Key.Red * 31 / 255);
                    ushort green = (ushort)(kvp.Key.Green * 31 / 255);
                    ushort blue = (ushort)(kvp.Key.Blue * 31 / 255);
                    value |= red;
                    value |= (ushort)(green << 5);
                    value |= (ushort)(blue << 10);
                    palette.Add(value);
                }
                palettes.Add(palette);
            }
            return (bytesWritten, opaque);
        }

        private static void WriteTextureMeta(TextureInfo texture, int offset, int size, bool opaque, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            int opacity = opaque ? 1 : 0;
            writer.Write((byte)texture.Format);
            writer.Write(padByte);
            writer.Write(texture.Width);
            writer.Write(texture.Height);
            writer.Write(padShort);
            writer.Write(offset);
            writer.Write(size);
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
    }
}
