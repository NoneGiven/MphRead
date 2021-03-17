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
        private readonly Vector3 _posOffset;

        private static readonly BeamProjectileEntity[] _beams = SceneSetup.CreateBeamList(64); // in-game: 18

        public PlatformEntity(PlatformEntityData data) : base(EntityType.Platform)
        {
            _data = data;
            Id = data.Header.EntityId;
            Flags = data.Flags;
            if (Flags.HasFlag(PlatformFlags.Breakable))
            {
                Flags |= PlatformFlags.Bit03;
                Flags |= PlatformFlags.Bit13;
                Flags |= PlatformFlags.BeamTarget;
            }
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                AddPlaceholderModel();
            }
            else
            {
                ModelInstance inst = Read.GetModelInstance(meta.Name);
                _models.Add(inst);
                ModelMetadata modelMeta = Metadata.ModelMetadata[meta.Name];
                if (modelMeta.CollisionPath != null)
                {
                    SetCollision(Collision.GetCollision(modelMeta), attach: inst);
                }
                // temporary
                if (meta.Name == "SamusShip")
                {
                    inst.SetNodeAnim(1);
                }
                else if (meta.Name == "SyluxTurret")
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
            _posOffset = data.PositionOffset.ToFloatVector();
        }

        public override void Initialize(Scene scene)
        {
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

        public override bool Process(Scene scene)
        {
            // btodo: the game does a bunch of flags checks for this
            // todo: 0 is valid for Sylux turret missiles, but without proper handling those would eat up the effect lists
            if (_data.BeamId > 0 && Flags.HasFlag(PlatformFlags.BeamSpawner))
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

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (_posOffset != Vector3.Zero)
            {
                transform.Row3.Xyz += Matrix.Vec3MultMtx3(_posOffset, transform);
            }
            return transform;
        }
    }

    [Flags]
    public enum PlatAnimFlags : ushort
    {
        None = 0x0,
        Active = 0x1,
        DisableReflect = 0x2,
        Draw = 0x4,
        Bit03 = 0x8,
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
        Bit07 = 0x80,
        Bit08 = 0x100,
        Bit09 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
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
        private readonly IReadOnlyList<Vector3> _positions;
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
            var positions = new List<Vector3>(0);
            for (int i = 0; i < data.PositionCount; i++)
            {
                positions.Add(data.Positions[i].ToFloatVector());
            }
            _positions = positions;
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
                    Position = _positions[_toIndex]; // the game doesn't do this
                    if (_fromIndex == _positions.Count - 1)
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
                    Position = _positions[_toIndex]; // the game doesn't do this
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
            Vector3 velocity = _positions[_toIndex] - _positions[_fromIndex];
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
