using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MphRead.Archive;
using MphRead.Entities;
using MphRead.Export;
using MphRead.Models;
using OpenTK.Mathematics;

namespace MphRead
{
    public static class Read
    {
        private static readonly Dictionary<string, NewModel> _modelCache = new Dictionary<string, NewModel>();
        private static readonly Dictionary<string, NewModel> _fhModelCache = new Dictionary<string, NewModel>();

        public static ModelInstance GetNewModel(string name)
        {
            if (!_modelCache.TryGetValue(name, out NewModel? model))
            {
                model = GetNewModel(name, firstHunt: false);
                if (model == null)
                {
                    throw new ProgramException("No model with this name is known.");
                }
                _modelCache.Add(name, model);
            }
            return new ModelInstance(model);
        }

        public static ModelInstance GetFhNewModel(string name)
        {
            if (!_fhModelCache.TryGetValue(name, out NewModel? model))
            {
                model = GetNewModel(name, firstHunt: true);
                if (model == null)
                {
                    throw new ProgramException("No model with this name is known.");
                }
                _fhModelCache.Add(name, model);
            }
            return new ModelInstance(model);
        }

        private static NewModel? GetNewModel(string name, bool firstHunt)
        {
            ModelMetadata? meta;
            if (firstHunt)
            {
                meta = Metadata.GetFirstHuntModelByName(name);
            }
            else
            {
                meta = Metadata.GetModelByName(name);
            }
            if (meta == null)
            {
                return null;
            }
            return GetNewModel(meta.Name, meta.ModelPath, meta.AnimationPath, meta.AnimationShare, meta.Recolors, meta.FirstHunt);
        }

        public static ModelInstance GetNewRoom(string name)
        {
            (RoomMetadata? meta, _) = Metadata.GetRoomByName(name);
            if (meta == null)
            {
                throw new ProgramException("No room with this name is known.");
            }
            if (!_modelCache.TryGetValue(meta.Name, out NewModel? model))
            {
                model = GetNewRoom(meta);
                if (model == null)
                {
                    throw new ProgramException("No model with this name is known.");
                }
                _modelCache.Add(name, model);
            }
            return new ModelInstance(model);
        }

        private static NewModel GetNewRoom(RoomMetadata meta)
        {
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", meta.ModelPath, meta.TexturePath ?? meta.ModelPath)
            };
            return GetNewModel(meta.Name, meta.ModelPath, meta.AnimationPath, animationShare: null, recolors,
                firstHunt: meta.FirstHunt || meta.Hybrid);
        }

        private static NewModel GetNewModel(string name, string modelPath, string? animationPath, string? animationShare,
            IReadOnlyList<RecolorMetadata> recolorMeta, bool firstHunt)
        {
            string root = firstHunt ? Paths.FhFileSystem : Paths.FileSystem;
            string path = Path.Combine(root, modelPath);
            ReadOnlySpan<byte> initialBytes = ReadBytes(path, firstHunt);
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
                if (Path.Combine(root, meta.ModelPath) != path)
                {
                    modelBytes = ReadBytes(meta.ModelPath, firstHunt);
                    modelHeader = ReadStruct<Header>(modelBytes[0..Sizes.Header]);
                }
                IReadOnlyList<Texture> textures = DoOffsets<Texture>(modelBytes, modelHeader.TextureOffset, modelHeader.TextureCount);
                IReadOnlyList<Palette> palettes = DoOffsets<Palette>(modelBytes, modelHeader.PaletteOffset, modelHeader.PaletteCount);
                ReadOnlySpan<byte> textureBytes = modelBytes;
                if (meta.TexturePath != meta.ModelPath)
                {
                    textureBytes = ReadBytes(meta.TexturePath, firstHunt);
                }
                ReadOnlySpan<byte> paletteBytes = textureBytes;
                if (meta.PalettePath != meta.TexturePath && meta.ReplaceIds.Count == 0)
                {
                    paletteBytes = ReadBytes(meta.PalettePath, firstHunt);
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
                string replacePath = meta.ReplacePath ?? meta.PalettePath;
                if (replacePath != meta.TexturePath && meta.ReplaceIds.Count > 0)
                {
                    paletteBytes = ReadBytes(replacePath, firstHunt);
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
            IReadOnlyList<int> nodeWeights = DoOffsets<int>(initialBytes, header.NodeWeightOffset, header.NodeWeightCount);
            AnimationResults animations = LoadAnimation(name, animationPath, nodes, firstHunt);
            if (animationShare != null)
            {
                AnimationResults shared = LoadAnimation(name, animationShare, nodes, firstHunt);
                shared.NodeAnimationGroups.AddRange(animations.NodeAnimationGroups);
                shared.MaterialAnimationGroups.AddRange(animations.MaterialAnimationGroups);
                shared.TexcoordAnimationGroups.AddRange(animations.TexcoordAnimationGroups);
                shared.TextureAnimationGroups.AddRange(animations.TextureAnimationGroups);
                animations = shared;
            }
            return new NewModel(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                textureMatrices, recolors, nodeWeights);
        }

        // NOTE: When _Texture file exists, the main _Model file header will list a non-zero number of textures/palettes,
        // but the texture/palette offset will be 0 (because they're located at the start of the _Texture file).
        // However, when recolor files are used (e.g. _pal01 or flagbase_ctf_mdl -> flagbase_ctf_green_img), the number
        // of textures/palettes will be zero as well. To get the real information, the _Model file for the recolor must
        // be used in addition to the main header. And after doing that, you might then still be dealing with a _Texture file.

        public static Model GetModelByName(string name, int defaultRecolor = 0, bool firstHunt = false)
        {
            return GetModelByName<Model>(name, defaultRecolor, firstHunt);
        }

        public static T GetModelByName<T>(string name, int defaultRecolor = 0, bool firstHunt = false) where T : Model
        {
            ModelMetadata? modelMeta;
            if (firstHunt)
            {
                modelMeta = Metadata.GetFirstHuntModelByName(name);
            }
            else
            {
                modelMeta = Metadata.GetModelByName(name);
            }
            if (modelMeta == null)
            {
                throw new ProgramException("No model with this name is known.");
            }
            return GetModel<T>(modelMeta, defaultRecolor);
        }

        public static RoomModel GetRoomByName(string name)
        {
            (RoomMetadata? roomMeta, _) = Metadata.GetRoomByName(name);
            if (roomMeta == null)
            {
                throw new ProgramException("No room with this name is known.");
            }
            return GetRoom(roomMeta);
        }

        public static RoomModel GetRoomById(int id)
        {
            RoomMetadata? roomMeta = Metadata.GetRoomById(id);
            if (roomMeta == null)
            {
                throw new ProgramException("No room with this ID is known.");
            }
            return GetRoom(roomMeta);
        }

        private static RoomModel GetRoom(RoomMetadata meta)
        {
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", meta.ModelPath, meta.TexturePath ?? meta.ModelPath)
            };
            return GetModel<RoomModel>(meta.Name, meta.ModelPath, meta.AnimationPath, animationShare: null,
                recolors, defaultRecolor: 0, useLightSources: false, firstHunt: meta.FirstHunt || meta.Hybrid);
        }

        private static T GetModel<T>(ModelMetadata meta, int defaultRecolor) where T : Model
        {
            return GetModel<T>(meta.Name, meta.ModelPath, meta.AnimationPath, meta.AnimationShare, meta.Recolors,
                defaultRecolor, meta.UseLightSources, firstHunt: meta.FirstHunt);
        }

        private static T GetModel<T>(string name, string modelPath, string? animationPath, string? animationShare,
            IReadOnlyList<RecolorMetadata> recolorMeta, int defaultRecolor, bool useLightSources, bool firstHunt) where T : Model
        {
            if (defaultRecolor < 0 || defaultRecolor > recolorMeta.Count - 1)
            {
                throw new ProgramException("The specified recolor index is invalid for this entity.");
            }
            string root = firstHunt ? Paths.FhFileSystem : Paths.FileSystem;
            string path = Path.Combine(root, modelPath);
            ReadOnlySpan<byte> initialBytes = ReadBytes(path, firstHunt);
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
                if (Path.Combine(root, meta.ModelPath) != path)
                {
                    modelBytes = ReadBytes(meta.ModelPath, firstHunt);
                    modelHeader = ReadStruct<Header>(modelBytes[0..Sizes.Header]);
                }
                IReadOnlyList<Texture> textures = DoOffsets<Texture>(modelBytes, modelHeader.TextureOffset, modelHeader.TextureCount);
                IReadOnlyList<Palette> palettes = DoOffsets<Palette>(modelBytes, modelHeader.PaletteOffset, modelHeader.PaletteCount);
                ReadOnlySpan<byte> textureBytes = modelBytes;
                if (meta.TexturePath != meta.ModelPath)
                {
                    textureBytes = ReadBytes(meta.TexturePath, firstHunt);
                }
                ReadOnlySpan<byte> paletteBytes = textureBytes;
                if (meta.PalettePath != meta.TexturePath && meta.ReplaceIds.Count == 0)
                {
                    paletteBytes = ReadBytes(meta.PalettePath, firstHunt);
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
                string replacePath = meta.ReplacePath ?? meta.PalettePath;
                if (replacePath != meta.TexturePath && meta.ReplaceIds.Count > 0)
                {
                    paletteBytes = ReadBytes(replacePath, firstHunt);
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
            IReadOnlyList<int> nodeWeights = DoOffsets<int>(initialBytes, header.NodeWeightOffset, header.NodeWeightCount);
            AnimationResults animations = LoadAnimation(name, animationPath, nodes, firstHunt);
            if (animationShare != null)
            {
                AnimationResults shared = LoadAnimation(name, animationShare, nodes, firstHunt);
                shared.NodeAnimationGroups.AddRange(animations.NodeAnimationGroups);
                shared.MaterialAnimationGroups.AddRange(animations.MaterialAnimationGroups);
                shared.TexcoordAnimationGroups.AddRange(animations.TexcoordAnimationGroups);
                shared.TextureAnimationGroups.AddRange(animations.TextureAnimationGroups);
                animations = shared;
            }
            if (typeof(T) == typeof(RoomModel))
            {
                return (new RoomModel(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                    animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                    textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights) as T)!;
            }
            if (typeof(T) == typeof(PlatformModel))
            {
                return (new PlatformModel(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                    animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                    textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights) as T)!;
            }
            if (typeof(T) == typeof(ObjectModel))
            {
                return (new ObjectModel(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                    animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                    textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights) as T)!;
            }
            if (typeof(T) == typeof(ForceFieldLockModel))
            {
                return (new ForceFieldLockModel(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                    animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                    textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights) as T)!;
            }
            return (new Model(name, header, nodes, meshes, materials, dlists, instructions, animations.NodeAnimationGroups,
                animations.MaterialAnimationGroups, animations.TexcoordAnimationGroups, animations.TextureAnimationGroups,
                textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights) as T)!;
        }

        private class AnimationResults
        {
            public List<NodeAnimationGroup> NodeAnimationGroups { get; } = new List<NodeAnimationGroup>();
            public List<MaterialAnimationGroup> MaterialAnimationGroups { get; } = new List<MaterialAnimationGroup>();
            public List<TexcoordAnimationGroup> TexcoordAnimationGroups { get; } = new List<TexcoordAnimationGroup>();
            public List<TextureAnimationGroup> TextureAnimationGroups { get; } = new List<TextureAnimationGroup>();
        }

        private static AnimationResults LoadAnimation(string model, string? path, IReadOnlyList<RawNode> nodes, bool firstHunt)
        {
            var results = new AnimationResults();
            if (path == null)
            {
                return results;
            }
            path = Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, path);
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
                int maxScale = 0;
                int maxRotation = 0;
                int maxTranslation = 0;
                RawNodeAnimationGroup rawGroup = DoOffset<RawNodeAnimationGroup>(bytes, offset);
                Debug.Assert(offset > rawGroup.AnimationOffset);
                Debug.Assert((offset - rawGroup.AnimationOffset) % Sizes.NodeAnimation == 0);
                int count = nodes.Count;
                // this group has one less animation than the model has nodes
                // todo: figure out if this is necessary
                if (model == "GuardBot1" && offset == 2300)
                {
                    count--;
                }
                IReadOnlyList<NodeAnimation> rawAnimations
                    = DoOffsets<NodeAnimation>(bytes, rawGroup.AnimationOffset, count);
                var animations = new Dictionary<string, NodeAnimation>();
                int i = 0;
                foreach (NodeAnimation animation in rawAnimations)
                {
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexX + animation.ScaleLutLengthX);
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexY + animation.ScaleLutLengthY);
                    maxScale = Math.Max(maxScale, animation.ScaleLutIndexZ + animation.ScaleLutLengthZ);
                    maxRotation = Math.Max(maxRotation, animation.RotateLutIndexX + animation.RotateLutLengthX);
                    maxRotation = Math.Max(maxRotation, animation.RotateLutIndexY + animation.RotateLutLengthY);
                    maxRotation = Math.Max(maxRotation, animation.RotateLutIndexZ + animation.RotateLutLengthZ);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexX + animation.TranslateLutLengthX);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexY + animation.TranslateLutLengthY);
                    maxTranslation = Math.Max(maxTranslation, animation.TranslateLutIndexZ + animation.TranslateLutLengthZ);
                    animations.Add(nodes[i++].Name.MarshalString(), animation);
                }
                var scales = DoOffsets<Fixed>(bytes, rawGroup.ScaleLutOffset, maxScale).Select(f => f.FloatValue).ToList();
                var rotations = new List<float>();
                foreach (ushort value in DoOffsets<ushort>(bytes, rawGroup.RotateLutOffset, maxRotation))
                {
                    long radians = (0x6487FL * value + 0x80000) >> 20;
                    rotations.Add(Fixed.ToFloat(radians));
                }
                var translations = DoOffsets<Fixed>(bytes, rawGroup.TranslateLutOffset, maxTranslation).Select(f => f.FloatValue).ToList();
                results.NodeAnimationGroups.Add(new NodeAnimationGroup(rawGroup, scales, rotations, translations, animations));
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
                    maxColor = Math.Max(maxColor, animation.DiffuseLutIndexR + animation.DiffuseLutLengthR);
                    maxColor = Math.Max(maxColor, animation.DiffuseLutIndexG + animation.DiffuseLutLengthG);
                    maxColor = Math.Max(maxColor, animation.DiffuseLutIndexB + animation.DiffuseLutLengthB);
                    maxColor = Math.Max(maxColor, animation.AmbientLutIndexR + animation.AmbientLutLengthR);
                    maxColor = Math.Max(maxColor, animation.AmbientLutIndexG + animation.AmbientLutLengthG);
                    maxColor = Math.Max(maxColor, animation.AmbientLutIndexB + animation.AmbientLutLengthB);
                    maxColor = Math.Max(maxColor, animation.SpecularLutIndexR + animation.SpecularLutLengthR);
                    maxColor = Math.Max(maxColor, animation.SpecularLutIndexG + animation.SpecularLutLengthG);
                    maxColor = Math.Max(maxColor, animation.SpecularLutIndexB + animation.SpecularLutLengthB);
                    maxColor = Math.Max(maxColor, animation.AlphaLutIndex + animation.AlphaLutLength);
                    animations.Add(animation.Name.MarshalString(), animation);
                }
                // the lambert3 LUT has one too few values for the diffuse blue channel's animation, so we need to read an extra byte
                if (model == "NoxGun" && offset == 48688)
                {
                    maxColor++;
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
                    animations.Add(animation.Name.MarshalString(), animation);
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
                    animations.Add(animation.Name.MarshalString(), animation);
                }
                IReadOnlyList<ushort> frameIndices = DoOffsets<ushort>(bytes, rawGroup.FrameIndexOffset, rawGroup.FrameIndexCount);
                IReadOnlyList<ushort> textureIds = DoOffsets<ushort>(bytes, rawGroup.TextureIdOffset, rawGroup.TextureIdCount);
                IReadOnlyList<ushort> paletteIds = DoOffsets<ushort>(bytes, rawGroup.PaletteIdOffset, rawGroup.PaletteIdCount);
                results.TextureAnimationGroups.Add(new TextureAnimationGroup(rawGroup, frameIndices, textureIds, paletteIds, animations));
            }
            return results;
        }

        private static ReadOnlySpan<byte> ReadBytes(string path, bool firstHunt)
        {
            return new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, path)));
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

        // todo: might as well just convert to float early
        private static byte AlphaFromShort(ushort value) => (value & 0x8000) == 0 ? (byte)0 : (byte)255;

        private static byte AlphaFromA5I3(byte value) => (byte)MathF.Round((value >> 3) / 31.0f * 255.0f);

        private static byte AlphaFromA3I5(byte value) => (byte)MathF.Round((value >> 5) / 7.0f * 255.0f);

        public static IReadOnlyList<Entity> GetEntities(string path, int layerId, bool firstHunt)
        {
            path = Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, path);
            ReadOnlySpan<byte> bytes = ReadBytes(path, firstHunt);
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
                EntityType.ItemSpawn => ReadEntity<ItemEntityData>(bytes, entry, header),
                EntityType.EnemySpawn => ReadEntity<EnemyEntityData>(bytes, entry, header),
                EntityType.TriggerVolume => ReadEntity<TriggerVolumeEntityData>(bytes, entry, header),
                EntityType.AreaVolume => ReadEntity<AreaVolumeEntityData>(bytes, entry, header),
                EntityType.JumpPad => ReadEntity<JumpPadEntityData>(bytes, entry, header),
                EntityType.PointModule => ReadEntity<PointModuleEntityData>(bytes, entry, header),
                EntityType.MorphCamera => ReadEntity<MorphCameraEntityData>(bytes, entry, header),
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
            return new Entity<T>(entry, (EntityType)header.Type, header.EntityId, ReadStruct<T>(bytes[start..end]), header);
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
                EntityType.FhPlayerSpawn => ReadFirstHuntEntity<PlayerSpawnEntityData>(bytes, entry, header),
                EntityType.FhDoor => ReadFirstHuntEntity<FhDoorEntityData>(bytes, entry, header),
                EntityType.FhItem => ReadFirstHuntEntity<FhItemEntityData>(bytes, entry, header),
                EntityType.FhEnemy => ReadFirstHuntEntity<FhEnemyEntityData>(bytes, entry, header),
                EntityType.FhTriggerVolume => ReadFirstHuntEntity<FhTriggerVolumeEntityData>(bytes, entry, header),
                EntityType.FhAreaVolume => ReadFirstHuntEntity<FhAreaVolumeEntityData>(bytes, entry, header),
                EntityType.FhPlatform => ReadFirstHuntEntity<FhPlatformEntityData>(bytes, entry, header),
                EntityType.FhJumpPad => ReadFirstHuntEntity<FhJumpPadEntityData>(bytes, entry, header),
                EntityType.FhPointModule => ReadFirstHuntEntity<PointModuleEntityData>(bytes, entry, header),
                EntityType.FhMorphCamera => ReadFirstHuntEntity<FhMorphCameraEntityData>(bytes, entry, header),
                _ => throw new ProgramException($"Invalid entity type {type}")
            };
        }

        private static Entity<T> ReadFirstHuntEntity<T>(ReadOnlySpan<byte> bytes, FhEntityEntry entry, EntityDataHeader header)
            where T : struct
        {
            int start = (int)entry.DataOffset;
            int end = start + Marshal.SizeOf<T>();
            return new Entity<T>(entry, (EntityType)(header.Type + 100), header.EntityId, ReadStruct<T>(bytes[start..end]), header);
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

        public static Effect LoadEffect(int id)
        {
            if (id < 1 || id > Metadata.Effects.Count)
            {
                throw new ProgramException("Could not get particle.");
            }
            (string name, string? archive) = Metadata.Effects[id];
            return LoadEffect(name, archive);
        }

        public static Effect LoadEffect(string name, string? archive)
        {
            string path;
            if (archive == null)
            {
                path = $"effects/{name}_PS.bin";
            }
            else
            {
                path = $"_archives/{archive}/{name}_PS.bin";
            }
            Effect effect = LoadEffect(path);
            foreach (EffectElement element in effect.Elements)
            {
                if (element.ChildEffectId != 0)
                {
                    LoadEffect((int)element.ChildEffectId);
                }
            }
            return effect;
        }

        private static readonly Dictionary<string, Effect> _effects = new Dictionary<string, Effect>();
        public static Dictionary<string, Model> EffectModels { get; } = new Dictionary<string, Model>();
        private static readonly Dictionary<(string, string), Particle> _particleDefs = new Dictionary<(string, string), Particle>();

        private static readonly Dictionary<string, NewEffect> _newEffects = new Dictionary<string, NewEffect>();
        private static readonly Dictionary<(string, string), NewParticle> _newParticleDefs = new Dictionary<(string, string), NewParticle>();

        private static Effect LoadEffect(string path)
        {
            if (_effects.TryGetValue(path, out Effect? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            RawEffect effect = ReadStruct<RawEffect>(bytes);
            var funcs = new Dictionary<uint, FxFuncInfo>();
            foreach (uint offset in DoOffsets<uint>(bytes, effect.FuncOffset, effect.FuncCount))
            {
                uint funcId = SpanReadUint(bytes, offset);
                uint paramOffset = SpanReadUint(bytes, offset + 4);
                DebugValidateParams(funcId, offset, paramOffset);
                uint paramCount = (offset - paramOffset) / 4;
                IReadOnlyList<int> parameters = DoOffsets<int>(bytes, paramOffset, paramCount);
                funcs.Add(offset, new FxFuncInfo(funcId, parameters));
            }
            // todo: these are also offsets into the func/param arrays; what are they used for?
            IReadOnlyList<uint> list2 = DoOffsets<uint>(bytes, effect.Offset2, effect.Count2);
            IReadOnlyList<uint> elementOffsets = DoOffsets<uint>(bytes, effect.ElementOffset, effect.ElementCount);
            var elements = new List<EffectElement>();
            foreach (uint offset in elementOffsets)
            {
                RawEffectElement element = DoOffset<RawEffectElement>(bytes, offset);
                var particles = new List<Particle>();
                foreach (uint nameOffset in DoOffsets<uint>(bytes, element.ParticleOffset, element.ParticleCount))
                {
                    // todo: move the model reference to the element instead of the particle definitions
                    particles.Add(GetParticle(element.ModelName.MarshalString(), ReadString(bytes, nameOffset, 16)));
                }
                var elemFuncs = new Dictionary<FuncAction, FxFuncInfo>();
                IReadOnlyList<uint> elemFuncMeta = DoOffsets<uint>(bytes, element.FuncOffset, 2 * element.FuncCount);
                for (int i = 0; i < elemFuncMeta.Count; i += 2)
                {
                    uint index = elemFuncMeta[i];
                    uint funcOffset = elemFuncMeta[i + 1];
                    if (funcOffset != 0)
                    {
                        // the main list always includes the offsets referenced by elements
                        elemFuncs.Add((FuncAction)index, funcs[funcOffset]);
                    }
                }
                elements.Add(new EffectElement(element, particles, funcs, elemFuncs));
            }
            var newEffect = new Effect(effect, funcs, list2, elements, path);
            _effects.Add(path, newEffect);
            return newEffect;
        }

        public static Particle GetSingleParticle(SingleType type)
        {
            if (Metadata.SingleParticles.TryGetValue(type, out (string Model, string Particle) meta))
            {
                return GetParticle(meta.Model, meta.Particle);
            }
            throw new ProgramException("Could not get single particle.");
        }

        private static Particle GetParticle(string modelName, string particleName)
        {
            if (_particleDefs.TryGetValue((modelName, particleName), out Particle? particle))
            {
                return particle;
            }
            if (!EffectModels.TryGetValue(modelName, out Model? model))
            {
                model = GetModelByName(modelName);
                EffectModels.Add(modelName, model);
            }
            Node? node = model.Nodes.FirstOrDefault(n => n.Name == particleName);
            // todo: see what the game does here; gib3/gib4 nodes are probably meant to be used for these
            if (modelName == "geo1" && particleName == "gib")
            {
                node = model.Nodes.First(n => n.Name == "gib3");
            }
            if (node != null && node.MeshCount > 0)
            {
                int materialId = model.Meshes[node.MeshId / 2].MaterialId;
                var newParticle = new Particle(particleName, model, node, materialId);
                _particleDefs.Add((modelName, particleName), newParticle);
                return newParticle;
            }
            throw new ProgramException("Could not get particle.");
        }

        public static NewEffect NewLoadEffect(int id)
        {
            if (id < 1 || id > Metadata.Effects.Count)
            {
                throw new ProgramException("Could not get particle.");
            }
            (string name, string? archive) = Metadata.Effects[id];
            return NewLoadEffect(name, archive);
        }

        public static NewEffect NewLoadEffect(string name, string? archive)
        {
            string path;
            if (archive == null)
            {
                path = $"effects/{name}_PS.bin";
            }
            else
            {
                path = $"_archives/{archive}/{name}_PS.bin";
            }
            NewEffect effect = NewLoadEffect(path);
            foreach (NewEffectElement element in effect.Elements)
            {
                if (element.ChildEffectId != 0)
                {
                    NewLoadEffect((int)element.ChildEffectId);
                }
            }
            return effect;
        }

        private static NewEffect NewLoadEffect(string path)
        {
            if (_newEffects.TryGetValue(path, out NewEffect? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            RawEffect effect = ReadStruct<RawEffect>(bytes);
            var funcs = new Dictionary<uint, FxFuncInfo>();
            foreach (uint offset in DoOffsets<uint>(bytes, effect.FuncOffset, effect.FuncCount))
            {
                uint funcId = SpanReadUint(bytes, offset);
                uint paramOffset = SpanReadUint(bytes, offset + 4);
                DebugValidateParams(funcId, offset, paramOffset);
                uint paramCount = (offset - paramOffset) / 4;
                IReadOnlyList<int> parameters = DoOffsets<int>(bytes, paramOffset, paramCount);
                funcs.Add(offset, new FxFuncInfo(funcId, parameters));
            }
            // todo: these are also offsets into the func/param arrays; what are they used for?
            IReadOnlyList<uint> list2 = DoOffsets<uint>(bytes, effect.Offset2, effect.Count2);
            IReadOnlyList<uint> elementOffsets = DoOffsets<uint>(bytes, effect.ElementOffset, effect.ElementCount);
            var elements = new List<NewEffectElement>();
            foreach (uint offset in elementOffsets)
            {
                RawEffectElement element = DoOffset<RawEffectElement>(bytes, offset);
                var particles = new List<NewParticle>();
                foreach (uint nameOffset in DoOffsets<uint>(bytes, element.ParticleOffset, element.ParticleCount))
                {
                    // todo: move the model reference to the element instead of the particle definitions
                    particles.Add(NewGetParticle(element.ModelName.MarshalString(), ReadString(bytes, nameOffset, 16)));
                }
                var elemFuncs = new Dictionary<FuncAction, FxFuncInfo>();
                IReadOnlyList<uint> elemFuncMeta = DoOffsets<uint>(bytes, element.FuncOffset, 2 * element.FuncCount);
                for (int i = 0; i < elemFuncMeta.Count; i += 2)
                {
                    uint index = elemFuncMeta[i];
                    uint funcOffset = elemFuncMeta[i + 1];
                    if (funcOffset != 0)
                    {
                        // the main list always includes the offsets referenced by elements
                        elemFuncs.Add((FuncAction)index, funcs[funcOffset]);
                    }
                }
                elements.Add(new NewEffectElement(element, particles, funcs, elemFuncs));
            }
            var newEffect = new NewEffect(effect, funcs, list2, elements, path);
            _newEffects.Add(path, newEffect);
            return newEffect;
        }

        public static NewParticle NewGetSingleParticle(SingleType type)
        {
            if (Metadata.SingleParticles.TryGetValue(type, out (string Model, string Particle) meta))
            {
                return NewGetParticle(meta.Model, meta.Particle);
            }
            throw new ProgramException("Could not get single particle.");
        }

        private static NewParticle NewGetParticle(string modelName, string particleName)
        {
            if (_newParticleDefs.TryGetValue((modelName, particleName), out NewParticle? particle))
            {
                return particle;
            }
            ModelInstance inst = GetNewModel(modelName);
            NewModel model = inst.Model;
            Node? node = model.Nodes.FirstOrDefault(n => n.Name == particleName);
            // todo: see what the game does here; gib3/gib4 nodes are probably meant to be used for these
            if (modelName == "geo1" && particleName == "gib")
            {
                node = model.Nodes.First(n => n.Name == "gib3");
            }
            if (node != null && node.MeshCount > 0)
            {
                int materialId = model.Meshes[node.MeshId / 2].MaterialId;
                var newParticle = new NewParticle(particleName, inst.Model, node, materialId);
                _newParticleDefs.Add((modelName, particleName), newParticle);
                return newParticle;
            }
            throw new ProgramException("Could not get particle.");
        }

        [Conditional("DEBUG")]
        private static void DebugValidateParams(uint funcId, uint funcOffset, uint paramOffset)
        {
            Debug.Assert(paramOffset == 0 || paramOffset < funcOffset);
            uint paramCount = 0;
            if (paramOffset != 0)
            {
                paramCount = (funcOffset - paramOffset) / 4;
            }
            if (funcId == 1 || funcId == 5 || funcId == 8 || funcId == 9 || funcId == 11 || funcId == 22 || funcId == 23
                || funcId == 24 || funcId == 25 || funcId == 26 || funcId == 29 || funcId == 31 || funcId == 32 || funcId == 35
                || funcId == 43 || funcId == 44 || funcId == 45)
            {
                Debug.Assert(paramCount == 0);
            }
            else if (funcId == 39 || funcId == 42)
            {
                Debug.Assert(paramCount == 1);
            }
            else if (funcId == 13 || funcId == 14 || funcId == 15 || funcId == 16 || funcId == 17 || funcId == 19 || funcId == 20
                || funcId == 46 || funcId == 47 || funcId == 48)
            {
                Debug.Assert(paramCount == 2);
            }
            else if (funcId == 4 || funcId == 40)
            {
                Debug.Assert(paramCount == 3);
            }
            else if (funcId == 49)
            {
                Debug.Assert(paramCount == 4);
            }
            else if (funcId == 41)
            {
                Debug.Assert(paramCount >= 4);
            }
            else
            {
                Debug.Assert(false, funcId.ToString());
            }
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

        private static int SpanReadInt(ReadOnlySpan<byte> bytes, ref int offset)
        {
            int result = MemoryMarshal.Read<int>(bytes[offset..(offset + sizeof(int))]);
            offset += sizeof(int);
            return result;
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

        public static int SpanReadInt(ReadOnlySpan<byte> bytes, uint offset)
        {
            return SpanReadInt(bytes, (int)offset);
        }

        public static int SpanReadInt(ReadOnlySpan<byte> bytes, int offset)
        {
            return SpanReadInt(bytes, ref offset);
        }

        public static uint SpanReadUint(ReadOnlySpan<byte> bytes, uint offset)
        {
            return SpanReadUint(bytes, (int)offset);
        }

        public static uint SpanReadUint(ReadOnlySpan<byte> bytes, int offset)
        {
            return SpanReadUint(bytes, ref offset);
        }

        public static ushort SpanReadUshort(ReadOnlySpan<byte> bytes, int offset)
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

        public static IReadOnlyList<T> DoOffsets<T>(ReadOnlySpan<byte> bytes, int offset, uint count) where T : struct
        {
            return DoOffsets<T>(bytes, (uint)offset, (int)count);
        }

        public static IReadOnlyList<T> DoOffsets<T>(ReadOnlySpan<byte> bytes, uint offset, uint count) where T : struct
        {
            return DoOffsets<T>(bytes, offset, (int)count);
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

        public static string ReadString(ReadOnlySpan<byte> bytes, uint offset, int length)
        {
            return ReadString(bytes, (int)offset, length);
        }

        public static string ReadString(ReadOnlySpan<byte> bytes, int offset, int length = -1)
        {
            int end = offset;
            for (int i = 0; i < length; i++)
            {
                if (bytes[offset + i] == 0)
                {
                    break;
                }
                end++;
            }
            if (end == offset)
            {
                return "";
            }
            return Encoding.ASCII.GetString(bytes[offset..end]);
        }

        public static IReadOnlyList<string> ReadStrings(ReadOnlySpan<byte> bytes, long offset, int count)
        {
            return ReadStrings(bytes, (int)offset, count);
        }

        public static IReadOnlyList<string> ReadStrings(ReadOnlySpan<byte> bytes, long offset, uint count)
        {
            return ReadStrings(bytes, (int)offset, (int)count);
        }

        public static IReadOnlyList<string> ReadStrings(ReadOnlySpan<byte> bytes, uint offset, uint count)
        {
            return ReadStrings(bytes, (int)offset, (int)count);
        }

        public static IReadOnlyList<string> ReadStrings(ReadOnlySpan<byte> bytes, int offset, int count)
        {
            var strings = new List<string>();

            while (strings.Count < count)
            {
                int end = offset;
                byte value = bytes[end];
                while (value != 0)
                {
                    value = bytes[++end];
                }
                if (end == offset)
                {
                    strings.Add("");
                }
                else
                {
                    strings.Add(Encoding.ASCII.GetString(bytes[offset..end]));
                }
                offset = end + 1;
            }
            return strings;
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

        public static void ReadAndExport(string name, bool firstHunt = false)
        {
            // todo: need non-throwing versions of these
            Model model;
            try
            {
                model = GetModelByName(name, firstHunt: firstHunt);
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
