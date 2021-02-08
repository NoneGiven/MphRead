namespace MphRead.Entities
{
    public class TeleporterEntity : VisibleEntityBase
    {
        private readonly TeleporterEntityData _data;

        public TeleporterEntity(TeleporterEntityData data, int areaId, bool multiplayer) : base(NewEntityType.Teleporter)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            Recolor = multiplayer ? 0 : areaId;
            if (data.Invisible != 0)
            {
                // mtodo: entity placeholders
            }
            else
            {
                // todo: how to use ArtifactId?
                int flags = data.ArtifactId < 8 && data.Invisible == 0 ? 2 : 0;
                string modelName;
                if ((flags & 2) == 0)
                {
                    modelName = multiplayer ? "TeleporterMP" : "TeleporterSmall";
                }
                else
                {
                    modelName = "Teleporter";
                }
                NewModel model = Read.GetNewModel(modelName);
                _models.Add(model);
            }
        }
    }
}
