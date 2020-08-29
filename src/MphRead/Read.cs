using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MphRead.Archive;
using MphRead.Export;
using OpenToolkit.Mathematics;

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
            // note: in RAM, model texture matrices are 4x4, but only the leftmost 4x2 or 4x3 is set,
            // and the rest is garbage data, and ultimately only the upper-left 3x2 is actually used
            var textureMatrices = new List<Matrix4>();
            if (name == "AlimbicCapsule")
            {
                Debug.Assert(header.TextureMatrixCount == 1);
                Matrix4 textureMatrix = Matrix4.Zero;
                textureMatrix.M21 = Fixed.ToFloat(-2048);
                textureMatrix.M31 = Fixed.ToFloat(410);
                textureMatrix.M32 = Fixed.ToFloat(-3891);
                textureMatrices.Add(textureMatrix);
            }
            //  todo: when the counts/offsets are zero, these values should probably be skipped, even though they are always present
            int count = (int)header.UnknownAnimationCount - Sizes.Header;
            Debug.Assert(count >= sizeof(uint) && count % sizeof(uint) == 0);
            count /= 4;
            IReadOnlyList<uint> nodeIds = DoOffsets<uint>(initialBytes, (uint)Sizes.Header, count);
            IReadOnlyList<uint> weightIds = DoOffsets<uint>(initialBytes, header.UnknownAnimationCount, count);
            AnimationResults animations = LoadAnimation(animationPath);
            return new Model(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                textureMatrices, recolors, defaultRecolor, useLightSources, nodeIds, weightIds);
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
            if (palette.Size % 2 != 0)
            {
                throw new ProgramException($"Palette size {palette.Size} is not divisible by 2.");
            }
            var data = new List<PaletteData>();
            for (int i = 0; i < palette.Size / 2; i++)
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
            if (version != 2)
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

        // todo: should return a CameraSequence class (flags etc.)
        public static IReadOnlyList<CameraSequenceFrame> ReadCameraSequence(string name)
        {
            name = Path.Combine(Paths.FileSystem, $@"cameraEditor\{name}.bin");
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(name));
            if (bytes.Length < Sizes.CameraSequenceHeader)
            {
                throw new ProgramException("Invalid camera sequence file format.");
            }
            CameraSequenceHeader header = ReadStruct<CameraSequenceHeader>(bytes);
            int length = Sizes.CameraSequenceHeader + Sizes.CameraSequenceFrame * header.Count;
            Debug.Assert(bytes.Length == length);
            if (bytes.Length < length)
            {
                throw new ProgramException("Invalid camera sequence file format.");
            }
            uint offset = (uint)Sizes.CameraSequenceHeader;
            IReadOnlyList<CameraSequenceFrame> frames = DoOffsets<CameraSequenceFrame>(bytes, offset, header.Count);
            return frames;
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
                throw new ProgramException($"End pointer {endPointer} too large for dlist size {bytes.Length}.");
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
