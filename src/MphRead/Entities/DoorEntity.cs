using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats.Collision;
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
        public int TargetRoomId { get; } = -1;
        public DoorEntity? LoaderDoor { get; set; }
        public DoorEntity? ConnectorDoor { get; set; }
        public Portal? Portal { get; private set; }
        public ModelInstance? ConnectorModel { get; set; }
        public CollisionInstance? ConnectorCollision { get; set; }
        public bool ConnectorInactive { get; set; }

        private bool Locked => Flags.TestFlag(DoorFlags.Locked);
        private bool Unlocked => Flags.TestFlag(DoorFlags.Unlocked);
        public Vector3 LockPosition => (_transform * _lockTransform).Row3.Xyz;
        public DoorEntityData Data => _data;

        private static readonly IReadOnlyList<int> _scanIds = new int[10]
        {
            0, 255, 264, 252, 256, 253, 254, 249, 266, 265
        };

        public DoorEntity(DoorEntityData data, string nodeName, Scene scene, int targetRoomId = -1)
            : base(EntityType.Door, nodeName, scene)
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
            Debug.Assert(scene.GameMode == GameMode.SinglePlayer);
            int state = GameState.StorySave.InitRoomState(_scene.RoomId, Id, active: _data.Locked != 0);
            if (state != 0 && !Cheats.UnlockAllDoors)
            {
                Flags |= DoorFlags.Locked;
            }
            UpdateScanId();
            Flags |= DoorFlags.Closed;
            if (_data.PaletteId == 9) // any beam door
            {
                Flags |= DoorFlags.ShowLock;
            }
            TargetRoomId = targetRoomId;
            if (TargetRoomId == -1 && _data.EntityFilename[0] != '\0')
            {
                for (int i = 0; i < Metadata.RoomList.Count; i++)
                {
                    RoomMetadata room = Metadata.RoomList[i];
                    string? filename = room.EntityFilename;
                    if (filename != null && Compare(_data.EntityFilename, filename))
                    {
                        TargetRoomId = room.Id;
                        break;
                    }
                }
            }
            if (_data.ConnectorId != 255 && TargetRoomId == -1)
            {
                throw new ProgramException("Loader door failed to find target room.");
            }
        }

        private bool Compare(ReadOnlySpan<char> data, ReadOnlySpan<char> room)
        {
            return MemoryExtensions.StartsWith(room, data[..15], StringComparison.InvariantCultureIgnoreCase);
        }

        public override void Initialize()
        {
            base.Initialize();
            _scene.LoadEffect(114); // lockDefeat
            if (_data.ConnectorId != 255 && _scene.Room != null)
            {
                _scene.Room.AddConnector(this);
            }
        }

        private const float _portWidth = 2.1f;
        private static readonly IReadOnlyList<float> _portHeights = new float[4]
        {
            3.4f, 3.4f, 6.4f, 3.4f
        };

        public Portal SetUpPort(string roomNodeName, string conNodeName)
        {
            float height = _portHeights[(int)Data.DoorType];
            Vector3 facing = FacingVector;
            Vector3 pos = Position;
            Vector3 up = UpVector;
            Vector3 negUp = -up;
            Vector3 right = Vector3.Cross(facing, up).Normalized();
            Vector3 negRight = -right;
            Vector3 widthVec = right * _portWidth;
            Vector3 heightVec = up * height;
            var points = new List<Vector3>(4)
            {
                pos - widthVec,
                pos - widthVec + heightVec,
                pos + widthVec + heightVec,
                pos + widthVec
            };
            var plane = new Vector4(facing, Vector3.Dot(facing, pos));
            // unlike with portals in collision files, these planes are computed
            // based on the assumption that doors are always axis aligned
            var planes = new List<Vector4>(4)
            {
                new Vector4(negRight, Vector3.Dot(negRight, points[3])),
                new Vector4(negUp, Vector3.Dot(negUp, points[2])),
                new Vector4(right, Vector3.Dot(right, points[1])),
                new Vector4(up, Vector3.Dot(up, points[0]))
            };
            Portal = new Portal(roomNodeName, conNodeName, points, planes, plane);
            Portal.Active = false;
            return Portal;
        }

        private void UpdateScanId()
        {
            if (_data.DoorType == DoorType.Boss)
            {
                _scanId = 269;
            }
            else if (Flags.TestFlag(DoorFlags.Locked))
            {
                _scanId = _scanIds[(int)_data.PaletteId];
            }
            else
            {
                _scanId = 251;
            }
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

        public override int GetScanId(bool alternate = false)
        {
            if (Flags.TestFlag(DoorFlags.ShouldOpen))
            {
                return 0;
            }
            return _scanId;
        }

        public override bool Process()
        {
            if (ConnectorInactive)
            {
                Flags &= ~DoorFlags.ShotOpen;
                Flags &= ~DoorFlags.ShouldOpen;
                ForceClose();
            }
            if (Unlocked && _lock.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                Flags &= ~DoorFlags.Locked;
                GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
            }
            UpdateScanId();
            if (Locked && !Unlocked)
            {
                Flags &= ~DoorFlags.ShotOpen;
            }
            if (!GameState.InRoomTransition)
            {
                if (ShouldOpen())
                {
                    Flags |= DoorFlags.ShouldOpen;
                }
                else
                {
                    Flags &= ~DoorFlags.ShouldOpen;
                }
            }
            if (Flags.TestFlag(DoorFlags.ShouldOpen) && _data.ConnectorId == 255 && TargetRoomId >= 0)
            {
                // the game also checks for loading connectors behind doors, but we do that at room load
                Flags &= ~DoorFlags.ShouldOpen;
                GameState.TransitionState = TransitionState.Start;
                Debug.Assert(_scene.Room != null && _scene.Room.LoaderDoor == null);
                _scene.Room.LoaderDoor = this;
                GameState.TransitionRoomId = TargetRoomId;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.Door)
                    {
                        var other = (DoorEntity)entity;
                        if (other.LoaderDoor == this)
                        {
                            other.ForceClose();
                        }
                    }
                }
            }
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
            bool portalActive = false;
            if (Flags.TestFlag(DoorFlags.ShouldOpen))
            {
                if (ConnectorModel?.Active == false)
                {
                    Debug.Assert(_scene.Room != null);
                    _scene.Room.ActivateConnector(this);
                }
                // todo: FPS stuff
                if (AnimInfo.Frame[0] > AnimInfo.FrameCount[0] / 2)
                {
                    Flags |= DoorFlags.Open;
                }
                if (_data.DoorType != DoorType.Standard || AnimInfo.Frame[0] >= 10)
                {
                    portalActive = true;
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
                            portalActive = true;
                        }
                    }
                    else if (!AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                    {
                        portalActive = true;
                    }
                }
                else
                {
                    portalActive = true;
                }
                Flags &= ~DoorFlags.Open;
            }
            if (Portal != null)
            {
                Portal.Active = portalActive;
            }
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
                uint flags = (uint)Flags;
                uint bits = (flags << 22) >> 30;
                if (bits < 2)
                {
                    bits = (bits + 1) & 3;
                    flags &= 0xFFFFFCFF;
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

        public void SetAnimationFrame(int frame)
        {
            _models[0].AnimInfo.Frame[1] = frame;
        }

        public int GetAnimationFrame()
        {
            return _models[0].AnimInfo.Frame[1];
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

        private void ForceClose()
        {
            ModelInstance inst = _models[0];
            if (_data.DoorType == DoorType.Thin)
            {
                inst.SetAnimation(1, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node, AnimFlags.None);
            }
            else
            {
                inst.SetAnimation(0, 0, SetFlags.Texture | SetFlags.Texcoord | SetFlags.Node, AnimFlags.Ended | AnimFlags.NoLoop);
                inst.AnimInfo.Flags[0] |= AnimFlags.Reverse;
            }
            if (Portal != null)
            {
                Portal.Active = false;
            }
        }

        public void Lock(bool updateState)
        {
            Flags |= DoorFlags.Locked;
            if (updateState)
            {
                GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 3);
            }
        }

        public void Unlock(bool updateState, bool noLockAnimSfx)
        {
            if (GameState.InRoomTransition)
            {
                return;
            }
            Flags |= DoorFlags.Unlocked;
            PlayerEntity.Main.DoorChimeSfxTimer = 2 / 30f;
            if (!noLockAnimSfx)
            {
                PlayerEntity.Main.DoorUnlockSfxTimer = 2 / 30f;
            }
            _lock.SetAnimation(1, AnimFlags.NoLoop);
            _scene.SpawnEffect(114, UpVector, FacingVector, LockPosition); // lockDefeat
            if (updateState)
            {
                GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Unlock)
            {
                Unlock(updateState: true, noLockAnimSfx: false);
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
                        ((DoorEntity)entity).Unlock(updateState: true, noLockAnimSfx: false);
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
            _soundSource.StopSfx(SfxId.DOOR_OPEN);
            LoaderDoor = null;
            ConnectorDoor = null;
            Portal = null;
            ConnectorModel = null;
            ConnectorCollision = null;
        }

        public override void GetDrawInfo()
        {
            if (ConnectorInactive)
            {
                return;
            }
            if (!IsVisible(NodeRef) && (Portal == null || !IsVisible(Portal.NodeRef2)))
            {
                return;
            }
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
            if (index == 1 || !Flags.TestFlag(DoorFlags.Locked))
            {
                return 0;
            }
            return Recolor;
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
