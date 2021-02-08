using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ArtifactEntity : VisibleEntityBase
    {
        private readonly ArtifactEntityData _data;
        private readonly float _heightOffset;

        public ArtifactEntity(ArtifactEntityData data) : base(NewEntityType.Artifact)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            string name = data.ModelId >= 8 ? "Octolith" : $"Artifact0{data.ModelId + 1}";
            NewModel model = Read.GetNewModel(name);
            _heightOffset = data.ModelId >= 8 ? 1.75f : model.Nodes[0].CullRadius;
            if (data.ModelId >= 8)
            {
                //model.Rotating = true;
                //model.SpinSpeed = 0.25f;
                //model.UseLightOverride = true;
            }
            _models.Add(model);
            if (data.HasBase != 0)
            {
                NewModel baseModel = Read.GetNewModel("ArtifactBase");
                _models.Add(baseModel);
            }
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            Matrix4 transform = base.GetModelTransform(model, index);
            if (index == 0)
            {
                transform.Row3.Y += _heightOffset;
            }
            else if (index == 1)
            {
                transform.Row3.Y -= 0.2f;
            }
            return transform;
        }
    }
}
