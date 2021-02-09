using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class JumpPadEntity : VisibleEntityBase
    {
        private readonly JumpPadEntityData _data;
        private readonly Matrix4 _beamTransform;

        private readonly CollisionVolume _volume;

        public JumpPadEntity(JumpPadEntityData data) : base(NewEntityType.JumpPad)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.Volume, Position);
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            NewModel baseModel = Read.GetNewModel(modelName);
            _models.Add(baseModel);
            NewModel beamModel = Read.GetNewModel("JumpPad_Beam");
            Vector3 beamVector = data.BeamVector.ToFloatVector().Normalized();
            _beamTransform = GetTransformMatrix(beamVector, beamVector.X != 0 || beamVector.Z != 0 ? Vector3.UnitY : Vector3.UnitX);
            _beamTransform.Row3.Y = 0.25f;
            // todo: room state
            if (data.Active == 0)
            {
                beamModel.Active = false;
            }
            _models.Add(beamModel);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            if (index == 1)
            {
                return Matrix4.CreateScale(model.Scale) * _beamTransform * _transform;
            }
            return base.GetModelTransform(model, index);
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.JumpPad)
            {
                AddVolumeItem(_volume, Vector3.UnitY, scene);
            }
        }
    }

    public class FhJumpPadEntity : VisibleEntityBase
    {
        private readonly FhJumpPadEntityData _data;
        private readonly Matrix4 _beamTransform;

        private readonly CollisionVolume _volume;

        public FhJumpPadEntity(FhJumpPadEntityData data) : base(NewEntityType.JumpPad)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.MoveVolume(_data.ActiveVolume, Position);
            string name = data.ModelId == 1 ? "balljump" : "jumppad_base";
            NewModel baseModel = Read.GetFhNewModel(name);
            _models.Add(baseModel);
            name = data.ModelId == 1 ? "balljump_ray" : "jumppad_ray";
            NewModel beamModel = Read.GetFhNewModel(name);
            Vector3 beamVector = data.BeamVector.ToFloatVector().Normalized();
            _beamTransform = GetTransformMatrix(beamVector, beamVector.X != 0 || beamVector.Z != 0 ? Vector3.UnitY : Vector3.UnitX);
            beamModel.Animations.TexcoordGroupId = -1;
            _models.Add(beamModel);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            if (index == 1)
            {
                // FH beam vectors are absolute, so don't include the parent rotation
                // todo: it would be nicer to just compute beamTransform as relative on creation
                Matrix4 transform = Matrix4.CreateScale(model.Scale) * _beamTransform;
                transform.Row3.Xyz = Position.AddY(0.25f);
                return transform;
            }
            return base.GetModelTransform(model, index);
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.JumpPad)
            {
                AddVolumeItem(_volume, Vector3.UnitY, scene);
            }
        }
    }
}
