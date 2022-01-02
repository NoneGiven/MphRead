using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class MorphCameraEntity : EntityBase
    {
        private readonly MorphCameraEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0xFF, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private static readonly Vector3 _volumeColor = new Vector3(1, 1, 0);

        public MorphCameraEntity(MorphCameraEntityData data, Scene scene) : base(EntityType.MorphCamera, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.MorphCamera)
            {
                AddVolumeItem(_volume, _volumeColor);
            }
        }
    }

    public class FhMorphCameraEntity : EntityBase
    {
        private readonly FhMorphCameraEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0xFF, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private static readonly Vector3 _volumeColor = new Vector3(1, 1, 0);

        public FhMorphCameraEntity(FhMorphCameraEntityData data, Scene scene) : base(EntityType.MorphCamera, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.MorphCamera)
            {
                AddVolumeItem(_volume, _volumeColor);
            }
        }
    }
}
