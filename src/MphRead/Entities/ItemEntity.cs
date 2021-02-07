using System;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public abstract class SpinningEntityBase : VisibleEntityBase
    {
        private float _spin;
        private readonly float _spinSpeed;
        private readonly Vector3 _spinAxis;
        private readonly int _modelIndex;

        private static ushort _nextItemRotation = 0;

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis,
            int modelIndex, NewEntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _modelIndex = modelIndex;
        }

        public override void Process(NewScene scene)
        {
            _spin = (float)(_spin + scene.FrameTime * 360 * _spinSpeed) % 360;
            base.Process(scene);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            var transform = Matrix4.CreateScale(model.Scale);
            if (index == _modelIndex && model.Animations.NodeGroupId == -1)
            {
                transform *= SceneSetup.ComputeNodeTransforms(Vector3.One, new Vector3(
                    MathHelper.DegreesToRadians(_spinAxis.X * _spin),
                    MathHelper.DegreesToRadians(_spinAxis.Y * _spin),
                    MathHelper.DegreesToRadians(_spinAxis.Z * _spin)),
                    Vector3.Zero);
            }
            transform *= _transform;
            if (index == _modelIndex)
            {
                transform.M42 += (MathF.Sin(_spin / 180 * MathF.PI) + 1) / 8f;
            }
            return transform;
        }

        private static float GetItemRotation()
        {
            float rotation = _nextItemRotation / (float)(UInt16.MaxValue + 1) * 360f;
            _nextItemRotation += 0x2000;
            return rotation;
        }
    }

    public class ItemEntity : SpinningEntityBase
    {
        private readonly ItemEntityData _data;

        public ItemEntity(ItemEntityData data) : base(0.35f, Vector3.UnitY, 0, NewEntityType.Item)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector.ToFloatVector(),
                data.Header.UpVector.ToFloatVector(), data.Header.Position.ToFloatVector().AddY(0.65f));
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
            _anyLighting = _models.Any(n => n.Materials.Any(m => m.Lighting != 0));
        }
    }

    public class FhItemEntity : SpinningEntityBase
    {
        private readonly FhItemEntityData _data;

        public FhItemEntity(FhItemEntityData data) : base(0.35f, Vector3.UnitY, 0, NewEntityType.Item)
        {
            _data = data;
            Id = data.Header.EntityId;
            // note: the actual height at creation is 1.0f greater than the spawner's,
            // but 0.5f is subtracted when drawing (after the floating calculation)
            ComputeTransform(data.Header.RightVector.ToFloatVector(),
                data.Header.UpVector.ToFloatVector(), data.Header.Position.ToFloatVector().AddY(0.5f));
            string name = Metadata.FhItems[(int)data.ModelId];
            NewModel model = Read.GetFhNewModel(name);
            _models.Add(model);
            _anyLighting = model.Materials.Any(m => m.Lighting != 0);
        }
    }
}
