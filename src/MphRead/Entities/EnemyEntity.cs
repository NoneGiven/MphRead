using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public readonly struct EnemyInstanceEntityData
    {
        public readonly EnemyType Type;
        public readonly EntityBase Spawner;

        public EnemyInstanceEntityData(EnemyType type, EntityBase spawner)
        {
            Type = type;
            Spawner = spawner;
        }
    }

    public class EnemyEntity : VisibleEntityBase
    {
        protected readonly EnemyInstanceEntityData _data;
        protected Vector3 _initialPosition; // todo: use init

        public EnemyEntity(EnemyInstanceEntityData data) : base(EntityType.EnemyInstance)
        {
            _data = data;
        }
    }
}
