namespace MphRead.Entities.Enemies
{
    public class Enemy47Entity : Enemy46Entity
    {
        public Enemy47Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
        }

        protected override bool EnemyInitialize()
        {
            EnemySpawnEntityData data = _spawner.Data;
            Setup(data.Header.FacingVector, data.Header.FacingVector, effectiveness: 0, data.Fields.S05.Volume0,
                data.Fields.S05.Volume1, data.Fields.S05.Volume2, data.Fields.S05.Volume3);
            return true;
        }

        protected override void CallSubroutine()
        {
            CallSubroutine(Metadata.Enemy47Subroutines, this);
        }

        #region Boilerplate

        public static bool Behavior00(Enemy47Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy47Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy47Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy47Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy47Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy47Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy47Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy47Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy47Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy47Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy47Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy47Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy47Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy47Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy47Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy47Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy47Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy47Entity enemy)
        {
            return enemy.Behavior17();
        }

        public static bool Behavior18(Enemy47Entity enemy)
        {
            return enemy.Behavior18();
        }

        public static bool Behavior19(Enemy47Entity enemy)
        {
            return enemy.Behavior18();
        }

        public static bool Behavior20(Enemy47Entity enemy)
        {
            return enemy.Behavior20();
        }

        public static bool Behavior21(Enemy47Entity enemy)
        {
            return enemy.Behavior21();
        }

        public static bool Behavior22(Enemy47Entity enemy)
        {
            return enemy.Behavior22();
        }

        public static bool Behavior23(Enemy47Entity enemy)
        {
            return enemy.Behavior23();
        }

        public static bool Behavior24(Enemy47Entity enemy)
        {
            return enemy.Behavior24();
        }

        public static bool Behavior25(Enemy47Entity enemy)
        {
            return enemy.Behavior25();
        }

        #endregion
    }
}
