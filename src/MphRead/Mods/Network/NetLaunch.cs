using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Everything a networked session needs done between "join" and "run",
    /// shared by the Windows launcher and the -connect command line.
    ///
    /// It lives here rather than in the launcher because none of it is
    /// Windows-specific and because two copies of this sequence is exactly
    /// how the two entry points drifted apart before: the launcher built its
    /// player slots one way, the test harness another, and the bug only
    /// existed in the path nobody was testing.
    /// </summary>
    public static class NetLaunch
    {
        /// <summary>
        /// Join a server and wait for it to say what is running.
        ///
        /// Both halves matter. The room key is what makes joining mid-match
        /// work -- the server owns the rotation, so loading whatever the menu
        /// had selected would put this client in a different level from
        /// everyone else. The slot matters because <see cref="BuildPlayers"/>
        /// keys off it, and starting before the Welcome arrived left a second
        /// client with no entity in its own slot.
        /// </summary>
        public static bool Join(string address, int port, string playerName, Hunter hunter,
            int timeoutMs = 8000, int color = -1)
        {
            NetSession.PlayerName = playerName;
            // Rolled here as well as in the launch plan, because joining
            // happens *before* the plan is built: the hunter announced in
            // Identify is what every other client draws this player as, and
            // Hunter.Random has no model for anybody to draw.
            NetSession.LocalHunter = Launcher.Hunters.Resolve(hunter);
            // The suit travels with the hunter, and for the same reason: it is
            // announced in Identify, before any launch plan exists. -1 means
            // "whatever this player last chose", which is every caller but the
            // command line's -recolor.
            NetSession.LocalColor = PlayerColors.Clamp(
                color < 0 ? Launcher.LauncherPrefs.LastColor : color);
            // A networked match is not limited to the four the DS could hold:
            // the server decides how many it admits, and every client has to
            // be able to hold that many slots for it to matter.
            PlayerEntity.MaxPlayers = PlayerEntity.SlotCapacity;
            NetSession.StartClient(address, port);
            if (!NetSession.Active)
            {
                LastJoinError = $"Could not open a socket for {address}:{port}.";
                return false;
            }
            var clock = Stopwatch.StartNew();
            int lastIdentify = 0;
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                NetSession.Update(clock.Elapsed.TotalSeconds);
                if (NetSession.Refused)
                {
                    // The server answered the Hello with a no. Nothing is
                    // gained by spending the rest of the eight seconds asking
                    // again, and the player gets the actual reason instead of
                    // a list of three.
                    LastJoinError = NetSession.RefusedReason.Describe($"{address}:{port}");
                    Console.WriteLine($"[net] {LastJoinError}");
                    return false;
                }
                if (NetSession.LocalSlot >= 0 && NetSession.ServerMatch?.RoomKey.Length > 0)
                {
                    MatchStatePacket state = NetSession.ServerMatch.Value;
                    Console.WriteLine($"[net] joining {state.RoomKey} ({(GameMode)state.Mode}), "
                        + $"{state.TimeRemaining:0} s remaining, slot {NetSession.LocalSlot}");
                    DisableCheatsForMatch();
                    return true;
                }
                // The name is what the roster keys off, and the first
                // Identify can be lost like any other datagram; a client whose
                // name never landed shows up on everyone else's scoreboard as
                // "PlayerN" for the rest of the match.
                if (clock.ElapsedMilliseconds - lastIdentify > 500)
                {
                    lastIdentify = (int)clock.ElapsedMilliseconds;
                    NetSession.SendIdentify();
                }
                Thread.Sleep(20);
            }
            LastJoinError = DescribeJoinFailure(address, port);
            Console.WriteLine($"[net] {LastJoinError}");
            return false;
        }

        /// <summary>
        /// Why the last <see cref="Join"/> failed, in a sentence a player can
        /// act on. Empty before the first failure.
        /// </summary>
        public static string LastJoinError { get; private set; } = "";

        /// <summary>
        /// Turn a silence into an answer.
        ///
        /// A server refuses a Hello by ignoring it -- when it is full, and
        /// when the client is a different build -- so all three of "off",
        /// "full" and "wrong version" reach the client as the same eight
        /// seconds of nothing, and every screen in this program guessed all
        /// three at once: "it may be off, full, or UDP may be blocked."
        ///
        /// The server does answer a StatusQuery in two of those three cases,
        /// and that reply carries both the player count and the protocol
        /// version, so the guess is unnecessary: ask, and say which it was.
        /// Client-side on purpose -- it works against servers already
        /// deployed, which an explicit refusal packet would not.
        /// </summary>
        private static string DescribeJoinFailure(string address, int port)
        {
            ServerStatus status = NetStatus.Query(address, port,
                allowJoinProbe: false, timeoutMs: 1500);
            if (!status.Online)
            {
                return $"No answer from {address}:{port}. The server may be off, "
                    + "or UDP may be blocked between here and it.";
            }
            if (status.MaxPlayers > 0 && status.Players >= status.MaxPlayers)
            {
                return $"{address}:{port} is full ({status.Players}/{status.MaxPlayers} "
                    + "players). Try again when somebody leaves.";
            }
            if (status.Protocol > 0 && status.Protocol != NetConfig.ProtocolVersion)
            {
                return $"{address}:{port} is running protocol {status.Protocol} and this "
                    + $"build speaks {NetConfig.ProtocolVersion}. One of you needs updating.";
            }
            return $"{address}:{port} answered, but would not admit this client "
                + $"({status.Players}/{status.MaxPlayers} players). "
                + "It may have filled up while joining.";
        }

        /// <summary>
        /// Turn every cheat off for the duration of a networked match.
        ///
        /// They are loaded from settings.json for every session, single or
        /// networked -- and `FreeWeaponSelect` even defaults to on -- and in a
        /// networked one they are not a private choice: the authority resolves
        /// damage and collision for everybody, so one player's Quadruple
        /// Damage multiplies what everyone deals or takes, depending only on
        /// who happened to connect first.
        ///
        /// All of them rather than the four that obviously matter: the list
        /// grows as upstream develops, and "which of these leaks into a match"
        /// is not a question worth re-answering every time it does. Single
        /// player is untouched -- these are restored from the settings file on
        /// the next launch.
        /// </summary>
        public static void DisableCheatsForMatch()
        {
            var turnedOff = new List<string>();
            foreach (PropertyInfo property in typeof(Cheats).GetProperties(
                BindingFlags.Public | BindingFlags.Static))
            {
                if (property.PropertyType != typeof(bool) || !property.CanRead || !property.CanWrite)
                {
                    continue;
                }
                if ((bool)(property.GetValue(null) ?? false))
                {
                    turnedOff.Add(property.Name);
                    property.SetValue(null, false);
                }
            }
            if (turnedOff.Count > 0)
            {
                string list = String.Join(", ", turnedOff);
                Console.WriteLine($"[net] cheats are off while connected ({list})");
                NetLog.Event($"cheats disabled for this session: {list}");
            }
        }

        /// <summary>
        /// The room and mode this client should load, or null when the server
        /// has not said.
        /// </summary>
        public static (string RoomKey, GameMode Mode)? ServerRoom()
        {
            MatchStatePacket? state = NetSession.ServerMatch;
            if (state == null || state.Value.RoomKey.Length == 0)
            {
                return null;
            }
            GameMode mode = Enum.IsDefined(typeof(GameMode), state.Value.Mode)
                ? (GameMode)state.Value.Mode
                : GameMode.Battle;
            return (state.Value.RoomKey, mode);
        }

        /// <summary>
        /// Entity layer to load a networked room with.
        ///
        /// Fixed rather than derived from how many players happen to be
        /// connected: SceneSetup picks the room's entity layout from the
        /// player count, so a client that joined alone and one that joined
        /// into a full match would lay out different spawn points, doors and
        /// items for the same map. Everyone loads the two-player layout, so
        /// everyone gets the same world.
        /// </summary>
        public const int RoomPlayerCount = 2;

        /// <summary>
        /// Create one player entity per slot, before the room loads.
        ///
        /// All four, always. Scene.AddPlayer is inert once the room has
        /// loaded, and Scene.AddRoom only lists players that are SlotActive
        /// at that moment, so a slot not built and listed here can never be
        /// filled later -- which is exactly why a client that started alone
        /// could never materialise anybody who joined afterwards. The
        /// unoccupied ones are left inactive: invisible, unsimulated, and
        /// waiting for NetSlotManager to switch them on.
        /// </summary>
        /// <param name="localSlot">
        /// Which slot is "this machine's own player" -- defaults to
        /// <see cref="NetSession.LocalSlot"/> for a real connection. Demo
        /// playback passes -1 explicitly: there is no local player during
        /// playback, and without this every slot-0 hunter, recolour and
        /// occupancy check below silently clamped to slot 0 (from
        /// <c>Math.Max(NetSession.LocalSlot, 0)</c>, since LocalSlot stays
        /// -1 for the whole session) -- overwriting slot 0's real recorded
        /// hunter with whatever dummy one the playback call site passed, and
        /// exempting it alone from the "not occupied, clear Active" pass a
        /// few lines down.
        /// </param>
        public static void BuildPlayers(Scene scene, Hunter localHunter, int localRecolor,
            int teamId = -1, int? localSlot = null)
        {
            int resolvedSlot = localSlot ?? Math.Max(NetSession.LocalSlot, 0);
            for (int slot = 0; slot < PlayerEntity.MaxPlayers; slot++)
            {
                // Only this machine's hunter is a local choice. Everyone
                // else's comes from the server's roster, because it is their
                // choice, not a row in this client's menu: building slot N
                // from the menu's "player N" meant a client on slot 1 played
                // whatever its own P2 row said while announcing its P1 row,
                // and two clients sharing a settings file both ended up
                // showing the same hunter for everybody.
                Hunter hunter = slot == resolvedSlot ? localHunter : NetSession.SlotHunter[slot];
                // The suit is only a starting value here. Every slot's choice
                // -- this machine's from its own preference, everybody else's
                // from the roster -- is settled by PlayerColors.Resolve, which
                // runs every frame and is what keeps two players of one hunter
                // apart.
                if (slot == resolvedSlot && slot >= 0 && slot < PlayerColors.Choice.Length)
                {
                    PlayerColors.Choice[slot] = PlayerColors.Clamp(localRecolor);
                }
                scene.AddPlayer(hunter, slot == resolvedSlot ? localRecolor : 0, teamId);
            }
            for (int slot = 0; slot < PlayerEntity.MaxPlayers; slot++)
            {
                PlayerEntity? player = slot < PlayerEntity.Players.Count
                    ? PlayerEntity.Players[slot]
                    : null;
                if (player == null)
                {
                    continue;
                }
                // No AI anywhere in a networked match: Scene.AddPlayer flags
                // every player after the first as a bot, and PlayerAi would
                // then overwrite the Controls that relayed intent fills in.
                player.IsBot = false;
                player.BotLevel = 0;
                if (slot == resolvedSlot)
                {
                    continue;
                }
                bool occupied = slot < NetSession.SlotOccupied.Length
                    && NetSession.SlotOccupied[slot];
                if (!occupied)
                {
                    // Active only. SlotActive stays on so AddRoom lists the
                    // entity and OnLoad initialises it; without that the slot
                    // is absent from the scene rather than merely empty.
                    player.LoadFlags &= ~LoadFlags.Active;
                }
            }
            PlayerEntity.PlayerCount = 1;
            // Before AddRoom: the room loader initialises the camera and HUD
            // against PlayerEntity.Main, so Main must already point at the
            // slot this client drives. A client on slot 1 that skipped this
            // was never its own main player -- its intro sequence never
            // ended, so it kept the spectator camera and never spawned.
            //
            // resolvedSlot itself may be -1 (demo playback, no local player
            // at all) -- Main still has to be a real array index, so this
            // falls back to slot 0 as a harmless placeholder that
            // SpectatorMode.Start immediately redirects once a real player
            // is available, same as it does after every subsequent cycle.
            int mainIndex = resolvedSlot >= 0 ? resolvedSlot : 0;
            PlayerEntity.MainPlayerIndex = mainIndex;
            PlayerColors.Resolve();
            // A hunter queued for a respawn belongs to the match it was asked
            // in. Carried into the next one it would override the choice the
            // launcher was just used to make.
            RespawnChoice.Reset();
            Console.WriteLine($"[net] player slots built, main player = slot {mainIndex}");
            NetLog.Event($"player slots built, main = slot {mainIndex}");
        }
    }
}
