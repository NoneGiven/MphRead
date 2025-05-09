using System;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy23Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private Enemy23Values _values;
        private CollisionVolume _homeVolume;
        private CollisionVolume _nearVolume;
        private CollisionVolume _rangeVolume;

        private Vector3 _curFacing;
        private Vector3 _moveTarget;
        private Vector3 _moveStart;
        private float _roamAngleSign = 1;
        private ushort _delayTimer = 0;
        private ushort _shotCount = 0;
        private ushort _shotTimer = 0;
        private bool _damaged = false;
        private float _speedFactor = 0;
        private float _speedInc = 0;
        private bool _reachedTarget = false;
        private float _moveDistSqr = 0; // square of the length of the full vector to the move target
        private float _moveDistSqrHalf = 0; // half of _moveDistSqr, switch from gaining speed to losing speed when reached
        private bool _increaseSpeed = false; // true during the first part of the move to a roam target, then false
        private ushort _camSeqDelayTimer = 0;

        private ushort _reachTargetHackTimer = 0;

        private Vector3 _eyePos;
        private Vector3 _aimVec = Vector3.UnitX;
        private Vector3 _targetVec;
        private Vector3 _crossVec = Vector3.UnitX;
        private float _aimAngleStep = 0;
        private ushort _aimSteps = 0;

        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;
        private EffectEntry? _effect = null;

        public Enemy23Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[11]
            {
                State00, State01, State02, State03, State04, State05, State06, State07, State08, State09, State10
            };
        }

        private static readonly int[] _recolors = new int[11]
        {
            0, 1, 0, 4, 0, 3, 2, 0, 0, 0, 0
        };

        protected override void EnemyInitialize()
        {
            int version = (int)_spawner.Data.Fields.S06.EnemyVersion;
            Recolor = _recolors[version];
            Vector3 facing = _spawner.FacingVector;
            Vector3 up = FixParallelVectors(facing, Vector3.UnitY);
            SetTransform(facing, up, _spawner.Position);
            SetUpModel(Metadata.EnemyModelNames[23], animIndex: 3);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S06.Volume0);
            _values = Metadata.Enemy23Values[(int)_spawner.Data.Fields.S06.EnemySubtype];
            _health = _healthMax = _values.HealthMax;
            Metadata.LoadEffectiveness(_values.Effectiveness, BeamEffectiveness);
            _scanId = _values.ScanId;
            _curFacing = facing;
            _homeVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume1, Position);
            _nearVolume = new CollisionVolume(Vector3.Zero, 1); // gets moved in the process function
            _rangeVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume3, Position);
            WeaponInfo weapon = Weapons.EnemyWeapons[version];
            _equipInfo = new EquipInfo(weapon, _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
            _equipInfo.UnchargedDamage = _values.BeamDamage;
            _equipInfo.SplashDamage = _values.SplashDamage;
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
            _speedFactor = Fixed.ToFloat(_values.MinSpeedFactor1) / 2;
            _state1 = _state2 = 9;
            Debug.Assert(_homeVolume.Type != VolumeType.Sphere);
            bool atHomePoint = false;
            Vector3 homePoint = Vector3.Zero;
            if (_spawner.Data.SpawnerHealth == 0)
            {
                atHomePoint = true;
            }
            else if (_homeVolume.Type == VolumeType.Cylinder)
            {
                Debug.Assert(_homeVolume.CylinderVector == Vector3.UnitY);
                homePoint = new Vector3(
                    _homeVolume.CylinderPosition.X,
                    _homeVolume.CylinderPosition.Y + _homeVolume.CylinderDot / 2,
                    _homeVolume.CylinderPosition.Z
                );
                if (MathF.Abs(Position.X - homePoint.X) < 1 / 4096f
                    && MathF.Abs(Position.Y - homePoint.Y) < 1 / 4096f
                    && MathF.Abs(Position.Z - homePoint.Z) < 1 / 4096f)
                {
                    atHomePoint = true;
                }
            }
            else if (_homeVolume.Type == VolumeType.Box)
            {
                homePoint = new Vector3(
                    _homeVolume.BoxPosition.X + _homeVolume.BoxVector1.X * (_homeVolume.BoxDot1 / 2),
                    _homeVolume.BoxPosition.Y + _homeVolume.BoxVector2.Y * (_homeVolume.BoxDot2 / 2),
                    _homeVolume.BoxPosition.Z + _homeVolume.BoxVector3.Z * (_homeVolume.BoxDot3 / 2)
                );
                if (MathF.Abs(Position.X - homePoint.X) < 1 / 4096f
                    && MathF.Abs(Position.Y - homePoint.Y) < 1 / 4096f
                    && MathF.Abs(Position.Z - homePoint.Z) < 1 / 4096f)
                {
                    atHomePoint = true;
                }
            }
            if (atHomePoint)
            {
                PickRoamTarget();
            }
            else
            {
                UpdateMoveTarget(homePoint);
                _speedFactor = Fixed.ToFloat(_values.MinSpeedFactor2) / 2;
            }
            _increaseSpeed = true;
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _aimVec = (PlayerEntity.Main.Position - Position).Normalized();
            _subId = _state1;
        }

        protected override void EnemyProcess()
        {
            if (_effect != null)
            {
                Vector3 facing = FacingVector;
                _eyePos = Position - facing;
                _effect.Transform(facing, Vector3.UnitY, _eyePos);
            }
            ContactDamagePlayer(_values.ContactDamage, knockback: true);
            _nearVolume = CollisionVolume.Move(new CollisionVolume(Vector3.Zero, 1), Position);
            _soundSource.PlaySfx(SfxId.PSYCHOBIT_FLY, loop: true);
            CallStateProcess();
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health > 0)
            {
                if (_state1 == 3)
                {
                    _damaged = true;
                }
                else if (_state1 != 9 && _state1 != 10)
                {
                    _state2 = 3;
                    _subId = _state2;
                    if (_state1 == 2)
                    {
                        _soundSource.StopSfx(SfxId.PSYCHOBIT_CHARGE);
                    }
                    if (_effect == null)
                    {
                        SpawnEffect();
                    }
                    _speed = Vector3.Zero;
                    _speedFactor = 0;
                    _speedInc = 0;
                    _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
                    _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
                    _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
                    _aimVec = (PlayerEntity.Main.Position - Position).Normalized();
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    SetTransform(_aimVec, Vector3.UnitY, Position);
                    _curFacing = _aimVec;
                }
            }
            else if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            return false;
        }

        private void SpawnEffect()
        {
            Vector3 facing = FacingVector;
            _eyePos = Position - facing;
            _effect = _scene.SpawnEffectGetEntry(240, Vector3.UnitX, Vector3.UnitY, _eyePos); // psychoCharge
            _effect?.SetElementExtension(true);
        }

        private void UpdateMoveTarget(Vector3 targetPoint)
        {
            Vector3 facing = FacingVector;
            _moveTarget = targetPoint;
            _moveStart = Position;
            _targetVec = _moveTarget - Position;
            _moveDistSqr = _targetVec.LengthSquared;
            _moveDistSqrHalf = _moveDistSqr / 2;
            _increaseSpeed = true;
            _targetVec = _targetVec.Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(facing, _targetVec)));
            _aimSteps = (ushort)(_values.AimSteps * 2); // todo: FPS stuff
            _aimAngleStep = angle / _aimSteps;
            _crossVec = Vector3.Cross(facing, _targetVec).Normalized();
        }

        private void PickRoamTarget()
        {
            Vector3 moveTarget;
            if (_homeVolume.Type == VolumeType.Cylinder)
            {
                float dist = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_homeVolume.CylinderRadius)));
                var vec = new Vector3(dist, 0, 0);
                _roamAngleSign *= -1;
                float angle = Fixed.ToFloat(Rng.GetRandomInt2(0xB4000)) * _roamAngleSign; // [0-180)
                var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(angle));
                vec = Matrix.Vec3MultMtx3(vec, rotY);
                vec.Y = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_homeVolume.CylinderDot)));
                moveTarget = _homeVolume.CylinderPosition + vec;
            }
            else
            {
                Debug.Assert(_homeVolume.Type == VolumeType.Box);
                float distX = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_homeVolume.BoxDot1)));
                float distY = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_homeVolume.BoxDot2)));
                float distZ = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_homeVolume.BoxDot3)));
                var vec = new Vector3(
                    _homeVolume.BoxVector1.X * distX,
                    _homeVolume.BoxVector2.Y * distY,
                    _homeVolume.BoxVector3.Z * distZ
                );
                moveTarget = _homeVolume.BoxPosition + vec;
            }
            UpdateMoveTarget(moveTarget);
            _speed = Vector3.Zero;
        }

        private void UpdateSpeed(float min, float max)
        {
            if (_increaseSpeed)
            {
                Vector3 between = _moveTarget - Position;
                if (between.LengthSquared < _moveDistSqrHalf)
                {
                    _increaseSpeed = false;
                }
            }
            // todo: FPS stuff
            if (_increaseSpeed)
            {
                _speedFactor += _speedInc;
                if (_speedFactor > max / 2)
                {
                    _speedFactor = max / 2;
                }
            }
            else
            {
                _speedFactor -= _speedInc;
                if (_speedFactor < min / 2)
                {
                    _speedFactor = min / 2;
                }
            }
            _speed = FacingVector * _speedFactor;
        }

        private void State00()
        {
            UpdateSpeed(Fixed.ToFloat(_values.MinSpeedFactor1), Fixed.ToFloat(_values.MaxSpeedFactor1));
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State01()
        {
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void UpdateFacing()
        {
            Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
            if (Vector3.Dot(_curFacing, between) > Fixed.ToFloat(_values.RangeMaxCosine))
            {
                SetTransform(between, Vector3.UnitY, Position);
            }
        }

        private void State02()
        {
            UpdateFacing();
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State03()
        {
            UpdateFacing();
            if (_shotCount > 0 && _shotTimer > 0)
            {
                _shotTimer--;
            }
            else
            {
                Vector3 targetPos = PlayerEntity.Main.Position.AddY(0.5f);
                _aimVec = (targetPos - Position).Normalized();
                Vector3 spawnPos = Position + _aimVec / 2;
                _equipInfo.UnchargedDamage = _values.BeamDamage;
                _equipInfo.SplashDamage = _values.SplashDamage;
                _equipInfo.HeadshotDamage = _values.BeamDamage;
                BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, _aimVec, BeamSpawnFlags.None, NodeRef, _scene);
                _shotCount--;
                _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
                _soundSource.PlaySfx(SfxId.PSYCHOBIT_BEAM);
                _models[0].SetAnimation(0, AnimFlags.NoLoop);
            }
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State04()
        {
            UpdateSpeed(Fixed.ToFloat(_values.MinSpeedFactor1), Fixed.ToFloat(_values.MaxSpeedFactor1));
            if (CallSubroutine(Metadata.Enemy23Subroutines, this) && _state2 == 2) // can't be true
            {
                _speed = Vector3.Zero;
                _curFacing = FacingVector;
                _models[0].SetAnimation(1);
                _soundSource.PlaySfx(SfxId.PSYCHOBIT_CHARGE, cancellable: true);
            }
        }

        private void State05()
        {
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State06()
        {
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State07()
        {
            UpdateSpeed(Fixed.ToFloat(_values.MinSpeedFactor2), Fixed.ToFloat(_values.MaxSpeedFactor2));
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State08()
        {
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State09()
        {
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State10()
        {
            _speed = FacingVector * _speedFactor;
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        public bool Behavior00()
        {
            Vector3 facing = FacingVector;
            bool result = SeekTargetVector(_targetVec, ref facing, _crossVec, ref _aimSteps, _aimAngleStep);
            SetTransform(facing, Vector3.UnitY, Position);
            if (!result)
            {
                return false;
            }
            // todo: FPS stuff
            float minFactor = Fixed.ToFloat(_values.MinSpeedFactor2);
            float maxFactor = Fixed.ToFloat(_values.MaxSpeedFactor2);
            _speedFactor = minFactor / 2;
            _speedInc = (maxFactor - minFactor) * (1f / _values.SpeedSteps);
            _speedInc /= 2;
            _speed = facing * _speedFactor;
            _curFacing = facing;
            _models[0].SetAnimation(2);
            return true;
        }

        private void CheckReachedTarget()
        {
            Vector3 nextPos = Position + _speed;
            if ((_moveStart - nextPos).LengthSquared > _moveDistSqr)
            {
                _speed = nextPos - Position;
                _reachedTarget = true;
            }
        }

        public bool Behavior01()
        {
            if (_reachedTarget && _reachTargetHackTimer == 0)
            {
                PickRoamTarget();
                _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
                _reachedTarget = false;
                return true;
            }
            if (_reachTargetHackTimer > 0)
            {
                _reachTargetHackTimer--;
            }
            CheckReachedTarget();
            return false;
        }

        public bool Behavior02()
        {
            return Behavior01();
        }

        public bool Behavior03()
        {
            Vector3 facing = FacingVector;
            bool result = SeekTargetVector(_targetVec, ref facing, _crossVec, ref _aimSteps, _aimAngleStep);
            SetTransform(facing, Vector3.UnitY, Position);
            if (!result)
            {
                return false;
            }
            _speedInc = 0;
            _speedFactor = 0;
            _curFacing = facing;
            _models[0].SetAnimation(1);
            _soundSource.PlaySfx(SfxId.PSYCHOBIT_CHARGE, cancellable: true);
            SpawnEffect();
            return true;
        }

        public bool Behavior04()
        {
            return Behavior00();
        }

        // todo: same as Behavior00 except using the first pair of factors and not setting animation
        public bool Behavior05()
        {
            Vector3 facing = FacingVector;
            bool result = SeekTargetVector(_targetVec, ref facing, _crossVec, ref _aimSteps, _aimAngleStep);
            SetTransform(facing, Vector3.UnitY, Position);
            if (!result)
            {
                return false;
            }
            // todo: FPS stuff
            float minFactor = Fixed.ToFloat(_values.MinSpeedFactor1);
            float maxFactor = Fixed.ToFloat(_values.MaxSpeedFactor1);
            _speedFactor = minFactor / 2;
            _speedInc = (maxFactor - minFactor) * (1f / _values.SpeedSteps);
            _speedInc /= 2;
            _speed = facing * _speedFactor;
            _curFacing = facing;
            return true;
        }

        public bool Behavior06()
        {
            if (_reachedTarget && _reachTargetHackTimer == 0)
            {
                // bug: _reachedTarget is not cleared here, resulting in only short, jerky movements after the player is in range
                // --> this additionally means the movements are longer in-game (1f of 30 fps speed) than for us
                // so we use the _reachTargetHackTimer to make this bugged movement last 2f instead of 1f
                _reachTargetHackTimer = 1; // todo: FPS stuff
                Vector3 facing = FacingVector;
                _targetVec = (PlayerEntity.Main.Position - Position).Normalized();
                float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(facing, _targetVec)));
                _aimSteps = (ushort)(_values.AimSteps * 2); // todo: FPS stuff
                _aimAngleStep = angle / _aimSteps;
                _crossVec = Vector3.Cross(facing, _targetVec).Normalized();
                _speed = Vector3.Zero;
                _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
                return true;
            }
            if (_reachTargetHackTimer > 0)
            {
                _reachTargetHackTimer--;
            }
            CheckReachedTarget();
            return false;
        }

        public bool Behavior07()
        {
            if (CameraSequence.Current?.BlockInput == true)
            {
                _camSeqDelayTimer = 40 * 2; // todo: FPS stuff
                return false;
            }
            if (_camSeqDelayTimer > 0)
            {
                _camSeqDelayTimer--;
                return false;
            }
            if (PlayerEntity.Main.Health > 0)
            {
                Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
                if (Vector3.Dot(_curFacing, between) > Fixed.ToFloat(_values.RangeMaxCosine)
                    && _rangeVolume.TestPoint(PlayerEntity.Main.Position))
                {
                    _speed = Vector3.Zero;
                    _models[0].SetAnimation(1);
                    _soundSource.PlaySfx(SfxId.PSYCHOBIT_CHARGE, cancellable: true);
                    SpawnEffect();
                    return true;
                }
            }
            return false;
        }

        public bool Behavior08()
        {
            if (_delayTimer > 0)
            {
                _delayTimer--;
                return false;
            }
            _soundSource.StopSfx(SfxId.PSYCHOBIT_CHARGE);
            _models[0].SetAnimation(0, AnimFlags.NoLoop);
            _aimVec = (PlayerEntity.Main.Position + Position).Normalized();
            return true;
        }

        private void MoveAway()
        {
            PickRoamTarget();
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
            _models[0].SetAnimation(3);
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            if (_state1 == 2)
            {
                _soundSource.StopSfx(SfxId.PSYCHOBIT_CHARGE);
            }
        }

        public bool Behavior09()
        {
            Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
            if (Vector3.Dot(_curFacing, between) >= Fixed.ToFloat(_values.RangeMaxCosine))
            {
                return false;
            }
            MoveAway();
            return true;
        }

        public bool Behavior10()
        {
            if (!_nearVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            MoveAway();
            return true;
        }

        public bool Behavior11()
        {
            if (_damaged || _shotCount > 0)
            {
                return false;
            }
            PickRoamTarget();
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            return true;
        }

        // todo: nearly the same as Behavior11
        public bool Behavior12()
        {
            if (!_damaged || _shotCount > 0)
            {
                return false;
            }
            PickRoamTarget();
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTime * 2); // todo: FPS stuff
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _damaged = false;
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            return true;
        }

        public override void Destroy()
        {
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            base.Destroy();
        }

        #region Boilerplate

        public static bool Behavior00(Enemy23Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy23Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy23Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy23Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy23Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy23Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy23Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy23Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy23Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy23Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy23Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy23Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy23Entity enemy)
        {
            return enemy.Behavior12();
        }

        #endregion
    }

    public struct Enemy23Values
    {
        public ushort HealthMax { get; set; }
        public ushort BeamDamage { get; set; }
        public ushort SplashDamage { get; set; }
        public ushort ContactDamage { get; set; }
        public int MinSpeedFactor1 { get; set; }
        public int MaxSpeedFactor1 { get; set; }
        public int MinSpeedFactor2 { get; set; }
        public int MaxSpeedFactor2 { get; set; }
        public int RangeMaxCosine { get; set; } // always -1 (greater than comparisons are always true)
        public int Unknown1C { get; set; } // functionless
        public int Unused20 { get; set; }
        public ushort DelayTime { get; set; }
        public ushort ShotTime { get; set; }
        public int Unused28 { get; set; }
        public ushort MinShots { get; set; }
        public ushort MaxShots { get; set; }
        public ushort DoubleSpeedSteps { get; set; }
        public ushort AimSteps { get; set; }
        public ushort SpeedSteps { get; set; } // set at runtime to DoubleSpeedSteps / 2
        public short ScanId { get; set; }
        public int Effectiveness { get; set; }
    }
}
