using System;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public readonly struct ItemInstanceEntityData
    {
        public readonly Vector3 Position;
        public readonly ItemType ItemType;
        public readonly uint DespawnTimer;

        public ItemInstanceEntityData(Vector3 position, ItemType type, uint despawnTimer)
        {
            Position = position;
            ItemType = type;
            DespawnTimer = despawnTimer;
        }
    }

    // todo: linked entity, despawn timer, other stuff
    public class ItemInstanceEntity : SpinningEntityBase
    {
        public ItemType ItemType { get; }
        private EffectEntry? _effectEntry = null;
        private const int _effectId = 144; // artifactKeyEffect

        private short DespawnTimer { get; set; } = -1;

        public ItemInstanceEntity(ItemInstanceEntityData data) : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance)
        {
            Position = data.Position.AddY(0.65f);
            SetUpModel(Metadata.Items[(int)data.ItemType]);
            ItemType = data.ItemType;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (ItemType == ItemType.ArtifactKey)
            {
                scene.LoadEffect(_effectId); // todo: needs to be loaded by the spawner
                Matrix4 transform = Matrix.GetTransform4(Vector3.UnitX, Vector3.UnitY, Position);
                _effectEntry = scene.SpawnEffectGetEntry(_effectId, transform);
                _effectEntry.SetElementExtension(true);
            }
        }

        public void OnPickedUp()
        {
            DespawnTimer = 0;
            // todo: the rest
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
            SetUpModel(Metadata.FhItems[(int)data.ItemType], firstHunt: true);
        }
    }

    public abstract class SpinningEntityBase : EntityBase
    {
        private float _spin;
        private readonly float _spinSpeed;
        private readonly Vector3 _spinAxis;
        protected int _spinModelIndex;
        protected int _floatModelIndex;

        private static ushort _nextItemRotation = 0;

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, EntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = -1;
            _floatModelIndex = -1;
        }

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex, EntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = spinModelIndex;
            _floatModelIndex = -1;
        }

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex, int floatModelIndex, EntityType type) : base(type)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = spinModelIndex;
            _floatModelIndex = floatModelIndex;
        }

        public override bool Process(Scene scene)
        {
            _spin = (float)(_spin + scene.FrameTime * 360 * _spinSpeed) % 360;
            return base.Process(scene);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            var transform = Matrix4.CreateScale(inst.Model.Scale);
            if (index == _spinModelIndex)
            {
                transform *= Matrix.GetTransformSRT(Vector3.One, new Vector3(
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
            float rotation = _nextItemRotation / (float)0x10000 * 360f;
            _nextItemRotation += 0x2000;
            return rotation;
        }
    }
}
