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
    /// Two rules keep a prediction from becoming a lie:
    ///
    /// <list type="number">
    /// <item><b>A prediction never scores and never ends a match.</b> The
    /// scoreboard is assigned from the snapshot for every slot, so a point
    /// awarded by a predicted kill is overwritten by the authority's answer
    /// within a snapshot either way; and <c>EndIfPointGoalReached</c> is
    /// already refused on a machine that is not keeping the score, by
    /// <c>NetMatchEnd.MayEndOnScore</c>. What the death path is allowed to do
    /// here is the part a player is waiting for -- the body drops, the banner
    /// says who it was, the mark lands.</item>
    /// <item><b>A prediction is only ever your own shot on somebody else.</b>
    /// Incoming damage is not predicted. Whether you were hit is a question
    /// about a shot fired on another machine, aimed at a copy of you that
    /// machine is holding, and this one has no better guess at it than the
    /// authority's -- it has a worse one. The one thing predicted *onto* this
    /// machine's own player is the health its own Shock Coil drains out of
    /// somebody else, which is not a guess about anybody else's input.</item>
    /// </list>
    ///
    /// <para>
    /// <b>What is held, and for how long.</b> Predicting a hit and then
    /// letting the next snapshot assign the authority's health straight over
    /// it is a prediction that lasts one frame: the flinch is instant and the
    /// bar springs back up, which is the thing "it is not registering" is
    /// actually describing. So a prediction is held -- the victim's health is
    /// the authority's number minus whatever this machine has predicted and
    /// not yet had confirmed, and a victim predicted dead stays down rather
    /// than being respawned by a snapshot that has not heard about it yet.
    /// The hold lasts one measured round trip and a margin
    /// (<see cref="HoldFrames"/>), never the two seconds a prediction is kept
    /// for the statistics: a mispredicted hit is a wrong health bar, and a
    /// wrong health bar has to expire in the time it takes the authority to
    /// answer rather than in the time it takes to be sure it never will.
    /// </para>
    ///
    /// What the prediction is reconciled against is <see cref="NetDamage"/>'s
    /// existing replay. A hit the authority confirms for a victim this
    /// machine already predicted is consumed and *not* shown a second time;
    /// one it never confirms expires quietly, and the health the snapshot
    /// carries -- which is applied every frame anyway -- puts the victim back
    /// where the authority says. Nothing has to be rolled back, because
    /// nothing durable is ever written: health is assigned from the snapshot,
    /// the score is assigned from the snapshot, and a wrongly killed puppet is
    /// put back on the map by the same branch that spawns everybody else.
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
        /// prediction nobody has confirmed in two dozen hits is not going to
        /// be.
        ///
        /// Two dozen rather than the eight this started at, because the Shock
        /// Coil resolves a hit every other frame for as long as the trigger is
        /// held: at 300 ms of round trip that is nine outstanding before the
        /// first answer arrives, and an overflow does not merely lose a
        /// statistic any more -- it drops that hit's damage out of the health
        /// the victim is being held at.
        /// </summary>
        private const int PendingCapacity = 24;

        private static readonly uint[,] _pendingFrame = new uint[Slots, PendingCapacity];

        /// <summary>
        /// What each outstanding prediction took off that victim, so the bar
        /// can be drawn at the authority's health minus what this machine has
        /// already landed on them. See <see cref="HealthFor"/>.
        /// </summary>
        private static readonly int[,] _pendingDamage = new int[Slots, PendingCapacity];

        /// <summary>Whether that prediction was the one that killed them here.</summary>
        private static readonly bool[,] _pendingLethal = new bool[Slots, PendingCapacity];

        private static readonly int[] _pendingCount = new int[Slots];
        private static readonly int[] _pendingHead = new int[Slots];

        /// <summary>
        /// How long a prediction is allowed to hold the picture: one measured
        /// round trip, a snapshot's gap, and a margin.
        ///
        /// Not <see cref="PendingFrames"/>, which is how long a prediction is
        /// kept for the *statistics* -- two seconds, deliberately generous,
        /// because a confirmation that arrives late is still a confirmation
        /// and counting it as a miss would flatter nothing. Holding a health
        /// bar for two seconds is a different proposition: a hit that was
        /// wrong is a health bar that is wrong, and it has to right itself in
        /// about the time the authority takes to answer rather than in the
        /// time it takes to be certain it never will.
        ///
        /// The ping is the server's own measurement of this client's round
        /// trip (see <see cref="NetSession.SlotPing"/>), which is zero until
        /// it has one -- and the clamp is what makes that read as "assume a
        /// quarter of a second" rather than as "hold nothing".
        /// </summary>
        private static int HoldFrames
        {
            get
            {
                int slot = NetHooks.LocalSlot;
                int ping = slot >= 0 && slot < NetSession.SlotPing.Length
                    ? NetSession.SlotPing[slot]
                    : 0;
                // 60 Hz of simulation, plus twelve frames for the gap between
                // snapshots and the rewind the authority may have applied.
                return Math.Clamp((int)(ping * 0.06f) + 12, 15, 90);
            }
        }

        /// <summary>
        /// Health this machine's own player has drained out of somebody else
        /// and not yet been told about, as (frame, amount).
        ///
        /// The Shock Coil takes what it deals and gives it to the shooter, and
        /// that is the one heal a client can work out for itself: it is the
        /// arithmetic of a hit this machine has already resolved, not a guess
        /// about anybody's input. Without it the beam was the one weapon in
        /// the game whose whole point arrived a round trip late -- the victim
        /// flinched instantly, courtesy of the prediction, and the health it
        /// bought did not turn up until the authority said so.
        ///
        /// Kept as a credit on top of the authority's number rather than as an
        /// absolute health, so damage taken while draining still shows the
        /// moment the authority reports it. A stale credit is worth a point or
        /// two for a fraction of a second; a stale absolute would hide a
        /// rocket.
        /// </summary>
        private const int HealCapacity = 48;
        private static readonly uint[] _healFrame = new uint[HealCapacity];
        private static readonly int[] _healAmount = new int[HealCapacity];
        private static int _healCount;
        private static int _healHead;

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

        /// <summary>
        /// Kills held back so the authority could make them -- which is what
        /// every lethal prediction did before death was predicted, and what
        /// one still does under <c>-nodeathprediction</c>.
        /// </summary>
        public static long LethalHeld { get; private set; }

        /// <summary>Kills this machine showed the instant it landed them.</summary>
        public static long DeathsPredicted { get; private set; }

        /// <summary>
        /// Deaths predicted here that the authority did not agree with, so the
        /// puppet was put back on the map. The number that says whether
        /// predicting death is worth having; a wrongly killed player is the
        /// most visible thing this whole file can get wrong.
        /// </summary>
        public static long DeathsUndone { get; private set; }

        /// <summary>Health drained by this machine's own beam, ahead of the authority.</summary>
        public static long DrainPredicted { get; private set; }

        /// <summary>
        /// Your own splash, on you, resolved the frame it went off -- the
        /// rocket jump. Counted apart from <see cref="Predicted"/> because it
        /// is not the same claim: source, target and input are all on this
        /// machine, so it is arithmetic rather than a bet on a rewind, and
        /// mixing the two would flatter the percentage that measures the bet.
        /// </summary>
        public static long SelfPredicted { get; private set; }

        /// <summary>Those of them the authority went on to agree with.</summary>
        public static long SelfConfirmed { get; private set; }

        /// <summary>
        /// Whether a lethal prediction is allowed to kill.
        ///
        /// On, and off with <c>-nodeathprediction</c>, which is the control
        /// for measuring it the way <c>-nohitprediction</c> is for the rest.
        /// With it off the damage is clamped to leave the victim standing on
        /// one point of health and the dying waits for the authority, which is
        /// what this did originally: the killing shot still *felt* instant,
        /// because the mark and the flinch were shown, but the prediction
        /// visibly stopped one point short of the thing it was predicting.
        /// </summary>
        public static bool DeathEnabled { get; set; } = true;

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
            Array.Clear(_pendingDamage);
            Array.Clear(_pendingLethal);
            Array.Clear(_pendingCount);
            Array.Clear(_pendingHead);
            Array.Clear(_healFrame);
            Array.Clear(_healAmount);
            _healCount = 0;
            _healHead = 0;
            Predicted = 0;
            Confirmed = 0;
            SelfPredicted = 0;
            SelfConfirmed = 0;
            Denied = 0;
            Unpredicted = 0;
            LethalHeld = 0;
            DeathsPredicted = 0;
            DeathsUndone = 0;
            DrainPredicted = 0;
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
            if (local < 0)
            {
                return false;
            }
            // Whose shot it is, and nothing about who it lands on.
            //
            // This used to refuse a hit whose victim was this machine's own
            // player -- rule two, incoming damage is not predicted -- and that
            // refusal is already made by the line below: damage arriving from
            // somebody else has an owner who is not this slot. What the extra
            // clause actually excluded was the one hit that is *entirely*
            // local: your own splash, on you. Source, target and input are all
            // on this machine, there is nothing to guess about anybody, and it
            // is the hit whose feedback matters most on the frame it happens,
            // because a rocket jump is not damage that arrives late -- it is a
            // jump that does not happen. See the self-damage section in
            // .claude/multiplayer/NETWORK-PREDICTION.md.
            PlayerEntity? owner = OwnerOf(source);
            return owner != null && owner.SlotIndex == local;
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
            if (attacker == null)
            {
                return;
            }
            int local = NetHooks.LocalSlot;
            if (local < 0 || attacker.SlotIndex != local)
            {
                return;
            }
            bool self = attacker == victim;
            if (Predicting && Enabled)
            {
                // A prediction never kills *you*, whatever DeathEnabled says.
                //
                // The knockback is applied by TakeDamage regardless of what
                // the number ends up being, so the clamp costs the rocket jump
                // nothing -- the push is the whole point and it lands either
                // way. What it avoids is the local death path run on a guess
                // about the machine's own player: the death camera, the
                // countdown, PausePrevented and the respawn are a great deal
                // more to take back than a puppet lying down, and none of it
                // is what "the jump has to be instant" is asking for.
                bool lethal = victim.Health > 0 && damage >= (uint)victim.Health;
                if (lethal && (!DeathEnabled || self))
                {
                    // The old rule one, kept as the control. The victim is
                    // left standing on a single point of health until the
                    // authority says otherwise, which it will within a round
                    // trip -- and when it does, NetDamage.Replay runs the kill
                    // in full, with the Death flag.
                    damage = (uint)Math.Max(0, victim.Health - 1);
                    LethalHeld++;
                    lethal = false;
                }
                Push(victim.SlotIndex, NetSession.NetFrame, (int)damage, lethal);
                // Counted apart from the rest. The confirmed percentage is a
                // claim about shots aimed at other people over a wire; a hit
                // on yourself, resolved on the machine that fired it, would
                // only flatter it.
                if (self)
                {
                    SelfPredicted++;
                }
                else
                {
                    Predicted++;
                }
                if (lethal)
                {
                    DeathsPredicted++;
                }
            }
            // Every machine, every mode: on the authority and offline this is
            // a hit that has actually happened, and there is no reason the
            // confirmation a player gets should depend on which machine is
            // running the match. Not in the story, which is the DS's game and
            // has no such mark.
            //
            // Never for your own splash landing on you: the mark answers "did
            // that land on somebody", and a rocket jump is not a hit anybody
            // wants confirming.
            if (!GameState.SinglePlayer && !self)
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
        public static bool Confirm(int slot, int landed = 1)
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
            // As many as the snapshot says landed, not one.
            //
            // A snapshot carries a *count* of hits since the last one and the
            // slot of only the last attacker, and this used to retire a single
            // prediction however many it was reporting. Two of this machine's
            // own hits inside one snapshot window therefore left one of them
            // outstanding, to time out later looking like a miss -- which is
            // most of what the "denied" number was measuring, and, now that a
            // prediction holds the victim's health, a hold that outlived its
            // confirmation by the whole of the window. Capped at what is
            // actually outstanding, so a burst that included somebody else's
            // hits cannot retire more than this machine predicted.
            bool self = slot == NetHooks.LocalSlot;
            int take = Math.Clamp(landed, 1, _pendingCount[slot]);
            for (int i = 0; i < take; i++)
            {
                _pendingHead[slot] = (_pendingHead[slot] + 1) % PendingCapacity;
                _pendingCount[slot]--;
                if (self)
                {
                    SelfConfirmed++;
                }
                else
                {
                    Confirmed++;
                }
            }
            return true;
        }

        /// <summary>
        /// Forget what is outstanding for one slot, because the slot has
        /// changed hands or the room has.
        ///
        /// A prediction describes a hit on a particular player in a particular
        /// room. Kept across either, the worst of it is a lethal one holding
        /// the new occupant of that slot dead on this screen for the length of
        /// the hold -- a player who has just spawned into a fresh map, lying
        /// down because somebody else was shot before the rotation. The
        /// statistics go with it: they are per-match, like
        /// <c>NetDamage.ResetForRoomChange</c>'s tallies.
        /// </summary>
        public static void ForgetSlot(int slot)
        {
            if (slot < 0 || slot >= Slots)
            {
                return;
            }
            for (int i = 0; i < PendingCapacity; i++)
            {
                _pendingFrame[slot, i] = 0;
                _pendingDamage[slot, i] = 0;
                _pendingLethal[slot, i] = false;
            }
            _pendingCount[slot] = 0;
            _pendingHead[slot] = 0;
        }

        /// <summary>
        /// The authority has put <paramref name="slot"/> back on the map, so
        /// everything this machine predicted about their last life is spent.
        ///
        /// Two jobs at one moment. It counts a kill this machine showed that
        /// the authority never confirmed -- the hold expired, the next
        /// snapshot stood them up, and that is exactly what a mispredicted
        /// kill looks like from here. And it drops the outstanding debit,
        /// because a prediction about the life that just ended must not come
        /// off the health of the one that just started: without this, a
        /// player killed and respawned inside the hold window would come back
        /// with the last twenty points this machine had landed on them
        /// already taken off.
        /// </summary>
        public static void NoteRespawn(int slot)
        {
            if (slot < 0 || slot >= Slots)
            {
                return;
            }
            for (int i = 0; i < _pendingCount[slot]; i++)
            {
                int at = (_pendingHead[slot] + i) % PendingCapacity;
                if (_pendingLethal[slot, at])
                {
                    DeathsUndone++;
                    break;
                }
            }
            ForgetSlot(slot);
        }

        /// <summary>Everything outstanding, for a rotation into a new room.</summary>
        public static void ForgetPending()
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                ForgetSlot(slot);
            }
            _healCount = 0;
            _healHead = 0;
        }

        /// <summary>
        /// Health this machine's own Shock Coil has just drained, before the
        /// authority has said so.
        ///
        /// Called from the beam's life-drain branch, beside the
        /// <c>GainHealth</c> it is reporting. Only the credit is recorded here
        /// -- the engine has already applied the heal locally, exactly as it
        /// does offline; this is what stops the next snapshot from assigning
        /// it straight back off again.
        /// </summary>
        public static void NoteDrain(PlayerEntity healer, int amount)
        {
            if (!Enabled || !Predicting || amount <= 0)
            {
                return;
            }
            int local = NetHooks.LocalSlot;
            if (local < 0 || healer.SlotIndex != local)
            {
                return;
            }
            if (_healCount == HealCapacity)
            {
                _healHead = (_healHead + 1) % HealCapacity;
                _healCount--;
            }
            int tail = (_healHead + _healCount) % HealCapacity;
            _healFrame[tail] = NetSession.NetFrame;
            _healAmount[tail] = amount;
            _healCount++;
            DrainPredicted += amount;
        }

        /// <summary>
        /// What this machine has taken off <paramref name="slot"/> and not yet
        /// been told about, in points of health.
        /// </summary>
        private static int Debit(int slot)
        {
            if (!Enabled || slot < 0 || slot >= Slots || _pendingCount[slot] == 0)
            {
                return 0;
            }
            uint now = NetSession.NetFrame;
            int hold = HoldFrames;
            int debit = 0;
            for (int i = 0; i < _pendingCount[slot]; i++)
            {
                int at = (_pendingHead[slot] + i) % PendingCapacity;
                if (now - _pendingFrame[slot, at] < (uint)hold)
                {
                    debit += _pendingDamage[slot, at];
                }
            }
            return debit;
        }

        /// <summary>
        /// The health to show for a puppet: the authority's number, less what
        /// this machine has already landed on them and not yet had confirmed.
        ///
        /// Never zero on its own account. Assigning zero health is not a
        /// death -- it skips the whole death path -- so a hold that ran the
        /// bar to the bottom would produce a player who is neither alive nor
        /// dead. A predicted kill goes through <c>TakeDamage</c> like any
        /// other and is held by <see cref="HeldDead"/> instead.
        /// </summary>
        public static int HealthFor(int slot, int authorityHealth)
        {
            int debit = Debit(slot);
            if (debit <= 0 || authorityHealth <= 1)
            {
                return authorityHealth;
            }
            return Math.Max(1, authorityHealth - debit);
        }

        /// <summary>
        /// This machine's own health: the authority's number plus whatever its
        /// beam has drained since the authority last spoke.
        /// </summary>
        public static int LocalHealthFor(PlayerEntity player, int authorityHealth)
        {
            if (!Enabled || authorityHealth <= 0)
            {
                return authorityHealth;
            }
            uint now = NetSession.NetFrame;
            int hold = HoldFrames;
            int credit = 0;
            for (int i = 0; i < _healCount; i++)
            {
                int at = (_healHead + i) % HealCapacity;
                if (now - _healFrame[at] < (uint)hold)
                {
                    credit += _healAmount[at];
                }
            }
            // And less your own splash, held the same way a victim's is. The
            // debit is the mirror of the credit and has to be here, not in
            // HealthFor: nothing calls HealthFor for the local slot, and
            // without this a rocket jump would take the health off for one
            // frame and the next snapshot would hand it straight back until
            // the authority caught up -- the same one-frame prediction the
            // hold exists to stop, on the one player who is looking at the
            // number.
            credit -= Debit(NetHooks.LocalSlot);
            if (credit == 0)
            {
                return authorityHealth;
            }
            int max = player.HealthMax > 0 ? player.HealthMax : authorityHealth;
            // Floored at 1 for HealthFor's reason: an assignment is not a
            // death, and a self-inflicted prediction is never lethal anyway.
            return Math.Clamp(authorityHealth + credit, 1, max);
        }

        /// <summary>
        /// Whether this machine has killed <paramref name="slot"/> and is
        /// still waiting to hear whether it was right.
        ///
        /// While this is true the snapshot is not allowed to put that player
        /// back on the map: the authority's copy of them is a round trip
        /// behind and still walking around, and respawning the corpse every
        /// snapshot until the kill is confirmed is worse than either answer.
        /// It stops being true the moment the kill is confirmed -- or, if it
        /// never is, when the hold expires and the next snapshot spawns them
        /// as it always did.
        /// </summary>
        public static bool HeldDead(int slot)
        {
            if (!Enabled || !DeathEnabled || slot < 0 || slot >= Slots)
            {
                return false;
            }
            uint now = NetSession.NetFrame;
            int hold = HoldFrames;
            for (int i = 0; i < _pendingCount[slot]; i++)
            {
                int at = (_pendingHead[slot] + i) % PendingCapacity;
                if (_pendingLethal[slot, at] && now - _pendingFrame[slot, at] < (uint)hold)
                {
                    return true;
                }
            }
            return false;
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
            // The drain credit is aged on the same clock. It is only ever read
            // through the hold window, so this is housekeeping rather than
            // policy -- it keeps the ring from filling with entries nothing
            // will ever count again.
            while (_healCount > 0 && now - _healFrame[_healHead] >= PendingFrames)
            {
                _healHead = (_healHead + 1) % HealCapacity;
                _healCount--;
            }
        }

        private static void Push(int slot, uint frame, int damage, bool lethal)
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
            _pendingDamage[slot, tail] = Math.Max(0, damage);
            _pendingLethal[slot, tail] = lethal;
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
            string deaths = DeathEnabled
                ? $"{DeathsPredicted} kills predicted, {DeathsUndone} undone"
                : $"{LethalHeld} kills left to the authority";
            string drain = DrainPredicted > 0 ? $", {DrainPredicted} health drained ahead" : "";
            string self = SelfPredicted > 0
                ? $", {SelfPredicted} self-hits predicted ({SelfConfirmed} confirmed)"
                : "";
            return $"hit prediction: {Predicted} predicted, {Confirmed} confirmed "
                + $"({agreed:F1}%), {Denied} denied, {Unpredicted} unpredicted, "
                + deaths + drain + self;
        }
    }
}
