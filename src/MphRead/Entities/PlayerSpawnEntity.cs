using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerSpawnEntity : EntityBase
    {
        private readonly PlayerSpawnEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x7F, 0x00, 0x00).AsVector4();
        private bool _active = false;

        public bool IsActive => _active;
        public bool Availability => _data.Availability != 0;
        public ushort Cooldown { get; set; }

        public PlayerSpawnEntity(PlayerSpawnEntityData data, string nodeName, Scene scene)
            : base(EntityType.PlayerSpawn, nodeName, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
        }

        public override void Initialize()
        {
            base.Initialize();
            SetTransform(_data.Header.FacingVector, _data.Header.UpVector, _data.Header.Position);
            // todo: room state
            _active = _data.Active != 0;
            AddPlaceholderModel();
        }

        public override bool Process()
        {
            if (Cooldown > 0)
            {
                Cooldown--;
            }
            return base.Process();
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Activate || (info.Message == Message.SetActive && (int)info.Param1 != 0))
            {
                _active = true;
                // todo: room state
            }
            else if (info.Message == Message.SetActive && (int)info.Param1 == 0)
            {
                _active = false;
                // todo: room state
            }
        }
    }
}
