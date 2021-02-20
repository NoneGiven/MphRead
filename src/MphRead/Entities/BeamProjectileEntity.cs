using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class BeamProjectileEntity : EntityBase
    {
        public BeamFlags Flags { get; set; }

        public BeamProjectileEntity() : base(EntityType.BeamProjectile)
        {
        }

        public override void GetDrawInfo(Scene scene)
        {
        }

        public static bool Spawn(EquipInfo equip, Vector3 position, Vector3 direction, BeamSpawnFlags spawnFlags, Scene scene)
        {
            static float GetAmount(float chargePct, int unchargedAmt, int minChargeAmt, int fullChargeAmt)
            {
                return chargePct <= 0 ? unchargedAmt : minChargeAmt + ((fullChargeAmt - minChargeAmt) * chargePct);
            }
            WeaponInfo weapon = equip.Weapon;
            bool charged = false;
            float chargePct = 0;
            if (weapon.Flags.HasFlag(WeaponFlags.CanCharge))
            {
                if (weapon.Flags.HasFlag(WeaponFlags.PartialCharge))
                {
                    if (equip.ChargeLevel >= weapon.MinCharge)
                    {
                        charged = true;
                        chargePct = (equip.ChargeLevel - weapon.MinCharge) / (float)(weapon.FullCharge - weapon.MinCharge);
                    }
                }
                else if (equip.ChargeLevel >= weapon.FullCharge)
                {
                    charged = true;
                    chargePct = 1;
                }
            }
            // btodo: calculate cost, Shock Coil frame stuff, pointer to ammo, return false if not enough
            if (!spawnFlags.HasFlag(BeamSpawnFlags.NoMuzzle))
            {
                byte effectId = weapon.MuzzleEffects[charged ? 1 : 0];
                if (effectId != 255)
                {
                    Debug.Assert(effectId >= 3);
                    Vector3 vec1 = direction;
                    Vector3 vec2;
                    if (vec1.Z <= Fixed.ToFloat(-3686) || vec1.Z >= Fixed.ToFloat(3686))
                    {
                        vec2 = Vector3.Cross(Vector3.UnitX, vec1).Normalized();
                    }
                    else
                    {
                        vec2 = Vector3.Cross(Vector3.UnitZ, vec1).Normalized();
                    }
                    Matrix4 transform = GetTransformMatrix(vec2, vec1);
                    transform.Row3.Xyz = position;
                    // the game does this by spawning a CBeamEffect, but that's unncessary for muzzle effects
                    // sktodo: players, enemies, and platforms need to load all the relevant beam effects when they're loaded
                    scene.SpawnEffect(effectId - 3, transform);
                }
            }
            return true;
        }
    }

    [Flags]
    public enum BeamFlags : ushort
    {
        None = 0x0,
        Collided = 0x1,
        Charged = 0x2,
        Homing = 0x4,
        Bit03 = 0x8,
        Bit04 = 0x10,
        Bit05 = 0x20,
        Bit06 = 0x40,
        Bit07 = 0x80,
        Bit08 = 0x100,
        Bit09 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000
    }

    [Flags]
    public enum BeamSpawnFlags : byte
    {
        None = 0x0,
        DoubleDamage = 0x1,
        Charged = 0x2,
        NoMuzzle = 0x4,
        PrimeHunter = 0x8
    }
}
