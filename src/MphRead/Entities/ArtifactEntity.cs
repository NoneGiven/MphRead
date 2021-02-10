using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ArtifactEntity : SpinningEntityBase
    {
        private readonly ArtifactEntityData _data;
        private readonly float _heightOffset;

        public ArtifactEntity(ArtifactEntityData data) : base(0.25f, Vector3.UnitY, NewEntityType.Artifact)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            string name = data.ModelId >= 8 ? "Octolith" : $"Artifact0{data.ModelId + 1}";
            ModelInstance inst = Read.GetNewModel(name);
            _heightOffset = data.ModelId >= 8 ? 1.75f : inst.Model.Nodes[0].CullRadius;
            if (data.ModelId >= 8)
            {
                _spinModelIndex = 0;
            }
            _models.Add(inst);
            if (data.HasBase != 0)
            {
                ModelInstance baseInst = Read.GetNewModel("ArtifactBase");
                _models.Add(baseInst);
            }
        }

        protected override LightInfo GetLightInfo(NewScene scene)
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
            return base.GetLightInfo(scene);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
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
