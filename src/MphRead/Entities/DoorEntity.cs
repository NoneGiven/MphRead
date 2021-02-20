using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class DoorEntity : EntityBase
    {
        private readonly DoorEntityData _data;
        private readonly Matrix4 _lockTransform;

        public DoorEntity(DoorEntityData data) : base(EntityType.Door)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            DoorMetadata meta = Metadata.Doors[(int)data.ModelId];
            int recolorId = 0;
            // AlimbicDoor, AlimbicThinDoor
            if (data.ModelId == 0 || data.ModelId == 3)
            {
                recolorId = Metadata.DoorPalettes[(int)data.PaletteId];
            }
            Recolor = recolorId;
            // in practice (actual palette indices, not the index into the metadata):
            // - standard = 0, 1, 2, 3, 4, 6
            // - morph ball = 0
            // - boss = 0
            // - thin = 0, 7
            ModelInstance inst = Read.GetModelInstance(meta.Name);
            _models.Add(inst);
            // todo: remove temporary code like this once animations are being selected properly
            inst.SetNodeAnim(-1);
            inst.SetMaterialAnim(-1);
            ModelInstance lockInst = Read.GetModelInstance(meta.LockName);
            _lockTransform = Matrix4.CreateTranslation(0, meta.LockOffset, 0);
            _models.Add(lockInst);
            // todo: use flags and room state to determine lock/color state
            lockInst.Active = false;
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            if (index == 1)
            {
                return Matrix4.CreateScale(inst.Model.Scale) * _transform * _lockTransform;
            }
            return base.GetModelTransform(inst, index);
        }

        protected override int GetModelRecolor(ModelInstance inst, int index)
        {
            if (index == 1)
            {
                return 0;
            }
            return base.GetModelRecolor(inst, index);
        }
    }

    public class FhDoorEntity : EntityBase
    {
        private readonly FhDoorEntityData _data;

        public FhDoorEntity(FhDoorEntityData data) : base(EntityType.Door)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = Read.GetModelInstance(Metadata.FhDoors[(int)data.ModelId], firstHunt: true);
            _models.Add(inst);
            // temporary
            inst.SetNodeAnim(-1);
            inst.SetMaterialAnim(-1);
        }
    }
}
