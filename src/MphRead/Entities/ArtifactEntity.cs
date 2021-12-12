using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ArtifactEntity : EntityBase
    {
        private readonly ArtifactEntityData _data;
        private readonly float _heightOffset;

        private bool _invSetUp = false;
        private EntityBase? _parent = null;
        private Vector3 _invPos;

        public ArtifactEntity(ArtifactEntityData data) : base(EntityType.Artifact)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            string name = data.ModelId >= 8 ? "Octolith" : $"Artifact0{data.ModelId + 1}";
            ModelInstance inst = SetUpModel(name);
            _heightOffset = data.ModelId >= 8 ? 1.75f : inst.Model.Nodes[0].BoundingRadius;
            if (data.HasBase != 0)
            {
                SetUpModel("ArtifactBase");
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.LinkedEntityId != -1)
            {
                if (scene.TryGetEntity(_data.LinkedEntityId, out EntityBase? parent))
                {
                    _parent = parent;
                }
            }
        }

        public override bool Process(Scene scene)
        {
            if (_parent != null)
            {
                if (!_invSetUp)
                {
                    _parent.GetDrawInfo(scene); // force update transforms
                    _invPos = Matrix.Vec3MultMtx4(Position, _parent.CollisionTransform.Inverted());
                    _invSetUp = true;
                }
                Position = Matrix.Vec3MultMtx4(_invPos, _parent.CollisionTransform);
            }
            return base.Process(scene);
        }

        protected override LightInfo GetLightInfo(Scene scene)
        {
            if (_data.ModelId >= 8)
            {
                Vector3 player = scene.CameraPosition;
                var vector1 = new Vector3(0, 1, 0);
                Vector3 vector2 = new Vector3(player.X - Position.X, 0, player.Z - Position.Z).Normalized();
                Matrix3 lightTransform = Matrix.GetTransform3(vector2, vector1);
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
