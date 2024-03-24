using System.Diagnostics;
using MphRead.Formats.Culling;

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
    }
}
