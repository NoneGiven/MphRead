using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class JumpPadEntity : EntityBase
    {
        private readonly JumpPadEntityData _data;
        private readonly Matrix4 _beamTransform;
        private readonly CollisionVolume _volume;

        private bool _invSetUp = false;
        private EntityBase? _parent = null;
        private Vector3 _invPos;

        public JumpPadEntity(JumpPadEntityData data) : base(EntityType.JumpPad)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            ModelInstance baseInst = Read.GetModelInstance(modelName);
            _models.Add(baseInst);
            ModelInstance beamInst = Read.GetModelInstance("JumpPad_Beam");
            Vector3 beamVector = data.BeamVector.ToFloatVector().Normalized();
            _beamTransform = GetTransformMatrix(beamVector, beamVector.X != 0 || beamVector.Z != 0 ? Vector3.UnitY : Vector3.UnitX);
            _beamTransform.Row3.Y = 0.25f;
            // todo: room state
            Active = data.Active != 0;
            _models.Add(beamInst);
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
            ModelInstance baseInst = Read.GetModelInstance(name, firstHunt: true);
            _models.Add(baseInst);
            name = data.ModelId == 1 ? "balljump_ray" : "jumppad_ray";
            ModelInstance beamInst = Read.GetModelInstance(name, firstHunt: true);
            Vector3 beamVector = data.BeamVector.ToFloatVector().Normalized();
            _beamTransform = GetTransformMatrix(beamVector, beamVector.X != 0 || beamVector.Z != 0 ? Vector3.UnitY : Vector3.UnitX);
            beamInst.SetTexcoordAnim(-1); // the game doesn't enable this animation
            _models.Add(beamInst);
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
