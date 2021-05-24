using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class OctolithFlagEntity : EntityBase
    {
        private readonly OctolithFlagEntityData _data;
        private readonly Vector3 _basePosition = Vector3.Zero;
        private bool _bounty = false;

        public OctolithFlagEntity(OctolithFlagEntityData data, GameMode mode) : base(EntityType.OctolithFlag)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            Recolor = mode == GameMode.Capture ? data.TeamId : 2;
            _bounty = mode != GameMode.Capture;
            if (mode == GameMode.Capture || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                SetUpModel("octolith_ctf");
                SetUpModel(mode == GameMode.Capture ? "flagbase_ctf" : "flagbase_bounty");
                _basePosition = Position;
                SetAtBase();
            }
        }

        private void SetAtBase()
        {
            Position = _basePosition.AddY(1.25f);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index == 1)
            {
                transform.Row3.Xyz = _basePosition;
            }
            return transform;
        }

        protected override int GetModelRecolor(ModelInstance inst, int index)
        {
            if (index == 1 && _bounty)
            {
                return 0;
            }
            return base.GetModelRecolor(inst, index);
        }
    }
}
