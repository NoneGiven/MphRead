using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ItemSpawnEntity : EntityBase
    {
        private readonly ItemEntityData _data;
        private bool _enabled;
        private bool _spawn = true;

        // used if there is no base model
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xC8, 0x00, 0xC8).AsVector4();

        public ItemSpawnEntity(ItemEntityData data) : base(EntityType.ItemSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _enabled = data.Enabled != 0;
            if (data.HasBase != 0)
            {
                _models.Add(Read.GetModelInstance("items_base"));
            }
            else
            {
                AddPlaceholderModel();
            }
        }

        public override void Process(Scene scene)
        {
            // todo: item spawning logic
            if (_enabled && _spawn)
            {
                ItemEntity item = SpawnItem(Position, (int)_data.ModelId);
                scene.AddEntity(item);
                _spawn = false;
            }
            base.Process(scene);
        }

        // todo: entity node ref
        public static ItemEntity SpawnItem(Vector3 position, int itemType)
        {
            return new ItemEntity(new ItemInstanceEntityData(position, itemType));
        }
    }

    public class FhItemSpawnEntity : EntityBase
    {
        private readonly FhItemEntityData _data;
        private bool _spawn = true;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xC8, 0x00, 0xC8).AsVector4();

        public FhItemSpawnEntity(FhItemEntityData data) : base(EntityType.ItemSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
        }

        public override void Process(Scene scene)
        {
            // todo: FH item spawning logic
            if (_spawn)
            {
                FhItemEntity item = SpawnItem(Position, (int)_data.ModelId);
                scene.AddEntity(item);
                _spawn = false;
            }
            base.Process(scene);
        }

        // todo: FH entity node ref
        public static FhItemEntity SpawnItem(Vector3 position, int itemType)
        {
            return new FhItemEntity(new ItemInstanceEntityData(position, itemType));
        }
    }
}
