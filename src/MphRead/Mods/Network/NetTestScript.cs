using System;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// What a scripted client is doing right now.
    ///
    /// Phases rather than one behaviour, because each one exercises a
    /// different part of what has to cross the wire, and a report that says
    /// only "they moved" cannot tell a working beam from a working morph.
    /// </summary>
    public enum TestPhase
    {
        Idle,
        Walk,
        Jump,
        Turn,
        Shoot,
        SwitchWeapons,
        Charge,
        MorphA,
        AltAttackA,
        MorphB,
        AltAttackB,
        Unmorph,
        Zoom,
        Afflict,
        Duel
    }

    /// <summary>
    /// A fixed tour of the game's features, run by both clients at once.
    ///
    /// Keyed to the server's match clock rather than to each client's own
    /// frame counter, so two processes that joined seconds apart are in the
    /// same phase at the same moment. That is what lets each side assert
    /// something about the other without any comparison step between them:
    /// during the morph phase both are morphing, so "I saw them in alt form"
    /// is a claim one client can check on its own.
    ///
    /// It writes into player.Controls, the same surface the keyboard and
    /// PlayerAi write into, so the engine cannot tell it apart from a person
    /// playing.
    /// </summary>
    public static class NetTestScript
    {
        /// <summary>
        /// Seconds per phase. Lowered by the map audit, which visits every
        /// room and cannot afford seventy seconds each.
        /// </summary>
        public static double PhaseSeconds { get; set; } = ReadPhaseSeconds();

        /// <summary>
        /// MPHREAD_PHASE_SECONDS shortens the tour without shortening it:
        /// fifteen phases at five seconds is seventy-five before the last one
        /// is reached, so a run budgeted for less than that reports
        /// `untested` for the alt attacks and the affinity weapon -- the
        /// three the Shock Coil and both bomb types live in -- while looking
        /// like a clean pass. Lowering the phase covers all fifteen instead
        /// of the first six.
        /// </summary>
        private static double ReadPhaseSeconds()
        {
            string? value = Environment.GetEnvironmentVariable("MPHREAD_PHASE_SECONDS");
            if (value != null && Double.TryParse(value,
                System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                && parsed > 0)
            {
                return parsed;
            }
            return 5;
        }

        private static readonly TestPhase[] _order =
        {
            TestPhase.Idle,
            TestPhase.Walk,
            TestPhase.Jump,
            TestPhase.Turn,
            TestPhase.Shoot,
            TestPhase.SwitchWeapons,
            TestPhase.Charge,
            TestPhase.MorphA,
            TestPhase.AltAttackA,
            TestPhase.MorphB,
            TestPhase.AltAttackB,
            TestPhase.Unmorph,
            TestPhase.Zoom,
            TestPhase.Afflict,
            TestPhase.Duel
        };

        /// <summary>How many phases one pass of the tour has.</summary>
        public static int PhaseCount => _order.Length;

        public static bool Enabled { get; set; }

        /// <summary>Degrees per frame, so a turn is a sweep rather than a snap.</summary>
        private const float TurnRate = 6f;
        /// <summary>Hold fire once the aim is this close, in degrees.</summary>
        private const float FiringCone = 6f;
        private const float PreferredRange = 4f;

        private static int _frame;
        private static int _stuckFrames;
        private static bool _stuckDirection;
        private static OpenTK.Mathematics.Vector3 _lastPosition;

        public static float AimDeltaX { get; private set; }
        public static float AimDeltaY { get; private set; }
        public static int FramesOnTarget { get; private set; }

        /// <summary>
        /// The phase every scripted client is in, from the server's clock.
        /// Falls back to the local frame counter only when no server state has
        /// arrived, where nothing is being compared anyway.
        /// </summary>
        public static TestPhase Phase
        {
            get
            {
                double elapsed = NetSession.ServerMatch?.TimeElapsed ?? (_frame / 60.0);
                int index = (int)(elapsed / PhaseSeconds) % _order.Length;
                return _order[index];
            }
        }

        public static void Reset()
        {
            _frame = 0;
            _stuckFrames = 0;
            _lastPosition = OpenTK.Mathematics.Vector3.Zero;
            AimDeltaX = 0;
            AimDeltaY = 0;
            FramesOnTarget = 0;
        }

        /// <summary>
        /// Drive one player with no session behind it, for the map audit.
        /// The slot decides the morph/shoot parity and the caller owns the
        /// frame counter, so several players can be driven in one frame.
        /// </summary>
        public static void ApplyOffline(PlayerEntity player, int slot, int frame)
        {
            _offlineSlot = slot;
            _frame = frame;
            Drive(player);
            _offlineSlot = -1;
        }

        private static int _offlineSlot = -1;

        /// <summary>
        /// Hold the trigger and nothing else. Used by the audit's affliction
        /// probe, which decides where the shot goes itself.
        /// </summary>
        public static void HoldFire(PlayerEntity player, bool down)
        {
            PlayerControls c = player.Controls;
            Clear(c);
            Hold(c.Shoot, down);
            Finish(player, c);
        }

        /// <summary>
        /// Morph, then lay bombs where you stand.
        ///
        /// The one thing the tour cannot do on purpose. Its alt-attack phases
        /// lay bombs wherever the walk happened to reach, and a bomb only
        /// hurts somebody standing on it, so "bombs did no damage" was the
        /// tour's answer on every map whether or not the weapon worked. This
        /// puts the ball on the target and presses the button.
        ///
        /// The press has to be a press: bomb laying is edge-triggered, so a
        /// held button lays one bomb and then nothing for the rest of the run.
        /// </summary>
        public static void LayBombs(PlayerEntity player, int frame)
        {
            PlayerControls c = player.Controls;
            Clear(c);
            if (player.Health == 0)
            {
                Hold(c.Shoot, true);
            }
            else if (!player.IsAltForm && Settled(player))
            {
                Hold(c.Morph, true);
            }
            else if (player.IsAltForm)
            {
                Hold(c.AltAttack, frame % 20 < 3);
            }
            Finish(player, c);
        }

        /// <summary>
        /// Stop driving a player, and ask it out of alt form if it is in one.
        ///
        /// Used by the map audit's world probe, which needs a biped standing
        /// still on a pad: several jump pads are flagged to ignore alt forms,
        /// and a report of "this pad does nothing" that only means "it was
        /// asked while the player was a morph ball" is worse than no report.
        /// </summary>
        public static void Rest(PlayerEntity player, bool wantBiped)
        {
            PlayerControls c = player.Controls;
            Clear(c);
            if (player.Health == 0)
            {
                // A dead player waits out its respawn timer unless it asks to
                // come back early by holding fire. Standing still through it
                // costs the audit's probes their whole window: they gave up on
                // a pad because the player they put on it was still dead.
                Hold(c.Shoot, true);
            }
            else if (wantBiped && player.IsAltForm && Settled(player))
            {
                Hold(c.Morph, true);
            }
            Finish(player, c);
        }

        /// <summary>
        /// Walk straight ahead and do nothing else.
        ///
        /// The map audit's render sweep drives with this: the question it asks
        /// is what a player sees after leaving the spot they spawned on, and
        /// anything else the tour does -- firing, morphing, aiming at people --
        /// only moves them somewhere less predictable.
        /// </summary>
        public static void WalkForward(PlayerEntity player)
        {
            PlayerControls c = player.Controls;
            Clear(c);
            if (player.Health == 0)
            {
                Hold(c.Shoot, true);
            }
            else
            {
                Hold(c.MoveUp, true);
            }
            Finish(player, c);
        }

        public static void Apply(PlayerEntity player)
        {
            if (!Enabled)
            {
                return;
            }
            _frame++;
            Drive(player);
        }

        private static void Drive(PlayerEntity player)
        {
            PlayerControls c = player.Controls;
            Clear(c);
            PlayerEntity? target = FindTarget(player);
            bool onTarget = AimAt(player, target);
            TestPhase phase = Phase;
            // Leave the zoom phase the way it was entered. Zoom is sticky --
            // nothing clears it when the phase ends -- and it changes what the
            // rest of the tour measures: the Imperialist does *half* damage
            // unzoomed, so a player that wandered out of the zoom phase still
            // zoomed was quietly firing a different weapon for every phase
            // after it. No other phase touches this control, so pressing it
            // here and falling through costs the phase nothing.
            if (phase != TestPhase.Zoom && player.EquipInfo.Zoomed && _frame % 8 == 0)
            {
                Hold(c.Zoom, true);
            }
            switch (phase)
            {
                case TestPhase.Idle:
                    break;
                case TestPhase.Walk:
                    Square(c);
                    break;
                case TestPhase.Jump:
                    Square(c);
                    Hold(c.Jump, _frame % 45 < 3);
                    break;
                case TestPhase.Turn:
                    // Sweep on the spot: separates "the other player's facing
                    // reaches me" from "their position does".
                    AimDeltaX = 4f;
                    AimDeltaY = MathF.Sin(_frame / 40f) * 2f;
                    break;
                case TestPhase.Shoot:
                    Hold(c.Shoot, _frame % 30 < 20);
                    break;
                case TestPhase.SwitchWeapons:
                    // One press per half second, so a receiver can see the
                    // weapon change rather than a blur.
                    Hold(c.NextWeapon, _frame % 30 == 0);
                    Hold(c.Shoot, _frame % 30 > 10 && _frame % 30 < 25);
                    break;
                case TestPhase.Charge:
                    Hold(c.Shoot, true);
                    break;
                case TestPhase.MorphA:
                    // Half the players morph while the other half shoot at
                    // them, then the roles swap in MorphB. Both sides morphing
                    // at once would never answer the question that matters
                    // here -- whether a player in alt form can still be hit.
                    MorphOrShoot(player, c, morphing: Even);
                    break;
                case TestPhase.AltAttackA:
                    AltAttackOrShoot(c, attacking: Even, onTarget);
                    break;
                case TestPhase.MorphB:
                    MorphOrShoot(player, c, morphing: !Even);
                    break;
                case TestPhase.AltAttackB:
                    // The mirror of AltAttackA. Without it the odd slots never
                    // pressed alt attack at all, and the report blamed the
                    // network for a bomb that was never laid.
                    AltAttackOrShoot(c, attacking: !Even, onTarget);
                    break;
                case TestPhase.Unmorph:
                    Hold(c.Morph, Settled(player) && player.IsAltForm && _frame % 40 == 0);
                    Square(c);
                    break;
                case TestPhase.Zoom:
                    // Zoom belongs to particular weapons, so issue one first.
                    //
                    // Issue, not press for: this used to press the
                    // Imperialist's own key, and a weapon key only selects a
                    // weapon the player has already picked up. On a fresh
                    // roster nobody has, so the key did nothing, the Zoom
                    // button was never held, and the phase reported
                    // `untested` for run after run -- which reads as "not
                    // proven" and was really "never attempted". The affliction
                    // phase below had already learned the same lesson.
                    if (!player.ModCanZoom)
                    {
                        player.ModArmZoomWeapon();
                    }
                    // Press towards the state we want, not on a timer.
                    // UpdateZoom is a toggle on the rising edge, so a fixed
                    // cadence of presses zooms in and straight back out again
                    // -- the owner ended a 300-frame phase having been zoomed
                    // for two of them while its puppet, which had received a
                    // different number of edges, sat zoomed for a hundred and
                    // twenty. Pressing only while not yet zoomed converges
                    // instead, and re-presses on its own if an edge is lost,
                    // which is what a player does.
                    Hold(c.Zoom, player.ModCanZoom && !player.EquipInfo.Zoomed && _frame % 8 == 0);
                    Hold(c.Shoot, player.ModCanZoom && _frame % 40 < 8);
                    break;
                case TestPhase.Afflict:
                    // Freeze, burn and disrupt are properties of a hunter's
                    // own weapon, so the phase begins by handing it over. Then
                    // it is an ordinary duel: the point is to land those shots
                    // on somebody, not to prove the gun exists.
                    //
                    // Charged, which it was not. Every affliction in the game
                    // is on the *charged* affinity shot, and the duel's fire
                    // pattern holds the trigger for 24 frames of every 30 --
                    // less than any weapon's full charge, so the phase armed
                    // the right gun and then fired it in the one way that
                    // cannot afflict anybody. Nothing said so: the affliction
                    // has no cross-check of its own in this harness, and
                    // `frozen` read `untested` on every client of every run.
                    player.ModArmAffinityWeapon();
                    Duel(player, c, target, onTarget, charged: true);
                    break;
                case TestPhase.Duel:
                    Duel(player, c, target, onTarget);
                    break;
            }
            Finish(player, c);
        }

        /// <summary>Not in the middle of changing form.</summary>
        private static bool Settled(PlayerEntity player)
        {
            return !player.IsMorphing && !player.IsUnmorphing;
        }

        /// <summary>Whether the slot being driven is an even-numbered one.</summary>
        private static bool Even => (_offlineSlot >= 0
            ? _offlineSlot
            : Math.Max(NetSession.LocalSlot, 0)) % 2 == 0;

        private static void MorphOrShoot(PlayerEntity player, PlayerControls c, bool morphing)
        {
            if (morphing)
            {
                // Only while settled in the other form. Pressing again while
                // the transition is still playing is what a person would not
                // do, and it lands on a remote copy that may already be a
                // step further along -- morphing it straight back out.
                Hold(c.Morph, Settled(player) && !player.IsAltForm && _frame % 40 == 0);
                Square(c);
                Hold(c.Boost, _frame % 60 < 20);
                return;
            }
            // Unmorph first if the previous phase left us in alt form, then
            // shoot: a biped shooting a morph ball is the case being tested.
            Hold(c.Morph, Settled(player) && player.IsAltForm && _frame % 40 == 0);
            Hold(c.Shoot, !player.IsAltForm && _frame % 30 < 24);
        }

        private static void AltAttackOrShoot(PlayerControls c, bool attacking, bool onTarget)
        {
            if (attacking)
            {
                Square(c);
                Hold(c.AltAttack, _frame % 45 < 6);
                return;
            }
            Hold(c.Shoot, onTarget && _frame % 30 < 24);
        }

        /// <summary>
        /// Everything released, every frame, before the phase presses what it
        /// wants. A key left down by the previous phase would otherwise keep
        /// acting, and a morph that never ended would make the rest of the
        /// tour meaningless.
        /// </summary>
        /// <summary>
        /// Previous frame's button state, taken by <see cref="Clear"/> and
        /// used by <see cref="Finish"/>. Static and reused: the players are
        /// driven one after another on one thread, and each is bracketed by
        /// the pair.
        /// </summary>
        private static bool[] _wasDown = Array.Empty<bool>();

        /// <summary>
        /// Start a frame of input for this player: remember what was held,
        /// then let go of everything.
        ///
        /// The edges are worked out in <see cref="Finish"/> rather than here.
        /// They used to be computed inside every Hold call, and a phase that
        /// wrote a button twice in a frame -- once by this clear, once by
        /// setting it to the same value -- wiped the edge the first write had
        /// produced. A charged weapon fires when the trigger is *released*, so
        /// scripted players held the charge and never let it go: the charge
        /// phase fired nothing, and the three afflictions, which exist only on
        /// a charged shot, could not be reached at all.
        /// </summary>
        private static void Clear(PlayerControls c)
        {
            if (_wasDown.Length < c.All.Length)
            {
                _wasDown = new bool[c.All.Length];
            }
            for (int i = 0; i < c.All.Length; i++)
            {
                Keybind bind = c.All[i];
                _wasDown[i] = bind.IsDown;
                bind.IsDown = false;
                bind.IsPressed = false;
                bind.IsReleased = false;
            }
        }

        /// <summary>Work out this frame's press and release edges.</summary>
        private static void Finish(PlayerEntity player, PlayerControls c)
        {
            bool any = false;
            for (int i = 0; i < c.All.Length && i < _wasDown.Length; i++)
            {
                Keybind bind = c.All[i];
                bind.IsPressed = bind.IsDown && !_wasDown[i];
                bind.IsReleased = !bind.IsDown && _wasDown[i];
                any |= bind.IsDown || bind.IsReleased;
            }
            if (any)
            {
                // A scripted client is not a bot and its binds are written
                // after the pass that answers "is anybody playing this", so
                // without this the tour's own players went idle, had their
                // guns lowered, and could not fire -- which is most of why
                // the shot counts in every report were a fraction of what the
                // tour actually asks for, and the whole of why the affliction
                // probe never got its charged shot away.
                player.ModNoteInput();
            }
        }

        private static bool AimAt(PlayerEntity player, PlayerEntity? target)
        {
            AimDeltaX = 0;
            AimDeltaY = 0;
            if (target == null)
            {
                return false;
            }
            (float turnX, float turnY) = player.ModAimDeltaTowards(target.ModAimTarget);
            if (!Single.IsFinite(turnX) || !Single.IsFinite(turnY))
            {
                // Aiming at a player whose position has gone bad would turn
                // this one's aim to nonsense too, and then publish it.
                return false;
            }
            AimDeltaX = Math.Clamp(turnX, -TurnRate, TurnRate);
            AimDeltaY = Math.Clamp(turnY, -TurnRate, TurnRate);
            bool onTarget = MathF.Abs(turnX) < FiringCone && MathF.Abs(turnY) < FiringCone;
            if (onTarget)
            {
                FramesOnTarget++;
            }
            return onTarget;
        }

        private static void Square(PlayerControls c)
        {
            int phase = _frame / 60 % 4;
            Hold(c.MoveUp, phase == 0);
            Hold(c.MoveRight, phase == 1);
            Hold(c.MoveDown, phase == 2);
            Hold(c.MoveLeft, phase == 3);
        }

        /// <summary>
        /// Frames left to keep the trigger *up* for, so that a charge which
        /// has finished actually leaves the gun. A charged weapon fires on
        /// release; holding fire forever charges forever.
        /// </summary>
        private static int _releaseFrames;

        private static void Duel(PlayerEntity player, PlayerControls c, PlayerEntity? target,
            bool onTarget, bool charged = false)
        {
            if (target == null)
            {
                Square(c);
                Hold(c.Shoot, _frame % 60 < 20);
                return;
            }
            float distance = (target.Position - player.Position).Length;
            // Walking straight at somebody across a level with walls in it
            // ends up sliding along one of them forever. When the ground stops
            // going by, break off sideways for a moment.
            float moved = (player.Position - _lastPosition).Length;
            _lastPosition = player.Position;
            _stuckFrames = moved < 0.02f ? _stuckFrames + 1 : 0;
            bool stuck = _stuckFrames > 20;
            if (stuck && _stuckFrames > 90)
            {
                _stuckFrames = 0;
                _stuckDirection = !_stuckDirection;
            }
            Hold(c.MoveUp, !stuck && distance > PreferredRange);
            Hold(c.MoveDown, !stuck && distance < PreferredRange / 2);
            Hold(c.MoveLeft, stuck ? _stuckDirection
                : distance <= PreferredRange && _frame / 90 % 2 == 0);
            Hold(c.MoveRight, stuck ? !_stuckDirection
                : distance <= PreferredRange && _frame / 90 % 2 == 1);
            Hold(c.Jump, stuck ? _stuckFrames % 30 < 3 : _frame % 150 < 3);
            if (!charged)
            {
                Hold(c.Shoot, onTarget && _frame % 30 < 24);
                return;
            }
            // Hold until the charge is full whether or not the aim has come
            // round yet -- a charge is built while waiting for a shot, which
            // is how the weapon is used -- and let go once it is full and the
            // target is in front of the gun.
            if (_releaseFrames > 0)
            {
                _releaseFrames--;
                return;
            }
            if (onTarget && player.ModChargeReady)
            {
                _releaseFrames = 4;
                return;
            }
            Hold(c.Shoot, true);
        }

        private static PlayerEntity? FindTarget(PlayerEntity self)
        {
            // A ring over the slots: everyone shoots at the next slot up from
            // their own, wrapping round.
            //
            // The choice has to be the same on every machine, and "nearest
            // player" is not: under latency each client is looking at a
            // slightly different world and two of them can pick different
            // targets, which makes "that slot took no damage" mean nothing at
            // all. Slot order is the one thing every client agrees on.
            //
            // Over the whole roster, not over three. This was `% 3` while a
            // three-client run was being debugged, which quietly aimed slots
            // 3 and up back at slots 1 and 2 -- so every run with more than
            // three players, the eight-client sweeps included, was measuring
            // a different scenario than the one it reported.
            if (NetSession.Active && self.SlotIndex >= 0)
            {
                int count = PlayerEntity.Players.Count;
                for (int step = 1; step < count; step++)
                {
                    int targetSlot = (self.SlotIndex + step) % count;
                    PlayerEntity target = PlayerEntity.Players[targetSlot];
                    if (target != self && target.LoadFlags.TestFlag(LoadFlags.Active)
                        && target.LoadFlags.TestFlag(LoadFlags.Spawned) && target.Health > 0)
                    {
                        return target;
                    }
                }
            }
            PlayerEntity? best = null;
            float bestDistance = Single.MaxValue;
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity other = PlayerEntity.Players[i];
                if (other == self || !other.LoadFlags.TestFlag(LoadFlags.Active)
                    || !other.LoadFlags.TestFlag(LoadFlags.Spawned) || other.Health == 0)
                {
                    continue;
                }
                float distance = (other.Position - self.Position).Length;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }
            return best;
        }

        /// <summary>Ask for a button. Edges come from <see cref="Finish"/>.</summary>
        private static void Hold(Keybind bind, bool down)
        {
            bind.IsDown = down;
        }
    }
}
