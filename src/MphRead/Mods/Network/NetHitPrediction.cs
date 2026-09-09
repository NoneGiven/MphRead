using System;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Hits that land the instant they are fired, on the machine that fired
    /// them.
    ///
    /// Lag compensation put the *authority's* answer where the shooter aimed;
    /// it did nothing about when that answer arrives. A client shoots, the
    /// intent goes upstream, the authority resolves it, the snapshot comes
    /// back: the hit is correct and a full round trip late, so at 150 ms a
    /// player empties a clip into somebody who does not flinch until a fifth
    /// of a second after each shot. The authority itself never had this --
    /// its own gun resolves in the frame it is fired -- which is exactly the
    /// asymmetry this removes. Everyone who is not the authority resolves
    /// their own shots locally, now, and the authority's answer arrives later
    /// to confirm or overrule it.
    ///
    /// <b>Lag compensation is what makes this sound.</b> The authority rewinds
    /// every other player to the snapshot frame the shooter had applied --
    /// which is the world the shooter's own machine is holding when it fires.
    /// The local resolution and the authority's are therefore the same test
    /// against the same positions, and they agree except for a frame of skew
    /// and for the things a client cannot know (invulnerability windows the
    /// authority has already opened, a victim killed by somebody else in the
    /// meantime). Without <see cref="NetUnlagged"/> underneath it this would
    /// mispredict exactly as often as the shots used to miss.
    ///
    /// Three rules keep a prediction from becoming a lie:
    ///
    /// <list type="number">
    /// <item><b>A prediction never kills.</b> Damage is clamped to leave the
    /// victim on one point of health. Death runs the whole engine death path
    /// -- the banner, the score, the respawn timer, and
    /// <c>EndIfPointGoalReached</c> -- and a kill that turned out not to have
    /// happened would have to be taken back out of all of it. The killing
    /// shot still *feels* instant, because the marker and the flinch are
    /// shown; only the dying waits for the authority, exactly as it does
    /// today.</item>
    /// <item><b>A prediction never scores.</b> It cannot: nothing but the
    /// death path awards a point, and the death path is the one thing a
    /// prediction is not allowed to reach.</item>
    /// <item><b>A prediction is only ever your own shot on somebody else.</b>
    /// Incoming damage is not predicted. Whether you were hit is a question
    /// about a shot fired on another machine, aimed at a copy of you that
    /// machine is holding, and this one has no better guess at it than the
    /// authority's -- it has a worse one.</item>
    /// </list>
    ///
    /// What the prediction is reconciled against is <see cref="NetDamage"/>'s
    /// existing replay. A hit the authority confirms for a victim this
    /// machine already predicted is consumed and *not* shown a second time;
    /// one it never confirms expires quietly, and the health the snapshot
    /// carries -- which is applied every frame anyway -- puts the victim back
    /// where the authority says. Nothing has to be rolled back, because
    /// nothing durable was ever written.
    /// </summary>
    public static class NetHitPrediction
    {
        private const int Slots = PlayerEntity.SlotCapacity;

        /// <summary>
        /// Off with <c>-nohitprediction</c>, which is the control: the same
        /// match measured with hits resolving locally and with them waiting
        /// for the authority is the only way to say what this is worth. On by
        /// default, as <see cref="NetUnlagged.Enabled"/> is.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// The mark over the crosshair. Separate from the prediction because
        /// it is not part of it: the authority draws the same mark for the
        /// same hits, and so does an offline match, where there is nothing to
        /// predict and the confirmation is simply true. Off with
        /// <c>-nohitmarker</c>.
        /// </summary>
        public static bool MarkerEnabled { get; set; } = true;

        /// <summary>
        /// True on a machine whose own damage resolution is a guess rather
        /// than the answer -- a client that is not the authority, outside a
        /// replay of the authority's own hit.
        ///
        /// This is the same condition <see cref="NetDamage.Suppress"/> used to
        /// throw the hit away under, and it is still what decides whether a
        /// hit counts: <see cref="NetDamage.Note"/> records nothing here, so
        /// the diagnostics still describe one machine's resolution and not
        /// eight machines' opinions of it.
        /// </summary>
        public static bool Predicting => NetSession.Active
            && !NetSession.IsHost && !NetSession.IsAuthority && !NetDamage.Replaying;

        /// <summary>
        /// How long a prediction waits for the authority to agree with it.
        ///
        /// Generous on purpose: it has to cover the round trip, the gap
        /// between snapshots, and the rewind depth the authority may have
        /// applied on top. Anything still outstanding after two seconds was
        /// not a slow confirmation, it was a miss.
        /// </summary>
        private const int PendingFrames = 120;

        /// <summary>
        /// Predictions outstanding for one victim at once. A burst of Judicator
        /// shots or a Battlehammer stream can put several in the air before
        /// the first is answered; beyond this the oldest is dropped, since a
        /// prediction nobody has confirmed in eight hits is not going to be.
        /// </summary>
        private const int PendingCapacity = 8;

        private static readonly uint[,] _pendingFrame = new uint[Slots, PendingCapacity];
        private static readonly int[] _pendingCount = new int[Slots];
        private static readonly int[] _pendingHead = new int[Slots];

        /// <summary>Hits this machine resolved for itself, before being told.</summary>
        public static long Predicted { get; private set; }

        /// <summary>Predictions the authority went on to agree with.</summary>
        public static long Confirmed { get; private set; }

        /// <summary>
        /// Predictions the authority never confirmed: a shot that connected
        /// here and nowhere else. The number that says whether this is worth
        /// having -- a few per cent is the frame of skew, a large share means
        /// the local world and the rewound one are not the same world.
        /// </summary>
        public static long Denied { get; private set; }

        /// <summary>
        /// Hits the authority credited this machine with that it had not
        /// predicted -- the opposite error, and the one that costs nothing:
        /// it is shown when it arrives, which is what every hit used to do.
        /// </summary>
        public static long Unpredicted { get; private set; }

        /// <summary>Kills held back so the authority could make them.</summary>
        public static long LethalHeld { get; private set; }

        private static int _markerTimer;

        /// <summary>
        /// Frames the mark stays up. Two tenths of a second: long enough to
        /// read at a glance, short enough that a stream of hits reads as a
        /// stream rather than as one long flash.
        /// </summary>
        private const int MarkerFrames = 12;

        /// <summary>
        /// How solid the mark is drawn, 0 when there is nothing to draw. It
        /// fades over its last few frames rather than blinking out.
        /// </summary>
        public static float MarkerAlpha
        {
            get
            {
                if (!MarkerEnabled || _markerTimer <= 0)
                {
                    return 0;
                }
                const int fade = 6;
                return _markerTimer >= fade ? 1f : _markerTimer / (float)fade;
            }
        }

        public static void Reset()
        {
            Array.Clear(_pendingFrame);
            Array.Clear(_pendingCount);
            Array.Clear(_pendingHead);
            Predicted = 0;
            Confirmed = 0;
            Denied = 0;
            Unpredicted = 0;
            LethalHeld = 0;
            _markerTimer = 0;
        }

        /// <summary>
        /// Whether damage this machine has just resolved should be allowed to
        /// land as a prediction rather than thrown away.
        ///
        /// Asked by <see cref="NetDamage.Suppress"/>, which is the top of
        /// <c>TakeDamage</c> -- before any of the engine's feedback has run,
        /// so a "no" here costs exactly what it always cost.
        /// </summary>
        public static bool Predicts(PlayerEntity victim, EntityBase? source)
        {
            if (!Enabled || victim.Health <= 0)
            {
                return false;
            }
            int local = NetHooks.LocalSlot;
            if (local < 0 || victim.SlotIndex == local)
            {
                return false;
            }
            PlayerEntity? owner = OwnerOf(source);
            return owner != null && owner.SlotIndex == local && owner != victim;
        }

        /// <summary>
        /// Whose shot this is. A halfturret's beams are Weavel's, and a bomb
        /// belongs to whoever laid it -- both are hits a player aimed and
        /// both are answered by the authority the same way.
        /// </summary>
        private static PlayerEntity? OwnerOf(EntityBase? source)
        {
            if (source is BeamProjectileEntity beam)
            {
                if (beam.Owner is PlayerEntity player)
                {
                    return player;
                }
                if (beam.Owner is HalfturretEntity turret)
                {
                    return turret.Owner;
                }
                return null;
            }
            if (source is BombEntity bomb)
            {
                return bomb.Owner;
            }
            if (source is PlayerEntity attacker)
            {
                // An alt form's attack -- Weavel's scythe, Spire's spin,
                // Sylux's trail -- is dealt by the player rather than by
                // anything it spawned. These are the hits that feel worst
                // when they arrive late, because they land at arm's length:
                // the whole of the attack is over before the authority's
                // answer to it comes back.
                return attacker;
            }
            return null;
        }

        /// <summary>
        /// A hit that has survived every one of <c>TakeDamage</c>'s refusals
        /// and is about to be applied: the damage is final, the attacker is
        /// known, and the death has not been decided yet. The last moment a
        /// prediction can be recorded, and the only one at which the clamp
        /// that keeps it from killing still works.
        /// </summary>
        public static void NoteHit(PlayerEntity victim, PlayerEntity? attacker, ref uint damage)
        {
            if (attacker == null || attacker == victim)
            {
                return;
            }
            int local = NetHooks.LocalSlot;
            if (local < 0 || attacker.SlotIndex != local)
            {
                return;
            }
            if (Predicting && Enabled)
            {
                if (victim.Health > 0 && damage >= (uint)victim.Health)
                {
                    // Rule one. The victim is left standing on a single point
                    // of health until the authority says otherwise, which it
                    // will within a round trip -- and when it does,
                    // NetDamage.Replay runs the kill in full, with the Death
                    // flag, exactly as it did before any of this existed.
                    damage = (uint)Math.Max(0, victim.Health - 1);
                    LethalHeld++;
                }
                Push(victim.SlotIndex, NetSession.NetFrame);
                Predicted++;
            }
            // Every machine, every mode: on the authority and offline this is
            // a hit that has actually happened, and there is no reason the
            // confirmation a player gets should depend on which machine is
            // running the match. Not in the story, which is the DS's game and
            // has no such mark.
            if (!GameState.SinglePlayer)
            {
                _markerTimer = MarkerFrames;
            }
        }

        /// <summary>
        /// The authority has confirmed a hit on <paramref name="slot"/> by
        /// this machine's player. Returns whether it was one this machine had
        /// already shown, in which case the replay is skipped -- the flinch,
        /// the sound and the knockback all happened when the trigger was
        /// pulled.
        /// </summary>
        public static bool Confirm(int slot)
        {
            if (slot < 0 || slot >= Slots)
            {
                return false;
            }
            if (_pendingCount[slot] == 0)
            {
                Unpredicted++;
                return false;
            }
            _pendingHead[slot] = (_pendingHead[slot] + 1) % PendingCapacity;
            _pendingCount[slot]--;
            Confirmed++;
            return true;
        }

        /// <summary>
        /// Once a simulation step: age the outstanding predictions out and
        /// count the mark down. In the step and not in the draw, because both
        /// are measured in frames and a picture with no step behind it must
        /// not advance either.
        /// </summary>
        public static void Tick()
        {
            if (_markerTimer > 0)
            {
                _markerTimer--;
            }
            if (!NetSession.Active)
            {
                return;
            }
            uint now = NetSession.NetFrame;
            for (int slot = 0; slot < Slots; slot++)
            {
                while (_pendingCount[slot] > 0)
                {
                    uint frame = _pendingFrame[slot, _pendingHead[slot]];
                    // Unsigned, so a counter that has been reset underneath us
                    // reads as an enormous age rather than a negative one --
                    // which is the right answer either way: nothing pending
                    // from before a reset can still be confirmed.
                    if (now - frame < PendingFrames)
                    {
                        break;
                    }
                    _pendingHead[slot] = (_pendingHead[slot] + 1) % PendingCapacity;
                    _pendingCount[slot]--;
                    Denied++;
                }
            }
        }

        private static void Push(int slot, uint frame)
        {
            if (slot < 0 || slot >= Slots)
            {
                return;
            }
            if (_pendingCount[slot] == PendingCapacity)
            {
                _pendingHead[slot] = (_pendingHead[slot] + 1) % PendingCapacity;
                _pendingCount[slot]--;
                Denied++;
            }
            int tail = (_pendingHead[slot] + _pendingCount[slot]) % PendingCapacity;
            _pendingFrame[slot, tail] = frame;
            _pendingCount[slot]++;
        }

        /// <summary>One line for the harness reports and the diagnostics screen.</summary>
        public static string Describe()
        {
            if (!Enabled)
            {
                return "hit prediction: off";
            }
            if (Predicted == 0)
            {
                return "hit prediction: on, nothing predicted here "
                    + $"({Unpredicted} hits arrived from the authority)";
            }
            double agreed = Confirmed * 100.0 / Predicted;
            return $"hit prediction: {Predicted} predicted, {Confirmed} confirmed "
                + $"({agreed:F1}%), {Denied} denied, {Unpredicted} unpredicted, "
                + $"{LethalHeld} kills left to the authority";
        }
    }
}
