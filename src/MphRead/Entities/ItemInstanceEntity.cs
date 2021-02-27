using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    // todo: despawn timer
    public readonly struct ItemInstanceEntityData
    {
        public readonly Vector3 Position;
        public readonly ItemType ItemType;

        public ItemInstanceEntityData(Vector3 position, ItemType type)
        {
            Position = position;
            ItemType = type;
        }
    }

    public class ItemInstanceEntity : SpinningEntityBase
    {
        public ItemType ItemType { get; }
        private EffectEntry? _effectEntry = null;
        private const int _effectId = 144; // artifactKeyEffect

        public ItemInstanceEntity(ItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            Position = data.Position.AddY(0.65f);
            ModelInstance inst = Read.GetModelInstance(Metadata.Items[(int)data.ItemType]);
            _models.Add(inst);
            ItemType = data.ItemType;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (ItemType == ItemType.ArtifactKey)
            {
                scene.LoadEffect(_effectId);
                Matrix4 transform = Matrix.GetTransform4(Vector3.UnitX, Vector3.UnitY, Position);
                _effectEntry = scene.SpawnEffectGetEntry(_effectId, transform);
                for (int i = 0; i < _effectEntry.Elements.Count; i++)
                {
                    EffectElementEntry element = _effectEntry.Elements[i];
                    element.Flags |= EffElemFlags.ElementExtension;
                }
            }
        }

        public override void Destroy(Scene scene)
        {
            if (_effectEntry != null)
            {
                scene.UnlinkEffectEntry(_effectEntry);
            }
        }
    }

    public readonly struct FhItemInstanceEntityData
    {
        public readonly Vector3 Position;
        public readonly FhItemType ItemType;

        public FhItemInstanceEntityData(Vector3 position, FhItemType type)
        {
            Position = position;
            ItemType = type;
        }
    }

    public class FhItemEntity : SpinningEntityBase
    {
        public FhItemEntity(FhItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            // note: the actual height at creation is 1.0f greater than the spawner's,
            // but 0.5f is subtracted when drawing (after the floating calculation)
            Position = data.Position.AddY(0.5f);
            ModelInstance inst = Read.GetModelInstance(Metadata.FhItems[(int)data.ItemType], firstHunt: true);
            _models.Add(inst);
        }
    }
}
