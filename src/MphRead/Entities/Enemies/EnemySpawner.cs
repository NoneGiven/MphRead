namespace MphRead.Entities.Enemies
{
    public class Enemy40Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _owner;

        public Enemy40Entity(EnemyInstanceEntityData data) : base(data)
        {
            Transform = data.Spawner.Transform;
            _initialPosition = Position;
            _owner = (EnemySpawnEntity)data.Spawner;
            string spawner = "EnemySpawner";
            if (_owner.Data.EnemyType == EnemyType.WarWasp || _owner.Data.EnemyType == EnemyType.BarbedWarWasp)
            {
                spawner = "PlantCarnivarous_Pod";
            }
            ModelInstance inst = SetUpModel(spawner);
            if (_owner.Data.EnemyType != EnemyType.WarWasp && _owner.Data.EnemyType != EnemyType.BarbedWarWasp)
            {
                if ((_owner.Flags & 2) != 0)
                {
                    inst.SetAnimation(1);
                }
                else
                {
                    inst.SetAnimation(2, AnimFlags.Paused | AnimFlags.Ended);
                } 
            }
            // temporary
            if (spawner == "EnemySpawner")
            {
                //inst.SetAnimation(-1);
            }
        }
    }
}
