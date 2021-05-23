using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ForceFieldEntity : EntityBase
    {
        private readonly ForceFieldEntityData _data;
        private bool _lockSpawned = false;

        public ForceFieldEntityData Data => _data;

        public ForceFieldEntity(ForceFieldEntityData data) : base(EntityType.ForceField)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            Scale = new Vector3(data.Width.FloatValue, data.Height.FloatValue, 1.0f);
            Recolor = Metadata.DoorPalettes[(int)data.Type];
            SetUpModel("ForceField");
            // todo: fade in/out "animation"
            Active = data.Active != 0;
        }

        public override bool Process(Scene scene)
        {
            // todo: despawn when deactivated/destroyed
            if (Active && _data.Type != 9 && !_lockSpawned)
            {
                EnemyInstanceEntity? enemy = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock);
                if (enemy != null)
                {
                    scene.AddEntity(enemy);
                    _lockSpawned = true;
                }
            }
            return base.Process(scene);
        }
    }
}
