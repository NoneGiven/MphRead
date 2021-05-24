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
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
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
            ModelInstance inst = SetUpModel(meta.Name);
            if (_data.ModelId == 3)
            {
                inst.SetAnimation(1, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node, AnimFlags.None);
            }
            else
            {
                inst.SetAnimation(0, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node, AnimFlags.Ended | AnimFlags.NoLoop);
                inst.AnimInfo.Flags[0] |= AnimFlags.Reverse;
            }
            inst.SetAnimation(0, 1, SetFlags.Material, AnimFlags.Ended | AnimFlags.NoLoop);
            inst.AnimInfo.Flags[1] |= AnimFlags.Reverse;
            ModelInstance lockInst = SetUpModel(meta.LockName);
            _lockTransform = Matrix4.CreateTranslation(0, meta.LockOffset, 0);
            // todo: use flags and room state to determine lock/color state
            // todo: locking/unlocking -- requires updating animation frames
            lockInst.Active = false;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            scene.LoadEffect(144);
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
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = SetUpModel(Metadata.FhDoors[(int)data.ModelId], firstHunt: true);
            inst.SetAnimation(0, AnimFlags.Ended | AnimFlags.NoLoop);
        }
    }
}
