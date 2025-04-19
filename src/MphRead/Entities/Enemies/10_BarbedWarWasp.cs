using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy10Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private Enemy10Values _values;
        private uint _movementType; // todo: enum
        private CollisionVolume _movementVolume;
        private CollisionVolume _homeVolume;
        private float _stepDistance = 0;
        private byte _nextPattern = 0;
        private byte _pattern = 0; // todo: enum
        private byte _finalMoveIndex = 0;
        private int _stepCount = 0;
        private Vector3 _moveTarget;
        private Vector3 _initialPos;
        private Vector3 _aimVector;
        private readonly Vector3[] _movePositions = new Vector3[16];
        private byte _moveIndex = 0;
        private byte _maxMoveIndex = 0;
        private float _circleAngle = 0;

        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;
        private ushort _shotCount = 0;
        private ushort _shotTimer = 0;

        public Enemy10Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[6]
            {
                State0, State1, State2, State3, State4, State5
            };
        }

        private static readonly int[] _recolors = new int[11]
        {
            0, 0, 0, 0, 0, 2, 1, 0, 0, 0, 0
        };

        // todo: this is mostly identical to War Wasp
        protected override void EnemyInitialize()
        {
            int version = (int)_spawner.Data.Fields.S08.EnemyVersion;
            Recolor = _recolors[version];
            Vector3 up = FixParallelVectors(_spawner.FacingVector, Vector3.UnitY);
            SetTransform(_spawner.FacingVector, up, _spawner.Position);
            _movementType = _spawner.Data.Fields.S08.WarWasp.MovementType;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, -0.45f, 0), 1.4f);
            _values = Metadata.Enemy10Values[(int)_spawner.Data.Fields.S08.EnemySubtype];
            _health = _healthMax = _values.HealthMax;
            Metadata.LoadEffectiveness(_values.Effectiveness, BeamEffectiveness);
            _scanId = _values.ScanId;
            _homeVolume = CollisionVolume.Move(_spawner.Data.Fields.S08.WarWasp.Volume2, Position);
            _movementVolume = CollisionVolume.Move(_spawner.Data.Fields.S08.WarWasp.Volume1, Position);
            WeaponInfo weapon = Weapons.EnemyWeapons[version];
            weapon.UnchargedDamage = _values.BeamDamage;
            weapon.SplashDamage = _values.SplashDamage;
            _equipInfo = new EquipInfo(weapon, _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _shotTimer = 30 * 2; // todo: FPS stuff
            SetUpModel(Metadata.EnemyModelNames[10], animIndex: 1);
            _stepDistance = Fixed.ToFloat(_values.StepDistance1);
            _initialPos = Position;
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
                _maxMoveIndex = (byte)(_spawner.Data.Fields.S08.WarWasp.PositionCount - 1);
                _finalMoveIndex = _maxMoveIndex;
                for (int i = 0; i < 16; i++)
                {
                    _movePositions[i] = _spawner.Data.Fields.S08.WarWasp.MovementVectors[i].ToFloatVector() + Position;
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
            _stepDistance = step / 2; // todo: FPS stuff
            float distance = travel.Length;
            _stepCount = (int)(distance / _stepDistance) + 1;
            if (distance == 0)
            {
                _speed = Vector3.Zero;
            }
            else
            {
                _speed = travel * (_stepDistance / distance);
            }
        }

        private void StartMovingTowardPosition()
        {
            _moveTarget = _movePositions[_moveIndex];
            StartMovingToward(_moveTarget, Fixed.ToFloat(_values.StepDistance1));
            SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
        }

        // todo: this is identical to War Wasp except for the increment
        private void MoveInCircle()
        {
            if (_movementType == 0)
            {
                _circleAngle += Fixed.ToFloat(_values.CircleIncrement) / 2; // todo: FPS stuff
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
            if (HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true) && _state1 == 3)
            {
                _state2 = 5;
                _subId = _state2;
                StartMovingToward(_moveTarget, Fixed.ToFloat(_values.StepDistance3));
                SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
                if (_models[0].AnimInfo.Index[0] == 2)
                {
                    _models[0].SetAnimation(1);
                }
            }
            ContactDamagePlayer(_values.ContactDamage, knockback: false);
            _soundSource.PlaySfx(SfxId.WASP_IDLE, loop: true);
            CallStateProcess();
        }

        // todo: identical to Warp Wasp
        private void State0()
        {
            MoveInCircle();
            if (Position != _moveTarget && _movementType != 0)
            {
                SetTransform((_moveTarget - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy10Subroutines, this);
        }

        // todo: identical to Warp Wasp
        private void State1()
        {
            Vector3 playerPos = PlayerEntity.Main.Position;
            if (Position != playerPos)
            {
                SetTransform((playerPos - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy10Subroutines, this);
        }

        private void State2()
        {
            State1();
        }

        private void State3()
        {
            if (_shotCount > 0 && _shotTimer == 10 * 2) // todo: FPS stuff
            {
                _equipInfo.Weapon.UnchargedDamage = _values.BeamDamage;
                _equipInfo.Weapon.SplashDamage = _values.SplashDamage;
                _equipInfo.Weapon.HeadshotDamage = _values.BeamDamage;
                Vector3 spawnPos = Position.AddY(-0.5f);
                BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, _aimVector, BeamSpawnFlags.None, _scene);
                _shotCount--;
                PlayBeamShotSfx();
            }
            else if (_shotTimer == 15 * 2) // todo: FPS stuff
            {
                _models[0].SetAnimation(2, AnimFlags.None | AnimFlags.Reverse);
                _models[0].AnimInfo.Frame[0] = 15;
            }
            _shotTimer--;
            CallSubroutine(Metadata.Enemy10Subroutines, this);
        }

        // the game shares this function with the player, but we don't need all the logic
        private void PlayBeamShotSfx()
        {
            int sfx = Metadata.BeamSfx[(int)_equipInfo.Weapon.Beam, (int)BeamSfx.Shot];
            if (sfx != -1)
            {
                _soundSource.PlaySfx(sfx);
            }
        }

        private void State4()
        {
            if (Position != _moveTarget)
            {
                SetTransform((_moveTarget - Position).Normalized(), Vector3.UnitY, Position);
            }
            CallSubroutine(Metadata.Enemy10Subroutines, this);
        }

        private void State5()
        {
            CallSubroutine(Metadata.Enemy10Subroutines, this);
        }

        private bool Behavior00()
        {
            if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                _aimVector = (PlayerEntity.Main.Position.AddY(0.5f) - Position).Normalized();
                _models[0].SetAnimation(2, AnimFlags.NoLoop);
                return true;
            }
            return false;
        }

        // todo: same as War Wasp Behavior02 except for the state comparison and replacement value in StartMovingToward
        private bool Behavior01()
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
                StartMovingToward(_moveTarget, _state1 == 4 ? Fixed.ToFloat(_values.StepDistance1) : _stepDistance);
                SetTransform(_speed.Normalized(), Vector3.UnitY, Position);
            }
            return true;
        }

        // todo: same as War Wasp Behavior01 except it doesn't have _attackDelay
        private bool Behavior02()
        {
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

        // todo: same as War Wasp Behavior03 except for the step distance
        private bool Behavior03()
        {
            if (_movementType == 3 || !_homeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _finalMoveIndex = _moveIndex;
            _nextPattern = _pattern = 2;
            _stepDistance = Fixed.ToFloat(_values.StepDistance2);
            return true;
        }

        private bool Behavior04()
        {
            if (_shotCount > 0 || _shotTimer > 0)
            {
                return false;
            }
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _shotTimer = 30 * 2; // todo: FPS stuff
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

        private bool Behavior05()
        {
            if (_shotCount == 0 || _shotTimer > 0)
            {
                return false;
            }
            _shotTimer = 30 * 2; // todo: FPS stuff
            _models[0].SetAnimation(0, AnimFlags.NoLoop);
            return true;
        }

        // todo: same as War Wasp Behavior06 except for setting the animation
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
            _models[0].SetAnimation(0, AnimFlags.NoLoop);
            return true;
        }

        // todo: same as War Wasp Behavior07
        private bool Behavior07()
        {
            if (_homeVolume.TestPoint(Position))
            {
                return false;
            }
            ReachTargetOrReversePattern();
            return true;
        }

        // todo: same as War Wasp Behavior08    
        private bool Behavior08()
        {
            if (_homeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            ReversePattern();
            return true;
        }

        // todo: same as War Wasp Behavior10
        private bool Behavior09()
        {
            CollisionResult res = default;
            if (!CollisionDetection.CheckBetweenPoints(Position, PlayerEntity.Main.Position, TestFlags.None, _scene, ref res))
            {
                return false;
            }
            ReachTargetOrReversePattern();
            return true;
        }

        // todo: same as War Wasp
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

        // todo: same as War Wasp
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

        #region Boilerplate

        public static bool Behavior00(Enemy10Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy10Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy10Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy10Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy10Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy10Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy10Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy10Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy10Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy10Entity enemy)
        {
            return enemy.Behavior09();
        }

        #endregion
    }

    public struct Enemy10Values
    {
        public ushort HealthMax { get; set; }
        public ushort BeamDamage { get; set; }
        public ushort SplashDamage { get; set; }
        public ushort ContactDamage { get; set; }
        public int StepDistance1 { get; set; }
        public int StepDistance2 { get; set; }
        public int StepDistance3 { get; set; }
        public int CircleIncrement { get; set; }
        public int Unknown18 { get; set; } // functionless
        public short MinShots { get; set; }
        public short MaxShots { get; set; }
        public int ScanId { get; set; }
        public int Effectiveness { get; set; }
    }
}
