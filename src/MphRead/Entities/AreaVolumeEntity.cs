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

        public AreaVolumeEntity(AreaVolumeEntityData data, Scene scene) : base(EntityType.AreaVolume, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideMessage);
            _exitEventColor = Metadata.GetEventColor(data.ExitMessage);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_scene.TryGetEntity(_data.ParentId, out EntityBase? parent))
            {
                _parent = parent;
            }
            if (_scene.TryGetEntity(_data.ChildId, out EntityBase? child))
            {
                _child = child;
            }
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.AreaInside || _scene.ShowVolumes == VolumeDisplay.AreaExit)
            {
                Vector3 color = _scene.ShowVolumes == VolumeDisplay.AreaInside ? _insideEventColor : _exitEventColor;
                AddVolumeItem(_volume, color);
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

        public override bool Process()
        {
            return base.Process();
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

        public FhAreaVolumeEntity(FhAreaVolumeEntityData data, Scene scene) : base(EntityType.AreaVolume, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideMessage);
            _exitEventColor = Metadata.GetEventColor(data.ExitMessage);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.ActiveVolume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.AreaInside || _scene.ShowVolumes == VolumeDisplay.AreaExit)
            {
                Vector3 color = _scene.ShowVolumes == VolumeDisplay.AreaInside ? _insideEventColor : _exitEventColor;
                AddVolumeItem(_volume, color);
            }
        }
    }
}
