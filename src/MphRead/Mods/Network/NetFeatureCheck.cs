using System;
using System.Collections.Generic;
using System.Text;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Watches a networked match feature by feature and says which parts of a
    /// player actually reach the other machines.
    ///
    /// The point of separating this from the movement check is that "they
    /// moved" was passing while beams, morphing and bombs were not crossing at
    /// all. A single verdict hides that; one per feature cannot.
    ///
    /// Every feature is recorded twice: what this client's own player did, and
    /// what it saw the other player do. That is what makes a failure
    /// attributable. "I never saw them fire" means nothing on its own -- if
    /// they never fired either, the script is at fault and not the network,
    /// and the report says so instead of blaming the wire.
    /// </summary>
    public sealed class NetFeatureCheck
    {
        private sealed class Record
        {
            public int SpawnedFrames;
            public int MovedFrames;
            public double Travelled;
            public float MinY = Single.MaxValue;
            public float MaxY = Single.MinValue;
            public double FacingDegrees;
            /// <summary>
            /// Frames on which at least one of this slot's projectiles was
            /// alive on this machine. Not a count of shots: a beam that hits
            /// a wall a metre away contributes one frame and a beam down a
            /// corridor contributes twenty, so this moves with where a puppet
            /// is standing and aiming as much as with whether its owner
            /// pulled the trigger. Read <see cref="ShotsFired"/> for that.
            /// </summary>
            public int BeamFrames;
            /// <summary>
            /// Beams this machine actually spawned for this slot, which is
            /// the question "did the fire button arrive" actually asks.
            /// Accumulated from NetDamage.Fired rather than read from it, so
            /// it survives the reset a map rotation does to those tallies.
            /// </summary>
            public int ShotsFired;
            public int LastFiredTotal;
            public int BombFrames;
            public int HalfturretFrames;
            public int AltFormFrames;
            public int AltFormInMorphPhase;
            public int BipedInUnmorphPhase;
            public int WeaponChanges;
            /// <summary>
            /// Rising edges on the alt attack button. Comparable between the
            /// player who pressed it and everyone who received the press,
            /// which is what separates "the press never arrived" from "it
            /// arrived and did nothing".
            /// </summary>
            public int AltAttackPresses;
            public int DamageEvents;
            public int DamageInAltForm;
            public int Deaths;
            public int ZoomFrames;
            /// <summary>
            /// Frames this slot was frozen solid, counted the same way on
            /// every machine -- the entity's own timer, which is set locally
            /// by the hit on the authority and by
            /// <c>PlayerState.FlagFrozen</c> everywhere else.
            ///
            /// This check did not exist while the bug it is for did: freeze
            /// is applied inside TakeDamage from the beam entity, the replay
            /// has no beam, and so a player frozen on the authority was
            /// frozen on no other machine at all -- walking about normally on
            /// their own screen while the authority drew them encased in ice
            /// and slid them around on their own reported positions.
            /// </summary>
            public int FrozenFrames;
            /// <summary>
            /// Frames this slot was disrupted, and frames it was on fire,
            /// counted the same way on every machine -- the entity's own
            /// timers, set locally by the hit on the authority and by
            /// <c>PlayerState.FlagDisrupted</c> / <c>FlagBurning</c>
            /// everywhere else.
            ///
            /// The same check as the freeze's, for the same two bugs one
            /// weapon along: both afflictions are applied inside TakeDamage
            /// from the beam entity, the replay has no beam, and so a victim
            /// on any machine but the authority's was disrupted and burning
            /// nowhere -- no screen distortion for the one player it is drawn
            /// for, and no flames on anybody.
            /// </summary>
            public int DisruptedFrames;
            public int BurningFrames;
            /// <summary>
            /// Frames this slot was flagged as watching rather than playing.
            /// Read off the entity for every slot alike -- the local player
            /// gets the flag from SpectatorMode, a puppet from the
            /// FlagSpectating bit in its snapshot -- so the two accounts of
            /// one player are directly comparable, which is the whole check:
            /// a spectator who is only hidden on their own machine is still
            /// solid, shootable and in the way on everybody else's.
            /// </summary>
            public int SpectatingFrames;
            public int DoubleDamageFrames;
            public Vector3 LastPosition;
            /// <summary>Last frame's position, for snap detection, which is a
            /// per-frame question rather than a per-sample one.</summary>
            public Vector3 LastFramePosition;
            public bool HaveFramePrevious;
            public Vector3 LastFacing;
            public bool HavePrevious;
            public int LastHealth = -1;
            public BeamType LastWeapon = BeamType.None;
            public bool WasAlive;
            public Hunter Hunter;
            public int FormDisagreeFrames;
            public int FormDisagreeRun;
            public int WorstFormDisagreeRun;
            public string WorstFormContext = "";
            public double WorstPositionGap;
            public double WorstStep;
            public int Teleports;
            /// <summary>
            /// Whether this slot was ever compared against a snapshot -- that
            /// is, whether this client spent any of the run not being the
            /// authority. Recorded rather than asked at the end, because
            /// authority moves when a peer leaves and a client promoted in
            /// the last three seconds of a run would otherwise report that it
            /// had measured nothing, having measured the whole match.
            /// </summary>
            public bool EverCompared;
            public int FramesSinceRespawn;
            /// <summary>
            /// Frames since a jump pad or a teleporter last acted on this
            /// slot, and the running total the engine has reported, so a
            /// launch can be told from a desync.
            /// </summary>
            public int FramesSinceLaunch;
            public int LastWorldEvents;
        }

        /// <summary>
        /// Ground a player can cover in one frame at full speed, with room to
        /// spare. Roughly 0.35 units per frame walking, more when boosting.
        /// </summary>
        private const float TeleportStep = 9f;

        /// <summary>
        /// How long after a jump pad or a teleporter a large step is still
        /// that, rather than a correction. A pad's launch is spent over
        /// several frames and a remote copy of it arrives in 30 Hz steps, so
        /// this is generous on purpose -- half a second of not calling a
        /// launch a desync costs nothing, and calling one a desync buries the
        /// real ones.
        /// </summary>
        private const int LaunchGraceFrames = 30;

        private readonly Record[] _records = new Record[PlayerEntity.MaxPlayers];
        private int _itemSamples;
        private long _itemTotal;
        private int _itemsNow;
        private int _itemsPickedUp;
        private int _lastItemCount = -1;
        private readonly Dictionary<TestPhase, int> _phaseFrames = new();
        private int _localSlot;

        public NetFeatureCheck()
        {
            for (int i = 0; i < _records.Length; i++)
            {
                _records[i] = new Record();
            }
        }

        public void Reset()
        {
            for (int i = 0; i < _records.Length; i++)
            {
                _records[i] = new Record();
            }
        }

        public void Observe(Scene scene)
        {
            // Ask the engine to report pads and teleporters. Off by default
            // and free when off; what it costs when on is an increment on the
            // frame a player touches one.
            Mods.WorldEvents.Watching = true;
            _localSlot = Math.Max(NetSession.LocalSlot, 0);
            // Path metrics -- distance walked, degrees turned -- are sampled
            // where the two sides can be compared: the local player at the
            // instants it publishes an intent, every remote player every
            // frame, because a remote's position and aim only change when one
            // of those packets lands. Both sides then measure the same
            // polyline and a shortfall means a packet went missing.
            //
            // Measuring the local player every frame instead compares a 60 Hz
            // path against its 30 Hz reconstruction. With one opponent the
            // aim moves slowly and the two agree; with seven it slews between
            // targets several times a second, the reconstruction cuts every
            // corner, and the observer reads about a third -- which is what
            // "an observer sees half of a player's turn under load" was.
            bool localSamplePath = NetSession.NetFrame % NetConfig.IntentSendInterval == 0;
            TestPhase phase = NetTestScript.Phase;
            _phaseFrames[phase] = _phaseFrames.GetValueOrDefault(phase) + 1;

            // One sweep of the entity list per frame rather than one per
            // player: beams and bombs are the only evidence that somebody
            // else's weapon fired on this machine, and they are owned rather
            // than indexed.
            Span<int> beams = stackalloc int[PlayerEntity.MaxPlayers];
            Span<int> bombs = stackalloc int[PlayerEntity.MaxPlayers];
            Span<int> turrets = stackalloc int[PlayerEntity.MaxPlayers];
            foreach (EntityBase entity in scene.Entities)
            {
                if (entity.Type == EntityType.BeamProjectile)
                {
                    Count(beams, (entity as BeamProjectileEntity)?.Owner);
                }
                else if (entity.Type == EntityType.Bomb)
                {
                    Count(bombs, (entity as BombEntity)?.Owner);
                }
                else if (entity.Type == EntityType.Halfturret)
                {
                    Count(turrets, (entity as HalfturretEntity)?.Owner);
                }
            }

            // Items are spawned and taken by each client's own simulation.
            // Nothing replicates them, so two clients holding different
            // numbers of them is a real difference in the world -- and one
            // that only shows up by counting.
            _itemsNow = 0;
            foreach (ItemInstanceEntity item in scene.GetItemInstanceEntities())
            {
                _itemsNow++;
            }
            if (_lastItemCount > _itemsNow)
            {
                _itemsPickedUp += _lastItemCount - _itemsNow;
            }
            _lastItemCount = _itemsNow;
            _itemSamples++;
            _itemTotal += _itemsNow;

            for (int slot = 0; slot < PlayerEntity.MaxPlayers; slot++)
            {
                if (slot >= PlayerEntity.Players.Count)
                {
                    continue;
                }
                PlayerEntity player = PlayerEntity.Players[slot];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    continue;
                }
                Record record = _records[slot];
                record.Hunter = player.Hunter;
                if (beams[slot] > 0)
                {
                    record.BeamFrames++;
                }
                int firedTotal = NetDamage.Fired[slot];
                // Negative means the tallies were cleared by a rotation, not
                // that shots were un-fired.
                if (firedTotal > record.LastFiredTotal)
                {
                    record.ShotsFired += firedTotal - record.LastFiredTotal;
                }
                record.LastFiredTotal = firedTotal;
                if (bombs[slot] > 0)
                {
                    record.BombFrames++;
                }
                if (player.Controls.AltAttack.IsPressed)
                {
                    record.AltAttackPresses++;
                }
                if (turrets[slot] > 0)
                {
                    record.HalfturretFrames++;
                }
                if (!player.LoadFlags.TestFlag(LoadFlags.Spawned))
                {
                    continue;
                }
                record.SpawnedFrames++;
                if (player.IsAltForm)
                {
                    record.AltFormFrames++;
                    if (phase == TestPhase.MorphA || phase == TestPhase.AltAttackA
                        || phase == TestPhase.MorphB || phase == TestPhase.AltAttackB)
                    {
                        record.AltFormInMorphPhase++;
                    }
                }
                else if (phase == TestPhase.Zoom || phase == TestPhase.Duel)
                {
                    // Sampled after the unmorph phase has had time to run: a
                    // player still in alt form here never came back out.
                    record.BipedInUnmorphPhase++;
                }
                if (player.EquipInfo.Zoomed)
                {
                    record.ZoomFrames++;
                }
                if (player.ModFrozen)
                {
                    record.FrozenFrames++;
                }
                if (player.ModDisrupted)
                {
                    record.DisruptedFrames++;
                }
                if (player.ModBurning)
                {
                    record.BurningFrames++;
                }
                // For this machine's own player, what it *meant* to do --
                // SpectatorMode, which is where the decision lives -- and for
                // everybody else the flag as it actually arrived. Deliberately
                // not the same source on both sides: reading the entity flag
                // for both made the two accounts agree while spectating was
                // reaching nobody, because a respawn wiped the flag at the
                // source and both sides then counted the same zero. The check
                // is only worth anything if the two sides can disagree.
                if (slot == _localSlot
                    ? Mods.SpectatorMode.IsSpectating
                    : player.Flags2.TestFlag(PlayerFlags2.Spectating))
                {
                    record.SpectatingFrames++;
                }
                if (player.DoubleDamage)
                {
                    record.DoubleDamageFrames++;
                }
                if (player.CurrentWeapon != record.LastWeapon)
                {
                    if (record.LastWeapon != BeamType.None)
                    {
                        record.WeaponChanges++;
                    }
                    record.LastWeapon = player.CurrentWeapon;
                }
                if (record.LastHealth > 0 && player.Health > 0 && player.Health < record.LastHealth)
                {
                    record.DamageEvents++;
                    if (player.IsAltForm)
                    {
                        // The specific claim: a morphed player is still a
                        // target. An alt form that cannot be hit is not a
                        // smaller hitbox, it is an invulnerable one.
                        record.DamageInAltForm++;
                    }
                }
                if (record.WasAlive && player.Health == 0)
                {
                    record.Deaths++;
                }
                // A jump pad and a teleporter both move a player further in
                // one frame than any amount of running, and the engine says
                // so at the moment they act (Mods.WorldEvents, already hooked
                // in JumpPadEntity and TeleporterEntity for the map audit).
                // Without asking, this check reported every pad in the game as
                // a visible teleport: two clients alone on AD2 ALINOS PERCH,
                // agreeing on every number between them, still produced
                // "2 teleport(s), worst jump 21.9 units" -- which reads as a
                // desync and is a player being thrown across a room exactly
                // as the map intends.
                int worldEvents = Mods.WorldEvents.JumpPadsFor(slot)
                    + Mods.WorldEvents.TeleportsFor(slot);
                record.FramesSinceLaunch = worldEvents != record.LastWorldEvents
                    ? 0
                    : record.FramesSinceLaunch + 1;
                record.LastWorldEvents = worldEvents;
                record.FramesSinceRespawn = player.Health > record.LastHealth || player.Health == 0
                    ? 0
                    : record.FramesSinceRespawn + 1;
                record.LastHealth = player.Health;
                record.WasAlive = player.Health > 0;
                record.MinY = MathF.Min(record.MinY, player.Position.Y);
                record.MaxY = MathF.Max(record.MaxY, player.Position.Y);
                // Snaps are a per-frame question -- did this player's position
                // jump between one frame and the next -- so they are measured
                // every frame for every slot, unlike the path lengths below.
                // Measuring them on the sampled cadence counted two frames of
                // ordinary movement as one large step.
                if (record.HaveFramePrevious)
                {
                    float frameStep = (player.Position - record.LastFramePosition).Length;
                    if (frameStep > 0.01f)
                    {
                        record.MovedFrames++;
                    }
                    // A single frame can only carry a player so far. Anything
                    // beyond that is the position being corrected rather than
                    // walked -- which is what a teleport is, seen from
                    // outside.
                    // A respawn moves a player across the level legitimately,
                    // and health and position arrive from different places, so
                    // they can be a few frames out of step. Only count jumps
                    // well clear of one.
                    if (frameStep > TeleportStep && record.FramesSinceRespawn > 60
                        && record.FramesSinceLaunch > LaunchGraceFrames)
                    {
                        record.Teleports++;
                        record.WorstStep = Math.Max(record.WorstStep, frameStep);
                    }
                }
                record.LastFramePosition = player.Position;
                record.HaveFramePrevious = true;

                bool samplePath = slot != _localSlot || localSamplePath;
                if (samplePath && record.HavePrevious)
                {
                    float step = (player.Position - record.LastPosition).Length;
                    if (step < 5)
                    {
                        record.Travelled += step;
                    }
                    // Accumulated degrees rather than frames that changed: a
                    // biped turns its body in steps, so counting frames made a
                    // player spinning on the spot look almost still.
                    float dot = Math.Clamp(Vector3.Dot(player.ModGunVector, record.LastFacing), -1f, 1f);
                    record.FacingDegrees += MathHelper.RadiansToDegrees(MathF.Acos(dot));
                }
                if (samplePath)
                {
                    record.LastPosition = player.Position;
                    record.LastFacing = player.ModGunVector;
                    record.HavePrevious = true;
                }

                if (slot == _localSlot || !NetSession.RemoteStateValid[slot])
                {
                    continue;
                }
                // Not on the authority, which is the one machine with nothing
                // to compare against: it publishes snapshots and does not
                // receive them, so RemoteStates holds whatever reached it
                // before it was promoted and never moves again. Measured
                // against a live world that gap grows without bound, and it
                // reported "their position drifted far from the authority's"
                // for players standing exactly where the authority had put
                // them. It read clean only while everybody was frozen.
                if (NetSession.IsAuthority)
                {
                    continue;
                }
                record.EverCompared = true;
                // What the authority last said about this player against what
                // this client is drawing. The totals cannot separate "never
                // arrived" from "arrived and was overridden"; this can.
                PlayerState state = NetSession.RemoteStates[slot];
                bool wantAlt = (state.Flags & PlayerState.FlagAltForm) != 0;
                // Only while the player is actually on screen. A dead one is
                // hidden (Spawn sets HideModel, death clears it again), so its
                // form is not a thing anybody can see disagree -- and it keeps
                // whatever form it died in until the respawn resets it.
                bool visible = player.Health > 0
                    && (state.Flags & PlayerState.FlagSpawned) != 0;
                if (visible && wantAlt != player.IsAltForm)
                {
                    record.FormDisagreeFrames++;
                    record.FormDisagreeRun++;
                    if (record.FormDisagreeRun > record.WorstFormDisagreeRun)
                    {
                        record.WorstFormDisagreeRun = record.FormDisagreeRun;
                        // Captured at the moment it is worst, because the
                        // interesting question is what the puppet was doing
                        // while it refused to change form.
                        record.WorstFormContext = $"phase {phase}, authority wanted "
                            + $"{(wantAlt ? "alt" : "biped")}, puppet {player.ModFormState()}, "
                            + $"hp {player.Health}";
                    }
                }
                else
                {
                    // The total counts every frame of every transition, which
                    // a morph animation legitimately spends disagreeing. The
                    // longest single run is what separates "the animation is
                    // playing" from "this puppet is stuck in the wrong form".
                    record.FormDisagreeRun = 0;
                }
                if (!visible)
                {
                    continue;
                }
                if ((state.Flags & PlayerState.FlagSpawned) != 0)
                {
                    double gap = (state.Position - player.Position).Length;
                    record.WorstPositionGap = Math.Max(record.WorstPositionGap, gap);
                }
            }
        }

        private void Count(Span<int> counts, EntityBase? owner)
        {
            if (owner is PlayerEntity player && player.SlotIndex >= 0
                && player.SlotIndex < counts.Length)
            {
                counts[player.SlotIndex]++;
            }
            else if (owner is HalfturretEntity turret && turret.Owner.SlotIndex >= 0
                && turret.Owner.SlotIndex < counts.Length)
            {
                counts[turret.Owner.SlotIndex]++;
            }
        }

        /// <summary>
        /// One line per feature: what this client's own player did, what it
        /// saw the other do, and a verdict. A feature nobody performed is
        /// reported as untested rather than as a failure -- blaming the
        /// network for a script that never pressed the button is exactly the
        /// kind of wrong answer this harness exists to avoid.
        /// </summary>
        public bool Report(out int failures)
        {
            failures = 0;
            Record mine = _records[_localSlot];
            string me = GameState.Nicknames[_localSlot];
            Console.WriteLine();
            Console.WriteLine("  feature coverage (mine = what my player did, "
                + "theirs = what I saw of them)");
            int fails = 0;
            bool anyRemote = false;

            // A machine-readable copy of every number, because the strict
            // check is not one this client can make: "I never saw them switch
            // weapons" is a failure only if they switched, and only their own
            // client knows that. compare-reports.py pairs these up.
            void Emit(string kind, string subject, string feature, double value)
            {
                Console.WriteLine($"  netcheck {me} {kind} {subject} {feature} {value:0.##}");
            }

            foreach ((string feature, Func<Record, double> get) in _features)
            {
                Emit("mine", me, feature, get(mine));
            }

            for (int slot = 0; slot < _records.Length; slot++)
            {
                if (slot == _localSlot || _records[slot].SpawnedFrames == 0)
                {
                    continue;
                }
                anyRemote = true;
                Record other = _records[slot];
                string them = GameState.Nicknames[slot];
                Console.WriteLine($"    --- as I saw {them} (slot {slot}, {other.Hunter}) ---");
                foreach ((string feature, Func<Record, double> get) in _features)
                {
                    Emit("saw", them, feature, get(other));
                }
                fails += ReportOne(mine, other, them);
            }
            if (!anyRemote)
            {
                Console.WriteLine("    no other player was ever spawned in this scene -- nothing to compare");
                failures = 1;
                return false;
            }
            var invulnerable = new List<string>();
            for (int slot = 0; slot < PlayerEntity.SlotCapacity && slot < PlayerEntity.Players.Count; slot++)
            {
                PlayerEntity player = PlayerEntity.Players[slot];
                if (_records[slot].SpawnedFrames > 0 && player.LoadFlags.TestFlag(LoadFlags.Spawned)
                    && !player.ModCanBeHurt())
                {
                    invulnerable.Add($"{GameState.Nicknames[slot]} (slot {slot})");
                }
            }
            if (invulnerable.Count > 0)
            {
                Console.WriteLine("    FAIL: no beam can hurt these players at all: "
                    + String.Join(", ", invulnerable));
                fails++;
            }
            var untouched = new List<string>();
            for (int slot = 0; slot < _records.Length; slot++)
            {
                // A player nobody could hurt for a whole match is not a good
                // player, it is a player the damage path never reached.
                if (_records[slot].SpawnedFrames > 600 && _records[slot].DamageEvents == 0)
                {
                    untouched.Add($"{GameState.Nicknames[slot]} (slot {slot})");
                }
            }
            if (untouched.Count > 0)
            {
                Console.WriteLine("    FAIL: never took a single hit: " + String.Join(", ", untouched));
                fails++;
            }
            if (_localSlot < PlayerEntity.Players.Count)
            {
                (int rows, float height) = PlayerEntity.Players[_localSlot].ModScoreboardSize();
                bool fits = height <= 192;
                Console.WriteLine($"    scoreboard: {rows} row(s), {height:0} px tall "
                    + (fits ? "(fits)" : "(OVERFLOWS the screen)"));
                if (!fits)
                {
                    fails++;
                }
            }
            Console.WriteLine($"    items: {_itemsNow} on the map now, "
                + $"{(_itemSamples > 0 ? _itemTotal / (double)_itemSamples : 0):0.0} on average, "
                + $"{_itemsPickedUp} taken or expired");
            Console.WriteLine(Scoreboard("    scoreboard as I see it:"));
            foreach (KeyValuePair<int, string> pair in Boards)
            {
                Console.WriteLine(pair.Value);
            }
            if (Boards.Count > 0)
            {
                // Those are the ones that can be compared between clients.
                // The final board below cannot: clients stop seconds apart,
                // and a slot's score is cleared the moment its player leaves
                // (NetSlotManager.Deactivate, deliberately -- somebody who has
                // gone is not on the board). So by the time the last client
                // prints, it is the only row left standing, and a run with
                // departures in it reports the scoreboards as disagreeing by
                // however many points the leavers had.
            }
            var phases = new StringBuilder("    phases seen:");
            foreach (KeyValuePair<TestPhase, int> pair in _phaseFrames)
            {
                phases.Append($" {pair.Key}={pair.Value}");
            }
            Console.WriteLine(phases.ToString());
            var pipeline = new StringBuilder("    damage pipeline (resolved here / replayed here):");
            for (int slot = 0; slot < PlayerEntity.SlotCapacity; slot++)
            {
                if (_records[slot].SpawnedFrames == 0)
                {
                    continue;
                }
                pipeline.Append($" [{slot}] {NetDamage.Resolved[slot]}/{NetDamage.Replayed[slot]}");
            }
            Console.WriteLine(pipeline.ToString());
            var fired = new StringBuilder("    shots spawned here (per slot):");
            for (int slot = 0; slot < PlayerEntity.SlotCapacity; slot++)
            {
                if (_records[slot].SpawnedFrames == 0)
                {
                    continue;
                }
                double avg = NetDamage.Fired[slot] > 0
                    ? NetDamage.AimDrift[slot] / NetDamage.Fired[slot]
                    : 0;
                fired.Append($" [{slot}] {NetDamage.Fired[slot]}"
                    + $"(drift {avg:0.0}/{NetDamage.WorstDrift[slot]:0.0} deg)");
            }
            Console.WriteLine(fired.ToString());
            var collision = new StringBuilder("    player collision checks:");
            for (int slot = 0; slot < PlayerEntity.SlotCapacity; slot++)
            {
                if (_records[slot].SpawnedFrames == 0)
                {
                    continue;
                }
                collision.Append($" [{slot}] {NetDamage.PlayerChecks[slot]}"
                    + $"/{NetDamage.PlayerOverlaps[slot]}"
                    + $"/{NetDamage.PlayerAccepted[slot]}");
            }
            Console.WriteLine(collision.ToString());
            var pairs = new StringBuilder("    player overlaps by shooter:");
            for (int shooter = 0; shooter < PlayerEntity.SlotCapacity; shooter++)
            {
                for (int target = 0; target < PlayerEntity.SlotCapacity; target++)
                {
                    int count = NetDamage.PlayerOverlapsByShooter[shooter, target];
                    if (count > 0)
                    {
                        pairs.Append($" [{shooter}->{target}] {count}");
                    }
                }
            }
            Console.WriteLine(pairs.ToString());
            Console.WriteLine($"    authority for {NetSession.AuthorityFrames} frame(s) "
                + $"of {NetSession.NetFrame}");
            Console.WriteLine($"    remote position snaps: {NetPlayerBridge.Snaps} "
                + $"(worst {NetPlayerBridge.WorstSnap:0.0} units) -- these are the visible teleports");
            // Where a puppet would have been culled into invisibility before
            // ModRefreshNodeRef started saying so. Zero on most maps.
            Console.WriteLine($"    node lookups unresolved: "
                + $"{NetPlayerBridge.NodeLookupsUnresolved} -- these players are drawn uncalled");
            if (NetPlayerBridge.RejectedUpdates > 0)
            {
                // Never silently: a rejected update means somebody produced a
                // position or an aim that was not a number, and the numbers
                // above are measurements of a match that was already sick.
                Console.WriteLine($"    FAIL: {NetPlayerBridge.RejectedUpdates} "
                    + "update(s) rejected for holding impossible values");
                fails++;
            }
            failures = fails;
            return fails == 0;
        }

        /// <summary>
        /// The scoreboard as it stood at each thirty-second mark of the
        /// SERVER's clock, while everybody was still connected.
        ///
        /// A series rather than one photograph, because clients join seconds
        /// apart and each one's first mark lands wherever its own arrival
        /// put it: in an eight-client run the last to join sampled at t=60
        /// while the rest sampled at t=30, and two photographs of different
        /// moments are not evidence about each other. With the whole series,
        /// the comparison can use the latest instant every client has.
        /// </summary>
        public readonly SortedDictionary<int, string> Boards = new();

        /// <summary>Take one. Called from the client's frame loop as each mark passes.</summary>
        public void SampleScoreboard(int serverSecond)
        {
            Boards[serverSecond] = Scoreboard($"    scoreboard at t={serverSecond}s:");
        }

        private string Scoreboard(string prefix)
        {
            var board = new StringBuilder(prefix);
            for (int slot = 0; slot < PlayerEntity.MaxPlayers; slot++)
            {
                if (_records[slot].SpawnedFrames == 0)
                {
                    continue;
                }
                board.Append($" [{slot}] {GameState.Nicknames[slot]} "
                    + $"{GameState.Kills[slot]}k/{GameState.Deaths[slot]}d/{GameState.Points[slot]}p");
            }
            return board.ToString();
        }

        private static readonly (string Name, Func<Record, double> Get)[] _features =
        {
            ("spawn", r => r.SpawnedFrames),
            ("movement", r => r.Travelled),
            ("jump", Height),
            ("facing", r => r.FacingDegrees),
            ("shooting", r => r.BeamFrames),
            ("shots", r => r.ShotsFired),
            ("weapon-switch", r => r.WeaponChanges),
            ("alt-attack", r => r.AltAttackPresses),
            ("alt-form", r => r.AltFormInMorphPhase),
            // Phase-free as well: the phase-scoped count depends on both
            // clients agreeing where a phase boundary falls, and a difference
            // there looks exactly like a replication failure.
            ("alt-form-total", r => r.AltFormFrames),
            ("unmorph", r => r.BipedInUnmorphPhase),
            ("bombs", r => r.BombFrames),
            ("halfturret", r => r.HalfturretFrames),
            ("zoom", r => r.ZoomFrames),
            ("frozen", r => r.FrozenFrames),
            ("disrupted", r => r.DisruptedFrames),
            ("burning", r => r.BurningFrames),
            ("spectating", r => r.SpectatingFrames),
            ("double-damage", r => r.DoubleDamageFrames),
            ("damage-taken", r => r.DamageEvents),
            ("hit-in-alt-form", r => r.DamageInAltForm),
            ("deaths", r => r.Deaths),
            ("teleports", r => r.Teleports)
        };

        /// <summary>
        /// The checks one client can make on its own: that what it can see
        /// happening is not obviously broken. Anything needing the other
        /// side's account of itself is left to the cross-report comparison.
        /// </summary>
        private int ReportOne(Record mine, Record other, string them)
        {
            var report = new StringBuilder();
            int fails = 0;

            void Line(string feature, double self, double seen, double needed, string unit,
                bool applicable = true, bool pairwise = false)
            {
                bool tested = applicable && (pairwise ? self + seen >= needed : self >= needed);
                bool ok = seen >= needed;
                string verdict = !applicable ? "n/a" : !tested ? "untested"
                    : pairwise ? "ok" : ok ? "ok" : "FAIL";
                if (tested && !pairwise && !ok)
                {
                    fails++;
                }
                report.AppendLine($"    {feature,-16} mine {self,7:0} {unit,-6} "
                    + $"theirs {seen,7:0} {unit,-6} {verdict}");
            }

            Line("spawn", mine.SpawnedFrames, other.SpawnedFrames, 30, "frames");
            Line("movement", mine.Travelled, other.Travelled, 5, "units");
            Line("jump", Height(mine), Height(other), 1.5, "units");
            Line("facing", mine.FacingDegrees, other.FacingDegrees, 180, "deg");
            // Both, because they fail differently. "shots" is the input
            // path: a shortfall there means the fire button did not arrive.
            // "shooting" is projectile-frames, which also moves with where
            // the puppet was pointing, so a shortfall there with "shots"
            // matching means the beams were spawned and died early -- a
            // puppet firing into a wall, not a lost press. Reading the second
            // as the first is what turned "they did 2429, I saw 406" into a
            // hunt for a network fault that was not there.
            Line("shots", mine.ShotsFired, other.ShotsFired, 10, "shots");
            Line("shooting", mine.BeamFrames, other.BeamFrames, 10, "beam-frames");
            Line("weapon switch", mine.WeaponChanges, other.WeaponChanges, 2, "changes",
                pairwise: true);
            Line("alt attack", mine.AltAttackPresses, other.AltAttackPresses, 3, "presses",
                pairwise: true);
            Line("alt form", mine.AltFormInMorphPhase, other.AltFormInMorphPhase, 30, "frames");
            Line("unmorph", mine.BipedInUnmorphPhase, other.BipedInUnmorphPhase, 30, "frames");
            // Hunters differ: only three lay bombs and only Weavel leaves a
            // halfturret. Judging one against another's abilities reported the
            // network as broken for a bomb that was never laid.
            Line("bombs", mine.BombFrames, other.BombFrames, 5, "frames",
                applicable: LaysBombs(other.Hunter));
            Line("halfturret", mine.HalfturretFrames, other.HalfturretFrames, 5, "frames",
                applicable: other.Hunter == Hunter.Weavel);
            Line("zoom", mine.ZoomFrames, other.ZoomFrames, 10, "frames");
            // No `frozen` row here on purpose, though the count is recorded
            // and does cross-check. This report compares *this* player against
            // *that* one, which works for the things the tour has both of them
            // do at once and not for a state one of them is put into: the tour
            // aims in a ring, so the only player a Noxus freezes is the next
            // slot up, and comparing a frozen player against an unfrozen one
            // reads as a replication failure when it is the pair being wrong.
            // The comparison that means something is the same slot seen by
            // everybody, which is `compare-reports.py`'s table -- measured on
            // TEST ARENA as 107 frames on the victim's own machine against 149
            // and 145 on the two watching.
            Line("double damage", mine.DoubleDamageFrames, other.DoubleDamageFrames, 10, "frames");
            Line("taking damage", mine.DamageEvents, other.DamageEvents, 1, "hits");
            Line("hit in alt form", mine.DamageInAltForm, other.DamageInAltForm, 2, "hits",
                pairwise: true);
            Line("deaths", mine.Deaths, other.Deaths, 1, "deaths", pairwise: true);
            Console.Write(report.ToString());
            Console.WriteLine($"    {them}: {other.Teleports} teleport(s), worst jump "
                + $"{other.WorstStep:0.0} units");
            Console.WriteLine(!other.EverCompared
                ? $"    {them}: form and position agreement not measured here -- "
                    + "this client is the authority and receives no snapshot to compare with"
                : $"    {them}: form disagreed on {other.FormDisagreeFrames} frame(s) "
                    + $"(longest run {other.WorstFormDisagreeRun}), "
                    + $"worst position gap {other.WorstPositionGap:0.00} units");
            if (other.WorstFormDisagreeRun > 60)
            {
                Console.WriteLine("    FAIL: their form stayed wrong for "
                    + $"{other.WorstFormDisagreeRun} frames in a row "
                    + $"-- {other.WorstFormContext}");
                fails++;
            }
            if (other.WorstPositionGap > 8 || !Double.IsFinite(other.WorstPositionGap))
            {
                Console.WriteLine("    FAIL: their position drifted far from the authority's");
                fails++;
            }
            return fails;
        }

        private static bool LaysBombs(Hunter hunter)
        {
            return hunter == Hunter.Samus || hunter == Hunter.Kanden || hunter == Hunter.Sylux;
        }

        private static double Height(Record record)
        {
            return record.MaxY > record.MinY ? record.MaxY - record.MinY : 0;
        }
    }
}
