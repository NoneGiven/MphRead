using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class MorphCameraEntity : VisibleEntityBase
    {
        private readonly MorphCameraEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0xFF, 0x00).AsVector4();

        public MorphCameraEntity(MorphCameraEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }

    public class FhMorphCameraEntity : VisibleEntityBase
    {
        private readonly FhMorphCameraEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0xFF, 0x00).AsVector4();

        public FhMorphCameraEntity(FhMorphCameraEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
