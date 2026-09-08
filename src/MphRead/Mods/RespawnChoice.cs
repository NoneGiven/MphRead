using MphRead.Entities;
using MphRead.Mods.Network;

namespace MphRead.Mods
{
    /// <summary>
    /// Changing hunter, and suit, without leaving the match.
    ///
    /// The DS asked once, in the lobby, and that was the answer for the whole
    /// game -- which is fine for a cartridge in a room and wrong for a server
    /// that has been running the same rotation for an hour. Somebody who wants
    /// to try Sylux on this map, or who is losing to a Trace on a long sight
    /// line, had to leave, change, and rejoin: a minute out of the match, a
    /// free slot for somebody else to take, and their score reset. It was one
    /// of the most asked-for things here.
    ///
    /// **At the next respawn, and not before.** Swapping a live player's
    /// character mid-fight would be a way to escape a fight -- morph out of a
    /// bad matchup, keep the health you have, keep the position you took. So
    /// the choice is made whenever you like and cashed in at the one moment
    /// that costs something and gives everyone else a chance to see it: the
    /// respawn you were already having.
    ///
    /// The change is applied in <c>PlayerEntity.Spawn</c>, before it lays out
    /// the new life, so that call gives the *new* hunter its abilities, its
    /// energy tank and its HUD -- rather than after, which would hand Spire's
    /// rock attack to whoever was standing there.
    /// </summary>
    public static class RespawnChoice
    {
        private static Hunter? _hunter;
        private static int? _color;

        /// <summary>What is queued, for a menu to show back.</summary>
        public static Hunter Hunter => _hunter ?? PlayerEntity.Main?.Hunter ?? Hunter.Samus;

        public static int Color
            => _color ?? (PlayerEntity.Main != null && PlayerEntity.Main.SlotIndex >= 0
                ? PlayerColors.Choice[PlayerEntity.Main.SlotIndex]
                : Launcher.LauncherPrefs.LastColor);

        /// <summary>Forget anything queued. Called when a match is built.</summary>
        public static void Reset()
        {
            _hunter = null;
            _color = null;
            // The results screen's other answer, forgotten with these two and
            // for the same reason: it describes the match that just ended.
            EndScreen.ClearReady();
            // And the rolling clip buffer, which holds the match that just
            // ended. Nobody pressed the button during it, so nobody wanted it.
            // A full recording started from the menu is untouched: that has a
            // file of its own and was asked for deliberately.
            Network.DemoClip.Purge();
        }

        /// <summary>
        /// Ask to come back as this hunter, in this suit.
        ///
        /// Takes both together because they are one question -- who you are --
        /// and because a colour change on its own must not be lost behind a
        /// hunter change that arrives a moment later from the same menu.
        /// <see cref="Hunter.Random"/> is rolled here rather than passed on:
        /// the value announced to the server has to be a hunter somebody can
        /// draw.
        /// </summary>
        public static void Request(Hunter hunter, int color)
        {
            _hunter = Launcher.Hunters.Resolve(hunter);
            _color = PlayerColors.Clamp(color);
        }

        /// <summary>
        /// Apply what was asked for, at the top of a respawn.
        ///
        /// Only this machine's own player, and only in a match: the adventure
        /// has one hunter by design, and a remote slot's hunter is that
        /// player's own choice, arriving on the roster.
        /// </summary>
        public static void ApplyOnSpawn(PlayerEntity player)
        {
            if (player != PlayerEntity.Main || !GameState.Multiplayer)
            {
                return;
            }
            if (!_hunter.HasValue && !_color.HasValue)
            {
                return;
            }
            Hunter hunter = _hunter ?? player.Hunter;
            int color = _color ?? Color;
            _hunter = null;
            _color = null;
            if (hunter != player.Hunter)
            {
                // Initialize() is what rebuilds the models, the values, the
                // collision volume and this machine's HUD from the hunter --
                // and Spawn, which is a line below this in the caller, is what
                // then places and arms them. The order is the whole trick: the
                // other way round, the new hunter would inherit the old one's
                // abilities and energy for one life.
                player.ModSetHunter(hunter);
                player.Initialize();
            }
            if (player.SlotIndex >= 0 && player.SlotIndex < PlayerColors.Choice.Length)
            {
                PlayerColors.Choice[player.SlotIndex] = color;
            }
            // Everybody else learns both from the roster, which is what the
            // server rebuilds when an Identify says something new. Without
            // this the change would be real on this screen and invisible on
            // every other one.
            if (NetSession.Active)
            {
                NetSession.LocalHunter = hunter;
                NetSession.LocalColor = color;
                NetSession.SendIdentify();
            }
            PlayerColors.Resolve();
        }
    }
}
