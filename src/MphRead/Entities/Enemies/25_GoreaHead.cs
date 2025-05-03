using MphRead.Effects;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy25Entity : GoreaEnemyEntityBase
    {
        private Node _attachNode = null!;
        private Enemy24Entity _gorea1A = null!;
        public int Damage { get; set; }
        private EffectEntry? _flashEffect = null;

        public Enemy25Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy24Entity owner)
            {
                _gorea1A = owner;
                InitializeCommon(owner.Spawner);
                Flags |= EnemyFlags.Invincible;
                Flags &= ~EnemyFlags.Visible;
                _state1 = _state2 = 255;
                ModelInstance ownerModel = _owner.GetModels()[0];
                _attachNode = ownerModel.Model.GetNodeByName("Head")!;
                Position += _attachNode.Position;
                _prevPos = Position;
                SetTransform(owner.FacingVector, owner.UpVector, Position);
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, Fixed.ToFloat(1314));
            }
        }

        protected override void EnemyProcess()
        {
            Matrix4 transform = GetNodeTransform(_gorea1A, _attachNode);
            Position = transform.Row3.Xyz;
            if (_flashEffect != null)
            {
                if (_flashEffect.IsFinished)
                {
                    RemoveFlashEffect();
                }
                else
                {
                    Vector3 position = Position + _gorea1A.FacingVector * Fixed.ToFloat(2949);
                    position += _gorea1A.UpVector * Fixed.ToFloat(-939);
                    _flashEffect.Transform(_gorea1A.FacingVector, _gorea1A.UpVector, position);
                }
            }
        }

        private void RemoveFlashEffect()
        {
            if (_flashEffect != null)
            {
                _scene.DetachEffectEntry(_flashEffect, setExpired: false);
                _flashEffect = null;
            }
        }

        public void RespawnFlashEffect()
        {
            RemoveFlashEffect();
            Vector3 spawnPos = Position + _gorea1A.FacingVector * Fixed.ToFloat(2949);
            spawnPos += _gorea1A.UpVector * Fixed.ToFloat(-939);
            _flashEffect = SpawnEffectGetEntry(104, spawnPos, extensionFlag: false); // goreaEyeFlash
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            Damage = 65535 - _health;
            _health = 65535;
            return true;
        }
    }
}
