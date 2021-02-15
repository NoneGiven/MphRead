using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public readonly struct BeamEffectEntityData
    {
        public readonly int Type;
        public readonly bool NoSplat;
        public readonly Matrix4 Transform;

        public BeamEffectEntityData(int type, bool noSplat, Matrix4 transform)
        {
            Type = type;
            NoSplat = noSplat;
            Transform = transform;
        }
    }

    public class BeamEffectEntity : EntityBase
    {
        private readonly BeamEffectEntityData _data;
        private int _lifespan = 0;

        protected BeamEffectEntity(BeamEffectEntityData data) : base(EntityType.BeamEffect)
        {
            _data = data;
            // already loaded by scene setup
            ModelInstance model;
            if (data.Type == 0)
            {
                model = Read.GetModelInstance("iceWave");
            }
            else if (data.Type == 1)
            {
                model = Read.GetModelInstance("sniperBeam");
            }
            else if (data.Type == 2)
            {
                model = Read.GetModelInstance("cylBossLaserBurn");
            }
            else
            {
                throw new ProgramException("Invalid beam effect type.");
            }
            _models.Add(model);
            // in-game all the group types are checked, but we're just checking what's actually used
            if (model.Model.AnimationGroups.Node.Count > 0)
            {
                _lifespan = (model.Model.AnimationGroups.Node[0].FrameCount - 1) * 2;
            }
            else if (model.Model.AnimationGroups.Material.Count > 0)
            {
                _lifespan = (model.Model.AnimationGroups.Material[0].FrameCount - 1) * 2;
            }
            Transform = data.Transform;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.Type == 0)
            {
                scene.SpawnEffect(78, _data.Transform); // iceWave
            }
        }

        public override void Process(Scene scene)
        {
            if (_lifespan-- <= 0)
            {
                Active = false;
                _models[0].Active = false;
            }
            base.Process(scene);
        }

        public static BeamEffectEntity? Create(BeamEffectEntityData data, Scene scene)
        {
            // ptodo: effect and type 0 both need to use mtxptr
            if (data.Type >= 3)
            {
                int effectId = data.Type - 3;
                if (data.NoSplat)
                {
                    if (effectId == 1)
                    {
                        // powerBeam --> powerBeamNoSplat
                        effectId = 2;
                    }
                    else if (effectId == 92)
                    {
                        // powerBeamCharge --> powerBeamChargeNoSplat
                        effectId = 98;
                    }
                }
                scene.SpawnEffect(effectId, data.Transform);
                return null;
            }
            // todo: pre-allocate list instead of creating here
            return new BeamEffectEntity(data);
        }
    }
}
