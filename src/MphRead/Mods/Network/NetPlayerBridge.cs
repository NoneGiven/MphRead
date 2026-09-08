using System;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Translates between MphRead's player state and the wire format.
    ///
    /// The injection design leans on something the project already does:
    /// PlayerAi.ProcessInput() drives bots by writing into player.Controls,
    /// the exact surface the keyboard writes into. A remote player is
    /// therefore just a third writer of that same surface -- no new input
    /// path, no engine change.
    /// </summary>
    public static class NetPlayerBridge
    {
        /// <summary>
        /// How long a form disagreement is tolerated before it is forced.
        /// Longer than the morph animation, so a transition that is simply
        /// playing out is never cut short -- that was what removed the
        /// morph-in animation entirely.
        /// </summary>
        private const int FormGraceFrames = 90;
        private static readonly int[] _formMismatch = new int[PlayerEntity.SlotCapacity];

        /// <summary>
        /// Whether the last snapshot had each slot standing on the map, so the
        /// frame the authority places somebody can be told from the thousands
        /// of frames afterwards on which it merely still has them placed.
        /// </summary>
        private static readonly bool[] _authoritySpawned = new bool[PlayerEntity.SlotCapacity];

        /// <summary>
        /// Placements refused because they did not belong to this room. Zero
        /// on a healthy session; any number at all means a rotation where one
        /// machine was still loading, which is worth seeing in the netdbg
        /// line rather than inferring from a player's account of falling out
        /// of the world.
        /// </summary>
        public static int PlacementsRefused;

        /// <summary>
        /// Beyond this a remote player is placed outright, not eased. Well
        /// past anything a lost burst of updates can account for, so what is
        /// left is a respawn or a teleporter -- where a jump is correct.
        /// </summary>
        private const float SnapDistance = 15f;
        /// <summary>How much of the remaining gap a remote player closes each frame.</summary>
        private const float CatchUpRate = 0.35f;
        /// <summary>Closed faster when the gap is wide, so catching up is not slow motion.</summary>
        private const float FastCatchUpRate = 0.6f;
        private const float FastCatchUpAbove = 3f;
        private static readonly int[] _formAttempts = new int[PlayerEntity.SlotCapacity];

        /// <summary>
        /// How many updates were thrown away for holding a value that is not
        /// a number, or one no room could contain.
        ///
        /// One of these is enough to ruin a match for everybody: a NaN
        /// position is written into a player, spreads to whoever aims at it,
        /// and is then published as authoritative. The player stops moving,
        /// dies repeatedly, and every measurement of it reads NaN. Dropping
        /// the update keeps the last good value instead, which is wrong for
        /// one frame rather than permanently.
        /// </summary>
        public static long RejectedUpdates { get; private set; }

        /// <summary>
        /// Times a remote player had to be placed rather than eased, and the
        /// worst of them. This is the teleport a player actually sees: the
        /// smoothed catch-up is invisible, a snap is not.
        /// </summary>
        public static long Snaps { get; private set; }
        public static float WorstSnap { get; private set; }

        /// <summary>
        /// Frames on which a player's room node could not be worked out from
        /// its position at all, even after the body and half a unit either
        /// side of it were tried.
        ///
        /// The measurement behind "players go invisible up there": the node is
        /// what the renderer culls against, so a lookup that fails leaves a
        /// puppet holding a stale one. Non-zero says the map has places the
        /// portal volumes do not cover, and which map and how often is the
        /// difference between a room to look at and a fluke.
        /// </summary>
        public static long NodeLookupsUnresolved;

        /// <summary>
        /// How far this machine's own player may be from the authority's copy
        /// of it before it is pulled back.
        ///
        /// Wide on purpose. The authority's copy is this client's own report
        /// from a round trip ago, so under boost across a bad line the two
        /// are several units apart while nothing at all is wrong, and a
        /// threshold tight enough to call that a desync is a threshold that
        /// fires constantly. This is here for corruption, not for latency.
        /// </summary>
        private const float DesyncDistance = 30f;

        /// <summary>
        /// The fastest a puppet may be said to be travelling, in units per
        /// frame. Boost -- the quickest a hunter moves under its own power --
        /// caps at 0.6, so this is eight times anything legitimate and exists
        /// only to stop a derived velocity from becoming a launch.
        /// </summary>
        private const float MaxReportedSpeed = 5f;

        /// <summary>Positions beyond this are not a level, they are corruption.</summary>
        private const float PositionLimit = 100000f;

        /// <summary>
        /// A position measured while its owner was in one form, expressed in
        /// the form this copy of the player is actually in.
        ///
        /// UpdateForm moves Position by the difference between the biped and
        /// alt collision volumes' centres each way, so `P_alt = P_biped +
        /// (bipedCentre - altCentre)`. The two are the same standing spot
        /// written in two reference frames, and nothing in the packet said
        /// which -- so for as long as a puppet's form lagged its owner's, it
        /// was placed in the wrong one and its hitbox sat that far off the
        /// body. Vertically, on a biped cylinder 1.6 units tall, which is
        /// enough for a shot aimed at the chest to pass under it.
        ///
        /// A no-op whenever the two agree, which is almost always.
        /// </summary>
        /// <summary>
        /// <see cref="InForm"/>, for the reconciliation path. Same
        /// conversion, same reason: a position recorded while its owner was a
        /// morph ball and applied to a biped is out by the difference between
        /// the two collision centres, which is most of a chest.
        /// </summary>
        public static Vector3 InFormFor(PlayerEntity player, Vector3 position, bool measuredInAlt)
        {
            return InForm(player, position, measuredInAlt);
        }

        private static Vector3 InForm(PlayerEntity player, Vector3 position, bool measuredInAlt)
        {
            if (measuredInAlt == player.IsAltForm)
            {
                return position;
            }
            int hunter = (int)player.Hunter;
            if (hunter < 0 || hunter >= 8)
            {
                return position;
            }
            Vector3 delta = PlayerEntity.PlayerVolumes[hunter, 0].SpherePosition
                - PlayerEntity.PlayerVolumes[hunter, 2].SpherePosition;
            return measuredInAlt ? position - delta : position + delta;
        }

        private static bool Sane(Vector3 value)
        {
            return Single.IsFinite(value.X) && Single.IsFinite(value.Y) && Single.IsFinite(value.Z)
                && MathF.Abs(value.X) < PositionLimit && MathF.Abs(value.Y) < PositionLimit
                && MathF.Abs(value.Z) < PositionLimit;
        }

        /// <summary>
        /// Rising edges from the last few frames, newest first, so a
        /// one-frame press survives a lost packet. See IntentPacket.Presses.
        /// </summary>
        private static readonly uint[] _pressHistory = new uint[IntentPacket.PressHistory];

        /// <summary>
        /// Record this frame's rising edges, whether or not a packet goes out
        /// this frame.
        ///
        /// Separate from building the packet because the two happen at
        /// different rates: edges have to be caught every frame -- a one-frame
        /// press exists only on the frame it happens -- while packets are sent
        /// less often to keep the relay from drowning. Folding this into the
        /// packet build meant a slower send rate silently dropped half of all
        /// morphs and weapon switches.
        /// </summary>
        public static void RecordPresses(PlayerEntity player)
        {
            PlayerControls c = player.Controls;
            IntentButtons pressed = IntentButtons.None;
            if (c.MoveLeft.IsPressed) pressed |= IntentButtons.MoveLeft;
            if (c.MoveRight.IsPressed) pressed |= IntentButtons.MoveRight;
            if (c.MoveUp.IsPressed) pressed |= IntentButtons.MoveUp;
            if (c.MoveDown.IsPressed) pressed |= IntentButtons.MoveDown;
            if (c.Shoot.IsPressed) pressed |= IntentButtons.Shoot;
            if (c.Zoom.IsPressed) pressed |= IntentButtons.Zoom;
            if (c.Jump.IsPressed) pressed |= IntentButtons.Jump;
            if (c.Morph.IsPressed) pressed |= IntentButtons.Morph;
            if (c.Boost.IsPressed) pressed |= IntentButtons.Boost;
            if (c.AltAttack.IsPressed) pressed |= IntentButtons.AltAttack;
            if (c.ScanVisor.IsPressed) pressed |= IntentButtons.ScanVisor;
            if (c.NextWeapon.IsPressed) pressed |= IntentButtons.NextWeapon;
            if (c.PrevWeapon.IsPressed) pressed |= IntentButtons.PrevWeapon;
            if (c.RolltLeft.IsPressed) pressed |= IntentButtons.RollLeft;
            if (c.RollRight.IsPressed) pressed |= IntentButtons.RollRight;
            if (c.RollUp.IsPressed) pressed |= IntentButtons.RollUp;
            if (c.RollDown.IsPressed) pressed |= IntentButtons.RollDown;
            for (int i = _pressHistory.Length - 1; i > 0; i--)
            {
                _pressHistory[i] = _pressHistory[i - 1];
            }
            _pressHistory[0] = (uint)pressed;
        }

        /// <summary>Local player's controls and aim -> wire intent (client side).</summary>
        public static IntentPacket CaptureIntent(PlayerEntity player)
        {
            PlayerControls c = player.Controls;
            IntentButtons buttons = IntentButtons.None;
            if (c.MoveLeft.IsDown) buttons |= IntentButtons.MoveLeft;
            if (c.MoveRight.IsDown) buttons |= IntentButtons.MoveRight;
            if (c.MoveUp.IsDown) buttons |= IntentButtons.MoveUp;
            if (c.MoveDown.IsDown) buttons |= IntentButtons.MoveDown;
            if (c.Shoot.IsDown) buttons |= IntentButtons.Shoot;
            if (c.Zoom.IsDown) buttons |= IntentButtons.Zoom;
            if (c.Jump.IsDown) buttons |= IntentButtons.Jump;
            if (c.Morph.IsDown) buttons |= IntentButtons.Morph;
            if (c.Boost.IsDown) buttons |= IntentButtons.Boost;
            if (c.AltAttack.IsDown) buttons |= IntentButtons.AltAttack;
            if (c.ScanVisor.IsDown) buttons |= IntentButtons.ScanVisor;
            if (c.NextWeapon.IsDown) buttons |= IntentButtons.NextWeapon;
            if (c.PrevWeapon.IsDown) buttons |= IntentButtons.PrevWeapon;
            if (c.RolltLeft.IsDown) buttons |= IntentButtons.RollLeft;
            if (c.RollRight.IsDown) buttons |= IntentButtons.RollRight;
            if (c.RollUp.IsDown) buttons |= IntentButtons.RollUp;
            if (c.RollDown.IsDown) buttons |= IntentButtons.RollDown;
            // The owner's own answer, not an edge for the receiver to rebuild.
            if (player.EquipInfo.Zoomed) buttons |= IntentButtons.ZoomedState;
            // Which frame Position below is measured in. See
            // IntentButtons.AltFormState.
            if (player.IsAltForm) buttons |= IntentButtons.AltFormState;
            // Whether Position below is where this player is, or where its
            // body is lying. See IntentButtons.InPlayState.
            if (player.LoadFlags.TestFlag(LoadFlags.Spawned) && player.Health > 0)
            {
                buttons |= IntentButtons.InPlayState;
            }
            // Watching rather than playing. The only route this has to the
            // rest of the match: see IntentButtons.SpectatingState.
            if (player.Flags2.TestFlag(PlayerFlags2.Spectating))
            {
                buttons |= IntentButtons.SpectatingState;
            }
            // Ready for the next match. Only the server reads it, and only
            // while the results screen is up -- see DedicatedServer's end
            // sequence.
            if (Mods.EndScreen.Ready)
            {
                buttons |= IntentButtons.ReadyState;
            }
            return new IntentPacket
            {
                Buttons = buttons,
                Aim = player.ModGunVector,
                Position = player.Position,
                // The owner's own weapon, every frame. The authority never
                // receives snapshots, so without this it showed a remote
                // player holding whatever a relayed NextWeapon press happened
                // to select from the weapons *it* believed that player had --
                // and availability comes from pickups, which are not shared.
                WeaponSelect = (byte)player.CurrentWeapon,
                // The owner's own count. Everyone simulates this player's
                // shots and spends the ammo; only the owner walks over the
                // pickups that refill it, so every other machine's copy runs
                // down and eventually refuses to spawn a beam at all.
                AmmoUa = (ushort)Math.Clamp(player.ModAmmo.Ua, 0, UInt16.MaxValue),
                AmmoMissiles = (ushort)Math.Clamp(player.ModAmmo.Missiles, 0, UInt16.MaxValue),
                Presses = (uint[])_pressHistory.Clone(),
                // Which frame of the authority's simulation this player was
                // looking at while they aimed and fired. The authority rewinds
                // everybody else to it before resolving the shot -- see
                // NetUnlagged. Zero on the authority itself, which is never
                // behind, and on a client that has not been sent a snapshot
                // yet; both are read as "no rewind".
                AckFrame = NetSession.LastSnapshotFrame
            };
        }

        /// <summary>
        /// Wire intent -> a remote player's controls (authority side). Mirrors
        /// how the keyboard path derives IsPressed/IsReleased from the
        /// previous frame, so gameplay code that tests those edges behaves
        /// the same for a remote player as for a local one.
        /// </summary>
        /// <summary>
        /// The bits of an intent that mean somebody pressed something, as
        /// opposed to the four that describe what state the sender is in.
        ///
        /// The difference matters for <see cref="PlayerEntity.ModNoteInput"/>:
        /// `InPlayState` is set on every packet a living player sends, so
        /// counting the whole mask would make a puppet look busy while its
        /// owner stood perfectly still -- and the engine lowers an idle
        /// player's gun, which their own screen would then be doing and
        /// nobody else's. Replicating the idle means replicating the idle.
        /// </summary>
        private const IntentButtons PressedButtons = ~(IntentButtons.ZoomedState
            | IntentButtons.AltFormState | IntentButtons.InPlayState
            | IntentButtons.SpectatingState | IntentButtons.ReadyState);

        /// <summary>Newest press frame already applied, per slot.</summary>
        private static readonly uint[] _lastPressFrame = new uint[PlayerEntity.SlotCapacity];
        private static readonly bool[] _pressSeen = new bool[PlayerEntity.SlotCapacity];

        public static void ApplyIntent(PlayerEntity player, in IntentPacket intent)
        {
            if (!Sane(intent.Aim))
            {
                RejectedUpdates++;
                NetLog.Event($"slot {player.SlotIndex} intent rejected: aim={intent.Aim}");
                return;
            }
            PlayerControls c = player.Controls;
            IntentButtons missed = MissedPresses(player.SlotIndex, intent);
            Set(c.MoveLeft, intent.Buttons.HasFlag(IntentButtons.MoveLeft), missed.HasFlag(IntentButtons.MoveLeft));
            Set(c.MoveRight, intent.Buttons.HasFlag(IntentButtons.MoveRight), missed.HasFlag(IntentButtons.MoveRight));
            Set(c.MoveUp, intent.Buttons.HasFlag(IntentButtons.MoveUp), missed.HasFlag(IntentButtons.MoveUp));
            Set(c.MoveDown, intent.Buttons.HasFlag(IntentButtons.MoveDown), missed.HasFlag(IntentButtons.MoveDown));
            Set(c.Shoot, intent.Buttons.HasFlag(IntentButtons.Shoot), missed.HasFlag(IntentButtons.Shoot));
            Set(c.Zoom, intent.Buttons.HasFlag(IntentButtons.Zoom), missed.HasFlag(IntentButtons.Zoom));
            Set(c.Jump, intent.Buttons.HasFlag(IntentButtons.Jump), missed.HasFlag(IntentButtons.Jump));
            Set(c.Morph, intent.Buttons.HasFlag(IntentButtons.Morph), missed.HasFlag(IntentButtons.Morph));
            if (c.Morph.IsPressed)
            {
                NetLog.Event($"slot {player.SlotIndex} morph press received, now {player.ModFormState()}");
            }
            Set(c.Boost, intent.Buttons.HasFlag(IntentButtons.Boost), missed.HasFlag(IntentButtons.Boost));
            Set(c.AltAttack, intent.Buttons.HasFlag(IntentButtons.AltAttack), missed.HasFlag(IntentButtons.AltAttack));
            Set(c.ScanVisor, intent.Buttons.HasFlag(IntentButtons.ScanVisor), missed.HasFlag(IntentButtons.ScanVisor));
            Set(c.NextWeapon, intent.Buttons.HasFlag(IntentButtons.NextWeapon), missed.HasFlag(IntentButtons.NextWeapon));
            Set(c.PrevWeapon, intent.Buttons.HasFlag(IntentButtons.PrevWeapon), missed.HasFlag(IntentButtons.PrevWeapon));
            Set(c.RolltLeft, intent.Buttons.HasFlag(IntentButtons.RollLeft), missed.HasFlag(IntentButtons.RollLeft));
            Set(c.RollRight, intent.Buttons.HasFlag(IntentButtons.RollRight), missed.HasFlag(IntentButtons.RollRight));
            Set(c.RollUp, intent.Buttons.HasFlag(IntentButtons.RollUp), missed.HasFlag(IntentButtons.RollUp));
            Set(c.RollDown, intent.Buttons.HasFlag(IntentButtons.RollDown), missed.HasFlag(IntentButtons.RollDown));
            if (intent.WeaponSelect != 0xFF)
            {
                player.ModSetWeapon((BeamType)intent.WeaponSelect);
            }
            player.ModSetAmmo(intent.AmmoUa, intent.AmmoMissiles);
            // Somebody is playing this hunter, even though it is not this
            // machine's keyboard doing it.
            //
            // Without this a puppet looked idle from the moment its owner
            // stopped respawning or changing weapon, and the engine lowers an
            // idle player's gun -- which `CanShoot` refuses to fire through.
            // So a player holding still and firing, which is what a sniper
            // does, had their shots fail to spawn on every other machine
            // including the authority, whose shots are the only ones that
            // count. See PlayerEntity.ModNoteInput.
            if ((intent.Buttons & PressedButtons) != IntentButtons.None)
            {
                player.ModNoteInput();
            }
            // After the weapon, because zoom belongs to one and the engine
            // refuses it on a weapon that cannot. Taken as state rather than
            // rebuilt from the press: see IntentButtons.ZoomedState.
            player.ModSetZoom(intent.Buttons.HasFlag(IntentButtons.ZoomedState));
            // The owner's own answer about whether it is still in the match.
            // On the authority this is what makes a spectator stop being a
            // target; from there the snapshot's FlagSpectating carries it to
            // everybody else.
            player.ModSetSpectating(intent.Buttons.HasFlag(IntentButtons.SpectatingState));
            // And which form its owner says it is in -- but only here, on the
            // machine that answers that question for everybody else.
            //
            // The form was replicated by replaying the morph *press* through
            // the engine and nothing else, which works until one of those
            // presses does not take: a packet lost at the wrong moment, or a
            // press that arrives while the puppet is somewhere it cannot
            // unmorph. The authority's copy is then in the wrong form for the
            // rest of the life -- and, since FlagAltForm in every snapshot is
            // read off that copy, every other client agrees with it. The one
            // machine that knows better is the owner's, and nothing was
            // asking. Reported as "a player appears to everyone else as being
            // in alt form when they are not".
            //
            // Its own answer, not an edge to rebuild, exactly like the zoom
            // and the spectating flag above it: a state cannot be lost the way
            // an edge can. Through ApplyForm rather than as a flag, so the
            // grace period still protects the round trip in which a puppet is
            // legitimately ahead of its owner's own report, and so the
            // transition is attempted before it is forced.
            //
            // Only on the authority. A client that also acted on this would be
            // taking form corrections from two sources at once -- the owner's
            // intent and the authority's snapshot -- and the two disagree for
            // exactly as long as it takes the authority to converge, which is
            // long enough for the puppet to be pulled both ways.
            if (NetSession.IsAuthority)
            {
                ApplyForm(player, intent.Buttons.HasFlag(IntentButtons.AltFormState));
            }
        }

        /// <summary>
        /// Rising edges this packet carries that this slot has not applied
        /// yet, taken from the packet's short history of them.
        ///
        /// Without this, an edge existed only in the single packet whose
        /// frame it fell on, and losing that packet lost the action outright.
        /// The frame each entry belongs to is what stops a press being
        /// applied twice when the redundant copies arrive.
        /// </summary>
        private static IntentButtons MissedPresses(int slot, in IntentPacket intent)
        {
            if (slot < 0 || slot >= _lastPressFrame.Length || intent.Presses == null)
            {
                return IntentButtons.None;
            }
            if (!_pressSeen[slot])
            {
                // First packet from this peer: note where their frame counter
                // stands and replay nothing. The history reaches back several
                // frames, and applying all of it would open with a burst of
                // presses from before this client was listening.
                _pressSeen[slot] = true;
                _lastPressFrame[slot] = intent.Frame;
                return IntentButtons.None;
            }
            IntentButtons missed = IntentButtons.None;
            for (int i = intent.Presses.Length - 1; i >= 0; i--)
            {
                if (intent.Frame < (uint)i)
                {
                    continue;
                }
                uint frame = intent.Frame - (uint)i;
                if (frame <= _lastPressFrame[slot])
                {
                    continue;
                }
                missed |= (IntentButtons)intent.Presses[i];
            }
            // Every frame up to this packet is now accounted for, whether or
            // not it carried a press. Leaving gaps here let the same frame be
            // consumed again by a later packet.
            _lastPressFrame[slot] = Math.Max(_lastPressFrame[slot], intent.Frame);
            return missed;
        }

        /// <summary>
        /// Drive one control from a relayed intent.
        ///
        /// The held state comes from the packet's button levels, but the
        /// rising edge comes only from the press history -- never from the
        /// level as well. Deriving it from both applied the same press twice:
        /// once when the level went down, once when the redundant copy
        /// arrived. For a toggle like morph, twice is the same as never, and
        /// the puppet ended up one transition behind its owner for the rest
        /// of the match -- drawn as a biped while morphed, and as a morph
        /// ball while walking.
        /// </summary>
        private static void Set(Keybind bind, bool down, bool pressed = false)
        {
            bool wasDown = bind.IsDown;
            bind.IsDown = down || pressed;
            bind.IsPressed = pressed;
            bind.IsReleased = !down && wasDown && !pressed;
        }

        /// <summary>
        /// Authoritative state -> a player, on a client that is not the
        /// authority.
        ///
        /// Snapping, not interpolating: correctness first. Smoothing belongs
        /// on top of a working baseline, not underneath one -- interpolating
        /// before the plain path is proven only hides where the two sides
        /// disagree.
        ///
        /// The cases are deliberately different. Somebody else's player is a
        /// puppet and takes everything, including the spawn itself, because
        /// Spawn() is what unhides the model. This machine's own player takes
        /// its spawn, its death and its health from the authority too -- those
        /// are the match, and a client that decided them for itself was
        /// playing a different one -- but keeps its facing, because aim has to
        /// answer the mouse now rather than after a round trip, and keeps its
        /// own position -- see the isLocal branch, and
        /// <see cref="DesyncDistance"/> for the one case that overrides it.
        /// </summary>
        public static void ApplyState(PlayerEntity player, in PlayerState state, bool isLocal)
        {
            if (!Sane(state.Position) || !Sane(state.Speed) || !Sane(state.Facing))
            {
                RejectedUpdates++;
                NetLog.Event($"slot {player.SlotIndex} snapshot rejected: "
                    + $"pos={state.Position} speed={state.Speed} facing={state.Facing}");
                return;
            }
            bool spawned = (state.Flags & PlayerState.FlagSpawned) != 0;
            bool wasInPlay = player.LoadFlags.TestFlag(LoadFlags.Spawned) && player.Health > 0;
            int slot = player.SlotIndex;
            // The frame the authority put this player back on the map.
            bool justPlaced = spawned && slot >= 0 && slot < _authoritySpawned.Length
                && !_authoritySpawned[slot];
            if (slot >= 0 && slot < _authoritySpawned.Length)
            {
                _authoritySpawned[slot] = spawned;
            }
            // The authority keeps the score for everybody, including for this
            // client's own player. Counting locally worked only for whoever
            // had been present since the first kill.
            //
            // Not for the second after a rotation, for the same reason peer
            // positions are ignored then: the two machines do not change room
            // on the same frame, so a client that finished loading first is
            // still being sent the finished match's snapshots -- and those
            // carry the winning score. Applying it to the fresh match put the
            // point goal back on the board on the frame it started, which
            // ended the new match instantly. Scores begin a match at zero on
            // every machine, so there is nothing to learn from the authority
            // during that second anyway.
            if (slot >= 0 && slot < GameState.Points.Length && !NetRoomChange.Settling)
            {
                GameState.Points[slot] = state.Points;
                GameState.Kills[slot] = state.Kills;
                GameState.Deaths[slot] = state.Deaths;
            }
            // Before health is reconciled, because the engine's damage
            // feedback is produced by the hit rather than by the number: a
            // client that only assigned the new health showed a bar dropping
            // in silence, with no indicator, no animation and no kill banner.
            NetDamage.Replay(player, state);
            if (!spawned)
            {
                // Waiting to be placed, or just killed. Health is the whole
                // point of this branch: it is how a client learns that it
                // died, and skipping it left a player who had been killed on
                // every other screen still walking around on its own.
                if (wasInPlay && state.Health == 0 && player.Health > 0)
                {
                    // Killed by something that leaves no damage record: a
                    // fall, a kill plane, the match ending them. Assigning
                    // zero health would look right and count nothing, so the
                    // scoreboards drifted apart by exactly those deaths.
                    player.ModNetDie();
                }
                player.Health = state.Health;
                return;
            }
            if (!wasInPlay)
            {
                // The authority has this player on the map and this machine
                // does not. Spawn() rather than a position write: it is what
                // clears HideModel, so a player that skipped it tracked
                // perfectly while drawing nothing at all.
                player.ModNetSpawn(state.Position, state.Facing);
            }
            if (isLocal)
            {
                // Position and speed stay with the machine playing this
                // character; only the match -- health, score, spawn, death --
                // comes from the authority.
                //
                // Taking them from the snapshot closes a loop with no way
                // out. The authority does not simulate a remote player's
                // movement: it puts the puppet wherever the owner's last
                // intent said. So the position it publishes for this client
                // *is* this client's own report from a round trip ago, and
                // writing it back here means the next intent carries it
                // again unchanged. The two values agree forever, the
                // character is pinned to the spot the loop closed on, and
                // every frame of local movement is computed and thrown away
                // before it is ever published. That froze every client except
                // the authority -- 0 units travelled in seventy seconds, on
                // loopback as much as over the wire.
                //
                // A respawn or a death does not come through here: those are
                // the !wasInPlay and !spawned branches above. What is left is
                // a divergence no latency can explain, and only that is
                // taken.
                if (NetRoomChange.Settling)
                {
                    // Nothing about position means anything for the second
                    // after a room change: this client has loaded the new
                    // room and the authority may not have, so its snapshot is
                    // still describing where everybody stood in the old one.
                    // Taking it drags this player to whatever those
                    // coordinates land on here, the local simulation walks
                    // back, and the two alternate -- measured at a six-player
                    // rotation, twenty-six corrections in four seconds
                    // between two fixed points a room apart.
                    //
                    // NoteRoomChanged has cleared the placement record, so
                    // the first snapshot after this window that says this
                    // player is spawned counts as a fresh placement and puts
                    // it where the authority wants it.
                }
                else if (justPlaced)
                {
                    // Except at a respawn, which is the one moment the
                    // authority owns this player's position outright.
                    //
                    // `GetRespawnPoint` chooses from the spawn points that are
                    // free of living players *on the machine running it*, and
                    // rotates its choice with the frame counter, so two
                    // machines running it a few frames apart do not pick the
                    // same one. The local player also respawns early by
                    // holding fire, so it makes that choice well before the
                    // authority makes its own. Both then believe they know
                    // where this player is standing, half a level apart, and
                    // nothing brings them back together: the client publishes
                    // its own spot in every intent and the authority replies
                    // with the other one, for the rest of the life.
                    //
                    // Fifty-five of these in a hundred seconds, at up to 175
                    // units. The old code hid it -- it overwrote this
                    // player's position from every snapshot, which froze it
                    // solid but did make the two agree.
                    //
                    // Spawning locally first is kept: it is what makes a
                    // respawn feel immediate rather than arrive a round trip
                    // later, and it is what still works if snapshots stall.
                    // Only the placement is handed over -- and only when it
                    // is a placement this room could have made.
                    //
                    // The settling window above covers a client that loaded
                    // faster than the authority, but only for a second, and
                    // loading a room is not a bounded thing: on a phone, or
                    // off a slow disk, the authority can still be in the map
                    // before the rotation long after that. Its snapshot then
                    // places this player at coordinates that meant a spawn
                    // point *there*, and here they are somewhere outside the
                    // level -- which is the report about spawning into a
                    // black world and falling out of it. See
                    // ModPlacementBelongsHere.
                    if (player.ModPlacementBelongsHere(state.Position))
                    {
                        Move(player, state.Position);
                    }
                    else
                    {
                        PlacementsRefused++;
                        NetLog.Event($"slot {player.SlotIndex} kept its own spawn: the "
                            + $"authority placed it at {state.Position}, which is not "
                            + "near any spawn point in this room");
                        // Keep the local spawn and let the next divergence
                        // check settle the two, which it will as soon as the
                        // authority is describing this room.
                        _authoritySpawned[slot] = false;
                    }
                    // Not the authority's speed: a player that has just been
                    // put on a spawn point is standing still, and whatever the
                    // snapshot carries here was derived across the teleport
                    // that put it there.
                    player.Speed = Vector3.Zero;
                }
                else if (Diverged(player, state, slot))
                {
                    NetLog.Event($"slot {player.SlotIndex} pulled back to the authority "
                        + $"from {player.Position} to {state.Position}");
                    Move(player, state.Position);
                    player.Speed = state.Speed;
                    _divergedFrames[slot] = 0;
                }
                player.Health = state.Health;
                // Including for this machine's own player: being frozen is
                // part of the match, like health and the score, and a victim
                // who kept walking about while the authority held them still
                // was the whole of the "frozen players who keep moving" bug.
                player.ModSetFrozen((state.Flags & PlayerState.FlagFrozen) != 0);
                ApplyAfflictions(player, state);
                return;
            }
            // Converted for the same reason the reported position is: the
            // authority published this while its own copy was in whatever
            // form FlagAltForm says, and this copy may not be in that form
            // yet.
            Move(player, InForm(player, state.Position,
                (state.Flags & PlayerState.FlagAltForm) != 0));
            player.Speed = state.Speed;
            player.Health = state.Health;
            player.ModSetFacing(state.Facing);
            player.ModSetWeapon((BeamType)state.CurrentWeapon);
            player.EquipInfo.Zoomed = (state.Flags & PlayerState.FlagZoomed) != 0;
            ApplyForm(player, (state.Flags & PlayerState.FlagAltForm) != 0);
            // Hidden and non-solid on this machine too, not just the one
            // whose input is frozen -- Quake 3's spectator, not a player who
            // merely stopped moving.
            player.ModSetSpectating((state.Flags & PlayerState.FlagSpectating) != 0);
            player.ModSetFrozen((state.Flags & PlayerState.FlagFrozen) != 0);
            ApplyAfflictions(player, state);
        }

        /// <summary>
        /// The two afflictions that are shown rather than simulated: the Volt
        /// Driver's disruption and the Magmaul's fire.
        ///
        /// Both are applied by <c>TakeDamage</c> from the beam entity that
        /// landed the hit, and a beam only ever exists on the machine that
        /// resolved it -- so before this, neither reached the victim on their
        /// own screen, or anybody watching. Carried as state for the same
        /// reason the freeze is, and applied to this machine's own player as
        /// well as to the puppets: being disrupted is something you are, not
        /// something the person who shot you can see.
        /// </summary>
        private static void ApplyAfflictions(PlayerEntity player, PlayerState state)
        {
            player.ModSetDisrupted((state.Flags & PlayerState.FlagDisrupted) != 0);
            player.ModSetBurning((state.Flags & PlayerState.FlagBurning) != 0);
        }

        /// <summary>
        /// Keep a remote player's form in step with the authority's, without
        /// stepping on the transition.
        ///
        /// The owner's relayed input drives the morph on every machine, so
        /// this is only a safety net for a transition that never happened at
        /// all -- a lost press, or a puppet that somehow stalled.
        ///
        /// It deliberately does nothing for a long while. A puppet acts on
        /// the press the moment it arrives, whereas the snapshot confirming
        /// it cannot come back until the authority has seen the press and
        /// published: for that round trip the puppet is *ahead* of the
        /// snapshot, not wrong. Treating that as a disagreement and
        /// "correcting" it made the puppet morph, unmorph and morph again on
        /// every single transition.
        /// </summary>
        private static void ApplyForm(PlayerEntity player, bool altForm)
        {
            int slot = player.SlotIndex;
            if (slot < 0 || slot >= _formMismatch.Length)
            {
                return;
            }
            if (player.IsAltForm == altForm)
            {
                _formMismatch[slot] = 0;
                _formAttempts[slot] = 0;
                return;
            }
            _formMismatch[slot]++;
            if (_formMismatch[slot] <= FormGraceFrames)
            {
                return;
            }
            _formMismatch[slot] = 0;
            // First the real transition, because that is what creates the
            // parts of a form that are separate entities -- Weavel's
            // halfturret exists only because EnterAltForm adds it, so a
            // client that skipped straight to the flag showed a Weavel in alt
            // form with no turret. Only if that does not take does the flag
            // get forced.
            if (_formAttempts[slot] == 0)
            {
                _formAttempts[slot] = 1;
                player.ModStartFormSwitch();
                return;
            }
            _formAttempts[slot] = 0;
            player.ModForceForm(altForm);
        }

        private static readonly int[] _divergedFrames = new int[PlayerEntity.SlotCapacity];

        /// <summary>
        /// How long this machine's own player must look wrong before it is
        /// moved. Long enough that nothing latency can produce survives it.
        /// </summary>
        private const int DivergedFramesBeforeCorrecting = 60;

        /// <summary>
        /// Whether the authority's copy of this machine's own player is
        /// somewhere it cannot be explained by the trip.
        ///
        /// Comparing it against where the player is *now* is the wrong
        /// question, and asking it that way was a bug of its own. The
        /// authority's copy is this client's own report from a round trip
        /// ago, so under anything fast the two are legitimately far apart:
        /// a player falling out of the level covers thirty units in the half
        /// second a 250 ms link takes to answer, and correcting that hauled it
        /// back up out of the fall, over and over, so it could never die.
        /// Seventy-seven of those in one run, and the peers watching saw a
        /// player jumping 64 units at a time.
        ///
        /// So compare it against where this player *was* when the authority
        /// was looking -- its own recorded position, a ping's worth of frames
        /// back. That is the same instant, and a difference then is a real
        /// disagreement rather than a stale reading. It still has to persist,
        /// because one bad snapshot is not a desync.
        /// </summary>
        private static bool Diverged(PlayerEntity player, in PlayerState state, int slot)
        {
            if (slot < 0 || slot >= _divergedFrames.Length)
            {
                return false;
            }
            Vector3 then = player.Position;
            int lagFrames = slot < NetSession.SlotPing.Length
                ? Math.Clamp(NetSession.SlotPing[slot] * 60 / 1000, 0, 100)
                : 0;
            if (lagFrames > 0 && NetSession.NetFrame > (uint)lagFrames
                && player.ModGetNetworkPosition(NetSession.NetFrame - (uint)lagFrames, out Vector3 past))
            {
                then = past;
            }
            if ((state.Position - then).LengthSquared <= DesyncDistance * DesyncDistance)
            {
                _divergedFrames[slot] = 0;
                return false;
            }
            _divergedFrames[slot]++;
            return _divergedFrames[slot] >= DivergedFramesBeforeCorrecting;
        }

        /// <summary>
        /// Forget where the authority had everybody standing, because it was
        /// in a different room. The next snapshot that reports a player
        /// spawned then counts as a placement rather than as a continuation,
        /// which is what re-seats everyone after a rotation.
        /// </summary>
        public static void NoteRoomChanged()
        {
            Array.Clear(_authoritySpawned);
            Array.Clear(_reportSeen);
            Array.Clear(_divergedFrames);
            Array.Clear(_spawnIntentFrame);
            Array.Clear(_wasInPlay);
            Array.Clear(_staleFrames);
        }

        public static void Reset()
        {
            Array.Clear(_formMismatch);
            Snaps = 0;
            WorstSnap = 0;
            NodeLookupsUnresolved = 0;
            PlacementsRefused = 0;
            Array.Clear(_formAttempts);
            Array.Clear(_lastPressFrame);
            Array.Clear(_pressSeen);
            Array.Clear(_pressHistory);
            Array.Clear(_authoritySpawned);
            Array.Clear(_divergedFrames);
            Array.Clear(_spawnIntentFrame);
            Array.Clear(_wasInPlay);
            Array.Clear(_staleFrames);
            Array.Clear(_lastReportPosition);
            Array.Clear(_lastReportFrame);
            Array.Clear(_reportSeen);
        }

        /// <summary>
        /// Forget everything remembered about one slot, because whoever was in
        /// it has gone and the next occupant is a different person.
        ///
        /// Every array above is indexed by slot and, until this existed, was
        /// cleared only when the whole session started or stopped, or when the
        /// room changed. A slot that changed hands mid-match therefore handed
        /// the newcomer the previous occupant's history -- their last reported
        /// position and frame number, their spawn barrier, their divergence
        /// and staleness counters.
        ///
        /// That is not a theoretical hazard; StaleSinceSpawn names it in so
        /// many words: "a peer that reconnects restarts its counter at zero,
        /// and a slot that changes hands inherits the barrier of whoever held
        /// it... which is a player nobody can hit and who slides without ever
        /// taking a step". It is bounded there by a 120-frame give-up, so it
        /// costs two seconds rather than a session -- but the bound is a
        /// mitigation for a state that should not exist, and two seconds of a
        /// player who cannot be hit is still the thing being reported.
        ///
        /// Cheap and unambiguous: a slot changing hands means the old
        /// occupant's history is meaningless by definition, so there is
        /// nothing to weigh up.
        /// </summary>
        public static void ForgetSlot(int slot)
        {
            if (slot < 0 || slot >= PlayerEntity.SlotCapacity)
            {
                return;
            }
            _formMismatch[slot] = 0;
            _formAttempts[slot] = 0;
            _lastPressFrame[slot] = 0;
            _pressSeen[slot] = false;
            _pressHistory[slot] = 0;
            _authoritySpawned[slot] = false;
            _divergedFrames[slot] = 0;
            _spawnIntentFrame[slot] = 0;
            _wasInPlay[slot] = false;
            _staleFrames[slot] = 0;
            _lastReportPosition[slot] = Vector3.Zero;
            _lastReportFrame[slot] = 0;
            _reportSeen[slot] = false;
        }

        /// <summary>
        /// Put a remote player where its owner says it is.
        ///
        /// Called for every client, the authority included, so there is
        /// exactly one simulation of each player: the one on the machine
        /// whose keyboard is driving it. Everyone else follows.
        /// </summary>
        public static void ApplyReportedPosition(PlayerEntity player, in IntentPacket intent)
        {
            if (!Sane(intent.Position))
            {
                RejectedUpdates++;
                return;
            }
            if (FrozenInPlace(player))
            {
                return;
            }
            if (intent.Position == Vector3.Zero)
            {
                return; // the owner has not spawned yet
            }
            if (StaleSinceSpawn(player, intent))
            {
                return;
            }
            Vector3 reported = InForm(player, intent.Position,
                intent.Buttons.HasFlag(IntentButtons.AltFormState));
            NoteReportedVelocity(player, reported, intent.Frame);
            Vector3 delta = reported - player.Position;
            float distance = delta.Length;
            if (distance > SnapDistance)
            {
                // Too far to be movement: a respawn, a teleporter, or a long
                // gap in the packets. Snapping is right here -- gliding across
                // half the level would be worse than a jump.
                Snaps++;
                WorstSnap = Math.Max(WorstSnap, distance);
                Move(player, reported);
                return;
            }
            // The owner also sends the aim that was calculated against this
            // position. Smoothing here leaves the authoritative hitbox behind
            // that aim under latency, so moving directly is required for
            // collision and rendering to agree.
            Move(player, reported);
        }

        /// <summary>
        /// The position half of <see cref="ApplyReportedPosition"/>, with none
        /// of its bookkeeping. Called a second time in the same frame, after
        /// the engine's movement step, so the velocity it derives and the
        /// snaps it counts must not be counted twice.
        /// </summary>
        public static void RestoreReportedPosition(PlayerEntity player, in IntentPacket intent)
        {
            if (!Sane(intent.Position) || intent.Position == Vector3.Zero
                || StaleSinceSpawn(player, intent) || FrozenInPlace(player))
            {
                return;
            }
            Move(player, InForm(player, intent.Position,
                intent.Buttons.HasFlag(IntentButtons.AltFormState)));
        }

        private static readonly uint[] _spawnIntentFrame = new uint[PlayerEntity.SlotCapacity];
        private static readonly bool[] _wasInPlay = new bool[PlayerEntity.SlotCapacity];
        private static readonly int[] _staleFrames = new int[PlayerEntity.SlotCapacity];

        /// <summary>
        /// The longest a puppet's position may be held back while its owner is
        /// still reporting frames from before it respawned. One round trip is
        /// thirty frames at 250 ms; this is four times that and then the guard
        /// gives up rather than waiting on a number that may never arrive.
        /// </summary>
        private const int StaleAfterSpawnFrames = 120;

        /// <summary>
        /// Whether this intent describes where a player stood before it was
        /// put back on the map, and so must not be acted on.
        ///
        /// This is the respawn glitch. A player dies; its owner keeps sending
        /// intents carrying the position it died at. The authority puts the
        /// puppet on a spawn point -- and on the very next frame this method
        /// moves it straight back to the death position, because that is what
        /// the newest intent from a round trip ago still says. It then
        /// publishes that as the authoritative position of a player it has
        /// flagged as spawned, for as long as the round trip lasts.
        ///
        /// What the owner does with that snapshot is the visible half: the
        /// first one it receives saying "you are spawned" is the one it takes
        /// its placement from, so it respawns correctly and is then teleported
        /// into wherever it had died -- through the floor as often as not, so
        /// its own screen goes black and everyone else sees a shadow and no
        /// model. Reported from play on the Pi after two or three respawns in
        /// one match, at fifteen milliseconds: it does not need latency, only
        /// a round trip.
        ///
        /// The barrier is the intent frame that was current when the puppet
        /// was placed. Anything up to and including it was composed before the
        /// spawn and describes a dead player; the first intent after it is the
        /// owner reporting where it actually is now.
        /// </summary>
        private static bool StaleSinceSpawn(PlayerEntity player, in IntentPacket intent)
        {
            int slot = player.SlotIndex;
            if (slot < 0 || slot >= _spawnIntentFrame.Length)
            {
                return false;
            }
            bool inPlay = player.LoadFlags.TestFlag(LoadFlags.Spawned) && player.Health > 0;
            if (inPlay && !_wasInPlay[slot])
            {
                _spawnIntentFrame[slot] = intent.Frame;
                _staleFrames[slot] = 0;
            }
            _wasInPlay[slot] = inPlay;
            if (!inPlay)
            {
                _staleFrames[slot] = 0;
                return false;
            }
            // The owner still says it is down, so what it is sending is where
            // its body is lying, not where it is. This is the test that
            // works: the frame number does not, because a dead player's
            // counter keeps rising and clears the barrier within two frames
            // while the position it carries stays on the corpse.
            if (!intent.Buttons.HasFlag(IntentButtons.InPlayState))
            {
                return true;
            }
            if (intent.Frame > _spawnIntentFrame[slot])
            {
                _staleFrames[slot] = 0;
                return false;
            }
            // Bounded, and that is the whole point of the counter. What this
            // has to cover is one round trip -- thirty frames at 250 ms -- and
            // a guard that waits for a frame number to grow can wait forever
            // if it never does: a peer that reconnects restarts its counter at
            // zero, and a slot that changes hands inherits the barrier of
            // whoever held it. Blocking a puppet's position for good would
            // leave its hitbox at the spawn point and its derived speed at
            // zero, which is a player nobody can hit and who slides without
            // ever taking a step -- and nothing would have said so.
            if (++_staleFrames[slot] <= StaleAfterSpawnFrames)
            {
                return true;
            }
            NetLog.Event($"slot {slot} still reporting pre-spawn frames after "
                + $"{_staleFrames[slot]} of them; following it anyway");
            _spawnIntentFrame[slot] = intent.Frame;
            _staleFrames[slot] = 0;
            return false;
        }

        private static readonly Vector3[] _lastReportPosition = new Vector3[PlayerEntity.SlotCapacity];
        private static readonly uint[] _lastReportFrame = new uint[PlayerEntity.SlotCapacity];
        private static readonly bool[] _reportSeen = new bool[PlayerEntity.SlotCapacity];

        /// <summary>
        /// How fast a puppet is travelling, worked out from the positions its
        /// owner reported rather than from a simulation of it.
        ///
        /// Nothing else fills this in. The authority skips a remote player's
        /// movement step entirely -- the owner already ran it and sent the
        /// result -- so Speed would keep whatever it last held, and it was
        /// therefore forced to zero. But Speed is in the snapshot, so that
        /// zero became the authoritative velocity of every remote player on
        /// every screen: opponents slid around at a dead stop, and each
        /// client had its own speed cleared sixty times a second.
        ///
        /// The gap between two reports is what it is divided by, so this
        /// stays right when a packet goes missing and the next one covers
        /// four frames instead of two.
        /// </summary>
        private static void NoteReportedVelocity(PlayerEntity player, Vector3 reported, uint frame)
        {
            int slot = player.SlotIndex;
            if (slot < 0 || slot >= _lastReportFrame.Length)
            {
                return;
            }
            if (_reportSeen[slot] && frame > _lastReportFrame[slot])
            {
                // Capped: a report that follows a long silence describes a
                // gap, not a frame of movement, and dividing by two hundred
                // is as wrong as dividing by one.
                uint elapsed = Math.Min(frame - _lastReportFrame[slot], 8);
                Vector3 travelled = reported - _lastReportPosition[slot];
                float step = travelled.Length;
                if (!Sane(travelled) || step > SnapDistance)
                {
                    // Not movement: a respawn, a teleporter, or a gap in the
                    // packets. Dividing a jump across the level by two frames
                    // produces a velocity of a hundred and fifty units a
                    // frame, and that number does not stay here -- it goes
                    // into the snapshot as this player's authoritative speed,
                    // every client applies it to its puppet, and the owner
                    // takes it back at its next respawn and is launched out of
                    // the level. Measured before this guard: the authority
                    // held a player at Y=163 and climbing 35 units a frame.
                    player.Speed = Vector3.Zero;
                }
                else
                {
                    Vector3 speed = travelled / elapsed;
                    float magnitude = speed.Length;
                    // Belt and braces. Boost, the fastest a hunter moves, caps
                    // at 0.6 units a frame; anything near this ceiling is
                    // already not a hunter running.
                    if (magnitude > MaxReportedSpeed)
                    {
                        speed *= MaxReportedSpeed / magnitude;
                    }
                    player.Speed = speed;
                }
            }
            if (!_reportSeen[slot] || frame > _lastReportFrame[slot])
            {
                _reportSeen[slot] = true;
                _lastReportFrame[slot] = frame;
                _lastReportPosition[slot] = reported;
            }
        }

        /// <summary>
        /// Move the player's room node along with it. NodeRef is what the
        /// renderer culls against (PlayerDraw: `IsMainPlayer ||
        /// IsVisible(NodeRef)`), and the engine normally advances it during
        /// simulation. Writing a position straight in skips that, so a remote
        /// player kept the node it spawned in and vanished -- or showed only
        /// a shadow -- as soon as the viewer was elsewhere.
        /// </summary>
        /// <summary>
        /// Whether this puppet is frozen, and so must not be moved by what its
        /// owner is still reporting.
        ///
        /// The other half of "frozen players who keep moving", and the half
        /// the state flag could not reach. A freeze is resolved on the
        /// authority, and its victim does not learn of it for a round trip --
        /// during which they are still walking about on their own machine and
        /// still reporting where they have got to. Every one of those reports
        /// was applied on top of a player the authority was holding perfectly
        /// still, so the host watched a block of ice slide across the room for
        /// as long as the trip took. At 250 ms that is fifteen frames of
        /// movement, which is exactly what it looks like.
        ///
        /// A frozen player cannot move: any position that arrives while the
        /// timer runs describes a moment before the ice, so there is nothing
        /// to lose by ignoring it. The local simulation still runs -- a frozen
        /// player falls -- and whatever the two copies disagree about by the
        /// time it thaws is what <see cref="Diverged"/> is for.
        /// </summary>
        private static bool FrozenInPlace(PlayerEntity player)
        {
            return player.ModFrozen;
        }

        private static void Move(PlayerEntity player, Vector3 position)
        {
            Vector3 previous = player.Position;
            player.Position = position;
            // This runs after PlayerProcess has captured PrevPosition. Keep
            // the next collision sweep anchored to the corrected position;
            // otherwise the engine treats the network correction as player
            // movement and can push the puppet away from the hitbox.
            player.PrevPosition = position;
            player.ModRefreshNodeRef(previous);
            // And the collision volume, which the engine only recomputes
            // inside the movement step this correction comes after. See
            // ModRefreshVolume: the shadow and the burn effect are drawn from
            // it, and shots are tested against it.
            player.ModRefreshVolume();
        }
    }
}
