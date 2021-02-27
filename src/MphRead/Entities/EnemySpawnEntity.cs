using System;
using System.Diagnostics;
using MphRead.Entities.Enemies;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class EnemySpawnEntity : EntityBase
    {
        private readonly EnemyEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();
        private bool _spawn = true;

        public EnemyEntityData Data => _data;

        // todo: enemy and item spawners should preload the models and effects that will be used when they spawn their entities
        public EnemySpawnEntity(EnemyEntityData data) : base(EntityType.EnemySpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            if (data.SpawnerModel != 0)
            {
                string spawner = "EnemySpawner";
                if (data.Type == EnemyType.WarWasp || data.Type == EnemyType.BarbedWarWasp)
                {
                    spawner = "PlantCarnivarous_Pod";
                }
                ModelInstance inst = Read.GetModelInstance(spawner);
                _models.Add(inst);
                // temporary
                if (spawner == "EnemySpawner")
                {
                    inst.SetNodeAnim(-1);
                    inst.SetMaterialAnim(-1);
                    inst.SetTexcoordAnim(-1);
                }
            }
            else
            {
                AddPlaceholderModel();
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_data.Type == EnemyType.Hunter)
            {
                var hunter = (Hunter)_data.Subtype;
                Debug.Assert(Enum.IsDefined(typeof(Hunter), hunter));
                // todo: random encounter setup
                if (hunter != Hunter.Random)
                {
                    SceneSetup.LoadHunterResources(hunter, scene);
                    var player = PlayerEntity.Spawn(hunter, position: Position, facing: _data.Header.FacingVector.ToFloatVector());
                    if (player != null)
                    {
                        scene.AddEntity(player);
                    }
                }
            }
        }

        public override bool Process(Scene scene)
        {
            // todo: enemy spawning logic
            if (_spawn)
            {
                _spawn = false;
                EnemyInstanceEntity? enemy = SpawnEnemy(this, _data.Type);
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
            else if (type == EnemyType.CarnivorousPlant)
            {
                return new Enemy51Entity(new EnemyInstanceEntityData(type, spawner));
            }
            //throw new ProgramException("Invalid enemy type."); // also make non-nullable
            return null;
        }
    }

    public class FhEnemySpawnEntity : EntityBase
    {
        private readonly FhEnemyEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        // todo: FH enemy spawning
        public FhEnemySpawnEntity(FhEnemyEntityData data) : base(EntityType.EnemySpawn)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
        }
    }
}
