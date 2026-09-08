using System;
using MphRead.Entities;
using MphRead.Mods.Launcher;
using MphRead.Mods.Network;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods
{
    /// <summary>
    /// Who to come back as, asked at the one moment there is nothing else to
    /// do: the results screen.
    ///
    /// The question used to live in the pause menu, two rows down a list of
    /// eight, and it was the wrong place for it in every way that matters.
    /// Opening the pause menu mid-match is a thing you do instead of playing,
    /// so the one screen where changing hunter is free was the one screen that
    /// never offered it -- and a drop-down row reading "Respawn as: SYLUX" is
    /// a settings control, not a choice anybody makes in the eight seconds
    /// between two maps. So both rows are gone from there, and the question is
    /// asked here instead, over the results, with the hunter's own portrait
    /// and an arrow either side of it.
    ///
    /// The answer is <see cref="RespawnChoice"/>'s, unchanged: it is cashed in
    /// at the next spawn, which after a match is the first spawn on the next
    /// map. Nothing here applies anything itself.
    ///
    /// <para>
    /// Only for a player. A demo has nobody to ask, a spectator has no player
    /// to change, and the adventure is one hunter's story.
    /// </para>
    /// </summary>
    public static class EndScreen
    {
        /// <summary>
        /// Whether the results screen is up and this machine has a player who
        /// could pick something.
        /// </summary>
        public static bool Available
        {
            get
            {
                if (!GameState.Multiplayer || GameState.MenuPause
                    || DemoPlayback.IsActive || SpectatorMode.IsSpectating
                    || SpectatorMode.FreeCamera || PlayerEntity.Main == null)
                {
                    return false;
                }
                return GameState.MatchState == MatchState.GameOver
                    || GameState.MatchState == MatchState.Ending;
            }
        }

        /// <summary>
        /// Whether this player has said they are ready for the next match.
        ///
        /// Read straight off the results screen by the intent packet each
        /// frame (IntentButtons.ReadyState) and by nothing else on this
        /// machine: the server is what shortens the wait, because it is the
        /// server that owns the rotation. Cleared when the screen goes, so it
        /// never carries into the next match.
        /// </summary>
        public static bool Ready { get; private set; }

        /// <summary>
        /// Forget the answer. Called when the results screen stops being
        /// available, which is the start of the next match.
        /// </summary>
        public static void ClearReady()
        {
            Ready = false;
        }

        public static void ToggleReady()
        {
            if (Available)
            {
                Ready = !Ready;
            }
        }

        /// <summary>The hunter queued for the next spawn, which is what is drawn.</summary>
        public static Hunter Hunter => RespawnChoice.Hunter;

        /// <summary>The suit queued for the next spawn, 0-3.</summary>
        public static int Suit => PlayerColors.Clamp(RespawnChoice.Color);

        /// <summary>
        /// The map the server says is next, or "" when there is no server or
        /// it has not said.
        ///
        /// <c>MatchStatePacket.NextRoomKey</c> has been on the wire since the
        /// rotation was written and nothing has ever read it. It is the one
        /// thing a results screen can say that a scoreboard cannot: everybody
        /// is about to be moved somewhere, and this is where they find out
        /// where.
        /// </summary>
        public static string NextRoomKey
        {
            get
            {
                MatchStatePacket? state = NetSession.ServerMatch;
                if (state == null)
                {
                    return "";
                }
                return state.Value.NextRoomKey ?? "";
            }
        }

        /// <summary>The next map's name as a person knows it, or its key.</summary>
        public static string NextRoomName
        {
            get
            {
                string key = NextRoomKey;
                if (key.Length == 0)
                {
                    return "";
                }
                try
                {
                    (RoomMetadata? meta, _) = Metadata.GetRoomByName(key);
                    return meta?.InGameName ?? key;
                }
                catch (Exception)
                {
                    return key;
                }
            }
        }

        // ------------------------------------------------------- the pointer

        /// <summary>
        /// A rectangle on the window, 0-1 each way.
        ///
        /// Kept in the window's own coordinates rather than the HUD's 256x192
        /// because that is what a mouse position arrives in, and because the
        /// HUD's horizontal unit is a different size on every window shape
        /// (see <c>HudAspectFix</c>) -- converting once, in the draw, is one
        /// place to be wrong instead of two.
        /// </summary>
        public readonly struct Hit
        {
            public readonly float Left;
            public readonly float Top;
            public readonly float Right;
            public readonly float Bottom;

            public Hit(float left, float top, float right, float bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public bool Contains(float x, float y)
            {
                return Right > Left && Bottom > Top
                    && x >= Left && x < Right && y >= Top && y < Bottom;
            }
        }

        /// <summary>
        /// What the panel put where, last time it was drawn.
        ///
        /// Published by the draw rather than worked out again here, so the
        /// boxes cannot drift from the picture: there is one layout, it is
        /// computed once a frame in <c>ModDrawEndScreen</c>, and this is a
        /// copy of it. Empty until the panel has been drawn at least once,
        /// which is also exactly when there is nothing to click.
        /// </summary>
        private static Hit _hitPrev;
        private static Hit _hitNext;
        private static Hit _hitReady;
        private static readonly Hit[] _hitSuits = new Hit[PlayerColors.Count];

        public static float PointerX { get; private set; } = -1;
        public static float PointerY { get; private set; } = -1;

        /// <summary>Called once a frame by the window, in window fractions.</summary>
        public static void NotePointer(float x, float y)
        {
            PointerX = x;
            PointerY = y;
        }

        public static void NoteLayout(Hit previous, Hit next, Hit[] suits, Hit ready = default)
        {
            _hitPrev = previous;
            _hitNext = next;
            _hitReady = ready;
            for (int i = 0; i < _hitSuits.Length && i < suits.Length; i++)
            {
                _hitSuits[i] = suits[i];
            }
        }

        /// <summary>Which suit swatch the pointer is over, or -1.</summary>
        public static int HoveredSuit()
        {
            if (!Available)
            {
                return -1;
            }
            for (int i = 0; i < _hitSuits.Length; i++)
            {
                if (_hitSuits[i].Contains(PointerX, PointerY))
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool HoveredPrev => Available && _hitPrev.Contains(PointerX, PointerY);
        public static bool HoveredNext => Available && _hitNext.Contains(PointerX, PointerY);
        public static bool HoveredReady => Available && _hitReady.Contains(PointerX, PointerY);

        /// <summary>
        /// A left click, offered before anything else sees it. Returns true
        /// when the picker took it.
        ///
        /// A suit is chosen by clicking it rather than by stepping through
        /// four of them, because there are four and they are all on screen --
        /// arrows are for the hunter, where there are seven and only one is
        /// shown at a time.
        /// </summary>
        public static bool HandleClick()
        {
            if (!Available)
            {
                return false;
            }
            if (_hitPrev.Contains(PointerX, PointerY))
            {
                Step(-1, 0);
                return true;
            }
            if (_hitNext.Contains(PointerX, PointerY))
            {
                Step(1, 0);
                return true;
            }
            if (_hitReady.Contains(PointerX, PointerY))
            {
                ToggleReady();
                return true;
            }
            for (int i = 0; i < _hitSuits.Length; i++)
            {
                if (_hitSuits[i].Contains(PointerX, PointerY))
                {
                    Choose(Hunter, i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A key press, taken before the game sees it. Returns true when it
        /// was one of ours, so nothing else acts on it.
        ///
        /// The arrow keys, and only while the results are up. Nothing is bound
        /// to them during a match, and there is nothing else to press here --
        /// every control the player has is switched off for the length of the
        /// end sequence -- so this claims no key anybody could want back.
        /// </summary>
        public static bool HandleKeyDown(Keys key)
        {
            if (!Available)
            {
                return false;
            }
            switch (key)
            {
                case Keys.Left:
                    Step(-1, 0);
                    return true;
                case Keys.Right:
                    Step(1, 0);
                    return true;
                case Keys.Up:
                    Step(0, -1);
                    return true;
                case Keys.Down:
                    Step(0, 1);
                    return true;
                case Keys.Enter:
                case Keys.KeyPadEnter:
                    ToggleReady();
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The same from a pad's d-pad, taken once a frame rather than from an
        /// event: GLFW reports a pad by polling, so there is no press to hook.
        /// </summary>
        public static void PollGamepad()
        {
            if (!Available)
            {
                return;
            }
            if (Input.GamepadInput.TakePress(Input.GamepadButtons.DpadLeft))
            {
                Step(-1, 0);
            }
            if (Input.GamepadInput.TakePress(Input.GamepadButtons.DpadRight))
            {
                Step(1, 0);
            }
            if (Input.GamepadInput.TakePress(Input.GamepadButtons.DpadUp))
            {
                Step(0, -1);
            }
            // A is the results screen's confirm, which is what Ready is.
            if (Input.GamepadInput.TakePress(Input.GamepadButtons.A))
            {
                ToggleReady();
            }
            if (Input.GamepadInput.TakePress(Input.GamepadButtons.DpadDown))
            {
                Step(0, 1);
            }
        }

        /// <summary>
        /// Move the choice and keep it.
        ///
        /// Written to <c>launcher.txt</c> on every press rather than on the
        /// way out, for the reason the pause menu did the same: a results
        /// screen is somewhere people alt-F4 from, and a choice made and lost
        /// is worse than no choice at all. It is a handful of writes across a
        /// ten-second screen, which is nothing next to what the match itself
        /// was doing a second ago.
        /// </summary>
        private static void Step(int hunterBy, int suitBy)
        {
            int hunter = (int)Launcher.Hunters.Resolve(Hunter);
            if (hunterBy != 0)
            {
                hunter = ((hunter + hunterBy) % Hunters.Playable + Hunters.Playable)
                    % Hunters.Playable;
            }
            int suit = Suit;
            if (suitBy != 0)
            {
                suit = ((suit + suitBy) % PlayerColors.Count + PlayerColors.Count)
                    % PlayerColors.Count;
            }
            Choose((Hunter)hunter, suit);
        }

        private static void Choose(Hunter hunter, int suit)
        {
            RespawnChoice.Request(hunter, suit);
            LauncherPrefs.LastHunter = hunter;
            LauncherPrefs.LastColor = suit;
            LauncherPrefs.Save();
        }
    }
}
