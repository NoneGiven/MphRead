using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TriggerVolumeEntity : VisibleEntityBase
    {
        private readonly TriggerVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();

        public TriggerVolumeEntity(TriggerVolumeEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }

    public class FhTriggerVolumeEntity : VisibleEntityBase
    {
        private readonly FhTriggerVolumeEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();

        public FhTriggerVolumeEntity(FhTriggerVolumeEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
