using System.Collections.Generic;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlatformEntity : EntityBase
    {
        private readonly PlatformEntityData _data;

        // used for ID 2 (energyBeam, arcWelder)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x2F, 0x4F, 0x4F).AsVector4();

        private uint _flags = 0;
        private readonly List<int> _effectNodeIds = new List<int>() { -1, -1, -1, -1 };
        private readonly List<EffectEntry?> _effects = new List<EffectEntry?>() { null, null, null, null };
        private const int _effectId = 182; // nozzleJet

        public PlatformEntity(PlatformEntityData data) : base(EntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            _flags = data.Flags;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                AddPlaceholderModel();
            }
            else
            {
                ModelInstance inst = Read.GetModelInstance(meta.Name);
                _models.Add(inst);
                // temporary
                if (meta.Name == "SamusShip" || meta.Name == "SyluxTurret")
                {
                    inst.SetNodeAnim(-1);
                }
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if ((_flags & 0x80000) != 0)
            {
                Model model = _models[0].Model;
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

        public override void Destroy(Scene scene)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                EffectEntry? effectEntry = _effects[i];
                if (effectEntry != null)
                {
                    scene.UnlinkEffectEntry(effectEntry);
                }
            }
        }

        public override void Process(Scene scene)
        {
            // todo: if "is_visible" returns false (and other conditions), don't draw the effects
            Model model = _models[0].Model;
            for (int i = 0; i < 4; i++)
            {
                if (_effectNodeIds[i] >= 0 && _effects[i] == null)
                {
                    Matrix4 transform = Matrix.GetTransform4(Vector3.UnitX, Vector3.UnitY, new Vector3(0, 2, 0));
                    _effects[i] = scene.SpawnEffectGetEntry(_effectId, transform);
                    for (int j = 0; j < _effects[i]!.Elements.Count; j++)
                    {
                        EffectElementEntry element = _effects[i]!.Elements[j];
                        element.Flags |= 0x80000; // set bit 19 (lifetime extension)
                    }
                }
                if (_effects[i] != null)
                {
                    Matrix4 transform = model.Nodes[_effectNodeIds[i]].Animation;
                    var position = new Vector3(
                        transform.M31 * 1.5f + transform.M41,
                        transform.M32 * 1.5f + transform.M42,
                        transform.M33 * 1.5f + transform.M43
                    );
                    transform = Matrix.GetTransform4(new Vector3(transform.Row1), new Vector3(transform.Row2), position);
                    for (int j = 0; j < _effects[i]!.Elements.Count; j++)
                    {
                        EffectElementEntry element = _effects[i]!.Elements[j];
                        element.Position = position;
                        element.Transform = transform;
                    }
                }
            }
            base.Process(scene);
        }
    }

    public class FhPlatformEntity : EntityBase
    {
        private readonly FhPlatformEntityData _data;

        public FhPlatformEntity(FhPlatformEntityData data) : base(EntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = Read.GetModelInstance("platform", firstHunt: true);
            _models.Add(inst);
        }
    }
}
