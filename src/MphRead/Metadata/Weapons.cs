using System.Runtime.InteropServices;

namespace MphRead
{
    public readonly struct WeaponInfo
    {
        public readonly BeamType Weapon;
        public readonly BeamType WeaponType; // same as Weapon except for platform/enemy beams
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly byte[] DrawFuncIds;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 2)]
        public readonly ushort[] Colors;
        public readonly uint Flags;
        public readonly ushort SplashDamage;
        public readonly ushort MinChargeSplashDamage;
        public readonly ushort ChargedSplashDamage;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly byte[] Field12;
        public readonly byte ShotCooldown;
        public readonly byte ShotCooldownRelated;
        public readonly byte AmmoType;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly byte[] BeamTypes; // correspond to collision effects
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly byte[] MuzzleEffects;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly byte[] Field1B;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly byte[] Field1D;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public readonly Affliction[] Afflictions; // bit 0 - freeze, bit 1 - disrupt, bit 3 - burn
        public readonly byte Field21;
        public readonly ushort MinCharge;
        public readonly ushort FullCharge;
        public readonly ushort AmmoCost;
        public readonly ushort MinChargeCost;
        public readonly ushort ChargeCost;
        public readonly ushort UnchargedDamage;
        public readonly ushort MinChargeDamage;
        public readonly ushort ChargedDamage;
        public readonly ushort HeadshotDamage;
        public readonly ushort MinChargeHeadshotDamage;
        public readonly ushort ChargedHeadshotDamage;
        public readonly ushort UnchargedLifespan;
        public readonly ushort MinChargeLifespan;
        public readonly ushort ChargedLifespan;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 2)]
        public readonly ushort[] Field3E;
        public readonly ushort Field42;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 2)]
        public readonly ushort[] Field44;
        public readonly int Field48; // uncharged
        public readonly int Field4C; // min charge
        public readonly int Field50; // full charge
        public readonly int Field54;
        public readonly int Field58; // uncharged
        public readonly int Field5C; // min charge
        public readonly int Field60; // full charge
        public readonly int Field64; // uncharged
        public readonly int Field68; // min charge
        public readonly int Field6C; // full charge
        public readonly int Field70; // uncharged
        public readonly int Field74; // min charge
        public readonly int Field78; // full charge
        public readonly int UnchargedGravity;
        public readonly int MinChargeGravity;
        public readonly int ChargedGravity;
        public readonly int UnchargedHoming;
        public readonly int MinChargeHoming;
        public readonly int ChargedHoming;
        public readonly int Field94;
        public readonly int Field98;
        public readonly int UnchargedScale;
        public readonly int MinChargeScale;
        public readonly int ChargedScale;
        public readonly int UnchargedDistance;
        public readonly int MinChargeDistance;
        public readonly int ChargedDistance;
        public readonly int FieldB4; // uncharged
        public readonly int FieldB8; // min charge
        public readonly int FieldBC; // full charge
        public readonly int FieldC0; // uncharged
        public readonly int FieldC4; // min charge
        public readonly int FieldC8; // full charge
        public readonly int FieldCC; // uncharged
        public readonly int FieldD0; // min charge
        public readonly int FieldD4; // full charge
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 2)]
        public readonly uint[] RicochetWeapon; // WeaponInfo*
        public readonly ushort ProjectileCount;
        public readonly ushort MinChargedProjectileCount;
        public readonly ushort ChargeProjectileCount;
        public readonly ushort SmokeStart; // start drawing gun smoke when smoke level reaches this value (and cap it)
        public readonly ushort SmokeMinimum; // continue drawing gun smoke until level drops below this value
        public readonly ushort SmokeDrain; // reduce level by this amount each frame (or bring it to 0)
        public readonly ushort SmokeShotAmount; // increase level by this amount when firing uncharged shot
        public readonly ushort SmokeChargeAmount; // increase level by this amount each frame while charging

        public string Name => Metadata.WeaponNames[(int)Weapon];
    }
}
