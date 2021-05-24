using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ItemSpawnEntity : EntityBase
    {
        private readonly ItemSpawnEntityData _data;
        private bool _enabled;
        private bool _spawn = true;

        public ItemSpawnEntityData Data => _data;

        // used if there is no base model
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xC8, 0x00, 0xC8).AsVector4();

        // todo: preload items and effects (including for enemies and platforms)
        public ItemSpawnEntity(ItemSpawnEntityData data) : base(EntityType.ItemSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _enabled = data.Enabled != 0;
            if (data.HasBase != 0)
            {
                SetUpModel("items_base");
            }
            else
            {
                AddPlaceholderModel();
            }
        }

        public override bool Process(Scene scene)
        {
            // todo: item spawning logic
            if (_enabled && _spawn)
            {
                ItemInstanceEntity item = SpawnItem(Position, _data.ItemType);
                scene.AddEntity(item);
                _spawn = false;
            }
            return base.Process(scene);
        }

        // todo: entity node ref
        public static ItemInstanceEntity SpawnItem(Vector3 position, ItemType itemType)
        {
            return new ItemInstanceEntity(new ItemInstanceEntityData(position, itemType));
        }
    }

    public class FhItemSpawnEntity : EntityBase
    {
        private readonly FhItemSpawnEntityData _data;
        private bool _spawn = true;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xC8, 0x00, 0xC8).AsVector4();

        public FhItemSpawnEntity(FhItemSpawnEntityData data) : base(EntityType.ItemSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
        }

        public override bool Process(Scene scene)
        {
            // todo: FH item spawning logic
            if (_spawn)
            {
                FhItemEntity item = SpawnItem(Position, _data.ItemType);
                scene.AddEntity(item);
                _spawn = false;
            }
            return base.Process(scene);
        }

        // todo: FH entity node ref
        public static FhItemEntity SpawnItem(Vector3 position, FhItemType itemType)
        {
            return new FhItemEntity(new FhItemInstanceEntityData(position, itemType));
        }
    }
}
