using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TriggerVolumeEntity : EntityBase
    {
        private readonly TriggerVolumeEntityData _data;
        private EntityBase? _parent = null;
        private EntityBase? _child = null;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _parentEventColor;
        private readonly Vector3 _childEventColor;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();
        public TriggerVolumeEntityData Data => _data;

        public TriggerVolumeEntity(TriggerVolumeEntityData data) : base(EntityType.TriggerVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            // todo: change the display/color when inactive (same for AreaVolumes)
            _parentEventColor = Metadata.GetEventColor(data.ParentEvent);
            _childEventColor = Metadata.GetEventColor(data.ChildEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (scene.TryGetEntity(_data.ParentId, out EntityBase? parent))
            {
                _parent = parent;
            }
            if (scene.TryGetEntity(_data.ChildId, out EntityBase? child))
            {
                _child = child;
            }
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (_data.Subtype == TriggerType.Normal &&
                (scene.ShowVolumes == VolumeDisplay.TriggerParent || scene.ShowVolumes == VolumeDisplay.TriggerChild))
            {
                Vector3 color = scene.ShowVolumes == VolumeDisplay.TriggerParent ? _parentEventColor : _childEventColor;
                AddVolumeItem(_volume, color, scene);
            }
        }

        public override EntityBase? GetParent()
        {
            return _parent;
        }

        public override EntityBase? GetChild()
        {
            return _child;
        }
    }

    public class FhTriggerVolumeEntity : EntityBase
    {
        private readonly FhTriggerVolumeEntityData _data;
        private EntityBase? _parent = null;
        private EntityBase? _child = null;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _parentEventColor;
        private readonly Vector3 _childEventColor;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();
        public FhTriggerVolumeEntityData Data => _data;

        public FhTriggerVolumeEntity(FhTriggerVolumeEntityData data) : base(EntityType.TriggerVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            _parentEventColor = Metadata.GetEventColor(data.ParentEvent);
            _childEventColor = Metadata.GetEventColor(data.ChildEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.ActiveVolume, Position);
            AddPlaceholderModel();
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (scene.TryGetEntity(_data.ParentId, out EntityBase? parent))
            {
                _parent = parent;
            }
            if (scene.TryGetEntity(_data.ChildId, out EntityBase? child))
            {
                _child = child;
            }
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (_data.Subtype != FhTriggerType.Threshold &&
                (scene.ShowVolumes == VolumeDisplay.TriggerParent || scene.ShowVolumes == VolumeDisplay.TriggerChild))
            {
                Vector3 color = scene.ShowVolumes == VolumeDisplay.TriggerParent ? _parentEventColor : _childEventColor;
                AddVolumeItem(_volume, color, scene);
            }
        }

        public override EntityBase? GetParent()
        {
            return _parent;
        }

        public override EntityBase? GetChild()
        {
            return _child;
        }
    }
}
