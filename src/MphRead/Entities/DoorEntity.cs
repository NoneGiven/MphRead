using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class DoorEntity : EntityBase
    {
        private readonly DoorEntityData _data;
        private readonly Matrix4 _lockTransform;
        private readonly ModelInstance _lock;
        private AnimationInfo AnimInfo => _models[0].AnimInfo;

        public float Radius { get; }
        public float RadiusSquared { get; }
        public DoorFlags Flags { get; private set; } = DoorFlags.None;

        private bool Locked => Flags.TestFlag(DoorFlags.Locked);
        private bool Unlocked => Flags.TestFlag(DoorFlags.Unlocked);
        public Vector3 LockPosition => (_transform * _lockTransform).Row3.Xyz;

        public DoorEntity(DoorEntityData data) : base(EntityType.Door)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            DoorMetadata meta = Metadata.Doors[(int)data.DoorType];
            Radius = meta.Radius;
            RadiusSquared = Radius * Radius;
            int recolorId = 0;
            if (data.DoorType == DoorType.Standard || data.DoorType == DoorType.Thin)
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
            if (_data.DoorType == DoorType.Thin)
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
            _lock = SetUpModel(meta.LockName);
            _lockTransform = Matrix4.CreateTranslation(0, meta.LockOffset, 0);
            // todo: scan ID
            // todo: use flags and room state to determine lock/color state
            if (_data.Locked != 0)
            {
                Flags |= DoorFlags.Locked;
            }
            Flags |= DoorFlags.Closed;
            if (_data.PaletteId == 9) // any beam door
            {
                Flags |= DoorFlags.ShowLock;
            }
            // todo: connector/room IDs, node refs, ports
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            scene.LoadEffect(114); // lockDefeat - todo: load in entity setup
        }

        public override bool Process(Scene scene)
        {
            if (Unlocked && _lock.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                Flags &= ~DoorFlags.Locked;
                // todo: room state
            }
            // todo: update scan ID
            if (Locked && !Unlocked)
            {
                Flags &= ~DoorFlags.ShotOpen;
            }
            // todo: only update this when not in room transition
            if (ShouldOpen(scene))
            {
                Flags |= DoorFlags.ShouldOpen;
            }
            else
            {
                Flags &= ~DoorFlags.ShouldOpen;
            }
            // todo: if should open and this is a loading door, clear should open and start room transition
            // todo: update SFX
            if (Flags.TestFlag(DoorFlags.ShouldOpen))
            {
                if (AnimInfo.Index[0] != 0)
                {
                    _models[0].SetAnimation(0, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node, AnimFlags.NoLoop);
                    // todo: play SFX
                }
                else if (AnimInfo.Flags[0].TestFlag(AnimFlags.Ended) && AnimInfo.Flags[0].TestFlag(AnimFlags.Reverse))
                {
                    AnimInfo.Flags[0] &= ~AnimFlags.Ended;
                    AnimInfo.Flags[0] &= ~AnimFlags.Paused;
                    AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                    // todo: play SFX
                }
            }
            else if (AnimInfo.Index[0] == 0 && AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                if (!AnimInfo.Flags[0].TestFlag(AnimFlags.Reverse))
                {
                    Flags |= DoorFlags.Closed;
                    AnimInfo.Flags[0] &= ~AnimFlags.Ended;
                    AnimInfo.Flags[0] &= ~AnimFlags.Paused;
                    AnimInfo.Flags[0] |= AnimFlags.Reverse;
                    AnimInfo.Flags[1] = AnimInfo.Flags[0];
                    // todo: play SFX
                }
                else if (_data.DoorType == DoorType.Thin)
                {
                    _models[0].SetAnimation(1, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node);
                }
            }
            if (Flags.TestFlag(DoorFlags.ShotOpen))
            {
                if (_data.DoorType == DoorType.Boss && AnimInfo.Flags[1].TestFlag(AnimFlags.Reverse))
                {
                    // todo: play SFX
                }
                AnimInfo.Flags[1] &= ~AnimFlags.Ended;
                AnimInfo.Flags[1] &= ~AnimFlags.Paused;
                AnimInfo.Flags[1] &= ~AnimFlags.Reverse;
            }
            else if (_data.DoorType == DoorType.Boss && AnimInfo.Flags[1].TestFlag(AnimFlags.Reverse))
            {
                // todo: play SFX
            }
            UpdateAnimFrames(_models[0], scene);
            UpdateAnimFrames(_models[1], scene);
            bool activatePortal = false;
            if (Flags.TestFlag(DoorFlags.ShouldOpen))
            {
                // todo: FPS stuff
                if (AnimInfo.Frame[0] > AnimInfo.FrameCount[0] / 2)
                {
                    Flags |= DoorFlags.Open;
                }
                if (_data.DoorType != DoorType.Standard || AnimInfo.Frame[0] >= 10)
                {
                    activatePortal = true;
                }
            }
            else
            {
                if (Flags.TestFlag(DoorFlags.Closed))
                {
                    if (_data.DoorType == DoorType.Thin)
                    {
                        if (AnimInfo.Index[0] == 0)
                        {
                            activatePortal = true;
                        }
                    }
                    else if (!AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                    {
                        activatePortal = true;
                    }
                }
                else
                {
                    activatePortal = true;
                }
                Flags &= ~DoorFlags.Open;
            }
            // todo: port flags
            Flags &= ~DoorFlags.Opening;
            Flags &= ~DoorFlags.Bit10;
            if (Flags.TestFlag(DoorFlags.ShouldOpen))
            {
                Flags |= DoorFlags.Opening;
            }
            if (Flags.TestFlag(DoorFlags.Bit8))
            {
                Flags |= DoorFlags.Bit10;
            }
            if (Locked && Flags.TestFlag(DoorFlags.ShowLock) && !Flags.TestFlag(DoorFlags.ShouldOpen)
                && (AnimInfo.Index[0] != 0 || AnimInfo.Flags[0].TestFlag(AnimFlags.Ended)))
            {
                // todo: bits 8/9 and 10 are basically a counter and a bool, and we should just replace them with that
                if (Flags.TestFlag(DoorFlags.Bit10) && scene.FrameCount > 3 * 2) // todo: FPS stuff
                {
                    // todo: play SFX
                }
                int flags = (int)Flags;
                int bits = (flags << 22) >> 30;
                if (bits < 2)
                {
                    bits = (bits + 1) & 3;
                    flags |= bits << 8;
                    Flags = (DoorFlags)flags;
                }
            }
            else
            {
                Flags &= ~DoorFlags.Bit8;
                Flags &= ~DoorFlags.Bit9;
            }
            return true;
        }

        private bool ShouldOpen(Scene scene)
        {
            if (Locked || !Flags.TestFlag(DoorFlags.ShotOpen))
            {
                return false;
            }
            for (int i = 0; i < scene.Entities.Count; i++)
            {
                EntityBase entity = scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health > 0 && !player.IsBot && (Position - player.Position).LengthSquared < 16)
                {
                    return true;
                }
            }
            if (Flags.TestFlag(DoorFlags.Opening))
            {
                Flags &= ~DoorFlags.ShotOpen;
            }
            return false;
        }

        public void Lock(bool updateState, Scene scene)
        {
            Flags |= DoorFlags.Locked;
            if (updateState)
            {
                // todo: if 1P mode, update room state
            }
        }

        public void Unlock(bool updateState, bool sfxBool, Scene scene)
        {
            // todo: return if in room transition
            Flags |= DoorFlags.Unlocked;
            // todo: something with SFX
            _lock.SetAnimation(1, AnimFlags.NoLoop);
            scene.SpawnEffect(114, UpVector, FacingVector, LockPosition); // lockDefeat
            if (updateState)
            {
                // todo: if 1P mode, update room state
            }
        }

        public override void HandleMessage(MessageInfo info, Scene scene)
        {
            if (info.Message == Message.Unlock)
            {
                Unlock(updateState: true, sfxBool: false, scene);
            }
            else if (info.Message == Message.Lock)
            {
                Lock(updateState: true, scene);
            }
            else if (info.Message == Message.UnlockConnectors)
            {
                for (int i = 0; i < scene.Entities.Count; i++)
                {
                    EntityBase entity = scene.Entities[i];
                    if (entity.Type == EntityType.Door && entity.Id == -1)
                    {
                        ((DoorEntity)entity).Unlock(updateState: true, sfxBool: false, scene);
                    }
                }
            }
            else if (info.Message == Message.LockConnectors)
            {
                for (int i = 0; i < scene.Entities.Count; i++)
                {
                    EntityBase entity = scene.Entities[i];
                    if (entity.Type == EntityType.Door && entity.Id == -1)
                    {
                        ((DoorEntity)entity).Lock(updateState: true, scene);
                    }
                }
            }
        }

        public override void Destroy(Scene scene)
        {
            // todo: stop SFX
            base.Destroy(scene);
        }

        public override void GetDrawInfo(Scene scene)
        {
            // todo?: is_visible
            _lock.Active = false;
            if (Locked && Flags.TestFlag(DoorFlags.ShowLock) && !Flags.TestFlag(DoorFlags.ShouldOpen)
                && (AnimInfo.Index[0] != 0 || AnimInfo.Flags[0].TestFlag(AnimFlags.Ended)))
            {
                _lock.Active = true;
            }
            base.GetDrawInfo(scene);
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

    public enum DoorFlags : ushort
    {
        None = 0,
        Loaded = 1,
        Locked = 2,
        Unlocked = 4,
        ShotOpen = 8,
        Opening = 0x10,
        ShouldOpen = 0x20,
        Open = 0x40,
        Closed = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800, // unused?
        ShowLock = 0x1000
    }
}
