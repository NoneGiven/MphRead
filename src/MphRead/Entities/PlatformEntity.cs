using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlatformEntity : VisibleEntityBase
    {
        private readonly PlatformEntityData _data;

        // used for ID 2 (energyBeam, arcWelder)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x2F, 0x4F, 0x4F).AsVector4();

        public PlatformEntity(PlatformEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                AddPlaceholderModel();
            }
            else
            {
                NewModel model = Read.GetNewModel(meta.Name);
                _models.Add(model);
                // temporary
                if (meta.Name == "SamusShip")
                {
                    model.Animations.NodeGroupId = 1;
                }
                else if (meta.Name == "SyluxTurret")
                {
                    model.Animations.NodeGroupId = -1;
                }
            }
        }
    }

    public class FhPlatformEntity : VisibleEntityBase
    {
        private readonly FhPlatformEntityData _data;

        public FhPlatformEntity(FhPlatformEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            NewModel model = Read.GetFhNewModel("platform");
            _models.Add(model);
        }
    }
}
