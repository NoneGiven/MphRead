using System;
using System.Diagnostics;
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
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();
        private bool _spawn = false;

        public SpawnerFlags Flags { get; set; }
        public EnemySpawnEntityData Data => _data;

        // todo: enemy and item spawners should preload the models and effects that will be used when they spawn their entities
        public EnemySpawnEntity(EnemySpawnEntityData data) : base(EntityType.EnemySpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
            if (data.EnemyType == EnemyType.ForceFieldLock || data.EnemyType == EnemyType.CarnivorousPlant)
            {
                _spawn = true;
            }
            // todo: start suspended, update based on range/node ref (w/ "camera is player" option)
            // todo: room state
            if (data.Active != 0 || data.AlwaysActive != 0)
            {
                Flags |= SpawnerFlags.Active;
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.SpawnerHealth != 0)
            {
                // todo: health and other stuff
                EnemyInstanceEntity? enemy = SpawnEnemy(this, EnemyType.Spawner);
                if (enemy != null)
                {
                    scene.AddEntity(enemy);
                }
            }
            if (_data.EnemyType == EnemyType.Hunter)
            {
                var hunter = (Hunter)_data.Fields.S09.HunterId;
                Debug.Assert(Enum.IsDefined(typeof(Hunter), hunter));
                // todo: random encounter setup
                if (hunter != Hunter.Random)
                {
                    //SceneSetup.LoadHunterResources(hunter, scene);
                    //var player = PlayerEntity.Spawn(hunter, position: Position, facing: _data.Header.FacingVector.ToFloatVector());
                    //if (player != null)
                    //{
                    //    scene.AddEntity(player);
                    //}
                }
            }
        }

        public override bool Process(Scene scene)
        {
            // todo: enemy spawning logic
            if (_spawn)
            {
                _spawn = false;
                EnemyInstanceEntity? enemy = SpawnEnemy(this, _data.EnemyType);
                if (enemy != null)
                {
                    scene.AddEntity(enemy);
                }
            }
            return base.Process(scene);
        }

        // todo: entity node ref
        public static EnemyInstanceEntity? SpawnEnemy(EntityBase spawner, EnemyType type)
        {
            if (type == EnemyType.ForceFieldLock)
            {
                return new Enemy49Entity(new EnemyInstanceEntityData(type, spawner));
            }
            if (type == EnemyType.CarnivorousPlant)
            {
                return new Enemy51Entity(new EnemyInstanceEntityData(type, spawner));
            }
            if (type == EnemyType.Spawner)
            {
                return new Enemy40Entity(new EnemyInstanceEntityData(type, spawner));
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
