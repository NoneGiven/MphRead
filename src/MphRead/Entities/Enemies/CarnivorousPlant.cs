namespace MphRead.Entities.Enemies
{
    public class Enemy51Entity : EnemyInstanceEntity
    {
        public Enemy51Entity(EnemyInstanceEntityData data) : base(data)
        {
            Transform = data.Spawner.Transform;
            _initialPosition = Position;
            var spawner = (EnemySpawnEntity)data.Spawner;
            ObjectMetadata meta = Metadata.GetObjectById(spawner.Data.Fields.S07.EnemySubtype);
            // todo: enemy spawners need to load these initially
            SetUpModel(meta.Name);
        }
    }
}
