namespace MphRead.Entities.Enemies
{
    public class Enemy51Entity : EnemyInstanceEntity
    {
        public Enemy51Entity(EnemyInstanceEntityData data) : base(data)
        {
            Transform = data.Spawner.Transform;
            _initialPosition = Position;
            var spawner = (EnemySpawnEntity)data.Spawner;
            ObjectMetadata meta = Metadata.GetObjectById(spawner.Data.TextureId);
            // todo: enemy spawners need to load these initially
            ModelInstance inst = Read.GetModelInstance(meta.Name);
            _models.Add(inst);
        }
    }
}
