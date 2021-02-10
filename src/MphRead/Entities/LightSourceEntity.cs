using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class LightSourceEntity : VisibleEntityBase
    {
        private readonly LightSourceEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xDE, 0xAD).AsVector4();

        public CollisionVolume Volume { get; }
        public bool Light1Enabled { get; }
        public Vector3 Light1Vector { get; }
        public Vector3 Light1Color { get; }
        public bool Light2Enabled { get; }
        public Vector3 Light2Vector { get; }
        public Vector3 Light2Color { get; }

        public LightSourceEntity(LightSourceEntityData data) : base(NewEntityType.LightSource)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            Volume = SceneSetup.MoveVolume(_data.Volume, Position);
            Light1Enabled = _data.Light1Enabled != 0;
            Light1Vector = _data.Light1Vector.ToFloatVector();
            Light1Color = _data.Light1Color.AsVector3();
            Light2Enabled = _data.Light2Enabled != 0;
            Light2Vector = _data.Light2Vector.ToFloatVector();
            Light2Color = _data.Light2Color.AsVector3();
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.LightColor1 || scene.ShowVolumes == VolumeDisplay.LightColor2)
            {
                Vector3 color = Vector3.Zero;
                if (scene.ShowVolumes == VolumeDisplay.LightColor1 && _data.Light1Enabled != 0)
                {
                    color = Light1Color;
                }
                else if (scene.ShowVolumes == VolumeDisplay.LightColor2 && _data.Light2Enabled != 0)
                {
                    color = Light2Color;
                }
                AddVolumeItem(Volume, color, scene);
            }
        }
    }
}
