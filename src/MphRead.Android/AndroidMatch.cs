using System;
using MphRead.Entities;
using MphRead.Mods.Launcher;
using MphRead.Mods.Network;
using OpenTK.Mathematics;

namespace MphRead.Droid
{
    /// <summary>
    /// A <see cref="LaunchPlan"/> turned into a loaded scene, on Android.
    ///
    /// The desktop's <see cref="MatchStart"/> does this and more -- it opens a
    /// <c>RenderWindow</c> and runs it to completion, which is a window and a
    /// loop this platform does not have. What is left when those two are taken
    /// away is what this file is: the same order, the same slot arithmetic and
    /// the same deference to the DS player cap, producing a
    /// <see cref="Scene"/> for <see cref="GameView"/> to drive.
    ///
    /// Kept beside the head rather than in Mods/Launcher for that reason:
    /// there is no second implementation of the *decisions* here, only of the
    /// three lines that own a window.
    /// </summary>
    internal static class AndroidMatch
    {
        /// <summary>Runs on the GL thread: everything below it touches GL.</summary>
        public static Scene Build(AndroidInput input, Vector2i size, LaunchPlan plan, Action close)
        {
            GameFiles.ApplyPaths();
            // Cheap once the binaries exist -- a file check per map -- and the
            // one place that is guaranteed to run before a room is loaded, so
            // a map added since the last launch is built rather than missing.
            AndroidMaps.EnsureBuilt();
            if (plan.Kind == LaunchKind.Demo)
            {
                return BuildDemo(input, size, plan, close);
            }
            if (plan.Kind == LaunchKind.Adventure)
            {
                // Its own path, for the reason MatchStart has one: the story
                // takes its room from the save slot rather than from the plan,
                // and it is the one kind of match that may write a save. Going
                // through the multiplayer path below is what this did before,
                // and an adventure plan carries an empty room key on purpose
                // -- so the story ended at "No room with this name is known."
                return BuildAdventure(input, size, plan, close);
            }
            // No slot means nothing can be written, which is what a match
            // needs -- the same reason MatchStart gives.
            Menu.SaveSlot = 0;
            var scene = new Scene(size, input.Keyboard, input.Mouse, _ => { }, close);
            if (NetSession.Active)
            {
                BuildNetworkedMatch(scene, plan);
            }
            else
            {
                // Offline the plan's mode is the match, so it is what decides
                // teams. GameState's own list rather than the mode's name:
                // Capture is a team mode that does not end in "Teams".
                AddLocalPlayers(scene, plan, GameState.IsTeamMode(plan.Mode));
                scene.AddRoom(plan.RoomKey, plan.Mode);
            }
            return scene;
        }

        /// <summary>
        /// A recorded match, played back -- the half of
        /// <c>MatchStart.LaunchDemo</c> that is not a window.
        ///
        /// The file is fed to <see cref="NetSession"/> exactly as a live
        /// connection would be, so every packet handler, room transition and
        /// match-end sequence runs unchanged; what makes it a replay rather
        /// than a match is that there is no local slot (-1) and so no player
        /// to spawn as. <see cref="Mods.SpectatorMode"/> takes the camera on the
        /// first frame anybody recorded becomes available.
        ///
        /// The room comes from the recording itself: a demo carries the
        /// server's own MatchState, which is what <see cref="NetLaunch.ServerRoom"/>
        /// reads. <see cref="DemoPlayback.Join"/> has already been called by
        /// the screen that picked the file -- it reports a bad file there,
        /// where there is still something to report on -- and calling it again
        /// here is what re-winds the reader for the run about to start.
        /// </summary>
        private static Scene BuildDemo(AndroidInput input, Vector2i size,
            LaunchPlan plan, Action close)
        {
            PlayerEntity.MaxPlayers = PlayerEntity.SlotCapacity;
            if (!DemoPlayback.Join(plan.DemoPath))
            {
                throw new ProgramException(DemoPlayback.LastError
                    ?? "That file could not be read as a demo.");
            }
            (string RoomKey, GameMode Mode)? room = NetLaunch.ServerRoom();
            if (room == null)
            {
                DemoPlayback.Stop();
                throw new ProgramException("The demo has no match info in it.");
            }
            Menu.SaveSlot = 0;
            var scene = new Scene(size, input.Keyboard, input.Mouse, _ => { }, close);
            NetLaunch.BuildPlayers(scene, Hunter.Samus, localRecolor: 0,
                teams: GameState.IsTeamMode(room.Value.Mode), localSlot: -1);
            scene.AddRoom(room.Value.RoomKey, room.Value.Mode, playerCount: NetLaunch.RoomPlayerCount);
            Console.WriteLine($"[match] demo, {room.Value.RoomKey}");
            return scene;
        }

        /// <summary>
        /// The story, from a save slot -- the half of
        /// <see cref="MatchStart"/>'s adventure path that is not a window.
        ///
        /// Everything decided here is decided there too, and for the same
        /// reasons: the slot is chosen before the save is read, because
        /// <see cref="GameState.CommitSave"/> writes nothing while
        /// <see cref="Menu.SaveSlot"/> is 0; the cap goes back to the four a
        /// DS game had, since an offline match in the same session may have
        /// raised it and the story's setup counts on the retail number; and
        /// one player is added, because the bot filling that the multiplayer
        /// path does means nothing here.
        ///
        /// The room comes out of <see cref="AdventureSave.Begin"/>: the
        /// slot's own checkpoint, or the Celestial Archives landing site for a
        /// new game.
        /// </summary>
        private static Scene BuildAdventure(AndroidInput input, Vector2i size,
            LaunchPlan plan, Action close)
        {
            string roomKey = AdventureSave.Begin(plan.SaveSlot, plan.NewGame);
            if (roomKey.Length == 0)
            {
                throw new ProgramException("That save slot does not name a room to load.");
            }
            GameState.Mode = GameMode.SinglePlayer;
            PlayerEntity.MaxPlayers = 4;
            var scene = new Scene(size, input.Keyboard, input.Mouse, _ => { }, close);
            scene.AddPlayer(plan.Hunter, recolor: 0, team: -1);
            scene.AddRoom(roomKey, GameMode.SinglePlayer);
            Console.WriteLine($"[match] adventure, slot {plan.SaveSlot}, "
                + $"{(plan.NewGame ? "new game" : "continued")}, room {roomKey}");
            return scene;
        }

        /// <summary>
        /// Write the save the session asked for, once the match is over.
        ///
        /// The desktop does this on the line after its render loop returns;
        /// this platform has no such line, so the render thread calls it as it
        /// tears the scene down. Safe to call after any match: it is
        /// <see cref="MatchStart.CommitAdventureSave"/>, which writes nothing
        /// unless a slot is selected, and only the story selects one.
        /// </summary>
        public static void Finish()
        {
            MatchStart.CommitAdventureSave();
        }

        /// <summary>
        /// The half of <see cref="MatchStart.Launch"/> that a joined session
        /// needs.
        ///
        /// The map is the server's, not the plan's: an online plan carries an
        /// empty room key on purpose, because the front screen joins before it
        /// knows what is running. Loading <c>plan.RoomKey</c> anyway is what
        /// made joining fail with "No room with this name is known" -- the
        /// empty string is not a room.
        /// </summary>
        private static void BuildNetworkedMatch(Scene scene, LaunchPlan plan)
        {
            (string RoomKey, GameMode Mode)? room = NetLaunch.ServerRoom();
            string roomKey = room?.RoomKey ?? plan.RoomKey;
            // The server's rotation decides the mode as well as the map; a
            // client that kept its own menu choice would score a different
            // game from everyone else on the same level.
            GameMode mode = room?.Mode ?? plan.Mode;
            if (roomKey.Length == 0)
            {
                throw new ProgramException("The server did not say which map it is running.");
            }
            // The server's mode, not the plan's: online it is the only thing
            // that may decide who is on which team, for the reason MatchStart
            // gives -- a client splitting an FFA server's slots into two halves
            // plays to a scoreboard nobody else on the server has. Which slot
            // lands on which side is BuildPlayers' own rule, the one
            // NetSlotManager uses, so every client agrees without being told.
            NetLaunch.BuildPlayers(scene, plan.Hunter, localRecolor: 0,
                teams: GameState.IsTeamMode(mode));
            scene.AddRoom(roomKey, mode, playerCount: NetLaunch.RoomPlayerCount);
        }

        private static void AddLocalPlayers(Scene scene, LaunchPlan plan, bool teamPlay)
        {
            int bots = Math.Clamp(plan.Bots, 0, PlayerEntity.SlotCapacity - 1);
            // Set rather than raise, for MatchStart's reason: the launcher comes
            // back between matches, and a seven-bot match must not leave the
            // next one at eight.
            PlayerEntity.MaxPlayers = Math.Max(4, bots + 1);
            scene.AddPlayer(plan.Hunter, recolor: 0, team: teamPlay ? 0 : -1);
            for (int i = 1; i <= bots; i++)
            {
                var hunter = (Hunter)(((int)plan.Hunter + i) % 7);
                scene.AddPlayer(hunter, recolor: 0, team: teamPlay ? i % 2 : -1);
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
            // Which slot the player is actually driving. Worth a line: only
            // the networked paths move MainPlayerIndex, and when one of them
            // left it pointing at slot 1 the next local match handed the
            // camera and the controls to a bot with nothing saying so.
            Console.WriteLine($"[match] local match, main player = slot "
                + $"{PlayerEntity.MainPlayerIndex}, bot = {PlayerEntity.Main.IsBot}");
        }
    }
}
