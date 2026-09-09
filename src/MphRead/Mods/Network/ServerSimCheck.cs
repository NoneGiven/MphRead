using System;
using System.Diagnostics;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// The headless simulation, run on its own with nobody connected, so the
    /// two questions a Pi asks can be answered before a match is put on one:
    /// how much memory does simulating a room cost, and does a step fit in
    /// 16.7 ms.
    ///
    /// It is not a network test -- <c>run-check.sh</c> is that. Every intent
    /// here is synthesised locally, so what it measures is the *simulation*:
    /// the room, the players in it, their shots and the snapshot composed at
    /// the end of every step. That is exactly the part that used to run on a
    /// player's PC and now has to run on a server, so it is the part whose
    /// cost was never known.
    ///
    /// Usage: -simcheck "ROOM" [-players N] [-seconds N] [-mode M]
    /// </summary>
    public static class ServerSimCheck
    {
        public static int Run(string room, int players, double seconds, GameMode mode)
        {
            players = Math.Clamp(players, 1, PlayerEntity.SlotCapacity);
            Console.WriteLine($"[simcheck] \"{room}\" ({mode}), {players} player(s), {seconds:0} s");
            long snapshotBytes = 0;
            long snapshots = 0;
            var sim = new ServerSim();
            if (!ServerSim.Available(out string why))
            {
                Console.WriteLine($"[simcheck] cannot simulate: {why}");
                return 1;
            }
            long beforeLoad = WorkingSetBytes();
            var loadClock = Stopwatch.StartNew();
            long matchEnds = 0;
            if (!sim.Start(room, mode, players, payload =>
            {
                snapshots++;
                snapshotBytes += payload.Length;
            }, () => matchEnds++))
            {
                return 1;
            }
            loadClock.Stop();
            long afterLoad = WorkingSetBytes();
            // Everybody in, before the first step: a slot the roster does not
            // mention is inactive, and an inactive slot is not simulated, so
            // an empty roster would measure an empty room.
            ApplyRoster(players);
            int steps = (int)Math.Round(seconds * 60);
            var driver = new IntentDriver(players);
            var wall = Stopwatch.StartNew();
            for (int i = 0; i < steps; i++)
            {
                driver.Feed((uint)(i + 1));
                sim.Step();
            }
            wall.Stop();
            long afterRun = WorkingSetBytes();
            int spawned = 0;
            for (int i = 0; i < players && i < PlayerEntity.Players.Count; i++)
            {
                if (PlayerEntity.Players[i].LoadFlags.TestFlag(LoadFlags.Spawned))
                {
                    spawned++;
                }
            }
            double meanStep = sim.Frames > 0 ? sim.StepSeconds / sim.Frames * 1000 : 0;
            // A step is owed 16.7 ms. What matters is not the mean but the
            // headroom: a box whose mean is 8 ms and whose worst is 40 drops
            // frames for everybody at once, and it is the worst that decides.
            double budget = meanStep / (1000 / 60.0) * 100;
            Console.WriteLine();
            Console.WriteLine($"SIMCHECK {room} | players {players} | steps {sim.Frames}"
                + $" | spawned {spawned}/{players}"
                + $" | snapshots {snapshots} ({(snapshots > 0 ? snapshotBytes / (double)snapshots : 0):0} B mean)"
                + (matchEnds > 0 ? " | match ended" : "")
                + $" | load {loadClock.Elapsed.TotalSeconds:0.0} s"
                + $" | step mean {meanStep:0.00} ms ({budget:0}% of budget)"
                + $" worst {sim.WorstStepSeconds * 1000:0.0} ms overrun {sim.OverrunSteps}"
                + $" | wall {wall.Elapsed.TotalSeconds:0.0} s for {seconds:0} s simulated"
                + $" | rss {Mb(beforeLoad)}->{Mb(afterLoad)}->{Mb(afterRun)} MB"
                + $" | peak {Mb(PeakWorkingSetBytes())} MB");
            sim.Stop();
            // The one thing this can fail on: a room that loaded and then
            // could not put anybody in it is a room the server cannot host,
            // and it is worth an exit code so a sweep over every map can be a
            // shell loop.
            return spawned == players ? 0 : 1;
        }

        private static void ApplyRoster(int players)
        {
            RosterPacket roster = RosterPacket.Create();
            for (int i = 0; i < players && i < RosterPacket.MaxSlots; i++)
            {
                roster.Slots[roster.Count] = (byte)i;
                // A different hunter per slot, cycling: eight copies of Samus
                // would measure one collision volume and one set of weapons.
                roster.Hunters[roster.Count] = (byte)(i % 7);
                roster.Colors[roster.Count] = 0;
                roster.Pings[roster.Count] = 0;
                roster.Names[roster.Count] = $"SIM{i + 1}";
                roster.Count++;
            }
            NetSession.ApplyRoster(roster);
        }

        /// <summary>
        /// Eight players walking a circle and firing, as intents.
        ///
        /// Not <see cref="NetTestScript"/>, which writes a player's Controls
        /// directly: on the server every slot without exception is driven from
        /// <see cref="NetSession.RemoteIntents"/>, and going in by any other
        /// door would exercise a path no real match uses.
        /// </summary>
        private sealed class IntentDriver
        {
            private readonly int _players;
            private readonly Vector3[] _at;

            public IntentDriver(int players)
            {
                _players = players;
                _at = new Vector3[players];
            }

            public void Feed(uint frame)
            {
                for (int slot = 0; slot < _players; slot++)
                {
                    PlayerEntity player = slot < PlayerEntity.Players.Count
                        ? PlayerEntity.Players[slot]
                        : null!;
                    // Where the engine actually put them. The position field
                    // is the owner's claim, and an owner who claimed a circle
                    // around the origin would be claiming a point inside the
                    // level's walls on most maps -- which is a test of the
                    // desync backstop, not of a match.
                    if (player != null && player.LoadFlags.TestFlag(LoadFlags.Spawned))
                    {
                        _at[slot] = player.Position;
                    }
                    double turn = frame / 60.0 + slot;
                    var aim = new Vector3((float)Math.Cos(turn), 0, (float)Math.Sin(turn));
                    var buttons = IntentButtons.MoveUp;
                    if (frame % 20 < 6)
                    {
                        buttons |= IntentButtons.Shoot;
                    }
                    NetSession.AcceptSlotIntent(slot, new IntentPacket
                    {
                        Frame = frame,
                        Buttons = buttons,
                        Presses = new uint[IntentPacket.PressHistory],
                        Aim = aim,
                        Position = _at[slot],
                        WeaponSelect = 0xFF,
                        AmmoUa = 400,
                        AmmoMissiles = 50,
                        // Behind by a tenth of a second, so the rewind is
                        // exercised rather than skipped: an ack of zero means
                        // "do not compensate", which is the one case a server
                        // measuring itself must not accidentally measure.
                        AckFrame = frame > 6 ? frame - 6 : 0
                    });
                }
            }
        }

        private static long WorkingSetBytes()
        {
            using var self = Process.GetCurrentProcess();
            self.Refresh();
            return self.WorkingSet64;
        }

        private static long PeakWorkingSetBytes()
        {
            using var self = Process.GetCurrentProcess();
            self.Refresh();
            return self.PeakWorkingSet64;
        }

        private static string Mb(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0");
    }
}
