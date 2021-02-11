using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using MphRead.Entities;
using OpenTK.Graphics.OpenGL;

namespace MphRead.Export
{
    public static class Images
    {
        public static void Screenshot(int width, int height, string? name = null)
        {
            using var bitmap = new Bitmap(width, height);
            var rectangle = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            GL.ReadPixels(0, 0, width, height, OpenTK.Graphics.OpenGL.PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
            bitmap.UnlockBits(data);
            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
            string path = Path.Combine(Paths.Export, "_screenshots");
            Directory.CreateDirectory(path);
            name ??= DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            bitmap.Save(Path.Combine(path, $"{name}.png"));
        }

        public static void ExportImages(NewModel model)
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
                    if (textureId == UInt16.MaxValue || usedCombos.Contains((textureId, paletteId)))
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

        public static void ExportPalettes(NewModel model)
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
            var bitmap = new Bitmap(width, height);
            for (int p = 0; p < pixels.Count; p++)
            {
                ColorRgba pixel = pixels[p];
                bitmap.SetPixel(p % width, p / width,
                    Color.FromArgb(pixel.Alpha, pixel.Red, pixel.Green, pixel.Blue));
            }
            bitmap.Save(imagePath);
        }
    }
}
