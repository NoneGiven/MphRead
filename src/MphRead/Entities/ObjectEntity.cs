using System;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ObjectEntity : VisibleEntityBase
    {
        private readonly ObjectEntityData _data;
        private CollisionVolume _effectVolume;
        private readonly bool _scanVisorOnly = false;

        // used for ID -1 (scan point, effect spawner)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x22, 0x8B, 0x22).AsVector4();

        public ObjectEntity(ObjectEntityData data) : base(NewEntityType.Object)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            if (data.EffectId > 0)
            {
                _effectVolume = SceneSetup.TransformVolume(data.Volume, Transform); // ntodo: no public statics
            }
            if (data.ModelId == UInt32.MaxValue)
            {
                AddPlaceholderModel();
            }
            else
            {
                ObjectMetadata meta = Metadata.GetObjectById((int)data.ModelId);
                Recolor = meta.RecolorId;
                NewModel model = Read.GetNewModel(meta.Name);
                if (meta != null && meta.AnimationIds[0] == 0xFF)
                {
                    model.Animations.NodeGroupId = -1;
                    model.Animations.MaterialGroupId = -1;
                    model.Animations.TexcoordGroupId = -1;
                    model.Animations.TextureGroupId = -1;
                }
                // AlimbicGhost_01, GhostSwitch
                if (data.ModelId == 0 || data.ModelId == 41)
                {
                    _scanVisorOnly = true;
                }
                _models.Add(model);
                // temporary
                if (model.Name == "AlimbicCapsule")
                {
                    model.Animations.NodeGroupId = -1;
                    model.Animations.MaterialGroupId = -1;
                }
                else if (model.Name == "WallSwitch")
                {
                    model.Animations.NodeGroupId = -1;
                    model.Animations.MaterialGroupId = -1;
                }
                else if (model.Name == "SniperTarget")
                {
                    model.Animations.NodeGroupId = -1;
                }
                else if (model.Name == "SecretSwitch")
                {
                    model.Animations.NodeGroupId = -1;
                    model.Animations.MaterialGroupId = -1;
                }
            }
        }

        public override void Process(NewScene scene)
        {
            ShouldDraw = !_scanVisorOnly || scene.ScanVisor;
            base.Process(scene);
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (_data.EffectId > 0 && scene.ShowVolumes == VolumeDisplay.Object)
            {
                AddVolumeItem(_effectVolume, Vector3.UnitX, scene);
            }
        }
    }
}
