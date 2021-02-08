using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class EnemySpawnEntity : VisibleEntityBase
    {
        private readonly EnemyEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        public EnemySpawnEntity(EnemyEntityData data) : base(NewEntityType.Enemy)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            // ntodo: enemy entities
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
                UsePlaceholderModel();
            }
        }
    }

    public class FhEnemySpawnEntity : VisibleEntityBase
    {
        private readonly FhEnemyEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        public FhEnemySpawnEntity(FhEnemyEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
