using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class NodeDefenseEntity : VisibleEntityBase
    {
        private readonly NodeDefenseEntityData _data;
        private readonly Matrix4 _circleScale;
        private readonly CollisionVolume _volume;

        public NodeDefenseEntity(NodeDefenseEntityData data, GameMode mode) : base(NewEntityType.NodeDefense)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            if (mode == GameMode.Defender || mode == GameMode.Nodes)
            {
                ModelInstance nodeInst = Read.GetNewModel("koth_data_flow");
                _models.Add(nodeInst);
                // todo: spinning + changing color when active
                ModelInstance circleInst = Read.GetNewModel("koth_terminal");
                float scale = data.Volume.CylinderRadius.FloatValue;
                _circleScale = Matrix4.CreateScale(scale);
                _models.Add(circleInst);
            }
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index == 1)
            {
                transform = _circleScale * transform;
                transform.Row3.Y += 0.7f;
            }
            return transform;
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.DefenseNode)
            {
                AddVolumeItem(_volume, Vector3.One, scene);
            }
        }
    }
}
