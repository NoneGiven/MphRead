using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Entities.Enemies
{
    public class Enemy50Entity : EnemyInstanceEntity
    {
        private readonly EnemyInstanceEntity _enemyOwner;

        public Enemy50Entity(EnemyInstanceEntityData data) : base(data)
        {
            var owner = data.Spawner as EnemyInstanceEntity;
            Debug.Assert(owner != null);
            _enemyOwner = owner;
        }

        protected override void EnemyProcess(Scene scene)
        {
            Transform = _enemyOwner.Transform.ClearScale();
            if (_enemyOwner.EnemyType == EnemyType.FireSpawn)
            {
                ContactDamagePlayer(1, knockback: true); // todo: get damage amount from owner values
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source, Scene scene)
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
                _enemyOwner.TakeDamage((uint)(_healthMax - _health), source, scene);
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

        public override void HandleMessage(MessageInfo info, Scene scene)
        {
            if (info.Message == Message.SetActive
                && info.Sender is EnemyInstanceEntity enemy && enemy.EnemyType == EnemyType.FireSpawn)
            {
                if (info.Param1 is int value && value == 1)
                {
                    Flags |= EnemyFlags.CollidePlayer;
                    Flags |= EnemyFlags.CollideBeam;
                    _hitPlayers = 1;
                }
                else
                {
                    Flags &= ~EnemyFlags.CollidePlayer;
                    Flags &= ~EnemyFlags.CollideBeam;
                    _hitPlayers = 0;
                }
            }
        }
    }
}
