using System;
using MphRead.Hud;
using MphRead.Mods.Input;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    /// <summary>
    /// The DS's bottom screen, drawn on the top one.
    ///
    /// Faint on purpose. A player with a tablet is reaching for a button with
    /// their hand, not looking for it with their eyes -- what they need is
    /// enough of an outline to know where the row is and that their zone is
    /// where they left it. Anything more solid is a second HUD sitting on top
    /// of the game.
    ///
    /// It goes bright for one thing only: while the zone is being drawn. That
    /// is the one moment the player *is* looking at it, and a rectangle you
    /// are dragging out has to be visible to be dragged.
    ///
    /// Circles rather than boxes, because the layout is a picture the hand
    /// learns and the DS's buttons are round. There is no circle primitive in
    /// the HUD -- it draws flat boxes -- so each one is a stack of horizontal
    /// spans, which is a few dozen quads apiece and costs nothing beside the
    /// room behind it.
    /// </summary>
    public partial class PlayerEntity
    {
        private static readonly Vector4 _stylusInk = new Vector4(0.85f, 0.30f, 0.30f, 1);
        private static readonly Vector4 _stylusFill = new Vector4(0.55f, 0.16f, 0.16f, 1);
        private static readonly Vector4 _stylusLit = new Vector4(1f, 0.72f, 0.35f, 1);

        internal void ModDrawStylusZone()
        {
            if (!IsMainPlayer || !StylusZone.Enabled && !StylusZone.Placing)
            {
                return;
            }
            // The overlay is drawn in the HUD's 256x192 space, and the zone is
            // stated in window fractions, so one is turned into the other
            // here. HudAspectFix is not wanted: a fraction of the window is
            // already a fraction of the window, whatever shape it is.
            float left = StylusZone.Left * 256f;
            float top = StylusZone.Top * 192f;
            float width = StylusZone.Width * 256f;
            float height = StylusZone.Height * 192f;
            if (width <= 1 || height <= 1)
            {
                return;
            }
            // Bright while it is being placed, barely there while it is being
            // played with.
            float alpha = StylusZone.Placing ? 0.55f : StylusZone.Opacity;
            var edge = new Vector4(_stylusInk.Xyz, alpha);
            var fill = new Vector4(_stylusFill.Xyz, alpha * 0.5f);
            // The screen itself: a hairline box, not a filled panel. What is
            // behind this is the game.
            float line = Math.Max(0.5f, height / 96f);
            _scene.DrawHudFlatBox(left, top, left + width, top + line, edge);
            _scene.DrawHudFlatBox(left, top + height - line, left + width, top + height, edge);
            _scene.DrawHudFlatBox(left, top, left + line, top + height, edge);
            _scene.DrawHudFlatBox(left + width - line, top, left + width, top + height, edge);
            float scaleX = width / StylusZone.DsWidth;
            float scaleY = height / StylusZone.DsHeight;
            foreach (StylusZone.Button button in StylusZone.Buttons)
            {
                bool lit = !StylusZone.Placing && StylusZone.Contact
                    && StylusZone.Region == button.Region;
                Vector4 colour = lit ? new Vector4(_stylusLit.Xyz, Math.Min(1, alpha * 3)) : fill;
                DrawStylusCircle(left + button.X * scaleX, top + button.Y * scaleY,
                    button.Radius * scaleX, button.Radius * scaleY, colour);
            }
        }

        /// <summary>
        /// A filled ellipse, as a stack of horizontal spans.
        ///
        /// Two radii rather than one: the zone is a fraction of the window in
        /// x and of its height in y, and a window is not square, so a circle
        /// in DS units is an ellipse in these.
        /// </summary>
        private void DrawStylusCircle(float centreX, float centreY, float radiusX, float radiusY,
            Vector4 colour)
        {
            int rows = (int)MathF.Ceiling(radiusY * 2);
            if (rows < 2 || radiusX <= 0)
            {
                return;
            }
            rows = Math.Min(rows, 96);
            float step = radiusY * 2 / rows;
            for (int i = 0; i < rows; i++)
            {
                float y = -radiusY + (i + 0.5f) * step;
                float t = y / radiusY;
                float half = radiusX * MathF.Sqrt(Math.Max(0, 1 - t * t));
                if (half <= 0)
                {
                    continue;
                }
                _scene.DrawHudFlatBox(centreX - half, centreY + y,
                    centreX + half, centreY + y + step, colour);
            }
        }
    }
}
