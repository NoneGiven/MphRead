using System;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;
namespace MphRead.Entities.Enemies
{
    public class Enemy23Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private Enemy23Values _values;
        private CollisionVolume _volume1;
        private CollisionVolume _volume2;
        private CollisionVolume _volume3;

        private Vector3 _field264;
        private Vector3 _moveTarget;
        private Vector3 _field2D8;
        private float _roamAngleSign = 1; // sign for some angle
        private ushort _field250 = 0;
        private ushort _shotCount = 0;
        private ushort _shotTimer = 0;
        private bool _field2B9 = false;
        private float _speedFactor = 0;
        private float _speedInc = 0;
        private bool _field2BB = false;
        private float _moveDistSqr = 0; // square of the length of the full vector to the move target
        private float _moveDistSqrHalf = 0; // half of _moveDistSqr, switch from gaining speed to losing speed when reached
        private bool _increaseSpeed = false; // true during the first part of the move to a roam target, then false
        private ushort _field2D4 = 0;

        private Vector3 _eyePos;
        private Vector3 _aimVec = Vector3.UnitX;
        private Vector3 _targetVec;
        private Vector3 _crossVec = Vector3.UnitX;
        private float _aimAngleStep = 0;
        private ushort _aimSteps = 0;

        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;
        private EffectEntry? _effect = null;

        public Enemy23Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[11]
            {
                State00, State01, State02, State03, State04, State05, State06, State07, State08, State09, State10
            };
        }

        protected override bool EnemyInitialize()
        {
            Recolor = (int)_spawner.Data.Fields.S06.EnemySubtype;
            Vector3 facing = _spawner.FacingVector;
            SetTransform(facing, Vector3.UnitY, _spawner.Position);
            SetUpModel(Metadata.EnemyModelNames[23], animIndex: 3);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S06.Volume0);
            _values = Metadata.Enemy23Values[Recolor];
            _health = _healthMax = _values.HealthMax;
            Metadata.LoadEffectiveness(_values.Effectiveness, BeamEffectiveness);
            // todo: scan ID
            _field264 = facing;
            _volume1 = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume1, Position);
            _volume2 = new CollisionVolume(Vector3.Zero, 1); // gets moved in the process function
            _volume3 = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume3, Position);
            WeaponInfo weapon = Weapons.EnemyWeapons[(int)_spawner.Data.Fields.S06.EnemyVersion];
            weapon.UnchargedDamage = _values.BeamDamage;
            weapon.SplashDamage = _values.SplashDamage;
            _equipInfo = new EquipInfo(weapon, _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
            _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
            _speedFactor = Fixed.ToFloat(_values.MinSpeedFactor1);
            _state1 = _state2 = 9;
            Debug.Assert(_volume1.Type != VolumeType.Sphere);
            bool atHomePoint = false;
            Vector3 homePoint = Vector3.Zero;
            if (_spawner.Data.SpawnerHealth == 0)
            {
                atHomePoint = true;
            }
            else if (_volume1.Type == VolumeType.Cylinder)
            {
                Debug.Assert(_volume1.CylinderVector == Vector3.UnitY);
                homePoint = new Vector3(
                    _volume1.CylinderPosition.X,
                    _volume1.CylinderPosition.Y + _volume1.CylinderDot / 2,
                    _volume1.CylinderPosition.Z
                );
                if (MathF.Abs(Position.X - homePoint.X) < 1 / 4096f
                    && MathF.Abs(Position.Y - homePoint.Y) < 1 / 4096f
                    && MathF.Abs(Position.Z - homePoint.Z) < 1 / 4096f)
                {
                    atHomePoint = true;
                }
            }
            else if (_volume1.Type == VolumeType.Box)
            {
                homePoint = new Vector3(
                    _volume1.BoxPosition.X + _volume1.BoxVector1.X * (_volume1.BoxDot1 / 2),
                    _volume1.BoxPosition.Y + _volume1.BoxVector2.Y * (_volume1.BoxDot2 / 2),
                    _volume1.BoxPosition.Z + _volume1.BoxVector3.Z * (_volume1.BoxDot3 / 2)
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
                _speedFactor = Fixed.ToFloat(_values.MinSpeedFactor2); // sktodo: FPS stuff?
            }
            _increaseSpeed = true;
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _aimVec = (PlayerEntity.Main.Position - Position).Normalized();
            _subId = _state1;
            return true;
        }

        protected override void EnemyProcess()
        {
            if (_effect != null)
            {
                Vector3 facing = FacingVector;
                _eyePos = Position - facing;
                _effect.Transform(facing, UpVector, _eyePos);
            }
            ContactDamagePlayer(_values.ContactDamage, knockback: true);
            _volume2 = CollisionVolume.Move(new CollisionVolume(Vector3.Zero, 1), Position);
            // todo: play SFX
            CallStateProcess();
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health > 0)
            {
                if (_state1 == 3)
                {
                    _field2B9 = true;
                }
                else if (_state1 != 9 && _state1 != 10)
                {
                    _state2 = 3;
                    _subId = _state2;
                    if (_state1 == 2)
                    {
                        // todo: stop SFX
                    }
                    if (_effect == null)
                    {
                        SpawnEffect();
                    }
                    _speed = Vector3.Zero;
                    _speedFactor = 0;
                    _speedInc = 0;
                    _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
                    _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
                    _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
                    _aimVec = (PlayerEntity.Main.Position - Position).Normalized();
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    SetTransform(_aimVec, UpVector, Position);
                    _field264 = _aimVec;
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
            _effect.SetElementExtension(true);
        }

        private void UpdateMoveTarget(Vector3 targetPoint)
        {
            // sktodo: FPS stuff?
            Vector3 facing = FacingVector;
            _moveTarget = targetPoint;
            _field2D8 = Position;
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
            if (_volume1.Type == VolumeType.Cylinder)
            {
                float dist = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_volume1.CylinderRadius)));
                var vec = new Vector3(dist, 0, 0);
                _roamAngleSign *= -1;
                float angle = Fixed.ToFloat(Rng.GetRandomInt2(0xB4000)) * _roamAngleSign; // [0-180)
                var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(angle));
                vec = Matrix.Vec3MultMtx3(vec, rotY);
                vec.Y = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_volume1.CylinderDot)));
                moveTarget = _volume1.CylinderPosition + vec;
            }
            else
            {
                Debug.Assert(_volume1.Type == VolumeType.Box);
                float distX = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_volume1.BoxDot1)));
                float distY = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_volume1.BoxDot2)));
                float distZ = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_volume1.BoxDot3)));
                var vec = new Vector3(
                    _volume1.BoxVector1.X * distX,
                    _volume1.BoxVector2.Y * distY,
                    _volume1.BoxVector3.Z * distZ
                );
                moveTarget = _volume1.BoxPosition + vec;
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
            if (_increaseSpeed)
            {
                _speedFactor += _speedInc / 2; // todo: FPS stuff
                if (_speedFactor > max)
                {
                    _speedFactor = max;
                }
            }
            else
            {
                _speedFactor -= _speedInc / 2; // todo: FPS stuff
                if (_speedFactor < min)
                {
                    _speedFactor = min;
                }
            }
            _speed = FacingVector * _speedFactor;
            _speed /= 2; // todo: FPS stuff
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
            if (Vector3.Dot(_field264, between) > Fixed.ToFloat(_values.Field18))
            {
                SetTransform(between, UpVector, Position);
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
                _equipInfo.Weapon.UnchargedDamage = _values.BeamDamage;
                _equipInfo.Weapon.SplashDamage = _values.SplashDamage;
                _equipInfo.Weapon.HeadshotDamage = _values.BeamDamage;
                BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, _aimVec, BeamSpawnFlags.None, _scene);
                _shotCount--;
                _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
                // todo: play SFX
                _models[0].SetAnimation(0, AnimFlags.NoLoop);
            }
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        private void State04()
        {
            UpdateSpeed(Fixed.ToFloat(_values.MinSpeedFactor1), Fixed.ToFloat(_values.MaxSpeedFactor1));
            if (CallSubroutine(Metadata.Enemy23Subroutines, this) && _state2 == 4)
            {
                _speed = Vector3.Zero;
                _field264 = FacingVector;
                _models[0].SetAnimation(1);
                // todo: play SFX
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
            _speed /= 2; // todo: FPS stuff
            CallSubroutine(Metadata.Enemy23Subroutines, this);
        }

        public bool Behavior00()
        {
            Vector3 facing = FacingVector;
            bool result = SeekTargetVector(_targetVec, ref facing, _crossVec, ref _aimSteps, _aimAngleStep);
            SetTransform(facing, UpVector, Position);
            if (!result)
            {
                return false;
            }
            float minFactor = Fixed.ToFloat(_values.MinSpeedFactor2);
            float maxFactor = Fixed.ToFloat(_values.MaxSpeedFactor2);
            _speedFactor = minFactor;
            _speedInc = (maxFactor - minFactor) * (1 / Fixed.ToFloat(_values.Field34));
            _speed = facing * _speedFactor;
            _speed /= 2; // todo: FPS stuff
            _field264 = facing;
            _models[0].SetAnimation(2);
            return true;
        }

        // or give up if the length has increased
        private void CheckReachedTarget()
        {
            Vector3 nextPos = Position + _speed;
            if ((_field2D8 - nextPos).LengthSquared > _moveDistSqr)
            {
                _speed = nextPos - Position;
                _field2BB = true;
            }
        }

        public bool Behavior01()
        {
            if (_field2BB)
            {
                PickRoamTarget();
                _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
                _field2BB = false;
                return true;
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
            SetTransform(facing, UpVector, Position);
            if (!result)
            {
                return false;
            }
            _speedInc = 0;
            _speedFactor = 0;
            _field264 = facing;
            _models[0].SetAnimation(1);
            // todo: play SFX
            SpawnEffect();
            return true;
        }

        public bool Behavior04()
        {
            return Behavior00();
        }

        // todo: same as Behavior00 except using te first pair of factors and not setting animation
        public bool Behavior05()
        {
            Vector3 facing = FacingVector;
            bool result = SeekTargetVector(_targetVec, ref facing, _crossVec, ref _aimSteps, _aimAngleStep);
            SetTransform(facing, UpVector, Position);
            if (!result)
            {
                return false;
            }
            float minFactor = Fixed.ToFloat(_values.MinSpeedFactor1);
            float maxFactor = Fixed.ToFloat(_values.MaxSpeedFactor1);
            _speedFactor = minFactor;
            _speedInc = (maxFactor - minFactor) * (1 / Fixed.ToFloat(_values.Field34));
            _speed = facing * _speedFactor;
            _speed /= 2; // todo: FPS stuff
            _field264 = facing;
            return true;
        }

        public bool Behavior06()
        {
            if (_field2BB)
            {
                Vector3 facing = FacingVector;
                _targetVec = (PlayerEntity.Main.Position - Position).Normalized();
                float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(facing, _targetVec)));
                _aimSteps = (ushort)(_values.AimSteps * 2); // todo: FPS stuff
                _aimAngleStep = angle / _aimSteps;
                _crossVec = Vector3.Cross(facing, _targetVec).Normalized();
                _speed = Vector3.Zero;
                _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
                _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
                return true;
            }
            CheckReachedTarget();
            return false;
        }

        public bool Behavior07()
        {
            // todo: if in cam seq blocking input, set _field2D4 to 40 * 2 and return false
            if (_field2D4 > 0)
            {
                _field2D4--;
                return false;
            }
            if (PlayerEntity.Main.Health > 0)
            {
                Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
                if (Vector3.Dot(_field264, between) > Fixed.ToFloat(_values.Field18)
                    && _volume3.TestPoint(PlayerEntity.Main.Position))
                {
                    _speed = Vector3.Zero;
                    _models[0].SetAnimation(1);
                    // todo: play SFX
                    SpawnEffect();
                    return true;
                }
            }
            return false;
        }

        public bool Behavior08()
        {
            if (_field250 > 0)
            {
                _field250--;
                return false;
            }
            // todo: stop SFX
            _models[0].SetAnimation(0, AnimFlags.NoLoop);
            _aimVec = (PlayerEntity.Main.Position + Position).Normalized();
            return true;
        }

        // todo: function name?
        private void MoveAway()
        {
            PickRoamTarget();
            _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
            _models[0].SetAnimation(3);
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            if (_state1 == 2)
            {
                // todo: stop SFX
            }
        }

        public bool Behavior09()
        {
            Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
            if (Vector3.Dot(_field264, between) <= Fixed.ToFloat(_values.Field18))
            {
                return false;
            }
            MoveAway();
            return true;
        }

        public bool Behavior10()
        {
            if (!_volume2.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            MoveAway();
            return true;
        }

        public bool Behavior11()
        {
            if (_field2B9 || _shotCount > 0)
            {
                return false;
            }
            PickRoamTarget();
            _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
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
            if (!_field2B9 || _shotCount > 0)
            {
                return false;
            }
            PickRoamTarget();
            _field250 = (ushort)(_values.Field24 * 2); // todo: FPS stuff
            _shotTimer = (ushort)(_values.ShotTimer * 2); // todo: FPS stuff
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _field2B9 = false;
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
            return true;
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
        public int Field18 { get; set; }
        public int Unknown1C { get; set; } // functionless
        public int Unused20 { get; set; }
        public ushort Field24 { get; set; }
        public ushort ShotTimer { get; set; }
        public int Unused28 { get; set; }
        public ushort MinShots { get; set; }
        public ushort MaxShots { get; set; }
        public ushort Field30 { get; set; }
        public ushort AimSteps { get; set; }
        public ushort Field34 { get; set; } // set at runtime to Field30 / 2
        public short ScanId { get; set; }
        public int Effectiveness { get; set; }
    }
}
