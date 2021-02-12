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
                inst.Active = true;
                inst.SetNodeAnim(-1);
                _models.Add(inst);
                inst = Read.GetModelInstance(name);
                inst.Active = true;
                inst.SetNodeAnim(-1);
                _models.Add(inst);
                inst = Read.GetModelInstance(name);
                inst.Active = true;
                inst.SetNodeAnim(-1);
                _models.Add(inst);
                Matrix4 transform = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(31.640625f))
                    * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(29.61914f));
                transform.Row3.Xyz = new Vector3(Fixed.ToFloat(7208), 0, Fixed.ToFloat(2375));
                _artifact1Transform = transform;
                _artifact2Transform = _artifact1Transform * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(119.9707f));
                _artifact3Transform = _artifact1Transform * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(239.9414f));
            }
            if (multiplayer)
            {
                AddPlaceholderModel();
            }
        }

        public override void Process(Scene scene)
        {
            // sktodo: set artifacts active based on state
            if (_data.ArtifactId < 8)
            {

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
                inst.Model.Nodes[0].Animation = Matrix4.Identity;
                return _artifact1Transform * _transform;
            }
            else if (index == 2)
            {
                // having the second transform is affecting where the second instance ends up???
                // --> because we're not calling updatetransforms between these calls?
                inst.Model.Nodes[0].Animation = Matrix4.Identity;
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
