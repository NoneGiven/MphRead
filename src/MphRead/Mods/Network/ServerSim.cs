using System;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Mods.Input;
using OpenTK.Mathematics;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// The match, simulated by the server that relays it.
    ///
    /// The authority was never a property of being a player. It is a property
    /// of being the machine everyone else's intent is pointed at, and until
    /// now that machine was whichever client joined first -- because a server
    /// with no game files cannot run the engine, and a server that ran a
    /// *reimplementation* of the engine would be a second answer free to
    /// disagree with the first. Both of those are still true. What changed is
    /// that the engine's simulation turns out to need no GL context at all:
    /// <c>Scene.OnSimulationFrame</c> and everything under it -- input, the
    /// entity step, collision, beams, damage -- contains no GL call anywhere,
    /// the split having already put every one of them in
    /// <c>Scene.OnDrawFrame</c>. So the server can run the real engine, and
    /// the "second answer" objection disappears: it is the same answer,
    /// compiled from the same file.
    ///
    /// What this buys, in order of how much it matters:
    ///
    /// - **Nobody is at zero latency.** The authority resolved its own shots
    ///   against its own present and everybody else's against a rewind
    ///   (<see cref="NetUnlagged"/>); it was the one player in the match who
    ///   could not be wrong about where anyone was. Now every player,
    ///   including whoever used to be slot 0, is compensated by exactly their
    ///   own round trip and nobody is compensated by zero.
    /// - **The match stops depending on a player's machine.** No handover when
    ///   the authority leaves, no stand-down when their line blips, no
    ///   half-second of nobody simulating while the server picks a successor.
    /// - **The scoreboard has one author.** Kills, points and the end of the
    ///   match are decided where the clock already lived.
    ///
    /// What it does **not** buy, and must not be sold as: a player still does
    /// not see their own hit register any sooner. Damage is felt when the
    /// snapshot carrying it arrives, which is a round trip after the trigger,
    /// wherever the authority sits -- moving it to the server equalises that
    /// wait, it does not shorten it. Shortening it is client-side hit
    /// prediction and is a separate piece of work.
    ///
    /// Nor does it make the server authoritative over *movement*:
    /// <see cref="IntentPacket.Position"/> is still where its sender says they
    /// are, exactly as it was when a client held this role. That is a
    /// deliberate non-change -- deriving position from buttons is what the
    /// intent stream was built to stop doing, and undoing it needs client
    /// prediction first. See <c>.claude/multiplayer/NETWORK-SERVERAUTH.md</c>.
    /// </summary>
    public sealed class ServerSim
    {
        private Scene? _scene;
        private string _room = "";
        private GameMode _mode = GameMode.Battle;

        /// <summary>Simulation steps run since this sim was started.</summary>
        public long Frames { get; private set; }

        /// <summary>
        /// Total time spent inside <see cref="Step"/>, for the one question an
        /// operator actually has: is this box keeping up with 60 Hz.
        /// </summary>
        public double StepSeconds { get; private set; }

        /// <summary>Longest single step, which is what a stutter is made of.</summary>
        public double WorstStepSeconds { get; private set; }

        /// <summary>Steps that took longer than the 16.7 ms they were owed.</summary>
        public long OverrunSteps { get; private set; }

        /// <summary>Steps that threw. Non-zero is a bug, not a slow machine.</summary>
        public long StepFailures { get; private set; }

        /// <summary>Steps the accumulator gave up on because it fell behind.</summary>
        public long DroppedSteps { get; private set; }

        /// <summary>Times the accumulator was reset rather than paid off.</summary>
        public long Stalls { get; private set; }

        private double _accumulator;
        private double _lastAdvance = -1;

        /// <summary>
        /// Run whatever steps the wall clock says are owed.
        ///
        /// The same fixed-step accumulator the game window runs
        /// (<see cref="Render.FrameTiming"/>) and for the same reason: every
        /// timer in this engine is counted in frames, so the simulation is
        /// pinned at exactly 60 Hz whatever the machine underneath it is
        /// doing. A server's loop is driven by arriving packets rather than by
        /// a display, so it wakes at no fixed rate at all -- which is
        /// precisely the case an accumulator exists for.
        ///
        /// Its own rather than FrameTiming's: that one is a singleton the
        /// render window owns, and it also measures the *draw* rate, which
        /// here would be a measurement of nothing.
        /// </summary>
        public void Advance(double now)
        {
            if (_scene == null)
            {
                return;
            }
            if (_lastAdvance < 0)
            {
                _lastAdvance = now;
                return;
            }
            double elapsed = now - _lastAdvance;
            _lastAdvance = now;
            if (elapsed > StallSeconds)
            {
                // Not a slow pass: a stall. Paying off four seconds of steps
                // would take longer than four seconds and the debt would grow.
                Stalls++;
                _accumulator = 0;
                return;
            }
            _accumulator += elapsed;
            int steps = 0;
            while (_accumulator >= Render.FrameTiming.StepSeconds
                && steps < Render.FrameTiming.MaxCatchUpSteps)
            {
                _accumulator -= Render.FrameTiming.StepSeconds;
                Step();
                steps++;
            }
            if (_accumulator >= Render.FrameTiming.StepSeconds)
            {
                // Owed more than the ceiling allows. Dropped rather than
                // carried, exactly as the game window drops them: a server
                // that cannot hold 60 Hz should run slow and say so, not
                // accumulate a debt it will never pay.
                DroppedSteps += (long)(_accumulator / Render.FrameTiming.StepSeconds);
                _accumulator = 0;
            }
        }

        /// <summary>
        /// Longer than this between passes is a stall, not a slow pass. A
        /// quarter of a second, as the window's accumulator uses.
        /// </summary>
        private const double StallSeconds = 0.25;

        public bool Running => _scene != null;

        /// <summary>
        /// The room the simulation is in *now*, which after a rotation is not
        /// the one it was started with.
        ///
        /// Read off the scene rather than remembered, because a rotation is a
        /// room transition inside the same scene (`NetRoomChange`) and nothing
        /// tells this class it happened. Remembering it made the server report
        /// its starting map for the rest of its life, which reads exactly like
        /// a server whose simulation failed to follow the rotation -- and the
        /// clients had followed it perfectly.
        /// </summary>
        public string Room
        {
            get
            {
                if (_scene == null)
                {
                    return "";
                }
                string current = Metadata.GetRoomById(_scene.RoomId, noThrow: true)?.Name ?? "";
                return current.Length > 0 ? current : _room;
            }
        }

        /// <summary>
        /// Whether this machine could simulate at all, asked before a socket
        /// is opened rather than discovered on the first join.
        ///
        /// A dedicated server has always been the one build that needs no game
        /// files, and that stays true: without them this returns false and the
        /// server runs as the relay it has always been, pointing the authority
        /// at a client. Simulating is an upgrade a server operator opts into
        /// by having a dump on the box, not a new requirement.
        /// </summary>
        public static bool Available(out string reason)
        {
            try
            {
                string root = MphRead.Paths.FileSystem;
                if (String.IsNullOrEmpty(root) || !System.IO.Directory.Exists(root))
                {
                    reason = "no game files are set up on this machine (see paths.txt)";
                    return false;
                }
            }
            catch (Exception ex)
            {
                reason = $"game files could not be located ({ex.Message})";
                return false;
            }
            if (!SyntheticInput.Available(out string inputReason))
            {
                reason = $"a keyboard could not be synthesised ({inputReason})";
                return false;
            }
            reason = "";
            return true;
        }

        /// <summary>
        /// Build the world and take the authority.
        ///
        /// The order is the launcher's, and for the launcher's reasons:
        /// players exist before the room does, because
        /// <c>Scene.AddRoom</c> only lists the slots that are already there
        /// and <c>Scene.AddPlayer</c> is inert once it has run. The one
        /// difference is <c>localSlot: -1</c> -- there is no player at this
        /// keyboard, so no slot is exempt from being a puppet.
        /// </summary>
        public bool Start(string roomKey, GameMode mode, int maxPlayers,
            SnapshotSink sink, Action matchEnded)
        {
            Stop();
            Mods.Headless.Enter();
            try
            {
                _room = roomKey;
                _mode = mode;
                // Before any player is created. Offline this is four, a DS
                // match's cap, and Create hands back null past it -- which
                // would leave a server advertising eight slots and simulating
                // the first four.
                PlayerEntity.MaxPlayers = Math.Clamp(maxPlayers, 2, PlayerEntity.SlotCapacity);
                NetSession.StartServerAuthority(sink, matchEnded);
                // A size, because the scene divides by it when it builds a
                // projection. Nothing here ever builds one; this is the DS's
                // own, so a stray aspect ratio is at least the right one.
                var scene = new Scene(new Vector2i(256, 192),
                    SyntheticInput.CreateKeyboard(), SyntheticInput.CreateMouse(),
                    _ => { }, () => { });
                // Samus for every unoccupied slot, as a placeholder only: each
                // slot's real hunter arrives on the roster and PlayerColors
                // settles it every frame thereafter, the same as on a client.
                NetLaunch.BuildPlayers(scene, Hunter.Samus, localRecolor: 0,
                    teams: GameState.IsTeamMode(mode), localSlot: -1);
                scene.AddRoom(roomKey, mode, playerCount: NetLaunch.RoomPlayerCount);
                scene.OnLoad();
                _scene = scene;
                Frames = 0;
                StepSeconds = 0;
                WorstStepSeconds = 0;
                OverrunSteps = 0;
                StepFailures = 0;
                DroppedSteps = 0;
                Stalls = 0;
                _accumulator = 0;
                _lastAdvance = -1;
                return true;
            }
            catch (Exception ex)
            {
                // The whole exception, not its message. A server that cannot
                // load a room is a server nobody can play on, and the one
                // thing worth knowing is which of the load's steps it died in
                // -- on a machine where nobody will be attaching a debugger.
                Console.WriteLine($"[sim] could not load \"{roomKey}\": {ex}");
                NetLog.Event($"server simulation failed to start: {ex}");
                Stop();
                return false;
            }
        }

        /// <summary>
        /// One 60 Hz step of the real engine.
        ///
        /// <c>OnSimulationFrame</c> and not <c>OnUpdateFrame</c>: the second
        /// one draws, and there is nothing here to draw with. Everything the
        /// authority owes the match happens inside this call -- the intents
        /// that arrived are applied, every player is stepped, shots are
        /// resolved against the rewound world, damage is recorded, and
        /// <c>NetHooks.AfterSimulation</c> publishes the snapshot through the
        /// sink.
        /// </summary>
        public void Step()
        {
            if (_scene == null)
            {
                return;
            }
            long start = Stopwatch.GetTimestamp();
            try
            {
                _scene.OnSimulationFrame();
            }
            catch (Exception ex)
            {
                // A server must not die of one bad frame. A client that throws
                // here takes down one player's game; this would take down
                // everybody's match and the relay with it.
                //
                // The first one gets its stack and the rest get a line: a
                // fault in the step usually repeats sixty times a second, so
                // printing the trace every time buries the trace.
                StepFailures++;
                if (StepFailures == 1)
                {
                    Console.WriteLine($"[sim] step failed: {ex}");
                }
                else
                {
                    Console.WriteLine($"[sim] step failed: {ex.Message}");
                }
                NetLog.Event($"server simulation step failed: {ex}");
            }
            double elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
            Frames++;
            StepSeconds += elapsed;
            if (elapsed > WorstStepSeconds)
            {
                WorstStepSeconds = elapsed;
            }
            if (elapsed > 1 / 60.0)
            {
                OverrunSteps++;
            }
        }

        public void Stop()
        {
            if (_scene == null)
            {
                return;
            }
            _scene = null;
            _room = "";
            NetSession.Stop();
            // The room's models, collision and entity lists, which are held in
            // a static cache keyed by path: without this a rotation through
            // twenty maps keeps all twenty.
            Read.ClearCache();
            GC.Collect(generation: 2, GCCollectionMode.Forced, blocking: true, compacting: true);
        }

        /// <summary>
        /// What lag compensation did, for the periodic server report.
        ///
        /// This has to be printed here or it is printed nowhere. Every
        /// `-netcheck` report carries the rewind figures, and every one of
        /// them now reads "nothing to compensate" -- correctly, because no
        /// client is the authority any more. The one machine that rewinds
        /// anything is this one, and it writes no report; without this the
        /// measurement that says lag compensation is working at all
        /// disappeared the moment the authority moved.
        ///
        /// The number to read is the mean rewind against the round trips of
        /// the players connected: see NETWORK-UNLAGGED.md.
        /// </summary>
        public string DescribeUnlagged() => NetUnlagged.Describe();

        /// <summary>One line for the periodic server report.</summary>
        public string Describe()
        {
            if (_scene == null)
            {
                return "not simulating";
            }
            double mean = Frames > 0 ? StepSeconds / Frames * 1000 : 0;
            return $"{Room} ({GameState.Mode}), {Frames} step(s), "
                + $"{mean:0.00} ms mean, {WorstStepSeconds * 1000:0.0} ms worst, "
                + $"{OverrunSteps} overrun, {DroppedSteps} dropped, {Stalls} stall(s)"
                + (StepFailures > 0 ? $", {StepFailures} FAILED" : "");
        }
    }
}
