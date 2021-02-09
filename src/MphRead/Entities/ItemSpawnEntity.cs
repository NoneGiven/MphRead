using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ItemSpawnEntity : SpinningEntityBase
    {
        private readonly ItemEntityData _data;

        public ItemSpawnEntity(ItemEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, NewEntityType.Item)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            NewModel model = Read.GetNewModel(Metadata.Items[(int)data.ModelId]);
            _models.Add(model);
            if (data.Enabled == 0)
            {
                model.Active = false;
            }
            if (data.HasBase != 0)
            {
                _models.Add(Read.GetNewModel("items_base"));
            }
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            Matrix4 transform = base.GetModelTransform(model, index);
            if (index == 0)
            {
                transform.Row3.Y += 0.65f;
            }
            return transform;
        }
    }

    public class FhItemSpawnEntity : SpinningEntityBase
    {
        private readonly FhItemEntityData _data;

        public FhItemSpawnEntity(FhItemEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, NewEntityType.Item)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            string name = Metadata.FhItems[(int)data.ModelId];
            NewModel model = Read.GetFhNewModel(name);
            _models.Add(model);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            Matrix4 transform = base.GetModelTransform(model, index);
            if (index == 0)
            {
                // note: the actual height at creation is 1.0f greater than the spawner's,
                // but 0.5f is subtracted when drawing (after the floating calculation)
                transform.Row3.Y += 0.5f;
            }
            return transform;
        }
    }
}
