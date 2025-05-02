using System;
using MphRead.Formats.Culling;

namespace MphRead.Entities.Enemies
{
    public class Enemy31Entity : GoreaEnemyEntityBase
    {
        public Gorea2Flags GoreaFlags { get; set; }
        private Enemy32Entity _sealSphere = null!;

        private byte _field244;
        public byte Field244 => _field244;

        public Enemy31Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
        }

        // skhere: member name, variable name
        public bool Func214080C()
        {
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
            return value == 1 || value == 2;
        }

        public void UpdatePhase()
        {
            if (_sealSphere.Damage <= 210)
            {
                GoreaFlags &= ~(Gorea2Flags.Bit14 | Gorea2Flags.Bit15);
            }
            else if (_sealSphere.Damage <= 503)
            {
                GoreaFlags &= ~(Gorea2Flags.Bit14 | Gorea2Flags.Bit15) | Gorea2Flags.Bit14;
            }
            else
            {
                GoreaFlags &= ~(Gorea2Flags.Bit14 | Gorea2Flags.Bit15) | Gorea2Flags.Bit15;
            }
        }

        private bool BehaviorXX()
        {
            return AnimationEnded();
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

        private bool Behavior04()
        {
            return true; // skhere
        }

        private bool Behavior05()
        {
            return true; // skhere
        }

        private bool Behavior06()
        {
            return true; // skhere
        }

        private bool Behavior07()
        {
            return true; // skhere
        }

        private bool Behavior08()
        {
            return true; // skhere
        }

        private bool Behavior09()
        {
            return true; // skhere
        }

        private bool Behavior10()
        {
            return true; // skhere
        }

        private bool Behavior11()
        {
            return true; // skhere
        }

        private bool Behavior12()
        {
            return true; // skhere
        }

        private bool Behavior13()
        {
            return true; // skhere
        }

        private bool Behavior14()
        {
            return true; // skhere
        }

        private bool Behavior15()
        {
            return true; // skhere
        }

        private bool Behavior16()
        {
            return true; // skhere
        }

        private bool Behavior17()
        {
            return true; // skhere
        }

        private bool Behavior18()
        {
            return true; // skhere
        }

        #region Boilerplate

        public static bool BehaviorXX(Enemy31Entity enemy)
        {
            return enemy.BehaviorXX();
        }

        public static bool Behavior00(Enemy31Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy31Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy31Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy31Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy31Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy31Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy31Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy31Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy31Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy31Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy31Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy31Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy31Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy31Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy31Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy31Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy31Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy31Entity enemy)
        {
            return enemy.Behavior17();
        }

        public static bool Behavior18(Enemy31Entity enemy)
        {
            return enemy.Behavior18();
        }

        #endregion
    }

    [Flags]
    public enum Gorea2Flags : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        Bit23 = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }
}
