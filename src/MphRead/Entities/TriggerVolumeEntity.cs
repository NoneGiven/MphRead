using System;
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
            if (_data.Subtype == TriggerType.Normal &&
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

        public override bool Process()
        {
            // todo: add "cutscene triggers active" toggle and use this code w/ parent/child refs
            //if (Id == 17 && Active && _volume.TestPoint(scene.CameraPosition))
            //{
            //    if (scene.TryGetEntity(18, out EntityBase? entity) && entity.Type == EntityType.CameraSequence)
            //    {
            //        Active = false;
            //        var trigger = (CameraSequenceEntity)entity;
            //        trigger.SetActive(true);
            //    } 
            //}
            return base.Process();
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
