using System;
using MphRead.Formats;
using OpenTK.Graphics.GL;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TriggerVolumeEntity : EntityBase
    {
        private readonly TriggerVolumeEntityData _data;
        private EntityBase? _parent = null;
        private EntityBase? _child = null;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _parentEventColor;
        private readonly Vector3 _childEventColor;

        public new bool Active { get; set; }
        private int _count = 0;
        private int _delayTimer = 0;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();
        public TriggerVolumeEntityData Data => _data;

        public TriggerVolumeEntity(TriggerVolumeEntityData data, Scene scene) : base(EntityType.TriggerVolume, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            // todo: change the display/color when inactive (same for AreaVolumes)
            _parentEventColor = Metadata.GetEventColor(data.ParentMessage);
            _childEventColor = Metadata.GetEventColor(data.ChildMessage);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            AddPlaceholderModel();
            // todo: room state
            Active = data.Active != 0 || data.AlwaysActive != 0;
            _delayTimer = _data.RepeatDelay * 2; // todo: FPS stuff
            // todo: music event stuff
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

        public override void GetDisplayVolumes()
        {
            if (_data.Subtype == TriggerType.Volume &&
                (_scene.ShowVolumes == VolumeDisplay.TriggerParent || _scene.ShowVolumes == VolumeDisplay.TriggerChild))
            {
                Vector3 color = _scene.ShowVolumes == VolumeDisplay.TriggerParent ? _parentEventColor : _childEventColor;
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

        private bool Trigger()
        {
            if (_delayTimer > 0)
            {
                _delayTimer--;
                return false;
            }
            if (_data.ParentMessage != Message.None)
            {
                if (_data.DeactivateAfterUse != 0)
                {
                    Deactivate();
                }
                _scene.SendMessage(_data.ParentMessage, this, _parent, _data.ParentMsgParam1, _data.ParentMsgParam2);
            }
            if (_data.ChildMessage != Message.None)
            {
                if (_data.DeactivateAfterUse != 0)
                {
                    Deactivate();
                }
                _scene.SendMessage(_data.ChildMessage, this, _child, _data.ChildMsgParam1, _data.ChildMsgParam2);
            }
            _delayTimer = _data.RepeatDelay * 2; // todo: FPS stuff
            if (_data.Subtype == TriggerType.StateBits)
            {
                Active = false;
            }
            return true;
        }

        public override bool Process()
        {
            if (!Active)
            {
                return base.Process();
            }
            if (_data.Subtype == TriggerType.Volume)
            {
                bool colliding = false;
                TriggerFlags flags = _data.TriggerFlags;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    for (int j = 0; j < player.EquipInfo.Beams.Length; j++)
                    {
                        BeamProjectileEntity beam = player.EquipInfo.Beams[j];
                        if (beam.Lifespan > 0)
                        {
                            CollisionResult discard = default;
                            // bug Omega Cannon (and platform/enemy beams) can chekc against higher bits than intended
                            if (((int)flags & (1 << (int)beam.WeaponType)) != 0
                                && (beam.Flags.TestFlag(BeamFlags.Charged) || !flags.TestFlag(TriggerFlags.BeamCharged))
                                && CollisionDetection.CheckCylinderOverlapVolume(_volume, beam.BackPosition, beam.Position,
                                    radius: 0.1f, ref discard))
                            {
                                Trigger();
                                colliding = true;
                                break;
                            }
                        }
                    }
                    if ((!player.IsBot || flags.TestFlag(TriggerFlags.IncludeBots))
                        && (player.IsAltForm && flags.TestFlag(TriggerFlags.PlayerAlt)
                        || !player.IsAltForm && flags.TestFlag(TriggerFlags.PlayerBiped))
                        && player.LoadFlags.TestFlag(LoadFlags.Spawned) && _volume.TestPoint(player.Position))
                    {
                        Trigger();
                        colliding = true;
                        break;
                    }
                }
                if (!colliding && _data.CheckDelay != 0)
                {
                    _delayTimer = _data.CheckDelay * 2; // todo: FPS stuff
                }
            }
            else if (_data.Subtype == TriggerType.Threshold)
            {
                if (_count == _data.TriggerThreshold && Trigger())
                {
                    _count = 0;
                }
            }
            else if (_data.Subtype == TriggerType.Automatic)
            {
                if (Trigger() && _data.DeactivateAfterUse != 0)
                {
                    Deactivate();
                }
            }
            else if (_data.Subtype == TriggerType.StateBits)
            {
                // todo: check global state bits
            }
            return base.Process();
        }

        private void Deactivate()
        {
            Active = false;
            // todo: room state
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (_data.Subtype == TriggerType.Relay)
            {
                if (_parent != null)
                {
                    _scene.SendMessage(info.Message, info.Sender, _parent, info.Param1, info.Param2);
                }
                if (_child != null)
                {
                    _scene.SendMessage(info.Message, info.Sender, _child, info.Param1, info.Param2);
                }
            }
            else
            {
                if (info.Message == Message.Trigger)
                {
                    if (_data.Subtype == TriggerType.Threshold)
                    {
                        if (_count < _data.TriggerThreshold)
                        {
                            _count++;
                        }
                        if (_count == _data.TriggerThreshold)
                        {
                            _delayTimer = _data.CheckDelay * 2; // todo: FPS stuff
                        }
                    }
                }
                else if (info.Message == Message.Activate)
                {
                    Active = true;
                    // todo: room state
                }
                else if (info.Message == Message.SetActive)
                {
                    if ((int)info.Param1 != 0)
                    {
                        Active = true;
                        // todo: room state
                    }
                    else
                    {
                        Active = false;
                        if (_data.Subtype == TriggerType.Automatic)
                        {
                            _delayTimer = _data.RepeatDelay * 2; // todo: FPS stuff
                        }
                        // todo: room state
                    }
                }
            }
        }
    }

    // area volumes don't use IncludeBots; jump pads only use PlayerBiped and PlayerAlt
    [Flags]
    public enum TriggerFlags : uint
    {
        None = 0x0,
        PowerBeam = 0x1,
        VoltDriver = 0x2,
        Missile = 0x4,
        Battlehammer = 0x8,
        Imperialist = 0x10,
        Judicator = 0x20,
        Magmaul = 0x40,
        ShockCoil = 0x80,
        BeamCharged = 0x100,
        PlayerBiped = 0x200,
        PlayerAlt = 0x400,
        Bit11 = 0x800, // unused
        IncludeBots = 0x1000
    }

    public class FhTriggerVolumeEntity : EntityBase
    {
        private readonly FhTriggerVolumeEntityData _data;
        private EntityBase? _parent = null;
        private EntityBase? _child = null;

        private readonly CollisionVolume _volume;
        private readonly Vector3 _parentEventColor;
        private readonly Vector3 _childEventColor;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x8C, 0x00).AsVector4();
        public FhTriggerVolumeEntityData Data => _data;

        public FhTriggerVolumeEntity(FhTriggerVolumeEntityData data, Scene scene) : base(EntityType.TriggerVolume, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _parentEventColor = Metadata.GetEventColor(data.ParentMessage);
            _childEventColor = Metadata.GetEventColor(data.ChildMessage);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.ActiveVolume, Position);
            AddPlaceholderModel();
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

        public override void GetDisplayVolumes()
        {
            if (_data.Subtype != FhTriggerType.Threshold &&
                (_scene.ShowVolumes == VolumeDisplay.TriggerParent || _scene.ShowVolumes == VolumeDisplay.TriggerChild))
            {
                Vector3 color = _scene.ShowVolumes == VolumeDisplay.TriggerParent ? _parentEventColor : _childEventColor;
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
    }

    // jump pads only use PlayerBiped and PlayerAlt
    public enum FhTriggerFlags : uint
    {
        None = 0x0,
        Beam = 0x1,
        PlayerBiped = 0x2,
        PlayerAlt = 0x4
    }
}
