using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy46Entity : EnemyInstanceEntity
    {
        protected readonly EnemySpawnEntity _spawner;
        private CollisionVolume _volume1;
        private CollisionVolume _volume2;

        public Enemy46Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[20]
            {
                State00, State01, State02, State03, State04, State05, State06, State07, State08, State09,
                State10, State11, State12, State13, State14, State15, State16, State17, State18, State19
            };
        }

        protected override bool EnemyInitialize()
        {
            EnemySpawnEntityData data = _spawner.Data;
            Setup(data.Header.FacingVector, data.Header.FacingVector, effectiveness: 0x5555, data.Fields.S00.Volume0,
                data.Fields.S00.Volume2, data.Fields.S00.Volume1, data.Fields.S00.Volume3);
            return true;
        }

        protected void Setup(Vector3Fx pos, Vector3Fx facing, int effectiveness, RawCollisionVolume hurtVolume,
            RawCollisionVolume volume1, RawCollisionVolume volume2, RawCollisionVolume volume3)
        {
        }

        protected virtual void CallSubroutine()
        {
            CallSubroutine(Metadata.Enemy46Subroutines, this);
        }

        private void State00()
        {
        }

        private void State01()
        {
        }

        private void State02()
        {
        }

        private void State03()
        {
        }

        private void State04()
        {
        }

        private void State05()
        {
        }

        private void State06()
        {
        }

        private void State07()
        {
        }

        private void State08()
        {
        }

        private void State09()
        {
        }

        private void State10()
        {
        }

        private void State11()
        {
        }

        private void State12()
        {
        }

        private void State13()
        {
        }

        private void State14()
        {
        }

        private void State15()
        {
        }

        private void State16()
        {
        }

        private void State17()
        {
        }

        private void State18()
        {
        }

        private void State19()
        {
        }

        protected bool Behavior00()
        {
            return true;
        }

        protected bool Behavior01()
        {
            return true;
        }

        protected bool Behavior02()
        {
            return true;
        }

        protected bool Behavior03()
        {
            return true;
        }

        protected bool Behavior04()
        {
            return true;
        }

        protected bool Behavior05()
        {
            return true;
        }

        protected bool Behavior06()
        {
            return true;
        }

        protected bool Behavior07()
        {
            return true;
        }

        protected bool Behavior08()
        {
            return true;
        }

        protected bool Behavior09()
        {
            return true;
        }

        protected bool Behavior10()
        {
            return true;
        }

        protected bool Behavior11()
        {
            return true;
        }

        protected bool Behavior12()
        {
            return true;
        }

        protected bool Behavior13()
        {
            return true;
        }

        protected bool Behavior14()
        {
            return true;
        }

        protected bool Behavior15()
        {
            return true;
        }

        protected bool Behavior16()
        {
            return true;
        }

        protected bool Behavior17()
        {
            return true;
        }

        protected bool Behavior18()
        {
            return true;
        }

        protected bool Behavior19()
        {
            return true;
        }

        protected bool Behavior20()
        {
            return true;
        }

        protected bool Behavior21()
        {
            return true;
        }

        protected bool Behavior22()
        {
            return true;
        }

        protected bool Behavior23()
        {
            return true;
        }

        protected bool Behavior24()
        {
            return true;
        }

        protected bool Behavior25()
        {
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy46Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy46Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy46Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy46Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy46Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy46Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy46Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy46Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy46Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy46Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy46Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy46Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy46Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy46Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy46Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy46Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy46Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy46Entity enemy)
        {
            return enemy.Behavior17();
        }

        public static bool Behavior18(Enemy46Entity enemy)
        {
            return enemy.Behavior18();
        }

        public static bool Behavior19(Enemy46Entity enemy)
        {
            return enemy.Behavior18();
        }

        public static bool Behavior20(Enemy46Entity enemy)
        {
            return enemy.Behavior20();
        }

        public static bool Behavior21(Enemy46Entity enemy)
        {
            return enemy.Behavior21();
        }

        public static bool Behavior22(Enemy46Entity enemy)
        {
            return enemy.Behavior22();
        }

        public static bool Behavior23(Enemy46Entity enemy)
        {
            return enemy.Behavior23();
        }

        public static bool Behavior24(Enemy46Entity enemy)
        {
            return enemy.Behavior24();
        }

        public static bool Behavior25(Enemy46Entity enemy)
        {
            return enemy.Behavior25();
        }

        #endregion
    }
}
