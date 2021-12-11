using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy51Entity : EnemyInstanceEntity
    {
        public Enemy51Entity(EnemyInstanceEntityData data) : base(data)
        {
        }

        public override bool EnemyInitialize(EnemySpawnEntity? spawner)
        {
            Debug.Assert(spawner != null);
            Transform = _data.Spawner.Transform;
            _prevPos = Position;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Static;
            _healthMax = _health = spawner.Data.Fields.S07.EnemyHealth;
            _boundingRadius = Fixed.ToFloat(1843);
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, Fixed.ToFloat(409), 0), _boundingRadius);
            _hurtVolume = CollisionVolume.Transform(_hurtVolumeInit, Transform);
            ObjectMetadata meta = Metadata.GetObjectById(spawner.Data.Fields.S07.EnemySubtype);
            // todo: enemy spawners need to load these initially
            SetUpModel(meta.Name);
            return true;
        }
    }
}
