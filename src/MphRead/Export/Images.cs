using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MphRead.Export
{
    public static class Images
    {
        private static Task? _task = null;
        private static readonly ConcurrentQueue<(Image, string)> _queue = new ConcurrentQueue<(Image, string)>();
        private static readonly PngEncoder _encoderUncomp = new PngEncoder() { CompressionLevel = PngCompressionLevel.NoCompression };
        private static readonly PngEncoder _encoderComp = new PngEncoder() { CompressionLevel = PngCompressionLevel.BestSpeed };

        public static void Screenshot(int width, int height, string? name = null)
        {
            byte[] buffer = new byte[width * height * 4];
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);
            for (int i = 3; i < buffer.Length; i += 4)
            {
                buffer[i] = 255;
            }
            using var image = Image.LoadPixelData<Rgba32>(buffer, width, height);
            image.Mutate(m => RotateFlipExtensions.RotateFlip(m, RotateMode.None, FlipMode.Vertical));
            string path = Path.Combine(Paths.Export, "_screenshots");
            Directory.CreateDirectory(path);
            name ??= DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            image.SaveAsPng(Path.Combine(path, $"{name}.png"), _encoderComp);
        }

        public static void Record(int width, int height, string name)
        {
            if (_task == null)
            {
                _task = Task.Run(async () => await ProcessQueue());
            }
            byte[] buffer = ArrayPool<byte>.Shared.Rent(width * height * 4);
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);
            for (int i = 3; i < width * height * 4; i += 4)
            {
                buffer[i] = 255;
            }
            var image = Image.LoadPixelData<Rgba32>(buffer, width, height);
            ArrayPool<byte>.Shared.Return(buffer);
            _queue.Enqueue((image, name));
        }

        private static async Task ProcessQueue()
        {
            while (true)
            {
                while (_queue.TryDequeue(out (Image Image, string Name) result))
                {
                    result.Image.Mutate(m => RotateFlipExtensions.RotateFlip(m, RotateMode.None, FlipMode.Vertical));
                    string path = Path.Combine(Paths.Export, "_screenshots");
                    Directory.CreateDirectory(path);
                    await result.Image.SaveAsPngAsync(Path.Combine(path, $"{result.Name}.png"), _encoderUncomp);
                    result.Image.Dispose();
                }
                await Task.Delay(15);
            }
        }

        public static void ExportImages(Model model)
        {
            string exportPath = Path.Combine(Paths.Export, model.Name);
            foreach (Recolor recolor in model.Recolors)
            {
                string colorPath = Path.Combine(exportPath, recolor.Name);
                Directory.CreateDirectory(colorPath);
                var usedTextures = new HashSet<int>();
                int id = 0;
                var usedCombos = new HashSet<(int, int)>();

                void DoTexture(int textureId, int paletteId)
                {
                    if (textureId == -1 || usedCombos.Contains((textureId, paletteId)))
                    {
                        return;
                    }
                    Texture texture = recolor.Textures[textureId];
                    IReadOnlyList<ColorRgba> pixels = recolor.GetPixels(textureId, paletteId);
                    if (texture.Width == 0 || texture.Height == 0 || pixels.Count == 0)
                    {
                        return;
                    }
                    Debug.Assert(texture.Width * texture.Height == pixels.Count);
                    usedTextures.Add(textureId);
                    usedCombos.Add((textureId, paletteId));
                    string filename = $"{textureId}-{paletteId}";
                    if (id > 0)
                    {
                        filename = $"anim__{id.ToString().PadLeft(3, '0')}";
                    }
                    SaveTexture(colorPath, filename, texture.Width, texture.Height, pixels);
                }

                foreach (Material material in model.Materials.OrderBy(m => m.TextureId).ThenBy(m => m.PaletteId))
                {
                    DoTexture(material.TextureId, material.PaletteId);
                }
                id = 1;
                usedCombos.Clear();
                foreach (TextureAnimationGroup group in model.AnimationGroups.Texture)
                {
                    foreach (TextureAnimation animation in group.Animations.Values)
                    {
                        for (int i = animation.StartIndex; i < animation.StartIndex + animation.Count; i++)
                        {
                            DoTexture(group.TextureIds[i], group.PaletteIds[i]);
                            id++;
                        }
                    }
                }
                if (usedTextures.Count != recolor.Textures.Count)
                {
                    string unusedPath = Path.Combine(colorPath, "unused");
                    Directory.CreateDirectory(unusedPath);
                    for (int t = 0; t < recolor.Textures.Count; t++)
                    {
                        if (usedTextures.Contains(t))
                        {
                            continue;
                        }
                        Texture texture = recolor.Textures[t];
                        for (int p = 0; p < recolor.Palettes.Count; p++)
                        {
                            IReadOnlyList<TextureData> textureData = recolor.TextureData[t];
                            IReadOnlyList<PaletteData> palette = recolor.PaletteData[p];
                            if (textureData.Any(t => t.Data >= palette.Count))
                            {
                                continue;
                            }
                            IReadOnlyList<ColorRgba> pixels = recolor.GetPixels(t, p);
                            string filename = $"{t}-{p}";
                            SaveTexture(unusedPath, filename, texture.Width, texture.Height, pixels);
                        }
                    }
                }
            }
        }

        public static void ExportPalettes(Model model)
        {
            string exportPath = Path.Combine(Paths.Export, model.Name);
            foreach (Recolor recolor in model.Recolors)
            {
                string palettePath = Path.Combine(exportPath, recolor.Name, "palettes");
                Directory.CreateDirectory(palettePath);
                for (int p = 0; p < recolor.Palettes.Count; p++)
                {
                    IReadOnlyList<ColorRgba> pixels = recolor.GetPalettePixels(p);
                    string filename = $"p{p}";
                    SaveTexture(palettePath, filename, 16, 16, pixels);
                }
            }
        }

        private static void SaveTexture(string directory, string filename, ushort width, ushort height, IReadOnlyList<ColorRgba> pixels)
        {
            string imagePath = Path.Combine(directory, $"{filename}.png");
            using var image = new Image<Rgba32>(width, height);
            for (int p = 0; p < pixels.Count; p++)
            {
                ColorRgba pixel = pixels[p];
                image[p % width, p / width] = new Rgba32(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
            }
            image.SaveAsPng(imagePath);
        }
    }
}
