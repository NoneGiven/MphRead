using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class JumpPadEntity : EntityBase
    {
        private readonly JumpPadEntityData _data;
        private readonly Matrix4 _beamTransform;
        private CollisionVolume _volume;
        private Vector3 _prevPos;

        private bool _invSetUp = false;
        private EntityBase? _parent = null;
        private Vector3 _invPos;

        public JumpPadEntity(JumpPadEntityData data) : base(EntityType.JumpPad)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _prevPos = Position;
            _volume = CollisionVolume.Move(_data.Volume, Position);
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            SetUpModel(modelName);
            ModelInstance beamInst = SetUpModel("JumpPad_Beam");
            Vector3 beamVector = data.BeamVector.ToFloatVector().Normalized();
            _beamTransform = GetTransformMatrix(beamVector, beamVector.X != 0 || beamVector.Z != 0 ? Vector3.UnitY : Vector3.UnitX);
            _beamTransform.Row3.Y = 0.25f;
            // todo: room state
            Active = data.Active != 0;
            beamInst.Active = Active;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.ParentId != -1)
            {
                if (scene.TryGetEntity(_data.ParentId, out EntityBase? parent))
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
            if (_prevPos != Position)
            {
                _volume = CollisionVolume.Move(_data.Volume, Position);
                _prevPos = Position;
            }
            return base.Process(scene);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            if (index == 1)
            {
                return Matrix4.CreateScale(inst.Model.Scale) * _beamTransform * _transform;
            }
            return base.GetModelTransform(inst, index);
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.JumpPad)
            {
                AddVolumeItem(_volume, Vector3.UnitY, scene);
            }
        }

        public override void SetActive(bool active)
        {
            base.SetActive(active);
            _models[1].Active = Active;
        }
    }

    public class FhJumpPadEntity : EntityBase
    {
        private readonly FhJumpPadEntityData _data;
        private readonly Matrix4 _beamTransform;

        private readonly CollisionVolume _volume;

        public FhJumpPadEntity(FhJumpPadEntityData data) : base(EntityType.JumpPad)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.ActiveVolume, Position);
            string name = data.ModelId == 1 ? "balljump" : "jumppad_base";
            SetUpModel(name, firstHunt: true);
            name = data.ModelId == 1 ? "balljump_ray" : "jumppad_ray";
            ModelInstance beamInst = SetUpModel(name, firstHunt: true);
            Vector3 beamVector = data.BeamVector.ToFloatVector().Normalized();
            _beamTransform = GetTransformMatrix(beamVector, beamVector.X != 0 || beamVector.Z != 0 ? Vector3.UnitY : Vector3.UnitX);
            // anitodo: what prevents the texcoord animation from playing in game?
            beamInst.SetAnimation(-1);
            beamInst.SetAnimation(0, 0, SetFlags.Node | SetFlags.Material | SetFlags.Texture);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            if (index == 1)
            {
                // FH beam vectors are absolute, so don't include the parent rotation
                // todo: it would be nicer to just compute beamTransform as relative on creation
                Matrix4 transform = Matrix4.CreateScale(inst.Model.Scale) * _beamTransform;
                transform.Row3.Xyz = Position.AddY(0.25f);
                return transform;
            }
            return base.GetModelTransform(inst, index);
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.JumpPad)
            {
                AddVolumeItem(_volume, Vector3.UnitY, scene);
            }
        }
    }
}
