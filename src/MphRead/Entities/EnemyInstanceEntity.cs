using System;
using System.Diagnostics;
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

    public class EnemyInstanceEntity : EntityBase
    {
        protected readonly EnemyInstanceEntityData _data;
        private ushort _framesSinceDamage = 510;
        protected ushort _health = 20;
        protected ushort _healthMax = 20;
        protected EntityBase? _owner = null;
        protected CollisionVolume _hurtVolume = default;
        protected CollisionVolume _hurtVolumeInit = default;
        private byte _state1 = 0; // todo: names ("next?")
        private byte _state2 = 0;
        private byte _hitPlayers = 0;
        protected Vector3 _prevPos = Vector3.Zero;
        protected Vector3 _speed = Vector3.Zero;
        protected float _boundingRadius = 0;

        public Effectiveness[] BeamEffectiveness = new Effectiveness[9];
        private bool _onlyMoveHurtVolume = false;
        public EnemyFlags Flags { get; set; }

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
                    _hitPlayers = 0;
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

        protected virtual bool EnemyInitialize()
        {
            // must return true if overriden
            return false;
        }

        protected virtual void EnemyProcess(Scene scene)
        {
        }

        protected virtual bool EnemyGetDrawInfo(Scene scene)
        {
            // must return true if overriden
            return false;
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
                int index = (int)beamSource.Weapon;
                Debug.Assert(index < BeamEffectiveness.Length);
                effectiveness = BeamEffectiveness[index];
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
            if (EnemyTakeDamage(effectiveness, source))
            {
                _health = prevHealth;
                unaffected = true;
                dead = false;
            }
            if (unaffected)
            {
                if (effectiveness == Effectiveness.Zero)
                {
                    // sktodo: create ineffective effect
                }
            }
            else
            {
                // sktodo: create damage effect
                if (dead)
                {
                    // todo: update records
                    if (_data.Type == EnemyType.Temroid)
                    {
                        // todo: detach
                    }
                    // todo: play SFX
                    // sktodo: create death effect
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
                        // sktodo: spawn effect
                        break;
                    }
                }
            }
        }

        protected virtual bool EnemyTakeDamage(Effectiveness effectiveness, EntityBase? source)
        {
            // when overridden, must return true when unaffected by damage and false otherwise
            return false;
        }

        public override void GetDisplayVolumes(Scene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.EnemyHurt)
            {
                AddVolumeItem(_hurtVolume, Vector3.UnitX, scene);
            }
        }
    }
}
