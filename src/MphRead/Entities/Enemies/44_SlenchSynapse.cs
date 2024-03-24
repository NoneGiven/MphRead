using System.Diagnostics;
using MphRead.Formats.Culling;

namespace MphRead.Entities.Enemies
{
    public class Enemy44Entity : EnemyInstanceEntity
    {
        private readonly Enemy41Entity _slench;

        public Enemy44Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as Enemy41Entity;
            Debug.Assert(spawner != null);
            _slench = spawner;
        }

        public void ChangeState(int state)
        {
            // sktodo
        }
    }
}
