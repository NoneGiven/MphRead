using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public readonly struct EnemyInstanceEntityData
    {
        public readonly EnemyType Type;
        public readonly EntityBase Spawner; // todo: nullable?

        public EnemyInstanceEntityData(EnemyType type, EntityBase spawner)
        {
            Type = type;
            Spawner = spawner;
        }
    }

    public class EnemyInstanceEntity : EntityBase
    {
        protected readonly EnemyInstanceEntityData _data;
        protected ushort _framesSinceDamage = 510;
        protected ushort _health = 20;
        protected ushort _healthMax = 20;
        protected EntityBase? _owner = null;
        protected CollisionVolume _hurtVolume = default;
        protected CollisionVolume _hurtVolumeInit = default;
        protected byte _state1 = 0; // todo: names ("next?")
        protected byte _state2 = 0;
        public bool[] HitPlayers { get; } = new bool[4];
        protected Vector3 _prevPos = Vector3.Zero;
        protected Vector3 _speed = Vector3.Zero;
        protected float _boundingRadius = 0;
        protected Action[]? _stateProcesses;

        public byte State1 => _state1;
        public byte State2 => _state2;

        public readonly Effectiveness[] BeamEffectiveness = new Effectiveness[9];
        private bool _onlyMoveHurtVolume = false;
        private bool _noIneffectiveEffect = false;
        public EnemyFlags Flags { get; set; }
        public CollisionVolume HurtVolume => _hurtVolume;
        public EnemyType EnemyType => _data.Type;
        public EntityBase? Owner => _owner;

        protected static BeamProjectileEntity[] _beams = null!;

        public EnemyInstanceEntity(EnemyInstanceEntityData data, Scene scene) : base(EntityType.EnemyInstance, scene)
        {
            _data = data;
            if (_beams == null)
            {
                _beams = SceneSetup.CreateBeamList(64, scene); // in-game: 64
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            // todo: set other properties, etc.
            _owner = _data.Spawner;
            Metadata.LoadEffectiveness(_data.Type, BeamEffectiveness);
            Flags = EnemyFlags.CollidePlayer | EnemyFlags.CollideBeam;
            if (EnemyInitialize() && _data.Spawner is EnemySpawnEntity spawner)
            {
                // todo: linked entity collision transform -- although I don't think this is ever used for enemies/spawners
            }
            _prevPos = Position;
            if (_data.Type == EnemyType.Gorea1A || _data.Type == EnemyType.GoreaHead || _data.Type == EnemyType.GoreaArm
                || _data.Type == EnemyType.GoreaLeg || _data.Type == EnemyType.Gorea1B || _data.Type == EnemyType.GoreaSealSphere1
                || _data.Type == EnemyType.Trocra || _data.Type == EnemyType.Gorea2 || _data.Type == EnemyType.GoreaSealSphere2)
            {
                _onlyMoveHurtVolume = true;
            }
            if (_data.Type == EnemyType.Cretaphid || _data.Type == EnemyType.CretaphidEye || _data.Type == EnemyType.CretaphidCrystal
                || _data.Type == EnemyType.Unknown22 || _data.Type == EnemyType.Gorea1A || _data.Type == EnemyType.GoreaHead
                || _data.Type == EnemyType.GoreaArm || _data.Type == EnemyType.GoreaLeg || _data.Type == EnemyType.Gorea1B
                || _data.Type == EnemyType.GoreaSealSphere1 || _data.Type == EnemyType.Trocra || _data.Type == EnemyType.Gorea2
                || _data.Type == EnemyType.GoreaSealSphere2 || _data.Type == EnemyType.GoreaMeteor || _data.Type == EnemyType.Slench
                || _data.Type == EnemyType.SlenchShield || _data.Type == EnemyType.SlenchNest || _data.Type == EnemyType.FireSpawn
                || _data.Type == EnemyType.HitZone)
            {
                _noIneffectiveEffect = true;
            }
        }

        public override void GetPosition(out Vector3 position)
        {
            position = _hurtVolume.GetCenter();
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = _hurtVolume.GetCenter();
            up = UpVector;
            facing = FacingVector;
        }

        public override bool GetTargetable()
        {
            return _health != 0;
        }

        public void ClearHitPlayers()
        {
            HitPlayers[0] = false;
            HitPlayers[1] = false;
            HitPlayers[2] = false;
            HitPlayers[3] = false;
        }

        public override bool Process()
        {
            bool inRange = false;
            if (_data.Type == EnemyType.Spawner || Flags.TestFlag(EnemyFlags.NoMaxDistance))
            {
                inRange = true;
            }
            else
            {
                float distSqr = 35f * 35f;
                if (Owner?.Type == EntityType.EnemySpawn)
                {
                    var spawner = (EnemySpawnEntity)Owner;
                    distSqr = spawner.Data.EnemyActiveDistance.FloatValue;
                    distSqr *= distSqr;
                }

                bool CheckInRange(Vector3 pos)
                {
                    Vector3 between = Position - pos;
                    return between.LengthSquared < distSqr && between.Y > -15 && between.Y < 15;
                }
                if (PlayerEntity.FreeCamera) // skdebug
                {
                    inRange = CheckInRange(_scene.CameraPosition);
                }
                if (!inRange)
                {
                    if (_scene.Multiplayer)
                    {
                        for (int i = 0; i < _scene.Entities.Count; i++)
                        {
                            EntityBase entity = _scene.Entities[i];
                            if (entity.Type != EntityType.Player)
                            {
                                continue;
                            }
                            var player = (PlayerEntity)entity;
                            if (player.Health > 0 && CheckInRange(player.Position))
                            {
                                inRange = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        inRange = CheckInRange(PlayerEntity.Main.Position);
                    }
                }
            }
            if (inRange)
            {
                if (_framesSinceDamage < 510)
                {
                    _framesSinceDamage++;
                }
                if (_health > 0)
                {
                    _state1 = _state2;
                    if (!Flags.TestFlag(EnemyFlags.Static))
                    {
                        DoMovement();
                    }
                    // todo: positional audio, node ref
                    ClearHitPlayers();
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Player)
                        {
                            continue;
                        }
                        var player = (PlayerEntity)entity;
                        CollisionResult hitRes = default;
                        if (player.Health > 0 && CollisionDetection.CheckVolumesOverlap(player.Volume, HurtVolume, ref hitRes))
                        {
                            HitPlayers[player.SlotIndex] = true;
                            if (Flags.TestFlag(EnemyFlags.CollidePlayer))
                            {
                                player.HandleCollision(hitRes);
                            }
                        }
                    }
                    EnemyProcess();
                    if (!Flags.TestFlag(EnemyFlags.Static))
                    {
                        UpdateHurtVolume();
                    }
                    // todo: node ref
                    base.Process();
                    return true;
                }
                _scene.SendMessage(Message.Destroyed, this, _owner, 0, 0);
                if (_owner is EnemySpawnEntity spawner)
                {
                    Vector3 pos = _hurtVolume.GetCenter().AddY(0.5f);
                    ItemSpawnEntity.SpawnItemDrop(spawner.Data.ItemType, pos, spawner.Data.ItemChance, _scene);
                }
                return false;
            }
            _scene.SendMessage(Message.Destroyed, this, _owner, 1, 0);
            return false;
        }

        protected bool ContactDamagePlayer(uint damage, bool knockback)
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return false;
            }
            PlayerEntity.Main.TakeDamage(damage, DamageFlags.None, _speed, this);
            if (knockback)
            {
                Vector3 between = PlayerEntity.Main.Volume.SpherePosition - Position;
                float factor = between.Length * 5;
                PlayerEntity.Main.Speed = PlayerEntity.Main.Speed.AddX(between.X / factor).AddZ(between.Z / factor);
            }
            return true;
        }

        private void DoMovement()
        {
            _prevPos = Position;
            Position += _speed;
            UpdateHurtVolume();
        }

        private void UpdateHurtVolume()
        {
            if (_onlyMoveHurtVolume)
            {
                _hurtVolume = CollisionVolume.Move(_hurtVolumeInit, Position);
            }
            else
            {
                _hurtVolume = CollisionVolume.Transform(_hurtVolumeInit, Transform);
            }
        }

        public override void GetDrawInfo()
        {
            if (_health > 0 && Flags.TestFlag(EnemyFlags.Visible))
            {
                if (!EnemyGetDrawInfo())
                {
                    // todo: is_visible
                    if (_framesSinceDamage < 10)
                    {
                        PaletteOverride = Metadata.RedPalette;
                    }
                    base.GetDrawInfo();
                    PaletteOverride = null;
                }
            }
        }

        /// <summary>
        /// Must return true if overriden.
        /// </summary>
        protected virtual bool EnemyInitialize()
        {
            return false;
        }

        protected virtual void EnemyProcess()
        {
        }

        /// <summary>
        /// Must return true if overriden.
        /// </summary>
        protected virtual bool EnemyGetDrawInfo()
        {
            return false;
        }

        public void SetHealth(ushort health)
        {
            _health = health;
        }

        protected virtual void Detach()
        {
        }

        public bool CheckHitByBomb(BombEntity bomb)
        {
            if (Flags.TestFlag(EnemyFlags.Invincible))
            {
                return false;
            }
            Vector3 between = Position = bomb.Position;
            if (between.LengthSquared > bomb.Radius * bomb.Radius)
            {
                return false;
            }
            TakeDamage(bomb.EnemyDamage, bomb);
            _scene.SendMessage(Message.Impact, bomb, bomb.Owner, this, 0); // the game doesn't set anything as sender
            return true;
        }

        public void TakeDamage(uint damage, EntityBase? source)
        {
            ushort prevHealth = _health;
            Effectiveness effectiveness = Effectiveness.Normal;
            BeamProjectileEntity? beamSource = null;
            if (source?.Type == EntityType.BeamProjectile)
            {
                beamSource = (BeamProjectileEntity)source;
            }
            if (beamSource != null)
            {
                if (beamSource.Owner?.Type == EntityType.EnemyInstance)
                {
                    return;
                }
                effectiveness = GetEffectiveness(beamSource.Beam);
            }
            bool unaffected = false;
            if (effectiveness == Effectiveness.Zero || Flags.TestFlag(EnemyFlags.Invincible)
                || (source?.Type == EntityType.Bomb && Flags.TestFlag(EnemyFlags.NoBombDamage)))
            {
                unaffected = true;
            }
            bool dead = false;
            if (!unaffected)
            {
                if (beamSource?.Owner?.Type == EntityType.Player)
                {
                    damage = (uint)(damage * Metadata.GetDamageMultiplier(effectiveness));
                    if (damage == 0)
                    {
                        damage = 1;
                    }
                }
                if (damage >= _health)
                {
                    dead = true;
                    _health = 0;
                }
                else
                {
                    _health -= (ushort)damage;
                }
            }
            if (EnemyTakeDamage(source))
            {
                _health = prevHealth;
                unaffected = true;
                dead = false;
            }
            if (unaffected)
            {
                if (effectiveness == Effectiveness.Zero && !_noIneffectiveEffect)
                {
                    Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
                    transform.Row3.Xyz = _hurtVolume.GetCenter();
                    EffectEntry effect = _scene.SpawnEffectGetEntry(115, transform); // ineffectivePsycho
                    effect.SetReadOnlyField(0, _boundingRadius);
                    _scene.DetachEffectEntry(effect, setExpired: false);
                }
            }
            else
            {
                if (beamSource != null)
                {
                    beamSource.SpawnDamageEffect(effectiveness);
                }
                if (dead)
                {
                    // todo: update records
                    if (_data.Type == EnemyType.Temroid) // condition is not strictly necessary
                    {
                        Detach();
                    }
                    // todo: play SFX
                    int effectId;
                    if (EnemyType == EnemyType.FireSpawn)
                    {
                        Debug.Assert(_owner?.Type == EntityType.EnemySpawn);
                        var spawner = (EnemySpawnEntity)_owner;
                        effectId = spawner.Data.Fields.S06.EnemySubtype == 1 ? 217 : 218;
                    }
                    else
                    {
                        effectId = Metadata.GetEnemyDeathEffect(EnemyType);
                    }
                    Matrix4 transform = Transform.ClearScale();
                    _scene.SpawnEffect(effectId, transform);
                }
                else
                {
                    _framesSinceDamage = 0;
                    // todo: play SFX
                    switch (_data.Type)
                    {
                    case EnemyType.Zoomer:
                    case EnemyType.Petrasyl1:
                    case EnemyType.Petrasyl2:
                    case EnemyType.Petrasyl3:
                    case EnemyType.Petrasyl4:
                        _models[0].SetAnimation(1, AnimFlags.NoLoop);
                        break;
                    case EnemyType.Blastcap:
                        _models[0].SetAnimation(0, AnimFlags.NoLoop);
                        // 3 - blastCapHit
                        Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
                        transform.Row3.Xyz = Position;
                        _scene.SpawnEffect(3, transform);
                        break;
                    }
                }
            }
        }

        public Effectiveness GetEffectiveness(BeamType beam)
        {
            int index = (int)beam;
            Debug.Assert(index < BeamEffectiveness.Length);
            return BeamEffectiveness[index];
        }

        /// <summary>
        /// When overridden, must return true when unaffected by damage and false otherwise.
        /// </summary>
        protected virtual bool EnemyTakeDamage(EntityBase? source)
        {
            return false;
        }

        protected void CallStateProcess()
        {
            Debug.Assert(_stateProcesses != null && _state1 >= 0 && _state1 < _stateProcesses.Length);
            _stateProcesses[_state1].Invoke();
        }

        protected bool CallSubroutine<T>(IReadOnlyList<EnemySubroutine<T>> subroutines, T enemy) where T : EnemyInstanceEntity
        {
            Debug.Assert(enemy == this);
            EnemySubroutine<T> subroutine = subroutines[_state1];
            if (subroutine.Behaviors.Count == 0)
            {
                return false;
            }
            int index = 0;
            while (!subroutine.Behaviors[index].Function.Invoke(enemy))
            {
                index++;
                if (index >= subroutine.Behaviors.Count)
                {
                    return false;
                }
            }
            _state2 = subroutine.Behaviors[index].NextState;
            return true;
        }

        protected bool HandleBlockingCollision(Vector3 position, CollisionVolume volume, bool updateSpeed)
        {
            bool a = false;
            bool b = false;
            return HandleBlockingCollision(position, volume, updateSpeed, ref a, ref b);
        }

        protected bool HandleBlockingCollision(Vector3 position, CollisionVolume volume, bool updateSpeed, ref bool a6, ref bool a7)
        {
            int count = 0;
            var results = new CollisionResult[30];
            Vector3 pointOne = Vector3.Zero;
            Vector3 pointTwo = Vector3.Zero;
            if (volume.Type == VolumeType.Cylinder)
            {
                pointOne = _prevPos.AddY(0.5f);
                pointTwo = Position.AddY(0.5f);
                count = CollisionDetection.CheckSphereBetweenPoints(pointOne, pointTwo, volume.CylinderRadius, limit: 30,
                    includeOffset: false, TestFlags.None, _scene, results);
            }
            else
            {
                count = CollisionDetection.CheckInRadius(position, _boundingRadius, limit: 30,
                    getSimpleNormal: false, TestFlags.None, _scene, results);
            }
            a6 = false;
            if (count == 0)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                CollisionResult result = results[i];
                float v18;
                if (result.Field0 != 0)
                {
                    v18 = _boundingRadius - result.Field14;
                }
                else if (volume.Type == VolumeType.Cylinder)
                {
                    v18 = _boundingRadius + result.Plane.W - Vector3.Dot(pointTwo, result.Plane.Xyz);
                }
                else
                {
                    v18 = _boundingRadius + result.Plane.W - Vector3.Dot(position, result.Plane.Xyz);
                }
                if (v18 > 0)
                {
                    if (result.Plane.Y < 0.1f && result.Plane.Y > -0.1f)
                    {
                        a7 = true;
                    }
                    else
                    {
                        a6 = true;
                    }
                    Position += result.Plane.Xyz * v18;
                    if (updateSpeed)
                    {
                        float dot = Vector3.Dot(_speed, result.Plane.Xyz);
                        if (dot < 0)
                        {
                            _speed += result.Plane.Xyz * -dot;
                        }
                    }
                }
            }
            return true;
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.EnemyHurt)
            {
                AddVolumeItem(_hurtVolume, Vector3.UnitX);
            }
        }

        public static Vector3 RotateVector(Vector3 vec, Vector3 axis, float angle)
        {
            return vec * Matrix3.CreateFromAxisAngle(axis, MathHelper.DegreesToRadians(angle));
        }
    }

    [Flags]
    public enum EnemyFlags : ushort
    {
        Visible = 1,
        NoHomingNc = 2,
        NoHomingCo = 4,
        Invincible = 8,
        NoBombDamage = 0x10,
        CollidePlayer = 0x20,
        CollideBeam = 0x40,
        NoMaxDistance = 0x80,
        OnRadar = 0x100,
        Static = 0x200
    }

    public enum Effectiveness : byte
    {
        Zero,
        Half,
        Normal,
        Double
    }

    public readonly struct EnemyBehavior<T> where T : EnemyInstanceEntity
    {
        public readonly byte NextState;
        public readonly Func<T, bool> Function;

        public EnemyBehavior(byte nextState, Func<T, bool> function)
        {
            NextState = nextState;
            Function = function;
        }
    }

    public readonly struct EnemySubroutine<T> where T : EnemyInstanceEntity
    {
        public readonly IReadOnlyList<EnemyBehavior<T>> Behaviors { get; }

        public EnemySubroutine(IReadOnlyList<EnemyBehavior<T>> behaviors)
        {
            Behaviors = behaviors;
        }
    }
}
