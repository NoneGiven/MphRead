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
    /// and this deliberately does not pretend otherwise. The frame is
    /// replicated at a whole multiple, hard-edged, with no interpolation:
    /// every DS pixel stays a sharp square exactly as it was drawn. What the
    /// multiple buys is that the texture is then *larger* than the box it
    /// lands in, so the GPU is minifying rather than magnifying, and a linear
    /// minification of hard squares is those same squares with a hairline of
    /// anti-aliasing along their edges. The picture stays the DS's own, and
    /// the staircase stops being uneven -- which is what the complaint was.
    ///
    /// It was resampled first -- bilinear over the mask, then a smoothstep --
    /// and that was wrong twice over. Interpolating a mask rounds every
    /// corner, and a 4x texture is *magnified* again at 1440p with the weapon
    /// list at 170%, so the result was a soft blob: the same word came back.
    /// Sharp is the goal here; the anti-aliasing is only there to stop the
    /// edge wobbling. See <see cref="HudObjectInstance.Smooth"/>, which is
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
        /// It has to stay above the largest magnification these are ever drawn
        /// at, because below that the GPU magnifies the texture instead and
        /// the icon goes soft -- which is exactly what four did. A weapon
        /// icon's ink is about twenty texels across, and the box it goes in is
        /// roughly sixty screen pixels at 1080p, ninety at 1440p and a hundred
        /// and thirty-five at 4K with the list at 170%: under seven in the
        /// worst case in play. Eight covers it, and costs 2.4 MB for the nine
        /// icons once -- <c>DrawWeaponList</c> reuses the instances across a
        /// map rotation rather than building nine more each time.
        /// </summary>
        public const int Factor = 8;

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
            var ink = new ColorRgba(color.Red, color.Green, color.Blue, 255);
            for (int y = 0; y < outHeight; y++)
            {
                // Whole squares, not samples: this row of output texels is one
                // row of source texels repeated Factor times. No interpolation
                // anywhere, so a corner stays a corner and the anti-aliasing
                // is left to the minification that follows.
                int sourceY = y / Factor;
                for (int x = 0; x < outWidth; x++)
                {
                    texture[y * outWidth + x] =
                        Ink(data, image, tilesX, width, height, x / Factor, sourceY) > 0
                            ? ink
                            : transparent;
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

    }
}
