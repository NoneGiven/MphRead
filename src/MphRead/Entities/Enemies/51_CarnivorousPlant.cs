using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy51Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;

        public Enemy51Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected override bool EnemyInitialize()
        {
            Transform = _data.Spawner.Transform;
            _prevPos = Position;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Static;
            _health = _healthMax = _spawner.Data.Fields.S07.EnemyHealth;
            _boundingRadius = Fixed.ToFloat(1843);
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, Fixed.ToFloat(409), 0), _boundingRadius);
            _hurtVolume = CollisionVolume.Transform(_hurtVolumeInit, Transform);
            ObjectMetadata meta = Metadata.GetObjectById(_spawner.Data.Fields.S07.EnemySubtype);
            SetUpModel(meta.Name);
            return true;
        }

        protected override void EnemyProcess()
        {
            ContactDamagePlayer(_spawner.Data.Fields.S07.EnemyDamage, knockback: false);
        }
    }
}
