using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy00Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private uint _movementType; // todo: enum
        private CollisionVolume _movementVolume;
        private CollisionVolume _homeVolume;
        private float _stepDistance = 0;
        private byte _nextPattern = 0;
        private byte _pattern = 0; // todo: enum
        private byte _finalMoveIndex = 0;
        private int _stepCount = 0;
        private ushort _attackDelay = 0;
        private Vector3 _attackTarget;
        private Vector3 _moveTarget;
        private Vector3 _initialPos;
        private readonly Vector3[] _movePositions = new Vector3[16];
        private byte _moveIndex = 0;
        private byte _maxMoveIndex = 0;
        private float _circleAngle = 0;

        public Enemy00Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
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
            Vector3 facing = _spawner.Data.Header.FacingVector.ToFloatVector();
            Vector3 up = FixParallelVectors(facing, Vector3.UnitY);
            SetTransform(facing, up, _spawner.Data.Header.Position.ToFloatVector());
            _movementType = _spawner.Data.Fields.S01.WarWasp.MovementType;
            _health = _healthMax = (ushort)(_movementType == 3 ? 8 : 40);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, -0.45f, 0), 1.4f);
            _homeVolume = CollisionVolume.Move(_spawner.Data.Fields.S01.WarWasp.Volume2, Position);
            _movementVolume = CollisionVolume.Move(_spawner.Data.Fields.S01.WarWasp.Volume1, Position);
            SetUpModel(Metadata.EnemyModelNames[0], animIndex: 1);
            _stepDistance = 0.2f;
            _attackDelay = 30 * 2; // todo: FPS stuff
            _attackTarget = _initialPos = Position;
            if (_movementType == 1)
            {
                _moveIndex = 1;
                _maxMoveIndex = 3;
                _finalMoveIndex = _maxMoveIndex;
                float xx = _movementVolume.BoxVector3.X * _movementVolume.BoxDot1;
                float xz = _movementVolume.BoxVector3.X * _movementVolume.BoxDot3;
                float zx = _movementVolume.BoxVector3.Z * _movementVolume.BoxDot1;
                float zz = _movementVolume.BoxVector3.Z * _movementVolume.BoxDot3;
                _movePositions[0] = _movementVolume.BoxPosition;
                _movePositions[1] = _movementVolume.BoxPosition.AddX(xz).AddZ(zz);
                _movePositions[2] = _movementVolume.BoxPosition.AddX(xz - zx).AddZ(zz + xx);
                _movePositions[3] = _movementVolume.BoxPosition.AddX(-zx).AddZ(xx);
            }
            else if (_movementType == 2 || _movementType == 3)
            {
                _maxMoveIndex = (byte)(_spawner.Data.Fields.S01.WarWasp.PositionCount - 1);
                _finalMoveIndex = _maxMoveIndex;
                for (int i = 0; i < 16; i++)
                {
                    _movePositions[i] = _spawner.Data.Fields.S01.WarWasp.MovementVectors[i].ToFloatVector() + Position;
                }
            }
            if (_movementType != 0)
            {
                StartMovingTowardPosition();
            }
        }

        private void StartMovingToward(Vector3 target, float step)
        {
            Vector3 travel = target - Position;
            _stepDistance = step;
            float distance = travel.Length;
            _stepCount = (int)(distance / _stepDistance) + 1;
            if (distance == 0)
            {
                _speed = Vector3.Zero;
            }
            else
            {
                _speed = travel * (_stepDistance / distance);
                // todo: FPS stuff
                _speed /= 2;
                _stepCount *= 2;
            }
        }

        private void StartMovingTowardPosition()
        {
            _moveTarget = _movePositions[_moveIndex];
            StartMovingToward(_moveTarget, step: _movementType == 3 ? 0.25f : 0.2f);
            Vector3 facing = _speed == Vector3.Zero ? FacingVector : _speed.Normalized();
            SetTransform(FacingVector, Vector3.UnitY, Position);
        }

        private void MoveInCircle()
        {
            if (_movementType == 0)
            {
                _circleAngle += 1.5f / 2; // todo: FPS stuff
                if (_circleAngle >= 360)
                {
                    _circleAngle -= 360;
                }
                float angle = MathHelper.DegreesToRadians(_circleAngle);
                _speed.X = _initialPos.X + MathF.Sin(angle) * _movementVolume.CylinderRadius;
                _speed.Z = _initialPos.Z + MathF.Cos(angle) * _movementVolume.CylinderRadius;
                SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
            }
        }

        protected override void EnemyProcess()
        {
            if (_state1 != 4 && _state1 != 5)
            {
                ContactDamagePlayer(3, knockback: false);
            }
            _soundSource.PlaySfx(SfxId.WASP_IDLE, loop: true);
            CallStateProcess();
        }

        private void State0()
        {
            MoveInCircle();
            if (Position != _moveTarget && _movementType != 0)
            {
                SetTransform((_moveTarget - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this);
        }

        private void State1()
        {
            Vector3 playerPos = PlayerEntity.Main.Position;
            if (Position != playerPos)
            {
                SetTransform((playerPos - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this);
        }

        private void State2()
        {
            State1();
        }

        private void State3()
        {
            State1();
        }

        private void State4()
        {
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(25, DamageFlags.None, direction: null, this);
                _stepCount = 0;
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this);
        }

        private void State5()
        {
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(25, DamageFlags.None, direction: null, this);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this);
        }

        private void State6()
        {
            if (Position != _moveTarget)
            {
                SetTransform((_moveTarget - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this);
        }

        private bool Behavior00()
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            StartMovingToward(_attackTarget, step: 1.2f);
            return true;
        }

        private bool Behavior01()
        {
            _attackDelay = 30 * 2; // todo: FPS stuff
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            if (_movementType == 0)
            {
                _speed = Vector3.Zero;
            }
            else
            {
                StartMovingTowardPosition();
                _models[0].SetAnimation(1);
            }
            return true;
        }

        private bool Behavior02()
        {
            if (_movementType == 0 && (_state1 == 0 || _state1 == 1))
            {
                return false;
            }
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            if (_movementType != 0)
            {
                if (_pattern == 1)
                {
                    _moveIndex = (byte)(_moveIndex == 0 ? _maxMoveIndex : _moveIndex - 1);
                }
                else if (_pattern == 2 || _pattern == 0)
                {
                    _moveIndex = (byte)(_moveIndex >= _maxMoveIndex ? 0 : _moveIndex + 1);
                    if (_pattern == 0 && _movementType == 3 && _moveIndex == 0)
                    {
                        _health = 0;
                    }
                }
                else if (_pattern == 3)
                {
                    _moveIndex--;
                }
                _moveTarget = _movePositions[_moveIndex];
                StartMovingToward(_moveTarget, _state1 == 6 ? 0.2f : _stepDistance);
                SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
            }
            return true;
        }

        private bool Behavior03()
        {
            if (_movementType == 3 || !_homeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _finalMoveIndex = _moveIndex;
            _nextPattern = _pattern = 2;
            _stepDistance = 0.15f;
            return true;
        }

        private bool Behavior04()
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            StartMovingToward(_moveTarget, 1.2f);
            return true;
        }

        private bool Behavior05()
        {
            if (!HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true))
            {
                return false;
            }
            StartMovingToward(_moveTarget, 1.2f);
            SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
            _models[0].SetAnimation(1);
            return true;
        }

        private bool Behavior06()
        {
            if (_movementType != 0 && _finalMoveIndex != _moveIndex)
            {
                return false;
            }
            _speed = Vector3.Zero;
            Vector3 playerPos = PlayerEntity.Main.Position;
            Vector3 facing = playerPos - Position;
            if (Position != playerPos)
            {
                facing = facing.Normalized();
            }
            SetTransform(facing, Vector3.UnitY, Position);
            return true;
        }

        private bool Behavior07()
        {
            if (_homeVolume.TestPoint(Position))
            {
                return false;
            }
            ReachTargetOrReversePattern();
            return true;
        }

        private bool Behavior08()
        {
            if (_homeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            ReversePattern();
            return true;
        }

        private void ReachTargetOrReversePattern()
        {
            _speed = _moveTarget - Position;
            if (_speed.Length == 0)
            {
                _stepCount = 0;
            }
            else
            {
                ReversePattern();
            }
        }

        private void ReversePattern()
        {
            _pattern = _nextPattern;
            if (_pattern == 0)
            {
                _finalMoveIndex = _maxMoveIndex;
                _moveIndex = (byte)(_moveIndex >= _maxMoveIndex ? 0 : _moveIndex + 1);
            }
            else
            {
                _finalMoveIndex = 0;
                _moveIndex = (byte)(_moveIndex == 0 ? _maxMoveIndex : _moveIndex - 1);
            }
            StartMovingTowardPosition();
        }

        private bool Behavior09()
        {
            if (_attackDelay > 0)
            {
                _attackDelay--;
                return false;
            }
            _attackTarget = PlayerEntity.Main.Position;
            _stepCount = 40 * 2; // todo: FPS stuff
            _models[0].SetAnimation(3, AnimFlags.NoLoop);
            _soundSource.PlaySfx(SfxId.WASP_ATTACK_SCR);
            return true;
        }

        private bool Behavior10()
        {
            CollisionResult res = default;
            if (!CollisionDetection.CheckBetweenPoints(Position, PlayerEntity.Main.Position, TestFlags.None, _scene, ref res))
            {
                return false;
            }
            ReachTargetOrReversePattern();
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy00Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy00Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy00Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy00Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy00Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy00Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy00Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy00Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy00Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy00Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy00Entity enemy)
        {
            return enemy.Behavior10();
        }

        #endregion
    }
}
