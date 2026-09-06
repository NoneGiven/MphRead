using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using MphRead.Mods.Render;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// The crosshair as it will actually be drawn, in the settings row that
    /// picks it.
    ///
    /// One point here is one pixel in the game, so the size stops read as what
    /// they are rather than as three words -- which is the whole reason the
    /// preview is worth the room it takes. The shapes come from
    /// <see cref="Crosshair"/>, the same table the renderer draws from, so the
    /// picture cannot drift away from the game.
    /// </summary>
    internal static class CrosshairPreview
    {
        public static void Draw(DrawingContext context, Rect area, CrosshairStyle style,
            CrosshairSize size)
        {
            context.DrawRectangle(GuiTheme.PanelBrush, new Pen(GuiTheme.EdgeBrush, 1),
                new RoundedRect(area, 4));
            double cx = area.X + area.Width / 2;
            double cy = area.Y + area.Height / 2;
            float scale = Crosshair.ScaleOf(size);
            IReadOnlyList<CrosshairBar> bars = Crosshair.BarsOf(style, scale);
            for (int i = 0; i < bars.Count; i++)
            {
                // Whole pixels, and with Y up: the shapes are measured the way
                // the HUD is drawn, and a window measures Y down.
                (float left, float right, float bottom, float top) =
                    Crosshair.EdgesOf(bars[i]);
                context.FillRectangle(GuiTheme.TextBrush, new Rect(
                    cx + left, cy - top, right - left, top - bottom));
            }
            (float radius, float thickness) = Crosshair.RingOf(style, scale);
            if (thickness > 0)
            {
                context.DrawEllipse(null, new Pen(GuiTheme.TextBrush, thickness),
                    new Point(cx, cy), radius, radius);
            }
        }
    }
}
