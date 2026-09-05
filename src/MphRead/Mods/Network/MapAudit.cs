using System;
using System.Collections.Generic;
using System.Text;
using MphRead.Entities;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Loads one room with a full house of players, runs it, and reports what
    /// the map contains and what the players managed to do in it.
    ///
    /// Separate from the network check on purpose: this asks whether the
    /// *game* holds up -- every map, the maximum number of players, the world
    /// entities each level actually has -- with no server involved. A crash
    /// here is a crash that would happen in a real match too, and finding it
    /// on one machine is far cheaper than finding it with eight people
    /// connected.
    ///
    /// One process per room, because Scene.AddRoom refuses a second room and
    /// GLFW wants its window on the main thread. The caller loops over the
    /// room list.
    ///
    /// Usage: -maptest "MP3 PROVING GROUND" [-players 8] [-seconds 10]
    /// </summary>
    public sealed class MapAudit : GameWindow
    {
        private readonly string _room;
        private readonly int _players;
        private readonly double _seconds;
        private int _frame;
        private int _spawned;
        private readonly int[] _deaths = new int[PlayerEntity.SlotCapacity];
        private readonly int[] _lastHealth = new int[PlayerEntity.SlotCapacity];
        private readonly bool[] _everSpawned = new bool[PlayerEntity.SlotCapacity];
        private readonly bool[] _everAltForm = new bool[PlayerEntity.SlotCapacity];
        private readonly bool[] _everFired = new bool[PlayerEntity.SlotCapacity];
        // How far each player actually got. "The bots do not move" was a
        // report nothing here could confirm or deny: a bot standing still
        // spawns, so it counted as spawned, and never fires or dies, which
        // reads the same as one that is losing. This is the metric that
        // separates them.
        private readonly Vector3[] _lastSeen = new Vector3[PlayerEntity.SlotCapacity];
        private readonly bool[] _haveLastSeen = new bool[PlayerEntity.SlotCapacity];
        private readonly float[] _travelled = new float[PlayerEntity.SlotCapacity];
        // The three states only an affinity weapon can inflict. Counted per
        // slot rather than as a total, because "somebody was frozen" and "one
        // player was frozen forty times" are different reports.
        private readonly bool[] _everFrozen = new bool[PlayerEntity.SlotCapacity];
        private readonly bool[] _everBurned = new bool[PlayerEntity.SlotCapacity];
        private readonly bool[] _everDisrupted = new bool[PlayerEntity.SlotCapacity];
        private double _lowestY = Double.MaxValue;

        // The render probe. A map whose geometry does not draw is a map that
        // passes every check above -- the players spawn, the pads fire, the
        // teleporters move -- and is unplayable, because the picture is black
        // with a gun in front of it. Nothing but the pixels says so, so the
        // pixels are read: once a second, from the main player's own camera,
        // as the fraction of the frame that is not the clear colour.
        //
        // Sampled rather than read every frame: ReadSceneTarget is a
        // glReadPixels and a stall, and one a second over a 20 s tour is
        // plenty to catch a room that is dark from the spawn and one that
        // goes dark after walking into it.
        private string? _shotDirectory;
        private int _shotsSaved;
        private int _litSamples;
        private double _litMin = Double.MaxValue;
        private double _litMax;
        private double _litFirst = -1;
        private double _litTotal;
        private const int _litSampleFrames = 60;

        /// <summary>
        /// Below this fraction of the frame lit, the room is not being drawn.
        /// See the note where this is used.
        /// </summary>
        private const double _renderFloor = 0.06;

        // The spawn render sweep: -maptest -renderprobe.
        //
        // A direct reading of the report this exists for -- "spawn the player,
        // screenshot after the spawn, screenshot after walking forward five
        // seconds, and a near-black frame with only the gun in it is a
        // failure". Doing it from every spawn point in the room rather than
        // whichever one the tour happened to get, because a map that draws
        // from nine of its ten spawns and not the tenth is exactly the shape
        // of thing one manual run finds by luck and misses by luck.
        private readonly bool _renderProbe;
        private readonly List<Vector3> _spawnSpots = new();
        private readonly List<Vector3> _spawnFacings = new();
        private readonly List<Formats.Culling.NodeRef> _spawnNodeRefs = new();
        private int _spawnIndex = -1;
        private int _spawnFrames;
        private double _spawnLitAtSpawn;
        private double _spawnLitWorst;
        private int _spawnFailures;
        /// <summary>Frames to settle on the spawn before the first reading.</summary>
        private const int _spawnSettleFrames = 30;
        /// <summary>Five seconds of walking, which is what the report asks for.</summary>
        private const int _spawnWalkFrames = 300;

        // The world probe: after the tour, stand a player on every jump pad
        // and teleporter in turn and see whether it does anything.
        private readonly List<EntityBase> _probeTargets = new();
        private int _probeIndex = -1;
        private int _probeFrames;
        private int _probeWait;
        private bool _probePlaced;
        private int _probeAttempt;
        private int _probeMarkPads;
        private int _probeMarkTeleports;
        private int _padsProbed;
        private int _padsFired;
        private int _telesProbed;
        private int _telesFired;
        private const int _probeSlot = 0;
        // Long enough for a pad to notice a player standing on it -- it tests
        // the volume once a frame -- and short enough that thirty of them fit
        // in a couple of seconds. A pad that says nothing gets longer looks
        // afterwards, because a pad the tour has just fired is on a cooldown
        // of its own and would otherwise be reported as dead.
        private const int _probeFrameLimit = 12;
        private const int _probeRetryFrameLimit = 45;
        private const int _probeMaxTargets = 24;

        // The affliction probe: freeze, burn and disrupt exist only on a
        // hunter's own weapon, so each is tried by the one hunter that can
        // inflict it, at point-blank range, against a player standing still.
        private static readonly (Hunter Shooter, string Name)[] _afflictions =
        {
            (Hunter.Noxus, "freeze"),
            (Hunter.Spire, "burn"),
            (Hunter.Kanden, "disrupt")
        };
        private int _afflictIndex = -1;
        private int _afflictFrames;
        private int _afflictShooter = -1;
        private int _afflictVictim = -1;
        private readonly bool[] _afflictTried = new bool[3];
        private readonly bool[] _afflictLanded = new bool[3];
        // Whether our shooter ever got a shot off. "Nobody fired" and "the
        // victim was hit and did not catch fire" are different findings, and
        // only the second is about the affliction. Health is no use for this:
        // the victim stands still while six other players duel around it.
        private readonly bool[] _afflictFired = new bool[3];
        private readonly bool[] _afflictHit = new bool[3];
        private int _afflictVictimHealth;
        private int _afflictWait;
        private Affliction _afflictShotAfflictions;
        private int _afflictMaxCharge;
        private int _afflictSetUpWait;
        // Long enough for a projectile to cross three units and for the state
        // it inflicts to be visible for a frame.
        private const int _afflictFrameLimit = 320;

        /// <summary>
        /// Frames on which drawing moved the simulation's own frame counter --
        /// which drawing must never do. Any value but 0 is a failure.
        /// </summary>
        private int _drawAdvancedTheGame;

        private static GameWindowSettings GameSettings() => new() { UpdateFrequency = 60 };

        private static NativeWindowSettings WindowSettings() => new()
        {
            // Bigger for -hudshots: the HUD is authored for a 256x192 screen
            // and scaled to the window, so at 320x180 a weapon icon is a few
            // pixels and a capture of it says nothing.
            ClientSize = WindowSize ?? (ShowWindow ? new Vector2i(1024, 576) : new Vector2i(320, 180)),
            Title = "MphRead map audit",
            Profile = ContextProfile.Compatability,
            // Explicitly, exactly as the game's own window does. Left
            // unset, OpenTK's default gave this window a *forward-compatible*
            // context, which removes every deprecated entry point -- and this
            // engine draws in immediate mode, so that is all of them. The
            // profile mask still answers "compatibility", so nothing looked
            // wrong; the driver only admitted it in a shader warning that
            // mentioned "OGL 3.0 forward-compatible context". Every frame came
            // out black with GL_INVALID_OPERATION on an Intel Iris Xe, while
            // the game rendered perfectly on the same machine, because the
            // game sets this and these windows did not.
            Flags = ContextFlags.Default,
            APIVersion = new Version(3, 2),
            // Visible only for -hudshots, which reads the window's own buffer
            // because that is the one the HUD is drawn into. Everything else
            // reads the offscreen target and wants no window on screen.
            StartVisible = ShowWindow
        };

        /// <summary>Set by -hudshots before the window is built.</summary>
        public static bool ShowWindow { get; set; }

        /// <summary>
        /// Pictures drawn per simulation step, set by <c>-maptest -drawrate
        /// N</c>. 1 is the way this has always run.
        ///
        /// This is how the decoupled loop is checked without a display and
        /// without a 144 Hz monitor. It is deliberately not the wall-clock
        /// accumulator the game uses: the harness wants the same run every
        /// time and wants alpha to visit its whole range, so it steps alpha
        /// 1/N, 2/N .. 1 across the N draws instead of taking whatever the
        /// machine's load produces. What is being checked is the half that can
        /// actually be wrong -- that drawing more often does not change what
        /// the simulation does -- and the accumulator arithmetic that feeds it
        /// is checked separately by -frametimingcheck.
        /// </summary>
        public static int DrawRate { get; set; } = 1;

        /// <summary>
        /// What -size WxH asked for, before the window is built.
        ///
        /// The HUD is laid out in a 4:3 space and stretched to the window, so
        /// how it looks is partly a question about the window's shape -- a
        /// panel that is right at 16:9 can be two thirds too wide at 21:9.
        /// One fixed capture size cannot answer that; this lets a shell loop.
        /// </summary>
        public static Vector2i? WindowSize { get; set; }

        public Scene Scene { get; }

        /// <summary>
        /// Spawn every player without waiting for a fire button or a timer.
        /// Read by NetHooks.ForceSpawn, which is the one place the engine asks
        /// whether a waiting player should be placed now.
        /// </summary>
        public static bool ForceEveryone { get; internal set; }

        /// <summary>Print what the affliction probe saw. Set by -netdebug.</summary>
        public static bool Diagnostic { get; set; }

        /// <summary>
        /// Leave the other players as bots and let PlayerAi drive them,
        /// instead of replacing them with the scripted tour.
        ///
        /// A different code path from the rest of the audit and the one the
        /// launcher's offline match actually uses: the tour writes Controls
        /// directly and never touches the behaviour trees, so a bot-only
        /// crash is invisible to it.
        /// </summary>
        private readonly bool _bots;

        private MapAudit(string room, int players, double seconds, GameMode mode, bool bots,
            bool renderProbe)
            : base(GameSettings(), WindowSettings())
        {
            _renderProbe = renderProbe;
            _bots = bots;
            _room = room;
            _players = players;
            _seconds = seconds;
            // The whole tour has to fit inside one visit, and there are more
            // than thirty rooms to get through.
            NetTestScript.PhaseSeconds = Math.Max(1.5, seconds / NetTestScript.PhaseCount);
            // Offline, MaxPlayers is still the four a DS match could hold, so
            // PlayerEntity.Create hands back null for every slot past the
            // fourth and those players silently never exist.
            PlayerEntity.MaxPlayers = Math.Max(PlayerEntity.MaxPlayers, players);
            ForceEveryone = true;
            // Nothing else in the program reads these; the audit is the only
            // caller that wants to know a pad fired.
            Mods.WorldEvents.Watching = true;
            Mods.WorldEvents.Reset();
            Scene = new Scene(Size, KeyboardState, MouseState, _ => { }, Close);
            // A different hunter per slot, cycling, so one run exercises
            // several alt forms, several affinity weapons and several
            // collision volumes rather than eight copies of Samus.
            for (int i = 0; i < players; i++)
            {
                Scene.AddPlayer((Hunter)(i % 7), recolor: 0, team: -1);
            }
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                player.IsBot = bots && i > 0;
                player.BotLevel = bots ? 1 : 0;
                if (i >= players)
                {
                    player.LoadFlags &= ~LoadFlags.Active;
                }
            }
            PlayerEntity.PlayerCount = players;
            PlayerEntity.MainPlayerIndex = 0;
            Scene.AddRoom(room, mode, playerCount: NetLaunch.RoomPlayerCount);
        }

        protected override void OnLoad()
        {
            Scene.Size = ClientSize;
            Scene.OnLoad();
            base.OnLoad();
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            Scene.OnResize();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GameState.ApplyPause();
            if (DrawRate > 1 && Mods.Render.FrameTiming.ForcedAlpha == null)
            {
                Mods.Render.FrameTiming.ForcedAlpha = 1f;
            }
            // One simulation step, then however many pictures of it were
            // asked for. _frame counts steps, not pictures, so -seconds still
            // means seconds of game and every existing probe keeps its timing.
            Scene.OnSimulationFrame();
            ulong frameCountBefore = Scene.FrameCount;
            int draws = Math.Max(1, DrawRate);
            for (int i = 0; i < draws; i++)
            {
                if (draws > 1)
                {
                    // Set, and left set for the whole run rather than cleared
                    // between frames: CaptureDrawState reads it at the *end of
                    // the simulation step*, before this loop runs, to know
                    // whether anything is going to blend against what it would
                    // remember. Cleared each time, the capture would always be
                    // skipped and nothing would ever interpolate.
                    Mods.Render.FrameTiming.ForcedAlpha = (i + 1) / (float)draws;
                }
                Scene.OnDrawFrame();
                if (!Scene.OnRenderFrame())
                {
                    return;
                }
                if (i < draws - 1)
                {
                    // Every picture but the last is finished and thrown away:
                    // what is being measured is that making it changed nothing,
                    // and the last one is the one the samplers below read.
                    SwapBuffers();
                    Scene.AfterRenderFrame();
                }
            }
            // The one invariant this whole feature rests on: drawing does not
            // advance the game. If a draw ever writes back to the world, the
            // frame counter is where it shows up first.
            if (Scene.FrameCount != frameCountBefore)
            {
                _drawAdvancedTheGame++;
            }
            _frame++;
            if (_renderProbe)
            {
                if (!StepSpawnRender())
                {
                    SwapBuffers();
                    Scene.AfterRenderFrame();
                    base.OnRenderFrame(args);
                    Close();
                    return;
                }
                SwapBuffers();
                Scene.AfterRenderFrame();
                base.OnRenderFrame(args);
                return;
            }
            Drive();
            Observe();
            SampleRender();
            SwapBuffers();
            Scene.AfterRenderFrame();
            base.OnRenderFrame(args);
            if (_frame >= _seconds * 60 && (_bots || (!StepProbe() && !StepAfflictionProbe())))
            {
                Close();
            }
        }

        /// <summary>
        /// Every player is driven, not just one. Eight players moving, firing
        /// and morphing at once is the load a map has to survive, and it is
        /// also what pushes them onto the jump pads and into the pits.
        /// </summary>
        private void Drive()
        {
            for (int slot = 0; slot < _players && slot < PlayerEntity.Players.Count; slot++)
            {
                PlayerEntity player = PlayerEntity.Players[slot];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    continue;
                }
                if (!player.LoadFlags.TestFlag(LoadFlags.Spawned) || player.Health == 0)
                {
                    // Spawning is handled by ForceEveryone rather than by
                    // holding fire: the main player's controls are refilled
                    // from the keyboard every frame, so anything written here
                    // is gone before the spawn check reads it.
                    continue;
                }
                if (_afflictIndex >= 0)
                {
                    // Everybody stands down for the affliction probe. Six
                    // other players duelling around the victim make its health
                    // useless as evidence that our shot landed, and one of
                    // them killing the shooter costs the whole window.
                    if (slot != _afflictShooter && slot != _afflictVictim)
                    {
                        NetTestScript.Rest(player, wantBiped: false);
                    }
                    continue;
                }
                if (_bots)
                {
                    // PlayerAi is writing these; the tour would fight it.
                    continue;
                }
                if (_probeIndex >= 0 && slot == _probeSlot)
                {
                    // The prober stands still: a jump pad may be flagged to
                    // ignore alt forms, and a player still walking the tour
                    // has stepped off the pad before it looks.
                    NetTestScript.Rest(player, wantBiped: true);
                    continue;
                }
                NetTestScript.ApplyOffline(player, slot, _frame);
                // The script decides where to point; offline, nothing else
                // applies it.
                player.ModApplyScriptAim(NetTestScript.AimDeltaX, NetTestScript.AimDeltaY);
            }
        }

        /// <summary>
        /// Stand a player on the next jump pad or teleporter and watch.
        ///
        /// Returns false when there is nothing left to try, which is what
        /// ends the audit. Driving a player onto a pad by the tour alone was
        /// luck: the report said "jumppads 8" whether the map's pads worked
        /// or not. This asks each one directly.
        /// </summary>
        private bool StepProbe()
        {
            if (_probeIndex < 0)
            {
                CollectProbeTargets();
                _probeIndex = 0;
            }
            if (_probeIndex >= _probeTargets.Count || _probeSlot >= PlayerEntity.Players.Count)
            {
                return false;
            }
            PlayerEntity player = PlayerEntity.Players[_probeSlot];
            EntityBase target = _probeTargets[_probeIndex];
            if (!_probePlaced)
            {
                if (!player.LoadFlags.TestFlag(LoadFlags.Active)
                    || !player.LoadFlags.TestFlag(LoadFlags.Spawned) || player.Health == 0)
                {
                    // Between lives: ask to come back, and wait. If it never
                    // does, abandon the probe rather than hold the audit open.
                    NetTestScript.Rest(player, wantBiped: true);
                    if (++_probeWait > 240)
                    {
                        _probeIndex = _probeTargets.Count;
                    }
                    return true;
                }
                _probeWait = 0;
                _probeMarkPads = Mods.WorldEvents.JumpPadsFor(_probeSlot);
                _probeMarkTeleports = Mods.WorldEvents.TeleportsFor(_probeSlot);
                // Three tries before a pad is called silent. Two heights,
                // because a trigger volume is placed relative to the entity
                // and some carry theirs above the model rather than on it; and
                // then once in alt form, because a jump pad can be flagged to
                // ignore bipeds -- the ones in a morph ball tunnel are, and
                // reporting those as broken would be reporting the map for
                // working as designed.
                float lift = _probeAttempt == 1 ? 1.6f : 0.5f;
                player.ModForceForm(altForm: _probeAttempt == 2);
                Vector3 spot = TriggerPoint(target).AddY(lift);
                player.Teleport(spot, target.FacingVector, Scene.GetNodeRefByPosition(spot));
                _probePlaced = true;
                _probeFrames = 0;
                return true;
            }
            _probeFrames++;
            // This pad, this player: seven other players are still touring the
            // room and one of them landing on a different pad is not an answer
            // to the question being asked here.
            bool fired = target.Type == EntityType.JumpPad
                ? Mods.WorldEvents.JumpPadsFor(_probeSlot) > _probeMarkPads
                    && Mods.WorldEvents.LastJumpPadId(_probeSlot) == target.Id
                : Mods.WorldEvents.TeleportsFor(_probeSlot) > _probeMarkTeleports
                    && Mods.WorldEvents.LastTeleporterId(_probeSlot) == target.Id;
            int limit = _probeAttempt == 0 ? _probeFrameLimit : _probeRetryFrameLimit;
            if (!fired && _probeFrames < limit)
            {
                return true;
            }
            if (!fired && _probeAttempt < 2)
            {
                _probeAttempt++;
                _probePlaced = false;
                return true;
            }
            _probeAttempt = 0;
            player.ModForceForm(altForm: false);
            if (target.Type == EntityType.JumpPad)
            {
                _padsProbed++;
                if (fired)
                {
                    _padsFired++;
                }
            }
            else
            {
                _telesProbed++;
                if (fired)
                {
                    _telesFired++;
                }
            }
            _probeIndex++;
            _probePlaced = false;
            return true;
        }

        /// <summary>
        /// Try each affliction once: stand the hunter that can inflict it three
        /// units in front of somebody and hold the trigger.
        ///
        /// The tour reaches these states only when the right hunter happens to
        /// land a shot with its own weapon in the seconds the afflict phase
        /// lasts, which on a large map is rarely. This asks directly, so a
        /// report of "no afflictions" means the state is broken rather than
        /// that nobody got shot.
        /// </summary>
        private bool StepAfflictionProbe()
        {
            if (_afflictIndex < 0)
            {
                _afflictIndex = 0;
                _afflictFrames = 0;
                _afflictShooter = -1;
            }
            if (_afflictIndex >= _afflictions.Length)
            {
                return false;
            }
            if (_afflictShooter < 0)
            {
                if (!SetUpAffliction(_afflictions[_afflictIndex].Shooter))
                {
                    // Usually the hunter that inflicts this one is dead for a
                    // moment; wait for it before deciding it is not here.
                    if (++_afflictSetUpWait > 150)
                    {
                        _afflictSetUpWait = 0;
                        _afflictIndex++;
                    }
                    return true;
                }
                _afflictSetUpWait = 0;
                _afflictTried[_afflictIndex] = true;
                _afflictFrames = 0;
                _afflictVictimHealth = PlayerEntity.Players[_afflictVictim].Health;
                return true;
            }
            PlayerEntity shooter = PlayerEntity.Players[_afflictShooter];
            PlayerEntity victim = PlayerEntity.Players[_afflictVictim];
            if (!Alive(shooter) || !Alive(victim))
            {
                // One of them is between lives. Ask to come back and wait,
                // rather than spend the window on a corpse.
                NetTestScript.Rest(shooter, wantBiped: true);
                NetTestScript.Rest(victim, wantBiped: true);
                if (++_afflictWait > 240)
                {
                    _afflictLanded[_afflictIndex] = false;
                    _afflictIndex++;
                    _afflictShooter = -1;
                    _afflictVictim = -1;
                    _afflictWait = 0;
                }
                return true;
            }
            _afflictWait = 0;
            if (shooter.IsAltForm || shooter.IsMorphing || shooter.IsUnmorphing)
            {
                // A morph ball cannot fire a beam. Ask it to come out and
                // spend no window frames waiting.
                NetTestScript.Rest(shooter, wantBiped: true);
                NetTestScript.Rest(victim, wantBiped: true);
                return true;
            }
            _afflictFrames++;
            {
                shooter.ModArmAffinityWeapon();
                // Level, from chest height to chest height. Aiming between
                // the two collision volumes' centres sounds more precise and
                // is worse: they sit low, so the shot goes into the floor a
                // couple of units short.
                Vector3 toVictim = victim.Position.AddY(0.5f) - shooter.Position.AddY(0.5f);
                if (toVictim.LengthSquared > 0.001f)
                {
                    shooter.ModSetAim(toVictim.Normalized());
                }
                // Charged, not tapped. Every affliction in the game lives on
                // the charged entry of its weapon's affliction pair, so a
                // probe that taps the trigger lands hit after hit that can
                // never freeze, burn or disrupt anybody. Hold until the weapon
                // says it is charged, then let go for a frame -- how long that
                // takes is the weapon's business, not a number guessed here.
                NetTestScript.HoldFire(shooter, !shooter.ModChargeReady);
                NetTestScript.Rest(victim, wantBiped: true);
            }
            foreach (EntityBase entity in Scene.Entities)
            {
                if (entity.Type == EntityType.BeamProjectile
                    && entity is BeamProjectileEntity shot && shot.Owner == shooter)
                {
                    _afflictFired[_afflictIndex] = true;
                    _afflictShotAfflictions |= shot.Afflictions;
                    break;
                }
            }
            if (victim.Health < _afflictVictimHealth)
            {
                _afflictHit[_afflictIndex] = true;
            }
            _afflictVictimHealth = Math.Min(_afflictVictimHealth, victim.Health);
            _afflictMaxCharge = Math.Max(_afflictMaxCharge, shooter.ModChargeLevel);
            bool landed = _afflictIndex switch
            {
                0 => victim.ModFrozen,
                1 => victim.ModBurning,
                _ => victim.ModDisrupted
            };
            if (landed || _afflictFrames >= _afflictFrameLimit)
            {
                if (Diagnostic)
                {
                    Console.WriteLine($"PROBE {_afflictions[_afflictIndex].Name}: "
                        + $"shooter slot {_afflictShooter} {shooter.Hunter} "
                        + $"{shooter.ModWeaponState}, maxcharge={_afflictMaxCharge}, shots carried "
                        + $"{_afflictShotAfflictions}, landed={landed}");
                }
                _afflictLanded[_afflictIndex] = landed;
                _afflictIndex++;
                _afflictShooter = -1;
                _afflictVictim = -1;
                _afflictShotAfflictions = Affliction.None;
                _afflictMaxCharge = 0;
            }
            return true;
        }

        private static bool Alive(PlayerEntity player)
        {
            return player.LoadFlags.TestFlag(LoadFlags.Active)
                && player.LoadFlags.TestFlag(LoadFlags.Spawned) && player.Health > 0;
        }

        /// <summary>Find a shooter of this hunter and somebody to shoot, and place them.</summary>
        private bool SetUpAffliction(Hunter hunter)
        {
            int shooter = -1;
            int victim = -1;
            for (int slot = 0; slot < _players && slot < PlayerEntity.Players.Count; slot++)
            {
                PlayerEntity player = PlayerEntity.Players[slot];
                if (!Alive(player))
                {
                    continue;
                }
                if (shooter < 0 && player.Hunter == hunter)
                {
                    shooter = slot;
                }
                else if (victim < 0)
                {
                    victim = slot;
                }
            }
            if (shooter < 0 || victim < 0)
            {
                return false;
            }
            PlayerEntity shooterPlayer = PlayerEntity.Players[shooter];
            PlayerEntity victimPlayer = PlayerEntity.Players[victim];
            // In front of the victim rather than at an arbitrary bearing: the
            // floor it is standing on is the floor the shooter needs too.
            Vector3 facing = victimPlayer.FacingVector;
            facing = new Vector3(facing.X, 0, facing.Z);
            if (facing.LengthSquared < 0.001f)
            {
                facing = Vector3.UnitZ;
            }
            facing = facing.Normalized();
            // Close: the probe is asking whether the state can be inflicted at
            // all, not whether the hunter can aim.
            Vector3 spot = victimPlayer.Position + facing * 2.2f;
            shooterPlayer.Teleport(spot, -facing, Scene.GetNodeRefByPosition(spot));
            shooterPlayer.ModArmAffinityWeapon();
            _afflictShooter = shooter;
            _afflictVictim = victim;
            return true;
        }

        /// <summary>
        /// Where to stand to be inside a volume.
        ///
        /// The entity's own position is not it: a trigger box is positioned
        /// relative to the entity and several jump pads carry theirs beside or
        /// above the model. Three of MP6 HEADSHOT's eight pads were reported
        /// as dead for exactly that reason -- the player was standing next to
        /// the box, not in it.
        /// </summary>
        private static Vector3 VolumeCenter(CollisionVolume volume)
        {
            return volume.Type switch
            {
                VolumeType.Box => volume.BoxPosition
                    + (volume.BoxVector1 * volume.BoxDot1
                        + volume.BoxVector2 * volume.BoxDot2
                        + volume.BoxVector3 * volume.BoxDot3) / 2,
                VolumeType.Cylinder => volume.CylinderPosition
                    + volume.CylinderVector * (volume.CylinderDot / 2),
                VolumeType.Sphere => volume.SpherePosition,
                _ => Vector3.Zero
            };
        }

        /// <summary>Where a player has to stand for this entity to notice it.</summary>
        private static Vector3 TriggerPoint(EntityBase entity)
        {
            if (entity is JumpPadEntity pad)
            {
                Vector3 center = VolumeCenter(pad.ModVolume);
                if (center != Vector3.Zero)
                {
                    return center;
                }
            }
            return entity.Position;
        }

        private void CollectProbeTargets()
        {
            foreach (EntityBase entity in Scene.Entities)
            {
                if (_probeTargets.Count >= _probeMaxTargets)
                {
                    break;
                }
                // Inactive ones are not meant to do anything, so a silent one
                // is not a finding.
                if (entity.Active
                    && (entity.Type == EntityType.JumpPad || entity.Type == EntityType.Teleporter))
                {
                    _probeTargets.Add(entity);
                }
            }
        }

        private void Observe()
        {
            _spawned = 0;
            for (int slot = 0; slot < _players && slot < PlayerEntity.Players.Count; slot++)
            {
                PlayerEntity player = PlayerEntity.Players[slot];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active)
                    || !player.LoadFlags.TestFlag(LoadFlags.Spawned))
                {
                    continue;
                }
                _spawned++;
                _everSpawned[slot] = true;
                if (player.IsAltForm)
                {
                    _everAltForm[slot] = true;
                }
                if (player.ModFrozen)
                {
                    _everFrozen[slot] = true;
                }
                if (player.ModBurning)
                {
                    _everBurned[slot] = true;
                }
                if (player.ModDisrupted)
                {
                    _everDisrupted[slot] = true;
                }
                if (_lastHealth[slot] > 0 && player.Health == 0)
                {
                    _deaths[slot]++;
                }
                _lastHealth[slot] = player.Health;
                _lowestY = Math.Min(_lowestY, player.Position.Y);
                if (_haveLastSeen[slot])
                {
                    float step = (player.Position - _lastSeen[slot]).Length;
                    // a respawn is a teleport, not a walk
                    if (step < 5)
                    {
                        _travelled[slot] += step;
                    }
                }
                _lastSeen[slot] = player.Position;
                _haveLastSeen[slot] = true;
            }
            foreach (EntityBase entity in Scene.Entities)
            {
                if (entity.Type == EntityType.BeamProjectile
                    && (entity as BeamProjectileEntity)?.Owner is PlayerEntity owner
                    && owner.SlotIndex >= 0 && owner.SlotIndex < _everFired.Length)
                {
                    _everFired[owner.SlotIndex] = true;
                }
            }
        }

        /// <summary>
        /// One frame of the spawn render sweep. False when every spawn point
        /// has been visited.
        ///
        /// The shape is the report's own recipe: stand on a spawn, let the
        /// player settle, read the frame; then walk straight ahead for five
        /// seconds, reading the frame as it goes, and keep the worst. A room
        /// that draws from the spawn and stops drawing once you have walked
        /// into it is the failure being looked for, and only the second
        /// reading finds it.
        /// </summary>
        private bool StepSpawnRender()
        {
            if (_spawnIndex < 0)
            {
                CollectSpawnSpots();
                _spawnIndex = 0;
                _spawnFrames = -1;
            }
            if (_spawnIndex >= _spawnSpots.Count)
            {
                return false;
            }
            PlayerEntity player = PlayerEntity.Main;
            if (_spawnFrames < 0)
            {
                // Between lives: ask to come back rather than measure a frame
                // drawn from a dead player's camera.
                if (!player.LoadFlags.TestFlag(LoadFlags.Spawned) || player.Health == 0)
                {
                    NetTestScript.Rest(player, wantBiped: true);
                    return true;
                }
                Vector3 spot = _spawnSpots[_spawnIndex];
                player.ModForceForm(altForm: false);
                // The spawn point's own node ref, which is what
                // PlayerProcess's respawn passes -- not a positional lookup.
                // GetNodeRefByPosition tests a position against the *portal
                // planes* of each room part in turn and takes the first whose
                // half-spaces it is inside, which for a part with two portals
                // is an unbounded wedge: it answers "part 5" for points
                // nowhere near part 5, and a sweep built on it measures the
                // approximation rather than the map.
                player.Teleport(spot, _spawnFacings[_spawnIndex], _spawnNodeRefs[_spawnIndex]);
                _spawnFrames = 0;
                _spawnLitWorst = Double.MaxValue;
                NetTestScript.Rest(player, wantBiped: true);
                return true;
            }
            _spawnFrames++;
            if (_spawnFrames < _spawnSettleFrames)
            {
                NetTestScript.Rest(player, wantBiped: true);
                return true;
            }
            if (_spawnFrames == _spawnSettleFrames)
            {
                _spawnLitAtSpawn = Mods.ScreenCapture.NonBlackFraction(Scene);
                _spawnLitWorst = _spawnLitAtSpawn;
                SaveSpawnShot("spawn");
            }
            NetTestScript.WalkForward(player);
            if (_spawnFrames % 30 == 0)
            {
                double lit = Mods.ScreenCapture.NonBlackFraction(Scene);
                if (lit < _spawnLitWorst)
                {
                    _spawnLitWorst = lit;
                }
            }
            if (_spawnFrames < _spawnSettleFrames + _spawnWalkFrames)
            {
                return true;
            }
            SaveSpawnShot("walked");
            Vector3 at = _spawnSpots[_spawnIndex];
            bool failed = _spawnLitWorst < _renderFloor;
            if (failed)
            {
                _spawnFailures++;
            }
            PlayerEntity main = PlayerEntity.Main;
            Console.WriteLine($"RENDERSPAWN {_room} | spawn {_spawnIndex} "
                + $"at {at.X:0.0},{at.Y:0.0},{at.Z:0.0} "
                + $"| at spawn {_spawnLitAtSpawn * 100:0.0}% "
                + $"| worst while walking {_spawnLitWorst * 100:0.0}%"
                + $" | part {main.NodeRef.PartIndex}"
                + (failed ? " | FAIL" : ""));
            _spawnIndex++;
            _spawnFrames = -1;
            return true;
        }

        private void SaveSpawnShot(string what)
        {
            if (_shotDirectory == null)
            {
                return;
            }
            string name = _room.Replace(' ', '_').Replace('-', '_');
            Mods.ScreenCapture.Save(Scene,
                System.IO.Path.Combine(_shotDirectory, $"{name}-spawn{_spawnIndex:00}-{what}.png"));
        }

        private void CollectSpawnSpots()
        {
            foreach (EntityBase entity in Scene.Entities)
            {
                if (entity.Type != EntityType.PlayerSpawn)
                {
                    continue;
                }
                // Just above the marker: a spawn point sits on the floor and
                // teleporting exactly onto it can put the player's own volume
                // inside it.
                _spawnSpots.Add(entity.Position.AddY(0.5f));
                Vector3 facing = entity.FacingVector;
                _spawnFacings.Add(facing.LengthSquared < 0.0001f ? Vector3.UnitZ : facing);
                _spawnNodeRefs.Add(entity.NodeRef);
            }
        }

        /// <summary>
        /// Read the frame back and record how much of it is lit.
        ///
        /// Before <c>SwapBuffers</c> deliberately: this reads the scene's own
        /// offscreen target, which is a texture the scene owns and is valid
        /// either way, but the window is <c>StartVisible = false</c> and a
        /// hidden window's back buffer is not -- see <c>ScreenCapture</c>.
        ///
        /// The first sample is kept separately from the rest. "Black from the
        /// spawn" and "black once you walk into it" are different faults with
        /// different causes, and a single average hides both.
        /// </summary>
        private void SampleRender()
        {
            if (_frame % _litSampleFrames != 1)
            {
                return;
            }
            double lit = Mods.ScreenCapture.NonBlackFraction(Scene);
            _litSamples++;
            _litTotal += lit;
            if (_litFirst < 0)
            {
                _litFirst = lit;
            }
            if (lit < _litMin)
            {
                _litMin = lit;
            }
            if (lit > _litMax)
            {
                _litMax = lit;
            }
            if (_shotDirectory != null)
            {
                string name = _room.Replace(' ', '_').Replace('-', '_');
                string path = System.IO.Path.Combine(_shotDirectory,
                    $"{name}-{_shotsSaved:00}.png");
                bool saved = ShowWindow
                    ? Mods.ScreenCapture.SaveWindow(Scene, path)
                    : Mods.ScreenCapture.Save(Scene, path);
                if (saved)
                {
                    _shotsSaved++;
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Scene.DoCleanup();
            base.OnClosing(e);
        }

        private int Report()
        {
            int spawnPoints = 0, jumpPads = 0, teleporters = 0, doors = 0, forceFields = 0;
            int itemSpawns = 0, items = 0, platforms = 0, morphCameras = 0, flagBases = 0;
            int nodeDefenses = 0, artifacts = 0, triggers = 0, areaVolumes = 0;
            foreach (EntityBase entity in Scene.Entities)
            {
                switch (entity.Type)
                {
                    case EntityType.PlayerSpawn: spawnPoints++; break;
                    case EntityType.JumpPad: jumpPads++; break;
                    case EntityType.Teleporter: teleporters++; break;
                    case EntityType.Door: doors++; break;
                    case EntityType.ForceField: forceFields++; break;
                    case EntityType.ItemSpawn: itemSpawns++; break;
                    case EntityType.ItemInstance: items++; break;
                    case EntityType.Platform: platforms++; break;
                    case EntityType.MorphCamera: morphCameras++; break;
                    case EntityType.FlagBase: flagBases++; break;
                    case EntityType.NodeDefense: nodeDefenses++; break;
                    case EntityType.Artifact: artifacts++; break;
                    case EntityType.TriggerVolume: triggers++; break;
                    case EntityType.AreaVolume: areaVolumes++; break;
                }
            }
            int spawnedEver = 0, altEver = 0, firedEver = 0, totalDeaths = 0;
            int frozenEver = 0, burnedEver = 0, disruptedEver = 0;
            for (int i = 0; i < _players; i++)
            {
                if (_everSpawned[i]) spawnedEver++;
                if (_everAltForm[i]) altEver++;
                if (_everFired[i]) firedEver++;
                if (_everFrozen[i]) frozenEver++;
                if (_everBurned[i]) burnedEver++;
                if (_everDisrupted[i]) disruptedEver++;
                totalDeaths += _deaths[i];
            }
            var line = new StringBuilder();
            line.Append($"MAPTEST {_room} | players {_players} | frames {_frame}");
            line.Append($" | spawned {spawnedEver}/{_players}");
            int moved = 0;
            float furthest = 0;
            for (int i = 0; i < _players && i < _travelled.Length; i++)
            {
                if (_travelled[i] > 20)
                {
                    moved++;
                }
                furthest = Math.Max(furthest, _travelled[i]);
            }
            line.Append($" | moved {moved}/{_players} (furthest {furthest:0} units)");
            line.Append($" | alt form {altEver}/{_players}");
            line.Append($" | fired {firedEver}/{_players}");
            line.Append($" | deaths {totalDeaths}");
            line.Append($" | afflicted freeze {frozenEver} burn {burnedEver}"
                + $" disrupt {disruptedEver}");
            var probe = new StringBuilder();
            for (int i = 0; i < _afflictions.Length; i++)
            {
                probe.Append(i == 0 ? " (probe " : " ");
                probe.Append(_afflictions[i].Name);
                probe.Append(' ');
                probe.Append(!_afflictTried[i] ? "n/a"
                    : _afflictLanded[i] ? "ok"
                    : !_afflictFired[i] ? "nofire"
                    : _afflictHit[i] ? "FAIL" : "nohit");
            }
            probe.Append(')');
            line.Append(probe);
            line.Append($" | spawnpoints {spawnPoints}");
            // Counted and then tried: "8 jump pads" says what the map holds,
            // "7/8 launched" says what it does.
            line.Append($" | jumppads {jumpPads} ({_padsFired}/{_padsProbed} launched)");
            line.Append($" teleporters {teleporters} ({_telesFired}/{_telesProbed} moved)");
            line.Append($" doors {doors}");
            line.Append($" forcefields {forceFields} platforms {platforms}");
            line.Append($" itemspawns {itemSpawns} items {items}");
            line.Append($" morphcams {morphCameras} flagbases {flagBases} nodes {nodeDefenses}");
            line.Append($" artifacts {artifacts} triggers {triggers} areas {areaVolumes}");
            line.Append($" | lowest Y {_lowestY:0.0}");
            if (_litSamples > 0)
            {
                line.Append($" | lit first {_litFirst * 100:0.0}%"
                    + $" min {_litMin * 100:0.0}% max {_litMax * 100:0.0}%"
                    + $" mean {_litTotal / _litSamples * 100:0.0}%"
                    + $" ({_litSamples} samples)");
            }
            Console.WriteLine(line.ToString());

            if (DrawRate > 1)
            {
                Console.WriteLine($"FRAMETIMING {_room} | {DrawRate} draws per step"
                    + $" | {_frame} steps, {Scene.FrameCount} counted"
                    + $" | entity draws {Scene.ModTotalEntityDraws}"
                    + $" ({Scene.ModBlendedDraws} blended)"
                    + $" | draws advancing the game: {_drawAdvancedTheGame}");
            }

            if (_renderProbe)
            {
                Console.WriteLine($"RENDERSWEEP {_room} | {_spawnSpots.Count} spawn point(s) "
                    + $"| {_spawnFailures} drew nothing");
                if (_spawnFailures > 0)
                {
                    Console.WriteLine($"MAPFAIL {_room} | {_spawnFailures} of {_spawnSpots.Count} "
                        + "spawn point(s) end in a frame with no room in it");
                }
                return _spawnFailures;
            }

            var problems = new List<string>();
            // One silent pad can be a pad somebody has to jump onto from
            // above; every pad in the room silent is the trigger path broken.
            if (_padsProbed > 0 && _padsFired == 0)
            {
                problems.Add($"none of the {_padsProbed} jump pad(s) launched a player "
                    + "standing on them");
            }
            if (_telesProbed > 0 && _telesFired == 0)
            {
                problems.Add($"none of the {_telesProbed} teleporter(s) moved a player "
                    + "standing on them");
            }
            if (spawnedEver < _players)
            {
                var missing = new StringBuilder();
                for (int i = 0; i < _players; i++)
                {
                    if (!_everSpawned[i])
                    {
                        missing.Append(missing.Length > 0 ? ", " : "");
                        missing.Append($"slot {i} ({PlayerEntity.Players[i].Hunter}, "
                            + $"hp {PlayerEntity.Players[i].Health}, "
                            + $"respawn {PlayerEntity.Players[i].RespawnTimer})");
                    }
                }
                problems.Add($"only {spawnedEver} of {_players} players ever reached the map "
                    + $"-- missing {missing}");
            }
            if (spawnPoints == 0)
            {
                problems.Add("no spawn points at all");
            }
            // The room drew nothing but the player's own gun and HUD.
            //
            // The threshold is deliberately low. A dark room is normal in this
            // game and several are mostly black sky; what is being caught is
            // the frame the report describes -- near-black with the viewmodel
            // in it and a handful of stray polygons floating in the middle.
            // Every room that renders at all is far above this, so a number
            // near it is the fault and not a dark map.
            if (_litSamples > 0 && _litMax < _renderFloor)
            {
                problems.Add($"the room never drew: at most {_litMax * 100:0.0}% of the frame "
                    + $"was lit across {_litSamples} samples (first {_litFirst * 100:0.0}%)");
            }
            else if (_litSamples > 1 && _litMin < _renderFloor)
            {
                problems.Add($"the room stopped drawing: {_litMin * 100:0.0}% of the frame lit "
                    + $"at its worst against {_litMax * 100:0.0}% at its best");
            }
            for (int i = 0; i < _players; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (_everSpawned[i] && !player.ModCanBeHurt())
                {
                    problems.Add($"slot {i} ({player.Hunter}) cannot be hurt by any beam");
                }
            }
            if (_drawAdvancedTheGame > 0)
            {
                problems.Add($"drawing advanced the simulation on {_drawAdvancedTheGame} "
                    + "frame(s): a draw pass is writing back to the world");
            }
            if (DrawRate > 1 && Mods.Render.FrameTiming.Interpolate && Scene.ModBlendedDraws == 0
                && Scene.ModTotalEntityDraws > 0)
            {
                problems.Add($"interpolation never engaged across {Scene.ModTotalEntityDraws} "
                    + "entity draws, so the extra frames are duplicates");
            }
            foreach (string problem in problems)
            {
                Console.WriteLine($"MAPFAIL {_room} | {problem}");
            }
            return problems.Count;
        }

        public static int Run(string room, int players, double seconds, GameMode mode,
            bool bots = false, string? shotDirectory = null, bool renderProbe = false,
            bool allNodes = false)
        {
            MapAudit? window = null;
            try
            {
                window = new MapAudit(room, Math.Clamp(players, 1, PlayerEntity.SlotCapacity),
                    seconds, mode, bots, renderProbe);
                // Draw every node the model has, ignoring the portal-graph
                // room-part culling. Answers one question and only one: is a
                // frame with no room in it a culling decision or geometry that
                // is not being drawn at all? It has to be set for a whole
                // frame, update included -- the draw lists are built during
                // the update and this is read while they are.
                window.Scene.ShowAllNodes = allNodes;
                if (shotDirectory != null)
                {
                    System.IO.Directory.CreateDirectory(shotDirectory);
                    window._shotDirectory = shotDirectory;
                }
                window.Run();
                return window.Report();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MAPCRASH {room} | {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
            finally
            {
                window?.Dispose();
            }
        }
    }
}
