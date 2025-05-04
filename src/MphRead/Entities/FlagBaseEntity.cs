using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class FlagBaseEntity : EntityBase
    {
        private readonly FlagBaseEntityData _data;
        private readonly CollisionVolume _volume;
        private readonly bool _capture = false;

        public NodeData3? EntNodeData { get; set; } = null;

        // flag base has a model in Bounty, but is invisible in Capture
        protected override Vector4? OverrideColor { get; } = new ColorRgb(15, 207, 255).AsVector4();

        public FlagBaseEntity(FlagBaseEntityData data, Scene scene) : base(EntityType.FlagBase, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            // note: an explicit mode check is necessary because e.g. Sic Transit has OctolithFlags/FlagBases
            // enabled in Defender mode according to their layer masks, but they don't appear in-game
            GameMode mode = scene.GameMode;
            if (mode == GameMode.Capture)
            {
                AddPlaceholderModel();
            }
            else if (mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                SetUpModel("flagbase_cap");
            }
            _capture = mode == GameMode.Capture;
        }

        public override bool Process()
        {
            base.Process();
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.OctolithFlag == null || _capture && player.TeamIndex != _data.TeamId)
                {
                    continue;
                }
                if (_volume.TestPoint(player.Position))
                {
                    if (_capture && !CheckOwnOctolith(player))
                    {
                        if (player == PlayerEntity.Main)
                        {
                            PlayerEntity.Main.QueueHudMessage(128, 50, 1 / 1000f, 0, 232); // your octolith is missing!
                        }
                        continue;
                    }
                    player.OctolithFlag.OnCaptured();
                }
            }
            return true;
        }

        private bool CheckOwnOctolith(PlayerEntity player)
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.OctolithFlag)
                {
                    continue;
                }
                var octolith = (OctolithFlagEntity)entity;
                if (octolith.Data.TeamId == player.TeamIndex && !octolith.AtBase)
                {
                    return false;
                }
            }
            return true;
        }

        // todo: is_visible
        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.FlagBase)
            {
                AddVolumeItem(_volume, Vector3.One);
            }
        }
    }
}
