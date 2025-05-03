using System;
using MphRead.Effects;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy33Entity : GoreaEnemyEntityBase
    {
        private Enemy31Entity _gorea2 = null!;
        private EntityBase? _target = null;
        private EffectEntry? _effect = null;
        private Vector3 _effectUp;
        private Vector3 _effectFacing;

        private int _shakeTimer = 0;
        private float _field1A0 = 0;
        private float _field1A4 = 0;
        private float _field1A8 = 0;
        private float _field1AC = 0;
        private Vector3 _basePos;
        private float _field1B0 = 0;
        private int _field1B4 = 0;
        private int _field1B6 = 0;
        private int _field1B8 = 0;
        private int _field1BC = 0;
        private float _field1BE = 0;
        private int  _itemChance1 = 0;
        private int  _itemChance2 = 0;
        private int  _itemChance3 = 0;
        private int  _itemChance4 = 0;
        private int  _field1C4 = 0;
        private bool _flag = false;

        public Enemy33Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            _stateProcesses = new Action[4]
            {
                State00, State01, State02, State03
            };
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy31Entity owner)
            {
                _gorea2 = owner;
                Flags |= EnemyFlags.Visible;
                Flags |= EnemyFlags.NoHomingNc;
                Flags &= ~EnemyFlags.Invincible;
                Flags |= EnemyFlags.CollidePlayer;
                Flags |= EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.NoMaxDistance;
                Flags |= EnemyFlags.OnRadar;
                SetTransform(owner.FacingVector, owner.UpVector, owner.Position);
                _basePos = _prevPos = Position;
                _boundingRadius = 1;
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, 1);
                _health = _healthMax = 8;
                _effectUp = Vector3.UnitY;
                _effectFacing = Vector3.UnitZ;
                _effect = SpawnEffectGetEntry(79, Position, _effectFacing, _effectUp, extensionFlag: true); // goreaMeteor
                _field1A0 = 2;
                _field1A4 = 0.125f;
                _field1A8 = 0.2f;
                _field1B0 = 1;
                _field1B4 = 15;
                _field1B6 = 390 * 2; // todo: FPS stuff
                _field1B8 = 150 * 2; // todo: FPS stuff
                _field1BC = 12;
                _itemChance1 = 40;
                _itemChance4 = 60;
                _field1C4 = 5;
                _flag = true;
                _target = PlayerEntity.Main;
            }
        }

        public void UpdatePosition(Vector3 position)
        {
            _basePos = _prevPos = Position = position;
        }

        private void State00()
        {
            // skhere
        }

        private void State01()
        {
            // skhere
        }

        private void State02()
        {
            // skhere
        }

        private void State03()
        {
            // skhere
        }

        private bool Behavior00()
        {
            return true; // skhere
        }

        private bool Behavior01()
        {
            return true; // skhere
        }

        private bool Behavior02()
        {
            return true; // skhere
        }

        private bool Behavior03()
        {
            return true; // skhere
        }

        #region Boilerplate

        public static bool Behavior00(Enemy33Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy33Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy33Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy33Entity enemy)
        {
            return enemy.Behavior03();
        }

        #endregion
    }
}
