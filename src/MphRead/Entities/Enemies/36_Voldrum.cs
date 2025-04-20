using System;
using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy36Entity : Enemy35Entity
    {
        private Enemy36Values _values;
        private EquipInfo _equipInfo1 = null!;
        private EquipInfo _equipInfo2 = null!;
        private int _ammo1 = 1000;
        private int _ammo2 = 1000;
        private ushort _delayTimer = 0;
        private ushort _shotCount = 0;
        private ushort _shotTimer = 0;

        public Enemy36Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            _stateProcesses = new Action[6]
            {
                State0, State1, State2, State3, State4, State5
            };
        }

        private static readonly int[] _recolors = new int[11]
        {
            0, 1, 0, 4, 0, 3, 2, 0, 0, 0, 0
        };

        // todo: share code (pass header values common with S00)
        protected override void Setup()
        {
            int version = (int)_spawner.Data.Fields.S06.EnemyVersion;
            Recolor = _recolors[version];
            _values = Metadata.Enemy36Values[(int)_spawner.Data.Fields.S06.EnemySubtype];
            Vector3 facing = _spawner.Data.Header.FacingVector.ToFloatVector().Normalized();
            Vector3 up = FixParallelVectors(facing, Vector3.UnitY);
            SetTransform(facing, up, _spawner.Data.Header.Position.ToFloatVector());
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 0.5f;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S06.Volume0);
            _homeVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume1, Position);
            Debug.Assert(_homeVolume.Type == VolumeType.Cylinder);
            _health = _healthMax = _values.HealthMax;
            Metadata.LoadEffectiveness(_values.Effectiveness, BeamEffectiveness);
            _scanId = _values.ScanId;
            WeaponInfo weapon = Weapons.EnemyWeapons[version];
            weapon.UnchargedDamage = _values.BeamDamage;
            weapon.SplashDamage = _values.SplashDamage;
            _equipInfo1 = new EquipInfo(weapon, _beams);
            _equipInfo2 = new EquipInfo(weapon, _beams);
            _equipInfo1.GetAmmo = () => _ammo1;
            _equipInfo1.SetAmmo = (newAmmo) => _ammo1 = newAmmo;
            _equipInfo1.GetAmmo = () => _ammo2;
            _equipInfo1.SetAmmo = (newAmmo) => _ammo2 = newAmmo;
            // todo: FPS stuff
            float minFactor = Fixed.ToFloat(_values.MinSpeedFactor);
            float maxFactor = Fixed.ToFloat(_values.MaxSpeedFactor);
            _minSpeedFactor = minFactor / 2;
            _maxSpeedFactor = maxFactor / 2;
            _speedFactor = minFactor / 2;
            _speedInc = (maxFactor - minFactor) * (1f / _values.SpeedSteps);
            _speedInc /= 2;
            _speedIncAmount = _speedInc;
            _delayTimer = (ushort)(_values.DelayTime * 2);
            _shotTimer = (ushort)(_values.ShotTime * 2);
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _aimStepCount = (ushort)(_values.AimSteps * 2);
            if (_spawner.Data.SpawnerHealth == 0 || MathF.Abs(Position.X - _homeVolume.CylinderPosition.X) < 1 / 4096f
                && MathF.Abs(Position.Z - _homeVolume.CylinderPosition.Z) < 1 / 4096f)
            {
                PickRoamTarget();
            }
            else
            {
                UpdateMoveTarget(_homeVolume.CylinderPosition.WithY(Position.Y));
            }
            _state1 = _state2 = 1;
            _subId = _state1;
            ModelInstance inst = SetUpModel(Metadata.EnemyModelNames[36], animIndex: 5);
        }

        protected override void EnemyProcess()
        {
            bool sfxGrounded = true;
            if (!_grounded)
            {
                _speed.Y -= Fixed.ToFloat(110) / 4; // todo: FPS stuff
            }
            if (_state1 == 2 || _state1 == 3)
            {
                bool discard = false;
                if (!HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true, ref _grounded, ref discard))
                {
                    sfxGrounded = false;
                }
            }
            else if (_state1 != 1 && _state1 != 4) // 0 or 5
            {
                if (!HandleCollision())
                {
                    sfxGrounded = false;
                }
            }
            if (_state1 != 0 && _state1 != 5)
            {
                ContactDamagePlayer(_values.ContactDamage, knockback: true);
            }
            CallStateProcess();
            if (_state1 != 0)
            {
                sfxGrounded = false;
            }
            float amount = 0xFFFF * _speedFactor * 2 / (_maxSpeedFactor * 2); // todo: FPS suff
            UpdateRollSfx(amount, sfxGrounded);
        }

        protected override bool HandleCollision()
        {
            return HandleCollision(4, 5);
        }

        private void State0()
        {
            UpdateSpeed();
            CallSubroutine(Metadata.Enemy36Subroutines, this);
        }

        private void State1()
        {
            CallSubroutine(Metadata.Enemy36Subroutines, this);
        }

        private void UpdateFacing()
        {
            _speed = Vector3.Zero;
            Vector3 facing = (PlayerEntity.Main.Position - Position).Normalized();
            if (facing.Y > 0.5f)
            {
                facing = facing.WithY(0.5f).Normalized();
            }
            else if (facing.Y < -0.5f)
            {
                facing = facing.WithY(-0.5f).Normalized();
            }
            SetTransform(facing, UpVector, Position);
        }

        private void State2()
        {
            UpdateFacing();
            CallSubroutine(Metadata.Enemy36Subroutines, this);
        }

        private void State3()
        {
            UpdateFacing();
            if (_shotCount > 0 && _shotTimer > 0)
            {
                _shotTimer--;
            }
            else
            {
                Vector3 facing = FacingVector;
                Vector3 spawnPos1 = Position.AddX(-0.43f);
                Vector3 spawnPos2 = Position.AddX(0.43f);
                _equipInfo1.Weapon.UnchargedDamage = _values.BeamDamage;
                _equipInfo1.Weapon.SplashDamage = _values.SplashDamage;
                _equipInfo1.Weapon.HeadshotDamage = _values.BeamDamage;
                BeamProjectileEntity.Spawn(this, _equipInfo1, spawnPos1, facing, BeamSpawnFlags.None, NodeRef, _scene);
                BeamProjectileEntity.Spawn(this, _equipInfo2, spawnPos2, facing, BeamSpawnFlags.None, NodeRef, _scene);
                _shotCount--;
                _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
                _models[0].SetAnimation(0);
                _soundSource.PlaySfx(SfxId.GUARD_BOT_ATTACK2);
            }
            CallSubroutine(Metadata.Enemy36Subroutines, this);
        }

        private void State4()
        {
            CallSubroutine(Metadata.Enemy36Subroutines, this);
        }

        private void State5()
        {
            State0();
        }

        private bool Behavior00()
        {
            bool collided = HandleCollision();
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _aimSteps, _aimAngleStep) || !collided)
            {
                return false;
            }
            _speedInc = _speedIncAmount;
            _speedFactor = _minSpeedFactor;
            _speed = (FacingVector * _speedFactor).WithY(0);
            _airborne = false;
            _timeInAir = 0;
            return true;
        }

        private bool Behavior01()
        {
            if (_delayTimer > 0)
            {
                _delayTimer--;
                return false;
            }
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            return true;
        }

        private bool Behavior02()
        {
            if (_shotCount > 0)
            {
                return false;
            }
            PickRoamTarget();
            _delayTimer = (ushort)(_values.DelayTime * 2);
            _shotTimer = (ushort)(_values.ShotTime * 2);
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _models[0].SetAnimation(5);
            return true;
        }

        private bool Behavior03()
        {
            Vector3 between = Position - _moveStart;
            if (between.LengthSquared <= _moveDistSqr && (!_airborne || _timeInAir <= 5 * 2)) // todo: FPS stuff
            {
                return false;
            }
            PickRoamTarget();
            if (_state1 == 5)
            {
                _targetVec = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
                float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
                _aimSteps = _aimStepCount;
                _aimAngleStep = angle / _aimSteps;
            }
            _speed = new Vector3(0, Fixed.ToFloat(_values.JumpSpeed) / 2, 0); // todo: FPS stuff
            _timeInAir = 0;
            _airborne = false;
            return true;
        }

        private bool Behavior04()
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return false;
            }
            Vector3 between = PlayerEntity.Main.Volume.SpherePosition - Position;
            float mag = between.Length * 5;
            PlayerEntity.Main.Speed = new Vector3(
                PlayerEntity.Main.Speed.X + between.X / mag,
                PlayerEntity.Main.Speed.Y,
                PlayerEntity.Main.Speed.Z + between.Z / mag
            );
            PlayerEntity.Main.TakeDamage(_values.ContactDamage, DamageFlags.NoDmgInvuln, null, this);
            PickRoamTarget();
            if (_state1 == 5)
            {
                _targetVec = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
                float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
                _aimSteps = _aimStepCount;
                _aimAngleStep = angle / _aimSteps;
            }
            _speed = new Vector3(0, Fixed.ToFloat(_values.JumpSpeed) / 2, 0); // todo: FPS stuff
            _timeInAir = 0;
            _airborne = false;
            return true;
        }

        private bool Behavior05()
        {
            if (PlayerEntity.Main.Health == 0)
            {
                return false;
            }
            Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
            // as with Psycho Bit, the dot product check could facilitate a vision angle range, but as written it's pointless
            if (Vector3.Dot(FacingVector, between) <= Fixed.ToFloat(_values.RangeMaxCosine))
            {
                return false;
            }
            _speed = Vector3.Zero;
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy36Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy36Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy36Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy36Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy36Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy36Entity enemy)
        {
            return enemy.Behavior05();
        }

        #endregion
    }

    public struct Enemy36Values
    {
        public ushort HealthMax { get; set; }
        public ushort BeamDamage { get; set; }
        public ushort SplashDamage { get; set; }
        public ushort ContactDamage { get; set; }
        public int MinSpeedFactor { get; set; }
        public int MaxSpeedFactor { get; set; }
        public ushort DoubleSpeedSteps { get; set; }
        public ushort AimSteps { get; set; }
        public ushort DelayTime { get; set; }
        public ushort ShotTime { get; set; }
        public ushort MinShots { get; set; }
        public ushort MaxShots { get; set; }
        public int JumpSpeed { get; set; }
        public int RangeMaxCosine { get; set; } // RangeMaxCosine?
        public ushort SpeedSteps { get; set; } // set at runtime to DoubleSpeedSteps / 2
        public short ScanId { get; set; }
        public int Effectiveness { get; set; }
    }
}
