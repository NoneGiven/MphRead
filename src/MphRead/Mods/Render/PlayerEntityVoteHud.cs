using System;
using MphRead.Hud;
using MphRead.Mods.Network;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    /// <summary>
    /// The map vote, on screen.
    ///
    /// Top left, and small. It is a question asked in the middle of a match:
    /// it has to be readable without stopping, and it must not sit where a
    /// player is looking. Everything it draws is read out of
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
        private const float VoteLeft = 6;
        private const float VoteTop = 8;
        private const float VoteLineHeight = 8;
        private const float VoteButtonWidth = 52;
        private const float VoteButtonHeight = 13;
        private const float VoteButtonGap = 4;

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
            float right = VoteLeft + Math.Max(118f, 0f) * aspect;
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
            DrawText2D((acceptLeft + acceptRight) / 2, top + 3, Align.Center, palette: 0,
                "ACCEPT", color: _voteInk, fontSpacing: 8, scale: 0.42f);
            DrawText2D((denyLeft + denyRight) / 2, top + 3, Align.Center, palette: 0,
                "DENY", color: _voteInk, fontSpacing: 8, scale: 0.42f);
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
