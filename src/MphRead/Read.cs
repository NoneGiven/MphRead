using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MphRead.Archive;
using MphRead.Export;
using OpenTK.Mathematics;

namespace MphRead
{
    public static class Read
    {
        public static bool ApplyFixes { get; set; } = true;

        private static readonly Dictionary<string, Model> _modelCache = new Dictionary<string, Model>();
        private static readonly Dictionary<string, Model> _fhModelCache = new Dictionary<string, Model>();

        public static ModelInstance GetModelInstance(string name, bool firstHunt = false,
            MetaDir dir = MetaDir.Models, bool noCache = false)
        {
            ModelInstance? inst = GetModelInstanceOrNull(name, firstHunt, dir, noCache);
            if (inst == null)
            {
                throw new ProgramException("No model with this name is known.");
            }
            return inst;
        }

        private static ModelInstance? GetModelInstanceOrNull(string name, bool firstHunt, MetaDir dir, bool noCache)
        {
            Dictionary<string, Model> cache = firstHunt ? _fhModelCache : _modelCache;
            if (noCache || !cache.TryGetValue(name, out Model? model))
            {
                model = GetModel(name, firstHunt, dir);
                if (model == null)
                {
                    return null;
                }
                if (!noCache)
                {
                    cache.Add(name, model);
                }
            }
            return new ModelInstance(model);
        }

        private static Model? GetModel(string name, bool firstHunt, MetaDir dir)
        {
            ModelMetadata? meta;
            if (firstHunt)
            {
                meta = Metadata.GetFirstHuntModelByName(name);
            }
            else
            {
                meta = Metadata.GetModelByName(name, dir);
            }
            if (meta == null)
            {
                return null;
            }
            return ReadModel(meta.Name, meta.ModelPath, meta.AnimationPath, meta.AnimationShare, meta.Recolors, meta.FirstHunt);
        }

        public static ModelInstance GetRoomModelInstance(string name)
        {
            ModelInstance? inst = GetRoomModelInstanceOrNull(name);
            if (inst == null)
            {
                throw new ProgramException("No room with this name is known.");
            }
            return inst;
        }

        private static ModelInstance? GetRoomModelInstanceOrNull(string name)
        {
            (RoomMetadata? meta, _) = Metadata.GetRoomByName(name);
            if (meta == null)
            {
                return null;
            }
            if (!_modelCache.TryGetValue(name, out Model? model))
            {
                model = GetRoomModel(meta);
                if (model == null)
                {
                    return null;
                }
                _modelCache.Add(name, model);
            }
            return new ModelInstance(model);
        }

        private static Model GetRoomModel(RoomMetadata meta)
        {
            var recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", meta.ModelPath, meta.TexturePath ?? meta.ModelPath)
            };
            return ReadModel(meta.Name, meta.ModelPath, meta.AnimationPath, animationShare: null, recolors,
                firstHunt: meta.FirstHunt || meta.Hybrid);
        }

        public static void RemoveModel(string name, bool firstHunt = false)
        {
            if (firstHunt)
            {
                _fhModelCache.Remove(name);
            }
            else
            {
                _modelCache.Remove(name);
            }
        }

        private static Model ReadModel(string name, string modelPath, string? animationPath, string? animationShare,
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
                if (ApplyFixes)
                {
                    if ((name == "Guardian_lod0" || name == "Guardian_lod1") && meta.Name != "pal_01")
                    {
                        var extraTex = new List<Texture>();
                        var extraPal = new List<Palette>();
                        if (meta.Name == "pal_02" || meta.Name == "pal_03" || meta.Name == "pal_04")
                        {
                            extraTex.AddRange(textures);
                            extraPal.AddRange(palettes);
                            if (meta.Name == "pal_02")
                            {
                                extraTex[0] = extraTex[5];
                                extraTex[1] = extraTex[4];
                                extraPal[7] = extraPal[4];
                                extraPal[3] = extraPal[0];
                            }
                            else if (meta.Name == "pal_03")
                            {
                                extraTex[0] = extraTex[6];
                                extraTex[1] = extraTex[3];
                                extraPal[7] = extraPal[5];
                                extraPal[3] = extraPal[1];
                            }
                            else if (meta.Name == "pal_04")
                            {
                                extraTex[0] = extraTex[7];
                                extraTex[1] = extraTex[2];
                                extraPal[7] = extraPal[6];
                                extraPal[3] = extraPal[2];
                            }
                        }
                        else if (meta.Name == "pal_Team01" || meta.Name == "pal_Team02")
                        {
                            extraTex.Add(textures[1]); // 0
                            extraTex.Add(textures[0]); // 1
                            extraPal.Add(new Palette());
                            extraPal.Add(new Palette());
                            extraPal.Add(new Palette());
                            extraPal.Add(palettes[0]); // 3
                            extraPal.Add(new Palette());
                            extraPal.Add(new Palette());
                            extraPal.Add(new Palette());
                            extraPal.Add(palettes[1]); // 7
                        }
                        textures = extraTex;
                        palettes = extraPal;
                    }
                    else if (name == "Alimbic_Power" || name == "Generic_Power"
                        || name == "Ice_Power" || name == "Lava_Power" || name == "Ruins_Power")
                    {
                        var extraTex = new List<Texture>();
                        extraTex.AddRange(textures);
                        var extraPal = new List<Palette>();
                        extraPal.AddRange(palettes);
                        if (name == "Alimbic_Power")
                        {
                            extraTex[1] = extraTex[0];
                            extraPal[1] = extraPal[0];
                        }
                        else if (name == "Lava_Power")
                        {
                            extraTex[1] = extraTex[2];
                            extraPal[1] = extraPal[2];
                        }
                        else if (name == "Ruins_Power")
                        {
                            extraTex[0] = extraTex[1];
                            extraPal[0] = extraPal[1];
                        }
                        textures = extraTex;
                        palettes = extraPal;
                    }
                }
                ReadOnlySpan<byte> textureBytes = modelBytes;
                if (meta.TexturePath != meta.ModelPath)
                {
                    textureBytes = ReadBytes(meta.TexturePath, firstHunt);
                }
                ReadOnlySpan<byte> paletteBytes = textureBytes;
                if (meta.PalettePath != meta.TexturePath && meta.ReplaceIds.Count == 0)
                {
                    paletteBytes = ReadBytes(meta.PalettePath, firstHunt);
                    Header paletteHeader = ReadStruct<Header>(paletteBytes[0..Sizes.Header]);
                    palettes = DoOffsets<Palette>(paletteBytes, paletteHeader.PaletteOffset, paletteHeader.PaletteCount);
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
                if (ApplyFixes)
                {
                    if (name == "Alimbic_Power" || name == "Generic_Power" || name == "Ice_Power" || name == "Lava_Power")
                    {
                        Recolor recolor = GetModelInstance("Ruins_Power").Model.Recolors[0];
                        Texture newTexture = recolor.Textures[8];
                        IReadOnlyList<TextureData> newTexData = recolor.TextureData[8];
                        IReadOnlyList<PaletteData> newPalette = Metadata.PowerPalettes[name];
                        Debug.Assert(newPalette.Count == 8);
                        var extraTex = new List<Texture>();
                        extraTex.AddRange(textures);
                        if (name == "Lava_Power")
                        {
                            textureData.Add(newTexData);
                            extraTex.Add(newTexture);
                        }
                        else
                        {
                            textureData[8] = newTexData;
                            extraTex[8] = newTexture;
                        }
                        var extraPal = new List<Palette>();
                        extraPal.AddRange(palettes);
                        if (name == "Lava_Power")
                        {
                            paletteData.Add(newPalette);
                            extraPal.Add(new Palette());
                        }
                        else
                        {
                            paletteData[8] = newPalette;
                        }
                        textures = extraTex;
                        palettes = extraPal;
                    }
                }
                else if (name == "Lava_Power")
                {
                    // file32Material uses texture/palette ID 8, but there are only 8 of each in LavaEquipTextureShare
                    var extraTex = new List<Texture>();
                    extraTex.AddRange(textures);
                    extraTex.Add(new Texture(TextureFormat.Palette8Bit, 1, 1));
                    textureData.Add(new List<TextureData>() { new TextureData(0, 255) });
                    var extraPal = new List<Palette>();
                    extraPal.AddRange(palettes);
                    extraPal.Add(new Palette());
                    paletteData.Add(new List<PaletteData>() { new PaletteData(0x7FFF) });
                    textures = extraTex;
                    palettes = extraPal;
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
                animations.NodeAnimationGroups.AddRange(shared.NodeAnimationGroups);
                animations.MaterialAnimationGroups.AddRange(shared.MaterialAnimationGroups);
                animations.TexcoordAnimationGroups.AddRange(shared.TexcoordAnimationGroups);
                animations.TextureAnimationGroups.AddRange(shared.TextureAnimationGroups);
                animations.NodeGroupOffsets.AddRange(shared.NodeGroupOffsets);
                animations.MaterialGroupOffsets.AddRange(shared.MaterialGroupOffsets);
                animations.TexcoordGroupOffsets.AddRange(shared.TexcoordGroupOffsets);
                animations.TextureGroupOffsets.AddRange(shared.TextureGroupOffsets);
            }
            // NodePosition and NodeInitialPosition are always 0
            IReadOnlyList<Vector3Fx> nodePos = DoOffsets<Vector3Fx>(initialBytes, header.NodePosition, header.NodeCount);
            IReadOnlyList<Vector3Fx> nodeInitPos = DoOffsets<Vector3Fx>(initialBytes, header.NodeInitialPosition, header.NodeCount);
            IReadOnlyList<int> posCounts;
            if (header.NodePosCounts != 0 && header.NodeWeightCount == 0)
            {
                int nodeWeightCount = ((int)header.NodePosCounts - Sizes.Header) / 4;
                posCounts = DoOffsets<int>(initialBytes, header.NodePosCounts, nodeWeightCount);
            }
            else
            {
                posCounts = DoOffsets<int>(initialBytes, header.NodePosCounts, header.NodeWeightCount);
            }
            int maxIndex = -1;
            if (header.NodePosCounts != 0)
            {
                for (int n = 0; n < header.NodeWeightCount; n++)
                {
                    for (int i = 0; i < posCounts[n]; i++)
                    {
                        int posIndex = i + nodeWeights[n];
                        maxIndex = Math.Max(maxIndex, posIndex);
                    }
                }
            }
            // NodePosScales is never set, and it would only be used if an entry in posCounts is greater than 1, which also never happens
            IReadOnlyList<Fixed> posScales = DoOffsets<Fixed>(initialBytes, header.NodePosScales, maxIndex + 1);
            return new Model(name, firstHunt, header, nodes, meshes, materials, dlists, instructions, animations,
                textureMatrices, recolors, nodeWeights, nodePos, nodeInitPos, posCounts, posScales);
        }

        private static AnimationResults LoadAnimation(string model, string? path, IReadOnlyList<RawNode> nodes, bool firstHunt)
        {
            string temp = model;
            var results = new AnimationResults();
            if (path == null)
            {
                return results;
            }
            path = Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, path);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            AnimationHeader header = ReadStruct<AnimationHeader>(bytes);
            IReadOnlyList<uint> nodeGroupOffsets = DoOffsets<uint>(bytes, header.NodeGroupOffset, header.Count);
            IReadOnlyList<uint> materialGroupOffsets = DoOffsets<uint>(bytes, header.MaterialGroupOffset, header.Count);
            IReadOnlyList<uint> texcoordGroupOffsets = DoOffsets<uint>(bytes, header.TexcoordGroupOffset, header.Count);
            IReadOnlyList<uint> textureGroupOffsets = DoOffsets<uint>(bytes, header.TextureGroupOffset, header.Count);
            results.NodeGroupOffsets.AddRange(nodeGroupOffsets);
            results.MaterialGroupOffsets.AddRange(materialGroupOffsets);
            results.TexcoordGroupOffsets.AddRange(texcoordGroupOffsets);
            results.TextureGroupOffsets.AddRange(textureGroupOffsets);
            foreach (uint offset in nodeGroupOffsets)
            {
                if (offset == 0)
                {
                    results.NodeAnimationGroups.Add(NodeAnimationGroup.Empty());
                    continue;
                }
                RawNodeAnimationGroup rawGroup = DoOffset<RawNodeAnimationGroup>(bytes, offset);
                if (nodes.Count == 0)
                {
                    Debug.Assert(offset == rawGroup.ScaleLutOffset && offset == rawGroup.RotateLutOffset
                        && offset == rawGroup.TranslateLutOffset && offset == rawGroup.AnimationOffset);
                    results.NodeAnimationGroups.Add(new NodeAnimationGroup(rawGroup, new List<float>(),
                        new List<float>(), new List<float>(), new Dictionary<string, NodeAnimation>()));
                    continue;
                }
                Debug.Assert(offset > rawGroup.AnimationOffset);
                Debug.Assert((offset - rawGroup.AnimationOffset) % Sizes.NodeAnimation == 0);
                Debug.Assert(rawGroup.RotateLutOffset > rawGroup.ScaleLutOffset);
                Debug.Assert((rawGroup.RotateLutOffset - rawGroup.ScaleLutOffset) % 4 == 0);
                Debug.Assert(rawGroup.TranslateLutOffset > rawGroup.RotateLutOffset);
                Debug.Assert((rawGroup.TranslateLutOffset - rawGroup.RotateLutOffset) % 2 == 0);
                Debug.Assert(rawGroup.AnimationOffset > rawGroup.TranslateLutOffset);
                Debug.Assert((rawGroup.AnimationOffset - rawGroup.TranslateLutOffset) % 4 == 0);
                int count = (int)(offset - rawGroup.AnimationOffset) / Sizes.NodeAnimation;
                // note: some groups have more animations than the model has nodes, and one of GuardBot1's groups has one less
                IReadOnlyList<NodeAnimation> rawAnimations = DoOffsets<NodeAnimation>(bytes, rawGroup.AnimationOffset, count);
                var animations = new Dictionary<string, NodeAnimation>();
                for (int i = 0; i < rawAnimations.Count; i++)
                {
                    string name = i < nodes.Count ? nodes[i].Name.MarshalString() : $"__no_node_{i.ToString().PadLeft(2, '0')}";
                    animations.Add(name, rawAnimations[i]);
                }
                int scaleCount = (int)(rawGroup.RotateLutOffset - rawGroup.ScaleLutOffset) / 4;
                int rotCount = (int)(rawGroup.TranslateLutOffset - rawGroup.RotateLutOffset) / 2; // might include padding
                int transCount = (int)(rawGroup.AnimationOffset - rawGroup.TranslateLutOffset) / 4;
                var scales = DoOffsets<Fixed>(bytes, rawGroup.ScaleLutOffset, scaleCount).Select(f => f.FloatValue).ToList();
                var rotations = new List<float>();
                foreach (ushort value in DoOffsets<ushort>(bytes, rawGroup.RotateLutOffset, rotCount))
                {
                    rotations.Add(value / 65536.0f * 2.0f * MathF.PI);
                }
                var translations = DoOffsets<Fixed>(bytes, rawGroup.TranslateLutOffset, transCount).Select(f => f.FloatValue).ToList();
                results.NodeAnimationGroups.Add(new NodeAnimationGroup(rawGroup, scales, rotations, translations, animations));
            }
            foreach (uint offset in materialGroupOffsets)
            {
                if (offset == 0)
                {
                    results.MaterialAnimationGroups.Add(MaterialAnimationGroup.Empty());
                    continue;
                }
                RawMaterialAnimationGroup rawGroup = DoOffset<RawMaterialAnimationGroup>(bytes, offset);
                if (rawGroup.AnimationCount == 0)
                {
                    Debug.Assert(offset == rawGroup.ColorLutOffset && offset == rawGroup.AnimationOffset);
                    results.MaterialAnimationGroups.Add(new MaterialAnimationGroup(rawGroup,
                        new List<float>(), new Dictionary<string, MaterialAnimation>()));
                    continue;
                }
                Debug.Assert(rawGroup.AnimationOffset > rawGroup.ColorLutOffset);
                IReadOnlyList<MaterialAnimation> rawAnimations
                    = DoOffsets<MaterialAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                var animations = new Dictionary<string, MaterialAnimation>();
                foreach (MaterialAnimation animation in rawAnimations)
                {
                    animations.Add(animation.Name.MarshalString(), animation);
                }
                // note: the NoxGun lambert3 blue channel LUT length is one too short -- colorCount takes care of this
                int colorCount = (int)(rawGroup.AnimationOffset - rawGroup.ColorLutOffset); // might include padding
                var colors = DoOffsets<byte>(bytes, rawGroup.ColorLutOffset, colorCount).Select(b => (float)b).ToList();
                results.MaterialAnimationGroups.Add(new MaterialAnimationGroup(rawGroup, colors, animations));
            }
            foreach (uint offset in texcoordGroupOffsets)
            {
                if (offset == 0)
                {
                    results.TexcoordAnimationGroups.Add(TexcoordAnimationGroup.Empty());
                    continue;
                }
                RawTexcoordAnimationGroup rawGroup = DoOffset<RawTexcoordAnimationGroup>(bytes, offset);
                if (rawGroup.AnimationCount == 0)
                {
                    Debug.Assert(offset == rawGroup.ScaleLutOffset && offset == rawGroup.RotateLutOffset
                        && offset == rawGroup.TranslateLutOffset && offset == rawGroup.AnimationOffset);
                    results.TexcoordAnimationGroups.Add(new TexcoordAnimationGroup(rawGroup, new List<float>(),
                        new List<float>(), new List<float>(), new Dictionary<string, TexcoordAnimation>()));
                    continue;
                }
                Debug.Assert(rawGroup.RotateLutOffset > rawGroup.ScaleLutOffset);
                Debug.Assert((rawGroup.RotateLutOffset - rawGroup.ScaleLutOffset) % 4 == 0);
                Debug.Assert(rawGroup.TranslateLutOffset > rawGroup.RotateLutOffset);
                Debug.Assert((rawGroup.TranslateLutOffset - rawGroup.RotateLutOffset) % 2 == 0);
                Debug.Assert(rawGroup.AnimationOffset > rawGroup.TranslateLutOffset);
                Debug.Assert((rawGroup.AnimationOffset - rawGroup.TranslateLutOffset) % 4 == 0);
                IReadOnlyList<TexcoordAnimation> rawAnimations
                    = DoOffsets<TexcoordAnimation>(bytes, rawGroup.AnimationOffset, (int)rawGroup.AnimationCount);
                var animations = new Dictionary<string, TexcoordAnimation>();
                foreach (TexcoordAnimation animation in rawAnimations)
                {
                    animations.Add(animation.Name.MarshalString(), animation);
                }
                int scaleCount = (int)(rawGroup.RotateLutOffset - rawGroup.ScaleLutOffset) / 4;
                int rotCount = (int)(rawGroup.TranslateLutOffset - rawGroup.RotateLutOffset) / 2; // might include padding
                int transCount = (int)(rawGroup.AnimationOffset - rawGroup.TranslateLutOffset) / 4;
                var scales = DoOffsets<Fixed>(bytes, rawGroup.ScaleLutOffset, scaleCount).Select(f => f.FloatValue).ToList();
                var rotations = new List<float>();
                foreach (ushort value in DoOffsets<ushort>(bytes, rawGroup.RotateLutOffset, rotCount))
                {
                    rotations.Add(value / 65536.0f * 2.0f * MathF.PI);
                }
                var translations = DoOffsets<Fixed>(bytes, rawGroup.TranslateLutOffset, transCount).Select(f => f.FloatValue).ToList();
                results.TexcoordAnimationGroups.Add(new TexcoordAnimationGroup(rawGroup, scales, rotations, translations, animations));
            }
            foreach (uint offset in textureGroupOffsets)
            {
                if (offset == 0)
                {
                    results.TextureAnimationGroups.Add(TextureAnimationGroup.Empty());
                    continue;
                }
                RawTextureAnimationGroup rawGroup = DoOffset<RawTextureAnimationGroup>(bytes, offset);
                if (rawGroup.AnimationCount == 0)
                {
                    Debug.Assert(offset == rawGroup.FrameIndexOffset && offset == rawGroup.TextureIdOffset
                        && offset == rawGroup.PaletteIdOffset && offset == rawGroup.AnimationOffset);
                    Debug.Assert(rawGroup.FrameIndexCount == 0 && rawGroup.TextureIdCount == 0 && rawGroup.PaletteIdCount == 0);
                    results.TextureAnimationGroups.Add(new TextureAnimationGroup(rawGroup, new List<ushort>(),
                        new List<ushort>(), new List<ushort>(), new Dictionary<string, TextureAnimation>()));
                    continue;
                }
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
            return GetEntitiesFromPath(path, layerId, firstHunt);
        }

        public static IReadOnlyList<Entity> GetEntitiesFromPath(string path, int layerId, bool firstHunt)
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
                EntityType.ItemSpawn => ReadEntity<ItemSpawnEntityData>(bytes, entry, header),
                EntityType.EnemySpawn => ReadEntity<EnemySpawnEntityData>(bytes, entry, header),
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
                EntityType.FhItemSpawn => ReadFirstHuntEntity<FhItemSpawnEntityData>(bytes, entry, header),
                EntityType.FhEnemySpawn => ReadFirstHuntEntity<FhEnemySpawnEntityData>(bytes, entry, header),
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

        private static readonly Dictionary<string, Effect> _effects = new Dictionary<string, Effect>();
        private static readonly Dictionary<(string, string), Particle> _particleDefs = new Dictionary<(string, string), Particle>();

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
            // these are also offsets into the func/param arrays, but don't seem to be used
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
            ModelInstance inst = GetModelInstance(modelName);
            Model model = inst.Model;
            Node? node = model.Nodes.FirstOrDefault(n => n.Name == particleName);
            // ptodo: see what the game does here; gib3/gib4 nodes are probably meant to be used for these
            if (modelName == "geo1" && particleName == "gib")
            {
                node = model.Nodes.First(n => n.Name == "gib3");
            }
            if (node != null && node.MeshCount > 0)
            {
                int materialId = model.Meshes[node.MeshId / 2].MaterialId;
                var newParticle = new Particle(particleName, inst.Model, node, materialId);
                _particleDefs.Add((modelName, particleName), newParticle);
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

        public static int SpanReadInt(ReadOnlySpan<byte> bytes, ref int offset)
        {
            int result = MemoryMarshal.Read<int>(bytes[offset..(offset + sizeof(int))]);
            offset += sizeof(int);
            return result;
        }

        public static uint SpanReadUint(ReadOnlySpan<byte> bytes, ref int offset)
        {
            uint result = MemoryMarshal.Read<uint>(bytes[offset..(offset + sizeof(uint))]);
            offset += sizeof(uint);
            return result;
        }

        public static ushort SpanReadUshort(ReadOnlySpan<byte> bytes, ref int offset)
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

        public static ushort SpanReadUshort(ReadOnlySpan<byte> bytes, uint offset)
        {
            return SpanReadUshort(bytes, (int)offset);
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

        public static T DoOffset<T>(ReadOnlySpan<byte> bytes, int offset) where T : struct
        {
            return DoOffset<T>(bytes, (uint)offset);
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

        public static IReadOnlyList<uint> DoListNullEnd(ReadOnlySpan<byte> bytes, uint offset)
        {
            int ioffset = (int)offset;
            var results = new List<uint>();
            if (offset != 0)
            {
                int size = sizeof(uint);
                for (uint i = 0; ; i++, ioffset += size)
                {
                    uint result = ReadStruct<uint>(bytes[ioffset..(ioffset + size)]);
                    if (result == 0)
                    {
                        break;
                    }
                    results.Add(result);
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

        public static T ReadStruct<T>(IntPtr pointer) where T : struct
        {
            object? result = Marshal.PtrToStructure(pointer, typeof(T));
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

        public static void ExtractArchive(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string output = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? "", "..", "_archives", name));
            try
            {
                int filesWritten = 0;
                Directory.CreateDirectory(output);
                Console.Write($"Reading {name}...");
                var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
                if (Encoding.ASCII.GetString(bytes[0..8]) == Archiver.MagicString)
                {
                    Console.Write(" Extracting archive...");
                    filesWritten = Archiver.Extract(path, output);
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
                    Console.Write(" Decompressing...");
                    Lz.Decompress(path, destination);
                    Console.Write(" Extracting archive...");
                    filesWritten = Archiver.Extract(destination, output);
                    Directory.Delete(temp, recursive: true);
                }
                Console.WriteLine();
                Console.WriteLine($"Extracted {filesWritten} file{(filesWritten == 1 ? "" : "s")}.");
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine($"Failed to extract archive. Verify an archive exists at {path}.");
            }
        }

        public static void ReadAndExport(string name, bool firstHunt = false, MetaDir dir = MetaDir.Models)
        {
            Model? model = GetModelInstanceOrNull(name, firstHunt, dir, noCache: true)?.Model;
            if (model == null)
            {
                model = GetRoomModelInstanceOrNull(name)?.Model;
                if (model == null)
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

    public class AnimationResults
    {
        public List<NodeAnimationGroup> NodeAnimationGroups { get; } = new List<NodeAnimationGroup>();
        public List<MaterialAnimationGroup> MaterialAnimationGroups { get; } = new List<MaterialAnimationGroup>();
        public List<TexcoordAnimationGroup> TexcoordAnimationGroups { get; } = new List<TexcoordAnimationGroup>();
        public List<TextureAnimationGroup> TextureAnimationGroups { get; } = new List<TextureAnimationGroup>();
        public List<uint> NodeGroupOffsets { get; } = new List<uint>();
        public List<uint> MaterialGroupOffsets { get; } = new List<uint>();
        public List<uint> TexcoordGroupOffsets { get; } = new List<uint>();
        public List<uint> TextureGroupOffsets { get; } = new List<uint>();
    }
}
