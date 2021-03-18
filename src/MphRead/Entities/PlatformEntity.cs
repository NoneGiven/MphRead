using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlatformEntity : EntityBase
    {
        private readonly PlatformEntityData _data;
        private readonly PlatformMetadata? _meta = null;

        // used for ID 2 (energyBeam, arcWelder)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x2F, 0x4F, 0x4F).AsVector4();

        public PlatformFlags Flags { get; private set; }
        private readonly List<int> _effectNodeIds = new List<int>() { -1, -1, -1, -1 };
        private readonly List<EffectEntry?> _effects = new List<EffectEntry?>() { null, null, null, null };
        private const int _nozzleEffectId = 182; // nozzleJet

        private bool _beamActive = false;
        private readonly int _beamInterval = 0;
        private int _beamIntervalTimer = 0;
        private int _beamIntervalIndex = 0;
        private readonly EquipInfo? _equipInfo;
        private readonly Vector3 _beamSpawnPos;
        private readonly Vector3 _beamSpawnDir;

        private uint _health = 0;
        private uint _halfHealth = 0;

        private PlatAnimFlags _animFlags = PlatAnimFlags.None;
        private PlatStateBits _stateBits = PlatStateBits.None;
        private PlatformState _state = PlatformState.Inactive;
        private int _fromIndex = 0;
        private int _toIndex = 0;
        private readonly int _delay;
        private int _moveTimer;
        private readonly float _forwardSpeed;
        private readonly float _backwardSpeed;
        private int _currentAnim = 0;
        private int _currentAnimId = 0;

        private readonly Vector3 _posOffset;
        private Vector3 _prevPos;
        private Vector3 _visiblePos;
        private Vector3 _prevVisiblePos;
        private Vector4 _curRotation;
        private Vector4 _fromRotation;
        private Vector4 _toRotation;
        private readonly IReadOnlyList<Vector3> _posList;
        private readonly IReadOnlyList<Vector4> _rotList;
        private Vector3 _velocity = Vector3.Zero;
        private float _movePercent = 0;
        private float _moveIncrement = 0;

        private static readonly BeamProjectileEntity[] _beams = SceneSetup.CreateBeamList(64); // in-game: 18

        public PlatformEntity(PlatformEntityData data) : base(EntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            Flags = data.Flags;
            if (Flags.HasFlag(PlatformFlags.Breakable))
            {
                Flags |= PlatformFlags.Bit03;
                Flags |= PlatformFlags.HideOnSleep;
                Flags |= PlatformFlags.BeamTarget;
            }
            _health = data.Health;
            if (Flags.HasFlag(PlatformFlags.SyluxShip))
            {
                _halfHealth = _health / 2;
            }
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _prevVisiblePos = _visiblePos = _prevPos = Position;
            _posOffset = data.PositionOffset.ToFloatVector();
            var posList = new List<Vector3>();
            for (int i = 0; i < data.PositionCount; i++)
            {
                posList.Add(data.Positions[i].ToFloatVector());
            }
            _posList = posList;
            var rotList = new List<Vector4>();
            for (int i = 0; i < data.PositionCount; i++)
            {
                rotList.Add(data.Rotations[i].ToFloatVector());
            }
            _rotList = rotList;
            _meta = Metadata.GetPlatformById((int)data.ModelId);
            if (_meta == null)
            {
                AddPlaceholderModel();
            }
            else
            {
                ModelInstance inst = Read.GetModelInstance(_meta.Name);
                _models.Add(inst);
                ModelMetadata modelMeta = Metadata.ModelMetadata[_meta.Name];
                if (modelMeta.AnimationPath != null)
                {
                    _animFlags |= PlatAnimFlags.HasAnim;
                }
                if (modelMeta.CollisionPath != null)
                {
                    SetCollision(Collision.GetCollision(modelMeta), attach: inst);
                }
                // temporary
                if (_meta.Name == "SyluxTurret")
                {
                    inst.SetNodeAnim(-1);
                }
            }
            _beamInterval = (int)data.BeamInterval * 2;
            if (data.BeamId > -1)
            {
                Debug.Assert(data.BeamId < Weapons.PlatformWeapons.Count);
                _equipInfo = new EquipInfo(Weapons.PlatformWeapons[data.BeamId], _beams);
                _beamSpawnPos = data.BeamSpawnPos.ToFloatVector();
                _beamSpawnDir = data.BeamSpawnDir.ToFloatVector();
                _beamIntervalIndex = 15;
            }
            _delay = data.Delay * 2;
            _moveTimer = _delay;
            _forwardSpeed = data.ForwardSpeed.FloatValue / 2f;
            _backwardSpeed = data.ForwardSpeed.FloatValue / 2f;
            UpdatePosition();
            _animFlags |= PlatAnimFlags.Draw;
            // todo: room state
            if (Flags.HasFlag(PlatformFlags.SamusShip))
            {
                SleepWake(wake: true, instant: true);
                _currentAnim = -2;
                // todo: room state for initial landed
                // --> options are instant_wake and wake, but it seems like it should be instant sleep?
                SetAnimation(PlatAnimId.InstantSleep, AnimFlags.None);
                _animFlags |= PlatAnimFlags.Active;
            }
            else
            {
                if (Flags.HasFlag(PlatformFlags.StartSleep))
                {
                    SleepWake(wake: false, instant: true);
                }
                else
                {
                    SleepWake(wake: true, instant: true);
                }
                // todo: more room state
                _animFlags |= PlatAnimFlags.Active;
                if (_animFlags.HasFlag(PlatAnimFlags.Active))
                {
                    Activate();
                }
                else
                {
                    Deactivate();
                }
                _currentAnim = -2;
                if (_animFlags.HasFlag(PlatAnimFlags.HasAnim))
                {
                    if (_animFlags.HasFlag(PlatAnimFlags.Active))
                    {
                        SetAnimation(PlatAnimId.InstantWake, AnimFlags.None);
                    }
                    else
                    {
                        SetAnimation(PlatAnimId.InstantSleep, AnimFlags.None);
                    }
                }
            }
        }

        public override void Initialize(Scene scene)
        {
            // sktodo: parent/mtxptr
            base.Initialize(scene);
            if (Flags.HasFlag(PlatformFlags.SamusShip))
            {
                Model model = _models[0].Model;
                for (int i = 0; i < model.Nodes.Count; i++)
                {
                    Node node = model.Nodes[i];
                    if (node.Name == "R_Turret")
                    {
                        _effectNodeIds[0] = i;
                    }
                    else if (node.Name == "R_Turret1")
                    {
                        _effectNodeIds[1] = i;
                    }
                    else if (node.Name == "R_Turret2")
                    {
                        _effectNodeIds[2] = i;
                    }
                    else if (node.Name == "R_Turret3")
                    {
                        _effectNodeIds[3] = i;
                    }
                }
                if (_effectNodeIds[0] != -1 || _effectNodeIds[1] != -1 || _effectNodeIds[2] != -1 || _effectNodeIds[3] != -1)
                {
                    scene.LoadEffect(_nozzleEffectId);
                }
            }
            if (_data.ResistEffectId != 0)
            {
                scene.LoadEffect(_data.ResistEffectId);
            }
            if (_data.DamageEffectId != 0)
            {
                scene.LoadEffect(_data.DamageEffectId);
            }
            if (_data.DeadEffectId != 0)
            {
                scene.LoadEffect(_data.DeadEffectId);
            }
            if (_data.BeamId == 0 && Flags.HasFlag(PlatformFlags.BeamSpawner))
            {
                scene.LoadEffect(183);
                scene.LoadEffect(184);
                scene.LoadEffect(185);
            }
        }

        public override void Destroy(Scene scene)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                EffectEntry? effectEntry = _effects[i];
                if (effectEntry != null)
                {
                    scene.UnlinkEffectEntry(effectEntry);
                }
            }
        }

        private void SetAnimation(PlatAnimId id, AnimFlags flags)
        {
            Debug.Assert(_meta != null);
            int index = _meta.AnimationIds[(int)id];
            SetAnimation(index, flags);
        }

        private void SetAnimation(int index, AnimFlags flags)
        {
            _currentAnimId = index;
            if (index >= 0)
            {
                Debug.Assert(!_models[0].IsPlaceholder);
                _models[0].SetAnimation(index, flags);
            }
        }

        private int GetAnimation(PlatAnimId id)
        {
            Debug.Assert(_meta != null);
            return _meta.AnimationIds[(int)id];
        }

        private void SleepWake(bool wake, bool instant)
        {
            if (wake)
            {
                _stateBits |= PlatStateBits.Awake;
                if (instant)
                {
                    SetAnimation(PlatAnimId.InstantWake, AnimFlags.None);
                }
                else
                {
                    SetAnimation(PlatAnimId.Wake, AnimFlags.Bit03);
                    // todo: rename cur_anim vs cur_anim_id
                    _currentAnim = GetAnimation(PlatAnimId.InstantWake);
                }
            }
            else
            {
                if (_stateBits.HasFlag(PlatStateBits.Awake))
                {
                    _stateBits |= PlatStateBits.WasAwake;
                }
                _stateBits &= PlatStateBits.Awake;
                if (instant)
                {
                    SetAnimation(PlatAnimId.InstantSleep, AnimFlags.None);
                }
                else
                {
                    SetAnimation(PlatAnimId.Sleep, AnimFlags.Bit03);
                    _currentAnim = GetAnimation(PlatAnimId.InstantSleep);
                }
                if (Flags.HasFlag(PlatformFlags.HideOnSleep))
                {
                    // todo: disable collision, stop sfx, room state
                    _animFlags &= PlatAnimFlags.Draw;
                }
            }
        }

        private void Activate()
        {
            _animFlags |= PlatAnimFlags.Active;
            // todo: more room state
            if (_state == PlatformState.Inactive)
            {
                // todo: messaging
                if (_data.PositionCount >= 2)
                {
                    UpdateMovement();
                    _state = PlatformState.Waiting;
                    _moveTimer = 0;
                    _stateBits |= PlatStateBits.Activated;
                    if (_fromIndex == _data.PositionCount - 1)
                    {
                        _stateBits |= PlatStateBits.Reverse;
                    }
                    else
                    {
                        _stateBits &= PlatStateBits.Reverse;
                    }
                }
            }
        }

        private void Deactivate()
        {
            if (_state != PlatformState.Inactive)
            {
                _state = PlatformState.Inactive;
                _animFlags &= PlatAnimFlags.Active;
                // todo: more room state, messaging
            }
        }

        public override bool Process(Scene scene)
        {
            _prevVisiblePos = _visiblePos;
            // ptodo: player bonk stuff
            if (!_animFlags.HasFlag(PlatAnimFlags.DisableReflect))
            {
                // ptodo: Sylux ship/turret stuff
                if (_data.MovementType == 1)
                {
                    // never true in-game
                    _position += _velocity;
                    _movePercent += _moveIncrement;
                    // sktodo
                }
                else if (_data.MovementType == 0)
                {
                    UpdateState();
                    _position += _velocity;
                    _movePercent += _moveIncrement;
                    UpdateRotation();
                }
                if (_animFlags.HasFlag(PlatAnimFlags.SeekPlayerHeight) && PlayerEntity.PlayerCount > 0)
                {
                    // also never true in-game
                    float offset = (PlayerEntity.Players[PlayerEntity.MainPlayer].Position.Y - _position.Y) * Fixed.ToFloat(20);
                    _position.Y += offset;
                }
            }
            UpdateTransform();
            _visiblePos = Position;

            bool spawnBeam = true;
            if (!_models[0].IsPlaceholder && Flags.HasFlag(PlatformFlags.SyluxShip))
            {
                if (_currentAnim != -2)
                {
                    spawnBeam = false;
                }
                // ptodo: else check parent state bits
            }
            // btodo: 0 is valid for Sylux turret missiles, but without collision handling those would eat up the effect lists
            if (spawnBeam && _animFlags.HasFlag(PlatAnimFlags.Draw) && !_animFlags.HasFlag(PlatAnimFlags.DisableReflect)
                && _stateBits.HasFlag(PlatStateBits.Awake) && Flags.HasFlag(PlatformFlags.BeamSpawner) && _data.BeamId > 0)
            {
                if (--_beamIntervalTimer <= 0)
                {
                    _beamIntervalIndex++;
                    _beamIntervalIndex %= 16;
                    _beamActive = (_data.BeamOnIntervals & (1 << _beamIntervalIndex)) != 0;
                    // todo: SFX
                    _beamIntervalTimer = _beamInterval;
                }
                if (_beamActive)
                {
                    Debug.Assert(_equipInfo != null);
                    Vector3 spawnPos = Matrix.Vec3MultMtx4(_beamSpawnPos, Transform);
                    Vector3 spawnDir = Matrix.Vec3MultMtx3(_beamSpawnDir, Transform).Normalized();
                    BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, spawnDir, BeamSpawnFlags.None, scene);
                    if (!_equipInfo.Weapon.Flags.HasFlag(WeaponFlags.Bit10))
                    {
                        _beamActive = false;
                    }
                }
            }
            // sktodo: does it matter if process_something_with_anim is called before the following?
            if (_currentAnim != -2 && _models[0].AnimFlags.HasFlag(AnimFlags.Bit04))
            {
                SetAnimation(_currentAnim, AnimFlags.None);
                _currentAnim = -2;
                _stateBits &= PlatStateBits.WasAwake;
            }
            // todo: if "is_visible" returns false (and other conditions), don't draw the effects
            Model model = _models[0].Model;
            for (int i = 0; i < 4; i++)
            {
                if (_effectNodeIds[i] >= 0 && _effects[i] == null)
                {
                    Matrix4 transform = Matrix.GetTransform4(Vector3.UnitX, Vector3.UnitY, new Vector3(0, 2, 0));
                    _effects[i] = scene.SpawnEffectGetEntry(_nozzleEffectId, transform);
                    for (int j = 0; j < _effects[i]!.Elements.Count; j++)
                    {
                        EffectElementEntry element = _effects[i]!.Elements[j];
                        element.Flags |= EffElemFlags.ElementExtension;
                    }
                }
                if (_effects[i] != null)
                {
                    Matrix4 transform = model.Nodes[_effectNodeIds[i]].Animation;
                    var position = new Vector3(
                        transform.M31 * 1.5f + transform.M41,
                        transform.M32 * 1.5f + transform.M42,
                        transform.M33 * 1.5f + transform.M43
                    );
                    transform = Matrix.GetTransform4(new Vector3(transform.Row1), new Vector3(transform.Row2), position);
                    for (int j = 0; j < _effects[i]!.Elements.Count; j++)
                    {
                        EffectElementEntry element = _effects[i]!.Elements[j];
                        element.Position = position;
                        element.Transform = transform;
                    }
                }
            }
            return base.Process(scene);
        }

        public override void GetDrawInfo(Scene scene)
        {
            if (_animFlags.HasFlag(PlatAnimFlags.Draw) || _models[0].IsPlaceholder)
            {
                base.GetDrawInfo(scene);
            }
        }

        private void UpdatePosition()
        {
            if (_data.PositionCount > 0)
            {
                _position = _posList[_fromIndex];
                _curRotation = _fromRotation = _rotList[_fromIndex];
                UpdateTransform();
            }
        }

        private void UpdateMovement()
        {
            UpdatePosition();
            Vector3 velocity = _posList[_toIndex] - _posList[_fromIndex];
            float speed = _stateBits.HasFlag(PlatStateBits.Reverse) ? _backwardSpeed : _forwardSpeed;
            float factor;
            if (_data.ForCutscene != 0)
            {
                _moveTimer = (int)(30f / (speed * 2f));
                factor = 1f / (_moveTimer + 1);
                _moveTimer *= 2;
                factor /= 2f;
            }
            else
            {
                float distance = velocity.Length;
                _moveTimer = (int)(distance / speed);
                factor = speed / distance;
            }
            _velocity = velocity * factor;
            _toRotation = _rotList[_toIndex];
            _movePercent = 0;
            _moveIncrement = factor;
        }

        private void UpdateRotation()
        {
            float pct = Math.Clamp(_movePercent, 0, 1);
            float dot = Vector4.Dot(_fromRotation, _toRotation);
            bool negDot = dot < 0;
            dot = MathF.Abs(dot);
            float factor;
            if (1 - dot >= Fixed.ToFloat(16))
            {
                float angle1 = MathF.Atan(MathF.Sqrt((1 - dot) / (dot + 1))) * 2f;
                float angle2 = pct * angle1;
                float sin = MathF.Sin(angle1);
                factor = MathF.Sin(angle1 - angle2) / sin;
                pct = MathF.Sin(angle2) / sin;
            }
            else
            {
                factor = 1 - pct;
            }
            if (negDot)
            {
                pct *= -1;
            }
            _curRotation = new Vector4(
                factor * _fromRotation.X + pct * _toRotation.X,
                factor * _fromRotation.Y + pct * _toRotation.Y,
                factor * _fromRotation.Z + pct * _toRotation.Z,
                factor * _fromRotation.W + pct * _toRotation.W
            ).Normalized();
            UpdateTransform();
        }

        private void UpdateState()
        {
            if (_state == PlatformState.Moving)
            {
                // todo: recoil timer
                if (_moveTimer > 0)
                {
                    _moveTimer--;
                    // todo: sfx everywhere
                }
                else
                {
                    _fromIndex = _toIndex;
                    // todo: messaging, room state
                    _state = PlatformState.Waiting;
                    _moveTimer = _delay;
                    _velocity = Vector3.Zero;
                    _moveIncrement = 0;
                    _movePercent = 0;
                    UpdatePosition();
                }
            }
            else if (_state == PlatformState.Waiting)
            {
                if (_moveTimer > 0)
                {
                    _moveTimer--;
                }
                else
                {
                    // sktodo
                    if (_stateBits.HasFlag(PlatStateBits.Activated))
                    {
                        if (_data.ReverseType == 0)
                        {
                            if (_stateBits.HasFlag(PlatStateBits.Reverse))
                            {
                                if (_fromIndex == 0)
                                {
                                    _stateBits &= PlatStateBits.Reverse;
                                }
                            }
                            else if (_fromIndex == _data.PositionCount - 1)
                            {
                                _stateBits |= PlatStateBits.Reverse;
                            }
                            _toIndex = _fromIndex + (_stateBits.HasFlag(PlatStateBits.Reverse) ? -1 : 1);
                            _state = PlatformState.Moving;
                        }
                        else if (_data.ReverseType == 1)
                        {
                            int index = _fromIndex + (_stateBits.HasFlag(PlatStateBits.Reverse) ? -1 : 1);
                            _toIndex = index % _data.PositionCount;
                            _state = PlatformState.Moving;
                        }
                        else if (_data.ReverseType == 2)
                        {
                            if ((_stateBits.HasFlag(PlatStateBits.Reverse) && _fromIndex == 0)
                                || (!_stateBits.HasFlag(PlatStateBits.Reverse) && _fromIndex == _data.PositionCount - 1))
                            {
                                Deactivate();
                                if (Flags.HasFlag(PlatformFlags.Bit08))
                                {
                                    SleepWake(wake: false, instant: false);
                                }
                            }
                            if (_state != PlatformState.Inactive)
                            {
                                _toIndex = _fromIndex + (_stateBits.HasFlag(PlatStateBits.Reverse) ? -1 : 1);
                                _state = PlatformState.Moving;
                            }
                        }
                    }
                }
            }
            else if (_state == PlatformState.Inactive)
            {
                if (_moveTimer > 0)
                {
                    _moveTimer--;
                }
                else if (_animFlags.HasFlag(PlatAnimFlags.Active))
                {
                    Activate();
                }
            }
        }

        private void UpdateTransform()
        {
            if (_data.PositionCount > 0)
            {
                Matrix4 transform = GetTransformMatrix();
                if (_posOffset != Vector3.Zero)
                {
                    transform.Row3.Xyz += Matrix.Vec3MultMtx3(_posOffset, transform);
                    // ptodo: Sylux ship/turret stuff
                    // ptodo: parent/mtxptr stuff
                    Transform = transform;
                }
            }
            else
            {
                // ptodo: parent/mtxptr stuff
                Position = _position;
            }
        }

        private Matrix4 GetTransformMatrix()
        {
            float v3 = _curRotation.Y * _curRotation.Y;
            float v4 = _curRotation.Z * _curRotation.Z;
            float v5 = 2 * _curRotation.X * _curRotation.Z;
            float v6 = 2 * _curRotation.Y * _curRotation.Z;
            float v7 = _curRotation.W * _curRotation.Z;
            float v8 = _curRotation.W * _curRotation.X;
            float v9 = _curRotation.W * _curRotation.Y;
            float v10 = 1 - 2 * _curRotation.X * _curRotation.X;
            float v13 = 2 * _curRotation.X * _curRotation.Y;
            float m11 = 1 - 2 * v3 - 2 * v4;
            float m12 = v13 + 2 * v7;
            float m13 = v5 - 2 * v9;
            float m21 = v13 - 2 * v7;
            float m22 = v10 - 2 * v4;
            float m23 = v6 + 2 * v8;
            float m31 = v5 + 2 * v9;
            float m32 = v6 - 2 * v8;
            float m33 = v10 - 2 * v3;
            return new Matrix4(
                m11, m12, m13, 0,
                m21, m22, m23, 0,
                m31, m32, m33, 0,
                _position.X, _position.Y, _position.Z, 1
            );
        }
    }

    [Flags]
    public enum PlatAnimFlags : ushort
    {
        None = 0x0,
        Active = 0x1,
        DisableReflect = 0x2,
        Draw = 0x4,
        TakeDamage = 0x8,
        Bit04 = 0x10, // functionless
        Bit05 = 0x20, // functionless
        SeekPlayerHeight = 0x40,
        WasDrawn = 0x80,
        HasAnim = 0x100,
        Bit09 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000
    }

    [Flags]
    public enum PlatformFlags : uint
    {
        None = 0x0,
        Hazard = 0x1,
        ContactDamage = 0x2,
        BeamSpawner = 0x4,
        Bit03 = 0x8, // functionless
        DamagedReflect1 = 0x10,
        DamagedReflect2 = 0x20,
        StandingColOnly = 0x40,
        StartSleep = 0x80,
        Bit08 = 0x100,
        Bit09 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        HideOnSleep = 0x2000,
        SyluxShip = 0x4000,
        Bit15 = 0x8000,
        BeamReflection = 0x10000,
        Bit17 = 0x20000,
        BeamTarget = 0x40000,
        SamusShip = 0x80000,
        Breakable = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        NoRecoil = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }

    [Flags]
    public enum PlatStateBits : uint
    {
        None = 0x0,
        Awake = 0x1,
        Activated = 0x2,
        Reverse = 0x4,
        WasAwake = 0x8
    }

    public enum PlatformState : byte
    {
        Inactive = 0,
        Moving = 1,
        Waiting = 2
    }

    public class FhPlatformEntity : EntityBase
    {
        private readonly FhPlatformEntityData _data;
        private readonly float _speed;
        private readonly IReadOnlyList<Vector3> _posList;
        private MoveState _state = MoveState.Sleep;
        private int _fromIndex = 0;
        private int _toIndex = 1;
        private readonly int _delay;
        private int _moveTimer;
        private Vector3 _velocity = Vector3.Zero;

        public FhPlatformEntity(FhPlatformEntityData data) : base(EntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            // todo: support loading genericmover, and do something similar for unused MPH models
            string name = "platform";
            ModelInstance inst = Read.GetModelInstance(name, firstHunt: true);
            ModelMetadata modelMeta = Metadata.FirstHuntModels[name];
            Debug.Assert(modelMeta.CollisionPath != null);
            SetCollision(Collision.GetCollision(modelMeta));
            _models.Add(inst);
            _speed = data.Speed.FloatValue / 2f;
            Debug.Assert(data.PositionCount >= 2 && data.PositionCount < 8);
            var posList = new List<Vector3>();
            for (int i = 0; i < data.PositionCount; i++)
            {
                posList.Add(data.Positions[i].ToFloatVector());
            }
            _posList = posList;
            _delay = data.Delay * 2;
            _moveTimer = _delay;
        }

        public override bool Process(Scene scene)
        {
            // todo: collision and stuff
            Position += _velocity;
            if (_moveTimer > 0)
            {
                _moveTimer--;
            }
            else
            {
                if (_state == MoveState.Sleep)
                {
                    // todo: must be active
                    // --> which specifically means a trigger volume needs to send an Activate message to this group this frame
                    _state = MoveState.MoveForward;
                    UpdateMovement();
                    _fromIndex++;
                }
                else if (_state == MoveState.MoveForward)
                {
                    _state = MoveState.Wait;
                    _velocity = Vector3.Zero;
                    Position = _posList[_toIndex]; // the game doesn't do this
                    if (_fromIndex == _posList.Count - 1)
                    {
                        _toIndex = _fromIndex - 1;
                    }
                    else
                    {
                        _fromIndex++;
                        _toIndex = _fromIndex + 1;
                    }
                    _moveTimer = _delay;
                }
                else if (_state == MoveState.Wait)
                {
                    _state = _toIndex >= _fromIndex ? MoveState.MoveForward : MoveState.MoveBackward;
                    UpdateMovement();
                }
                else if (_state == MoveState.MoveBackward)
                {
                    _velocity = Vector3.Zero;
                    Position = _posList[_toIndex]; // the game doesn't do this
                    if (_toIndex > 0)
                    {
                        _state = MoveState.Wait;
                        _fromIndex--;
                        _toIndex = _fromIndex - 1;
                    }
                    else
                    {
                        _state = MoveState.Sleep;
                        _fromIndex = 0;
                        _toIndex = 1;
                    }
                    _moveTimer = _delay;
                }
            }
            return base.Process(scene);
        }

        private void UpdateMovement()
        {
            Vector3 velocity = _posList[_toIndex] - _posList[_fromIndex];
            float distance = velocity.Length;
            _moveTimer = (int)(distance / _speed);
            float factor = _speed / distance;
            _velocity = velocity * factor;
        }

        private enum MoveState : byte
        {
            Sleep,
            MoveForward,
            Wait,
            MoveBackward
        }
    }
}
