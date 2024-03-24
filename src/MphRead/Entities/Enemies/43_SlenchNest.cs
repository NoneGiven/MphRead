using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy43Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;

        public Enemy43Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected override void EnemyInitialize()
        {
            Transform = _spawner.Transform;
            _health = _healthMax = 100;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 2;
            _boundingRadius = 0;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, rad: 0);
            SetUpModel("BigEyeNest");
        }
    }
}
