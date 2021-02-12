namespace MphRead.Entities
{
    // todo: pre-allocate list
    public class BeamEffectEntity : EntityBase
    {
        public BeamEffectEntity() : base(EntityType.BeamEffect)
        {
            // todo: load these along with the room
            Read.GetModelInstance("iceWave"); // node anim c = 30
            Read.GetModelInstance("sniperBeam"); // node anim c = 30
            Read.GetModelInstance("cylBossLaserBurn"); // mat anim c = 61
            // todo: double check the "initial frame minus 1" thing (lifetime ends up being full amount?)
        }
    }
}
