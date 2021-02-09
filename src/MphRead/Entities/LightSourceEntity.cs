using System.Buffers;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class LightSourceEntity : VisibleEntityBase
    {
        private readonly LightSourceEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xDE, 0xAD).AsVector4();

        private readonly CollisionVolume _volume;
        private readonly Vector3 _light1Vector;
        private readonly Vector3 _light1Color;
        private readonly Vector3 _light2Vector;
        private readonly Vector3 _light2Color;

        public LightSourceEntity(LightSourceEntityData data) : base(NewEntityType.PlayerSpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _volume = SceneSetup.TransformVolume(_data.Volume, Transform);
            _light1Vector = _data.Light1Vector.ToFloatVector();
            _light1Color = _data.Light1Color.AsVector3();
            _light2Vector = _data.Light2Vector.ToFloatVector();
            _light2Color = _data.Light2Color.AsVector3();
            UsePlaceholderModel();
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.LightColor1 || scene.ShowVolumes == VolumeDisplay.LightColor2)
            {
                Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(8);
                Vector3 point0 = _volume.BoxPosition;
                Vector3 sideX = _volume.BoxVector1 * _volume.BoxDot1;
                Vector3 sideY = _volume.BoxVector2 * _volume.BoxDot2;
                Vector3 sideZ = _volume.BoxVector3 * _volume.BoxDot3;
                verts[0] = point0;
                verts[1] = point0 + sideZ;
                verts[2] = point0 + sideX;
                verts[3] = point0 + sideX + sideZ;
                verts[4] = point0 + sideY;
                verts[5] = point0 + sideY + sideZ;
                verts[6] = point0 + sideX + sideY;
                verts[7] = point0 + sideX + sideY + sideZ;
                Vector3 color = Vector3.Zero;
                if (scene.ShowVolumes == VolumeDisplay.LightColor1 && _data.Light1Enabled != 0)
                {
                    color = _light1Color;
                }
                else if (scene.ShowVolumes == VolumeDisplay.LightColor2 && _data.Light2Enabled != 0)
                {
                    color = _light2Color;
                }
                CullingMode cullingMode = _volume.TestPoint(scene.CameraPosition) ? CullingMode.Front : CullingMode.Back;
                scene.AddRenderItem(cullingMode, scene.GetNextPolygonId(), color, VolumeType.Box, verts);
            }
        }
    }
}
