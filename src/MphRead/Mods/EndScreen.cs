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
            RespawnChoice.Request((Hunter)hunter, suit);
            LauncherPrefs.LastHunter = (Hunter)hunter;
            LauncherPrefs.LastColor = suit;
            LauncherPrefs.Save();
        }
    }
}
