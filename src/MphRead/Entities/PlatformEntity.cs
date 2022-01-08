using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlatformEntity : EntityBase
    {
        private readonly PlatformEntityData _data;
        private readonly PlatformMetadata _meta;

        // used for ID 2 (energyBeam, arcWelder)
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x2F, 0x4F, 0x4F).AsVector4();

        public PlatformFlags Flags { get; private set; }
        public PlatStateFlags StateFlags => _stateFlags;
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
        private int _ammo = 0;

        private int _health = 0;
        private int _halfHealth = 0;
        private readonly Effectiveness[] _beamEffectiveness = new Effectiveness[9];

        private ushort _timeSincePlayerCol = 0;
        private bool _playerCol = false;
        private PlatformEntity? _parent = null;
        private EntityCollision? _parentEntCol = null;
        private EntityBase? _scanMessageTarget = null;
        private EntityBase? _hitMessageTarget = null;
        private EntityBase? _playerColMessageTarget = null;
        private EntityBase? _deathMessageTarget = null;
        private readonly EntityBase?[] _lifetimeMessageTargets = new EntityBase?[4];
        private readonly Message[] _lifetimeMessages = new Message[4];
        private readonly int[] _lifetimeMessageParam1s = new int[4];
        private readonly int[] _lifetimeMessageParam2s = new int[4];
        private readonly int[] _lifetimeMessageIndices = new int[4];

        private PlatAnimFlags _animFlags = PlatAnimFlags.None;
        private PlatStateFlags _stateFlags = PlatStateFlags.None;
        private PlatformState _state = PlatformState.Inactive;
        private int _fromIndex = 0;
        private int _toIndex = 1;
        private readonly int _delay;
        private int _moveTimer;
        private int _recoilTimer;
        private readonly float _forwardSpeed;
        private readonly float _backwardSpeed;
        private int _currentAnim = 0;

        // todo: would be nice to have the ability to manipulate these transforms manually
        private readonly Vector3 _posOffset;
        private Vector3 _curPosition;
        private Vector4 _curRotation;
        private Vector4 _fromRotation;
        private Vector4 _toRotation;
        private readonly IReadOnlyList<Vector3> _posList;
        private readonly IReadOnlyList<Vector4> _rotList;
        private Vector3 _velocity = Vector3.Zero;
        private float _movePercent = 0;
        private float _moveIncrement = 0;
        private Vector3 _visiblePosition;
        private Vector3 _prevVisiblePosition;

        private static BeamProjectileEntity[] _beams = null!;

        public PlatformEntityData Data => _data;
        public Vector3 Velocity => _velocity;

        public PlatformEntity(PlatformEntityData data, Scene scene) : base(EntityType.Platform, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            Flags = data.Flags;
            if (Flags.TestFlag(PlatformFlags.Breakable))
            {
                Flags |= PlatformFlags.BeamColEffect;
                Flags |= PlatformFlags.HideOnSleep;
                Flags |= PlatformFlags.BeamTarget;
            }
            _health = (int)data.Health;
            if (Flags.TestFlag(PlatformFlags.SyluxShip))
            {
                _halfHealth = _health / 2;
            }
            Metadata.LoadEffectiveness(_data.Effectiveness, _beamEffectiveness);
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _curPosition = Position;
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
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                AddPlaceholderModel();
                _meta = Metadata.InvisiblePlat;
            }
            else
            {
                _meta = meta;
                if (_meta.Lighting)
                {
                    _anyLighting = true;
                }
                ModelInstance inst = SetUpModel(_meta.Name);
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
                    inst.SetAnimation(-1);
                }
            }
            _beamInterval = (int)data.BeamInterval * 2; // todo: FPS stuff
            if (_beams == null)
            {
                _beams = SceneSetup.CreateBeamList(64, scene); // in-game: 18
            }
            if (data.BeamId > -1)
            {
                _ammo = 1000;
                Debug.Assert(data.BeamId < Weapons.PlatformWeapons.Count);
                _equipInfo = new EquipInfo(Weapons.PlatformWeapons[data.BeamId], _beams);
                _equipInfo.GetAmmo = () => _ammo;
                _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
                _beamSpawnPos = data.BeamSpawnPos.ToFloatVector();
                _beamSpawnDir = data.BeamSpawnDir.ToFloatVector();
                _beamIntervalIndex = 15;
            }
            _delay = data.Delay * 2; // todo: FPS stuff
            _moveTimer = _delay;
            _recoilTimer = 0;
            _forwardSpeed = data.ForwardSpeed.FloatValue / 2f; // todo: FPS stuff
            _backwardSpeed = data.ForwardSpeed.FloatValue / 2f; // todo: FPS stuff
            UpdatePosition();
            _animFlags |= PlatAnimFlags.Draw;
            // todo: room state
            if (Flags.TestFlag(PlatformFlags.SamusShip))
            {
                SleepWake(wake: true, instant: true);
                _currentAnim = -2;
                // todo: room state for initial landed
                // --> options are instant_wake and wake, but it seems like it should be instant_sleep?
                SetPlatAnimation(PlatAnimId.InstantSleep, AnimFlags.None);
                _animFlags |= PlatAnimFlags.Active;
            }
            else
            {
                if (Flags.TestFlag(PlatformFlags.StartSleep))
                {
                    SleepWake(wake: false, instant: true);
                }
                else
                {
                    SleepWake(wake: true, instant: true);
                }
                // todo: more room state
                //_animFlags |= PlatAnimFlags.Active;
                if (_animFlags.TestFlag(PlatAnimFlags.Active))
                {
                    Activate();
                }
                else
                {
                    Deactivate();
                }
                _currentAnim = -2;
                if (_animFlags.TestFlag(PlatAnimFlags.HasAnim))
                {
                    if (_animFlags.TestFlag(PlatAnimFlags.Active))
                    {
                        SetPlatAnimation(PlatAnimId.InstantWake, AnimFlags.None);
                    }
                    else
                    {
                        SetPlatAnimation(PlatAnimId.InstantSleep, AnimFlags.None);
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (Flags.TestFlag(PlatformFlags.SamusShip))
            {
                _effectNodeIds[0] = _models[0].Model.GetNodeIndexByName("R_Turret");
                _effectNodeIds[1] = _models[0].Model.GetNodeIndexByName("R_Turret1");
                _effectNodeIds[2] = _models[0].Model.GetNodeIndexByName("R_Turret2");
                _effectNodeIds[3] = _models[0].Model.GetNodeIndexByName("R_Turret3");
                if (_effectNodeIds[0] != -1 || _effectNodeIds[1] != -1 || _effectNodeIds[2] != -1 || _effectNodeIds[3] != -1)
                {
                    _scene.LoadEffect(_nozzleEffectId);
                }
            }
            if (_data.ResistEffectId != 0)
            {
                _scene.LoadEffect(_data.ResistEffectId);
            }
            if (_data.DamageEffectId != 0)
            {
                _scene.LoadEffect(_data.DamageEffectId);
            }
            if (_data.DeadEffectId != 0)
            {
                _scene.LoadEffect(_data.DeadEffectId);
            }
            if (_data.BeamId == 0 && Flags.TestFlag(PlatformFlags.BeamSpawner))
            {
                _scene.LoadEffect(183); // syluxMissile
                _scene.LoadEffect(184); // syluxMissileCol
                _scene.LoadEffect(185); // syluxMissileFlash
            }
            _scene.TryGetEntity(_data.ScanMsgTarget, out _scanMessageTarget); // todo: send scan message
            _scene.TryGetEntity(_data.BeamHitMsgTarget, out _hitMessageTarget);
            _scene.TryGetEntity(_data.PlayerColMsgTarget, out _playerColMessageTarget);
            _scene.TryGetEntity(_data.DeadMsgTarget, out _deathMessageTarget);
            _scene.TryGetEntity(_data.LifetimeMsg1Target, out EntityBase? lifetimeMessageTarget);
            _lifetimeMessageTargets[0] = lifetimeMessageTarget;
            _scene.TryGetEntity(_data.LifetimeMsg2Target, out lifetimeMessageTarget);
            _lifetimeMessageTargets[1] = lifetimeMessageTarget;
            _scene.TryGetEntity(_data.LifetimeMsg3Target, out lifetimeMessageTarget);
            _lifetimeMessageTargets[2] = lifetimeMessageTarget;
            _scene.TryGetEntity(_data.LifetimeMsg4Target, out lifetimeMessageTarget);
            _lifetimeMessageTargets[3] = lifetimeMessageTarget;
            _lifetimeMessages[0] = _data.LifetimeMessage1;
            _lifetimeMessages[1] = _data.LifetimeMessage2;
            _lifetimeMessages[2] = _data.LifetimeMessage3;
            _lifetimeMessages[3] = _data.LifetimeMessage4;
            _lifetimeMessageParam1s[0] = _data.LifetimeMsg1Param1;
            _lifetimeMessageParam1s[1] = _data.LifetimeMsg2Param1;
            _lifetimeMessageParam1s[2] = _data.LifetimeMsg3Param1;
            _lifetimeMessageParam1s[3] = _data.LifetimeMsg4Param1;
            _lifetimeMessageParam2s[0] = _data.LifetimeMsg1Param2;
            _lifetimeMessageParam2s[1] = _data.LifetimeMsg2Param2;
            _lifetimeMessageParam2s[2] = _data.LifetimeMsg3Param2;
            _lifetimeMessageParam2s[3] = _data.LifetimeMsg4Param2;
            _lifetimeMessageIndices[0] = _data.LifetimeMsg1Index;
            _lifetimeMessageIndices[1] = _data.LifetimeMsg2Index;
            _lifetimeMessageIndices[2] = _data.LifetimeMsg3Index;
            _lifetimeMessageIndices[3] = _data.LifetimeMsg4Index;
            if (_data.ParentId != -1)
            {
                if (_scene.TryGetEntity(_data.ParentId, out EntityBase? parent) && parent.Type == EntityType.Platform)
                {
                    // only relevant for SyluxShip/Turret
                    _parent = (PlatformEntity)parent;
                    _parentEntCol = _parent.EntityCollision[0];
                }
            }
            Matrix4 transform = GetTransform();
            _visiblePosition = transform.Row3.Xyz;
        }

        public override void Destroy()
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                EffectEntry? effectEntry = _effects[i];
                if (effectEntry != null)
                {
                    _scene.UnlinkEffectEntry(effectEntry);
                }
            }
        }

        public override void GetPosition(out Vector3 position)
        {
            GetVectors(out position, out _, out _);
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            if (Flags.TestFlag(PlatformFlags.SamusShip))
            {
                Matrix4 transform = GetTransform();
                Vector3 offset = Matrix.Vec3MultMtx3(new Vector3(0, 0.8f, 4.2f), transform);
                position = transform.Row3.Xyz + offset;
            }
            else if (Flags.TestFlag(PlatformFlags.SyluxShip) && EntityCollision[0] != null)
            {
                Matrix4 transform = GetTransform();
                position = Matrix.Vec3MultMtx4(_beamSpawnPos, transform);
            }
            else
            {
                position = _visiblePosition;
            }
            up = UpVector;
            facing = FacingVector;
        }

        private void SetPlatAnimation(PlatAnimId id, AnimFlags flags)
        {
            int index = _meta.AnimationIds[(int)id];
            SetPlatAnimation(index, flags);
        }

        private void SetPlatAnimation(int index, AnimFlags flags)
        {
            _currentAnim = index;
            if (index >= 0)
            {
                Debug.Assert(!_models[0].IsPlaceholder);
                _models[0].SetAnimation(index, flags);
            }
        }

        private int GetAnimation(PlatAnimId id)
        {
            return _meta.AnimationIds[(int)id];
        }

        private void SleepWake(bool wake, bool instant)
        {
            if (wake)
            {
                _stateFlags |= PlatStateFlags.Awake;
                if (instant)
                {
                    SetPlatAnimation(PlatAnimId.InstantWake, AnimFlags.None);
                }
                else
                {
                    SetPlatAnimation(PlatAnimId.Wake, AnimFlags.NoLoop);
                    _currentAnim = GetAnimation(PlatAnimId.InstantWake);
                }
            }
            else
            {
                if (_stateFlags.TestFlag(PlatStateFlags.Awake))
                {
                    _stateFlags |= PlatStateFlags.WasAwake;
                }
                _stateFlags &= ~PlatStateFlags.Awake;
                if (instant)
                {
                    SetPlatAnimation(PlatAnimId.InstantSleep, AnimFlags.None);
                }
                else
                {
                    SetPlatAnimation(PlatAnimId.Sleep, AnimFlags.NoLoop);
                    _currentAnim = GetAnimation(PlatAnimId.InstantSleep);
                }
                if (Flags.TestFlag(PlatformFlags.HideOnSleep))
                {
                    for (int i = 0; i < EntityCollision.Length; i++)
                    {
                        EntityCollision? entCol = EntityCollision[i];
                        if (entCol != null)
                        {
                            entCol.Collision.Active = false;
                        }
                    }
                    _animFlags &= ~PlatAnimFlags.Draw;
                    // todo: stop sfx, room state
                }
            }
        }

        private void Activate()
        {
            _animFlags |= PlatAnimFlags.Active;
            // todo: more room state
            if (_state == PlatformState.Inactive)
            {
                if (Flags.TestFlag(PlatformFlags.DripMoat))
                {
                    _scene.SendMessage(Message.DripMoatPlatform, this, PlayerEntity.Main, 1, 0);
                }
                if (_data.PositionCount >= 2)
                {
                    UpdateMovement();
                    _state = PlatformState.Waiting;
                    _moveTimer = 0;
                    _stateFlags |= PlatStateFlags.Activated;
                    if (_fromIndex == _data.PositionCount - 1)
                    {
                        _stateFlags |= PlatStateFlags.Reverse;
                    }
                    else
                    {
                        _stateFlags &= ~PlatStateFlags.Reverse;
                    }
                }
            }
        }

        private void Deactivate()
        {
            if (_state != PlatformState.Inactive)
            {
                _state = PlatformState.Inactive;
                _animFlags &= ~PlatAnimFlags.Active;
                // todo: more room state
                if (Flags.TestFlag(PlatformFlags.DripMoat))
                {
                    _scene.SendMessage(Message.DripMoatPlatform, this, PlayerEntity.Main, 0, 0);
                }
            }
        }

        public override void SetActive(bool active)
        {
            if (active)
            {
                _stateFlags |= PlatStateFlags.Activated;
                Activate();
            }
            else
            {
                _stateFlags &= ~PlatStateFlags.Activated;
                Deactivate();
            }
        }

        public override bool Process()
        {
            UpdateLinkedInverse(0);
            _prevVisiblePosition = _visiblePosition;
            if (++_timeSincePlayerCol >= 3 * 2) // todo: FPS stuff
            {
                _timeSincePlayerCol = 3 * 2;
                _playerCol = false;
            }
            if (!_animFlags.TestFlag(PlatAnimFlags.DisableReflect))
            {
                bool isTurret = false;
                bool turretAiming = false;
                if (Flags.TestFlag(PlatformFlags.SyluxShip) && _parent != null)
                {
                    isTurret = true;
                    if (_stateFlags.TestFlag(PlatStateFlags.Awake)
                        && _models[0].AnimInfo.Index[0] == GetAnimation(PlatAnimId.InstantWake))
                    {
                        turretAiming = true;
                    }
                }
                if (Flags.TestFlag(PlatformFlags.SyluxShip)
                    && (isTurret || _stateFlags.TestFlag(PlatStateFlags.Awake) || _stateFlags.TestFlag(PlatStateFlags.WasAwake)))
                {
                    Vector3 target = Vector3.Zero;
                    if (isTurret)
                    {
                        Debug.Assert(_parentEntCol != null);
                        if (turretAiming)
                        {
                            PlayerEntity mainPlayer = PlayerEntity.Main;
                            target = new Vector3(
                                mainPlayer.Position.X - _visiblePosition.X,
                                mainPlayer.Position.Y + 1 - _visiblePosition.Y,
                                mainPlayer.Position.Z - _visiblePosition.Z
                            );
                        }
                        else
                        {
                            target = Matrix.Vec3MultMtx3(Vector3.UnitZ, _parentEntCol.Transform);
                        }
                        target = target.Normalized();
                    }
                    else
                    {
                        PlayerEntity mainPlayer = PlayerEntity.Main;
                        target = new Vector3(
                            mainPlayer.Position.X - _visiblePosition.X,
                            0,
                            mainPlayer.Position.Z - _visiblePosition.Z
                        ).Normalized();
                    }
                    Vector3 cross1 = Vector3.Cross(Vector3.UnitY, target).Normalized();
                    Vector3 cross2 = Vector3.Cross(target, cross1).Normalized();
                    Vector4 rotation = ChooseVectors(cross1, cross2, target);
                    if (!_models[0].IsPlaceholder)
                    {
                        float pct = Fixed.ToFloat(_parent == null ? 64 : 256);
                        rotation = ComputeRotationSin(_curRotation, rotation, pct);
                    }
                    _curRotation = rotation.Normalized();
                    if (_data.MovementType == 0)
                    {
                        UpdateState();
                        _curPosition += _velocity;
                    }
                }
                else
                {
                    if (_data.MovementType == 1)
                    {
                        // never true in-game
                        _curPosition += _velocity;
                        _movePercent += _moveIncrement;
                        _curRotation = ComputeRotationLinear(_fromRotation, _toRotation, _movePercent);
                    }
                    else if (_data.MovementType == 0)
                    {
                        UpdateState();
                        _curPosition += _velocity;
                        _movePercent += _moveIncrement;
                        _curRotation = ComputeRotationSin(_fromRotation, _toRotation, _movePercent);
                    }
                    if (_animFlags.TestFlag(PlatAnimFlags.SeekPlayerHeight) && PlayerEntity.PlayerCount > 0)
                    {
                        // also never true in-game
                        PlayerEntity mainPlayer = PlayerEntity.Main;
                        float offset = (mainPlayer.Position.Y - _curPosition.Y) * Fixed.ToFloat(20);
                        _curPosition.Y += offset;
                    }
                }
            }
            _visiblePosition = GetTransform().Row3.Xyz;
            bool spawnBeam = true;
            if (!_models[0].IsPlaceholder && Flags.TestFlag(PlatformFlags.SyluxShip))
            {
                if (_currentAnim != -2)
                {
                    spawnBeam = false;
                }
                else if (_parent != null
                    && !_parent.StateFlags.TestFlag(PlatStateFlags.Awake) && !_parent.StateFlags.TestFlag(PlatStateFlags.WasAwake))
                {
                    spawnBeam = false;
                }
            }
            _stateFlags |= PlatStateFlags.Awake;
            // btodo: 0 is valid for Sylux turret missiles, but without collision handling those would eat up the effect lists
            if (spawnBeam && _animFlags.TestFlag(PlatAnimFlags.Draw) && !_animFlags.TestFlag(PlatAnimFlags.DisableReflect)
                && _stateFlags.TestFlag(PlatStateFlags.Awake) && Flags.TestFlag(PlatformFlags.BeamSpawner) && _data.BeamId > 0)
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
                    Matrix4 transform = GetTransform();
                    Vector3 spawnPos = Matrix.Vec3MultMtx4(_beamSpawnPos, transform);
                    Vector3 spawnDir = Matrix.Vec3MultMtx3(_beamSpawnDir, transform).Normalized();
                    BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, spawnDir, BeamSpawnFlags.None, _scene);
                    if (!_equipInfo.Weapon.Flags.TestFlag(WeaponFlags.RepeatFire))
                    {
                        _beamActive = false;
                    }
                }
            }
            if (!_models[0].IsPlaceholder && _animFlags.TestFlag(PlatAnimFlags.HasAnim) && _currentAnim >= 0)
            {
                UpdateAnimFrames(_models[0]);
            }
            if (_currentAnim != -2 && _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                SetPlatAnimation(_currentAnim, AnimFlags.None);
                _currentAnim = -2;
                _stateFlags &= ~PlatStateFlags.WasAwake;
            }
            // todo: SFX and other stuff
            // todo: if "is_visible" returns false (and other conditions), don't draw the effects
            Model model = _models[0].Model;
            for (int i = 0; i < 4; i++)
            {
                if (_effectNodeIds[i] >= 0 && _effects[i] == null)
                {
                    Matrix4 transform = Matrix.GetTransform4(Vector3.UnitX, Vector3.UnitY, new Vector3(0, 2, 0));
                    _effects[i] = _scene.SpawnEffectGetEntry(_nozzleEffectId, transform);
                    _effects[i]!.SetElementExtension(true);
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
                    _effects[i]!.Transform(position, transform);
                }
            }
            if (_data.PositionCount > 0)
            {
                Transform = GetTransform();
            }
            // todo?: for some reason, the game uses an inverse view matrix when using animation
            UpdateCollisionTransform(0, CollisionTransform);
            return true;
        }

        // todo: use more flags
        public override void GetDrawInfo()
        {
            if (_animFlags.TestFlag(PlatAnimFlags.Draw) || _models[0].IsPlaceholder)
            {
                base.GetDrawInfo();
            }
        }

        private void UpdatePosition()
        {
            if (_data.PositionCount > 0)
            {
                _curPosition = _posList[_fromIndex];
                _curRotation = _fromRotation = _rotList[_fromIndex];
            }
        }

        private void UpdateMovement()
        {
            UpdatePosition();
            Vector3 velocity = _posList[_toIndex] - _posList[_fromIndex];
            float speed = _stateFlags.TestFlag(PlatStateFlags.Reverse) ? _backwardSpeed : _forwardSpeed;
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

        private static Vector4 ChooseVectors(Vector3 vec1, Vector3 vec2, Vector3 vec3)
        {
            float sqrt;
            float inv;
            if (vec3.Z + vec1.X + vec2.Y >= 0)
            {
                sqrt = MathF.Sqrt(vec3.Z + vec1.X + vec2.Y + 1);
                inv = 1 / sqrt / 2f;
                return new Vector4(
                    (vec2.Z - vec3.Y) * inv,
                    (vec3.X - vec1.Z) * inv,
                    (vec1.Y - vec2.X) * inv,
                    sqrt / 2f
                );
            }
            if (vec2.Y <= vec1.X)
            {
                if (vec3.Z <= vec1.X)
                {
                    sqrt = MathF.Sqrt(vec1.X - (vec2.Y + vec3.Z) + 1);
                    inv = 1 / sqrt / 2f;
                    return new Vector4(
                        sqrt / 2f,
                        (vec2.X + vec1.Y) * inv,
                        (vec1.Z + vec3.X) * inv,
                        (vec2.Z - vec3.Y) * inv
                    );
                }
            }
            else if (vec3.Z <= vec2.Y)
            {
                sqrt = MathF.Sqrt(vec2.Y - (vec3.Z + vec1.X) + 1);
                inv = 1 / sqrt / 2f;
                return new Vector4(
                    (vec2.X + vec1.Y) * inv,
                    (vec3.Y + vec2.Z) * inv,
                    sqrt / 2f,
                    (vec3.X - vec1.Z) * inv
                );
            }
            sqrt = MathF.Sqrt(vec3.Z - (vec1.X + vec2.Y) + 1);
            inv = 1 / sqrt / 2f;
            return new Vector4(
                (vec1.Z + vec3.X) * inv,
                (vec3.Y + vec2.Z) * inv,
                sqrt / 2f,
                (vec1.Y - vec2.X) * inv
            );
        }

        private static Vector4 ComputeRotationLinear(Vector4 fromRot, Vector4 toRot, float pct)
        {
            if (pct <= 0)
            {
                return fromRot;
            }
            if (pct >= 1)
            {
                return toRot;
            }
            return new Vector4(
                ((1 - pct) * fromRot.X) + (pct * toRot.X),
                ((1 - pct) * fromRot.Y) + (pct * toRot.Y),
                ((1 - pct) * fromRot.Z) + (pct * toRot.Z),
                ((1 - pct) * fromRot.W) + (pct * toRot.W)
            );
        }

        private static Vector4 ComputeRotationSin(Vector4 fromRot, Vector4 toRot, float pct)
        {
            pct = Math.Clamp(pct, 0, 1);
            float dot = Vector4.Dot(fromRot, toRot);
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
            return new Vector4(
                factor * fromRot.X + pct * toRot.X,
                factor * fromRot.Y + pct * toRot.Y,
                factor * fromRot.Z + pct * toRot.Z,
                factor * fromRot.W + pct * toRot.W
            ).Normalized();
        }

        public void Recoil()
        {
            if (_state == PlatformState.Moving && !Flags.TestFlag(PlatformFlags.NoRecoil) && _data.ForCutscene == 0)
            {
                _recoilTimer = 31 * 2; // todo: FPS stuff
                _moveTimer += 60 * 2; // todo: FPS stuff
                _velocity.Y *= -1;
            }
        }

        private void UpdateState()
        {
            if (_state == PlatformState.Moving)
            {
                if (_recoilTimer > 0)
                {
                    // when recoil collision is detected, recoil_timer is set to 31f (and 60f is added to move_timer)
                    _recoilTimer--;
                    if (_recoilTimer == 0)
                    {
                        _velocity.Y *= -1;
                    }
                }
                if (_moveTimer > 0)
                {
                    _moveTimer--;
                    // todo: sfx everywhere
                }
                else
                {
                    _fromIndex = _toIndex;
                    for (int i = 0; i < _lifetimeMessages.Length; i++)
                    {
                        if (_lifetimeMessageIndices[i] == _fromIndex)
                        {
                            Message message = _lifetimeMessages[i];
                            EntityBase? target = _lifetimeMessageTargets[i];
                            if (message != Message.None && target != null)
                            {
                                _scene.SendMessage(message, this, target,
                                    _lifetimeMessageParam1s[i], _lifetimeMessageParam2s[i]);
                            }
                        }
                    }
                    // todo: room state etc.
                    if (_state != PlatformState.Inactive)
                    {
                        _state = PlatformState.Waiting;
                        _moveTimer = _delay;
                    }
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
                    if (_stateFlags.TestFlag(PlatStateFlags.Activated))
                    {
                        if (_data.ReverseType == 0)
                        {
                            // reverse when reaching last position
                            if (_stateFlags.TestFlag(PlatStateFlags.Reverse))
                            {
                                if (_fromIndex == 0)
                                {
                                    _stateFlags &= ~PlatStateFlags.Reverse;
                                }
                            }
                            else if (_fromIndex == _data.PositionCount - 1)
                            {
                                _stateFlags |= PlatStateFlags.Reverse;
                            }
                            _toIndex = _fromIndex + (_stateFlags.TestFlag(PlatStateFlags.Reverse) ? -1 : 1);
                            _state = PlatformState.Moving;
                        }
                        else if (_data.ReverseType == 1)
                        {
                            // wrap around from last position to first
                            int index = _fromIndex + (_stateFlags.TestFlag(PlatStateFlags.Reverse) ? -1 : 1);
                            _toIndex = index % _data.PositionCount;
                            _state = PlatformState.Moving;
                        }
                        else if (_data.ReverseType == 2)
                        {
                            // deactivate when reaching last position and wait for another activation to reverse
                            if ((_stateFlags.TestFlag(PlatStateFlags.Reverse) && _fromIndex == 0)
                                || (!_stateFlags.TestFlag(PlatStateFlags.Reverse) && _fromIndex == _data.PositionCount - 1))
                            {
                                Deactivate();
                                if (Flags.TestFlag(PlatformFlags.SleepAtEnd))
                                {
                                    SleepWake(wake: false, instant: false);
                                }
                            }
                            if (_state != PlatformState.Inactive)
                            {
                                _toIndex = _fromIndex + (_stateFlags.TestFlag(PlatStateFlags.Reverse) ? -1 : 1);
                                _state = PlatformState.Moving;
                            }
                        }
                    }
                    if (_state == PlatformState.Moving)
                    {
                        UpdateMovement();
                    }
                }
            }
            else if (_state == PlatformState.Inactive)
            {
                if (_moveTimer > 0)
                {
                    _moveTimer--;
                }
                else if (_animFlags.TestFlag(PlatAnimFlags.Active))
                {
                    Activate();
                }
            }
        }

        private Matrix4 GetTransform()
        {
            Matrix4 transform;
            if (_data.PositionCount > 0)
            {
                transform = GetTransformMatrix();
                if (_posOffset != Vector3.Zero)
                {
                    transform.Row3.Xyz += Matrix.Vec3MultMtx3(_posOffset, transform);
                }
                if (_parentEntCol != null)
                {
                    if (Flags.TestFlag(PlatformFlags.SyluxShip))
                    {
                        transform.Row3.Xyz = Matrix.Vec3MultMtx4(transform.Row3.Xyz, _parentEntCol.Transform);
                    }
                    else
                    {
                        transform *= _parentEntCol.Transform;
                    }
                }
            }
            else
            {
                Debug.Assert(_parentEntCol == null);
                transform = Transform;
            }
            return transform;
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            return Matrix4.CreateScale(inst.Model.Scale) * GetTransform();
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
                _curPosition.X, _curPosition.Y, _curPosition.Z, 1
            );
        }

        public override void CheckContactDamage(ref DamageResult result)
        {
            result.Damage = _data.ContactDamage;
            // probably not important in practice, but this logic could lead to weird behavior from not updating TakeDamage
            if (!Flags.TestFlag(PlatformFlags.Hazard))
            {
                result.TakeDamage = false;
            }
            else if (Flags.TestFlag(PlatformFlags.ContactDamage))
            {
                result.TakeDamage = true;
            }
        }

        public override void CheckBeamReflection(ref bool result)
        {
            // similarly non-airtight logic here
            if (!Flags.TestFlag(PlatformFlags.BeamReflection) || _animFlags.TestFlag(PlatAnimFlags.DisableReflect))
            {
                if (!Flags.TestFlag(PlatformFlags.DamageReflect1))
                {
                    result = false;
                }
                else if (Flags.TestFlag(PlatformFlags.DamageReflect2))
                {
                    result = true;
                }
            }
            else
            {
                result = true;
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            void OnActivate()
            {
                Activate();
                _beamIntervalTimer = _beamInterval;
                _beamIntervalIndex = 15;
                _beamActive = false;
            }

            // the game updates unused flags for SamusShip
            if (!Flags.TestFlag(PlatformFlags.SamusShip))
            {
                if (StateFlags.TestFlag(PlatStateFlags.Awake))
                {
                    if (info.Message == Message.Activate)
                    {
                        OnActivate();
                    }
                    else if (info.Message == Message.SetActive)
                    {
                        if ((int)info.Param1 != 0)
                        {
                            OnActivate();
                        }
                        else
                        {
                            Deactivate();
                        }
                    }
                    else if (info.Message == Message.Damage)
                    {
                        _health = (int)info.Param1;
                    }
                    else if (info.Message == Message.SetSeekPlayerY)
                    {
                        _animFlags &= ~PlatAnimFlags.SeekPlayerHeight;
                        if ((int)info.Param1 != 0)
                        {
                            _animFlags |= PlatAnimFlags.SeekPlayerHeight;
                        }
                    }
                    else if (info.Message == Message.SetBeamReflection)
                    {
                        _animFlags &= ~PlatAnimFlags.DisableReflect;
                        if ((int)info.Param1 != 0)
                        {
                            _animFlags |= PlatAnimFlags.DisableReflect;
                        }
                    }
                    else if (info.Message == Message.PlatformSleep)
                    {
                        SleepWake(wake: false, instant: false);
                        Deactivate();
                    }
                    else if (info.Message == Message.BeamCollideWith)
                    {
                        if (_health > 0)
                        {
                            if (_data.BeamHitMessage != Message.None && _hitMessageTarget != null)
                            {
                                _scene.SendMessage(_data.BeamHitMessage, this, _hitMessageTarget,
                                    _data.BeamHitMsgParam1, _data.BeamHitMsgParam2);
                            }
                            int effectId = 0;
                            if (!Flags.TestFlag(PlatformFlags.BeamColEffect)
                                || Flags.TestFlag(PlatformFlags.BeamReflection) && !_animFlags.TestFlag(PlatAnimFlags.DisableReflect))
                            {
                                effectId = _data.ResistEffectId;
                            }
                            else
                            {
                                var beam = (BeamProjectileEntity)info.Sender;
                                // bug?: checking BeamKind instead of Beam here
                                if (_beamEffectiveness[(int)beam.BeamKind] == Effectiveness.Zero)
                                {
                                    effectId = _data.ResistEffectId;
                                }
                                else
                                {
                                    effectId = _data.DamageEffectId;
                                    _health -= (int)beam.Damage;
                                    if (_health <= _halfHealth)
                                    {
                                        if (_halfHealth > 0)
                                        {
                                            // SyluxShip/Turret at half health
                                            for (int i = 0; i < _lifetimeMessages.Length; i++)
                                            {
                                                Message message = _lifetimeMessages[i];
                                                EntityBase? target = _lifetimeMessageTargets[i];
                                                if (message != Message.None && target != null)
                                                {
                                                    _scene.SendMessage(message, this, target,
                                                        _lifetimeMessageParam1s[i], _lifetimeMessageParam2s[i]);
                                                }
                                            }
                                            _halfHealth = 0;
                                        }
                                        else
                                        {
                                            // destroyed
                                            effectId = _data.DeadEffectId;
                                            if (Flags.TestFlag(PlatformFlags.Breakable))
                                            {
                                                _scene.SendMessage(Message.PlatformSleep, this, this, 0, 0);
                                                // todo: play SFX
                                            }
                                            Vector3 spawnPos = _visiblePosition.AddY(1);
                                            ItemSpawnEntity.SpawnItemDrop(_data.ItemType, spawnPos, _data.ItemChance, _scene);
                                            if (_data.DeadMessage != Message.None && _deathMessageTarget != null)
                                            {
                                                _scene.SendMessage(_data.DeadMessage, this, _deathMessageTarget,
                                                    _data.DeadMsgParam1, _data.DeadMsgParam2);
                                            }
                                            _health = 0;
                                        }
                                    }
                                }
                                if (effectId != 0)
                                {
                                    var result = (CollisionResult)info.Param1;
                                    Debug.Assert(result.EntityCollision != null);
                                    Vector3 spawnPos = result.Position;
                                    Vector3 spawnUp = result.Plane.Xyz;
                                    spawnPos = Matrix.Vec3MultMtx4(spawnPos, result.EntityCollision.Inverse1);
                                    spawnUp = Matrix.Vec3MultMtx4(spawnUp, result.EntityCollision.Inverse1);
                                    Vector3 spawnFacing;
                                    if (spawnUp.Z <= -0.9f || spawnUp.Z >= 0.9f)
                                    {
                                        spawnFacing = Vector3.Cross(Vector3.UnitX, spawnUp).Normalized();
                                    }
                                    else
                                    {
                                        spawnFacing = Vector3.Cross(Vector3.UnitZ, spawnUp).Normalized();
                                    }
                                    _scene.SpawnEffect(effectId, spawnFacing, spawnUp, spawnPos, entCol: result.EntityCollision);
                                }
                            }
                        }
                    }
                    else if (info.Message == Message.PlayerCollideWith)
                    {
                        bool alreadyColliding = _playerCol;
                        _timeSincePlayerCol = 0;
                        _playerCol = true;
                        if (_data.PlayerColMessage != Message.None && _playerColMessageTarget != null && !alreadyColliding
                            && !(Flags.TestFlag(PlatformFlags.StandingColOnly) && (int)info.Param2 == 0))
                        {
                            _scene.SendMessage(_data.PlayerColMessage, this, _playerColMessageTarget,
                                _data.PlayerColMsgParam1, _data.PlayerColMsgParam2);
                        }
                    }
                }
                else if (info.Message == Message.PlatformWakeup)
                {
                    SleepWake(wake: true, instant: false);
                    if ((int)info.Param1 != 0)
                    {
                        OnActivate();
                    }
                }
                if (info.Message == Message.SetPlatformIndex)
                {
                    int index = (int)info.Param1 - 1;
                    if (_fromIndex != index)
                    {
                        if (_state == PlatformState.Inactive)
                        {
                            _fromIndex = index;
                            UpdatePosition();
                            Deactivate();
                            _velocity = Vector3.Zero;
                            _movePercent = 0;
                            _moveIncrement = 0;
                        }
                        else if ((int)info.Param2 != 0)
                        {
                            _fromIndex = index;
                            UpdatePosition();
                        }
                    }
                }
            }
        }
    }

    [Flags]
    public enum PlatAnimFlags : ushort
    {
        None = 0x0,
        Active = 0x1,
        DisableReflect = 0x2,
        Draw = 0x4,
        Bit03 = 0x8, // functionless
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
        BeamColEffect = 0x8,
        DamageReflect1 = 0x10,
        DamageReflect2 = 0x20,
        StandingColOnly = 0x40,
        StartSleep = 0x80,
        SleepAtEnd = 0x100,
        DripMoat = 0x200,
        SkipNodeRef = 0x400,
        DrawIfNodeRef = 0x800,
        DrawAlways = 0x1000,
        HideOnSleep = 0x2000,
        SyluxShip = 0x4000,
        Bit15 = 0x8000,
        BeamReflection = 0x10000,
        Bit17 = 0x20000,
        BeamTarget = 0x40000,
        SamusShip = 0x80000,
        Breakable = 0x100000,
        Bit21 = 0x200000,
        DrawIfCull = 0x400000,
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
    public enum PlatStateFlags : uint
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

        public FhPlatformEntity(FhPlatformEntityData data, Scene scene) : base(EntityType.Platform, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            // todo: support loading genericmover, and do something similar for unused MPH models
            string name = "platform";
            SetUpModel(name, firstHunt: true);
            ModelMetadata modelMeta = Metadata.FirstHuntModels[name];
            Debug.Assert(modelMeta.CollisionPath != null);
            SetCollision(Collision.GetCollision(modelMeta));
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

        public override bool Process()
        {
            // todo: collision and stuff
            Vector3 position = _position;
            position += _velocity;
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
                    position = _posList[_toIndex]; // the game doesn't do this
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
                    position = _posList[_toIndex]; // the game doesn't do this
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
            Position = position;
            base.Process();
            UpdateCollisionTransform(0, Transform);
            return true;
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
