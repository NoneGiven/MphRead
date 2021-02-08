using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class LightSourceEntity : VisibleEntityBase
    {
        private readonly LightSourceEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xDE, 0xAD).AsVector4();

        public LightSourceEntity(LightSourceEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
