using System;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy11Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private CollisionVolume _volume1;
        private CollisionVolume _volume2;
        private Vector3 _targetPos;
        private int _moveTimer = 0;
        private EffectEntry? _effect = null;

        public Enemy11Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[5]
            {
                State0, State1, State2, State3, State4
            };
        }

        protected override bool EnemyInitialize()
        {
            _health = _healthMax = 12;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            Vector3 position = _spawner.Data.Header.Position.ToFloatVector();
            SetTransform((PlayerEntity.Main.Position - position).Normalized(), Vector3.UnitY, position);
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S02.Volume0);
            _volume1 = CollisionVolume.Move(_spawner.Data.Fields.S02.Volume1, position);
            _volume2 = CollisionVolume.Move(_spawner.Data.Fields.S02.Volume2, position);
            _targetPos = position + _spawner.Data.Fields.S02.PathVector.ToFloatVector();
            SetUpModel(Metadata.EnemyModelNames[11], animIndex: 4);
            return true;
        }

        private void Die()
        {
            TakeDamage(100, source: null);
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
        }

        protected override void EnemyProcess()
        {
            Vector3 facing = (PlayerEntity.Main.Position - Position).Normalized();
            SetTransform(facing, Vector3.UnitY, Position);
            if (_effect != null)
            {
                _effect.Transform(Position, Transform);
            }
            if (ContactDamagePlayer(20, knockback: false))
            {
                Die();
            }
            if (_state1 == 4 && HandleBlockingCollision(Position, _hurtVolume, updateSpeed: true))
            {
                Die();
            }
            CallStateProcess();
        }

        // todo: really no need for these to be separate functions
        private void State0()
        {
            CallSubroutine(Metadata.Enemy11Subroutines, this);
        }

        private void State1()
        {
            State0();
        }

        private void State2()
        {
            State0();
        }

        private void State3()
        {
            State0();
        }

        private void State4()
        {
            State0();
        }

        // attacking
        private bool Behavior00()
        {
            return false;
        }

        // start attack (moving toward player)
        private bool Behavior01()
        {
            if (_moveTimer > 0)
            {
                _moveTimer--;
                return false;
            }
            Vector3 target = -PlayerEntity.Main.FacingVector + PlayerEntity.Main.Position;
            target.Y = PlayerEntity.Main.Position.Y + 0.5f;
            _speed = target - Position;
            float mag = _speed.Length;
            // _moveTimer is not used again after this
            _moveTimer = (int)(mag / 0.6f) + 1;
            _moveTimer *= 2; // todo: FPS stuff
            _speed *= 0.6f / mag;
            _speed /= 2; // todo: FPS stuff
            // todo: play SFX
            _models[0].SetAnimation(1);
            return true;
        }

        // pause before attack
        private bool Behavior02()
        {
            if (_moveTimer > 0)
            {
                _moveTimer--;
                return false;
            }
            _models[0].SetAnimation(3);
            _moveTimer = 20 * 2; // todo: FPS stuff
            _speed = Vector3.Zero;
            return true;
        }

        // start moving down to starting position for attack
        private bool Behavior03()
        {
            if (!_volume2.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _effect = _scene.SpawnEffectGetEntry(29, Vector3.UnitX, Vector3.UnitY, Position); // shriekBatTrail
            _effect.SetElementExtension(true);
            _speed = _targetPos - Position;
            float mag = _speed.Length;
            _moveTimer = (int)(mag / 0.3f) + 1;
            _moveTimer *= 2; // todo: FPS stuff
            _speed *= 0.3f / mag;
            _speed /= 2; // todo: FPS stuff
            _models[0].SetAnimation(2);
            // todo: play SFX
            return true;
        }

        // wait for player to be in range
        private bool Behavior04()
        {
            if (!_volume1.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _models[0].SetAnimation(0);
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy11Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy11Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy11Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy11Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy11Entity enemy)
        {
            return enemy.Behavior04();
        }

        #endregion
    }
}
