using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy42Entity : EnemyInstanceEntity
    {
        private readonly Enemy41Entity _slench;
        public Enemy41Entity Slench => _slench;

        public Enemy42Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as Enemy41Entity;
            Debug.Assert(spawner != null);
            _slench = spawner;
        }

        protected override void EnemyInitialize()
        {
            Transform = _slench.Transform;
            _health = _healthMax = 255;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 2;
            _boundingRadius = 0;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, rad: 1);
        }

        protected override void EnemyProcess()
        {
            Vector3 facing = _slench.FacingVector.Normalized();
            Position = _slench.Position + facing * _slench.ShieldOffset;
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (!_slench.SlenchFlags.TestFlag(SlenchFlags.Bit0) && _slench.SlenchFlags.TestFlag(SlenchFlags.Bit8))
            {
                _slench.Flags &= ~EnemyFlags.Invincible;
                _slench.TakeDamage((uint)(_healthMax - _health), source);
                _slench.Flags |= EnemyFlags.Invincible;
            }
            else
            {
                _slench.ShieldTakeDamage(source);
            }
            _health = _healthMax;
            return false;
        }

        public void UpdateScanId(int scanId)
        {
            _scanId = scanId;
        }
    }
}
