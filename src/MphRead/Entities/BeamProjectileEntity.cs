using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class BeamProjectileEntity : EntityBase
    {
        public BeamFlags Flags { get; set; }
        public BeamType Weapon { get; set; }
        public BeamType WeaponType { get; set; }

        public Vector3 Velocity { get; set; }
        public Vector3 Acceleration { get; set; }
        public Vector3 FrontPosition { get; set; }
        public Vector3 BackPosition { get; set; }
        public Vector3 SpawnPosition { get; set; }
        public List<Vector3> PastPositions { get; } = new List<Vector3>();
        public Vector3 Field7C { get; set; }

        public int DrawFuncId { get; set; }
        public float Age { get; set; }
        public float Lifespan { get; set; }

        public ushort Color { get; set; }
        public byte CollisionEffect { get; set; }
        public byte DamageDirType { get; set; }
        public byte SplashDamageType { get; set; }
        public float Homing { get; set; }

        public Vector3 Vec1 { get; set; }
        public Vector3 Vec2 { get; set; }

        public float Damage { get; set; }
        public float HeadshotDamage { get; set; }
        public float SplashDamage { get; set; }
        public float BeamScale { get; set; }
        public float MaxDistance { get; set; }
        public Affliction Afflictions { get; set; }

        public EntityBase? Owner { get; set; }
        public WeaponInfo? RicochetWeapon { get; set; }

        public float Field1E { get; set; }
        public float Field1F { get; set; }
        public float Field30 { get; set; }
        public float Speed { get; set; }
        public float InitialSpeed { get; set; }
        public float FieldC0 { get; set; }
        public float FieldE0 { get; set; }
        public float FieldE8 { get; set; }
        public float FieldEC { get; set; }
        public float FieldF0 { get; set; }

        public BeamProjectileEntity() : base(EntityType.BeamProjectile)
        {
        }

        public override void GetDrawInfo(Scene scene)
        {
        }

        public static bool Spawn(EntityBase owner, EquipInfo equip, Vector3 position, Vector3 direction, BeamSpawnFlags spawnFlags, Scene scene)
        {
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
            float GetAmount(int unchargedAmt, int minChargeAmt, int fullChargeAmt)
            {
                return chargePct <= 0 ? unchargedAmt : minChargeAmt + ((fullChargeAmt - minChargeAmt) * chargePct);
            }
            // btodo: calculate cost, Shock Coil frame stuff, pointer to ammo, return false if not enough
            if (!spawnFlags.HasFlag(BeamSpawnFlags.NoMuzzle))
            {
                byte effectId = weapon.MuzzleEffects[charged ? 1 : 0];
                if (effectId != 255)
                {
                    Debug.Assert(effectId >= 3);
                    Vector3 effVec1 = direction;
                    Vector3 effVec2;
                    if (effVec1.Z <= Fixed.ToFloat(-3686) || effVec1.Z >= Fixed.ToFloat(3686))
                    {
                        effVec2 = Vector3.Cross(Vector3.UnitX, effVec1).Normalized();
                    }
                    else
                    {
                        effVec2 = Vector3.Cross(Vector3.UnitZ, effVec1).Normalized();
                    }
                    Matrix4 transform = GetTransformMatrix(effVec2, effVec1);
                    transform.Row3.Xyz = position;
                    // the game does this by spawning a CBeamEffect, but that's unncessary for muzzle effects
                    // sktodo: players, enemies, and platforms need to load all the relevant beam effects when they're loaded
                    scene.SpawnEffect(effectId - 3, transform);
                }
            }
            int projectiles = (int)GetAmount(weapon.Projectiles, weapon.MinChargeProjectiles, weapon.ChargedProjectiles);
            if (projectiles <= 0)
            {
                return true;
            }

            bool instant = (charged && weapon.Flags.HasFlag(WeaponFlags.InstantCharged))
                || (!charged && weapon.Flags.HasFlag(WeaponFlags.InstantUncharged));

            BeamFlags flags = BeamFlags.None;
            float speed = GetAmount(weapon.UnchargedSpeed, weapon.MinChargeSpeed, weapon.ChargedSpeed);
            float fieldC0 = GetAmount(weapon.Field70, weapon.Field74, weapon.Field78);
            ushort field30 = weapon.Field3E[charged ? 1 : 0];
            ushort field1F = weapon.Field44[charged ? 1 : 0];
            float gravity = GetAmount(weapon.UnchargedGravity, weapon.MinChargeGravity, weapon.ChargedGravity);
            var acceleration = new Vector3(0, gravity, 0);
            float homing = GetAmount(weapon.UnchargedHoming, weapon.MinChargeHoming, weapon.ChargedHoming);
            if (homing > 0)
            {
                flags |= BeamFlags.Homing;
            }
            if (charged || spawnFlags.HasFlag(BeamSpawnFlags.Charged))
            {
                flags |= BeamFlags.Charged;
            }
            byte drawFuncId = weapon.DrawFuncIds[charged ? 1 : 0];
            ushort color = weapon.Colors[charged ? 1 : 0];
            byte colEffect = weapon.CollisionEffects[charged ? 1 : 0];
            byte dmgDirType = weapon.DmgDirTypes[charged ? 1 : 0];
            float fieldE0 = GetAmount(weapon.Field48, weapon.Field4C, weapon.Field50);
            int damage = (int)GetAmount(weapon.UnchargedDamage, weapon.MinChargeDamage, weapon.ChargedDamage);
            int hsDamage = (int)GetAmount(weapon.HeadshotDamage, weapon.MinChargeHeadshotDamage, weapon.ChargedHeadshotDamage);
            int splashDmg = (int)GetAmount(weapon.SplashDamage, weapon.MinChargeSplashDamage, weapon.ChargedSplashDamage);
            float scale = GetAmount(weapon.UnchargedScale, weapon.MinChargeScale, weapon.ChargedScale);
            byte splashDmgType = weapon.SplashDamageTypes[charged ? 1 : 0];
            if (spawnFlags.HasFlag(BeamSpawnFlags.DoubleDamage))
            {
                damage *= 2;
                hsDamage *= 2;
                splashDmg *= 2;
            }
            else if (spawnFlags.HasFlag(BeamSpawnFlags.PrimeHunter))
            {
                damage = 150 * damage / 100;
                hsDamage = 150 * hsDamage / 100;
                splashDmg = 150 * splashDmg / 100;
            }
            if (weapon.Weapon == BeamType.Imperialist && !equip.Flags.HasFlag(EquipFlags.Zoomed))
            {
                damage /= 2;
                hsDamage /= 2;
                splashDmg /= 2;
            }
            // btodo: Shock Coil damage based on frame stuff
            ushort field1E = weapon.Field1D[charged ? 1 : 0];
            float maxDist = GetAmount(weapon.UnchargedDistance, weapon.MinChargeDistance, weapon.ChargedDistance) / 4096f;
            Affliction afflictions = weapon.Afflictions[charged ? 1 : 0];
            float fieldF0 = GetAmount(weapon.Field58, weapon.Field5C, weapon.Field60);
            float lifespan = GetAmount(weapon.UnchargedLifespan, weapon.MinChargeLifespan, weapon.ChargedLifespan) * (1 / 30f);
            // btodo: set flags
            float fieldE8 = GetAmount(weapon.FieldC0, weapon.FieldC4, weapon.FieldC8);
            float fieldEC = GetAmount(weapon.FieldCC, weapon.FieldD0, weapon.FieldD4);
            int maxSpread = (int)GetAmount(weapon.UnchargedSpread, weapon.MinChargeSpread, weapon.ChargedSpread);
            WeaponInfo? ricochetWeapon = charged ? weapon.RicochetWeapon1 : weapon.RicochetWeapon0;
            Vector3 vec1 = direction;
            Vector3 field7C;
            if (vec1.X != 0 || vec1.Z != 0)
            {
                field7C = new Vector3(vec1.Z, 0, -vec1.X).Normalized();
            }
            else
            {
                field7C = Vector3.UnitX;
            }
            Vector3 vec2 = Vector3.Cross(vec1, field7C).Normalized();
            Vector3 velocity = Vector3.Zero;
            if (maxSpread <= 0)
            {
                velocity = direction * speed;
            }
            for (int i = 0; i < projectiles; i++)
            {
                // btodo: find existing beam to reuse, call hit function, destroy (instead of creating new)
                var beam = new BeamProjectileEntity();
                // btodo: draw gun smoke
                if (!charged)
                {
                    equip.SmokeLevel += weapon.SmokeShotAmount;
                    if (equip.SmokeLevel > weapon.SmokeStart)
                    {
                        equip.SmokeLevel = weapon.SmokeStart;
                    }
                }
                beam.Weapon = weapon.Weapon;
                beam.WeaponType = weapon.WeaponType;
                beam.Flags = flags;
                beam.Age = 0;
                beam.InitialSpeed = beam.Speed = speed;
                beam.FieldC0 = fieldC0;
                beam.Field30 = field30;
                beam.Field1F = field1F;
                beam.Homing = homing;
                beam.DrawFuncId = drawFuncId;
                beam.Color = color;
                beam.CollisionEffect = colEffect;
                beam.DamageDirType = dmgDirType;
                beam.SplashDamageType = splashDmgType;
                beam.FieldE0 = fieldE0;
                beam.SpawnPosition = beam.BackPosition = beam.FrontPosition = position;
                for (int j = 0; j < 5; j++)
                {
                    beam.PastPositions.Add(position);
                }
                // btodo: transform
                beam.Vec1 = vec1;
                beam.Vec2 = vec2;
                beam.Field7C = field7C;
                beam.Damage = damage;
                beam.HeadshotDamage = hsDamage;
                beam.SplashDamage = splashDmg;
                beam.BeamScale = scale;
                beam.Field1E = field1E;
                beam.MaxDistance = maxDist;
                beam.Afflictions = afflictions;
                beam.FieldF0 = fieldF0;
                beam.Lifespan = lifespan;
                beam.FieldE8 = fieldE8;
                beam.FieldEC = fieldEC;
                beam.Owner = owner;
                beam.RicochetWeapon = ricochetWeapon;
                // todo: game state max damage stuff (efficiency?)
                if (instant)
                {
                    SpawnIceWave(beam, weapon, chargePct, scene);
                    // we don't actually "spawn" the beam projectile
                    beam.Velocity = beam.Acceleration = Vector3.Zero;
                    beam.Flags = BeamFlags.Collided;
                    beam.Lifespan = 0;
                    beam._models.Clear();
                    return true;
                }
                if (maxSpread > 0)
                {
                    float angle1 = MathHelper.DegreesToRadians(Test.GetRandomInt2((uint)maxSpread) / 4096f);
                    float angle2 = MathHelper.DegreesToRadians(Test.GetRandomInt2(0x168000) / 4096f);
                    float sin1 = MathF.Sin(angle1);
                    float cos1 = MathF.Cos(angle1);
                    float sin2 = MathF.Sin(angle2);
                    float cos2 = MathF.Cos(angle2);
                    velocity.X = direction.X * cos1 + (beam.Vec2.X * cos2 + beam.Field7C.X * sin2) * sin1;
                    velocity.Y = direction.Y * cos1 + (beam.Vec2.Y * cos2 + beam.Field7C.Y * sin2) * sin1;
                    velocity.Z = direction.Z * cos1 + (beam.Vec2.Z * cos2 + beam.Field7C.Z * sin2) * sin1;
                    velocity *= beam.Speed;
                }
                beam.Velocity = velocity;
                beam.Acceleration = acceleration;
                // btodo: draw func/model/effect setup
                // btodo: targeting stuff
                scene.AddEntity(beam);
            }
            return true;
        }

        private static void SpawnIceWave(BeamProjectileEntity beam, WeaponInfo weapon, float chargePct, Scene scene)
        {
            float angle = chargePct <= 0
                ? weapon.UnchargedSpread
                : weapon.MinChargeSpread + ((weapon.ChargedSpread - weapon.MinChargeSpread) * chargePct);
            angle /= 4096f;
            // todo: collision check with player and halfturret
            Vector3 vec1 = beam.Vec1;
            Vector3 vec2;
            if (vec1.X != 0 || vec1.Z != 0)
            {
                var temp = Vector3.Cross(Vector3.UnitY, vec1);
                vec2 = Vector3.Cross(vec1, temp).Normalized();
            }
            else
            {
                var temp = Vector3.Cross(Vector3.UnitX, vec1);
                vec2 = Vector3.Cross(vec1, temp).Normalized();
            }
            Matrix4 transform = Matrix4.CreateScale(beam.MaxDistance) * GetTransformMatrix(vec2, vec1);
            transform.Row3.Xyz = beam.FrontPosition;
            var ent = BeamEffectEntity.Create(new BeamEffectEntityData(type: 0, noSplat: false, transform), scene);
            if (ent != null)
            {
                scene.AddEntity(ent);
            }
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
        HasModel = 0x100,
        Bit09 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000
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
