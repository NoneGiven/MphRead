using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class MorphCameraEntity : VisibleEntityBase
    {
        private readonly MorphCameraEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0xFF, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private static readonly Vector3 _volumeColor = new Vector3(1, 1, 0);

        public MorphCameraEntity(MorphCameraEntityData data) : base(NewEntityType.MorphCamera)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.MorphCamera)
            {
                AddVolumeItem(_volume, _volumeColor, scene);
            }
        }
    }

    public class FhMorphCameraEntity : VisibleEntityBase
    {
        private readonly FhMorphCameraEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0xFF, 0x00).AsVector4();

        private readonly CollisionVolume _volume;
        private static readonly Vector3 _volumeColor = new Vector3(1, 1, 0);

        public FhMorphCameraEntity(FhMorphCameraEntityData data) : base(NewEntityType.MorphCamera)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.Volume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.MorphCamera)
            {
                AddVolumeItem(_volume, _volumeColor, scene);
            }
        }
    }
}