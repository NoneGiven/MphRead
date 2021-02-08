namespace MphRead.Entities
{
    public class PlatformEntity : VisibleEntityBase
    {
        private readonly PlatformEntityData _data;

        public PlatformEntity(PlatformEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                // mtodo: entity placeholders
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
