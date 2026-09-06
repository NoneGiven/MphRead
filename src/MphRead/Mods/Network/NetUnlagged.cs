using System;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Backwards reconciliation: a shot is resolved against the world the
    /// shooter was actually looking at, not the one that exists by the time
    /// their trigger arrives.
    ///
    /// Ported from Q-Zandronum (IgeNiaI/Q-Zandronum, <c>unlagged.cpp</c>),
    /// itself Spleen's Skulltag work. The Doom original is
    /// <c>UNLAGGED_Reconcile</c> / <c>UNLAGGED_Restore</c> around a hitscan
    /// trace, plus Q-Zandronum's own addition -- the part this game actually
    /// needed -- <c>UNLAGGED_DoUnlagActors</c>, which does the same thing for
    /// a projectile and then walks it forward to the present.
    ///
    /// The fault it fixes here is not the Doom one, because the topology is
    /// not Doom's. Nobody is a neutral server: the first client to join
    /// simulates the match and everybody else feeds it intent through the
    /// relay. So for a client B shooting at C:
    ///
    ///   * B's screen shows C where the last snapshot put them, which the
    ///     authority composed one downstream trip ago from an intent C sent
    ///     one upstream trip before that.
    ///   * B presses fire. The intent reaches the authority one upstream trip
    ///     later, and the authority spawns the beam against the C it holds
    ///     *now*.
    ///
    /// The two differ by B's full round trip, and it is entirely one-sided: B
    /// aims at a hunter and the shot is resolved against wherever that hunter
    /// walked to in the meantime. At the Pi's 15 ms nobody notices. At 150 ms
    /// a strafing player is most of their own width away from where they are
    /// being shot at, which is the "my shots go through people" report, and
    /// no amount of smoothing the puppets can touch it -- the puppets are
    /// right, the clock is wrong.
    ///
    /// The authority's own player needs none of this and gets a rewind of
    /// zero: it aims at the puppets it holds and resolves against those same
    /// puppets, so the two already agree. Compensation is owed to exactly the
    /// people who are not running the simulation, in exactly the amount they
    /// are behind it -- which is what <see cref="IntentPacket.AckFrame"/> is
    /// for.
    ///
    /// What is deliberately *not* ported: Zandronum reconciles sectors and
    /// polyobjects as well as players, because a Doom map's floor is a moving
    /// hitbox. Nothing in an MPH room moves that a shot can be stopped by in
    /// the same way, and rewinding room geometry would mean unwinding the
    /// collision structures the whole engine indexes against. Players are the
    /// whole of it here.
    /// </summary>
    public static class NetUnlagged
    {
        private const int Slots = PlayerEntity.SlotCapacity;

        /// <summary>
        /// How many frames of position history are kept.
        ///
        /// Zandronum keeps <c>UNLAGGEDTICS</c> 35, which is one second at its
        /// 35 Hz. One second is the right amount of history for the same
        /// reason there as here -- it is longer than any round trip worth
        /// playing on and shorter than any interval over which a rewind would
        /// be a lie -- so this is one second at 60, rounded up to a power of
        /// two so the ring index is a mask.
        /// </summary>
        public const int HistoryFrames = 64;

        /// <summary>
        /// The furthest back a shot may ever be resolved, whatever the packet
        /// claims.
        ///
        /// Two jobs, and the second is the one that matters. It keeps the
        /// rewind inside the history, which is arithmetic; and it bounds what
        /// a client can ask for, which is not. The ack is a number the sender
        /// chose, and a sender that chooses a very old one is asking to shoot
        /// at where everybody stood a second ago -- so this is the ceiling on
        /// how much of that anyone is allowed. 24 frames is 400 ms, past any
        /// line the game is playable on.
        /// </summary>
        public const int MaxRewindFrames = 24;

        /// <summary>
        /// Whether reconciliation runs at all. Off restores the previous
        /// behaviour exactly: shots resolve against the present, which is
        /// what every build before this one did.
        ///
        /// Zandronum spells this <c>sv_nounlagged</c> and defaults it on;
        /// this is the same decision, made once for the match by whoever
        /// simulates it, and exposed as <c>-nounlagged</c> so a run of the
        /// harness can measure both.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Shots that were resolved somewhere other than the present, and how
        /// far back. <see cref="WorstRewind"/> is the deepest single rewind,
        /// which under a healthy line should sit near a client's round trip
        /// in frames and never near <see cref="MaxRewindFrames"/>.
        /// </summary>
        public static long ShotsCompensated { get; private set; }
        public static long FramesRewound { get; private set; }
        public static int WorstRewind { get; private set; }

        /// <summary>
        /// Beam steps run during catch-up, and how many of them ended in a
        /// collision.
        ///
        /// The pair says whether the projectile half is doing anything: steps
        /// with no hits means shots are being launched forward and missing
        /// exactly as they did before, which is either a genuinely clean
        /// match or a sign the rewind is not reaching the beams.
        /// </summary>
        public static long CatchUpSteps { get; private set; }
        public static long CatchUpHits { get; private set; }

        /// <summary>
        /// Shots whose rewind was refused because the history did not hold
        /// the frame asked for.
        ///
        /// Non-zero is not automatically wrong -- a client that has just
        /// joined has acked nothing yet -- but a number that keeps climbing
        /// during a match means acks are arriving older than
        /// <see cref="HistoryFrames"/>, and the shots behind them are being
        /// resolved against the present with no compensation at all.
        /// </summary>
        public static long HistoryMisses { get; private set; }

        // The ring. All slots are written in one pass, so one frame stamp per
        // cell covers the lot: a stamp that does not match the frame being
        // asked for means the cell has been overwritten by a later one.
        private static readonly Vector3[,] _position = new Vector3[Slots, HistoryFrames];
        private static readonly bool[,] _altForm = new bool[Slots, HistoryFrames];
        private static readonly bool[,] _inPlay = new bool[Slots, HistoryFrames];
        private static readonly uint[] _stamp = new uint[HistoryFrames];

        /// <summary>
        /// The newest frame in the history.
        ///
        /// Needed to tell the two ways a lookup can fail apart. The snapshot
        /// that records a frame is published *after* that frame's simulation
        /// step (see <c>NetHooks.AfterSimulation</c>), so the frame a shot is
        /// fired in is never in the history yet -- asking for it is normal and
        /// means "the present". Asking for something older than the ring is a
        /// gap, and a different thing entirely.
        /// </summary>
        private static uint _newest;

        // Where everybody was before the rewind, so Restore can put them back.
        private static readonly Vector3[] _restore = new Vector3[Slots];
        private static readonly bool[] _moved = new bool[Slots];
        private static bool _reconciled;

        // Which of the shooter's beam pool entries were alive before the shot,
        // so the ones it spawned can be told from the ones already in flight.
        // Grown from the pool rather than assumed: PlayerEntity asks for 16
        // today, and a silently short array here would quietly stop
        // compensating whichever beams fell off the end.
        private static bool[] _beamsBefore = new bool[16];
        private static PlayerEntity? _shooter;
        private static int _rewind;

        /// <summary>
        /// True while a shot is being spawned into the past.
        ///
        /// Zandronum's <c>unlagInProgress</c>, and here for the same reason:
        /// the state above is one shot's worth, so a second
        /// <see cref="BeginShot"/> reached from inside a catch-up step -- a
        /// beam whose collision kills a player, say -- would overwrite the
        /// first shot's bookkeeping and leave the world rewound with nothing
        /// holding the note to put it back.
        /// </summary>
        private static bool _inProgress;

        public static void Reset()
        {
            Array.Clear(_stamp);
            Array.Clear(_moved);
            _newest = 0;
            _reconciled = false;
            _inProgress = false;
            _shooter = null;
            _rewind = 0;
            ShotsCompensated = 0;
            FramesRewound = 0;
            WorstRewind = 0;
            CatchUpSteps = 0;
            CatchUpHits = 0;
            HistoryMisses = 0;
        }

        /// <summary>
        /// Record where everybody is, as of the snapshot numbered
        /// <paramref name="frame"/>.
        ///
        /// Called from <see cref="NetSession.BroadcastSnapshot"/> and nowhere
        /// else, deliberately. The history has to hold what the snapshot
        /// *said*, not what was true at some nearby moment: a client's ack
        /// names a snapshot, and the whole mechanism is the claim that cell
        /// <c>frame</c> is the picture that client was looking at. Recording
        /// anywhere else in the frame would make that claim off by however
        /// much the players moved in between, which is precisely the error
        /// being corrected.
        /// </summary>
        public static void Record(uint frame)
        {
            if (!Enabled)
            {
                return;
            }
            // A rewound world must never outlive the frame it was rewound in.
            // Everything below records where players *are*, and if a shot left
            // them displaced -- an exception between BeginShot and EndShot is
            // the only way, but it is a way -- this would file the rewound
            // positions as the truth and then publish them to everybody. One
            // call, once a frame, and a no-op on all but the pathological one.
            Restore();
            _inProgress = false;
            int index = (int)(frame % HistoryFrames);
            _stamp[index] = frame;
            _newest = frame;
            for (int i = 0; i < Slots; i++)
            {
                if (i >= PlayerEntity.Players.Count)
                {
                    _inPlay[i, index] = false;
                    continue;
                }
                PlayerEntity player = PlayerEntity.Players[i];
                bool active = player.LoadFlags.TestFlag(LoadFlags.Active) && player.ModIsInPlay;
                _inPlay[i, index] = active;
                if (active)
                {
                    _position[i, index] = player.Position;
                    _altForm[i, index] = player.IsAltForm;
                }
            }
        }

        /// <summary>
        /// How far behind the simulation the owner of <paramref name="slot"/>
        /// is, in frames, clamped to what can honestly be served.
        ///
        /// Zandronum's <c>UNLAGGED_Gametic</c>, with its two sources collapsed
        /// into one. It offers a choice between the client's acknowledged
        /// server tic and a figure derived from its measured ping
        /// (<c>cl_ping_unlagged</c>); the ack is the better of the two -- a
        /// ping is a smoothed average of a number that is not smooth, while
        /// the ack is the exact frame the client is answering -- and having
        /// only one of them means there is only one thing to be wrong.
        /// </summary>
        private static int RewindFor(int slot)
        {
            if (slot < 0 || slot >= Slots || slot == NetSession.LocalSlot)
            {
                // The authority's own player already aims and resolves
                // against the same puppets. Nothing is owed.
                return 0;
            }
            if (!NetSession.RemoteIntentValid[slot])
            {
                return 0;
            }
            uint ack = NetSession.RemoteIntents[slot].AckFrame;
            uint now = NetSession.NetFrame;
            if (ack == 0 || ack >= now)
            {
                // Nothing acked yet (a client that has only just joined), or
                // an ack from a counter that has restarted and is ahead of
                // ours. Both mean "no usable answer", not "no delay" -- but
                // the present is the only defensible guess for either.
                return 0;
            }
            long depth = now - ack;
            if (depth > MaxRewindFrames)
            {
                depth = MaxRewindFrames;
            }
            return (int)depth;
        }

        /// <summary>
        /// Whether this machine is the one whose shots decide anything.
        ///
        /// Everybody spawns beams -- a client fires its own gun for its own
        /// eyes -- but only the authority's collide with anybody. Rewinding
        /// on a machine whose shots are decoration would move other players'
        /// hitboxes underneath the person watching them, for no gain at all.
        /// </summary>
        private static bool Simulating => NetSession.Active
            && (NetSession.Role == NetRole.Host || NetSession.IsAuthority);

        /// <summary>
        /// Put the world back to what <paramref name="shooter"/> could see,
        /// ready for a shot to be spawned into it.
        ///
        /// Everybody except the shooter is moved. The shooter is left exactly
        /// where they are, for the same reason Zandronum leaves them ("the
        /// client is supposed to predict him"): their own position is the one
        /// thing they were not looking at a stale copy of. They told us where
        /// they are in the same packet as the trigger, and
        /// <see cref="NetPlayerBridge"/> has already put them there.
        /// </summary>
        public static void BeginShot(PlayerEntity shooter)
        {
            if (_inProgress)
            {
                return;
            }
            _shooter = null;
            _rewind = 0;
            if (!Enabled || !Simulating || shooter.IsBot)
            {
                return;
            }
            int slot = shooter.SlotIndex;
            int rewind = RewindFor(slot);
            if (rewind <= 0)
            {
                return;
            }
            uint target = NetSession.NetFrame - (uint)rewind;
            if (!Reconcile(slot, target))
            {
                HistoryMisses++;
                return;
            }
            _shooter = shooter;
            _rewind = rewind;
            ShotsCompensated++;
            FramesRewound += rewind;
            if (rewind > WorstRewind)
            {
                WorstRewind = rewind;
            }
            // Remember what was already in flight, so the beams this shot is
            // about to create can be picked out of the pool afterwards.
            BeamProjectileEntity[] beams = shooter.EquipInfo.Beams;
            if (_beamsBefore.Length < beams.Length)
            {
                _beamsBefore = new bool[beams.Length];
            }
            for (int i = 0; i < beams.Length; i++)
            {
                _beamsBefore[i] = beams[i].Lifespan > 0;
            }
            _inProgress = true;
        }

        /// <summary>
        /// Move every player but <paramref name="exceptSlot"/> to where they
        /// stood at <paramref name="frame"/>. Returns whether the history
        /// still held it.
        /// </summary>
        private static bool Reconcile(int exceptSlot, uint frame)
        {
            int index = (int)(frame % HistoryFrames);
            if (_stamp[index] != frame || frame == 0)
            {
                return false;
            }
            Restore();
            for (int i = 0; i < Slots && i < PlayerEntity.Players.Count; i++)
            {
                if (i == exceptSlot || !_inPlay[i, index])
                {
                    continue;
                }
                PlayerEntity player = PlayerEntity.Players[i];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active) || !player.ModIsInPlay)
                {
                    continue;
                }
                Vector3 was = _position[i, index];
                if (!Single.IsFinite(was.X) || !Single.IsFinite(was.Y) || !Single.IsFinite(was.Z))
                {
                    continue;
                }
                // A position recorded in one form, applied to a body that has
                // since changed into the other, is the same standing spot in
                // the wrong reference frame -- and a biped cylinder is tall
                // enough for that to put a chest shot under the model. The
                // conversion is NetPlayerBridge's, for exactly this reason.
                _restore[i] = player.Position;
                _moved[i] = true;
                player.ModPlaceAt(NetPlayerBridge.InFormFor(player, was, _altForm[i, index]));
            }
            _reconciled = true;
            return true;
        }

        /// <summary>
        /// Undo the rewind. Safe to call when nothing was rewound, which is
        /// most frames.
        /// </summary>
        public static void Restore()
        {
            if (!_reconciled)
            {
                return;
            }
            for (int i = 0; i < Slots && i < PlayerEntity.Players.Count; i++)
            {
                if (!_moved[i])
                {
                    continue;
                }
                _moved[i] = false;
                PlayerEntity.Players[i].ModPlaceAt(_restore[i]);
            }
            _reconciled = false;
        }

        /// <summary>
        /// Walk the shot that was just spawned forward to the present, then
        /// put the world back.
        ///
        /// This is Q-Zandronum's <c>UNLAGGED_DoUnlagActors</c>, and it is the
        /// half that matters in this game. Doom's unlagged is built around
        /// hitscan, where rewinding the targets is the whole fix because the
        /// trace resolves in the instant it is fired. Almost nothing here is
        /// hitscan: a Missile, a Battlehammer round and a Magmaul shot all
        /// travel, so rewinding the world and spawning into it fixes only the
        /// first frame of the shot and then leaves the projectile crawling
        /// out of a muzzle a round trip late -- visibly behind its owner's
        /// screen, and easy to walk out of.
        ///
        /// So the beam is stepped once per frame it is owed, with the world
        /// re-reconciled at each step so that anything it runs into on the
        /// way is checked against where that player was at that moment. What
        /// arrives in the present is a shot already the right distance down
        /// range, having hit whatever it would have hit.
        ///
        /// The loop stops the moment every new beam is done -- collided, or
        /// out of lifespan -- which is what makes a point-blank shot cost one
        /// step instead of twenty-four.
        /// </summary>
        public static void EndShot(PlayerEntity shooter)
        {
            if (_shooter != shooter || _rewind <= 0)
            {
                Restore();
                _shooter = null;
                _inProgress = false;
                return;
            }
            int slot = shooter.SlotIndex;
            BeamProjectileEntity[] beams = shooter.EquipInfo.Beams;
            int newCount = 0;
            for (int i = 0; i < beams.Length; i++)
            {
                if (!_beamsBefore[i] && beams[i].Lifespan > 0)
                {
                    _beamsBefore[i] = true; // reused as "this one is ours, and still going"
                    newCount++;
                }
                else
                {
                    _beamsBefore[i] = false;
                }
            }
            if (newCount == 0)
            {
                // Nothing spawned: out of ammo, or a continuous beam being
                // replaced rather than created. Nothing to walk forward.
                Restore();
                _shooter = null;
                _inProgress = false;
                return;
            }
            for (int step = 1; step <= _rewind && newCount > 0; step++)
            {
                uint frame = NetSession.NetFrame - (uint)(_rewind - step);
                if (!Reconcile(slot, frame))
                {
                    if (frame <= _newest)
                    {
                        // A genuine gap: the history did not reach back this
                        // far. Stepping on against present positions would
                        // resolve the rest of this shot against a world nobody
                        // was ever shown, so stop and leave it where it got to
                        // -- at worst the behaviour with none of this.
                        HistoryMisses++;
                        break;
                    }
                    // Not a gap. The frame this shot is being fired in has no
                    // snapshot yet, because the snapshot is published after the
                    // step; so the last step of every catch-up asks for a frame
                    // that does not exist, and the right world for it is the
                    // one that does -- the present, which is what the catch-up
                    // is catching up to. Without this the loop stopped one step
                    // short of the present on every shot.
                    Restore();
                }
                for (int i = 0; i < beams.Length; i++)
                {
                    if (!_beamsBefore[i])
                    {
                        continue;
                    }
                    BeamProjectileEntity beam = beams[i];
                    bool hit = beam.Flags.TestFlag(BeamFlags.Collided);
                    CatchUpSteps++;
                    if (!beam.Process() || beam.Flags.TestFlag(BeamFlags.Collided))
                    {
                        if (!hit && beam.Flags.TestFlag(BeamFlags.Collided))
                        {
                            CatchUpHits++;
                        }
                        _beamsBefore[i] = false;
                        newCount--;
                    }
                }
            }
            Restore();
            _shooter = null;
            _rewind = 0;
            _inProgress = false;
        }

        /// <summary>
        /// One line for the harness reports and the diagnostics screen.
        /// </summary>
        public static string Describe()
        {
            if (!Enabled)
            {
                return "lag compensation: off";
            }
            if (ShotsCompensated == 0)
            {
                return "lag compensation: on, nothing to compensate "
                    + $"(history misses {HistoryMisses})";
            }
            double mean = FramesRewound / (double)ShotsCompensated;
            return $"lag compensation: {ShotsCompensated} shots rewound, "
                + $"mean {mean:F1} frames ({mean * 1000 / 60:F0} ms), worst {WorstRewind}, "
                + $"catch-up {CatchUpSteps} steps / {CatchUpHits} hits, "
                + $"history misses {HistoryMisses}";
        }
    }
}
