using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerSpawnEntity : VisibleEntityBase
    {
        private readonly PlayerSpawnEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x7F, 0x00, 0x00).AsVector4();

        public PlayerSpawnEntity(PlayerSpawnEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            UsePlaceholderModel();
        }
    }
}
