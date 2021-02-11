using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TeleporterEntity : EntityBase
    {
        private readonly TeleporterEntityData _data;
        private readonly Vector3 _targetPos = Vector3.Zero;

        // used for invisible teleporters
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0xFF).AsVector4();
        // used for multiplayer teleporter destination -- todo: confirm 1P doesn't have any intra-room teleporters
        private readonly Vector4 _overrideColor2 = new ColorRgb(0xAA, 0xAA, 0xAA).AsVector4();

        public TeleporterEntity(TeleporterEntityData data, int areaId, bool multiplayer) : base(EntityType.Teleporter)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            if (data.Invisible != 0)
            {
                AddPlaceholderModel();
            }
            else
            {
                Recolor = multiplayer ? 0 : areaId;
                // todo: how to use ArtifactId?
                int flags = data.ArtifactId < 8 && data.Invisible == 0 ? 2 : 0;
                string modelName;
                if ((flags & 2) == 0)
                {
                    modelName = multiplayer ? "TeleporterMP" : "TeleporterSmall";
                }
                else
                {
                    modelName = "Teleporter";
                }
                ModelInstance inst = Read.GetModelInstance(modelName);
                _models.Add(inst);
            }
            if (multiplayer)
            {
                AddPlaceholderModel(); // always at least the second model
            }
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (inst.IsPlaceholder && index != 0)
            {
                transform.Row3.Xyz = _targetPos;
            }
            return transform;
        }

        protected override Vector4? GetOverrideColor(ModelInstance inst, int index)
        {
            if (inst.IsPlaceholder && index != 0)
            {
                return _overrideColor2;
            }
            return base.GetOverrideColor(inst, index);
        }
    }
}
