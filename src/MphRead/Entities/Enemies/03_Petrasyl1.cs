using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy03Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private float _idleRangeX = 0;
        private float _idleRangeZ = 0;
        private Vector3 _initialPos;
        private Vector3 _idleLimits;
        private Vector3 _field194;
        private Vector3 _field1A0;
        private float _rotYOffset = 0;
        private float _rotYSpeed = 0;
        private ushort _field170 = 0;
        private ushort _field172 = 0;
        private bool _teleportInAtInitial = true;
        private float _angleY = 0;
        private int _field18C = 0; // counter/steps in idle Z range

        public Enemy03Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[3]
            {
                State00, State01, State02
            };
        }

        protected override bool EnemyInitialize()
        {
            _health = _healthMax = 8;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 0.5f;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S03.Volume0);
            SetUpModel(Metadata.EnemyModelNames[3]);
            _models[0].SetAnimation(9, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _models[0].SetAnimation(2, slot: 1, SetFlags.Texcoord);
            _idleRangeX = _spawner.Data.Fields.S03.IdleRange.X.FloatValue;
            _idleRangeZ = _spawner.Data.Fields.S03.IdleRange.Z.FloatValue;
            Vector3 facing = _spawner.Data.Fields.S03.Facing.ToFloatVector().WithY(0).Normalized();
            Vector3 position = _spawner.Data.Fields.S03.Position.ToFloatVector() + _spawner.Data.Header.Position.ToFloatVector();
            position = position.AddY(Fixed.ToFloat(5461));
            _initialPos = position;
            _idleLimits = new Vector3(
                position.X + facing.X * _idleRangeZ - facing.Z * _idleRangeX,
                position.Y,
                position.Z + facing.Z * _idleRangeZ + facing.X * _idleRangeX
            );
            facing.X *= -1;
            facing.Z *= -1;
            SetTransform(facing, Vector3.UnitY, position);
            _field194 = facing;
            _field1A0 = facing;
            _rotYOffset = Fixed.ToFloat(Rng.GetRandomInt2(0x1800) + 2048) / 2; // [0.25, 1)
            _rotYSpeed = Fixed.ToFloat(Rng.GetRandomInt2(0x6000)) + 1; // [1, 7)
            _field170 = _field172 = 20 * 2; // todo: FPS stuff
            UpdateState();
            return true;
        }

        private void UpdateState()
        {
            if (_state2 == 0)
            {
                _models[0].SetAnimation(3, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                if (_teleportInAtInitial)
                {
                    Position = new Vector3(_initialPos.X, Position.Y, _initialPos.Z);
                    _teleportInAtInitial = false;
                }
                else
                {
                    Position = new Vector3(_idleLimits.X, Position.Y, _idleLimits.Z);
                    _teleportInAtInitial = true;
                }
                _field194 = (-_field194).Normalized();
                SetTransform(_field194, UpVector, Position);
                _field170 = 20 * 2; // todo: FPS stuff
                // todo: play SFX
            }
            else if (_state2 == 1)
            {
                _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                Flags &= ~EnemyFlags.NoHomingNc;
                Flags &= ~EnemyFlags.Invincible;
                _field18C = (int)(_idleRangeZ / 0.7f) * 2; // todo: FPS stuff
                _speed = (_field194 * 0.7f).WithY(0);
                _speed /= 2; // todo: FPS stuff
            }
            else if (_state2 == 2)
            {
                _models[0].SetAnimation(4, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                Flags |= EnemyFlags.NoHomingNc;
                Flags |= EnemyFlags.Invincible;
                _speed = Vector3.Zero;
                _field172 = 20 * 2; // todo: FPS stuff
                // todo: play SFX
            }
        }

        protected override void EnemyProcess()
        {
            CallStateProcess();
        }

        private void State00()
        {
            if (CallSubroutine(Metadata.Enemy03Subroutines, this))
            {
                UpdateState();
            }
        }

        private void State01()
        {
            // todo: play SFX
            _angleY += _rotYSpeed / 2; // todo: FPS stuff
            if (_angleY >= 360)
            {
                _angleY -= 360;
            }
            float sin = MathF.Sin(MathHelper.DegreesToRadians(_angleY));
            _speed.Y = _initialPos.Y + sin * _rotYOffset - Position.Y;
            _speed.Y /= 2; // todo: FPS stuff
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(12, DamageFlags.None, FacingVector, this);
            }
            Vector3 between = PlayerEntity.Main.Position - Position;
            if (between.LengthSquared >= 7 * 7)
            {
                _field1A0 = _field194.WithY(0).Normalized();
            }
            else
            {
                _field1A0 = between.WithY(0).Normalized();
            }
            Vector3 prevFacing = FacingVector;
            Vector3 newFacing = prevFacing;
            // todo: FPS stuff
            newFacing.X += (_field1A0.X - prevFacing.X) / 8 / 2;
            newFacing.Z += (_field1A0.Z - prevFacing.Z) / 8 / 2;
            if (newFacing.X == 0 && newFacing.Z == 0)
            {
                newFacing = prevFacing;
            }
            Debug.Assert(newFacing != Vector3.Zero);
            newFacing = newFacing.Normalized();
            if (MathF.Abs(newFacing.X - prevFacing.X) < 1 / 4096f && MathF.Abs(newFacing.Z - prevFacing.Z) < 1 / 4096f)
            {
                newFacing.X += 0.125f / 2;
                newFacing.Z -= 0.125f / 2;
                if (newFacing.X == 0 && newFacing.Z == 0)
                {
                    newFacing.X += 0.125f / 2;
                    newFacing.Z -= 0.125f / 2;
                }
                newFacing = newFacing.Normalized();
            }
            SetTransform(newFacing, UpVector, Position);
            AnimationInfo animInfo = _models[0].AnimInfo;
            if (animInfo.Index[0] != 0 && animInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            }
            if (CallSubroutine(Metadata.Enemy03Subroutines, this))
            {
                // todo: stop SFX
                UpdateState();
            }
        }

        private void State02()
        {
            State00();
        }

        private bool Behavior00()
        {
            if (_field172 == 0)
            {
                return true;
            }
            _field172--;
            return false;
        }

        private bool Behavior01()
        {
            if (_field18C == 0)
            {
                return true;
            }
            _field18C--;
            return false;
        }

        private bool Behavior02()
        {
            if (_field170 == 0)
            {
                return true;
            }
            _field170--;
            return false;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy03Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy03Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy03Entity enemy)
        {
            return enemy.Behavior02();
        }

        #endregion
    }
}
