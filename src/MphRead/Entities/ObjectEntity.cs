using System;
using System.Diagnostics;
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
        private Vector3 _visiblePosition;

        private ObjectFlags _flags = 0;
        private readonly int _effectInterval = 0;
        private int _effectIntervalTimer = 0;
        private int _effectIntervalIndex = 0;
        private bool _effectProcessing = false;
        private EffectEntry? _effectEntry = null;
        public bool _effectActive = false;
        private readonly bool _scanVisorOnly = false;
        private int _state = 0;
        private readonly ObjectMetadata? _meta;

        private EntityBase? _parent = null;
        private EntityCollision? _parentEntCol = null;
        private Matrix4 _invTransform = Matrix4.Identity;
        private EntityBase? _scanMsgTarget = null;

        // used for ID -1 (scan point, effect spawner)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x22, 0x8B, 0x22).AsVector4();
        public ObjectEntityData Data => _data;

        public ObjectEntity(ObjectEntityData data, Scene scene) : base(EntityType.Object, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _prevTransform = Transform;
            UpdateVisiblePosition();
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
                _meta = Metadata.GetObjectById(data.ModelId);
                if (_meta.Lighting)
                {
                    _anyLighting = true;
                }
                Recolor = _meta.RecolorId;
                ModelInstance inst = SetUpModel(_meta.Name);
                // AlimbicGhost_01, GhostSwitch
                if (data.ModelId == 0 || data.ModelId == 41)
                {
                    _scanVisorOnly = true;
                }
                _state = (int)(_flags & ObjectFlags.State);
                int animIndex = _meta.AnimationIds[_state];
                // AlimbicCapsule
                if (data.ModelId == 45)
                {
                    inst.SetAnimation(animIndex, slot: 1, SetFlags.Texcoord);
                    inst.SetAnimation(animIndex, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                    if (_state == 2)
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
                    if (data.ModelId == 46 && _state == 2 || data.ModelId == 53 && _state == 1)
                    {
                        animFlags = AnimFlags.NoLoop;
                    }
                    inst.SetAnimation(animIndex, animFlags);
                }
                else
                {
                    _flags |= ObjectFlags.NoAnimation;
                }
                ModelMetadata modelMeta = Metadata.ModelMetadata[_meta.Name];
                if (modelMeta.CollisionPath != null)
                {
                    SetCollision(Collision.GetCollision(modelMeta), attach: inst);
                    if (modelMeta.ExtraCollisionPath != null)
                    {
                        // in game, collision isn't even set up unless state starts at 2, but we should still set it up ("reactivation")
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

        public override void Initialize()
        {
            base.Initialize();
            if (_data.EffectId > 0)
            {
                _scene.LoadEffect(_data.EffectId);
            }
            if (_scene.TryGetEntity(_data.ScanMsgTarget, out EntityBase? target))
            {
                _scanMsgTarget = target;
            }
        }

        public override void Destroy()
        {
            if (_effectEntry != null)
            {
                _scene.UnlinkEffectEntry(_effectEntry);
            }
        }

        private void UpdateVisiblePosition()
        {
            _visiblePosition = Position;
            if (_data.ModelId != -1)
            {
                Vector3 up = UpVector;
                Vector3 facing = FacingVector;
                Vector3 right = RightVector;
                Vector3 offset = Metadata.ObjectVisPosOffsets[_data.ModelId];
                _visiblePosition.X += right.X * offset.X + up.X * offset.Y + facing.X * offset.Z;
                _visiblePosition.Y += right.Y * offset.X + up.Y * offset.Y + facing.Y * offset.Z;
                _visiblePosition.Z += right.Z * offset.X + up.Z * offset.Y + facing.Z * offset.Z;
            }
        }

        public override void GetPosition(out Vector3 position)
        {
            position = _visiblePosition;
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = _visiblePosition;
            up = UpVector;
            facing = FacingVector;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Activate)
            {
                UpdateState(2);
            }
            else if (info.Message == Message.SetActive)
            {
                UpdateState((int)info.Param1);
            }
        }

        private void UpdateState(int state)
        {
            if (_state == state)
            {
                return;
            }
            int animId = _meta == null ? -1 : _meta.AnimationIds[state];
            if (animId < 0)
            {
                _flags |= ObjectFlags.NoAnimation;
            }
            else
            {
                bool needsUpdate = true;
                if (_data.ModelId == 45) // AlimbicCapsule
                {
                    if (state <= 1)
                    {
                        // todo: play SFX
                        _models[0].SetAnimation(animId, 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                        EntityCollision? entCol = EntityCollision[1];
                        if (entCol?.Collision != null)
                        {
                            entCol.Collision.Active = false;
                        }
                    }
                    needsUpdate = false;
                }
                else if (_data.ModelId == 46) // SniperTarget
                {
                    Debug.Assert(_meta != null);
                    if (state == 0)
                    {
                        if (_models[0].AnimInfo.Index[0] == _meta.AnimationIds[0])
                        {
                            _models[0].AnimInfo.Flags[0] &= ~AnimFlags.Ended;
                            _models[0].AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                            _models[0].AnimInfo.Flags[0] |= AnimFlags.NoLoop;
                        }
                        else
                        {
                            _models[0].SetAnimation(animId, AnimFlags.NoLoop);
                        }
                        needsUpdate = false;
                    }
                    else if (state != 1)
                    {
                        _models[0].SetAnimation(animId, AnimFlags.NoLoop);
                        // todo: play SFX
                        needsUpdate = false;
                    }
                    else if (_models[0].AnimInfo.Index[0] == _meta.AnimationIds[0])
                    {
                        _models[0].AnimInfo.Flags[0] &= ~AnimFlags.Ended;
                        _models[0].AnimInfo.Flags[0] |= AnimFlags.Reverse;
                        _models[0].AnimInfo.Flags[0] |= AnimFlags.NoLoop;
                        needsUpdate = false;
                    }
                }
                else if (_data.ModelId == 53) // WallSwitch
                {
                    _models[0].SetAnimation(animId, AnimFlags.NoLoop);
                    // todo: play SFX
                    needsUpdate = false;
                }
                else if (_data.ModelId >= 47 && _data.ModelId <= 52 && (state == 1 || state == 2)) // SecretSwitch
                {
                    if (state == 1 && _state == 0)
                    {
                        return;
                    }
                    // todo: play SFX
                    _models[0].SetAnimation(animId, AnimFlags.NoLoop);
                    needsUpdate = false;
                }
                if (needsUpdate)
                {
                    _models[0].SetAnimation(animId);
                }
            }
            _state = state;
            _effectIntervalTimer = 0;
            _effectIntervalIndex = 15;
            // todo: room state
            if (state != 0 || _data.ModelId == 53) // WallSwitch
            {
                // todo: scan ID
            }
            else
            {
                RemoveEffect();
                // todo: scan ID
            }
        }

        public override bool Process()
        {
            base.Process();
            if (_data.ModelId == 46) // SniperTarget
            {
                if (_state != 2)
                {
                    Vector3 between = PlayerEntity.Main.Position - Position;
                    if (Vector3.Dot(between, between) >= 15 * 15)
                    {
                        if (_scanMsgTarget != null)
                        {
                            _scene.SendMessage(Message.SetActive, this, _scanMsgTarget, 1, 0);
                        }
                        UpdateState(1);
                        if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                        {
                            Debug.Assert(_meta != null);
                            _models[0].SetAnimation(_meta.AnimationIds[_state]);
                        }
                    }
                    else
                    {
                        if (_scanMsgTarget != null)
                        {
                            _scene.SendMessage(Message.SetActive, this, _scanMsgTarget, 0, 0);
                        }
                        UpdateState(0);
                    }
                }
            }
            else if (_data.ModelId == 53) // WallSwitch
            {
                if (_state == 1 && _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    UpdateState(2);
                }
            }
            else if (_data.ModelId >= 47 && _data.ModelId <= 52) // SecretSwitch
            {
                // todo: update audio
                if (_state == 1 && _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    UpdateState(0);
                }
            }
            if (_state == 0 && _data.EffectId == 0) // skdebug
            {
                return true;
            }
            if (!_flags.TestFlag(ObjectFlags.EntityLinked))
            {
                if (_data.LinkedEntity != -1 && _scene.TryGetEntity(_data.LinkedEntity, out EntityBase? entity))
                {
                    _parent = entity;
                    _parentEntCol = _parent.EntityCollision[0];
                    if (_parentEntCol != null)
                    {
                        _invTransform = _transform * _parentEntCol.Inverse2;
                    }
                }
                _flags |= ObjectFlags.EntityLinked;
            }
            ShouldDraw = !_scanVisorOnly || _scene.ScanVisor;
            if (_parentEntCol != null)
            {
                Transform = _invTransform * _parentEntCol.Transform;
                UpdateVisiblePosition();
            }
            if (Transform != _prevTransform)
            {
                _effectVolume = CollisionVolume.Transform(_data.Volume, Transform);
                _prevTransform = Transform;
            }
            UpdateCollisionTransform(0, CollisionTransform); // whether transform or animation, should include parent if any
            UpdateCollisionTransform(1, CollisionTransform); // the game does this in draw for objects
            UpdateLinkedInverse(0);
            UpdateLinkedInverse(1);
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
                    Vector3 cameraPosition = _scene.CameraMode == CameraMode.Player
                        ? PlayerEntity.Main.CameraInfo.Position
                        : _scene.CameraPosition; // skdebug
                    processEffect = _effectVolume.TestPoint(cameraPosition);
                }
                else if (_flags.TestFlag(ObjectFlags.IsVisible))
                {
                    processEffect = _state != 0;
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
                                    RemoveEffect();
                                }
                                else
                                {
                                    _effectEntry = _scene.SpawnEffectGetEntry(_data.EffectId, Transform);
                                    _effectEntry.SetElementExtension(true);
                                }
                            }
                        }
                        else if ((_data.EffectOnIntervals & (1 << _effectIntervalIndex)) != 0)
                        {
                            EntityCollision? entCol = _parent?.EntityCollision[0];
                            Vector3 spawnFacing = FacingVector;
                            Vector3 spawnUp = UpVector;
                            Vector3 spawnPos = Position;
                            if (entCol != null)
                            {
                                spawnPos = Matrix.Vec3MultMtx4(spawnPos, entCol.Inverse1);
                                spawnUp = Matrix.Vec3MultMtx3(spawnUp, entCol.Inverse1);
                                spawnFacing = Matrix.Vec3MultMtx3(spawnFacing, entCol.Inverse1);
                            }
                            if (_data.EffectFlags.TestFlag(ObjEffFlags.UseEffectOffset))
                            {
                                Vector3 offset = _data.EffectPositionOffset.ToFloatVector();
                                offset.X *= Fixed.ToFloat(2 * (Rng.GetRandomInt1(0x1000u) - 2048));
                                offset.Y *= Fixed.ToFloat(2 * (Rng.GetRandomInt1(0x1000u) - 2048));
                                offset.Z *= Fixed.ToFloat(2 * (Rng.GetRandomInt1(0x1000u) - 2048));
                                spawnPos += Matrix.Vec3MultMtx3(offset, GetTransformMatrix(spawnFacing, spawnUp));
                            }
                            // todo: play SFX
                            _scene.SpawnEffect(_data.EffectId, spawnFacing, spawnUp, spawnPos, entCol: entCol);
                        }
                        _effectIntervalTimer = _effectInterval;
                    }
                }
                _effectProcessing = processEffect;
            }
            if (_effectEntry != null)
            {
                _effectEntry.Transform(Position, Transform.ClearScale());
            }
            if (_data.ModelId == 0 && _models[0].AnimInfo.Index[0] == 3 && _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                _models[0].SetAnimation((int)Rng.GetRandomInt1(2));
            }
            return true;
        }

        private void RemoveEffect()
        {
            if (_effectEntry != null)
            {
                if (_data.EffectFlags.TestFlag(ObjEffFlags.DestroyEffect))
                {
                    _scene.UnlinkEffectEntry(_effectEntry);
                }
                else
                {
                    _scene.DetachEffectEntry(_effectEntry, setExpired: false);
                }
            }
        }

        public override void GetDisplayVolumes()
        {
            if (_data.EffectId > 0 && _scene.ShowVolumes == VolumeDisplay.Object)
            {
                AddVolumeItem(_effectVolume, Vector3.UnitX);
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
        RepeatScanMessage = 0x4, // todo: send scan message
        WeaponZoom = 0x8,
        AttachEffect = 0x10,
        DestroyEffect = 0x20,
        AlwaysUpdateEffect = 0x40,
        Unknown = 0x8000
    }
}
