using System;
using System.Collections.Generic;
using MphRead.Hud;

namespace MphRead.Mods.Render
{
    /// <summary>
    /// The weapon icons, drawn as shapes instead of as blocks.
    ///
    /// The art here is a DS touchscreen sprite: one texel per DS pixel, and
    /// each weapon's drawing is about twenty of them across. The weapon list
    /// puts one in a box that is fifty or sixty screen pixels wide on a modern
    /// display -- two and a half times the size it was drawn at, and not a
    /// whole number of times -- so nearest-neighbour magnification gives every
    /// icon a staircase edge with steps of two pixels in some rows and three
    /// in others. That is what "the icons are blurry" is: not a soft picture,
    /// a wobbly one. Pro mode makes it worse rather than better, because it
    /// draws the list at 170%.
    ///
    /// There is no more resolution to be had -- the source is the source --
    /// but there is more *precision*. These frames are silhouettes: the list
    /// draws each one in a flat colour with palette index 0 left transparent
    /// (see <c>DrawWeaponList</c>), so the only information in a texel is
    /// whether it is ink. A silhouette can be resampled properly, which a
    /// photograph cannot: the mask is bilinearly resampled at four times the
    /// size, and the coverage that comes out is pushed back through a narrow
    /// smoothstep so an edge stays an edge rather than becoming a gradient.
    /// The result is an anti-aliased shape at 4x, minified into the box by the
    /// GPU -- which is why <see cref="HudObjectInstance.Smooth"/> exists, and
    /// the one place in this program that asks for linear filtering.
    ///
    /// <para>
    /// The instance keeps the *source* width and height, not the texture's.
    /// Everything that places these icons -- <c>ModIconBounds</c>, the list's
    /// own arithmetic, the pro HUD's ammo icon -- measures in the frame's own
    /// pixels, and <c>DrawHudObject</c> reads Width and Height only to work
    /// out the destination's shape. So a 4x texture behind a 1x instance
    /// changes what is sampled and nothing about where it lands.
    /// </para>
    /// </summary>
    public static class SmoothHudIcon
    {
        /// <summary>
        /// How many texels are generated per source texel, each way.
        ///
        /// Four covers every case that matters: a weapon icon is around
        /// twenty texels across and reaches sixty screen pixels at 1080p in
        /// pro mode, so 4x puts the texture comfortably above the box on any
        /// display anybody plays on and the GPU is minifying rather than
        /// magnifying -- which is the side of the line where filtering helps.
        /// Eight would quadruple the memory for no visible difference.
        /// </summary>
        public const int Factor = 4;

        /// <summary>
        /// An instance sized for the sheet's frames, with room for a 4x
        /// texture behind them. Nothing is drawn into it until
        /// <see cref="Tint"/> is called.
        /// </summary>
        public static HudObjectInstance Create(HudObject sheet)
        {
            var inst = new HudObjectInstance(sheet.Width, sheet.Height,
                sheet.Width * Factor, sheet.Height * Factor);
            inst.Smooth = true;
            inst.Enabled = true;
            return inst;
        }

        /// <summary>
        /// Draw one frame of the sheet into the instance, in a flat colour.
        ///
        /// Does nothing when it is asked for the frame and colour it already
        /// holds, which is what makes this safe to call once a frame -- and it
        /// is called that way, by the weapon list and by the pro HUD's ammo
        /// readout, which ask for the same pair every frame of a match.
        /// </summary>
        public static void Tint(HudObjectInstance inst, IReadOnlyList<byte> data, int frame,
            ColorRgba color, Scene scene)
        {
            if (inst.CharacterData == data && inst.CurrentFrame == frame
                && inst.Color.HasValue && inst.Color.Value.Equals(color)
                && inst.BindingId != -1)
            {
                return;
            }
            inst.CharacterData = data;
            inst.CurrentFrame = frame;
            inst.Color = color;
            inst.PaletteIndex = -1;
            Build(inst, data, frame, color, scene);
        }

        private static void Build(HudObjectInstance inst, IReadOnlyList<byte> data, int frame,
            ColorRgba color, Scene scene)
        {
            int width = inst.Width;
            int height = inst.Height;
            int outWidth = width * Factor;
            int outHeight = height * Factor;
            ColorRgba[] texture = inst.Texture;
            if (texture.Length < outWidth * outHeight)
            {
                return;
            }
            int tilesX = width / 8;
            int image = frame * width * height;
            var transparent = new ColorRgba();
            for (int y = 0; y < outHeight; y++)
            {
                // The centre of this output texel in source coordinates, half
                // a texel back so the four samples straddle it. Without the
                // shift the whole icon walks half a source pixel up and left.
                float sourceY = (y + 0.5f) / Factor - 0.5f;
                int y0 = (int)MathF.Floor(sourceY);
                float fy = sourceY - y0;
                for (int x = 0; x < outWidth; x++)
                {
                    float sourceX = (x + 0.5f) / Factor - 0.5f;
                    int x0 = (int)MathF.Floor(sourceX);
                    float fx = sourceX - x0;
                    float top = Lerp(Ink(data, image, tilesX, width, height, x0, y0),
                        Ink(data, image, tilesX, width, height, x0 + 1, y0), fx);
                    float bottom = Lerp(Ink(data, image, tilesX, width, height, x0, y0 + 1),
                        Ink(data, image, tilesX, width, height, x0 + 1, y0 + 1), fx);
                    float coverage = Lerp(top, bottom, fy);
                    // A narrow band around half coverage rather than the raw
                    // interpolation: bilinear on its own spreads every edge
                    // over a whole source texel, which at this magnification
                    // is a visible halo -- the actual blur the complaint would
                    // have been about if this had been done the lazy way. The
                    // band is what keeps the shape crisp while still giving
                    // the edge somewhere between two and three levels to land
                    // on.
                    float alpha = Smoothstep(0.30f, 0.70f, coverage);
                    texture[y * outWidth + x] = alpha <= 0
                        ? transparent
                        : new ColorRgba(color.Red, color.Green, color.Blue,
                            (byte)MathF.Round(alpha * 255));
                }
            }
            if (inst.BindingId == -1)
            {
                inst.BindingId = scene.BindGetTexture(texture, outWidth, outHeight);
            }
            else
            {
                scene.BindTexture(texture, outWidth, outHeight, inst.BindingId);
            }
        }

        /// <summary>
        /// 1 where the frame has ink and 0 everywhere else, including outside
        /// it -- an icon that touches the edge of its frame should fade out
        /// there rather than being extended.
        ///
        /// The index arithmetic is the 8x8 tiling the DS stores these in; it
        /// is the same walk <c>PlayerEntity.ModIconBounds</c> does, and for
        /// the same reason: palette index 0 is the transparent one.
        /// </summary>
        private static float Ink(IReadOnlyList<byte> data, int image, int tilesX,
            int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return 0;
            }
            int index = image + y / 8 * tilesX * 64 + x / 8 * 64 + y % 8 * 8 + x % 8;
            if (index < 0 || index >= data.Count)
            {
                return 0;
            }
            return data[index] == 0 ? 0 : 1;
        }

        private static float Lerp(float first, float second, float by)
        {
            return first + (second - first) * by;
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
