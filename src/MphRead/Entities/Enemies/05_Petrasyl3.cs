using System;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy05Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;

        private Vector3 _initialPos;
        private Vector3 _field184;
        private Vector3 _field190;
        private float _weaveOffset = 0;
        private float _field1B0 = 0; // todo: visualize stuff and also get this field name
        private float _bobAngle = 0;
        private float _bobOffset = 0;
        private float _bobSpeed = 0;
        private ushort _field170 = 0;
        private float _targetY = 0;
        private ushort _field1A0 = 0;

        public Enemy05Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[2]
            {
                State0, State1
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
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S04.Volume0);
            SetUpModel(Metadata.EnemyModelNames[5]);
            _models[0].SetAnimation(9, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _models[0].SetAnimation(2, slot: 1, SetFlags.Texcoord);
            Vector3 facing = _spawner.Data.Header.FacingVector.ToFloatVector().Normalized();
            Vector3 position = _spawner.Data.Fields.S04.Position.ToFloatVector() + _spawner.Data.Header.Position.ToFloatVector();
            position = position.AddY(Fixed.ToFloat(5461));
            SetTransform(facing, Vector3.UnitY, position);
            _initialPos = position;
            _weaveOffset = Fixed.ToFloat(_spawner.Data.Fields.S04.WeaveOffset);
            _field1B0 = Fixed.ToFloat(_spawner.Data.Fields.S04.Field88);
            _field184 = facing;
            _field190 = facing;
            _bobOffset = Fixed.ToFloat(Rng.GetRandomInt2(0x1AAB) + 1365) / 2; // [0.1667, 1)
            _bobSpeed = Fixed.ToFloat(Rng.GetRandomInt2(0x3000)) + 1; // [1, 4)
            _targetY = Position.Y;
            _field170 = 20 * 2; // todo: FPS stuff
            UpdateState();
            return true;
        }

        private void UpdateState()
        {
            if (_state2 == 0)
            {
                // begin teleporting in
                _models[0].SetAnimation(6, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                _field170 = 20 * 2; // todo: FPS stuff
                _soundSource.PlaySfx(SfxId.MOCHTROID_TELEPORT_IN);
            }
            else if (_state2 == 1)
            {
                // finish teleporting in
                _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                Flags &= ~EnemyFlags.NoHomingNc;
                Flags &= ~EnemyFlags.Invincible;
                _field184 = new Vector3(
                    Fixed.ToFloat(Rng.GetRandomInt2(4096) - 2048), // [-0.5, 0.5)
                    Fixed.ToFloat(Rng.GetRandomInt2(4096) - 2048),
                    Fixed.ToFloat(Rng.GetRandomInt2(4096) - 2048)
                );
                if (_field184.X == 0 && _field184.Z == 0)
                {
                    _field184 = FacingVector;
                }
                else
                {
                    _field184 = _field184.Normalized();
                }
                _speed = _field184 * 0.05f;
                _speed /= 2; // todo: FPS stuff
            }
        }

        private void UpdateMovement()
        {
            _soundSource.PlaySfx(SfxId.MOCHTROID_FLY, loop: true);
            var toTarget = new Vector3(Position.X - _initialPos.X, _targetY - _initialPos.Y, Position.Z - _initialPos.Z);
            _bobAngle += _bobSpeed / 2; // todo: FPS stuff
            if (_bobAngle >= 360)
            {
                _targetY = Position.Y;
                _bobAngle -= 360;
            }
            float ySin = MathF.Sin(MathHelper.DegreesToRadians(_bobAngle));
            float ySpeedInc = _targetY + ySin * _bobOffset - Position.Y;
            if (_field1A0 > 0)
            {
                _field1A0--;
            }
            if (_field1A0 == 0)
            {
                if (_targetY >= _initialPos.Y)
                {
                    if (_targetY <= _initialPos.Y + _field1B0)
                    {
                        if (toTarget.X * toTarget.X + toTarget.Z * toTarget.Z <= _weaveOffset * _weaveOffset)
                        {
                            for (int i = 0; i < _scene.Entities.Count; i++)
                            {
                                EntityBase entity = _scene.Entities[i];
                                if (entity.Type != EntityType.EnemyInstance || entity == this)
                                {
                                    continue;
                                }
                                var enemy = (EnemyInstanceEntity)entity;
                                if (enemy.EnemyType != EnemyType.Petrasyl3)
                                {
                                    continue;
                                }
                                CollisionResult discard = default;
                                if (CollisionDetection.CheckVolumesOverlap(_hurtVolume, enemy.HurtVolume, ref discard))
                                {
                                    _field184 = Position - enemy.Position;
                                    _field1A0 = 5 * 2; // todo: FPS stuff
                                    break;
                                }
                            }
                        }
                        else
                        {
                            _field184.X *= -1;
                            _field184.Y = Fixed.ToFloat(Rng.GetRandomInt2(0x1000)) - 0.5f - (toTarget.Y - _field1B0 / 2) / _field1B0;
                            _field184.Z *= -1;
                            _field1A0 = 5 * 2; // todo: FPS stuff
                        }
                    }
                    else
                    {
                        _field184.X = Fixed.ToFloat(Rng.GetRandomInt2(0x1000)) - 0.5f - toTarget.X / _weaveOffset;
                        _field184.Y = -Fixed.ToFloat(Rng.GetRandomInt2(0x800)) - 0.5f;
                        _field184.Z = Fixed.ToFloat(Rng.GetRandomInt2(0x1000)) - 0.5f - toTarget.Z / _weaveOffset;
                        _field1A0 = 5 * 2; // todo: FPS stuff
                    }
                }
                else
                {
                    _field184.X = Fixed.ToFloat(Rng.GetRandomInt2(0x1000)) - 0.5f - toTarget.X / _weaveOffset;
                    _field184.Y = Fixed.ToFloat(Rng.GetRandomInt2(0x800)) + 0.5f;
                    _field184.Z = Fixed.ToFloat(Rng.GetRandomInt2(0x1000)) - 0.5f - toTarget.Z / _weaveOffset;
                    _field1A0 = 5 * 2; // todo: FPS stuff
                }
            }
            Vector3 prevFacing = FacingVector;
            if (_field184.X == 0 && _field184.Y == 0)
            {
                _field184 = prevFacing;
            }
            else
            {
                _field184 = _field184.Normalized();
            }
            Vector3 between = PlayerEntity.Main.Position - Position;
            if (between.LengthSquared >= 2 * 2)
            {
                _field190 = _field184.WithY(0).Normalized();
            }
            else
            {
                _field190 = between.WithY(0).Normalized();
            }
            Vector3 newFacing = prevFacing;
            // todo: FPS stuff
            newFacing.X += (_field190.X - prevFacing.X) / 8 / 2;
            newFacing.Z += (_field190.Z - prevFacing.Z) / 8 / 2;
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
            // todo: FPS stuff
            _speed = _field184 * 0.05f;
            _speed /= 2;
            _targetY += _speed.Y / 2;
            _speed.Y += ySpeedInc / 2;
        }

        protected override void EnemyProcess()
        {
            CallStateProcess();
        }

        private void State0()
        {
            if (CallSubroutine(Metadata.Enemy05Subroutines, this))
            {
                UpdateState();
            }
        }

        private void State1()
        {
            UpdateMovement();
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(12, DamageFlags.None, FacingVector, this);
            }
            AnimationInfo animInfo = _models[0].AnimInfo;
            if (animInfo.Index[0] != 0 && animInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            }
        }

        private bool Behavior00()
        {
            return false;
        }

        private bool Behavior01()
        {
            if (_field170 == 0)
            {
                return true;
            }
            _field170--;
            return false;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy05Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy05Entity enemy)
        {
            return enemy.Behavior01();
        }

        #endregion
    }
}
