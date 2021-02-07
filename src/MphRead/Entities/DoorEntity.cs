using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class DoorEntity : VisibleEntityBase
    {
        private readonly DoorEntityData _data;
        private readonly Matrix4 _lockTransform;

        public DoorEntity(DoorEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
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
            NewModel model = Read.GetNewModel(meta.Name);
            _models.Add(model);
            // todo: remove temporary code like this once animations are being selected properly
            model.Animations.NodeGroupId = -1;
            model.Animations.MaterialGroupId = -1;
            NewModel doorLock = Read.GetNewModel(meta.LockName);
            _lockTransform = Matrix4.CreateTranslation(0, meta.LockOffset, 0);
            doorLock.Active = true; // todo: use flags to determine lock/color state
            _models.Add(doorLock);
        }

        protected override Matrix4 GetModelTransformBefore(NewModel model, int index)
        {
            if (index == 1)
            {
                return _lockTransform;
            }
            return base.GetModelTransformBefore(model, index);
        }
    }

    public class FhDoorEntity : VisibleEntityBase
    {
        private readonly FhDoorEntityData _data;

        public FhDoorEntity(FhDoorEntityData data) : base(NewEntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            NewModel model = Read.GetFhNewModel(Metadata.FhDoors[(int)data.ModelId]);
            _models.Add(model);
            // temporary
            model.Animations.NodeGroupId = -1;
            model.Animations.MaterialGroupId = -1;
        }
    }
}
