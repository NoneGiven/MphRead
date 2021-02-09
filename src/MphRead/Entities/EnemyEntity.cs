using OpenTK.Mathematics;

namespace MphRead.Entities
{
    // size: 52
    public readonly struct EnemyInstanceEntityData
    {
        public readonly EnemyType Type;
        public readonly EnemySpawnEntity Spawner;

        public EnemyInstanceEntityData(EnemyType type, EnemySpawnEntity spawner)
        {
            Type = type;
            Spawner = spawner;
        }
    }

    public class EnemyEntity : VisibleEntityBase
    {
        protected readonly EnemyInstanceEntityData _data;
        protected readonly Vector3 _initialPosition;

        public EnemyEntity(EnemyInstanceEntityData data) : base(NewEntityType.EnemyInstance)
        {
            _data = data;
            Transform = data.Spawner.Transform;
            _initialPosition = Position;
        }
    }
}
