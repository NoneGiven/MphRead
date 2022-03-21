using System.Diagnostics;
using MphRead.Formats.Collision;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy40Entity : EnemyInstanceEntity
    {
        public enum SpawnerModelType
        {
            Spawner,
            Nest
        }

        private readonly EnemySpawnEntity _spawner;
        private byte _animTimer = 0;
        private EntityCollision? _parentEntCol = null;
        private Matrix4 _invTransform = Matrix4.Identity;

        public SpawnerModelType ModelType { get; private set; }

        public Enemy40Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        // this happens in the spawner's set_entity_refs in-game
        protected override bool EnemyInitialize()
        {
            Transform = _data.Spawner.Transform;
            if (_data.Spawner is EnemySpawnEntity spawner && spawner.ParentEntCol != null)
            {
                _parentEntCol = spawner.ParentEntCol;
                _invTransform = _transform * spawner.ParentEntCol.Inverse2;
            }
            _boundingRadius = Fixed.ToFloat(3072);
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, _boundingRadius);
            _hurtVolume = CollisionVolume.Transform(_hurtVolumeInit, Transform);
            _healthMax = _health = _spawner.Data.SpawnerHealth;
            Flags |= EnemyFlags.Visible;
            if (_parentEntCol == null)
            {
                Flags |= EnemyFlags.Static;
            }
            string model = "EnemySpawner";
            if (_spawner.Data.EnemyType == EnemyType.WarWasp || _spawner.Data.EnemyType == EnemyType.BarbedWarWasp)
            {
                model = "PlantCarnivarous_Pod";
                ModelType = SpawnerModelType.Nest;
            }
            ModelInstance inst = SetUpModel(model);
            if (_spawner.Data.EnemyType != EnemyType.WarWasp && _spawner.Data.EnemyType != EnemyType.BarbedWarWasp)
            {
                if (_spawner.Flags.TestFlag(SpawnerFlags.Active))
                {
                    inst.SetAnimation(1);
                }
                else
                {
                    inst.SetAnimation(2, AnimFlags.Paused | AnimFlags.Ended);
                }
            }
            else
            {
                inst.SetAnimation(0);
            }
            _spawner.Flags |= SpawnerFlags.HasModel;
            return true;
        }

        protected override void EnemyProcess()
        {
            if (_parentEntCol != null)
            {
                Transform = _invTransform * _parentEntCol.Transform;
            }
            if (_spawner.Flags.TestFlag(SpawnerFlags.Active) && !_spawner.Flags.TestFlag(SpawnerFlags.Suspended))
            {
                Flags &= ~EnemyFlags.Invincible;
            }
            else
            {
                Flags |= EnemyFlags.Invincible;
            }
            if (_spawner.Flags.TestFlag(SpawnerFlags.PlayAnimation))
            {
                _spawner.Flags &= ~SpawnerFlags.PlayAnimation;
                if (_animTimer == 0)
                {
                    // sktodo: play SFX
                }
                _animTimer = 30;
                if (ModelType == SpawnerModelType.Spawner)
                {
                    _models[0].SetAnimation(2, AnimFlags.NoLoop);
                }
            }
            else if (ModelType == SpawnerModelType.Spawner)
            {
                if (Flags.TestFlag(EnemyFlags.Invincible))
                {
                    _models[0].SetAnimation(2, AnimFlags.Ended | AnimFlags.Paused);
                }
                else if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended) && _models[0].AnimInfo.Index[0] != 1)
                {
                    if (_models[0].AnimInfo.Index[0] != 0)
                    {
                        _models[0].SetAnimation(1);
                    }
                    else
                    {
                        // set health to 0 when dying animation finishes
                        _health = 0;
                    }
                }
            }
            if (_animTimer > 0)
            {
                _animTimer--;
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health == 0 && ModelType == SpawnerModelType.Spawner)
            {
                // keep health above 0 to finish playing dying animation
                if (_models[0].AnimInfo.Index[0] != 0)
                {
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                }
                _health = 1;
            }
            return false;
        }
    }
}
