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
        NoVolumeUpdate = 0x200
    }

    public class EnemyInstanceEntity : EntityBase
    {
        protected readonly EnemyInstanceEntityData _data;
        protected Vector3 _initialPosition; // todo: use init
        private ushort _framesSinceDamage = 510;
        private ushort _health = 20;
        private ushort _healthMax = 20;
        private EntityBase? _owner = null;
        private CollisionVolume _hurtVolume = default;
        private CollisionVolume _hurtVolumeInit = default;
        private byte _state1 = 0; // todo: names ("next?")
        private byte _state2 = 0;
        private byte _hitPlayers = 0;

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
            Flags = EnemyFlags.CollidePlayer | EnemyFlags.CollideBeam;
            var spawner = _data.Spawner as EnemySpawnEntity;
            if (EnemyInitialize(spawner) && spawner != null)
            {
                // todo: linked entity collision transform -- although I don't think this is ever used for enemies/spawners
            }
            if (_data.Type == EnemyType.Gorea1A || _data.Type == EnemyType.GoreaHead || _data.Type == EnemyType.GoreaArm
                || _data.Type == EnemyType.GoreaLeg || _data.Type == EnemyType.Gorea1B || _data.Type == EnemyType.GoreaSealSphere1
                || _data.Type == EnemyType.Trocra || _data.Type == EnemyType.Gorea2 || _data.Type == EnemyType.GoreaSealSphere2)
            {
                _onlyMoveHurtVolume = true;
            }
        }

        public override bool Process(Scene scene)
        {
            bool inRange = false;
            if (_data.Type == EnemyType.Spawner || Flags.TestFlag(EnemyFlags.NoMaxDistance))
            {
                inRange = true;
            }
            else
            {
                // sktodo
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
                    UpdateHurtVolume();
                    // todo: positional audio, node ref
                    _hitPlayers = 0;
                    // todo: player collision
                    EnemyProcess(scene);
                    UpdateHurtVolume();
                    // node ref
                    return base.Process(scene);
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

        private void UpdateHurtVolume()
        {
            if (_onlyMoveHurtVolume)
            {
                _hurtVolume = CollisionVolume.Move(_hurtVolumeInit, Position);
            }
            else
            {
                _hurtVolume = CollisionVolume.Transform(_hurtVolume, Transform);
            }
        }

        public virtual bool EnemyInitialize(EnemySpawnEntity? spawner)
        {
            // must return true if overriden
            return false;
        }

        public virtual void EnemyProcess(Scene scene)
        {
        }
    }
}
