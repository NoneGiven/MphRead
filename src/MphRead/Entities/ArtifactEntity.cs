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

        public override LightInfo GetLightInfo(NewModel model, NewScene scene)
        {
            if (_data.ModelId >= 8)
            {
                Vector3 player = scene.CameraPosition;
                var vector1 = new Vector3(0, 1, 0);
                Vector3 vector2 = new Vector3(player.X - Position.X, 0, player.Z - Position.Z).Normalized();
                Matrix3 lightTransform = SceneSetup.GetTransformMatrix(vector2, vector1);
                return new LightInfo(
                    (Metadata.OctolithLight1Vector * lightTransform).Normalized(),
                    Metadata.OctolithLightColor,
                    (Metadata.OctolithLight2Vector * lightTransform).Normalized(),
                    Metadata.OctolithLightColor
                );
            }
            return base.GetLightInfo(model, scene);
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
