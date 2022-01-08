using System;
using System.Diagnostics;

namespace MphRead.Entities.Enemies
{
    public class Enemy05Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;

        public Enemy05Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[3]
            {
                State0, State1, State2
            };
        }

        private void State0()
        {
        }

        private void State1()
        {
        }

        private void State2()
        {
        }

        private bool Behavior00()
        {
            return true;
        }

        private bool Behavior01()
        {
            return true;
        }

        private bool Behavior02()
        {
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy05Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy05Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy05Entity enemy)
        {
            return enemy.Behavior02();
        }

        #endregion
    }
}
