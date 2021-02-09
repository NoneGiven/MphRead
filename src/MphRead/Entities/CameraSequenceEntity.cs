using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class CameraSequenceEntity : VisibleEntityBase
    {
        private readonly CameraSequenceEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x69, 0xB4).AsVector4();

        public CameraSequenceEntity(CameraSequenceEntityData data) : base(NewEntityType.CameraSequence)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
