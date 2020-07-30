using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using MphRead.Archive;
using MphRead.Export;

namespace MphRead
{
    public static class Read
    {
        // NOTE: When _Texture file exists, the main _Model file header will list a non-zero number of textures/palettes,
        // but the texture/palette offset will be 0 (because they're located at the start of the _Texture file).
        // However, when recolor files are used (e.g. _pal01 or flagbase_ctf_mdl -> flagbase_ctf_green_img), the number
        // of textures/palettes will be zero as well. To get the real information, the _Model file for the recolor must
        // be used in addition to the main header. And after doing that, you might then still be dealing with a _Texture file.

        public static Model GetModelByName(string name, int defaultRecolor = 0, bool firstHunt = false)
        {
            ModelMetadata? entityMeta;
            if (firstHunt)
            {
                entityMeta = Metadata.GetFirstHuntEntityByName(name);
            }
            else
            {
                entityMeta = Metadata.GetEntityByName(name);
            }
            if (entityMeta == null)
            {
                throw new ProgramException("No entity with this name is known. Please provide metadata for a custom entity.");
            }
            return GetModel(entityMeta, defaultRecolor);
        }

        public static Model GetModelByPath(string path, bool externalTexture = false)
        {
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", path, externalTexture ? path.Replace("_Model", "_Tex") : path)
            };
            return GetModel("model", path, null, recolors, defaultRecolor: 0, useLightSources: false);
        }

        public static Model GetRoomByName(string name)
        {
            (RoomMetadata? roomMeta, _) = Metadata.GetRoomByName(name);
            if (roomMeta == null)
            {
                throw new ProgramException("No room with this name is known. Please provide metadata for a custom room.");
            }
            return GetRoom(roomMeta);
        }

        public static Model GetRoomById(int id)
        {
            RoomMetadata? roomMeta = Metadata.GetRoomById(id);
            if (roomMeta == null)
            {
                throw new ProgramException("No room with this ID is known.");
            }
            return GetRoom(roomMeta);
        }

        private static Model GetRoom(RoomMetadata meta)
        {
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", meta.ModelPath, meta.TexturePath ?? meta.ModelPath)
            };
            Model room = GetModel(meta.Name, meta.ModelPath, meta.AnimationPath, recolors, defaultRecolor: 0, useLightSources: false);
            room.Type = ModelType.Room;
            return room;
        }

        private static Model GetModel(ModelMetadata meta, int defaultRecolor)
        {
            Model model = GetModel(meta.Name, meta.ModelPath, meta.AnimationPath, meta.Recolors, defaultRecolor, meta.UseLightSources);
            return model;
        }

        public static Model GetModelDirect(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", path)
            };
            return GetModel(name, path, null, recolors, defaultRecolor: 0, useLightSources: false);
        }

        public static Header GetHeader(string path)
        {
            path = Path.Combine(Paths.FileSystem, path);
            ReadOnlySpan<byte> bytes = ReadBytes(path);
            return ReadStruct<Header>(bytes[0..Sizes.Header]);
        }

        private static Model GetModel(string name, string modelPath, string? animationPath,
            IReadOnlyList<RecolorMetadata> recolorMeta, int defaultRecolor, bool useLightSources)
        {
            if (defaultRecolor < 0 || defaultRecolor > recolorMeta.Count - 1)
            {
                throw new ProgramException("The specified recolor index is invalid for this entity.");
            }
            string path = Path.Combine(Paths.FileSystem, modelPath);
            ReadOnlySpan<byte> initialBytes = ReadBytes(path);
            Header header = ReadStruct<Header>(initialBytes[0..Sizes.Header]);
            IReadOnlyList<RawNode> nodes = DoOffsets<RawNode>(initialBytes, header.NodeOffset, header.NodeCount);
            IReadOnlyList<RawMesh> meshes = DoOffsets<RawMesh>(initialBytes, header.MeshOffset, header.MeshCount);
            IReadOnlyList<DisplayList> dlists = DoOffsets<DisplayList>(initialBytes, header.DlistOffset, header.MeshCount);
            var instructions = new List<IReadOnlyList<RenderInstruction>>();
            foreach (DisplayList dlist in dlists)
            {
                instructions.Add(DoRenderInstructions(initialBytes, dlist));
            }
            IReadOnlyList<RawMaterial> materials = DoOffsets<RawMaterial>(initialBytes, header.MaterialOffset, header.MaterialCount);
            IReadOnlyList<Matrix44Fx> textureMatrices = DoOffsets<Matrix44Fx>(initialBytes, header.TextureMatrixOffset, header.MatrixCount);
            var recolors = new List<Recolor>();
            foreach (RecolorMetadata meta in recolorMeta)
            {
                ReadOnlySpan<byte> modelBytes = initialBytes;
                Header modelHeader = header;
                if (Path.Combine(Paths.FileSystem, meta.ModelPath) != path)
                {
                    modelBytes = ReadBytes(meta.ModelPath);
                    modelHeader = ReadStruct<Header>(modelBytes[0..Sizes.Header]);
                }
                IReadOnlyList<Texture> textures = DoOffsets<Texture>(modelBytes, modelHeader.TextureOffset, modelHeader.TextureCount);
                IReadOnlyList<Palette> palettes = DoOffsets<Palette>(modelBytes, modelHeader.PaletteOffset, modelHeader.PaletteCount);
                ReadOnlySpan<byte> textureBytes = modelBytes;
                if (meta.TexturePath != meta.ModelPath)
                {
                    textureBytes = ReadBytes(meta.TexturePath);
                }
                ReadOnlySpan<byte> paletteBytes = textureBytes;
                if (meta.PalettePath != meta.TexturePath && meta.ReplaceIds.Count == 0)
                {
                    paletteBytes = ReadBytes(meta.PalettePath);
                    if (meta.SeparatePaletteHeader)
                    {
                        Header paletteHeader = ReadStruct<Header>(paletteBytes[0..Sizes.Header]);
                        palettes = DoOffsets<Palette>(paletteBytes, paletteHeader.PaletteOffset, paletteHeader.PaletteCount);
                    }
                }
                var textureData = new List<IReadOnlyList<TextureData>>();
                var paletteData = new List<IReadOnlyList<PaletteData>>();
                foreach (Texture texture in textures)
                {
                    textureData.Add(GetTextureData(texture, textureBytes));
                }
                foreach (Palette palette in palettes)
                {
                    paletteData.Add(GetPaletteData(palette, paletteBytes));
                }
                if (meta.PalettePath != meta.TexturePath && meta.ReplaceIds.Count > 0)
                {
                    paletteBytes = ReadBytes(meta.PalettePath);
                    Header paletteHeader = ReadStruct<Header>(paletteBytes[0..Sizes.Header]);
                    IReadOnlyList<Palette> replacePalettes
                        = DoOffsets<Palette>(paletteBytes, paletteHeader.PaletteOffset, paletteHeader.PaletteCount);
                    var replacePaletteData = new List<IReadOnlyList<PaletteData>>();
                    foreach (Palette palette in replacePalettes)
                    {
                        replacePaletteData.Add(GetPaletteData(palette, paletteBytes));
                    }
                    for (int i = 0; i < replacePaletteData.Count; i++)
                    {
                        if (meta.ReplaceIds.TryGetValue(i, out IEnumerable<int>? replaceIds))
                        {
                            // note: palette header is not being replaced
                            foreach (int replaceId in replaceIds)
                            {
                                paletteData[replaceId] = replacePaletteData[i];
                            }
                        }
                    }
                }
                recolors.Add(new Recolor(meta.Name, textures, palettes, textureData, paletteData));
            }
            AnimationResults animations = LoadAnimationAndDump(animationPath);
            return new Model(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                textureMatrices, recolors, defaultRecolor, useLightSources);
        }

        private class AnimationResults
        {
            public List<NodeAnimationGroup> NodeAnimationGroups { get; } = new List<NodeAnimationGroup>();
            public List<MaterialAnimationGroup> MaterialAnimationGroups { get; } = new List<MaterialAnimationGroup>();
            public List<TexcoordAnimationGroup> TexcoordAnimationGroups { get; } = new List<TexcoordAnimationGroup>();
            public List<TextureAnimationGroup> TextureAnimationGroups { get; } = new List<TextureAnimationGroup>();
        }

        // todo: parse node animations, figure out group indexing
        private static AnimationResults LoadAnimation(string? path)
        {
            var results = new AnimationResults();
            if (path == null)
            {
                return results;
            }
            path = Path.Combine(Paths.FileSystem, path);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            AnimationHeader header = ReadStruct<AnimationHeader>(bytes);
            var nodeGroupOffsets = new List<uint>();
            var materialGroupOffsets = new List<uint>();
            var texcoordGroupOffsets = new List<uint>();
            var textureGroupOffsets = new List<uint>();
            for (int i = 0; i < header.Count; i++)
            {
                nodeGroupOffsets.Add(SpanReadUint(bytes, (int)header.NodeGroupOffset + i * sizeof(uint)));
            }
            for (int i = 0; i < header.Count; i++)
            {
                materialGroupOffsets.Add(SpanReadUint(bytes, (int)header.MaterialGroupOffset + i * sizeof(uint)));
            }
            for (int i = 0; i < header.Count; i++)
            {
                texcoordGroupOffsets.Add(SpanReadUint(bytes, (int)header.TexcoordGroupOffset + i * sizeof(uint)));
            }
            for (int i = 0; i < header.Count; i++)
            {
                textureGroupOffsets.Add(SpanReadUint(bytes, (int)header.TextureGroupOffset + i * sizeof(uint)));
            }
            foreach (uint offset in nodeGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                RawNodeAnimationGroup rawGroup = DoOffset<RawNodeAnimationGroup>(bytes, offset);
                // there doesn't seem to be an animation count, so we have to assume it from the space between offsets
                Debug.Assert(offset > rawGroup.AnimationOffset);
                Debug.Assert((offset - rawGroup.AnimationOffset) % Sizes.NodeAnimation == 0);
                int animationCount = (int)((offset - rawGroup.AnimationOffset) / Sizes.NodeAnimation);
                IReadOnlyList<NodeAnimation> rawAnimations
                    = DoOffsets<NodeAnimation>(bytes, rawGroup.AnimationOffset, animationCount);
                var animations = new Dictionary<string, NodeAnimation>();
                int i = 0;
                foreach (NodeAnimation animation in rawAnimations)
                {
                    animations.Add($"{offset}-{i++}", animation);
                }
                // todo: do the animation have counts like the others, or do we have to just assume the layout?
                Debug.Assert(rawGroup.UInt16Pointer > rawGroup.Fixed32Pointer);
                Debug.Assert(rawGroup.Int32Pointer > rawGroup.UInt16Pointer);
                Debug.Assert(rawGroup.AnimationOffset > rawGroup.Int32Pointer);
                Debug.Assert((rawGroup.UInt16Pointer - rawGroup.Fixed32Pointer) % sizeof(int) == 0);
                Debug.Assert((rawGroup.Int32Pointer - rawGroup.UInt16Pointer) % sizeof(ushort) == 0);
                Debug.Assert((rawGroup.AnimationOffset - rawGroup.Int32Pointer) % sizeof(int) == 0);
                int maxFixed32 = (int)((rawGroup.UInt16Pointer - rawGroup.Fixed32Pointer) / sizeof(int));
                int maxUInt16 = (int)((rawGroup.Int32Pointer - rawGroup.UInt16Pointer) / sizeof(ushort));
                int maxInt32 = (int)((rawGroup.AnimationOffset - rawGroup.Int32Pointer) / sizeof(int));
                // todo: what are these?
                var fixed32s = DoOffsets<Fixed>(bytes, rawGroup.Fixed32Pointer, maxFixed32).ToList();
                var uint16s = DoOffsets<ushort>(bytes, rawGroup.UInt16Pointer, maxUInt16).ToList();
                var int32s = DoOffsets<int>(bytes, rawGroup.Int32Pointer, maxInt32).ToList();
                results.NodeAnimationGroups.Add(new NodeAnimationGroup(rawGroup, fixed32s, uint16s, int32s, animations));
            }
            foreach (uint offset in materialGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                int maxColor = 0;
                RawMaterialAnimationGroup rawGroup = DoOffset<RawMaterialAnimationGroup>(bytes, offset);
                IReadOnlyList<MaterialAnimation> rawAnimations
                    = DoOffsets<MaterialAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                var animations = new Dictionary<string, MaterialAnimation>();
                foreach (MaterialAnimation animation in rawAnimations)
                {
                    maxColor = Math.Max(maxColor, animation.DiffuseLutStartIndexR + animation.DiffuseLutLengthR);
                    maxColor = Math.Max(maxColor, animation.DiffuseLutStartIndexG + animation.DiffuseLutLengthG);
                    maxColor = Math.Max(maxColor, animation.DiffuseLutStartIndexB + animation.DiffuseLutLengthB);
                    maxColor = Math.Max(maxColor, animation.AmbientLutStartIndexR + animation.AmbientLutLengthR);
                    maxColor = Math.Max(maxColor, animation.AmbientLutStartIndexG + animation.AmbientLutLengthG);
                    maxColor = Math.Max(maxColor, animation.AmbientLutStartIndexB + animation.AmbientLutLengthB);
                    maxColor = Math.Max(maxColor, animation.SpecularLutStartIndexR + animation.SpecularLutLengthR);
                    maxColor = Math.Max(maxColor, animation.SpecularLutStartIndexG + animation.SpecularLutLengthG);
                    maxColor = Math.Max(maxColor, animation.SpecularLutStartIndexB + animation.SpecularLutLengthB);
                    maxColor = Math.Max(maxColor, animation.AlphaLutStartIndex + animation.AlphaLutLength);
                    animations.Add(animation.Name, animation);
                }
                var colors = DoOffsets<byte>(bytes, rawGroup.ColorLutOffset, maxColor).Select(b => (float)b).ToList();
                results.MaterialAnimationGroups.Add(new MaterialAnimationGroup(rawGroup, colors, animations));
            }
            foreach (uint offset in texcoordGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                int maxScale = 0;
                int maxRotation = 0;
                int maxTranslation = 0;
                RawTexcoordAnimationGroup rawGroup = DoOffset<RawTexcoordAnimationGroup>(bytes, offset);
                IReadOnlyList<TexcoordAnimation> rawAnimations
                    = DoOffsets<TexcoordAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                var animations = new Dictionary<string, TexcoordAnimation>();
                foreach (TexcoordAnimation animation in rawAnimations)
                {
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexS + animation.ScaleLutLengthS);
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexT + animation.ScaleLutLengthT);
                    maxRotation = Math.Max(maxRotation, animation.RotateLutIndexZ + animation.RotateLutLengthZ);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexS + animation.TranslateLutLengthS);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexT + animation.TranslateLutLengthT);
                    animations.Add(animation.Name, animation);
                }
                var scales = DoOffsets<Fixed>(bytes, rawGroup.ScaleLutOffset, maxScale).Select(f => f.FloatValue).ToList();
                var rotations = new List<float>();
                foreach (ushort value in DoOffsets<ushort>(bytes, rawGroup.RotateLutOffset, maxRotation))
                {
                    long radians = (0x6487FL * value + 0x80000) >> 20;
                    rotations.Add(Fixed.ToFloat(radians));
                }
                var translations = DoOffsets<Fixed>(bytes, rawGroup.TranslateLutOffset, maxTranslation).Select(f => f.FloatValue).ToList();
                results.TexcoordAnimationGroups.Add(new TexcoordAnimationGroup(rawGroup, scales, rotations, translations, animations));
            }
            foreach (uint offset in textureGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                RawTextureAnimationGroup rawGroup = DoOffset<RawTextureAnimationGroup>(bytes, offset);
                IReadOnlyList<TextureAnimation> rawAnimations
                    = DoOffsets<TextureAnimation>(bytes, rawGroup.AnimationOffset, rawGroup.AnimationCount);
                var animations = new Dictionary<string, TextureAnimation>();
                foreach (TextureAnimation animation in rawAnimations)
                {
                    animations.Add(animation.Name, animation);
                }
                IReadOnlyList<ushort> frameIndices = DoOffsets<ushort>(bytes, rawGroup.FrameIndexOffset, rawGroup.FrameIndexCount);
                IReadOnlyList<ushort> textureIds = DoOffsets<ushort>(bytes, rawGroup.TextureIdOffset, rawGroup.TextureIdCount);
                IReadOnlyList<ushort> paletteIds = DoOffsets<ushort>(bytes, rawGroup.PaletteIdOffset, rawGroup.PaletteIdCount);
                results.TextureAnimationGroups.Add(new TextureAnimationGroup(rawGroup, frameIndices, textureIds, paletteIds, animations));
            }
            return results;
        }

        private class DumpResult
        {
            public uint Offset { get; }
            public int Length { get; }
            public string Description { get; }
            public IReadOnlyList<byte> Bytes { get; }
            public int Size { get; }

            protected DumpResult(uint offset, string description, IEnumerable<byte> bytes, int size)
            {
                Offset = offset;
                Length = bytes.Count();
                Description = description;
                Bytes = bytes.ToList();
                Size = size;
            }

            protected DumpResult(uint offset, string description, ReadOnlySpan<byte> bytes, int size)
            {
                Offset = offset;
                Length = bytes.Length;
                Description = description;
                Bytes = bytes.ToArray().ToList();
                Size = size;
            }
        }

        private class DumpResult<T> : DumpResult
        {
            public T Structure { get; }

            public DumpResult(uint offset, string description, IEnumerable<byte> bytes, T structure, int size = 0)
                : base(offset, description, bytes, size)
            {
                Structure = structure;
            }

            public DumpResult(uint offset, string description, ReadOnlySpan<byte> bytes, T structure, int size = 0)
                : base(offset, description, bytes, size)
            {
                Structure = structure;
            }
        }

        private static AnimationResults LoadAnimationAndDump(string? path)
        {
            var results = new AnimationResults();
            if (path == null)
            {
                return results;
            }
            var dump = new List<DumpResult>();
            path = Path.Combine(Paths.FileSystem, path);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            AnimationHeader header = ReadStruct<AnimationHeader>(bytes);
            dump.Add(new DumpResult<AnimationHeader>(0, "Header", bytes[0..Marshal.SizeOf<AnimationHeader>()], header));
            var nodeGroupOffsets = new List<uint>();
            var materialGroupOffsets = new List<uint>();
            var texcoordGroupOffsets = new List<uint>();
            var textureGroupOffsets = new List<uint>();
            var unusedGroupOffsets = new List<uint>();
            for (int i = 0; i < header.Count; i++)
            {
                nodeGroupOffsets.Add(SpanReadUint(bytes, (int)header.NodeGroupOffset + i * sizeof(uint)));
            }
            dump.Add(new DumpResult<List<uint>>(header.NodeGroupOffset, "NodeGroupOffsets",
                bytes[((int)header.NodeGroupOffset)..((int)header.NodeGroupOffset + header.Count * sizeof(uint))], nodeGroupOffsets));
            for (int i = 0; i < header.Count; i++)
            {
                materialGroupOffsets.Add(SpanReadUint(bytes, (int)header.MaterialGroupOffset + i * sizeof(uint)));
            }
            dump.Add(new DumpResult<List<uint>>(header.MaterialGroupOffset, "MaterialGroupOffsets",
                bytes[((int)header.MaterialGroupOffset)..((int)header.MaterialGroupOffset + header.Count * sizeof(uint))], materialGroupOffsets));
            for (int i = 0; i < header.Count; i++)
            {
                texcoordGroupOffsets.Add(SpanReadUint(bytes, (int)header.TexcoordGroupOffset + i * sizeof(uint)));
            }
            dump.Add(new DumpResult<List<uint>>(header.TexcoordGroupOffset, "TexcoordGroupOffsets",
                bytes[((int)header.TexcoordGroupOffset)..((int)header.TexcoordGroupOffset + header.Count * sizeof(uint))], texcoordGroupOffsets));
            for (int i = 0; i < header.Count; i++)
            {
                textureGroupOffsets.Add(SpanReadUint(bytes, (int)header.TextureGroupOffset + i * sizeof(uint)));
            }
            dump.Add(new DumpResult<List<uint>>(header.TextureGroupOffset, "TextureGroupOffsets",
                bytes[((int)header.TextureGroupOffset)..((int)header.TextureGroupOffset + header.Count * sizeof(uint))], textureGroupOffsets));
            for (int i = 0; i < header.Count; i++)
            {
                unusedGroupOffsets.Add(SpanReadUint(bytes, (int)header.UnusedGroupOffset + i * sizeof(uint)));
            }
            dump.Add(new DumpResult<List<uint>>(header.UnusedGroupOffset, "UnusedGroupOffsets",
                bytes[((int)header.UnusedGroupOffset)..((int)header.UnusedGroupOffset + header.Count * sizeof(uint))], unusedGroupOffsets));
            foreach (uint offset in nodeGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                RawNodeAnimationGroup rawGroup = DoOffset<RawNodeAnimationGroup>(bytes, offset);
                dump.Add(new DumpResult<RawNodeAnimationGroup>(offset, "NodeAnimationGroup",
                    bytes[(int)offset..((int)offset + Marshal.SizeOf<RawNodeAnimationGroup>())], rawGroup));
                // there doesn't seem to be an animation count, so we have to assume it from the space between offsets
                Debug.Assert(offset > rawGroup.AnimationOffset);
                Debug.Assert((offset - rawGroup.AnimationOffset) % Sizes.NodeAnimation == 0);
                int animationCount = (int)((offset - rawGroup.AnimationOffset) / Sizes.NodeAnimation);
                IReadOnlyList<NodeAnimation> rawAnimations
                    = DoOffsets<NodeAnimation>(bytes, rawGroup.AnimationOffset, animationCount);
                for (int j = 0; j < animationCount; j++)
                {
                    int size = Marshal.SizeOf<NodeAnimation>();
                    long start = rawGroup.AnimationOffset + j * size;
                    dump.Add(new DumpResult<NodeAnimation>((uint)start, "NodeAnimation",
                        bytes[(int)start..(int)(start + size)], rawAnimations[j]));
                }
                var animations = new Dictionary<string, NodeAnimation>();
                int i = 0;
                foreach (NodeAnimation animation in rawAnimations)
                {
                    animations.Add($"{offset}-{i++}", animation);
                }
                // todo: do the animation have counts like the others, or do we have to just assume the layout?
                Debug.Assert(rawGroup.UInt16Pointer > rawGroup.Fixed32Pointer);
                Debug.Assert(rawGroup.Int32Pointer > rawGroup.UInt16Pointer);
                Debug.Assert(rawGroup.AnimationOffset > rawGroup.Int32Pointer);
                Debug.Assert((rawGroup.UInt16Pointer - rawGroup.Fixed32Pointer) % sizeof(int) == 0);
                Debug.Assert((rawGroup.Int32Pointer - rawGroup.UInt16Pointer) % sizeof(ushort) == 0);
                Debug.Assert((rawGroup.AnimationOffset - rawGroup.Int32Pointer) % sizeof(int) == 0);
                int maxFixed32 = (int)((rawGroup.UInt16Pointer - rawGroup.Fixed32Pointer) / sizeof(int));
                int maxUInt16 = (int)((rawGroup.Int32Pointer - rawGroup.UInt16Pointer) / sizeof(ushort));
                int maxInt32 = (int)((rawGroup.AnimationOffset - rawGroup.Int32Pointer) / sizeof(int));
                // todo: what are these?
                var fixed32s = DoOffsets<Fixed>(bytes, rawGroup.Fixed32Pointer, maxFixed32).ToList();
                if (fixed32s.Count > 0)
                {
                    dump.Add(new DumpResult<List<Fixed>>(rawGroup.Fixed32Pointer, "Node Fixed32s",
                        bytes[(int)rawGroup.Fixed32Pointer..((int)rawGroup.Fixed32Pointer + maxFixed32 * sizeof(int))], fixed32s));
                }
                var uint16s = DoOffsets<ushort>(bytes, rawGroup.UInt16Pointer, maxUInt16).ToList();
                if (uint16s.Count > 0)
                {
                    dump.Add(new DumpResult<List<ushort>>(rawGroup.UInt16Pointer, "Node UInt16s",
                        bytes[(int)rawGroup.UInt16Pointer..((int)rawGroup.UInt16Pointer + maxUInt16 * sizeof(ushort))], uint16s));
                }
                var int32s = DoOffsets<int>(bytes, rawGroup.Int32Pointer, maxInt32).ToList();
                if (int32s.Count > 0)
                {
                    dump.Add(new DumpResult<List<int>>(rawGroup.Int32Pointer, "Node Int32s",
                        bytes[(int)rawGroup.Int32Pointer..((int)rawGroup.Int32Pointer + maxInt32 * sizeof(int))], int32s));
                }
                results.NodeAnimationGroups.Add(new NodeAnimationGroup(rawGroup, fixed32s, uint16s, int32s, animations));
            }
            foreach (uint offset in materialGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                int maxColor = 0;
                RawMaterialAnimationGroup rawGroup = DoOffset<RawMaterialAnimationGroup>(bytes, offset);
                dump.Add(new DumpResult<RawMaterialAnimationGroup>(offset, "MaterialAnimationGroup",
                    bytes[(int)offset..((int)offset + Marshal.SizeOf<RawMaterialAnimationGroup>())], rawGroup));
                IReadOnlyList<MaterialAnimation> rawAnimations
                    = DoOffsets<MaterialAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                for (int j = 0; j < rawGroup.AnimationCount; j++)
                {
                    int size = Marshal.SizeOf<MaterialAnimation>();
                    long start = rawGroup.AnimationOffset + j * size;
                    dump.Add(new DumpResult<MaterialAnimation>((uint)start, "MaterialAnimation",
                        bytes[(int)start..(int)(start + size)], rawAnimations[j]));
                }
                var animations = new Dictionary<string, MaterialAnimation>();
                foreach (MaterialAnimation animation in rawAnimations)
                {
                    maxColor = Math.Max(maxColor, animation.DiffuseLutStartIndexR + animation.DiffuseLutLengthR);
                    maxColor = Math.Max(maxColor, animation.DiffuseLutStartIndexG + animation.DiffuseLutLengthG);
                    maxColor = Math.Max(maxColor, animation.DiffuseLutStartIndexB + animation.DiffuseLutLengthB);
                    maxColor = Math.Max(maxColor, animation.AmbientLutStartIndexR + animation.AmbientLutLengthR);
                    maxColor = Math.Max(maxColor, animation.AmbientLutStartIndexG + animation.AmbientLutLengthG);
                    maxColor = Math.Max(maxColor, animation.AmbientLutStartIndexB + animation.AmbientLutLengthB);
                    maxColor = Math.Max(maxColor, animation.SpecularLutStartIndexR + animation.SpecularLutLengthR);
                    maxColor = Math.Max(maxColor, animation.SpecularLutStartIndexG + animation.SpecularLutLengthG);
                    maxColor = Math.Max(maxColor, animation.SpecularLutStartIndexB + animation.SpecularLutLengthB);
                    maxColor = Math.Max(maxColor, animation.AlphaLutStartIndex + animation.AlphaLutLength);
                    animations.Add(animation.Name, animation);
                }
                var colors = DoOffsets<byte>(bytes, rawGroup.ColorLutOffset, maxColor).Select(b => (float)b).ToList();
                if (colors.Count > 0)
                {
                    dump.Add(new DumpResult<List<float>>(rawGroup.ColorLutOffset, "Material Colors",
                        bytes[(int)rawGroup.ColorLutOffset..((int)rawGroup.ColorLutOffset + maxColor * sizeof(byte))],
                        colors, sizeof(byte)));
                    int padding = colors.Count % 4;
                    if (padding != 0)
                    {
                        IEnumerable<byte> paddingBytes = Enumerable.Repeat((byte)0, 4 - padding);
                        dump.Add(new DumpResult<List<byte>>(rawGroup.ColorLutOffset + (uint)maxColor * sizeof(byte),
                            "Padding", paddingBytes, paddingBytes.ToList()));
                    }
                }
                results.MaterialAnimationGroups.Add(new MaterialAnimationGroup(rawGroup, colors, animations));
            }
            foreach (uint offset in texcoordGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                int maxScale = 0;
                int maxRotation = 0;
                int maxTranslation = 0;
                RawTexcoordAnimationGroup rawGroup = DoOffset<RawTexcoordAnimationGroup>(bytes, offset);
                dump.Add(new DumpResult<RawTexcoordAnimationGroup>(offset, "TexcoordAnimationGroup",
                    bytes[(int)offset..((int)offset + Marshal.SizeOf<RawTexcoordAnimationGroup>())], rawGroup));
                IReadOnlyList<TexcoordAnimation> rawAnimations
                    = DoOffsets<TexcoordAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                for (int j = 0; j < rawGroup.AnimationCount; j++)
                {
                    int size = Marshal.SizeOf<TexcoordAnimation>();
                    long start = rawGroup.AnimationOffset + j * size;
                    dump.Add(new DumpResult<TexcoordAnimation>((uint)start, "TexcoordAnimation",
                        bytes[(int)start..(int)(start + size)], rawAnimations[j]));
                }
                var animations = new Dictionary<string, TexcoordAnimation>();
                foreach (TexcoordAnimation animation in rawAnimations)
                {
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexS + animation.ScaleLutLengthS);
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexT + animation.ScaleLutLengthT);
                    maxRotation = Math.Max(maxRotation, animation.RotateLutIndexZ + animation.RotateLutLengthZ);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexS + animation.TranslateLutLengthS);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexT + animation.TranslateLutLengthT);
                    animations.Add(animation.Name, animation);
                }
                var scales = DoOffsets<Fixed>(bytes, rawGroup.ScaleLutOffset, maxScale).Select(f => f.FloatValue).ToList();
                if (scales.Count > 0)
                {
                    dump.Add(new DumpResult<List<float>>(rawGroup.ScaleLutOffset, "Texcoord Scales",
                        bytes[(int)rawGroup.ScaleLutOffset..((int)rawGroup.ScaleLutOffset + maxScale * sizeof(int))],
                        scales, sizeof(int)));
                }
                var rotations = new List<float>();
                foreach (ushort value in DoOffsets<ushort>(bytes, rawGroup.RotateLutOffset, maxRotation))
                {
                    long radians = (0x6487FL * value + 0x80000) >> 20;
                    rotations.Add(Fixed.ToFloat(radians));
                }
                if (rotations.Count > 0)
                {
                    dump.Add(new DumpResult<List<float>>(rawGroup.RotateLutOffset, "Texcoord Rotations",
                        bytes[(int)rawGroup.RotateLutOffset..((int)rawGroup.RotateLutOffset + maxRotation * sizeof(ushort))],
                        rotations, sizeof(ushort)));
                }
                var translations = DoOffsets<Fixed>(bytes, rawGroup.TranslateLutOffset, maxTranslation).Select(f => f.FloatValue).ToList();
                if (translations.Count > 0)
                {
                    dump.Add(new DumpResult<List<float>>(rawGroup.TranslateLutOffset, "Texcoord Translations",
                        bytes[(int)rawGroup.TranslateLutOffset..((int)rawGroup.TranslateLutOffset + maxTranslation * sizeof(int))],
                        translations, sizeof(int)));
                }
                results.TexcoordAnimationGroups.Add(new TexcoordAnimationGroup(rawGroup, scales, rotations, translations, animations));
            }
            foreach (uint offset in textureGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                RawTextureAnimationGroup rawGroup = DoOffset<RawTextureAnimationGroup>(bytes, offset);
                dump.Add(new DumpResult<RawTextureAnimationGroup>(offset, "TextureAnimationGroup",
                    bytes[(int)offset..((int)offset + Marshal.SizeOf<RawTextureAnimationGroup>())], rawGroup));
                IReadOnlyList<TextureAnimation> rawAnimations
                    = DoOffsets<TextureAnimation>(bytes, rawGroup.AnimationOffset, rawGroup.AnimationCount);
                for (int j = 0; j < rawGroup.AnimationCount; j++)
                {
                    int size = Marshal.SizeOf<TextureAnimation>();
                    long start = rawGroup.AnimationOffset + j * size;
                    dump.Add(new DumpResult<TextureAnimation>((uint)start, "TextureAnimation",
                        bytes[(int)start..(int)(start + size)], rawAnimations[j]));
                }
                var animations = new Dictionary<string, TextureAnimation>();
                foreach (TextureAnimation animation in rawAnimations)
                {
                    animations.Add(animation.Name, animation);
                }
                IReadOnlyList<ushort> frameIndices = DoOffsets<ushort>(bytes, rawGroup.FrameIndexOffset, rawGroup.FrameIndexCount);
                if (frameIndices.Count > 0)
                {
                    dump.Add(new DumpResult<List<ushort>>(rawGroup.FrameIndexOffset, "Frame Indices",
                        bytes[(int)rawGroup.FrameIndexOffset..((int)rawGroup.FrameIndexOffset + sizeof(ushort) * rawGroup.FrameIndexCount)],
                        frameIndices.ToList()));
                }
                IReadOnlyList<ushort> textureIds = DoOffsets<ushort>(bytes, rawGroup.TextureIdOffset, rawGroup.TextureIdCount);
                if (textureIds.Count > 0)
                {
                    dump.Add(new DumpResult<List<ushort>>(rawGroup.TextureIdOffset, "Texture IDs",
                        bytes[(int)rawGroup.TextureIdOffset..((int)rawGroup.TextureIdOffset + sizeof(ushort) * rawGroup.TextureIdCount)],
                        textureIds.ToList()));
                }
                IReadOnlyList<ushort> paletteIds = DoOffsets<ushort>(bytes, rawGroup.PaletteIdOffset, rawGroup.PaletteIdCount);
                if (paletteIds.Count > 0)
                {
                    dump.Add(new DumpResult<List<ushort>>(rawGroup.PaletteIdOffset, "Palette IDs",
                        bytes[(int)rawGroup.PaletteIdOffset..((int)rawGroup.PaletteIdOffset + sizeof(ushort) * rawGroup.PaletteIdCount)],
                        paletteIds.ToList()));
                }
                results.TextureAnimationGroups.Add(new TextureAnimationGroup(rawGroup, frameIndices, textureIds, paletteIds, animations));
            }
            var gaps = new List<DumpResult>();
            dump = dump.OrderBy(d => d.Offset).ToList();
            for (int i = 0; i < dump.Count; i++)
            {
                DumpResult line = dump[i];
                uint offset = (uint)(line.Offset + line.Length);
                if (i == dump.Count - 1)
                {
                    if (offset != bytes.Length)
                    {
                        var gap = new List<byte>();
                        for (uint b = offset; b < bytes.Length; b++)
                        {
                            gap.Add(bytes[(int)b]);
                        }
                        gaps.Add(new DumpResult<byte>(offset, "Gap", gap, 0));
                    }
                }
                else
                {
                    DumpResult next = dump[i + 1];
                    if (offset < next.Offset)
                    {
                        var gap = new List<byte>();
                        for (uint b = offset; b < next.Offset; b++)
                        {
                            gap.Add(bytes[(int)b]);
                        }
                        gaps.Add(new DumpResult<byte>(offset, "Gap", gap, 0));
                    }
                }
            }
            dump.AddRange(gaps);
            dump = dump.OrderBy(d => d.Offset).ToList();
            string filename = Path.GetFileNameWithoutExtension(path);
            var lines = new List<string>();
            lines.Add(filename);
            lines.Add($"{bytes.Length} bytes (0x00 - 0x{bytes.Length - 1:X2})");
            lines.Add($"Gaps: {gaps.Count}");
            lines.Add("");
            foreach (DumpResult line in dump)
            {
                lines.AddRange(Dump(line));
                lines.Add("");
            }
            lines.RemoveAt(lines.Count - 1);
            string dumpFile = $"{filename}.txt";
            string dumpPath = Path.Combine(Paths.Export, "..", "..", "Dumps", path.Contains("_fh") ? "FH" : "MPH");
            Directory.CreateDirectory(dumpPath);
            File.WriteAllLines(Path.Combine(dumpPath, dumpFile), lines);
            return results;
        }

        private static IEnumerable<string> Dump(DumpResult line)
        {
            var lines = new List<string>();
            void AddHeader()
            {
                lines.Add($"0x{line.Offset:X2}: {line.Description}");
                lines.Add($"{line.Length} bytes (0x{line.Offset:X2} - 0x{line.Offset + line.Length - 1:X2})");
            }
            void AddListHeader(int size)
            {
                // sktodo
                lines.Add($"0x{line.Offset:X2}: {line.Description}");
                int count = line.Length / size;
                string entries = $"{count} entr{(count == 1 ? "y" : "ies")}";
                lines.Add($"{entries}, {line.Length} bytes (0x{line.Offset:X2} - 0x{line.Offset + line.Length - 1:X2})");
            }
            if (line is DumpResult<byte> result0)
            {
                AddHeader();
                lines.Add(String.Join(' ', result0.Bytes.Select(b => b.ToString("X2"))));
            }
            else if (line is DumpResult<AnimationHeader> result1)
            {
                AddHeader();
                lines.AddRange(DumpObj(result1.Structure));
            }
            else if (line is DumpResult<RawNodeAnimationGroup> result2)
            {
                AddHeader();
                lines.AddRange(DumpObj(result2.Structure));
            }
            else if (line is DumpResult<NodeAnimation> result3)
            {
                AddHeader();
                lines.AddRange(DumpObj(result3.Structure));
            }
            else if (line is DumpResult<RawMaterialAnimationGroup> result4)
            {
                AddHeader();
                lines.AddRange(DumpObj(result4.Structure));
            }
            else if (line is DumpResult<MaterialAnimation> result5)
            {
                AddHeader();
                lines.AddRange(DumpObj(result5.Structure));
            }
            else if (line is DumpResult<RawTexcoordAnimationGroup> result6)
            {
                AddHeader();
                lines.AddRange(DumpObj(result6.Structure));
            }
            else if (line is DumpResult<TexcoordAnimation> result7)
            {
                AddHeader();
                lines.AddRange(DumpObj(result7.Structure));
            }
            else if (line is DumpResult<RawTextureAnimationGroup> result8)
            {
                AddHeader();
                lines.AddRange(DumpObj(result8.Structure));
            }
            else if (line is DumpResult<TextureAnimation> result9)
            {
                AddHeader();
                lines.AddRange(DumpObj(result9.Structure));
            }
            else if (line is DumpResult<List<Fixed>> result10)
            {
                AddListHeader(sizeof(int));
                lines.Add(String.Join(", ", result10.Structure));
            }
            else if (line is DumpResult<List<int>> result11)
            {
                AddListHeader(sizeof(int));
                lines.Add(String.Join(", ", result11.Structure));
            }
            else if (line is DumpResult<List<uint>> result12)
            {
                AddListHeader(sizeof(uint));
                foreach (uint item in result12.Structure)
                {
                    lines.Add($"0x{item:X2}");
                }
            }
            else if (line is DumpResult<List<uint>> result13)
            {
                AddListHeader(sizeof(uint));
                foreach (uint item in result13.Structure)
                {
                    lines.Add($"0x{item:X2}");
                }
            }
            else if (line is DumpResult<List<float>> result14)
            {
                AddListHeader(result14.Size);
                lines.Add(String.Join(", ", result14.Structure));
            }
            else if (line is DumpResult<List<ushort>> result15)
            {
                AddListHeader(sizeof(ushort));
                lines.Add(String.Join(", ", result15.Structure));
            }
            else if (line is DumpResult<List<byte>> result16)
            {
                AddHeader();
                lines.Add(String.Join(' ', result16.Structure.Select(s => $"{s:X2}")));
            }
            return lines;
        }

        private static IEnumerable<string> DumpObj(object obj)
        {
            var lines = new List<string>();
            var fields = new List<(string, object?)>();
            Type type = obj.GetType();
            foreach (FieldInfo info in type.GetFields())
            {
                fields.Add((info.Name, info.GetValue(obj)));
            }
            foreach (PropertyInfo info in type.GetProperties())
            {
                fields.Add((info.Name, info.GetValue(obj)));
            }
            foreach ((string name, object? value) in fields)
            {
                if (name.Contains("Offset") || name.Contains("Pointer"))
                {
                    lines.Add($"{name} = 0x{value:X2}");
                }
                else
                {
                    lines.Add($"{name} = {value}");
                }
            }
            return lines;
        }

        private static ReadOnlySpan<byte> ReadBytes(string path)
        {
            return new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
        }

        private static IReadOnlyList<TextureData> GetTextureData(Texture texture, ReadOnlySpan<byte> textureBytes)
        {
            var data = new List<TextureData>();
            int pixelCount = texture.Width * texture.Height;
            int entriesPerByte = 1;
            if (texture.Format == TextureFormat.Palette2Bit)
            {
                entriesPerByte = 4;
            }
            else if (texture.Format == TextureFormat.Palette4Bit)
            {
                entriesPerByte = 2;
            }
            if (pixelCount % entriesPerByte != 0)
            {
                throw new ProgramException($"Pixel count {pixelCount} is not divisible by {entriesPerByte}.");
            }
            pixelCount /= entriesPerByte;
            if (texture.Format == TextureFormat.DirectRgb)
            {
                for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                {
                    ushort color = SpanReadUshort(textureBytes, (int)(texture.ImageOffset + pixelIndex * 2));
                    byte alpha = AlphaFromShort(color);
                    data.Add(new TextureData(color, alpha));
                }
            }
            else
            {
                for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                {
                    byte entry = textureBytes[(int)(texture.ImageOffset + pixelIndex)];
                    for (int entryIndex = 0; entryIndex < entriesPerByte; entryIndex++)
                    {
                        uint index = (uint)(entry >> ((pixelIndex * entriesPerByte + entryIndex) % entriesPerByte
                            * (8 / entriesPerByte)));
                        byte alpha = 255;
                        if (texture.Format == TextureFormat.Palette2Bit)
                        {
                            index &= 0x3;
                        }
                        else if (texture.Format == TextureFormat.Palette4Bit)
                        {
                            index &= 0xF;
                        }
                        else if (texture.Format == TextureFormat.PaletteA5I3)
                        {
                            index &= 0x7;
                            alpha = AlphaFromA5I3(entry);
                        }
                        else if (texture.Format == TextureFormat.PaletteA3I5)
                        {
                            index &= 0x1F;
                            alpha = AlphaFromA3I5(entry);
                        }
                        if (texture.Format == TextureFormat.Palette2Bit || texture.Format == TextureFormat.Palette4Bit
                            || texture.Format == TextureFormat.Palette8Bit)
                        {
                            if (texture.Opaque == 0 && index == 0)
                            {
                                alpha = 0;
                            }
                        }
                        data.Add(new TextureData(index, alpha));
                    }
                }
            }
            return data;
        }

        private static IReadOnlyList<PaletteData> GetPaletteData(Palette palette, ReadOnlySpan<byte> paletteBytes)
        {
            if (palette.Count % 2 != 0)
            {
                throw new ProgramException($"Palette count {palette.Count} is not divisible by 2.");
            }
            var data = new List<PaletteData>();
            for (int i = 0; i < palette.Count / 2; i++)
            {
                ushort entry = SpanReadUshort(paletteBytes, (int)(palette.Offset + i * 2));
                data.Add(new PaletteData(entry));
            }
            return data;
        }

        private static ColorRgba ColorFromShort(ushort value, byte alpha)
        {
            byte red = (byte)(((value >> 0) & 0x1F) << 3);
            byte green = (byte)(((value >> 5) & 0x1F) << 3);
            byte blue = (byte)(((value >> 10) & 0x1F) << 3);
            return new ColorRgba(red, green, blue, alpha);
        }

        private static byte AlphaFromShort(ushort value) => (value & 0x8000) == 0 ? (byte)0 : (byte)255;

        private static byte AlphaFromA5I3(byte value) => (byte)((value >> 3) / 31.0f * 255.0f);

        private static byte AlphaFromA3I5(byte value) => (byte)((value >> 5) / 7.0f * 255.0f);

        public static IReadOnlyList<Entity> GetEntities(string path, int layerId)
        {
            path = Path.Combine(Paths.FileSystem, path);
            ReadOnlySpan<byte> bytes = ReadBytes(path);
            uint version = BitConverter.ToUInt32(bytes[0..4]);
            if (version == 1)
            {
                return GetFirstHuntEntities(bytes);
            }
            else if (version != 2)
            {
                throw new ProgramException($"Unexpected entity header version {version}.");
            }
            var entities = new List<Entity>();
            EntityHeader header = ReadStruct<EntityHeader>(bytes[0..Sizes.EntityHeader]);
            for (int i = 0; ; i++)
            {
                int start = Sizes.EntityHeader + Sizes.EntityEntry * i;
                int end = start + Sizes.EntityEntry;
                EntityEntry entry = ReadStruct<EntityEntry>(bytes[start..end]);
                if (entry.DataOffset == 0)
                {
                    break;
                }
                if (layerId == -1 || (entry.LayerMask & (1 << layerId)) != 0)
                {
                    entities.Add(ReadEntity(bytes, entry));
                }
            }
            Debug.Assert(layerId == -1 || entities.Count == header.Lengths[layerId]);
            return entities;
        }

        private static Entity ReadEntity(ReadOnlySpan<byte> bytes, EntityEntry entry)
        {
            int start = (int)entry.DataOffset;
            int end = start + Sizes.EntityDataHeader;
            EntityDataHeader header = ReadStruct<EntityDataHeader>(bytes[start..end]);
            var type = (EntityType)header.Type;
            return type switch
            {
                EntityType.Platform => ReadEntity<PlatformEntityData>(bytes, entry, header),
                EntityType.Object => ReadEntity<ObjectEntityData>(bytes, entry, header),
                EntityType.PlayerSpawn => ReadEntity<PlayerSpawnEntityData>(bytes, entry, header),
                EntityType.Door => ReadEntity<DoorEntityData>(bytes, entry, header),
                EntityType.Item => ReadEntity<ItemEntityData>(bytes, entry, header),
                EntityType.Enemy => ReadEntity<EnemyEntityData>(bytes, entry, header),
                EntityType.Unknown7 => ReadEntity<Unknown7EntityData>(bytes, entry, header),
                EntityType.Unknown8 => ReadEntity<Unknown8EntityData>(bytes, entry, header),
                EntityType.JumpPad => ReadEntity<JumpPadEntityData>(bytes, entry, header),
                EntityType.PointModule => ReadEntity<PointModuleEntityData>(bytes, entry, header),
                EntityType.CameraPosition => ReadEntity<CameraPositionEntityData>(bytes, entry, header),
                EntityType.OctolithFlag => ReadEntity<OctolithFlagEntityData>(bytes, entry, header),
                EntityType.FlagBase => ReadEntity<FlagBaseEntityData>(bytes, entry, header),
                EntityType.Teleporter => ReadEntity<TeleporterEntityData>(bytes, entry, header),
                EntityType.NodeDefense => ReadEntity<NodeDefenseEntityData>(bytes, entry, header),
                EntityType.LightSource => ReadEntity<LightSourceEntityData>(bytes, entry, header),
                EntityType.Artifact => ReadEntity<ArtifactEntityData>(bytes, entry, header),
                EntityType.CameraSequence => ReadEntity<CameraSequenceEntityData>(bytes, entry, header),
                EntityType.ForceField => ReadEntity<ForceFieldEntityData>(bytes, entry, header),
                _ => throw new ProgramException($"Invalid entity type {type}")
            };
        }

        private static Entity<T> ReadEntity<T>(ReadOnlySpan<byte> bytes, EntityEntry entry, EntityDataHeader header)
            where T : struct
        {
            int start = (int)entry.DataOffset;
            int end = start + entry.Length;
            Debug.Assert(entry.Length == Marshal.SizeOf<T>());
            return new Entity<T>(entry, (EntityType)header.Type, header.EntityId, ReadStruct<T>(bytes[start..end]));
        }

        private static IReadOnlyList<Entity> GetFirstHuntEntities(ReadOnlySpan<byte> bytes)
        {
            var entities = new List<Entity>();
            for (int i = 0; ; i++)
            {
                int start = sizeof(uint) + Sizes.FhEntityEntry * i;
                int end = start + Sizes.EntityEntry;
                FhEntityEntry entry = ReadStruct<FhEntityEntry>(bytes[start..end]);
                if (entry.DataOffset == 0)
                {
                    break;
                }
                entities.Add(ReadFirstHuntEntity(bytes, entry));
            }
            return entities;
        }

        private static Entity ReadFirstHuntEntity(ReadOnlySpan<byte> bytes, FhEntityEntry entry)
        {
            int start = (int)entry.DataOffset;
            int end = start + Sizes.EntityDataHeader;
            EntityDataHeader header = ReadStruct<EntityDataHeader>(bytes[start..end]);
            var type = (EntityType)(header.Type + 100);
            return type switch
            {
                EntityType.FhPlayerSpawn => ReadFirstHuntEntity<FhPlayerSpawnEntityData>(bytes, entry, header),
                EntityType.FhDoor => ReadFirstHuntEntity<FhDoorEntityData>(bytes, entry, header),
                EntityType.FhItem => ReadFirstHuntEntity<FhItemEntityData>(bytes, entry, header),
                EntityType.FhEnemy => ReadFirstHuntEntity<FhEnemyEntityData>(bytes, entry, header),
                EntityType.FhUnknown9 => ReadFirstHuntEntity<FhUnknown9EntityData>(bytes, entry, header),
                EntityType.FhUnknown10 => ReadFirstHuntEntity<FhUnknown10EntityData>(bytes, entry, header),
                EntityType.FhPlatform => ReadFirstHuntEntity<FhPlatformEntityData>(bytes, entry, header),
                EntityType.FhJumpPad => ReadFirstHuntEntity<FhJumpPadEntityData>(bytes, entry, header),
                EntityType.FhPointModule => ReadFirstHuntEntity<FhPointModuleEntityData>(bytes, entry, header),
                EntityType.FhCameraPosition => ReadFirstHuntEntity<FhCameraPositionEntityData>(bytes, entry, header),
                _ => throw new ProgramException($"Invalid entity type {type}")
            };
        }

        private static Entity<T> ReadFirstHuntEntity<T>(ReadOnlySpan<byte> bytes, FhEntityEntry entry, EntityDataHeader header)
            where T : struct
        {
            int start = (int)entry.DataOffset;
            int end = start + Marshal.SizeOf<T>();
            return new Entity<T>(entry, (EntityType)(header.Type + 100), header.EntityId, ReadStruct<T>(bytes[start..end]));
        }

        private static void Nop() { }

        private static IReadOnlyList<RenderInstruction> DoRenderInstructions(ReadOnlySpan<byte> bytes, DisplayList dlist)
        {
            if (dlist.Size % 4 != 0)
            {
                throw new ProgramException($"Dlist size {dlist.Size} not divisible by 4.");
            }
            var list = new List<RenderInstruction>();
            int pointer = (int)dlist.Offset;
            int endPointer = pointer + (int)dlist.Size;
            if (endPointer >= bytes.Length)
            {
                throw new ProgramException($"End pointer size {endPointer} too long for dlist size {dlist.Size}.");
            }
            while (pointer < endPointer)
            {
                uint packedInstructions = SpanReadUint(bytes, ref pointer);
                for (int i = 0; i < 4; i++)
                {
                    var instruction = (InstructionCode)(((packedInstructions & 0xFF) << 2) + 0x400);
                    int arity = RenderInstruction.GetArity(instruction);
                    var arguments = new List<uint>();
                    for (int j = 0; j < arity; j++)
                    {
                        arguments.Add(SpanReadUint(bytes, ref pointer));
                    }
                    list.Add(new RenderInstruction(instruction, arguments.ToArray()));
                    packedInstructions >>= 8;
                }
            }
            return list;
        }

        private static uint SpanReadUint(ReadOnlySpan<byte> bytes, ref int offset)
        {
            uint result = MemoryMarshal.Read<uint>(bytes[offset..(offset + sizeof(uint))]);
            offset += sizeof(uint);
            return result;
        }

        private static ushort SpanReadUshort(ReadOnlySpan<byte> bytes, ref int offset)
        {
            ushort result = MemoryMarshal.Read<ushort>(bytes[offset..(offset + sizeof(ushort))]);
            offset += sizeof(ushort);
            return result;
        }

        private static uint SpanReadUint(ReadOnlySpan<byte> bytes, int offset)
        {
            return SpanReadUint(bytes, ref offset);
        }

        private static ushort SpanReadUshort(ReadOnlySpan<byte> bytes, int offset)
        {
            return SpanReadUshort(bytes, ref offset);
        }

        private static string GetModelName(string path)
        {
            if (path.Contains("_mdl_"))
            {
                path = path.Replace("_mdl_", "_");
            }
            if (path.Contains("_Model.bin"))
            {
                path = path.Replace("_Model.bin", "");
            }
            else if (path.Contains("_model.bin"))
            {
                path = path.Replace("_model.bin", "");
            }
            return Path.GetFileNameWithoutExtension(path);
        }

        public static T DoOffset<T>(ReadOnlySpan<byte> bytes, uint offset) where T : struct
        {
            return DoOffsets<T>(bytes, offset, 1).First();
        }

        public static IReadOnlyList<T> DoOffsets<T>(ReadOnlySpan<byte> bytes, uint offset, int count) where T : struct
        {
            int ioffset = (int)offset;
            var results = new List<T>();
            if (offset != 0)
            {
                int size = Marshal.SizeOf(typeof(T));
                for (uint i = 0; i < count; i++, ioffset += size)
                {
                    results.Add(ReadStruct<T>(bytes[ioffset..(ioffset + size)]));
                }
            }
            return results;
        }

        public static T ReadStruct<T>(ReadOnlySpan<byte> bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes.ToArray(), GCHandleType.Pinned);
            object? result = Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            if (result == null)
            {
                throw new ProgramException($"Failed to read {typeof(T)} struct.");
            }
            return (T)result;
        }

        public static void ExtractArchive(string name)
        {
            string input = Path.Combine(Paths.FileSystem, "archives", $"{name}.arc");
            string output = Path.Combine(Paths.FileSystem, "_archives", name);
            try
            {
                int filesWritten = 0;
                Directory.CreateDirectory(output);
                Console.WriteLine("Reading file...");
                var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(input));
                if (Encoding.ASCII.GetString(bytes[0..8]) == Archiver.MagicString)
                {
                    Console.WriteLine("Extracting archive...");
                    filesWritten = Archiver.Extract(input, output);
                }
                else if (bytes[0] == Lz.MagicByte)
                {
                    string temp = Path.Combine(Paths.Export, "__temp");
                    try
                    {
                        Directory.Delete(temp, recursive: true);
                    }
                    catch { }
                    Directory.CreateDirectory(temp);
                    string destination = Path.Combine(temp, $"{name}.arc");
                    Console.WriteLine("Decompressing...");
                    Lz.Decompress(input, destination);
                    Console.WriteLine("Extracting archive...");
                    filesWritten = Archiver.Extract(destination, output);
                    Directory.Delete(temp, recursive: true);
                }
                Console.WriteLine($"Extracted {filesWritten} file{(filesWritten == 1 ? "" : "s")} to {output}.");
            }
            catch
            {
                Console.WriteLine($"Failed to extract archive. Verify an archive exists at {input}.");
            }
        }

        public static void ReadAndExport(string name)
        {
            // todo: need non-throwing versions of these
            Model model;
            try
            {
                model = GetModelByName(name);
            }
            catch
            {
                try
                {
                    model = GetRoomByName(name);
                }
                catch
                {
                    Console.WriteLine($"No model or room with the name {name} could be found.");
                    return;
                }
            }
            try
            {
                Images.ExportImages(model);
                Collada.ExportModel(model);
                Console.WriteLine("Exported successfully.");
            }
            catch
            {
                Console.WriteLine("Failed to export model. Verify your export path is accessible.");
            }
        }

        private static void DumpEntityList(IEnumerable<Entity> entities)
        {
            foreach (EntityType type in entities.Select(e => e.Type).Distinct())
            {
                int count = entities.Count(e => e.Type == type);
                Console.WriteLine($"{count}x {type}");
            }
            Console.WriteLine();
            foreach (Entity entity in entities)
            {
                Console.WriteLine(entity.Type);
            }
        }
    }
}
