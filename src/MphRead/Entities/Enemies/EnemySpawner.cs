using System.Diagnostics;

namespace MphRead.Entities.Enemies
{
    public class Enemy40Entity : EnemyInstanceEntity
    {
        public Enemy40Entity(EnemyInstanceEntityData data) : base(data)
        {
            var spawner = _owner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            Transform = data.Spawner.Transform;
            string model = "EnemySpawner";
            if (spawner.Data.EnemyType == EnemyType.WarWasp || spawner.Data.EnemyType == EnemyType.BarbedWarWasp)
            {
                model = "PlantCarnivarous_Pod";
            }
            ModelInstance inst = SetUpModel(model);
            if (spawner.Data.EnemyType != EnemyType.WarWasp && spawner.Data.EnemyType != EnemyType.BarbedWarWasp)
            {
                if ((spawner.Flags & 2) != 0)
                {
                    inst.SetAnimation(1);
                }
                else
                {
                    inst.SetAnimation(2, AnimFlags.Paused | AnimFlags.Ended);
                }
            }
        }
    }
}
