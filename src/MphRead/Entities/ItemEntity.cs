using System;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public abstract class SpinningEntityBase : VisibleEntityBase
    {
        private float _spin;
        private readonly float _spinSpeed;
        private readonly Vector3 _spinAxis;
        protected int _spinModelIndex;
        protected int _floatModelIndex;

        private static ushort _nextItemRotation = 0;

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, NewEntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = -1;
            _floatModelIndex = -1;
        }

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex, NewEntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = spinModelIndex;
            _floatModelIndex = -1;
        }

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex, int floatModelIndex, NewEntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = spinModelIndex;
            _floatModelIndex = floatModelIndex;
        }

        public override void Process(NewScene scene)
        {
            _spin = (float)(_spin + scene.FrameTime * 360 * _spinSpeed) % 360;
            base.Process(scene);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            var transform = Matrix4.CreateScale(model.Scale);
            if (index == _spinModelIndex && model.Animations.NodeGroupId == -1)
            {
                transform *= SceneSetup.ComputeNodeTransforms(Vector3.One, new Vector3(
                    MathHelper.DegreesToRadians(_spinAxis.X * _spin),
                    MathHelper.DegreesToRadians(_spinAxis.Y * _spin),
                    MathHelper.DegreesToRadians(_spinAxis.Z * _spin)),
                    Vector3.Zero);
            }
            transform *= _transform;
            if (index == _floatModelIndex)
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

        public ItemEntity(ItemEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, NewEntityType.Item)
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

    public class FhItemEntity : SpinningEntityBase
    {
        private readonly FhItemEntityData _data;

        public FhItemEntity(FhItemEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, NewEntityType.Item)
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
