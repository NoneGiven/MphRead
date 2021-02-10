namespace MphRead.Entities.Enemies
{
    public class Enemy51Entity : EnemyEntity
    {
        public Enemy51Entity(EnemyInstanceEntityData data) : base(data)
        {
            Transform = data.Spawner.Transform;
            _initialPosition = Position;
            var spawner = (EnemySpawnEntity)data.Spawner;
            ObjectMetadata meta = Metadata.GetObjectById(spawner.Data.TextureId);
            NewModel enemy = Read.GetNewModel(meta.Name);
            _models.Add(enemy);
        }
    }
}