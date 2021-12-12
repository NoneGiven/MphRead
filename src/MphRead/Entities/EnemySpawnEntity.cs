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
        public EnemySpawnEntity(EnemySpawnEntityData data) : base(EntityType.EnemySpawn)
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

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.EntityId1 != -1)
            {
                scene.TryGetEntity(_data.EntityId1, out _entity1);
            }
            if (_data.EntityId2 != -1)
            {
                scene.TryGetEntity(_data.EntityId2, out _entity2);
            }
            if (_data.EntityId3 != -1)
            {
                scene.TryGetEntity(_data.EntityId3, out _entity3);
            }
            // todo: linked entity
            if (_data.SpawnerHealth > 0)
            {
                EnemyInstanceEntity? enemy = SpawnEnemy(this, EnemyType.Spawner);
                if (enemy != null)
                {
                    scene.AddEntity(enemy);
                }
            }
        }

        public override bool Process(Scene scene)
        {
            if (!Flags.TestFlag(SpawnerFlags.Active))
            {
                return base.Process(scene);
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
                return base.Process(scene);
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
                        spawned = SpawnEnemy(this, _data.EnemyType);
                    }
                    if (spawned == null)
                    {
                        break;
                    }
                    scene.AddEntity(spawned);
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
                DeactivateAndSendMessages(scene);
            }
            return base.Process(scene);
        }

        private PlayerEntity? SpawnHunter()
        {
            // todo: spawn enemy hunter
            return null;
        }

        private void DeactivateAndSendMessages(Scene scene)
        {
            Flags &= ~SpawnerFlags.Active;
            // todo: room state/story save
            if (_entity1 != null)
            {
                scene.SendMessage(_data.Message1, this, _entity1, -1, 0);
            }
            if (_entity2 != null)
            {
                scene.SendMessage(_data.Message2, this, _entity2, -1, 0);
            }
            if (_entity3 != null)
            {
                scene.SendMessage(_data.Message3, this, _entity3, -1, 0);
            }
        }

        public override void HandleMessage(MessageInfo info, Scene scene)
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
                    DeactivateAndSendMessages(scene);
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
        public static EnemyInstanceEntity? SpawnEnemy(EntityBase spawner, EnemyType type)
        {
            if (type == EnemyType.Spawner)
            {
                return new Enemy40Entity(new EnemyInstanceEntityData(type, spawner));
            }
            if (type == EnemyType.ForceFieldLock)
            {
                return new Enemy49Entity(new EnemyInstanceEntityData(type, spawner));
            }
            if (type == EnemyType.HitZone)
            {
                return new Enemy50Entity(new EnemyInstanceEntityData(type, spawner));
            }
            if (type == EnemyType.CarnivorousPlant)
            {
                return new Enemy51Entity(new EnemyInstanceEntityData(type, spawner));
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
        public FhEnemySpawnEntity(FhEnemySpawnEntityData data) : base(EntityType.EnemySpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
        }
    }
}
