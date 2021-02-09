using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class AreaVolumeEntity : VisibleEntityBase
    {
        private readonly AreaVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private readonly Vector3 _insideEventColor;
        private readonly Vector3 _exitEventColor;

        public AreaVolumeEntity(AreaVolumeEntityData data) : base(NewEntityType.AreaVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideEvent);
            _exitEventColor = Metadata.GetEventColor(data.ExitEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.Volume, Position);
            UsePlaceholderModel();
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

    public class FhAreaVolumeEntity : VisibleEntityBase
    {
        private readonly FhAreaVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private readonly Vector3 _insideEventColor;
        private readonly Vector3 _exitEventColor;

        public FhAreaVolumeEntity(FhAreaVolumeEntityData data) : base(NewEntityType.AreaVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideEvent);
            _exitEventColor = Metadata.GetEventColor(data.ExitEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.ActiveVolume, Position);
            UsePlaceholderModel();
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
