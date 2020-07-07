using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead
{
    public static class Read
    {
        // NOTE: When _Texture file exists, the main _Model file header will list a non-zero number of textures/palettes,
        // but the texture/palette offset will be 0 (because they're located at the start of the _Texture file).
        // However, when recolor files are used (e.g. _pal01 or flagbase_ctf_mdl -> flagbase_ctf_green_img), the number
        // of textures/palettes will be zero as well. To get the real information, the _Model file for the recolor must
        // be used in addition to the main header. And after doing that, you might then still be dealing with a _Texture file.

        public static Model GetModelByName(string name, int defaultRecolor = 0)
        {
            EntityMetadata? entityMeta = Metadata.GetEntityByName(name);
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
            return GetModel("model", path, null, recolors, 0);
        }

        public static Model GetRoomByName(string name)
        {
            RoomMetadata? roomMeta = Metadata.GetRoomByName(name);
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
            Model room = GetModel(meta.Name, meta.ModelPath, meta.AnimationPath, recolors, defaultRecolor: 0);
            room.Animate = (meta.AnimationPath != null);
            return room;
        }

        private static Model GetModel(EntityMetadata meta, int defaultRecolor)
        {
            Model model = GetModel(meta.Name, meta.ModelPath, meta.AnimationPath, meta.Recolors, defaultRecolor);
            model.Animate = (meta.AnimationPath != null);
            return model;
        }

        public static Model GetModelDirect(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", path)
            };
            return GetModel(name, path, null, recolors, defaultRecolor: 0);
        }

        public static Header GetHeader(string path)
        {
            path = Path.Combine(Paths.FileSystem, path);
            ReadOnlySpan<byte> bytes = ReadBytes(path);
            return ReadStruct<Header>(bytes[0..Sizes.Header]);
        }

        private static Model GetModel(string name, string modelPath, string? animationPath,
            IReadOnlyList<RecolorMetadata> recolorMeta, int defaultRecolor)
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
                if (meta.PalettePath != meta.TexturePath)
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
                recolors.Add(new Recolor(meta.Name, textures, palettes, textureData, paletteData));
            }
            AnimationResults animations = LoadAnimation(animationPath);
            var model = new Model(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                textureMatrices, recolors, defaultRecolor);
            foreach (TexcoordAnimationGroup group in model.TexcoordAnimationGroups)
            {
                group.CurrentFrame = 0;
                foreach (Material material in model.Materials)
                {
                    material.TexcoordAnimationId = -1;
                    for (int i = 0; i < group.Animations.Count; i++)
                    {
                        if (group.Animations[i].Name == material.Name)
                        {
                            // todo: currently overwriting things so the last group's information is always used
                            material.TexcoordAnimationId = i;
                        }
                    }
                }
            }
            return model;
        }

        private class AnimationResults
        {
            public List<NodeAnimationGroup> NodeAnimationGroups { get; } = new List<NodeAnimationGroup>();
            public List<MaterialAnimationGroup> MaterialAnimationGroups { get; } = new List<MaterialAnimationGroup>();
            public List<TexcoordAnimationGroup> TexcoordAnimationGroups { get; } = new List<TexcoordAnimationGroup>();
            public List<TextureAnimationGroup> TextureAnimationGroups { get; } = new List<TextureAnimationGroup>();
        }

        // todo: parse the rest of the animation types
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
                results.NodeAnimationGroups.Add(DoOffset<NodeAnimationGroup>(bytes, offset));
            }
            foreach (uint offset in materialGroupOffsets)
            {
                if (offset == 0)
                {
                    continue;
                }
                results.MaterialAnimationGroups.Add(DoOffset<MaterialAnimationGroup>(bytes, offset));
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
                IReadOnlyList<RawTexcoordAnimation> rawAnimations
                    = DoOffsets<RawTexcoordAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                var animations = new List<TexcoordAnimation>();
                foreach (RawTexcoordAnimation rawAnimation in rawAnimations)
                {
                    var animation = new TexcoordAnimation(rawAnimation);
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexS + animation.ScaleLutLengthS);
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexT + animation.ScaleLutLengthT);
                    maxRotation = Math.Max(maxRotation, animation.RotateLutIndexZ + animation.RotateLutLengthZ);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexS + animation.TranslateLutLengthS);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexT + animation.TranslateLutLengthT);
                    animations.Add(animation);
                }
                var scales = DoOffsets<Fixed>(bytes, rawGroup.ScaleLutOffset, maxScale).Select(f => f.FloatValue).ToList();
                var rotations = new List<float>();
                foreach (ushort value in DoOffsets<ushort>(bytes, rawGroup.RotateLutOffset, maxRotation))
                {
                    int radians = (0x6487F * value + 0x80000) >> 20;
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
                results.TextureAnimationGroups.Add(DoOffset<TextureAnimationGroup>(bytes, offset));
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
            if (texture.Format == TextureFormat.DirectRgb || texture.Format == TextureFormat.DirectRgba)
            {
                for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                {
                    ushort color = SpanReadUshort(textureBytes, (int)(texture.ImageOffset + pixelIndex * 2));
                    byte alpha = texture.Format == TextureFormat.DirectRgb ? (byte)255 : AlphaFromShort(color);
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

        private static byte AlphaFromShort(ushort value) => (value & 0x8000) == 0 ? (byte)255 : (byte)0;

        private static byte AlphaFromA5I3(byte value) => (byte)((value >> 3) / 31.0f * 255.0f);

        private static byte AlphaFromA3I5(byte value) => (byte)((value >> 5) / 7.0f * 255.0f);

        public static IReadOnlyList<Entity> GetEntities(string path, int layerId)
        {
            path = Path.Combine(Paths.FileSystem, path);
            ReadOnlySpan<byte> bytes = ReadBytes(path);
            EntityHeader header = ReadStruct<EntityHeader>(bytes[0..Sizes.EntityHeader]);
            if (header.Version != 2)
            {
                throw new ProgramException($"Unexpected entity header version {header.Version}.");
            }
            var entities = new List<Entity>();
            for (int i = 0; entities.Count < header.Lengths[layerId]; i++)
            {
                int start = Sizes.EntityHeader + Sizes.EntityEntry * i;
                int end = start + Sizes.EntityEntry;
                EntityEntry entry = ReadStruct<EntityEntry>(bytes[start..end]);
                if ((entry.LayerMask & (1 << layerId)) != 0)
                {
                    start = (int)entry.DataOffset;
                    end = start + Sizes.EntityDataHeader;
                    EntityDataHeader init = ReadStruct<EntityDataHeader>(bytes[start..end]);
                    var type = (EntityType)init.Type;
                    end = start + entry.Length;
                    if (type == EntityType.Platform)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<PlatformEntityData>());
                        entities.Add(new Entity<PlatformEntityData>(entry, type, init.SomeId,
                            ReadStruct<PlatformEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Object)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<ObjectEntityData>());
                        entities.Add(new Entity<ObjectEntityData>(entry, type, init.SomeId,
                            ReadStruct<ObjectEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.PlayerSpawn)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<PlayerSpawnEntityData>());
                        entities.Add(new Entity<PlayerSpawnEntityData>(entry, type, init.SomeId,
                            ReadStruct<PlayerSpawnEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Door)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<DoorEntityData>());
                        entities.Add(new Entity<DoorEntityData>(entry, type, init.SomeId,
                            ReadStruct<DoorEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Item)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<ItemEntityData>());
                        entities.Add(new Entity<ItemEntityData>(entry, type, init.SomeId,
                            ReadStruct<ItemEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Enemy)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<EnemyEntityData>());
                        entities.Add(new Entity<EnemyEntityData>(entry, type, init.SomeId,
                            ReadStruct<EnemyEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Unknown7)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<Unknown7EntityData>());
                        entities.Add(new Entity<Unknown7EntityData>(entry, type, init.SomeId,
                            ReadStruct<Unknown7EntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Unknown8)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<Unknown8EntityData>());
                        entities.Add(new Entity<Unknown8EntityData>(entry, type, init.SomeId,
                            ReadStruct<Unknown8EntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.JumpPad)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<JumpPadEntityData>());
                        entities.Add(new Entity<JumpPadEntityData>(entry, type, init.SomeId,
                            ReadStruct<JumpPadEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.CameraPos)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<CameraPosEntityData>());
                        entities.Add(new Entity<CameraPosEntityData>(entry, type, init.SomeId,
                            ReadStruct<CameraPosEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Unknown12)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<Unknown12EntityData>());
                        entities.Add(new Entity<Unknown12EntityData>(entry, type, init.SomeId,
                            ReadStruct<Unknown12EntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Unknown13)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<Unknown13EntityData>());
                        entities.Add(new Entity<Unknown13EntityData>(entry, type, init.SomeId,
                            ReadStruct<Unknown13EntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Teleporter)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<TeleporterEntityData>());
                        entities.Add(new Entity<TeleporterEntityData>(entry, type, init.SomeId,
                            ReadStruct<TeleporterEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Unknown15)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<Unknown15EntityData>());
                        entities.Add(new Entity<Unknown15EntityData>(entry, type, init.SomeId,
                            ReadStruct<Unknown15EntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Unknown16)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<Unknown16EntityData>());
                        entities.Add(new Entity<Unknown16EntityData>(entry, type, init.SomeId,
                            ReadStruct<Unknown16EntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.Artifact)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<ArtifactEntityData>());
                        entities.Add(new Entity<ArtifactEntityData>(entry, type, init.SomeId,
                            ReadStruct<ArtifactEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.CameraSeq)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<CameraSeqEntityData>());
                        entities.Add(new Entity<CameraSeqEntityData>(entry, type, init.SomeId,
                            ReadStruct<CameraSeqEntityData>(bytes[start..end])));
                    }
                    else if (type == EntityType.ForceField)
                    {
                        Debug.Assert(entry.Length == Marshal.SizeOf<ForceFieldEntityData>());
                        entities.Add(new Entity<ForceFieldEntityData>(entry, type, init.SomeId,
                            ReadStruct<ForceFieldEntityData>(bytes[start..end])));
                    }
                    else
                    {
                        throw new ProgramException($"Invalid entity type {type}");
                    }
                }
            }
            return entities;
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
    }
}
