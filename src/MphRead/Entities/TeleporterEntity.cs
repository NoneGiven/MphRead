using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TeleporterEntity : EntityBase
    {
        private readonly TeleporterEntityData _data;
        private readonly Vector3 _targetPos = Vector3.Zero;
        private readonly Matrix4 _artifact1Transform;
        private readonly Matrix4 _artifact2Transform;
        private readonly Matrix4 _artifact3Transform;

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
                // todo: use room state/artifact bits/etc. to determine active state
                Recolor = multiplayer ? 0 : areaId;
                string modelName;
                if (data.ArtifactId >= 8)
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
            // 0-7 = big teleporter using the corresponding artifact model
            // 8, 10, 11, 255 = small teleporter (no apparent meaning to each value beyond that)
            if (data.ArtifactId < 8)
            {
                string name = $"Artifact0{data.ArtifactId + 1}";
                ModelInstance inst = Read.GetModelInstance(name);
                inst.Active = false;
                inst.SetNodeAnim(-1);
                inst.SetTexcoordAnim(-1);
                _models.Add(inst);
                inst = Read.GetModelInstance(name);
                inst.Active = false;
                inst.SetNodeAnim(-1);
                inst.SetTexcoordAnim(-1);
                _models.Add(inst);
                inst = Read.GetModelInstance(name);
                inst.Active = false;
                inst.SetNodeAnim(-1);
                inst.SetTexcoordAnim(-1);
                _models.Add(inst);
                float angleY = MathHelper.DegreesToRadians(337 * (360 / 4096f));
                float angleZ = MathHelper.DegreesToRadians(360 * (360 / 4096f));
                Matrix4 transform = Matrix4.CreateRotationY(angleY) * Matrix4.CreateRotationZ(angleZ);
                transform.Row3.Xyz = new Vector3(Fixed.ToFloat(7208), Fixed.ToFloat(2375), 0);
                _artifact1Transform = transform;
                angleY = MathHelper.DegreesToRadians(1365 * (360 / 4096f));
                _artifact2Transform = _artifact1Transform * Matrix4.CreateRotationY(angleY);
                angleY = MathHelper.DegreesToRadians(2730 * (360 / 4096f));
                _artifact3Transform = _artifact1Transform * Matrix4.CreateRotationY(angleY);
            }
            if (multiplayer)
            {
                AddPlaceholderModel();
            }
        }

        public override void Process(Scene scene)
        {
            // todo: set artifacts active based on state
            if (_data.ArtifactId < 8)
            {
                _models[1].Active = true;
                _models[2].Active = true;
                _models[3].Active = true;
            }
            base.Process(scene);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index != 0 && inst.IsPlaceholder)
            {
                transform.Row3.Xyz = _targetPos;
            }
            else if (index == 1)
            {
                return _artifact1Transform * _transform;
            }
            else if (index == 2)
            {
                return _artifact2Transform * _transform;
            }
            else if (index == 3)
            {
                return _artifact3Transform * _transform;
            }
            return transform;
        }

        protected override Vector4? GetOverrideColor(ModelInstance inst, int index)
        {
            if (index != 0 && inst.IsPlaceholder)
            {
                return _overrideColor2;
            }
            return base.GetOverrideColor(inst, index);
        }

        protected override int GetModelRecolor(ModelInstance inst, int index)
        {
            if (index != 0)
            {
                return 0;
            }
            return base.GetModelRecolor(inst, index);
        }
    }
}
