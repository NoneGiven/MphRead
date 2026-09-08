using System;
using System.Text;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Periodic report of what the running game believes about a networked
    /// session.
    ///
    /// This exists because the interesting failure is invisible from the
    /// wire: two clients can be perfectly connected -- correct slots, agreed
    /// clock -- while each one's scene contains only its own player, so
    /// nobody sees anybody and the scoreboard lists one name. Only the game
    /// process can see that, so it reports it here rather than a test trying
    /// to reconstruct the engine's state from outside.
    ///
    /// Printed to the console once a second while NDS_NET_DEBUG is set, or
    /// whenever -netdebug is passed.
    /// </summary>
    public static class NetDiagnostics
    {
        private static double _lastReport;
        private static bool _enabled;
        private static bool _checked;

        public static bool Enabled
        {
            get
            {
                if (!_checked)
                {
                    _checked = true;
                    _enabled = Environment.GetEnvironmentVariable("MPHREAD_NET_DEBUG") != null;
                }
                return _enabled;
            }
            set
            {
                _checked = true;
                _enabled = value;
            }
        }

        public static void Report(double time)
        {
            if (!Enabled || !NetSession.Active)
            {
                return;
            }
            if (time - _lastReport < 1.0)
            {
                return;
            }
            _lastReport = time;

            var line = new StringBuilder();
            line.Append("[netdbg] role=").Append(NetSession.Role);
            line.Append(" slot=").Append(NetSession.LocalSlot);

            int active = 0;
            int created = 0;
            line.Append(" slots=[");
            for (int i = 0; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity? player = PlayerEntity.Players[i];
                if (player == null)
                {
                    line.Append('-');
                    continue;
                }
                created++;
                bool isActive = player.LoadFlags.TestFlag(LoadFlags.Active);
                if (isActive)
                {
                    active++;
                }
                // A = active player, B = active but AI-driven (wrong on a
                // remote slot -- PlayerAi would overwrite network intent),
                // s = slot-active only, . = present but inactive.
                line.Append(isActive ? (player.IsBot ? 'B' : 'A')
                    : player.LoadFlags.TestFlag(LoadFlags.SlotActive) ? 's' : '.');
            }
            line.Append("] active=").Append(active);
            line.Append(" scoreboard=").Append(GameState.ActivePlayers);
            line.Append(" created=").Append(created);

            line.Append(" remoteState=[");
            for (int i = 0; i < NetSession.RemoteStateValid.Length; i++)
            {
                line.Append(NetSession.RemoteStateValid[i] ? 'y' : 'n');
            }
            line.Append("] remoteIntent=[");
            for (int i = 0; i < NetSession.RemoteIntentValid.Length; i++)
            {
                line.Append(NetSession.RemoteIntentValid[i] ? 'y' : 'n');
            }
            line.Append(']');

            // Surface the failure directly rather than leaving it to be
            // spotted in the slot map: an AI-driven remote slot is always a
            // bug, and it is the one that produced bots in a networked match.
            int botRemotes = 0;
            for (int i = 0; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity? p = PlayerEntity.Players[i];
                if (p != null && i != NetSession.LocalSlot && p.IsBot
                    && p.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    botRemotes++;
                }
            }
            if (botRemotes > 0)
            {
                line.Append("  !! ").Append(botRemotes).Append(" remote slot(s) still AI-driven");
            }

            // The team index every weapon that refuses to hurt a team mate
            // reads, printed as the number it actually is rather than as the
            // mode it was supposed to come from. In a free-for-all these must
            // all differ: a bomb and a homing beam both treat "same team" as
            // "not a target", so one repeated value here is a whole class of
            // weapons silently doing nothing.
            line.Append(" team=[");
            for (int i = 0; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity? p = PlayerEntity.Players[i];
                if (i > 0)
                {
                    line.Append(',');
                }
                line.Append(p == null ? "-" : p.TeamIndex.ToString());
            }
            line.Append(']');
            line.Append(" shockcoil=").Append(NetDamage.ShockCoilAcquired)
                .Append('/').Append(NetDamage.ShockCoilSpawned);
            line.Append(" bomb=").Append(NetDamage.BombHits)
                .Append('/').Append(NetDamage.BombPlayerChecks)
                .Append(" bombTeamSkips=").Append(NetDamage.BombTeamSkips);
            // What actually hurt somebody, by weapon. Only the weapons that
            // landed anything, so the line stays readable and a name missing
            // from it is the finding.
            line.Append(" dmg[");
            bool first = true;
            for (int i = 0; i < NetDamage.HitsByBeam.Length; i++)
            {
                if (NetDamage.HitsByBeam[i] == 0)
                {
                    continue;
                }
                if (!first)
                {
                    line.Append(' ');
                }
                first = false;
                line.Append((BeamType)i).Append('=').Append(NetDamage.DamageByBeam[i])
                    .Append('/').Append(NetDamage.HitsByBeam[i]);
            }
            if (NetDamage.BombDamageHits > 0)
            {
                if (!first)
                {
                    line.Append(' ');
                }
                line.Append("Bomb=").Append(NetDamage.BombDamageDealt)
                    .Append('/').Append(NetDamage.BombDamageHits);
            }
            line.Append(']');
            line.Append(" bombSpawn=").Append(NetDamage.BombSpawnMade)
                .Append('/').Append(NetDamage.BombSpawnCalls)
                .Append(" det=").Append(NetDamage.BombSpawnDetonated)
                .Append(" stale=").Append(NetDamage.BombSpawnStaleCount)
                .Append(" poolEmpty=").Append(NetDamage.BombSpawnPoolEmpty);
            line.Append(" bombNearest=")
                .Append(NetDamage.BombNearest == Single.MaxValue ? "n/a"
                    : NetDamage.BombNearest.ToString("0.00"))
                .Append(" bombRadius=").Append(NetDamage.BombRadiusSeen.ToString("0.00"));
            // Zero on a healthy session. Anything else is a rotation where
            // one machine was still loading and this client refused to be put
            // where the previous room said. See NetPlayerBridge.
            if (NetPlayerBridge.PlacementsRefused > 0)
            {
                line.Append(" placementsRefused=").Append(NetPlayerBridge.PlacementsRefused);
            }

            MatchStatePacket? match = NetSession.ServerMatch;
            if (match != null)
            {
                line.Append(" serverMap=").Append(match.Value.RoomKey);
                line.Append(" serverPlayers=").Append(match.Value.PlayerCount);
            }
            Console.WriteLine(line.ToString());
        }
    }
}
