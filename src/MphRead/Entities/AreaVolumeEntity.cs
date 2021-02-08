using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class AreaVolumeEntity : VisibleEntityBase
    {
        private readonly AreaVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();

        public AreaVolumeEntity(AreaVolumeEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }

    public class FhAreaVolumeEntity : VisibleEntityBase
    {
        private readonly FhAreaVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();

        public FhAreaVolumeEntity(FhAreaVolumeEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
