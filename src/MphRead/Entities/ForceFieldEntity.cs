using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ForceFieldEntity : VisibleEntityBase
    {
        private readonly ForceFieldEntityData _data;
        private bool _lockSpawned = false;

        public ForceFieldEntityData Data => _data;

        public ForceFieldEntity(ForceFieldEntityData data) : base(NewEntityType.ForceField)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            Scale = new Vector3(data.Width.FloatValue, data.Height.FloatValue, 1.0f);
            Recolor = Metadata.DoorPalettes[(int)data.Type];
            ModelInstance inst = Read.GetNewModel("ForceField");
            _models.Add(inst);
            Active = data.Active != 0;
        }

        public override void Process(NewScene scene)
        {
            // todo: despawn when deactivated/destroyed
            if (Active && _data.Type != 9 && !_lockSpawned)
            {
                EnemyEntity? enemy = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock);
                if (enemy != null)
                {
                    scene.AddEntity(enemy);
                    _lockSpawned = true;
                }
            }
            base.Process(scene);
        }
    }
}
