using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class OctolithFlagEntity : VisibleEntityBase
    {
        private readonly OctolithFlagEntityData _data;
        private readonly Vector3 _basePosition = Vector3.Zero;

        public OctolithFlagEntity(OctolithFlagEntityData data, GameMode mode) : base(NewEntityType.OctolithFlag)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            Recolor = mode == GameMode.Capture ? data.TeamId : 2;
            if (mode == GameMode.Capture || mode == GameMode.Bounty)
            {
                NewModel octolith = Read.GetNewModel("octolith_ctf");
                _models.Add(octolith);
                // note: in-game, the flag is responsible for drawing its own base in Capture mode as well,
                // but we have that implemented in the flag base entity (which is used in Capture mode, but is invisible)
                if (mode == GameMode.Bounty)
                {
                    NewModel flagBase = Read.GetNewModel("flagbase_bounty");
                    _models.Add(flagBase);
                }
                _basePosition = Position;
                SetAtBase();
            }
        }

        private void SetAtBase()
        {
            Position = _basePosition.AddY(1.25f);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            Matrix4 transform = base.GetModelTransform(model, index);
            if (index == 1)
            {
                transform.Row3.Xyz = _basePosition;
            }
            return transform;
        }

        protected override int GetModelRecolor(NewModel model, int index)
        {
            if (index == 1)
            {
                return 0;
            }
            return base.GetModelRecolor(model, index);
        }
    }
}
