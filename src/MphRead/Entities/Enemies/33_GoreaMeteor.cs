using MphRead.Formats.Culling;

namespace MphRead.Entities.Enemies
{
    public class Enemy33Entity : GoreaEnemyEntityBase
    {
        private Enemy31Entity _gorea2 = null!;

        public Enemy33Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy31Entity owner)
            {
                _gorea2 = owner;
            }
        }

        private bool Behavior00()
        {
            return true; // skhere
        }

        private bool Behavior01()
        {
            return true; // skhere
        }

        private bool Behavior02()
        {
            return true; // skhere
        }

        private bool Behavior03()
        {
            return true; // skhere
        }

        #region Boilerplate

        public static bool Behavior00(Enemy33Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy33Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy33Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy33Entity enemy)
        {
            return enemy.Behavior03();
        }

        #endregion
    }
}
