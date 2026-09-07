using System;
using MphRead.Hud;
using MphRead.Text;

namespace MphRead.Entities
{
    /// <summary>
    /// Keeping the ammo count out from under the weapon icon.
    ///
    /// A partial of PlayerEntity for the reason the chat log and the Pro HUD
    /// readouts are: the HUD's font, its layout table and its icon instances
    /// are all private to it, and the alternative to one call from
    /// <c>DrawAmmoBar</c> is opening the whole of it up.
    ///
    /// Every hunter lays its own readouts out -- eight arrangements in
    /// <c>HudElements.HunterObjects</c>, one per helmet -- and on **Spire**
    /// two of them land on each other. His weapon icon is at the top of the
    /// screen (y 20, where every other hunter's is between 104 and 150) and
    /// is a 32-unit sprite, so it reaches down to y 52; his ammo count is
    /// drawn at y 46, just above the top of the vertical ammo bar, exactly
    /// where his health figure sits above its own bar on the other side. The
    /// two boxes overlap by six units, and the icon is drawn *after* the
    /// count (see <c>DrawHudObjects</c>), so it covers the top of the digits
    /// -- leaving a number you can see the bottom third of. Reported as "with
    /// Spire the ammo value cannot be read".
    ///
    /// Fixed here rather than by editing that hunter's row in the layout
    /// table, for two reasons. The table is upstream's transcription of the
    /// game's own data and is otherwise untouched by this fork, so a number
    /// changed in it is a number that disagrees with the cartridge for ever
    /// and silently. And the rule this enforces -- a readout is worth nothing
    /// under an opaque sprite -- is not a fact about Spire: it holds for any
    /// hunter, any window shape, and any weapon whose count grows a digit,
    /// and stating it once covers all of them.
    /// </summary>
    public partial class PlayerEntity
    {
        /// <summary>
        /// A gap between the digits and the icon, in HUD units. Enough that
        /// the two read as two things rather than as one crowded one.
        /// </summary>
        private const float AmmoIconGap = 2;

        /// <summary>
        /// Where the ammo count should actually be drawn: the position asked
        /// for, unless the weapon icon is about to be drawn on top of it.
        ///
        /// Moved sideways rather than up or down, because what is above and
        /// below it is spoken for: the icon is above, and the ammo bar the
        /// number labels starts a couple of units below. Left first, since
        /// the icon sits against the right edge of the screen on the hunter
        /// this happens to; right if the left would run off the screen.
        /// </summary>
        internal float ModAmmoTextX(float x, float y, Align align, ReadOnlySpan<char> text)
        {
            if (!_weaponIconInst.Enabled || text.Length == 0)
            {
                return x;
            }
            float iconLeft = _hudObjects.WeaponIconPosX + _objShiftX;
            float iconTop = _hudObjects.WeaponIconPosY + _objShiftY;
            float iconRight = iconLeft + _weaponIconInst.Width;
            // How tall the icon actually comes out, which is not the 32 units
            // it is drawn as. DrawHudObject's mode 0 -- the default, and what
            // the weapon icon is drawn with -- sizes a sprite off the window's
            // *width* and then keeps it square in pixels, so on anything wider
            // than the DS's 4:3 it grows in both directions: a 32-unit icon is
            // 43 units tall on 16:9 and 56 on 21:9. That is the same
            // widescreen stretch HudAspectFix exists for, seen from the other
            // end, and dividing by it gives the height the icon is really
            // covering.
            float iconBottom = iconTop + _weaponIconInst.Height / Math.Max(HudAspectFix, 0.0001f);
            // The font's own line box. Glyphs are placed by their per-glyph
            // offset below the anchor, so this is the band the run occupies
            // rather than the ink's exact extent -- which is what should be
            // compared against a sprite that is opaque over all of its own.
            float textTop = y;
            float textBottom = y + FontLineHeight;
            if (textBottom <= iconTop || textTop >= iconBottom)
            {
                return x;
            }
            float width = ModTextWidth(text);
            float left = align switch
            {
                Align.Right => x - width,
                Align.Center or Align.PadCenter => x - width / 2,
                _ => x
            };
            float right = left + width;
            if (right <= iconLeft || left >= iconRight)
            {
                return x;
            }
            // Left, into the room the icon is not using. The shift is applied
            // to the anchor rather than to the box, so it works whichever way
            // the caller's alignment measures from.
            float shift = right - iconLeft + AmmoIconGap;
            if (left - shift >= AmmoIconGap)
            {
                return x - shift;
            }
            return x + (iconRight - left + AmmoIconGap);
        }

        /// <summary>
        /// The vertical space one line of the HUD font takes, which is
        /// <c>DrawText2D</c>'s own default spacing. Not the glyph height:
        /// glyphs hang from their own offsets inside this box, and the box is
        /// what has to clear a sprite.
        /// </summary>
        private const float FontLineHeight = 12;

        /// <summary>
        /// How wide a run comes out, in the HUD's 256-unit x space.
        ///
        /// The same sum <c>DrawText2D</c> does as it draws -- the font's own
        /// per-glyph advance, aspect-corrected the same way -- because a width
        /// measured any other way is right on a 4:3 window and wrong on every
        /// other one.
        /// </summary>
        private float ModTextWidth(ReadOnlySpan<char> text)
        {
            if (text.Length == 0)
            {
                return 0;
            }
            Font font = SetUpFont(text[0], set: false);
            float aspectFix = HudAspectFix;
            float width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\0')
                {
                    break;
                }
                width += font.Widths[GlyphIndex(font, text[i])] * aspectFix;
            }
            return width;
        }
    }
}
