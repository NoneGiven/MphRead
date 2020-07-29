using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenToolkit.Graphics.OpenGL;

namespace MphRead.Export
{
    public static class Images
    {
        public static void Screenshot(int width, int height, string? name = null)
        {
            using var bitmap = new Bitmap(width, height);
            var rectangle = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            GL.ReadPixels(0, 0, width, height, OpenToolkit.Graphics.OpenGL.PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
            bitmap.UnlockBits(data);
            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
            string path = Path.Combine(Paths.Export, "_screenshots");
            Directory.CreateDirectory(path);
            name ??= DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            bitmap.Save(Path.Combine(path, $"{name}.png"));
        }

        public static void ExportImages(Model model)
        {
            string exportPath = Path.Combine(Paths.Export, model.Name);
            foreach (Recolor recolor in model.Recolors)
            {
                string colorPath = Path.Combine(exportPath, recolor.Name);
                Directory.CreateDirectory(colorPath);
                var usedTextures = new HashSet<int>();
                foreach (Material material in model.Materials.OrderBy(m => m.TextureId).ThenBy(m => m.PaletteId))
                {
                    if (material.TextureId == UInt16.MaxValue)
                    {
                        continue;
                    }
                    Texture texture = recolor.Textures[material.TextureId];
                    IReadOnlyList<ColorRgba> pixels = recolor.GetPixels(material.TextureId, material.PaletteId);
                    if (texture.Width == 0 || texture.Height == 0 || pixels.Count == 0)
                    {
                        continue;
                    }
                    Debug.Assert(texture.Width * texture.Height == pixels.Count);
                    usedTextures.Add(material.TextureId);
                    string filename = $"{material.TextureId}-{material.PaletteId}";
                    SaveTexture(colorPath, filename, texture.Width, texture.Height, pixels);
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
                        for (int p = 0; p < model.Palettes.Count; p++)
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
