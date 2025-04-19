using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy35Entity : EnemyInstanceEntity
    {
        protected readonly EnemySpawnEntity _spawner;
        protected CollisionVolume _homeVolume;
        private bool _handledRamCol = false;
        // for (unused) rehits? game uses a byte and decrements it instead of using a bool
        private bool _ramDamageNeeded = false;
        private ushort _ramDelay = 0;
        protected ushort _timeInAir = 0;
        protected bool _airborne = false;
        protected bool _grounded = false;

        private Vector3 _moveTarget;
        protected Vector3 _targetVec;
        protected Vector3 _moveStart;
        private float _roamAngleSign = 1;
        protected float _aimAngleStep = 0;
        protected ushort _aimSteps = 0;
        protected float _moveDistSqr = 0;
        private float _moveDistSqrHalf = 0;
        private bool _increaseSpeed = false;
        protected float _speedFactor = 0;
        protected float _speedInc = 0;
        private float _rollSfxAmount = 0;

        // todo: FPS stuff
        protected float _speedIncAmount = 410 / 4096f * (1 / 3f) / 2;
        protected ushort _aimStepCount = 10 * 2;
        protected float _minSpeedFactor = 0.1f / 2;
        protected float _maxSpeedFactor = 0.2f / 2;

        public Enemy35Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[7]
            {
                State0, State1, State2, State3, State4, State5, State6
            };
        }

        protected override void EnemyInitialize()
        {
            Setup();
        }

        protected virtual void Setup()
        {
            Vector3 facing = _spawner.Data.Header.FacingVector.ToFloatVector().Normalized();
            Vector3 up = FixParallelVectors(facing, Vector3.UnitY);
            SetTransform(facing, up, _spawner.Data.Header.Position.ToFloatVector());
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 0.5f;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S00.Volume0);
            _homeVolume = CollisionVolume.Move(_spawner.Data.Fields.S00.Volume1, Position);
            Debug.Assert(_homeVolume.Type == VolumeType.Cylinder);
            _health = _healthMax = 42;
            _speedInc = _speedIncAmount;
            _speedFactor = _minSpeedFactor;
            _ramDamageNeeded = true;
            _ramDelay = 40 * 2; // todo: FPS stuff
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
            SetUpModel(Metadata.EnemyModelNames[35], animIndex: 4);
        }

        protected override void EnemyProcess()
        {
            bool sfxGrounded = true;
            if (!_grounded)
            {
                _speed.Y -= Fixed.ToFloat(110) / 4; // todo: FPS stuff
            }
            if (_state1 == 2)
            {
                bool discard = false;
                if (!HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true, ref _grounded, ref discard))
                {
                    sfxGrounded = false;
                }
            }
            else if (_state1 == 0 || _state1 == 6)
            {
                if (!HandleCollision())
                {
                    sfxGrounded = false;
                }
            }
            if (_state1 != 3 && _state1 != 4)
            {
                ContactDamagePlayer(2, knockback: true);
            }
            CallStateProcess();
            if (_state1 != 0 && _state1 != 6)
            {
                sfxGrounded = false;
            }
            float amount = 0xFFFF * _speedFactor * 2 / 0.2f; // todo: FPS suff
            UpdateRollSfx(amount, sfxGrounded);
        }

        protected void UpdateRollSfx(float newAmount, bool grounded)
        {
            float prevAmount = _rollSfxAmount;
            if (!grounded)
            {
                newAmount = ExponentialDecay(0.5f, prevAmount);
            }
            else if (_scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                if (newAmount < prevAmount)
                {
                    newAmount = prevAmount + (newAmount - prevAmount) / 4;
                }
                else
                {
                    newAmount = prevAmount + (newAmount - prevAmount) / 2;
                }
            }
            else
            {
                newAmount = _rollSfxAmount;
            }
            if (newAmount < 20)
            {
                newAmount = 0;
            }
            _rollSfxAmount = newAmount;
            _soundSource.PlaySfx(SfxId.DGN_GUARD_BOT_ROLL, loop: true, amountA: _rollSfxAmount);
        }

        protected virtual bool HandleCollision()
        {
            return HandleCollision(5, 6);
        }

        protected bool HandleCollision(int stateA, int stateB)
        {
            _grounded = false;
            var results = new CollisionResult[30];
            int count = CollisionDetection.CheckInRadius(Position, _boundingRadius, limit: 30,
                getSimpleNormal: false, TestFlags.None, _scene, results);
            if (count == 0)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                CollisionResult result = results[i];
                float v7;
                if (result.Field0 != 0)
                {
                    v7 = _boundingRadius - result.Field14;
                }
                else
                {
                    v7 = _boundingRadius + result.Plane.W - Vector3.Dot(Position, result.Plane.Xyz);
                }
                if (v7 > 0)
                {
                    Position += result.Plane.Xyz * v7;
                    if (result.Plane.Y >= 0.1f || result.Plane.Y <= -0.1f)
                    {
                        _grounded = true;
                    }
                    else if (_state1 != 1 && _state1 != stateA)
                    {
                        _airborne = true;
                        if (_state1 != 0 && _state1 != stateB)
                        {
                            _speed = -_speed;
                            _speed.Y = Fixed.ToFloat(1000) / 2; // todo: FPS stuff
                        }
                        else
                        {
                            _timeInAir++;
                        }
                    }
                    float dot = Vector3.Dot(_speed, result.Plane.Xyz);
                    if (dot < 0)
                    {
                        _speed += result.Plane.Xyz * -dot;
                    }
                }
            }
            return true;
        }

        // todo: this is similar to Psycho Bit (cylinder only)
        protected void PickRoamTarget()
        {
            float dist = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(_homeVolume.CylinderRadius)));
            var vec = new Vector3(dist, 0, 0);
            _roamAngleSign *= -1;
            float angle = Fixed.ToFloat(Rng.GetRandomInt2(0xB4000)) * _roamAngleSign; // [0-180)
            var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(angle));
            vec = Matrix.Vec3MultMtx3(vec, rotY);
            var moveTarget = new Vector3(_homeVolume.CylinderPosition.X + vec.X, Position.Y, _homeVolume.CylinderPosition.Z + vec.Z);
            UpdateMoveTarget(moveTarget);
        }

        // todo: this is similar to Psycho Bit (doesn't have cross vector)
        protected void UpdateMoveTarget(Vector3 targetPoint)
        {
            _moveTarget = targetPoint;
            _moveStart = Position;
            _targetVec = _moveTarget - Position;
            _moveDistSqr = _targetVec.LengthSquared;
            _moveDistSqrHalf = _moveDistSqr / 2;
            _increaseSpeed = true;
            _targetVec = _targetVec.Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _aimSteps = _aimStepCount;
            _aimAngleStep = angle / _aimSteps;
        }

        // todo: this is similar to Psycho Bit (no values struct, no y speed)
        protected void UpdateSpeed()
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
                _speedFactor += _speedInc;
                if (_speedFactor > _maxSpeedFactor)
                {
                    _speedFactor = _maxSpeedFactor;
                }
            }
            else
            {
                _speedFactor -= _speedInc;
                if (_speedFactor < _minSpeedFactor)
                {
                    _speedFactor = _minSpeedFactor;
                }
            }
            Vector3 facing = FacingVector;
            _speed.X = facing.X * _speedFactor;
            _speed.Z = facing.Z * _speedFactor;
        }

        private void State0()
        {
            UpdateSpeed();
            CallSubroutine(Metadata.Enemy35Subroutines, this);
        }

        private void State1()
        {
            CallSubroutine(Metadata.Enemy35Subroutines, this);
        }

        private void State2()
        {
            Vector3 facing = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            SetTransform(facing, Vector3.UnitY, Position);
            CallSubroutine(Metadata.Enemy35Subroutines, this);
        }

        // todo: could be shared as part of the speed update function
        private void State3()
        {
            _speedFactor -= _speedInc;
            if (_speedFactor < _minSpeedFactor)
            {
                _speedFactor = _minSpeedFactor;
            }
            Vector3 facing = FacingVector;
            _speed.X = facing.X * _speedFactor;
            _speed.Z = facing.Z * _speedFactor;
            CallSubroutine(Metadata.Enemy35Subroutines, this);
        }

        private void State4()
        {
            if (_handledRamCol && _ramDamageNeeded)
            {
                // pretty sure this can't be true, as when _handledRamCol is set true, _ramDamageNeeded is set false
                PlayerEntity.Main.TakeDamage(15, DamageFlags.NoDmgInvuln, null, this);
                _ramDamageNeeded = false;
            }
            else
            {
                CallSubroutine(Metadata.Enemy35Subroutines, this);
            }
        }

        private void State5()
        {
            CallSubroutine(Metadata.Enemy35Subroutines, this);
        }

        private void State6()
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
            _speed = FacingVector * _speedFactor;
            return true;
        }

        private bool Behavior01()
        {
            if (!HandleCollision())
            {
                return false;
            }
            PickRoamTarget();
            _ramDamageNeeded = true;
            _handledRamCol = false;
            _speedInc = _speedIncAmount;
            _speedFactor = _minSpeedFactor;
            _speed = FacingVector * _speedFactor;
            return true;
        }

        // hop
        private bool Behavior02()
        {
            Vector3 between = Position - _moveStart;
            if (between.LengthSquared <= _moveDistSqr && (!_airborne || _timeInAir <= 5 * 2)) // todo: FPS stuff
            {
                return false;
            }
            PickRoamTarget();
            _speed = new Vector3(0, 0.2f / 2, 0); // todo: FPS stuff
            _timeInAir = 0;
            _airborne = false;
            _soundSource.PlaySfx(SfxId.GUARD_BOT_JUMP);
            return true;
        }

        // prepare to ram
        private bool Behavior03()
        {
            if (_ramDelay > 0)
            {
                _ramDelay--;
                return false;
            }
            Vector3 facing = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            SetTransform(facing, Vector3.UnitY, Position);
            _timeInAir = 0;
            _airborne = false;
            _moveTarget = PlayerEntity.Main.Position.WithY(Position.Y);
            _targetVec = _moveTarget - Position;
            _moveDistSqr = _targetVec.LengthSquared;
            _moveDistSqrHalf = _moveDistSqr / 2;
            _speedInc = 0.005f / 2; // todo: FPS stuff
            _speedFactor = 0.6f / 2; // todo: FPS stuff
            _ramDelay = 40 * 2; // todo: FPS stuff
            _models[0].SetAnimation(0);
            _soundSource.PlaySfx(SfxId.GUARD_BOT_ATTACK1);
            return true;
        }

        private bool Behavior04()
        {
            bool collided = HandleCollision();
            if (!_handledRamCol && HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(15, DamageFlags.NoDmgInvuln, null, this);
                _handledRamCol = true;
                _ramDamageNeeded = false;
                _speed = -_speed;
                _speed.Y = Fixed.ToFloat(1000) / 2; // todo: FPS stuff
                _models[0].SetAnimation(4);
                return true;
            }
            if (collided && _airborne)
            {
                _airborne = false;
                _timeInAir = 0;
                _models[0].SetAnimation(4);
                return true;
            }
            return false;
        }

        // also hop
        private bool Behavior05()
        {
            if (_speedFactor != _maxSpeedFactor)
            {
                Debug.Assert(MathF.Abs(_speedFactor - _maxSpeedFactor) >= 1 / 4096f);
                if (_homeVolume.TestPoint(Position))
                {
                    return false;
                }
            }
            PickRoamTarget();
            _speed = new Vector3(0, 0.2f / 2, 0); // todo: FPS stuff
            _models[0].SetAnimation(4);
            _soundSource.PlaySfx(SfxId.GUARD_BOT_JUMP);
            return true;
        }

        private bool Behavior06()
        {
            if (PlayerEntity.Main.Health == 0)
            {
                return false;
            }
            Vector3 between = (PlayerEntity.Main.Position - Position).Normalized();
            // as with Psycho Bit, the dot product check could facilitate a vision angle range, but as written it's pointless
            if (Vector3.Dot(FacingVector, between) <= -1 || !_homeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _speed = Vector3.Zero;
            _models[0].SetAnimation(1);
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy35Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy35Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy35Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy35Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy35Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy35Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy35Entity enemy)
        {
            return enemy.Behavior06();
        }

        #endregion
    }
}
