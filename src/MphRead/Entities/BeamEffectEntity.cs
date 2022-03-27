using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public readonly struct BeamEffectEntityData
    {
        // used as a type for icewave/sniperbeam/cylburn, or subtract 3 to get an effect ID to spawn
        public readonly int Type;
        // in game this is treated as an anim ID for icewave/sniperbeam/cylburn, but all of those only have one animation anyway
        public readonly bool NoSplat;
        public readonly Matrix4 Transform;
        public readonly EntityCollision? EntityCollision;

        public BeamEffectEntityData(int type, bool noSplat, Matrix4 transform, EntityCollision? entCol = null)
        {
            Type = type;
            NoSplat = noSplat;
            Transform = transform;
            EntityCollision = entCol;
        }
    }

    public class BeamEffectEntity : EntityBase
    {
        private int _lifespan = 0;

        public BeamEffectEntity(Scene scene) : base(EntityType.BeamEffect, scene)
        {
        }

        public void Spawn(BeamEffectEntityData data)
        {
            _models.Clear();
            // already loaded by scene setup
            ModelInstance model;
            if (data.Type == 0)
            {
                model = SetUpModel("iceWave", 0, AnimFlags.NoLoop);
            }
            else if (data.Type == 1)
            {
                model = SetUpModel("sniperBeam", 0, AnimFlags.NoLoop);
            }
            else if (data.Type == 2)
            {
                model = SetUpModel("cylBossLaserBurn");
            }
            else
            {
                throw new ProgramException("Invalid beam effect type.");
            }
            _lifespan = 0;
            // in-game all the group types are checked, but we're just checking what's actually used
            if (model.Model.AnimationGroups.Node.Count > 0 && data.Type != 2)
            {
                _lifespan = (model.Model.AnimationGroups.Node[0].FrameCount - 1) * 2;
            }
            else if (model.Model.AnimationGroups.Material.Count > 0)
            {
                _lifespan = (model.Model.AnimationGroups.Material[0].FrameCount - 1) * 2;
            }
            Transform = data.Transform;
            if (data.Type == 0)
            {
                _scene.SpawnEffect(78, data.Transform.ClearScale()); // iceWave
            }
        }

        public void Reposition(Vector3 offset)
        {
            // todo?: update more effect stuff?
            Position += offset;
        }

        public override bool Process()
        {
            if (_lifespan-- <= 0)
            {
                return false;
            }
            return base.Process();
        }

        public override void Destroy()
        {
            _scene.UnlinkBeamEffect(this);
            base.Destroy();
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
                scene.SpawnEffect(effectId, data.Transform, entCol: data.EntityCollision);
                return null;
            }
            return scene.InitBeamEffect(data);
        }
    }
}
