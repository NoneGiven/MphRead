using System.Collections.Generic;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlatformEntity : VisibleEntityBase
    {
        private readonly PlatformEntityData _data;

        // used for ID 2 (energyBeam, arcWelder)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x2F, 0x4F, 0x4F).AsVector4();

        private readonly List<int> _effectNodeIds = new List<int>() { -1, -1, -1, -1 };
        private const int _effectId = 182; // nozzleJet

        public PlatformEntity(PlatformEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                AddPlaceholderModel();
            }
            else
            {
                NewModel model = Read.GetNewModel(meta.Name);
                _models.Add(model);
                // temporary
                if (meta.Name == "SamusShip")
                {
                    model.Animations.NodeGroupId = 1;
                }
                else if (meta.Name == "SyluxTurret")
                {
                    model.Animations.NodeGroupId = -1;
                }
            }
        }

        public override void Init(NewScene scene)
        {
            base.Init(scene);
            if ((_data.Flags & 0x80000) != 0)
            {
                NewModel model = _models[0];
                for (int i = 0; i < model.Nodes.Count; i++)
                {
                    Node node = model.Nodes[i];
                    if (node.Name == "R_Turret")
                    {
                        _effectNodeIds[0] = i;
                    }
                    else if (node.Name == "R_Turret1")
                    {
                        _effectNodeIds[1] = i;
                    }
                    else if (node.Name == "R_Turret2")
                    {
                        _effectNodeIds[2] = i;
                    }
                    else if (node.Name == "R_Turret3")
                    {
                        _effectNodeIds[3] = i;
                    }
                }
                if (_effectNodeIds[0] != -1 || _effectNodeIds[1] != -1 || _effectNodeIds[2] != -1 || _effectNodeIds[3] != -1)
                {
                    scene.LoadEffect(_effectId);
                }
            }
        }
    }

    public class FhPlatformEntity : VisibleEntityBase
    {
        private readonly FhPlatformEntityData _data;

        public FhPlatformEntity(FhPlatformEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            NewModel model = Read.GetFhNewModel("platform");
            _models.Add(model);
        }
    }
}
