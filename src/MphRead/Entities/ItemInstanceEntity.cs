using MphRead.Effects;
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

    public class ItemInstanceEntity : SpinningEntityBase
    {
        public int ItemType { get; }
        private EffectEntry? _effectEntry = null;
        private const int _effectId = 144; // artifactKeyEffect

        public ItemInstanceEntity(ItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            Position = data.Position.AddY(0.65f);
            ModelInstance inst = Read.GetModelInstance(Metadata.Items[data.ItemType]);
            _models.Add(inst);
            ItemType = data.ItemType;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (ItemType == 19) // Artifact_Key
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

    public class FhItemEntity : SpinningEntityBase
    {
        public FhItemEntity(ItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            // note: the actual height at creation is 1.0f greater than the spawner's,
            // but 0.5f is subtracted when drawing (after the floating calculation)
            Position = data.Position.AddY(0.5f);
            ModelInstance inst = Read.GetModelInstance(Metadata.FhItems[data.ItemType], firstHunt: true);
            _models.Add(inst);
        }
    }
}
