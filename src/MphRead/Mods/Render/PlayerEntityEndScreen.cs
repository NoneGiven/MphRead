using System;
using MphRead.Hud;
using MphRead.Mods;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    /// <summary>
    /// The results screen's own corner: who you come back as, in what suit,
    /// and where everybody is going next.
    ///
    /// A partial of PlayerEntity for the same reason the pro HUD and the chat
    /// box are -- the text and box drawing are private to the HUD, and this
    /// needs both. <see cref="EndScreen"/> owns the state and the input; this
    /// draws it and decides nothing.
    ///
    /// Top right, because the two things already on this screen are the
    /// winner's camera behind everything and the scoreboard down the middle,
    /// and the corner opposite the match clock is the one piece of the frame
    /// neither of them uses.
    /// </summary>
    public partial class PlayerEntity
    {
        private static readonly Vector4 _endPanel = new Vector4(0, 0, 0, 0.62f);
        private static readonly Vector4 _endPanelEdge = new Vector4(1, 1, 1, 0.16f);
        private static readonly Vector4 _endSwatchEdge = new Vector4(1, 1, 1, 0.9f);
        private static readonly Vector4 _endSwatchWell = new Vector4(0, 0, 0, 0.45f);
        private static readonly ColorRgba _endInk = new ColorRgba(235, 238, 245, 255);
        private static readonly ColorRgba _endDim = new ColorRgba(165, 174, 190, 255);
        private static readonly ColorRgba _endArrow = new ColorRgba(255, 215, 90, 255);

        /// <summary>Panel geometry, in HUD units measured off the screen's height.</summary>
        private const float EndPanelWidth = 74;
        private const float EndPanelTop = 4;
        private const float EndPanelHeight = 82;
        private const float EndPortrait = 26;

        internal void ModDrawEndScreen()
        {
            if (!EndScreen.Available)
            {
                return;
            }
            float aspect = HudAspectFix;
            float right = 254;
            float left = right - EndPanelWidth * aspect;
            float centre = left + EndPanelWidth / 2 * aspect;
            float bottom = EndPanelTop + EndPanelHeight;
            _scene.DrawHudFlatBox(left, EndPanelTop, right, bottom, _endPanel);
            // A hairline down the inside edge. What is behind this is a lit
            // room and a scoreboard, and a panel with no edge on it reads as
            // a dark patch of the map rather than as something to look at.
            _scene.DrawHudFlatBox(left, EndPanelTop, left + 0.6f * aspect, bottom, _endPanelEdge);

            DrawText2D(centre, EndPanelTop + 2, Align.Center, palette: 0, "CHOOSE HUNTER",
                color: _endDim, fontSpacing: 8, scale: 0.5f);

            int hunter = Math.Clamp((int)EndScreen.Hunter, 0, Mods.Launcher.Hunters.Playable - 1);
            float portraitTop = EndPanelTop + 9;
            // The portraits are built with the rest of the HUD, so this is
            // never null in a match -- and a results screen is not the place
            // to find out that some path reached here before the HUD was set
            // up. The rest of the panel is still worth drawing without it.
            HudObjectInstance? portrait = hunter < _hunterInsts.Length
                ? _hunterInsts[hunter]
                : null;
            if (portrait != null)
            {
                // Mode 1 and a scale off the frame's own 32 units, so the
                // picture keeps its shape on any window -- the same reasoning
                // as the weapon list and the target-info portrait, see
                // DrawOpponent.
                portrait.Alpha = 1;
                portrait.PositionX = (centre - EndPortrait / 2 * aspect) / 256f;
                portrait.PositionY = portraitTop / 192f;
                _scene.DrawHudObject(portrait, mode: 1, scale: EndPortrait / 32f);
            }

            // The arrows are the whole instruction. There is no line of text
            // saying which key to press, because there is no room for one and
            // because a left arrow beside a picture has never needed one.
            float arrowY = portraitTop + EndPortrait / 2 - 5;
            DrawText2D(left + 4 * aspect, arrowY, Align.Left, palette: 0, "<",
                color: _endArrow, fontSpacing: 8, scale: 0.9f);
            DrawText2D(right - 4 * aspect, arrowY, Align.Right, palette: 0, ">",
                color: _endArrow, fontSpacing: 8, scale: 0.9f);

            DrawText2D(centre, portraitTop + EndPortrait + 1, Align.Center, palette: 0,
                ((Hunter)hunter).ToString().ToUpperInvariant(),
                color: _endInk, fontSpacing: 8, scale: 0.6f);

            float suitTop = portraitTop + EndPortrait + 11;
            DrawText2D(centre, suitTop, Align.Center, palette: 0, "SUIT",
                color: _endDim, fontSpacing: 8, scale: 0.45f);
            int suit = EndScreen.Suit;
            DrawEndSuits((Hunter)hunter, suit, left, suitTop + 6, aspect);
            DrawText2D(centre, suitTop + 16, Align.Center, palette: 0,
                Mods.HunterSuits.Name(Mods.HunterSuits.Color((Hunter)hunter, suit)),
                color: _endInk, fontSpacing: 8, scale: 0.45f);

            string next = EndScreen.NextRoomName;
            if (next.Length > 0)
            {
                DrawText2D(centre, bottom - 7, Align.Center, palette: 0,
                    $"NEXT: {next.ToUpperInvariant()}",
                    color: _endDim, fontSpacing: 8, scale: 0.45f);
            }
        }

        /// <summary>
        /// The four suits as four blocks of their own colour, the chosen one
        /// ringed.
        ///
        /// Colours read out of the hunter's own model rather than named here
        /// -- see <see cref="Mods.HunterSuits"/> -- because the four are not
        /// the same four for every hunter, and a row of boxes labelled 1 to 4
        /// answers nothing anybody is asking.
        /// </summary>
        private void DrawEndSuits(Hunter hunter, int chosen, float left, float top, float aspect)
        {
            const float slot = 16;
            const float box = 11;
            const float height = 8;
            float startX = left + (EndPanelWidth - slot * Mods.Network.PlayerColors.Count) / 2 * aspect;
            for (int i = 0; i < Mods.Network.PlayerColors.Count; i++)
            {
                float x = startX + (i * slot + (slot - box) / 2) * aspect;
                ColorRgba color = Mods.HunterSuits.Color(hunter, i);
                if (i == chosen)
                {
                    _scene.DrawHudFlatBox(x - 1.5f * aspect, top - 1.5f,
                        x + (box + 1.5f) * aspect, top + height + 1.5f, _endSwatchEdge);
                }
                else
                {
                    _scene.DrawHudFlatBox(x - 1 * aspect, top - 1,
                        x + (box + 1) * aspect, top + height + 1, _endSwatchWell);
                }
                _scene.DrawHudFlatBox(x, top, x + box * aspect, top + height,
                    new Vector4(color.Red / 255f, color.Green / 255f, color.Blue / 255f, 1));
            }
        }
    }
}
