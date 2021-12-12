using System;
using MphRead.Effects;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ObjectEntity : EntityBase
    {
        private readonly ObjectEntityData _data;
        private CollisionVolume _effectVolume;
        private Matrix4 _prevTransform;

        private ObjectFlags _flags = 0;
        private int _effectInterval = 0;
        private int _effectIntervalTimer = 0;
        private int _effectIntervalIndex = 0;
        private bool _effectProcessing = false;
        private EffectEntry? _effectEntry = null;
        public bool _effectActive = false;
        private readonly bool _scanVisorOnly = false;

        private bool _invSetUp = false;
        private EntityBase? _parent = null;
        private Matrix4 _invTransform;

        // used for ID -1 (scan point, effect spawner)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x22, 0x8B, 0x22).AsVector4();
        public ObjectEntityData Data => _data;

        public ObjectEntity(ObjectEntityData data) : base(EntityType.Object)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _prevTransform = Transform;
            _flags = data.Flags;
            // todo: room state affecting animation ID
            _flags &= ~ObjectFlags.NoAnimation;
            _flags &= ~ObjectFlags.IsVisible;
            _flags &= ~ObjectFlags.EntityLinked;
            if (data.EffectId > 0 && data.EffectFlags.TestFlag(ObjEffFlags.UseEffectVolume))
            {
                _effectVolume = CollisionVolume.Transform(data.Volume, Transform);
            }
            _effectInterval = (int)data.EffectInterval * 2;
            if (data.ModelId == -1)
            {
                AddPlaceholderModel();
                // todo: use this for visibility
                _flags |= ObjectFlags.NoAnimation;
                // todo: this should get cleared if there's an effect ID and "is_visible" returns false
                _flags |= ObjectFlags.IsVisible;
            }
            else
            {
                ObjectMetadata meta = Metadata.GetObjectById(data.ModelId);
                if (meta.Lighting)
                {
                    _anyLighting = true;
                }
                Recolor = meta.RecolorId;
                ModelInstance inst = SetUpModel(meta.Name);
                // AlimbicGhost_01, GhostSwitch
                if (data.ModelId == 0 || data.ModelId == 41)
                {
                    _scanVisorOnly = true;
                }
                int state = (int)(_flags & ObjectFlags.State);
                int animIndex = meta.AnimationIds[state];
                // AlimbicCapsule
                if (data.ModelId == 45)
                {
                    inst.SetAnimation(animIndex, slot: 1, SetFlags.Texcoord);
                    inst.SetAnimation(animIndex, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                    if (state == 2)
                    {
                        inst.AnimInfo.Flags[0] |= AnimFlags.Paused;
                    }
                    else
                    {
                        inst.AnimInfo.Flags[0] |= AnimFlags.Ended;
                        inst.AnimInfo.Frame[0] = inst.AnimInfo.FrameCount[0] - 1;
                    }
                }
                else if (animIndex >= 0)
                {
                    AnimFlags animFlags = AnimFlags.None;
                    // SniperTarget, WallSwitch
                    if (data.ModelId == 46 && state == 2 || data.ModelId == 53 && state == 1)
                    {
                        animFlags = AnimFlags.NoLoop;
                    }
                    inst.SetAnimation(animIndex, animFlags);
                }
                else
                {
                    _flags |= ObjectFlags.NoAnimation;
                }
                ModelMetadata modelMeta = Metadata.ModelMetadata[meta.Name];
                if (modelMeta.CollisionPath != null)
                {
                    SetCollision(Collision.GetCollision(modelMeta), attach: inst);
                    if (modelMeta.ExtraCollisionPath != null)
                    {
                        // ctodo: disable capsule shield collision when appropriate
                        // --> in game, collision isn't even set up unless anim ID starts at 2, but we should still set it up ("reactivation")
                        SetCollision(Collision.GetCollision(modelMeta, extra: true), slot: 1);
                    }
                }
                // temporary -- room state/end flag processing
                if (inst.Model.Name == "SecretSwitch" || inst.Model.Name == "AlimbicStatue_lod0")
                {
                    inst.SetAnimation(-1);
                }
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.EffectId > 0)
            {
                scene.LoadEffect(_data.EffectId);
            }
            if (_data.LinkedEntity != -1)
            {
                if (scene.TryGetEntity(_data.LinkedEntity, out EntityBase? parent))
                {
                    _parent = parent;
                }
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
            if (_parent != null)
            {
                // sktodo
                //if (!_invSetUp)
                //{
                //    _parent.GetDrawInfo(scene); // force update transforms
                //    _invTransform = _transform * _parent.CollisionTransform.Inverted();
                //    _invSetUp = true;
                //}
                //// todo: visible position stuff (get vecs)
                //Transform = _invTransform * _parent.CollisionTransform;
            }
            if (Transform != _prevTransform)
            {
                _effectVolume = CollisionVolume.Transform(_data.Volume, Transform);
                _prevTransform = Transform;
            }
            if (_data.EffectId > 0)
            {
                bool processEffect = false;
                if (_data.EffectFlags.TestFlag(ObjEffFlags.AlwaysUpdateEffect))
                {
                    processEffect = true;
                }
                else if (_data.EffectFlags.TestFlag(ObjEffFlags.UseEffectVolume))
                {
                    // todo: add an option to disable this check
                    processEffect = _effectVolume.TestPoint(scene.CameraPosition);
                }
                else if (_flags.TestFlag(ObjectFlags.IsVisible))
                {
                    processEffect = (_flags & ObjectFlags.State) != 0;
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
                        if (_data.EffectFlags.TestFlag(ObjEffFlags.AttachEffect))
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
                                    _effectEntry = scene.SpawnEffectGetEntry(_data.EffectId, Transform);
                                    for (int i = 0; i < _effectEntry.Elements.Count; i++)
                                    {
                                        EffectElementEntry element = _effectEntry.Elements[i];
                                        element.Flags |= EffElemFlags.ElementExtension;
                                    }
                                }
                            }
                        }
                        else if ((_data.EffectOnIntervals & (1 << _effectIntervalIndex)) != 0)
                        {
                            // ptodo: mtxptr stuff
                            Matrix4 spawnTransform = Transform;
                            if (_data.EffectFlags.TestFlag(ObjEffFlags.UseEffectOffset))
                            {
                                Vector3 offset = _data.EffectPositionOffset.ToFloatVector();
                                offset.X *= Fixed.ToFloat(2 * (Rng.GetRandomInt1(0x1000u) - 2048));
                                offset.Y *= Fixed.ToFloat(2 * (Rng.GetRandomInt1(0x1000u) - 2048));
                                offset.Z *= Fixed.ToFloat(2 * (Rng.GetRandomInt1(0x1000u) - 2048));
                                offset = Matrix.Vec3MultMtx3(offset, Transform.ClearScale());
                                spawnTransform = new Matrix4(
                                    spawnTransform.Row0,
                                    spawnTransform.Row1,
                                    spawnTransform.Row2,
                                    new Vector4(offset) + spawnTransform.Row3
                                );
                            }
                            EntityBase? owner = _parent == null ? null : this;
                            scene.SpawnEffect(_data.EffectId, spawnTransform, owner: owner);
                        }
                        _effectIntervalTimer = _effectInterval;
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
                    element.Transform = Transform.ClearScale();
                }
            }
            return base.Process(scene);
        }

        private void RemoveEffect(Scene scene)
        {
            if (_effectEntry != null)
            {
                if (_data.EffectFlags.TestFlag(ObjEffFlags.DestroyEffect))
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

    [Flags]
    public enum ObjectFlags : byte
    {
        None = 0x0,
        StateBit0 = 0x1,
        StateBit1 = 0x2,
        State = 0x3,
        NoAnimation = 0x4,
        EntityLinked = 0x8,
        IsVisible = 0x10
    }

    [Flags]
    public enum ObjEffFlags : uint
    {
        None = 0x0,
        UseEffectVolume = 0x1,
        UseEffectOffset = 0x2,
        RepeatScanMessage = 0x4,
        WeaponZoom = 0x8,
        AttachEffect = 0x10,
        DestroyEffect = 0x20,
        AlwaysUpdateEffect = 0x40,
        Unknown = 0x8000
    }
}
