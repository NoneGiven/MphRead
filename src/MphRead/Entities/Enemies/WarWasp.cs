using System;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy00Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private uint _movementType; // todo: enum
        private CollisionVolume _volume1;
        private CollisionVolume _volume2;
        private float _stepDistance = 0;
        private byte _nextPattern = 0;
        private byte _pattern = 0; // todo: enum
        private byte _finalMoveIndex = 0;
        private int _stepCount = 0;
        private ushort _attackDelay = 0;
        private Vector3 _attackTarget;
        private Vector3 _moveTarget;
        private Vector3 _initPos;
        private readonly Vector3[] _movePositions = new Vector3[16];
        private byte _moveIndex = 0;
        private byte _maxMoveIndex = 0;
        private float _circleAngle = 0;

        public Enemy00Entity(EnemyInstanceEntityData data) : base(data)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action<Scene>[7]
            {
                State0, State1, State2, State3, State4, State5, State6
            };
        }

        protected override bool EnemyInitialize(Scene scene)
        {
            Matrix4 transform = GetTransformMatrix(_spawner.Data.Header.FacingVector.ToFloatVector(), Vector3.UnitY);
            transform.Row3.Xyz = _spawner.Data.Header.Position.ToFloatVector();
            Transform = transform;
            _movementType = _spawner.Data.Fields.S01.WarWasp.MovementType;
            _health = _healthMax = (ushort)(_movementType == 3 ? 8 : 40);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, Fixed.ToFloat(-1843), 0), Fixed.ToFloat(5734));
            _volume2 = CollisionVolume.Move(_spawner.Data.Fields.S01.WarWasp.Volume2, Position);
            _volume1 = CollisionVolume.Move(_spawner.Data.Fields.S01.WarWasp.Volume1, Position);
            SetUpModel("warwasp_lod0", animIndex: 1);
            _stepDistance = 0.2f;
            _attackDelay = 30 * 2; // todo: FPS stuff
            _attackTarget = _initPos = Position;
            if (_movementType == 1)
            {
                _moveIndex = 1;
                _maxMoveIndex = 3;
                _finalMoveIndex = _maxMoveIndex;
                float xx = _volume1.BoxVector3.X * _volume1.BoxDot1;
                float xz = _volume1.BoxVector3.X * _volume1.BoxDot3;
                float zx = _volume1.BoxVector3.Z * _volume1.BoxDot1;
                float zz = _volume1.BoxVector3.Z * _volume1.BoxDot3;
                _movePositions[0] = _volume1.BoxPosition;
                _movePositions[1] = _volume1.BoxPosition.AddX(xz).AddZ(zz);
                _movePositions[2] = _volume1.BoxPosition.AddX(xz - zx).AddZ(zz + xx);
                _movePositions[3] = _volume1.BoxPosition.AddX(-zx).AddZ(xx);
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
                Func2161E30();
            }
            return true;
        }

        private void StartMovingToward(Vector3 target, float step)
        {
            Vector3 travel = target - Position;
            _stepDistance = step / 2; // todo: FPS stuff
            float distance = travel.Length;
            _stepCount = (int)(distance / _stepDistance) + 1; // todo: why add 1?
            _speed = travel * (_stepDistance / distance);
        }

        // todo: function names
        private void Func2161E30()
        {
            _moveTarget = _movePositions[_moveIndex];
            StartMovingToward(_moveTarget, step: _movementType == 3 ? 0.25f : 0.2f);
            SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
        }

        private void Func2161F2C()
        {
            if (_movementType == 0)
            {
                _circleAngle += 1.5f / 2; // todo: FPS stuff
                if (_circleAngle >= 360)
                {
                    _circleAngle -= 360;
                }
                float angle = MathHelper.DegreesToRadians(_circleAngle);
                _speed.X = _initPos.X + MathF.Sin(angle) * _volume1.CylinderRadius;
                _speed.Z = _initPos.Z + MathF.Cos(angle) * _volume1.CylinderRadius;
                SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
            }
        }

        private void State0(Scene scene)
        {
            Func2161F2C();
            if (Position != _moveTarget && _movementType != 0)
            {
                SetTransform((_moveTarget - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this, scene);
        }

        private void State1(Scene scene)
        {
            // todo: use player position
            if (Position != scene.CameraPosition)
            {
                SetTransform((scene.CameraPosition - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this, scene);
        }

        private void State2(Scene scene)
        {
            State1(scene);
        }

        private void State3(Scene scene)
        {
            State1(scene);
        }

        private void State4(Scene scene)
        {
            if ((HitPlayers & 1) != 0) // todo: use player slot index
            {
                // todo: damage player
                _stepCount = 0;
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this, scene);
        }

        private void State5(Scene scene)
        {
            if ((HitPlayers & 1) != 0) // todo: use player slot index
            {
                // todo: damage player
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this, scene);
        }

        private void State6(Scene scene)
        {
            if (Position != _moveTarget)
            {
                SetTransform((_moveTarget - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy00Subroutines, this, scene);
        }

        private bool Behavior00(Scene scene)
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            StartMovingToward(_attackTarget, step: 1.2f);
            return true;
        }

        private bool Behavior01(Scene scene)
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
                Func2161E30();
                _models[0].SetAnimation(1);
            }
            return true;
        }

        private bool Behavior02(Scene scene)
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

        private bool Behavior03(Scene scene)
        {
            if (_movementType == 3 || !_volume2.TestPoint(scene.CameraPosition)) // todo: use player position
            {
                return false;
            }
            _finalMoveIndex = _moveIndex;
            _nextPattern = _pattern = 2;
            _stepDistance = 0.15f;
            return true;
        }

        private bool Behavior04(Scene scene)
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            StartMovingToward(_moveTarget, 1.2f);
            return true;
        }

        private bool Behavior05(Scene scene)
        {
            // sktodo: sub_204FA3C
            StartMovingToward(_moveTarget, 1.2f);
            SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
            _models[0].SetAnimation(1);
            return true;
        }

        private bool Behavior06(Scene scene)
        {
            if (_movementType != 0 && _finalMoveIndex != _moveIndex)
            {
                return false;
            }
            _speed = Vector3.Zero;
            // todo: use player position
            Vector3 facing = scene.CameraPosition - Position;
            if (Position != scene.CameraPosition)
            {
                facing = facing.Normalized();
            }
            SetTransform(facing, Vector3.UnitY, Position);
            return true;
        }

        private bool Behavior07(Scene scene)
        {
            if (_volume2.TestPoint(Position))
            {
                return false;
            }
            DoThing1();
            return true;
        }

        private void DoThing1()
        {
            _speed = _moveTarget - Position;
            if (_speed.Length == 0)
            {
                _stepCount = 0;
            }
            else
            {
                DoThing2();
            }
        }

        private bool Behavior08(Scene scene)
        {
            if (_volume2.TestPoint(scene.CameraPosition)) // todo: use player position  
            {
                return false;
            }
            DoThing2();
            return true;
        }

        private void DoThing2()
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
            Func2161E30();
        }

        private bool Behavior09(Scene scene)
        {
            if (_attackDelay > 0)
            {
                _attackDelay--;
                return false;
            }
            _attackTarget = scene.CameraPosition; // todo: use player position
            _stepCount = 40 * 2; // todo: FPS stuff
            _models[0].SetAnimation(3, AnimFlags.NoLoop);
            // todo: play SFX
            return true;
        }

        private bool Behavior10(Scene scene)
        {
            CollisionResult res = default;
            if (!CollisionDetection.CheckBetweenPoints(Position, scene.CameraPosition, TestFlags.None, scene, ref res))
            {
                return false;
            }
            DoThing1();
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior00(scene);
        }

        public static bool Behavior01(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior01(scene);
        }

        public static bool Behavior02(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior02(scene);
        }

        public static bool Behavior03(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior03(scene);
        }

        public static bool Behavior04(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior04(scene);
        }

        public static bool Behavior05(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior05(scene);
        }

        public static bool Behavior06(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior06(scene);
        }

        public static bool Behavior07(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior07(scene);
        }

        public static bool Behavior08(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior08(scene);
        }

        public static bool Behavior09(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior09(scene);
        }

        public static bool Behavior10(Enemy00Entity enemy, Scene scene)
        {
            return enemy.Behavior10(scene);
        }

        #endregion
    }
}
