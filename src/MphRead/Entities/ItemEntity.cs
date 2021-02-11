using OpenTK.Mathematics;

namespace MphRead.Entities
{
    // todo: despawn timer, enum
    public readonly struct ItemInstanceEntityData
    {
        public readonly Vector3 Position;
        public readonly int ItemType;

        public ItemInstanceEntityData(Vector3 position, int type)
        {
            Position = position;
            ItemType = type;
        }
    }

    public class ItemEntity : SpinningEntityBase
    {
        public ItemEntity(ItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            Position = data.Position.AddY(0.65f);
            ModelInstance inst = Read.GetNewModel(Metadata.Items[data.ItemType]);
            _models.Add(inst);
        }
    }

    public class FhItemEntity : SpinningEntityBase
    {
        public FhItemEntity(ItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            // note: the actual height at creation is 1.0f greater than the spawner's,
            // but 0.5f is subtracted when drawing (after the floating calculation)
            Position = data.Position.AddY(0.5f);
            ModelInstance inst = Read.GetFhNewModel(Metadata.FhItems[data.ItemType]);
            _models.Add(inst);
        }
    }
}
