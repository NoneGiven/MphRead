namespace MphRead.Entities.Enemies
{
    public class Enemy51Entity : EnemyEntity
    {
        public Enemy51Entity(EnemyInstanceEntityData data) : base(data)
        {
            ObjectMetadata meta = Metadata.GetObjectById(data.Spawner.Data.TextureId);
            NewModel enemy = Read.GetNewModel(meta.Name);
            _models.Add(enemy);
        }
    }
}
