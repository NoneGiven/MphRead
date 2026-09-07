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
        private static readonly Vector4 _endSwatchHover = new Vector4(1, 1, 1, 0.28f);

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
            //
            // Drawn on a box each rather than as bare glyphs: they are
            // clickable now, and a target you can hit has to look like one.
            // The box is also the hit area, published below.
            float arrowY = portraitTop + EndPortrait / 2 - 6;
            EndScreen.Hit prev = DrawEndArrow(left + 2 * aspect, arrowY, "<",
                EndScreen.HoveredPrev, aspect);
            EndScreen.Hit forward = DrawEndArrow(right - (2 + EndArrowBox) * aspect, arrowY, ">",
                EndScreen.HoveredNext, aspect);

            DrawText2D(centre, portraitTop + EndPortrait + 1, Align.Center, palette: 0,
                ((Hunter)hunter).ToString().ToUpperInvariant(),
                color: _endInk, fontSpacing: 8, scale: 0.6f);

            float suitTop = portraitTop + EndPortrait + 11;
            DrawText2D(centre, suitTop, Align.Center, palette: 0, "SUIT",
                color: _endDim, fontSpacing: 8, scale: 0.45f);
            int suit = EndScreen.Suit;
            DrawEndSuits((Hunter)hunter, suit, left, suitTop + 6, aspect);
            // What was just drawn, in the window's own coordinates, so a click
            // is tested against the picture rather than against a second copy
            // of this arithmetic. See EndScreen.NoteLayout.
            EndScreen.NoteLayout(prev, forward, _endSuitHits);
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

        /// <summary>Side of an arrow's clickable box, in HUD height units.</summary>
        private const float EndArrowBox = 12;

        /// <summary>
        /// One arrow on its own box, lit while the pointer is over it, and the
        /// box handed back so it can be clicked.
        /// </summary>
        private EndScreen.Hit DrawEndArrow(float x, float y, string glyph, bool hovered, float aspect)
        {
            float rightEdge = x + EndArrowBox * aspect;
            float bottomEdge = y + EndArrowBox;
            _scene.DrawHudFlatBox(x, y, rightEdge, bottomEdge,
                hovered ? _endArrowHover : _endArrowWell);
            DrawText2D(x + EndArrowBox / 2 * aspect, y + 2, Align.Center, palette: 0, glyph,
                color: _endArrow, fontSpacing: 8, scale: 0.9f);
            return ModHudHit(x, y, rightEdge, bottomEdge);
        }

        /// <summary>
        /// A box in HUD units turned into one in window fractions, which is
        /// what a mouse position arrives in.
        ///
        /// The HUD's horizontal unit is 1/256 of the window and its vertical
        /// unit 1/192 of it, whatever shape the window is -- the aspect
        /// correction every measurement above carries is already baked into
        /// the numbers by the time they reach here, so this is a plain
        /// division and not a second correction.
        /// </summary>
        private static EndScreen.Hit ModHudHit(float left, float top, float right, float bottom)
        {
            return new EndScreen.Hit(left / 256f, top / 192f, right / 256f, bottom / 192f);
        }

        private static readonly Vector4 _endArrowWell = new Vector4(1, 1, 1, 0.10f);
        private static readonly Vector4 _endArrowHover = new Vector4(1, 0.84f, 0.35f, 0.32f);

        private readonly EndScreen.Hit[] _endSuitHits =
            new EndScreen.Hit[Mods.Network.PlayerColors.Count];

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
            int hoveredSuit = EndScreen.HoveredSuit();
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
                if (i == hoveredSuit && i != chosen)
                {
                    // A wash over the swatch rather than a ring around it: the
                    // chosen one already wears the ring, and two kinds of
                    // outline on one row is two things to tell apart.
                    _scene.DrawHudFlatBox(x, top, x + box * aspect, top + height,
                        _endSwatchHover);
                }
                // A slot's worth, not a swatch's: the gaps between four
                // squares are dead pixels in the middle of the one row people
                // will aim at, and there is nothing else to hit there.
                _endSuitHits[i] = ModHudHit(startX + i * slot * aspect, top - 1.5f,
                    startX + (i + 1) * slot * aspect, top + height + 1.5f);
            }
        }
    }
}
