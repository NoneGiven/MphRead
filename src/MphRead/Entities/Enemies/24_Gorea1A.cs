using System;
using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class GoreaEnemyEntityBase : EnemyInstanceEntity
    {
        protected readonly EnemySpawnEntity _spawner;

        public GoreaEnemyEntityBase(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected void SetUp()
        {
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 3;
            SetTransform(_spawner.FacingVector, Vector3.UnitY, _spawner.Position);
            _boundingRadius = 1;
            _healthMax = _health = UInt16.MaxValue;
        }

        protected void SpawnEffect(int effectId, Vector3 position)
        {
            SpawnEffect(effectId, position, Vector3.UnitX, Vector3.UnitY);
        }

        protected void SpawnEffect(int effectId, Vector3 position, Vector3 facing, Vector3 up)
        {
            _scene.SpawnEffect(effectId, facing, up, position);
        }
    }

    public class Enemy24Entity : EnemyInstanceEntity
    {
        public Enemy24Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
        }
    }
}
