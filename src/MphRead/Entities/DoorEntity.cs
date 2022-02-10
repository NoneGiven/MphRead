using System;
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
        public DoorFlags Flags { get; set; } = DoorFlags.None;

        private bool Locked => Flags.TestFlag(DoorFlags.Locked);
        private bool Unlocked => Flags.TestFlag(DoorFlags.Unlocked);
        public Vector3 LockPosition => (_transform * _lockTransform).Row3.Xyz;
        public DoorEntityData Data => _data;

        public DoorEntity(DoorEntityData data, Scene scene) : base(EntityType.Door, scene)
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

        public override void Initialize()
        {
            base.Initialize();
            _scene.LoadEffect(114); // lockDefeat - todo: load in entity setup
        }

        public override void GetPosition(out Vector3 position)
        {
            position = LockPosition;
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = LockPosition;
            up = UpVector;
            facing = FacingVector;
        }

        public override bool Process()
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
            if (ShouldOpen())
            {
                Flags |= DoorFlags.ShouldOpen;
            }
            else
            {
                Flags &= ~DoorFlags.ShouldOpen;
            }
            // todo: if should open and this is a loading door, clear should open and start room transition
            _soundSource.Update(Position, rangeIndex: 6);
            if (Flags.TestFlag(DoorFlags.ShouldOpen))
            {
                if (AnimInfo.Index[0] != 0)
                {
                    _models[0].SetAnimation(0, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node, AnimFlags.NoLoop);
                    _soundSource.PlaySfx(SfxId.DOOR3_OPEN_SCR);
                }
                else if (AnimInfo.Flags[0].TestFlag(AnimFlags.Ended) && AnimInfo.Flags[0].TestFlag(AnimFlags.Reverse))
                {
                    AnimInfo.Flags[0] &= ~AnimFlags.Ended;
                    AnimInfo.Flags[0] &= ~AnimFlags.Paused;
                    AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                    _soundSource.PlaySfx(_data.DoorType == DoorType.Boss ? SfxId.DOOR2_OPEN : SfxId.DOOR_OPEN);
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
                    SfxId sfx = _data.DoorType switch
                    {
                        DoorType.Boss => SfxId.DOOR2_CLOSE_SCR,
                        DoorType.Thin => SfxId.DOOR3_CLOSE_SCR,
                        _ => SfxId.DOOR_CLOSE
                    };
                    _soundSource.PlaySfx(sfx);
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
                    _soundSource.StopSfx(SfxId.DOOR2_LOOP);
                    if (AnimInfo.Frame[1] < 2)
                    {
                        _soundSource.PlaySfx(SfxId.DOOR2_PRE_OPEN, recency: Single.MaxValue, sourceOnly: true);
                    }
                }
                AnimInfo.Flags[1] &= ~AnimFlags.Ended;
                AnimInfo.Flags[1] &= ~AnimFlags.Paused;
                AnimInfo.Flags[1] &= ~AnimFlags.Reverse;
            }
            else if (_data.DoorType == DoorType.Boss && AnimInfo.Flags[1].TestFlag(AnimFlags.Reverse))
            {
                _soundSource.PlaySfx(SfxId.DOOR2_LOOP, loop: true);
            }
            UpdateAnimFrames(_models[0]);
            UpdateAnimFrames(_models[1]);
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
                if (Flags.TestFlag(DoorFlags.Bit10) && _scene.FrameCount > 3 * 2) // todo: FPS stuff
                {
                    _soundSource.PlaySfx(SfxId.LOCK_ANIM, recency: Single.MaxValue, sourceOnly: true);
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

        private bool ShouldOpen()
        {
            if (Locked || !Flags.TestFlag(DoorFlags.ShotOpen))
            {
                return false;
            }
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
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

        public void Lock(bool updateState)
        {
            Flags |= DoorFlags.Locked;
            if (updateState)
            {
                // todo: if 1P mode, update room state
            }
        }

        public void Unlock(bool updateState, bool sfxBool)
        {
            // todo: return if in room transition
            Flags |= DoorFlags.Unlocked;
            // todo: something with SFX
            _lock.SetAnimation(1, AnimFlags.NoLoop);
            _scene.SpawnEffect(114, UpVector, FacingVector, LockPosition); // lockDefeat
            if (updateState)
            {
                // todo: if 1P mode, update room state
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Unlock)
            {
                Unlock(updateState: true, sfxBool: false);
            }
            else if (info.Message == Message.Lock)
            {
                Lock(updateState: true);
            }
            else if (info.Message == Message.UnlockConnectors)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.Door && entity.Id == -1)
                    {
                        ((DoorEntity)entity).Unlock(updateState: true, sfxBool: false);
                    }
                }
            }
            else if (info.Message == Message.LockConnectors)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.Door && entity.Id == -1)
                    {
                        ((DoorEntity)entity).Lock(updateState: true);
                    }
                }
            }
        }

        public override void Destroy()
        {
            // todo: stop SFX
            base.Destroy();
        }

        public override void GetDrawInfo()
        {
            // todo?: is_visible
            _lock.Active = false;
            if (Locked && Flags.TestFlag(DoorFlags.ShowLock) && !Flags.TestFlag(DoorFlags.ShouldOpen)
                && (AnimInfo.Index[0] != 0 || AnimInfo.Flags[0].TestFlag(AnimFlags.Ended)))
            {
                _lock.Active = true;
            }
            base.GetDrawInfo();
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

        public FhDoorEntity(FhDoorEntityData data, Scene scene) : base(EntityType.Door, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = SetUpModel(Metadata.FhDoors[(int)data.ModelId], firstHunt: true);
            inst.SetAnimation(0, AnimFlags.Ended | AnimFlags.NoLoop);
        }
    }

    [Flags]
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
