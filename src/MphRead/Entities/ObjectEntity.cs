using System;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ObjectEntity : EntityBase
    {
        private readonly ObjectEntityData _data;
        private CollisionVolume _effectVolume;
        private uint _flags = 0;
        private int _effectIntervalTimer = 0;
        private int _effectIntervalIndex = 0;
        private bool _effectProcessing = false;
        private EffectEntry? _effectEntry = null;
        public bool _effectActive = false;
        private readonly bool _scanVisorOnly = false;

        // used for ID -1 (scan point, effect spawner)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x22, 0x8B, 0x22).AsVector4();
        public ObjectEntityData Data => _data;

        public ObjectEntity(ObjectEntityData data) : base(EntityType.Object)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            _flags = data.Flags;
            // todo: bits 0 and 1 should be cleared if entity ID is -1 (and they should also be affected by room state otherwise)
            _flags &= 0xFB;
            _flags &= 0xF7;
            _flags &= 0xEF;
            if (data.EffectId > 0 && (data.EffectFlags & 1) != 0)
            {
                _effectVolume = CollisionVolume.Transform(data.Volume, Transform);
            }
            if (data.ModelId == UInt32.MaxValue)
            {
                AddPlaceholderModel();
                // todo: this also applies for other models depending on the anim ID
                _flags |= 4;
                // todo: this should get cleared if there's an effect ID and "is_visible" returns false
                _flags |= 0x10;
            }
            else
            {
                ObjectMetadata meta = Metadata.GetObjectById((int)data.ModelId);
                Recolor = meta.RecolorId;
                ModelInstance inst = Read.GetModelInstance(meta.Name);
                if (meta != null && meta.AnimationIds[0] == 0xFF)
                {
                    inst.SetNodeAnim(-1);
                    inst.SetMaterialAnim(-1);
                    inst.SetTexcoordAnim(-1);
                    inst.SetTexcoordAnim(-1);
                }
                // AlimbicGhost_01, GhostSwitch
                if (data.ModelId == 0 || data.ModelId == 41)
                {
                    _scanVisorOnly = true;
                }
                _models.Add(inst);
                // temporary
                if (inst.Model.Name == "AlimbicCapsule")
                {
                    inst.SetNodeAnim(-1);
                    inst.SetMaterialAnim(-1);
                }
                else if (inst.Model.Name == "WallSwitch")
                {
                    inst.SetNodeAnim(-1);
                    inst.SetMaterialAnim(-1);
                }
                else if (inst.Model.Name == "SniperTarget")
                {
                    inst.SetNodeAnim(-1);
                }
                else if (inst.Model.Name == "SecretSwitch")
                {
                    inst.SetNodeAnim(-1);
                    inst.SetMaterialAnim(-1);
                }
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.EffectId > 0)
            {
                scene.LoadEffect((int)_data.EffectId);
            }
        }

        public override void Destroy(Scene scene)
        {
            if (_effectEntry != null)
            {
                scene.UnlinkEffectEntry(_effectEntry);
            }
        }

        public override bool Process(Scene scene)
        {
            ShouldDraw = !_scanVisorOnly || scene.ScanVisor;
            if (_data.EffectId != 0 && scene.FrameCount % 2 == 0)
            {
                bool processEffect = false;
                if ((_data.EffectFlags & 0x40) != 0)
                {
                    processEffect = true;
                }
                else if ((_flags & 0x10) != 0)
                {
                    if ((_data.EffectFlags & 1) != 0)
                    {
                        // todo: add an option to disable this check
                        processEffect = _effectVolume.TestPoint(scene.CameraPosition);
                    }
                    else
                    {
                        processEffect = (_flags & 3) != 0;
                    }
                }
                if (processEffect)
                {
                    if (!_effectProcessing)
                    {
                        _effectIntervalTimer = 0;
                        _effectIntervalIndex = 15;
                    }
                    if (--_effectIntervalTimer > 0)
                    {
                        // todo: lots of SFX stuff
                    }
                    else
                    {
                        _effectIntervalIndex++;
                        _effectIntervalIndex %= 16;
                        if ((_data.EffectFlags & 0x10) != 0)
                        {
                            bool previouslyActive = _effectActive;
                            _effectActive = (_data.EffectOnIntervals & (1 << _effectIntervalIndex)) != 0;
                            if (_effectActive != previouslyActive)
                            {
                                if (!_effectActive)
                                {
                                    RemoveEffect(scene);
                                }
                                else
                                {
                                    _effectEntry = scene.SpawnEffectGetEntry((int)_data.EffectId, Transform);
                                    for (int i = 0; i < _effectEntry.Elements.Count; i++)
                                    {
                                        EffectElementEntry element = _effectEntry.Elements[i];
                                        element.Flags |= 0x80000; // set bit 19 (lifetime extension)
                                    }
                                }
                            }
                        }
                        else if ((_data.EffectOnIntervals & (1 << _effectIntervalIndex)) != 0)
                        {
                            // ptodo: mtxptr stuff
                            Matrix4 spawnTransform = Transform;
                            if ((_data.EffectFlags & 2) != 0)
                            {
                                Vector3 offset = _data.EffectPositionOffset.ToFloatVector();
                                offset.X *= Fixed.ToFloat(2 * (Test.GetRandomInt1(0x1000u) - 2048));
                                offset.Y *= Fixed.ToFloat(2 * (Test.GetRandomInt1(0x1000u) - 2048));
                                offset.Z *= Fixed.ToFloat(2 * (Test.GetRandomInt1(0x1000u) - 2048));
                                offset = Matrix.Vec3MultMtx3(offset, Transform.ClearScale());
                                spawnTransform = new Matrix4(
                                    spawnTransform.Row0,
                                    spawnTransform.Row1,
                                    spawnTransform.Row2,
                                    new Vector4(offset) + spawnTransform.Row3
                                );
                            }
                            scene.SpawnEffect((int)_data.EffectId, spawnTransform);
                        }
                        _effectIntervalTimer = (int)_data.EffectInterval;
                    }
                }
                _effectProcessing = processEffect;
            }
            if (_effectEntry != null)
            {
                for (int i = 0; i < _effectEntry.Elements.Count; i++)
                {
                    EffectElementEntry element = _effectEntry.Elements[i];
                    element.Position = Position;
                    element.Transform = Transform;
                }
            }
            return base.Process(scene);
        }

        private void RemoveEffect(Scene scene)
        {
            if (_effectEntry != null)
            {
                if ((_data.EffectFlags & 0x20) != 0)
                {
                    scene.UnlinkEffectEntry(_effectEntry);
                }
                else
                {
                    scene.DetachEffectEntry(_effectEntry, setExpired: false);
                }
            }
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (_data.EffectId > 0 && scene.ShowVolumes == VolumeDisplay.Object)
            {
                AddVolumeItem(_effectVolume, Vector3.UnitX, scene);
            }
        }
    }
}
