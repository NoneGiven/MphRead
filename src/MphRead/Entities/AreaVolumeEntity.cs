using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class AreaVolumeEntity : EntityBase
    {
        private readonly AreaVolumeEntityData _data;
        private EntityBase? _parent = null;
        private EntityBase? _child = null;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _insideEventColor;
        private readonly Vector3 _exitEventColor;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();
        public AreaVolumeEntityData Data => _data;

        public AreaVolumeEntity(AreaVolumeEntityData data) : base(EntityType.AreaVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideEvent);
            _exitEventColor = Metadata.GetEventColor(data.ExitEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void Init(NewScene scene)
        {
            base.Init(scene);
            if (scene.TryGetEntity(_data.ParentId, out EntityBase? parent))
            {
                _parent = parent;
            }
            if (scene.TryGetEntity(_data.ChildId, out EntityBase? child))
            {
                _child = child;
            }
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.AreaInside || scene.ShowVolumes == VolumeDisplay.AreaExit)
            {
                Vector3 color = scene.ShowVolumes == VolumeDisplay.AreaInside ? _insideEventColor : _exitEventColor;
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

    public class FhAreaVolumeEntity : EntityBase
    {
        private readonly FhAreaVolumeEntityData _data;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _insideEventColor;
        private readonly Vector3 _exitEventColor;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();
        public FhAreaVolumeEntityData Data => _data;

        public FhAreaVolumeEntity(FhAreaVolumeEntityData data) : base(EntityType.AreaVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideEvent);
            _exitEventColor = Metadata.GetEventColor(data.ExitEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.ActiveVolume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.AreaInside || scene.ShowVolumes == VolumeDisplay.AreaExit)
            {
                Vector3 color = scene.ShowVolumes == VolumeDisplay.AreaInside ? _insideEventColor : _exitEventColor;
                AddVolumeItem(_volume, color, scene);
            }
        }
    }
}
