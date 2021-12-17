using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
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
        public byte HitPlayers { get; set; }
        protected Vector3 _prevPos = Vector3.Zero;
        protected Vector3 _speed = Vector3.Zero;
        protected float _boundingRadius = 0;
        protected Action<Scene>[]? _stateProcesses;

        public readonly Effectiveness[] BeamEffectiveness = new Effectiveness[9];
        private bool _onlyMoveHurtVolume = false;
        private bool _noIneffectiveEffect = false;
        public EnemyFlags Flags { get; set; }
        public CollisionVolume HurtVolume => _hurtVolume;
        public EnemyType EnemyType => _data.Type;
        public EntityBase? Owner => _owner;

        protected static readonly BeamProjectileEntity[] _beams = SceneSetup.CreateBeamList(64); // in-game: 64

        public EnemyInstanceEntity(EnemyInstanceEntityData data) : base(EntityType.EnemyInstance)
        {
            _data = data;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            // todo: set other properties, etc.
            _owner = _data.Spawner;
            Metadata.LoadEffectiveness(_data.Type, BeamEffectiveness);
            Flags = EnemyFlags.CollidePlayer | EnemyFlags.CollideBeam;
            if (EnemyInitialize(scene) && _data.Spawner is EnemySpawnEntity spawner)
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

        public override bool Process(Scene scene)
        {
            bool inRange = true; // todo: should default to false, but with logic for view mode/"camera is player"
            if (_data.Type == EnemyType.Spawner || Flags.TestFlag(EnemyFlags.NoMaxDistance))
            {
                inRange = true;
            }
            else
            {
                // todo: check range
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
                    HitPlayers = 0;
                    // todo: player collision
                    EnemyProcess(scene);
                    if (!Flags.TestFlag(EnemyFlags.Static))
                    {
                        UpdateHurtVolume();
                    }
                    // todo: node ref
                    base.Process(scene);
                    return true;
                }
                scene.SendMessage(Message.Destroyed, this, _owner, 0, 0);
                if (_owner is EnemySpawnEntity spawner)
                {
                    Vector3 pos = _hurtVolume.GetCenter().AddY(0.5f);
                    ItemSpawnEntity.SpawnItemDrop(spawner.Data.ItemType, pos, spawner.Data.ItemChance, scene);
                }
                return false;
            }
            scene.SendMessage(Message.Destroyed, this, _owner, 1, 0);
            return false;
        }

        protected void ContactDamagePlayer(uint damage, bool knockback)
        {
            // todo: test hit bits against main player, do damage + knockback
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

        public override void GetDrawInfo(Scene scene)
        {
            if (_health > 0 && Flags.TestFlag(EnemyFlags.Visible))
            {
                if (!EnemyGetDrawInfo(scene))
                {
                    // todo: is_visible
                    if (_framesSinceDamage < 10)
                    {
                        PaletteOverride = Metadata.RedPalette;
                    }
                    base.GetDrawInfo(scene);
                    PaletteOverride = null;
                }
            }
        }

        /// <summary>
        /// Must return true if overriden.
        /// </summary>
        protected virtual bool EnemyInitialize(Scene scene)
        {
            return false;
        }

        protected virtual void EnemyProcess(Scene scene)
        {
        }

        /// <summary>
        /// Must return true if overriden.
        /// </summary>
        protected virtual bool EnemyGetDrawInfo(Scene scene)
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

        public void TakeDamage(uint damage, EntityBase? source, Scene scene)
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
                effectiveness = GetEffectiveness(beamSource.Weapon);
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
            if (EnemyTakeDamage(source, scene))
            {
                _health = prevHealth;
                unaffected = true;
                dead = false;
            }
            if (unaffected)
            {
                if (effectiveness == Effectiveness.Zero && !_noIneffectiveEffect)
                {
                    // 115 - ineffectivePsycho
                    Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
                    transform.Row3.Xyz = _hurtVolume.GetCenter();
                    EffectEntry effect = scene.SpawnEffectGetEntry(115, transform);
                    effect.SetReadOnlyField(0, _boundingRadius);
                    scene.DetachEffectEntry(effect, setExpired: false);
                }
            }
            else
            {
                if (beamSource != null)
                {
                    beamSource.SpawnDamageEffect(effectiveness, scene);
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
                    scene.SpawnEffect(effectId, transform);
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
                        scene.SpawnEffect(3, transform);
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
        protected virtual bool EnemyTakeDamage(EntityBase? source, Scene scene)
        {
            return false;
        }

        protected void CallStateProcess(Scene scene)
        {
            Debug.Assert(_stateProcesses != null && _state1 >= 0 && _state1 < _stateProcesses.Length);
            _stateProcesses[_state1].Invoke(scene);
        }

        protected bool CallSubroutine<T>(IReadOnlyList<EnemySubroutine<T>> subroutines, T enemy, Scene scene) where T : EnemyInstanceEntity
        {
            Debug.Assert(enemy == this);
            EnemySubroutine<T> subroutine = subroutines[_state1];
            if (subroutine.Behaviors.Count == 0)
            {
                return false;
            }
            int index = 0;
            while (!subroutine.Behaviors[index].Function.Invoke(enemy, scene))
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

        protected bool HandleBlockingCollision(Vector3 position, CollisionVolume volume, bool updateSpeed,
            Action<bool>? a6 = null, Action<bool>? a7 = null)
        {
            // sktodo: this
            return false;
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.EnemyHurt)
            {
                AddVolumeItem(_hurtVolume, Vector3.UnitX, scene);
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
        public readonly Func<T, Scene, bool> Function;

        public EnemyBehavior(byte nextState, Func<T, Scene, bool> function)
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
