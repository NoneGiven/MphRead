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

        public static void PackModel(float scaleBase, int scaleFactor, IReadOnlyList<IReadOnlyList<RenderInstruction>> dlists,
            IReadOnlyList<int> nodeMtxIds, IReadOnlyList<int> nodePosScaleCounts, IReadOnlyList<Material> materials,
            IReadOnlyList<TextureInfo> textures, IReadOnlyList<Node> nodes, IReadOnlyList<Mesh> meshes)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int primitiveCount = 0;
            int vertexCount = 0;
            foreach (IReadOnlyList<RenderInstruction> dlist in dlists)
            {
                (int primitives, int vertices) = GetDlistCounts(dlist);
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
            int textureDataOffset = offset;
            stream.Position = textureDataOffset;
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

            // palette data
            int paletteDataOffset = offset;
            // texture data
            int paletteOffset = offset;
            // dlist data
            int dlistDataOffset = offset;
            // dlist metadata
            int dlistsOffset = offset;
            // materials
            int materialsOffset = offset;
            // nodes
            int nodesOffset = offset;
            // meshes
            int meshesOffset = offset;

            stream.Position = 0;
            // write header
            byte padByte = 0;
            ushort padShort = 0;
            int flags = 0;
            int nodeScaleOffset = 0;
            int nodeInitPosOffset = 0;
            int nodePosOffset = 0;
            int texMtxOffset = 0;
            int texMtxCount = 0; // sktodo
            // sktodo: animation (file)
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
            writer.Write(texMtxCount);
            Debug.Assert(stream.Position == Sizes.Header);
            // write node matrix IDs
            foreach (int value in nodeMtxIds)
            {
                writer.Write(value);
            }
            // write node pos counts
            foreach (int value in nodePosScaleCounts)
            {
                writer.Write(value);
            }
            // write texture data
            // write texture metadata
            // write palette data
            // write texture data
            // write dlist data
            // write dlist metadata
            // write materials
            // write nodes
            // write meshes
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
    }
}
