using System;
using System.Linq;

namespace MphRead.Entities
{
    public class ObjectEntity : VisibleEntityBase
    {
        private readonly ObjectEntityData _data;
        private CollisionVolume _effectVolume;
        private readonly bool _scanVisorOnly = false;

        public ObjectEntity(ObjectEntityData data) : base(NewEntityType.Object)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            if (data.ModelId == UInt32.MaxValue)
            {
                // mtodo: entity placeholders
            }
            else
            {
                ObjectMetadata meta = Metadata.GetObjectById((int)data.ModelId);
                Recolor = meta.RecolorId;
                NewModel model = Read.GetNewModel(meta.Name);
                if (data.EffectId > 0)
                {
                    _effectVolume = SceneSetup.TransformVolume(data.Volume, Transform); // ntodo: no public statics
                }
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
    }
}
