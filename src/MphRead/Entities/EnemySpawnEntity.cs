using System;
using MphRead.Entities.Enemies;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    [Flags]
    public enum SpawnerFlags : byte
    {
        Suspended = 1,
        Active = 2,
        HasModel = 4,
        PlayAnimation = 8,
        CounterBit0 = 0x10, // we don't use these
        CounterBit1 = 0x20,
        CounterBit2 = 0x40,
        CounterBit3 = 0x80
    }

    public class EnemySpawnEntity : EntityBase
    {
        private readonly EnemySpawnEntityData _data;
        private int _activeCount = 0; // todo: names are backwards?
        private int _spawnedCount = 0;
        private int _cooldownTimer = 0;
        private EntityBase? _entity1;
        private EntityBase? _entity2;
        private EntityBase? _entity3;

        public SpawnerFlags Flags { get; set; }
        public EnemySpawnEntityData Data => _data;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        // todo: enemy and item spawners should preload the models and effects that will be used when they spawn their entities
        public EnemySpawnEntity(EnemySpawnEntityData data, Scene scene) : base(EntityType.EnemySpawn, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _cooldownTimer = _data.InitialCooldown * 2; // todo: FPS stuff
            // todo: room state
            if (data.Active != 0 || data.AlwaysActive != 0)
            {
                Flags |= SpawnerFlags.Active;
            }
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
            // todo: start suspended, update based on range/node ref (w/ "camera is player" option)
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_data.EntityId1 != -1)
            {
                _scene.TryGetEntity(_data.EntityId1, out _entity1);
            }
            if (_data.EntityId2 != -1)
            {
                _scene.TryGetEntity(_data.EntityId2, out _entity2);
            }
            if (_data.EntityId3 != -1)
            {
                _scene.TryGetEntity(_data.EntityId3, out _entity3);
            }
            // todo: linked entity
            if (_data.SpawnerHealth > 0)
            {
                EnemyInstanceEntity? enemy = SpawnEnemy(this, EnemyType.Spawner, _scene);
                if (enemy != null)
                {
                    _scene.AddEntity(enemy);
                }
            }
        }

        public override void SetActive(bool active)
        {
            base.SetActive(active);
            Flags |= SpawnerFlags.Active;
        }

        public override bool Process()
        {
            if (!Flags.TestFlag(SpawnerFlags.Active))
            {
                return base.Process();
            }
            // todo: linked ent
            if (_cooldownTimer > 0)
            {
                _cooldownTimer--;
            }
            if (Flags.TestFlag(SpawnerFlags.Suspended) && _cooldownTimer > 0)
            {
                // todo: remove Suspended flag based on node ref
            }
            if (Flags.TestFlag(SpawnerFlags.Suspended))
            {
                return base.Process();
            }
            bool inRange = true; // todo: do range check
            if (!inRange)
            {
                Flags |= SpawnerFlags.Suspended;
            }
            else if (_spawnedCount < _data.SpawnTotal && _cooldownTimer == 0 && _data.SpawnCount > 0
                && (_data.SpawnLimit == 0 || _activeCount < _data.SpawnLimit))
            {
                for (int i = 0; i < _data.SpawnCount; i++)
                {
                    EntityBase? spawned;
                    if (_data.EnemyType == EnemyType.Hunter)
                    {
                        spawned = SpawnHunter();
                    }
                    else
                    {
                        spawned = SpawnEnemy(this, _data.EnemyType, _scene);
                    }
                    if (spawned == null)
                    {
                        break;
                    }
                    _scene.AddEntity(spawned);
                    if (Flags.TestFlag(SpawnerFlags.HasModel))
                    {
                        Flags |= SpawnerFlags.PlayAnimation;
                    }
                    _spawnedCount++;
                    _activeCount++;
                    _cooldownTimer = _data.CooldownTime * 2; // todo: FPS stuff
                    if (_spawnedCount >= _data.SpawnTotal || _data.SpawnLimit > 0 && _activeCount >= _data.SpawnLimit)
                    {
                        break;
                    }
                }
            }
            if (_data.SpawnLimit > 0 && _activeCount >= _data.SpawnLimit && _spawnedCount == 0)
            {
                DeactivateAndSendMessages();
            }
            return base.Process();
        }

        private PlayerEntity? SpawnHunter()
        {
            // todo: spawn enemy hunter
            return null;
        }

        private void DeactivateAndSendMessages()
        {
            Flags &= ~SpawnerFlags.Active;
            // todo: room state/story save
            if (_entity1 != null)
            {
                _scene.SendMessage(_data.Message1, this, _entity1, -1, 0);
            }
            if (_entity2 != null)
            {
                _scene.SendMessage(_data.Message2, this, _entity2, -1, 0);
            }
            if (_entity3 != null)
            {
                _scene.SendMessage(_data.Message3, this, _entity3, -1, 0);
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Destroyed)
            {
                if (info.Param1 is int value && value != 0)
                {
                    // enemy was out of range
                    --_spawnedCount;
                    --_activeCount;
                    _cooldownTimer = 0;
                }
                else
                {
                    if (info.Sender is EnemyInstanceEntity enemy && enemy.EnemyType == EnemyType.Spawner)
                    {
                        Flags &= ~SpawnerFlags.Active;
                        // todo: room state
                    }
                    else
                    {
                        --_spawnedCount;
                        _cooldownTimer = _data.CooldownTime * 2; // todo: FPS stuff
                    }
                }
                if (!Flags.TestFlag(SpawnerFlags.Active) && _spawnedCount == 0)
                {
                    DeactivateAndSendMessages();
                }
            }
            else if (info.Message == Message.Activate)
            {
                Flags |= SpawnerFlags.Active;
                // todo: room state
            }
            else if (info.Message == Message.SetActive)
            {
                if (info.Param1 is int value && value != 1)
                {
                    Flags |= SpawnerFlags.Active;
                    // todo: room state
                }
                else
                {
                    Flags &= ~SpawnerFlags.Active;
                    // todo: room state
                }
            }
            else if (info.Message == Message.Unknown36)
            {
                // todo: pass to Gorea2
            }
        }

        // todo: entity node ref
        public static EnemyInstanceEntity? SpawnEnemy(EntityBase spawner, EnemyType type, Scene scene)
        {
            if (type == EnemyType.WarWasp)
            {
                return new Enemy00Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Zoomer)
            {
                return new Enemy01Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Temroid)
            {
                return new Enemy02Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Petrasyl1)
            {
                return new Enemy03Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Petrasyl2)
            {
                return new Enemy04Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Petrasyl3)
            {
                return new Enemy05Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Petrasyl4)
            {
                return new Enemy06Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.BarbedWarWasp)
            {
                return new Enemy10Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Shriekbat)
            {
                return new Enemy11Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Geemer)
            {
                return new Enemy12Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Blastcap)
            {
                return new Enemy16Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.AlimbicTurret)
            {
                return new Enemy18Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.PsychoBit1)
            {
                return new Enemy23Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.FireSpawn)
            {
                return new Enemy39Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.Spawner)
            {
                return new Enemy40Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.ForceFieldLock)
            {
                return new Enemy49Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.HitZone)
            {
                return new Enemy50Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            if (type == EnemyType.CarnivorousPlant)
            {
                return new Enemy51Entity(new EnemyInstanceEntityData(type, spawner), scene);
            }
            //throw new ProgramException("Invalid enemy type."); // also make non-nullable
            return null;
        }
    }

    public class FhEnemySpawnEntity : EntityBase
    {
        private readonly FhEnemySpawnEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        // todo: FH enemy spawning
        public FhEnemySpawnEntity(FhEnemySpawnEntityData data, Scene scene) : base(EntityType.EnemySpawn, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
        }
    }
}
