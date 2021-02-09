using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TriggerVolumeEntity : VisibleEntityBase
    {
        private readonly TriggerVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private readonly Vector3 _parentEventColor;
        private readonly Vector3 _childEventColor;

        public TriggerVolumeEntity(TriggerVolumeEntityData data) : base(NewEntityType.TriggerVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            // todo: change the display/color when inactive (same for AreaVolumes)
            _parentEventColor = Metadata.GetEventColor(data.ParentEvent);
            _childEventColor = Metadata.GetEventColor(data.ChildEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.TriggerParent || scene.ShowVolumes == VolumeDisplay.TriggerChild)
            {
                Vector3 color = scene.ShowVolumes == VolumeDisplay.TriggerParent ? _parentEventColor : _childEventColor;
                AddVolumeItem(_volume, color, scene);
            }
        }
    }

    public class FhTriggerVolumeEntity : VisibleEntityBase
    {
        private readonly FhTriggerVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private readonly Vector3 _parentEventColor;
        private readonly Vector3 _childEventColor;

        public FhTriggerVolumeEntity(FhTriggerVolumeEntityData data) : base(NewEntityType.TriggerVolume)
        {
            _data = data;
            Id = data.Header.EntityId;
            _parentEventColor = Metadata.GetEventColor(data.ParentEvent);
            _childEventColor = Metadata.GetEventColor(data.ChildEvent);
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.ActiveVolume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.TriggerParent || scene.ShowVolumes == VolumeDisplay.TriggerChild)
            {
                Vector3 color = scene.ShowVolumes == VolumeDisplay.TriggerParent ? _parentEventColor : _childEventColor;
                AddVolumeItem(_volume, color, scene);
            }
        }
    }
}
