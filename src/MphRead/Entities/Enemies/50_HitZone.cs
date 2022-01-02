using System.Diagnostics;

namespace MphRead.Entities.Enemies
{
    public class Enemy50Entity : EnemyInstanceEntity
    {
        private readonly EnemyInstanceEntity _enemyOwner;

        public Enemy50Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var owner = data.Spawner as EnemyInstanceEntity;
            Debug.Assert(owner != null);
            _enemyOwner = owner;
        }

        protected override void EnemyProcess()
        {
            Transform = _enemyOwner.Transform.ClearScale();
            if (_enemyOwner.EnemyType == EnemyType.FireSpawn)
            {
                var fireSpawn = (Enemy39Entity)_enemyOwner;
                ContactDamagePlayer(fireSpawn.Values.ContactDamage, knockback: true);
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_enemyOwner.EnemyType != EnemyType.FireSpawn)
            {
                Effectiveness eff0 = _enemyOwner.BeamEffectiveness[0];
                Effectiveness eff1 = _enemyOwner.BeamEffectiveness[1];
                Effectiveness eff2 = _enemyOwner.BeamEffectiveness[2];
                Effectiveness eff3 = _enemyOwner.BeamEffectiveness[3];
                Effectiveness eff4 = _enemyOwner.BeamEffectiveness[4];
                Effectiveness eff5 = _enemyOwner.BeamEffectiveness[5];
                Effectiveness eff6 = _enemyOwner.BeamEffectiveness[6];
                Effectiveness eff7 = _enemyOwner.BeamEffectiveness[7];
                Effectiveness eff8 = _enemyOwner.BeamEffectiveness[8];
                _enemyOwner.BeamEffectiveness[0] = BeamEffectiveness[0];
                _enemyOwner.BeamEffectiveness[1] = BeamEffectiveness[1];
                _enemyOwner.BeamEffectiveness[2] = BeamEffectiveness[2];
                _enemyOwner.BeamEffectiveness[3] = BeamEffectiveness[3];
                _enemyOwner.BeamEffectiveness[4] = BeamEffectiveness[4];
                _enemyOwner.BeamEffectiveness[5] = BeamEffectiveness[5];
                _enemyOwner.BeamEffectiveness[6] = BeamEffectiveness[6];
                _enemyOwner.BeamEffectiveness[7] = BeamEffectiveness[7];
                _enemyOwner.BeamEffectiveness[8] = BeamEffectiveness[8];
                _enemyOwner.TakeDamage((uint)(_healthMax - _health), source);
                _enemyOwner.BeamEffectiveness[0] = eff0;
                _enemyOwner.BeamEffectiveness[1] = eff1;
                _enemyOwner.BeamEffectiveness[2] = eff2;
                _enemyOwner.BeamEffectiveness[3] = eff3;
                _enemyOwner.BeamEffectiveness[4] = eff4;
                _enemyOwner.BeamEffectiveness[5] = eff5;
                _enemyOwner.BeamEffectiveness[6] = eff6;
                _enemyOwner.BeamEffectiveness[7] = eff7;
                _enemyOwner.BeamEffectiveness[8] = eff8;
            }
            return _health > 0;
        }

        public void SetUp(ushort health, CollisionVolume hurtVolume, float boundingRadius)
        {
            _health = _healthMax = health;
            _hurtVolumeInit = hurtVolume;
            _boundingRadius = boundingRadius;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.SetActive
                && info.Sender is EnemyInstanceEntity enemy && enemy.EnemyType == EnemyType.FireSpawn)
            {
                if (info.Param1 is int value && value == 1)
                {
                    Flags |= EnemyFlags.CollidePlayer;
                    Flags |= EnemyFlags.CollideBeam;
                    HitPlayers = 1;
                }
                else
                {
                    Flags &= ~EnemyFlags.CollidePlayer;
                    Flags &= ~EnemyFlags.CollideBeam;
                    HitPlayers = 0;
                }
            }
        }
    }
}
