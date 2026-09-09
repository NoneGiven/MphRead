using System;
using MphRead.Hud;
using MphRead.Mods.Network;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    /// <summary>
    /// The map vote, on screen.
    ///
    /// Small, and out of the way of both the middle of the screen and
    /// whatever else the platform has put in a corner -- see
    /// <see cref="VoteLeft"/>. It is a question asked in the middle of a
    /// match: it has to be readable without stopping, and it must not sit
    /// where a player is looking. Everything it draws is read out of
    /// <see cref="MapVote"/>, which is a copy of what the server last said --
    /// nothing here decides anything about the vote, including whether this
    /// player has already answered it.
    ///
    /// Two ways to answer, and which one is offered depends on what the
    /// machine has. A keyboard gets F1 and F2 and is told so; a touchscreen
    /// gets two flat rectangles, because a phone has no function keys and a
    /// prompt it cannot answer is worse than no prompt. The rectangles are
    /// deliberately plain -- a translucent green and a translucent red, no
    /// border, no icon -- since they are drawn over a running match and
    /// anything louder would be in the way for the thirty seconds they exist.
    /// </summary>
    public partial class PlayerEntity
    {
        /// <summary>
        /// Where the panel's top-left corner goes.
        ///
        /// The top-left corner of the HUD on a desktop, and a good way in and
        /// down from it on a phone -- because on a phone that corner is not
        /// free. MENU, SCORE and CHAT are drawn on the glass at 0.12 of the
        /// height with a radius of 0.06, so the three of them own everything
        /// above y 37 in HUD units, and the pro HUD's weapon column owns the
        /// left edge (out to x 46) from y 46 down. The panel used to be drawn
        /// underneath all of that: the prompt was legible only in the gaps
        /// between three circles, and every tap meant for ACCEPT pressed MENU.
        /// Clearing the row costs nothing on any window shape, since the row's
        /// height is a fraction of the screen's and so is fixed in these
        /// units.
        ///
        /// The right-hand edge is the other constraint, and it is what sets
        /// <see cref="VoteButtonScale"/>: WEAPON reaches x 216 and y 45 on a
        /// 4:3 tablet, where a HUD unit across is a HUD unit down, so the
        /// panel has to end before it.
        /// </summary>
        private static float VoteLeft => OperatingSystem.IsAndroid() ? 52 : 6;
        private static float VoteTop => OperatingSystem.IsAndroid() ? 42 : 8;
        private const float VoteLineHeight = 8;

        /// <summary>
        /// How much bigger the two answers are drawn where they are pressed
        /// with a thumb rather than clicked. Same reasoning as
        /// <c>EndScale</c>: a fingertip is about nine millimetres and these
        /// were laid out for a pointer a pixel wide.
        /// </summary>
        private static float VoteButtonScale => OperatingSystem.IsAndroid() ? 1.35f : 1f;

        private static float VoteButtonWidth => 52 * VoteButtonScale;
        private static float VoteButtonHeight => 13 * VoteButtonScale;
        private static float VoteButtonGap => 4 * VoteButtonScale;

        private static readonly Vector4 _votePanel = new Vector4(0, 0, 0, 0.42f);
        private static readonly Vector4 _voteAccept = new Vector4(0.24f, 0.78f, 0.33f, 0.40f);
        private static readonly Vector4 _voteAcceptLit = new Vector4(0.30f, 0.92f, 0.40f, 0.60f);
        private static readonly Vector4 _voteDeny = new Vector4(0.85f, 0.24f, 0.24f, 0.40f);
        private static readonly Vector4 _voteDenyLit = new Vector4(0.98f, 0.32f, 0.32f, 0.60f);
        private static readonly ColorRgba _voteInk = new ColorRgba(235, 238, 245, 255);
        private static readonly ColorRgba _voteDimInk = new ColorRgba(170, 178, 190, 255);

        /// <summary>
        /// Whether this build answers a vote by touch rather than by key.
        ///
        /// The platform rather than a setting: a desktop with a touchscreen
        /// still has the function keys, and the reason the buttons exist is
        /// that a phone does not.
        /// </summary>
        private static bool VoteByTouch => OperatingSystem.IsAndroid();

        internal void ModDrawVote()
        {
            if (!MapVote.Active || !IsMainPlayer)
            {
                return;
            }
            // Not over the results screen. That panel is the middle of the
            // screen and its own set of things to click, and a vote drawn on
            // top of it is two prompts competing for one pointer.
            if (Mods.EndScreen.Available)
            {
                MapVote.NoteLayout(default, default);
                return;
            }
            float aspect = HudAspectFix;
            string prompt = MapVote.PromptLine();
            string tally = MapVote.TallyLine();
            bool buttons = VoteByTouch && !MapVote.Answered;
            float height = VoteLineHeight * 2 + 4
                + (buttons ? VoteButtonHeight + VoteButtonGap : 0);
            // Wide enough for the prompt, and never narrower than the two
            // buttons under it plus their gap.
            float width = Math.Max(118f, VoteButtonWidth * 2 + VoteButtonGap + 6);
            float right = VoteLeft + width * aspect;
            _scene.DrawHudFlatBox(VoteLeft, VoteTop, right, VoteTop + height, _votePanel);
            DrawText2D(VoteLeft + 3 * aspect, VoteTop + 2, Align.Left, palette: 0,
                prompt, color: _voteInk, fontSpacing: 8, scale: 0.42f);
            DrawText2D(VoteLeft + 3 * aspect, VoteTop + 2 + VoteLineHeight, Align.Left,
                palette: 0, tally, color: _voteDimInk, fontSpacing: 8, scale: 0.42f);
            if (!buttons)
            {
                MapVote.NoteLayout(default, default);
                return;
            }
            float top = VoteTop + VoteLineHeight * 2 + 4;
            float bottom = top + VoteButtonHeight;
            float acceptLeft = VoteLeft + 3 * aspect;
            float acceptRight = acceptLeft + VoteButtonWidth * aspect;
            float denyLeft = acceptRight + VoteButtonGap * aspect;
            float denyRight = denyLeft + VoteButtonWidth * aspect;
            Mods.EndScreen.Hit accept = ModVoteHit(acceptLeft, top, acceptRight, bottom);
            Mods.EndScreen.Hit deny = ModVoteHit(denyLeft, top, denyRight, bottom);
            bool overAccept = accept.Contains(Mods.EndScreen.PointerX, Mods.EndScreen.PointerY);
            bool overDeny = deny.Contains(Mods.EndScreen.PointerX, Mods.EndScreen.PointerY);
            _scene.DrawHudFlatBox(acceptLeft, top, acceptRight, bottom,
                overAccept ? _voteAcceptLit : _voteAccept);
            _scene.DrawHudFlatBox(denyLeft, top, denyRight, bottom,
                overDeny ? _voteDenyLit : _voteDeny);
            // Centred in the box rather than a fixed drop from its top, so
            // the label stays in the middle of a button drawn at any size.
            float labelY = top + (VoteButtonHeight - 16 * 0.42f * VoteButtonScale) / 2;
            DrawText2D((acceptLeft + acceptRight) / 2, labelY, Align.Center, palette: 0,
                "ACCEPT", color: _voteInk, fontSpacing: 8, scale: 0.42f * VoteButtonScale);
            DrawText2D((denyLeft + denyRight) / 2, labelY, Align.Center, palette: 0,
                "DENY", color: _voteInk, fontSpacing: 8, scale: 0.42f * VoteButtonScale);
            MapVote.NoteLayout(accept, deny);
        }

        /// <summary>
        /// The same conversion <c>ModHudHit</c> makes: HUD units into the
        /// window fractions the pointer is reported in.
        /// </summary>
        private static Mods.EndScreen.Hit ModVoteHit(float left, float top, float right, float bottom)
        {
            return new Mods.EndScreen.Hit(left / 256f, top / 192f, right / 256f, bottom / 192f);
        }
    }
}
