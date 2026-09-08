using System;
using MphRead.Entities;
using MphRead.Mods.Network;

namespace MphRead.Mods.Launcher
{
    /// <summary>
    /// Turning a <see cref="LaunchPlan"/> into a running match.
    ///
    /// This used to sit inside <c>LauncherEntry</c>, which is WinForms and
    /// therefore Windows. Nothing in it is: it opens a <see cref="RenderWindow"/>,
    /// fills the player slots and loads a room, all of which the Linux build
    /// has always been able to do. Having it here is what lets
    /// <see cref="TextLauncher"/> start exactly the match the window would have
    /// started -- the same bot count, the same cap on the slots, the same
    /// deference to what the server says it is running -- instead of a second
    /// implementation that agrees with it until it does not.
    /// </summary>
    public static class MatchStart
    {
        /// <summary>
        /// Load what the plan asked for and run until the match ends.
        ///
        /// For an online or hosted game the front screen has already joined --
        /// hosting included, because a host runs the server in this process and
        /// joins it over the loopback like everybody else -- so all that is
        /// left is to load what the server says is running.
        /// </summary>
        public static void Launch(MenuSettings settings, LaunchPlan plan)
        {
            if (!GameFiles.Ready)
            {
                Console.WriteLine("[launcher] no game files; nothing to load");
                return;
            }
            GameFiles.ApplyPaths();
            // The custom maps, here rather than only in ModEntry.TryHandle.
            // A launcher session never reaches TryHandle: the front screen is
            // dispatched from TryHandleHeadless, which returns as soon as it
            // has run one, so on Windows -- where double-clicking the binary
            // *is* the launcher -- nothing ever built the binaries a custom
            // room is made of. The room is registered from its JSON either
            // way, so it sat in the map picker and took the process down the
            // moment somebody picked it. This is the last point before a room
            // is loaded, and the first at which the game files are known to be
            // there, which is what generating needs.
            MapGen.CustomRooms.GenerateMissing();
            if (plan.Kind == LaunchKind.Adventure)
            {
                LaunchAdventure(plan);
                return;
            }
            if (plan.Kind == LaunchKind.Demo)
            {
                LaunchDemo(plan);
                return;
            }
            // No slot means nothing can be written, which is what a match
            // needs: leaving the story's slot selected would let a multiplayer
            // session's exit commit whatever it had done to the shared
            // StorySave over a real save file.
            Menu.SaveSlot = 0;
            if (plan.Kind == LaunchKind.Online || plan.Kind == LaunchKind.Host)
            {
                (string RoomKey, GameMode Mode)? room = NetLaunch.ServerRoom();
                if (room != null)
                {
                    settings.RoomKey = room.Value.RoomKey;
                }
                else
                {
                    Console.WriteLine("[net] no map reported by the server; "
                        + "loading the selected map instead");
                }
            }

            string roomKey = plan.Kind == LaunchKind.Offline
                ? plan.RoomKey
                : settings.RoomKey;
            if (roomKey.Length == 0 || roomKey == "none")
            {
                return;
            }

            // A custom map that failed to build is still a room in the table --
            // the launcher lists it and the picker shows a frame for it -- and
            // loading one reaches for binaries that are not there. On Windows
            // that is a process with no console dying on a null path, which
            // says nothing to the player and nothing to anybody debugging it.
            string? unplayable = MapGen.CustomRooms.WhyUnplayable(roomKey);
            if (unplayable != null)
            {
                Console.WriteLine($"[launcher] {unplayable}");
                return;
            }

            RenderWindow.LogCreatingWindow();
            using var renderer = new RenderWindow();
            // The server's rotation decides the mode as well as the map; a
            // client that kept its own menu choice would score a different
            // game from everyone else on the same level. Settled before the
            // players are built, because who is on which team follows from
            // it -- this used to be read afterwards, so a client joining a
            // team server took its team decision from its own launcher.
            GameMode mode = plan.Mode;
            if (NetSession.Active && NetLaunch.ServerRoom() is { } serverRoom)
            {
                mode = serverRoom.Mode;
            }
            // GameState's own list, not the mode's name: Capture is a team
            // mode that does not end in "Teams", and testing the name left
            // every player and bot in a Capture match on no team at all.
            //
            // Online the match's own mode is the only thing that may answer
            // this. A local "team play" preference splitting an FFA server's
            // slots into two halves would give this client a scoreboard
            // nobody else on the server is playing to.
            bool teamPlay = NetSession.Active
                ? GameState.IsTeamMode(mode)
                : settings.TeamPlay == "on" || GameState.IsTeamMode(plan.Mode);

            if (NetSession.Active)
            {
                NetLaunch.BuildPlayers(renderer.Scene, plan.Hunter,
                    localRecolor: LauncherPrefs.LastColor, teams: teamPlay);
            }
            else
            {
                AddLocalPlayers(renderer, plan, teamPlay);
            }
            renderer.AddRoom(roomKey, mode, playerCount: NetSession.Active
                ? NetLaunch.RoomPlayerCount
                : 0);
            renderer.Run();
        }

        /// <summary>
        /// The story, from a save slot.
        ///
        /// Nothing here is new engine work: adventure mode is what
        /// <see cref="GameMode.SinglePlayer"/> has always meant, and the room
        /// to open comes out of the slot's own checkpoint. The two things the
        /// launcher has to get right are choosing the slot before the save is
        /// read -- <see cref="GameState.CommitSave"/> writes nothing while
        /// <see cref="Menu.SaveSlot"/> is 0 -- and asking for one player, since
        /// the multiplayer path's bot filling has no meaning here.
        /// </summary>
        private static void LaunchAdventure(LaunchPlan plan)
        {
            string roomKey = AdventureSave.Begin(plan.SaveSlot, plan.NewGame);
            if (roomKey.Length == 0)
            {
                Console.WriteLine("[launcher] no adventure room to load");
                return;
            }
            GameState.Mode = GameMode.SinglePlayer;
            RenderWindow.LogCreatingWindow();
            using (var renderer = new RenderWindow())
            {
                // Back to the four a DS game had: a previous offline match in
                // the same session may have raised this to eight, and the
                // story's own setup counts on the retail number.
                PlayerEntity.MaxPlayers = 4;
                renderer.AddPlayer(plan.Hunter, recolor: LauncherPrefs.LastColor, team: -1);
                renderer.AddRoom(roomKey, GameMode.SinglePlayer);
                renderer.Run();
            }
            CommitAdventureSave();
        }

        /// <summary>
        /// Watch a recorded match. Joins the demo file the same way
        /// <see cref="NetLaunch.Join"/> joins a live server -- blocking until
        /// the first recorded packets say what room and mode were being
        /// played -- then loads that room exactly like a normal online
        /// match, so every mode-specific and room-specific setup runs
        /// unchanged. <see cref="Mods.SpectatorMode"/> is entered as soon as
        /// a player becomes available, since there is no local player to
        /// spawn as here.
        /// </summary>
        private static void LaunchDemo(LaunchPlan plan)
        {
            PlayerEntity.MaxPlayers = PlayerEntity.SlotCapacity;
            if (!DemoPlayback.Join(plan.DemoPath))
            {
                Console.WriteLine("[demo] could not open or read the demo file");
                return;
            }
            (string RoomKey, GameMode Mode)? room = NetLaunch.ServerRoom();
            if (room == null)
            {
                Console.WriteLine("[demo] the demo has no match info");
                DemoPlayback.Stop();
                return;
            }
            Menu.SaveSlot = 0;
            RenderWindow.LogCreatingWindow();
            using var renderer = new RenderWindow();
            NetLaunch.BuildPlayers(renderer.Scene, Hunter.Samus, localRecolor: 0,
                teams: GameState.IsTeamMode(room.Value.Mode), localSlot: -1);
            renderer.AddRoom(room.Value.RoomKey, room.Value.Mode, playerCount: NetLaunch.RoomPlayerCount);
            renderer.Run();
            DemoPlayback.Stop();
        }

        /// <summary>
        /// Write the save the session asked for.
        ///
        /// The game does not write its own save: reaching the ship sets
        /// <see cref="Menu.NeededSave"/> and something else is expected to act
        /// on it once the window has closed. That something was the console
        /// menu's loop, which a launcher-started session never returns
        /// through -- so the story ran, asked to be saved, and lost everything
        /// on exit. This is that step, on the path the launcher does take.
        ///
        /// A prompt is honoured as a yes. The front screens have no console to
        /// ask on, and the two settings that reach here already carry the
        /// answer that matters: leaving through the ship asks (so it saves),
        /// and quitting outright defaults to Never (so it does not).
        ///
        /// Public because Android has the same problem and must not grow a
        /// second answer to it: there the render thread calls this as it tears
        /// the scene down, which is the same moment as the line below the
        /// render loop here.
        /// </summary>
        public static void CommitAdventureSave()
        {
            if (Menu.NeededSave != SaveWhen.Never && Menu.SaveSlot != 0)
            {
                GameState.CommitSave();
            }
            Menu.NeededSave = SaveWhen.Never;
        }

        /// <summary>
        /// One human and however many bots were asked for.
        ///
        /// Bots are ordinary players whose Controls are written by PlayerAi,
        /// which is what Scene.AddPlayer already arranges for every player
        /// after the first -- so the only work here is choosing who they are.
        /// A different hunter each keeps a practice match from being a room
        /// full of one's own reflection, and alternating teams is what makes
        /// the Teams modes mean anything offline.
        /// </summary>
        private static void AddLocalPlayers(RenderWindow renderer, LaunchPlan plan,
            bool teamPlay)
        {
            int bots = Math.Clamp(plan.Bots, 0, PlayerEntity.SlotCapacity - 1);
            // PlayerEntity.Create refuses every slot past MaxPlayers, and the
            // offline default is still the four a DS match could hold, so
            // asking for seven opponents would silently produce three. Set
            // rather than raise: the launcher comes back between matches now,
            // and a seven-bot match must not leave the next one at eight.
            PlayerEntity.MaxPlayers = Math.Max(4, bots + 1);
            // The player's own suit, and the first one for each bot: they are
            // each a different hunter (see below), so nobody collides and
            // there is nothing for PlayerColors to resolve offline.
            renderer.AddPlayer(plan.Hunter, recolor: LauncherPrefs.LastColor,
                team: teamPlay ? 0 : -1);
            for (int i = 1; i <= bots; i++)
            {
                var hunter = (Hunter)(((int)plan.Hunter + i) % 7);
                renderer.AddPlayer(hunter, recolor: 0, team: teamPlay ? i % 2 : -1);
            }
            int level = Math.Clamp(plan.BotLevel, 0, 2);
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (player.IsBot)
                {
                    player.BotLevel = level;
                }
            }
        }
    }
}
