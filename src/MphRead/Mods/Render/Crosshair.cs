using System;
using System.Collections.Generic;

namespace MphRead.Mods.Render
{
    public enum CrosshairSize
    {
        Small = 0,
        Medium = 1,
        Big = 2
    }

    public enum CrosshairStyle
    {
        Cross = 0,
        Dot = 1,
        CrossDot = 2,
        Circle = 3,
        Brackets = 4
    }

    /// <summary>
    /// One filled rectangle of a crosshair, in pixels, measured from the
    /// centre of the screen. Y is up, as it is in the clip space the HUD is
    /// drawn into -- not down, as it would be in a window.
    /// </summary>
    public readonly struct CrosshairBar
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public CrosshairBar(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// The shapes each crosshair is made of, in one place because two very
    /// different things draw them: the game, through immediate-mode GL with a
    /// flat fill and no asset, and the settings screen's preview, through
    /// Avalonia. A player who picks a crosshair from a picture and then finds
    /// something else in the match has been lied to by the settings, so the
    /// picture and the game read the same table rather than each describing
    /// the shape in its own terms.
    ///
    /// Everything here is in pixels at <see cref="CrosshairSize.Medium"/> and
    /// is multiplied by <see cref="ScaleOf"/>. Pixels rather than a fraction
    /// of the window because a crosshair is aimed with, and the size that
    /// works is the size it appears on the screen -- which is why there is a
    /// size setting at all.
    /// </summary>
    public static class Crosshair
    {
        public static CrosshairSize Size { get; set; } = CrosshairSize.Medium;

        public static CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;

        /// <summary>What the size stops multiply every measurement by.</summary>
        public static float ScaleOf(CrosshairSize size)
        {
            return size switch
            {
                CrosshairSize.Small => 0.7f,
                CrosshairSize.Big => 1.5f,
                _ => 1f
            };
        }

        public static float Scale => ScaleOf(Size);

        public static readonly string[] SizeNames = { "Small", "Medium", "Big" };

        public static readonly string[] StyleNames =
        {
            "Cross", "Dot", "Cross + dot", "Circle", "Brackets"
        };

        /// <summary>
        /// The ring this style draws, or 0 thickness for the styles that have
        /// none. Radius is to the middle of the stroke.
        /// </summary>
        public static (float Radius, float Thickness) RingOf(CrosshairStyle style, float scale)
        {
            return style switch
            {
                CrosshairStyle.Circle => (8f * scale, 2f * scale),
                _ => (0f, 0f)
            };
        }

        public static IReadOnlyList<CrosshairBar> BarsOf(CrosshairStyle style, float scale)
        {
            var bars = new List<CrosshairBar>(8);
            switch (style)
            {
            case CrosshairStyle.Cross:
                AddCross(bars, arm: 9f, thickness: 3f, gap: 3f, scale);
                break;
            case CrosshairStyle.Dot:
                bars.Add(Dot(4f, scale));
                break;
            case CrosshairStyle.CrossDot:
                AddCross(bars, arm: 8f, thickness: 3f, gap: 5f, scale);
                bars.Add(Dot(3f, scale));
                break;
            case CrosshairStyle.Circle:
                break;
            case CrosshairStyle.Brackets:
                AddBrackets(bars, corner: 10f, length: 6f, thickness: 2f, scale);
                break;
            }
            return bars;
        }

        private static CrosshairBar Dot(float side, float scale)
        {
            return new CrosshairBar(0, 0, side * scale, side * scale);
        }

        private static void AddCross(List<CrosshairBar> bars, float arm, float thickness,
            float gap, float scale)
        {
            float offset = (gap + arm / 2) * scale;
            float longSide = arm * scale;
            float shortSide = thickness * scale;
            bars.Add(new CrosshairBar(0, offset, shortSide, longSide));
            bars.Add(new CrosshairBar(0, -offset, shortSide, longSide));
            bars.Add(new CrosshairBar(-offset, 0, longSide, shortSide));
            bars.Add(new CrosshairBar(offset, 0, longSide, shortSide));
        }

        /// <summary>
        /// Four corner brackets, each an L of two bars. The corner itself is
        /// where the two meet, so they are placed from it rather than from the
        /// centre -- otherwise the L opens up as the size goes down.
        /// </summary>
        private static void AddBrackets(List<CrosshairBar> bars, float corner, float length,
            float thickness, float scale)
        {
            float c = corner * scale;
            float len = length * scale;
            float thick = thickness * scale;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1 : 1;
                float sy = (i & 2) == 0 ? 1 : -1;
                // The arm along the top or bottom edge, running inward.
                bars.Add(new CrosshairBar(sx * (c - len / 2 + thick / 2), sy * (c - thick / 2),
                    len, thick));
                // The arm down the side, running inward, starting under it.
                bars.Add(new CrosshairBar(sx * (c - thick / 2), sy * (c - len / 2 - thick / 2),
                    thick, len));
            }
        }

        /// <summary>
        /// A bar's edges, snapped to whole pixels.
        ///
        /// Everything here is centred on the middle of the screen, and a bar
        /// an odd number of pixels wide therefore has its edges on half
        /// pixels -- which the rasteriser resolves one way on the left and the
        /// other on the right, so a three-pixel dot came out two pixels wide
        /// and sitting off to one side of the ring it was supposed to be in
        /// the middle of. Rounding both edges outward makes every bar an even
        /// number of pixels and exactly symmetrical about the centre, at the
        /// cost of at most half a pixel of size.
        /// </summary>
        public static (float Left, float Right, float Bottom, float Top) EdgesOf(CrosshairBar bar)
        {
            return (Snap(bar.X - bar.Width / 2, down: true),
                Snap(bar.X + bar.Width / 2, down: false),
                Snap(bar.Y - bar.Height / 2, down: true),
                Snap(bar.Y + bar.Height / 2, down: false));
        }

        private static float Snap(float value, bool down)
        {
            return down ? MathF.Floor(value) : MathF.Ceiling(value);
        }

        public static CrosshairSize ParseSize(string? value, CrosshairSize fallback)
        {
            return Enum.TryParse(value, ignoreCase: true, out CrosshairSize parsed)
                ? parsed
                : fallback;
        }

        public static CrosshairStyle ParseStyle(string? value, CrosshairStyle fallback)
        {
            return Enum.TryParse(value, ignoreCase: true, out CrosshairStyle parsed)
                ? parsed
                : fallback;
        }
    }
}
