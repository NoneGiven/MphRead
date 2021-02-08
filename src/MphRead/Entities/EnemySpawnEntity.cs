namespace MphRead.Entities
{
    public class EnemySpawnEntity : VisibleEntityBase
    {
        private readonly EnemyEntityData _data;

        public EnemySpawnEntity(EnemyEntityData data) : base(NewEntityType.Enemy)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            //if (data.Type == EnemyType.CarnivorousPlant)
            //{
            //    ObjectMetadata meta = Metadata.GetObjectById(data.TextureId);
            //    NewModel enemy = Read.GetNewModel(meta.Name); // meta.RecolorId
            //    _models.Add(enemy);
            //}
            if (data.SpawnerModel != 0)
            {
                string spawner = "EnemySpawner";
                if (data.Type == EnemyType.WarWasp || data.Type == EnemyType.BarbedWarWasp)
                {
                    spawner = "PlantCarnivarous_Pod";
                }
                NewModel model = Read.GetNewModel(spawner);
                _models.Add(model);
                // temporary
                if (spawner == "EnemySpawner")
                {
                    model.Animations.NodeGroupId = -1;
                    model.Animations.MaterialGroupId = -1;
                    model.Animations.TexcoordGroupId = 1;
                }
            }
            else
            {
                // mtodo: entity placeholders
            }
        }
    }
}
