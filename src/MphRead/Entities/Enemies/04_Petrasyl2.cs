using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy04Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;

        private Vector3 _initialPos;
        private Vector3 _field188;
        private Vector3 _field194;
        private float _weaveOffset = 0;
        private float _bobAngle = 0;
        private float _bobOffset = 0;
        private float _bobSpeed = 0;
        private float _weaveAngle = 0;
        private ushort _field170 = 0;
        private ushort _field172 = 0;

        public Enemy04Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
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
            SetUpModel(Metadata.EnemyModelNames[4]);
            _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _models[0].SetAnimation(2, slot: 1, SetFlags.Texcoord);
            Vector3 facing = _spawner.Data.Header.FacingVector.ToFloatVector().Normalized();
            Vector3 position = _spawner.Data.Fields.S04.Position.ToFloatVector() + _spawner.Data.Header.Position.ToFloatVector();
            position = position.AddY(Fixed.ToFloat(5461)); // 5461
            SetTransform(facing, Vector3.UnitY, position);
            _initialPos = position;
            _weaveOffset = Fixed.ToFloat(_spawner.Data.Fields.S04.WeaveOffset);
            _field188 = facing;
            _field194 = facing;
            _bobOffset = Fixed.ToFloat(Rng.GetRandomInt2(0x1AAB) + 1365) / 2; // [0.1667, 1)
            _bobSpeed = Fixed.ToFloat(Rng.GetRandomInt2(0x6000)) + 1; // [1, 7)
            UpdateState();
            return true;
        }

        private void UpdateState()
        {
            if (_state2 == 0)
            {
                // finish teleporting in
                _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                Flags &= ~EnemyFlags.NoHomingNc;
                Flags &= ~EnemyFlags.Invincible;
            }
            else if (_state2 == 1)
            {
                // begin teleporting in
                _models[0].SetAnimation(5, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                _field170 = 30 * 2; // todo: FPS stuff
                // todo: play SFX
            }
        }

        private void UpdateMovement()
        {
            // todo: play SFX
            _field188 = _spawner.Position - Position;
            _field188 = new Vector3(-_field188.Z, 0, _field188.X);
            if (_field188 != Vector3.Zero)
            {
                _field188 = _field188.Normalized();
            }
            else
            {
                _field188 = FacingVector;
            }
            int animFrame = _models[0].AnimInfo.Frame[0];
            if (_state1 != 0)
            {
                _weaveAngle += 1.5f / 2; // todo: FPS stuff
            }
            else
            {
                _weaveAngle += (1.5f * animFrame + (30 - animFrame)) / 30f / 2; // todo: FPS stuff
            }
            if (_weaveAngle >= 360)
            {
                _weaveAngle -= 360;
            }
            float angle = MathHelper.DegreesToRadians(_weaveAngle);
            float xzSin = MathF.Sin(angle);
            float xzCos = MathF.Cos(angle);
            if (_state1 != 0)
            {
                _speed.X = _initialPos.X + xzSin * _weaveOffset - Position.X;
                _speed.Z = _initialPos.Z + xzCos * _weaveOffset - Position.Z;
            }
            else
            {
                _speed.X = _initialPos.X + xzSin * (_weaveOffset * animFrame / 30) - Position.X;
                _speed.Z = _initialPos.Z + xzCos * (_weaveOffset * animFrame / 30) - Position.Z;
            }
            _bobAngle += _bobSpeed / 2; // todo: FPS stuff
            if (_bobAngle >= 360)
            {
                _bobAngle -= 360;
            }
            float ySin = MathF.Sin(MathHelper.DegreesToRadians(_bobAngle));
            _speed.Y = _initialPos.Y + ySin * _bobOffset - Position.Y;
            _speed /= 2; // todo: FPS stuff
            Vector3 between = PlayerEntity.Main.Position - Position;
            if (between.LengthSquared >= 7 * 7)
            {
                _field194 = _field188.WithY(0).Normalized();
            }
            else
            {
                _field194 = between.WithY(0).Normalized();
            }
            Vector3 prevFacing = FacingVector;
            Vector3 newFacing = prevFacing;
            // todo: FPS stuff
            newFacing.X += (_field194.X - prevFacing.X) / 8 / 2;
            newFacing.Z += (_field194.Z - prevFacing.Z) / 8 / 2;
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
        }

        protected override void EnemyProcess()
        {
            CallStateProcess();
        }

        private void State0()
        {
            UpdateMovement();
            if (CallSubroutine(Metadata.Enemy04Subroutines, this))
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

        public static bool Behavior00(Enemy04Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy04Entity enemy)
        {
            return enemy.Behavior01();
        }

        #endregion
    }
}
