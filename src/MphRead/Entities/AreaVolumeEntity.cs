using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class AreaVolumeEntity : EntityBase
    {
        private readonly AreaVolumeEntityData _data;
        private EntityBase? _parent = null;
        private EntityBase? _child = null;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _insideEventColor;
        private readonly Vector3 _exitEventColor;

        public new bool Active { get; set; }
        private readonly int[] _cooldownSlots = new int[4];
        private readonly bool[] _triggeredSlots = new bool[4];
        private readonly uint[] _prioritySlots = new uint[4];
        private int _cooldownTime = 0;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();
        public AreaVolumeEntityData Data => _data;

        public AreaVolumeEntity(AreaVolumeEntityData data, string nodeName, Scene scene)
            : base(EntityType.AreaVolume, nodeName, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideMessage);
            _exitEventColor = Metadata.GetEventColor(data.ExitMessage);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
            for (int i = 0; i < 4; i++)
            {
                _prioritySlots[i] = _data.Priority;
            }
            _cooldownTime = _data.Cooldown;
            if (_cooldownTime > 0)
            {
                _cooldownTime--;
            }
            _cooldownTime *= 2; // todo: FPS stuff
            if (GameState.Mode == GameMode.SinglePlayer)
            {
                int state = GameState.StorySave.InitRoomState(_scene.RoomId, Id, active: data.Active != 0);
                if (data.AlwaysActive != 0)
                {
                    Active = data.Active != 0;
                }
                else
                {
                    Active = state != 0;
                }
            }
            else
            {
                Active = data.Active != 0;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_scene.TryGetEntity(_data.ParentId, out EntityBase? parent))
            {
                _parent = parent;
            }
            if (_scene.TryGetEntity(_data.ChildId, out EntityBase? child))
            {
                _child = child;
            }
        }

        public override bool GetTargetable()
        {
            return false;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Activate)
            {
                Active = true;
                if (GameState.Mode == GameMode.SinglePlayer)
                {
                    GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 3);
                }
            }
            else if (info.Message == Message.SetActive)
            {
                if ((int)info.Param1 != 0)
                {
                    Active = true;
                    if (GameState.Mode == GameMode.SinglePlayer)
                    {
                        GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 3);
                    }
                }
                else
                {
                    Active = false;
                    if (GameState.Mode == GameMode.SinglePlayer)
                    {
                        GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
                    }
                }
            }
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.AreaInside || _scene.ShowVolumes == VolumeDisplay.AreaExit)
            {
                Vector3 color = _scene.ShowVolumes == VolumeDisplay.AreaInside ? _insideEventColor : _exitEventColor;
                AddVolumeItem(_volume, color);
            }
        }

        public override EntityBase? GetParent()
        {
            return _parent;
        }

        public override EntityBase? GetChild()
        {
            return _child;
        }

        private void Trigger(PlayerEntity player)
        {
            if (!_triggeredSlots[player.SlotIndex])
            {
                SendInsideEvent(player);
            }
            else if (_data.AllowMultiple != 0)
            {
                int cooldown = _cooldownSlots[player.SlotIndex];
                if (cooldown > 0)
                {
                    _cooldownSlots[player.SlotIndex] = cooldown - 1;
                }
                else
                {
                    SendInsideEvent(player);
                    _cooldownSlots[player.SlotIndex] = _cooldownTime;
                }
            }
        }

        private void SendInsideEvent(PlayerEntity player)
        {
            _triggeredSlots[player.SlotIndex] = true;
            if (_data.AllowMultiple == 0)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.AreaVolume)
                    {
                        continue;
                    }
                    var other = (AreaVolumeEntity)entity;
                    if (other != this && other._parent == _parent
                        && other._triggeredSlots[player.SlotIndex]
                        && other.Data.InsideMessage == _data.InsideMessage
                        && other.Data.InsideMsgParam1 == _data.InsideMsgParam1
                        && other.Data.InsideMsgParam2 == _data.InsideMsgParam2)
                    {
                        return;
                    }
                }
            }
            Message message = _data.InsideMessage;
            if (message == Message.Damage || message == Message.Death || message == Message.Gravity
                || message == Message.PreventFormSwitch || message == Message.DripMoatPlatform)
            {
                _scene.SendMessage(message, this, player, _data.InsideMsgParam1, _data.InsideMsgParam2);
            }
            else if (message != Message.Unused22)
            {
                _scene.SendMessage(message, this, _parent, _data.InsideMsgParam1, _data.InsideMsgParam2, _data.MessageDelay);
            }
        }

        private void SendExitEvent(PlayerEntity player)
        {
            if (!_triggeredSlots[player.SlotIndex])
            {
                return;
            }
            _triggeredSlots[player.SlotIndex] = false;
            _prioritySlots[player.SlotIndex] = _data.Priority;
            _cooldownSlots[player.SlotIndex] = _cooldownTime;
            if (_data.InsideMessage == Message.Gravity)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.AreaVolume)
                    {
                        continue;
                    }
                    var other = (AreaVolumeEntity)entity;
                    other._prioritySlots[player.SlotIndex] = other.Data.Priority;
                }
            }
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.AreaVolume)
                {
                    continue;
                }
                var other = (AreaVolumeEntity)entity;
                if (other != this && other._child == _child
                    && other._triggeredSlots[player.SlotIndex]
                    && other.Data.ExitMessage == _data.ExitMessage
                    && other.Data.ExitMsgParam1 == _data.ExitMsgParam1
                    && other.Data.ExitMsgParam2 == _data.ExitMsgParam2)
                {
                    return;
                }
            }
            Message message = _data.ExitMessage;
            if (message == Message.Damage || message == Message.Death)
            {
                _scene.SendMessage(message, this, player, _data.ExitMsgParam1, _data.ExitMsgParam2);
            }
            else
            {
                _scene.SendMessage(message, this, _child, _data.ExitMsgParam1, _data.ExitMsgParam2, _data.MessageDelay);
            }
        }

        private bool PrioritizeGravity(Vector3 position, int slot)
        {
            bool result = true;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.AreaVolume)
                {
                    continue;
                }
                var other = (AreaVolumeEntity)entity;
                if (other.Data.InsideMessage == _data.InsideMessage && other._volume.TestPoint(position))
                {
                    if (other.Data.Priority > _prioritySlots[slot])
                    {
                        _prioritySlots[slot] = other.Data.Priority;
                    }
                    if (_data.Priority != _prioritySlots[slot])
                    {
                        result = false;
                    }
                }
            }
            return result;
        }

        public override bool Process()
        {
            if (!Active)
            {
                return base.Process();
            }
            TriggerFlags flags = _data.TriggerFlags;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (GameState.Mode == GameMode.SinglePlayer && player != PlayerEntity.Main)
                {
                    continue;
                }
                for (int j = 0; j < player.EquipInfo.Beams.Length; j++)
                {
                    BeamProjectileEntity beam = player.EquipInfo.Beams[j];
                    if (beam.Lifespan > 0)
                    {
                        CollisionResult discard = default;
                        // bug Omega Cannon can check against a higher bit than intended
                        if (((int)flags & (1 << (int)beam.Beam)) != 0
                            && (beam.Flags.TestFlag(BeamFlags.Charged) || !flags.TestFlag(TriggerFlags.BeamCharged)))
                        {
                            if (CollisionDetection.CheckCylinderOverlapVolume(_volume, beam.BackPosition, beam.Position,
                                radius: 0.1f, ref discard))
                            {
                                if (_data.InsideMessage == Message.Gravity)
                                {
                                    PrioritizeGravity(beam.Position, player.SlotIndex);
                                }
                                Trigger(player);
                            }
                            else
                            {
                                SendExitEvent(player);
                            }
                        }
                    }
                }
                if (player.IsAltForm && flags.TestFlag(TriggerFlags.PlayerAlt)
                    || !player.IsAltForm && flags.TestFlag(TriggerFlags.PlayerBiped))
                {
                    if (player.LoadFlags.TestFlag(LoadFlags.Spawned) && _volume.TestPoint(player.Position))
                    {
                        bool trigger = true;
                        if (_data.InsideMessage == Message.Gravity)
                        {
                            trigger = PrioritizeGravity(player.Position, player.SlotIndex);
                        }
                        if (trigger)
                        {
                            Trigger(player);
                        }
                    }
                    else
                    {
                        SendExitEvent(player);
                    }
                }
            }
            return base.Process();
        }
    }

    public class FhAreaVolumeEntity : EntityBase
    {
        private readonly FhAreaVolumeEntityData _data;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _insideEventColor;
        private readonly Vector3 _exitEventColor;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0x00).AsVector4();
        public FhAreaVolumeEntityData Data => _data;

        public FhAreaVolumeEntity(FhAreaVolumeEntityData data, Scene scene) : base(EntityType.AreaVolume, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _insideEventColor = Metadata.GetEventColor(data.InsideMessage);
            _exitEventColor = Metadata.GetEventColor(data.ExitMessage);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.ActiveVolume, Position);
            AddPlaceholderModel();
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.AreaInside || _scene.ShowVolumes == VolumeDisplay.AreaExit)
            {
                Vector3 color = _scene.ShowVolumes == VolumeDisplay.AreaInside ? _insideEventColor : _exitEventColor;
                AddVolumeItem(_volume, color);
            }
        }
    }
}
