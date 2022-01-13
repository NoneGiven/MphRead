using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy38Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private CollisionVolume _volume1;
        private CollisionVolume _volume2;
        private Vector3 _initalPos;
        private Vector3 _initalFacing;
        private Vector3 _targetVec;

        private ushort _jumpTimer = 0; // for becoming vulnerable
        private ushort _delayTimer = 0;
        private float _jumpHeight = 0;
        private float _aimAngleStep = 0;
        private ushort _aimSteps = 0; // also used as a timer in one instance

        public Enemy38Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[17]
            {
                State00, State01, State02, State03, State04, State05, State06, State07,
                State08, State09, State10, State11, State12, State13, State14, State15, State16
            };
        }

        protected override bool EnemyInitialize()
        {
            Vector3 facing = _spawner.FacingVector;
            SetTransform(facing, Vector3.UnitY, _spawner.Position);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 0.5f;
            _health = _healthMax = 150;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S00.Volume0);
            _volume1 = CollisionVolume.Move(_spawner.Data.Fields.S00.Volume2, Position);
            _volume2 = CollisionVolume.Move(_spawner.Data.Fields.S00.Volume1, Position);
            _jumpTimer = 5 * 2; // todo: FPS stuff
            _delayTimer = 15 * 2; // todo: FPS stuff
            _aimSteps = 8 * 2; // todo: FPS stuff
            _targetVec = Vector3.UnitX;
            _initalPos = Position;
            _initalFacing = facing;
            SetUpModel(Metadata.EnemyModelNames[38], animIndex: 1);
            return true;
        }

        protected override void EnemyProcess()
        {
            if (_state1 == 3 || _state1 == 4)
            {
                Vector3 facing = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
                SetTransform(facing, Vector3.UnitY, Position);
            }
            if (_state1 == 7 || _state1 == 8)
            {
                _speed.Y -= 0.02f / 4; // todo: FPS stuff
            }
            else if (_state1 != 9 && _state1 != 10)
            {
                _speed.Y -= Fixed.ToFloat(100) / 4; // todo: FPS stuff
            }
            if (_state1 != 7 && _state1 != 10)
            {
                HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true);
            }
            ContactDamagePlayer(_state1 == 10 ? 40u : 10u, knockback: true);
            CallStateProcess();
        }

        private void State00()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State01()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State02()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State03()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        // sktodo: function name
        private void DoThing()
        {
            int frame = _models[0].AnimInfo.Frame[0];
            if (frame >= 17)
            {
                _speed.X = 0;
                _speed.Z = 0;
            }
            else
            {
                if (frame == 12 && _scene.FrameCount % 2 == 0)
                {
                    // todo: set camera shake
                }
                Vector3 facing = FacingVector;
                // todo: FPS stuff
                _speed.X = facing.X * 0.1465f / 2;
                _speed.Z = facing.Z * 0.1465f / 2;
            }
        }

        private void State04()
        {
            DoThing();
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State05()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State06()
        {
            if (_delayTimer == 10 * 2) // todo: FPS stuff
            {
                _models[0].SetAnimation(6, AnimFlags.NoLoop);
            }
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State07()
        {
            AnimationInfo animInfo = _models[0].AnimInfo;
            if (animInfo.Index[0] == 6 && animInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                _models[0].SetAnimation(4);
            }
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State08()
        {
            State07();
        }

        private void State09()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State10()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State11()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void FaceInitialPosition()
        {
            if (MathF.Abs(Position.X - _initalPos.X) >= 1 / 4096f || MathF.Abs(Position.Z - _initalPos.Z) >= 1 / 4096f)
            {
                Vector3 facing = (_initalPos - Position).WithY(FacingVector.Y).Normalized();
                SetTransform(facing, Vector3.UnitY, Position);
            }
        }

        private void State12()
        {
            Vector3 playerPos = PlayerEntity.Main.Position;
            if (!_volume2.TestPoint(playerPos) || !_volume1.TestPoint(playerPos))
            {
                FaceInitialPosition();
            }
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State13()
        {
            FaceInitialPosition();
            DoThing();
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State14()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State15()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private void State16()
        {
            CallSubroutine(Metadata.Enemy38Subroutines, this);
        }

        private bool Behavior00()
        {
            if (_aimSteps > 0)
            {
                _aimSteps--;
                return false;
            }
            _speed.Y = Fixed.ToFloat(-3000) / 2; // todo: FPS stuff
            _aimSteps = 8 * 2; // todo: FPS stuff
            _models[0].SetAnimation(3);
            return true;
        }

        private bool Behavior01()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _targetVec = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _aimSteps = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _aimSteps;
            _models[0].SetAnimation(7, AnimFlags.NoLoop);
            return true;
        }

        private bool Behavior02()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _models[0].SetAnimation(1);
            return true;
        }

        private bool Behavior03()
        {
            return _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended);
        }

        private bool Behavior04()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _aimSteps, _aimAngleStep))
            {
                return false;
            }
            _models[0].SetAnimation(0, AnimFlags.NoLoop | AnimFlags.Reverse);
            // todo: play SFX
            return true;
        }

        private bool Behavior05()
        {
            if (!_volume1.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _models[0].SetAnimation(0, AnimFlags.NoLoop);
            // todo: play SFX
            return true;
        }

        private bool Behavior06()
        {
            if (!HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true))
            {
                return false;
            }
            _speed = Vector3.Zero;
            Flags |= EnemyFlags.Invincible;
            // todo: set camera shake
            _models[0].SetAnimation(5, AnimFlags.NoLoop);
            return true;
        }

        private bool Behavior07()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _aimSteps, _aimAngleStep))
            {
                return false;
            }
            // todo: play SFX
            return true;
        }

        // mid-jump
        private bool Behavior08()
        {
            if (Position.Y < _jumpHeight)
            {
                return false;
            }
            _speed = Vector3.Zero;
            return true;
        }

        // early in jump
        private bool Behavior09()
        {
            if (_jumpTimer > 0)
            {
                _jumpTimer--;
                return false;
            }
            Flags &= ~EnemyFlags.Invincible;
            _jumpTimer = 5 * 2; // todo: FPS stuff
            return true;
        }

        // initiate jump
        private bool Behavior10()
        {
            if (_delayTimer > 0)
            {
                _delayTimer--;
                return false;
            }
            Vector3 between = PlayerEntity.Main.Position - Position;
            float factor = MathF.Sqrt(Fixed.ToFloat(80) / Fixed.ToFloat(22937));
            _speed = new Vector3(
                between.X * factor,
                MathF.Sqrt(Fixed.ToFloat(448)),
                between.Z * factor
            );
            _speed /= 2; // todo: FPS stuff
            _jumpHeight = Position.Y + 2.8f;
            _delayTimer = 15 * 2; // todo: FPS stuff
            _aimSteps = 8 * 2; // todo: FPS stuff
            // todo: play SFX
            return true;
        }

        private bool Behavior11()
        {
            return SeekTargetFacing(_targetVec, Vector3.UnitY, ref _aimSteps, _aimAngleStep);
        }

        private bool Behavior12()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _models[0].SetAnimation(2, AnimFlags.NoLoop);
            // todo: play SFX
            return true;
        }

        private bool Behavior13()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _aimSteps, _aimAngleStep))
            {
                return false;
            }
            _models[0].SetAnimation(2, AnimFlags.NoLoop);
            // todo: play SFX
            return true;
        }

        private bool Behavior14()
        {
            if (_volume2.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _speed = Vector3.Zero;
            _targetVec = (_initalPos - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _aimSteps = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _aimSteps;
            _models[0].SetAnimation(7, AnimFlags.NoLoop);
            return true;
        }

        private bool Behavior15()
        {
            Vector3 between = Position - PlayerEntity.Main.Position;
            if (between.LengthSquared >= 5 * 5)
            {
                return false;
            }
            _delayTimer = 40 * 2; // todo: FPS stuff
            return true;
        }

        private bool Behavior16()
        {
            if (_delayTimer > 0)
            {
                _delayTimer--;
                return false;
            }
            _delayTimer = 15 * 2; // todo: FPS stuff
            _models[0].SetAnimation(2, AnimFlags.NoLoop);
            // todo: play SFX
            return true;
        }

        // reached home point, start turning to original orientation
        private bool Behavior17()
        {
            Vector3 between = Position - _initalPos;
            if (between.LengthSquared >= 1)
            {
                return false;
            }
            _targetVec = _initalFacing;
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _aimSteps = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _aimSteps;
            return true;
        }

        private bool Behavior18()
        {
            Vector3 playerPos = PlayerEntity.Main.Position;
            if (!_volume2.TestPoint(playerPos) || !_volume1.TestPoint(playerPos))
            {
                return false;
            }
            _targetVec = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _aimSteps = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _aimSteps;
            _models[0].SetAnimation(7, AnimFlags.NoLoop);
            _delayTimer = 15 * 2; // todo: FPS stuff
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy38Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy38Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy38Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy38Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy38Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy38Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy38Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy38Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy38Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy38Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy38Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy38Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy38Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy38Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy38Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy38Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy38Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy38Entity enemy)
        {
            return enemy.Behavior17();
        }

        public static bool Behavior18(Enemy38Entity enemy)
        {
            return enemy.Behavior18();
        }

        #endregion
    }
}
