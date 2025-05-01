using MphRead.Formats.Culling;

namespace MphRead.Entities.Enemies
{
    public class Enemy32Entity : GoreaEnemyEntityBase
    {
        public Enemy32Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
        }
    }
}
