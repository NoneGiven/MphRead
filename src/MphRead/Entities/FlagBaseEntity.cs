using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class FlagBaseEntity : EntityBase
    {
        private readonly FlagBaseEntityData _data;
        private readonly CollisionVolume _volume;

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
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.FlagBase)
            {
                AddVolumeItem(_volume, Vector3.One);
            }
        }
    }
}
