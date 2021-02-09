using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class FlagBaseEntity : VisibleEntityBase
    {
        private readonly FlagBaseEntityData _data;
        private readonly CollisionVolume _volume;

        public FlagBaseEntity(FlagBaseEntityData data, GameMode mode) : base(NewEntityType.FlagBase)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.Volume, Position);
            Recolor = mode == GameMode.Capture ? (int)data.TeamId : 0;
            // note: this mode check is necessary because e.g. Sic Transit has OctolithFlags/FlagBases
            // enabled in Defender mode according to their layer masks, but they don't appear in-game
            if (mode == GameMode.Capture || mode == GameMode.Bounty)
            {
                string name = mode == GameMode.Capture ? "flagbase_ctf" : "flagbase_cap";
                NewModel model = Read.GetNewModel(name);
                _models.Add(model);
            }
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.FlagBase)
            {
                AddVolumeItem(_volume, Vector3.One, scene);
            }
        }
    }
}
