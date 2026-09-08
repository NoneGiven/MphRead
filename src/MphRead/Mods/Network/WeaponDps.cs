using System;
using MphRead.Entities;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// How much damage a weapon does per second, held on a target that does
    /// not move, fight back or die.
    ///
    /// Written because a weapon was changed on the strength of reading the
    /// code. The Shock Coil is the only beam that stays alive and re-tests
    /// collision every frame, and every beam hit carries NoDmgInvuln, so
    /// nothing limits its rate but the frame rate -- which this engine runs at
    /// twice the original. That reasoning was sound and it was still only
    /// reasoning: the scripted tour keeps Sylux in alt form laying bombs and
    /// barely fires the beam at anybody, so no check in the project could
    /// confirm or deny it.
    ///
    /// The victim is left to die and the window closes on the kill. It does
    /// not restore the victim's health -- an older comment here said it did --
    /// and it must not keep counting afterwards: the respawn puts the victim
    /// somewhere else on the map, usually outside a short weapon's range, so
    /// every second after the kill is a second of firing at nobody divided
    /// into the damage that did land.
    /// </summary>
    public sealed class WeaponDps : GameWindow
    {
        private readonly string _room;
        private readonly Hunter _hunter;
        private readonly BeamType _beam;
        private readonly double _seconds;
        private readonly float _distance;
        private readonly bool _bombs;
        private int _frame;
        private int _firingFrames;
        private int _damage;
        private int _hits;
        private int _lastHealth = -1;
        private int _startHealth;
        private int _killFrames = -1;
        private int _beamFrames;
        private int _placedFrame = -1;
        private bool _placed;

        /// <summary>
        /// The highest Shock Coil contact timer reached.
        ///
        /// The weapon's damage is almost entirely the ramp this drives:
        /// the base is divided by 32 and dithers between 0 and 1, so a run
        /// that never gets this above 60 is measuring a Shock Coil that has
        /// not started working yet, however long the beam was alive.
        /// </summary>
        private int _worstShockCoilTimer;
        private int _lastAmmo = -1;
        private int _lastHitFrame = -1;

        /// <summary>
        /// Topped up to the player's own maximum, not to a large number. The
        /// engine clamps health to HealthMax, so writing 999 reads back as 99
        /// on the next frame -- which the first version of this probe counted
        /// as nine hundred points of damage in one hit.
        /// </summary>
        private static int FullHealth(PlayerEntity player) => Math.Max(1, player.HealthMax);

        public Scene Scene { get; }

        private static GameWindowSettings GameSettings() => new() { UpdateFrequency = 60 };

        private static NativeWindowSettings WindowSettings() => new()
        {
            ClientSize = new Vector2i(320, 180),
            Title = "MphRead weapon probe",
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
            StartVisible = false
        };

        private WeaponDps(string room, Hunter hunter, BeamType beam, double seconds, float distance,
            bool bombs)
            : base(GameSettings(), WindowSettings())
        {
            _room = room;
            _hunter = hunter;
            _beam = beam;
            _seconds = seconds;
            _distance = distance;
            _bombs = bombs;
            PlayerEntity.MaxPlayers = Math.Max(PlayerEntity.MaxPlayers, 2);
            MapAudit.ForceEveryone = true;
            Scene = new Scene(Size, KeyboardState, MouseState, _ => { }, Close);
            // The victim is slot 0 and the shooter is slot 1, deliberately.
            // PlayerEntity.ProcessInput refills the *main* player's controls
            // from the keyboard every frame, so anything written into slot 0
            // is gone before the simulation reads it -- the first version of
            // this probe held fire for twelve seconds and spawned no beam at
            // all. The victim is meant to stand still, which is exactly what
            // an empty keyboard gives it.
            Scene.AddPlayer(Hunter.Samus, recolor: 0, team: -1);
            Scene.AddPlayer(hunter, recolor: 0, team: -1);
            for (int i = 2; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity.Players[i].LoadFlags &= ~LoadFlags.Active;
            }
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity.Players[i].IsBot = false;
            }
            PlayerEntity.PlayerCount = 2;
            PlayerEntity.MainPlayerIndex = 0;
            Scene.AddRoom(room, GameMode.Battle, playerCount: NetLaunch.RoomPlayerCount);
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
            Scene.OnUpdateFrame();
            if (!Scene.OnRenderFrame())
            {
                return;
            }
            _frame++;
            Step();
            SwapBuffers();
            Scene.AfterRenderFrame();
            base.OnRenderFrame(args);
            if (_placed && _frame - _placedFrame >= _seconds * 60)
            {
                Close();
            }
            else if (_frame > (_seconds + 20) * 60)
            {
                Close(); // never got set up; the report says so
            }
        }

        private static bool Alive(PlayerEntity player)
        {
            return player.LoadFlags.TestFlag(LoadFlags.Active)
                && player.LoadFlags.TestFlag(LoadFlags.Spawned) && player.Health > 0;
        }

        /// <summary>
        /// Take the victim's health reading for this frame, before anything
        /// decides to stop early.
        ///
        /// Both halves of this used to sit below the guard that returns when
        /// the victim is dead -- so the frame that killed them, which is the
        /// one carrying the last and largest drop, was the one frame never
        /// counted. A weapon that kills in stages lost its final hit; Lockjaw,
        /// whose snare does sixty damage from each of three bombs and takes a
        /// hunter from full to nothing in one go, had *every* hit it ever
        /// landed fall in that gap and reported a flat zero while visibly
        /// killing the target in under two seconds.
        /// </summary>
        private void Account(PlayerEntity victim)
        {
            if (!_placed)
            {
                return;
            }
            if (_lastHealth >= 0 && victim.Health < _lastHealth)
            {
                _damage += _lastHealth - victim.Health;
                _hits++;
                _lastHitFrame = _firingFrames;
            }
            if (_killFrames < 0 && _lastHealth > 0 && victim.Health == 0)
            {
                _killFrames = _firingFrames;
            }
            _lastHealth = victim.Health;
        }

        private void Step()
        {
            if (PlayerEntity.Players.Count < 2)
            {
                return;
            }
            PlayerEntity victim = PlayerEntity.Players[0];
            PlayerEntity shooter = PlayerEntity.Players[1];
            // The kill has to be recorded here, before the guard below sends
            // the probe home for the frame.
            //
            // Alive() is false exactly when the victim is dead, so the check
            // further down -- which sits after this return -- never once ran
            // on a frame where the victim was dead, and every run reported
            // "did not kill" however fast the weapon was. The window then kept
            // counting while the victim respawned somewhere else on the map,
            // out of range of a short beam, so the seconds after the kill were
            // divided into the damage before it: the Shock Coil killed in 2.8
            // seconds and was reported at a third of its real rate.
            Account(victim);
            if (!Alive(shooter) || !Alive(victim))
            {
                NetTestScript.Rest(shooter, wantBiped: true);
                NetTestScript.Rest(victim, wantBiped: true);
                return;
            }
            if (!_bombs && (shooter.IsAltForm || shooter.IsMorphing || shooter.IsUnmorphing))
            {
                NetTestScript.Rest(shooter, wantBiped: true);
                NetTestScript.Rest(victim, wantBiped: true);
                return;
            }
            if (!_placed)
            {
                // In front of the victim, on the floor the victim is standing
                // on -- an arbitrary bearing puts the shooter in a wall.
                Vector3 facing = victim.FacingVector;
                facing = new Vector3(facing.X, 0, facing.Z);
                facing = facing.LengthSquared < 0.001f ? Vector3.UnitZ : facing.Normalized();
                Vector3 spot = victim.Position + facing * (_bombs ? 0.6f : _distance);
                shooter.Teleport(spot, -facing, Scene.GetNodeRefByPosition(spot));
                _placed = true;
                _placedFrame = _frame;
                _lastHealth = victim.Health;
                _startHealth = victim.Health;
            }
            if (_killFrames >= 0)
            {
                // Already answered. Standing here shooting a corpse only
                // measures the respawn timer.
                NetTestScript.Rest(shooter, wantBiped: true);
                NetTestScript.Rest(victim, wantBiped: true);
                return;
            }
            if (_bombs)
            {
                // Walk the shooter round the victim while it lays.
                //
                // Sylux is why. A Morph Ball bomb hurts whoever is standing on
                // it, so laying three in one spot measures it fine; Lockjaw
                // does its damage with the snare stretched between three
                // bombs, and three bombs dropped on one spot make a triangle
                // with no area, which nobody can be inside. Standing still
                // reports Lockjaw as doing nothing whether or not it works,
                // which is the same trap the scripted tour falls into.
                // Sylux only. A Morph Ball or Stinglarva bomb hurts whoever
                // stands on it, so the ring would carry every one of them out
                // of its own radius and report a working weapon as dead --
                // which is exactly what it did the first time this ran.
                if (_hunter == Hunter.Sylux)
                {
                    float angle = MathHelper.DegreesToRadians(_firingFrames * 6f);
                    var offset = new Vector3(MathF.Cos(angle) * 2.4f, 0, MathF.Sin(angle) * 2.4f);
                    Vector3 ring = victim.Position + offset;
                    shooter.Teleport(ring, -offset.Normalized(), Scene.GetNodeRefByPosition(ring));
                }
                NetTestScript.LayBombs(shooter, _frame);
                NetTestScript.Rest(victim, wantBiped: true);
                foreach (EntityBase entity in Scene.Entities)
                {
                    if (entity.Type == EntityType.Bomb)
                    {
                        _beamFrames++;
                        break;
                    }
                }
                _lastAmmo = shooter.ModAmmo.Ua;
                shooter.Health = FullHealth(shooter);
                _firingFrames++;
                return;
            }
            if (shooter.ShockCoilTimer > _worstShockCoilTimer)
            {
                _worstShockCoilTimer = shooter.ShockCoilTimer;
            }
            shooter.ModArmWeapon(_beam);
            // Chest to chest. Between the two collision volumes' centres
            // sounds more precise and is worse: they sit low, so the shot
            // goes into the floor a couple of units short.
            Vector3 toVictim = victim.Position.AddY(0.5f) - shooter.Position.AddY(0.5f);
            if (toVictim.LengthSquared > 0.001f)
            {
                shooter.ModSetAim(toVictim.Normalized());
            }
            NetTestScript.HoldFire(shooter, down: true);
            NetTestScript.Rest(victim, wantBiped: true);
            // "It never fired" and "it fired and missed" are different
            // answers and only the second is about the weapon.
            foreach (EntityBase entity in Scene.Entities)
            {
                if (entity.Type == EntityType.BeamProjectile
                    && entity is BeamProjectileEntity shot && shot.Owner == shooter)
                {
                    _beamFrames++;
                    break;
                }
            }
            _lastAmmo = shooter.ModAmmo.Ua;
            // The shooter is kept alive -- splash from its own weapon, or a
            // fall, would end the window for a reason that is not the
            // measurement. The victim is left to die: time to kill from full
            // health is the one number here that cannot be misread.
            shooter.Health = FullHealth(shooter);
            _firingFrames++;
        }

        private int Report()
        {
            if (!_placed || _firingFrames == 0)
            {
                Console.WriteLine($"DPSFAIL {_room} | {_hunter} {_beam} | never got set up");
                return 1;
            }
            double seconds = _firingFrames / 60.0;
            double window = _killFrames > 0 ? _killFrames / 60.0 : seconds;
            string kill = _killFrames > 0
                ? $"killed {_startHealth} hp in {_killFrames / 60.0:0.00} s"
                : $"did not kill {_startHealth} hp in {seconds:0.0} s";
            Console.WriteLine($"DPS {_room} | {_hunter} {(_bombs ? "laying bombs" : $"holding {_beam}")} at {(_bombs ? 0.6f : _distance):0.0} units | {kill} | "
                + $"damage {_damage} | hits {_hits} | "
                + $"{_damage / window:0.0} per second | "
                + $"{(_hits > 0 ? _damage / (double)_hits : 0):0.0} per hit | "
                + $"{_hits / window:0.0} hits per second | "
                + $"beam alive on {_beamFrames} of {_firingFrames} frame(s)"
                + $" | shockCoilTimer {_worstShockCoilTimer} (ramp needs 60 for +1, 240 for +4)"
                + $" | victim ended on {_lastHealth} hp | shooter ammo {_lastAmmo}"
                + $" | last hit on firing frame {_lastHitFrame} of {_firingFrames}");
            return 0;
        }

        public static int Run(string room, Hunter hunter, BeamType beam, double seconds, float distance,
            bool bombs = false)
        {
            WeaponDps? window = null;
            try
            {
                window = new WeaponDps(room, hunter, beam, seconds, Math.Clamp(distance, 0.5f, 40f), bombs);
                window.Run();
                return window.Report();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DPSCRASH {room} | {ex.GetType().Name}: {ex.Message}");
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
