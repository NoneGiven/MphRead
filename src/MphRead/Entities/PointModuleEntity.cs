using System.Linq;

namespace MphRead.Entities
{
    public class PointModuleEntity : VisibleEntityBase
    {
        private readonly PointModuleEntityData _data;

        public PointModuleEntity(PointModuleEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            NewModel model = Read.GetFhNewModel("pick_morphball");
            _models.Add(model);
            _anyLighting = model.Materials.Any(m => m.Lighting != 0);
        }
    }
}
