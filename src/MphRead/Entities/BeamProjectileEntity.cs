using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class BeamProjectileEntity : EntityBase
    {
        public BeamFlags Flags { get; set; }
        public BeamType Weapon { get; set; }
        public BeamType WeaponType { get; set; }

        public Vector3 Velocity { get; set; }
        public Vector3 Acceleration { get; set; } // only used for gravity
        public Vector3 BackPosition { get; set; }
        public Vector3 SpawnPosition { get; set; }
        public List<Vector3> PastPositions { get; } = new List<Vector3>();
        public Vector3 Field7C { get; set; }

        public int DrawFuncId { get; set; }
        public float Age { get; set; }
        public float Lifespan { get; set; }

        public Vector3 Color { get; set; }
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
        public EffectEntry? Effect { get; set; }
        public EntityBase? Target { get; set; }

        public float Field1E { get; set; }
        public int SpeedInterpolation { get; set; }
        public float SpeedDecayTime { get; set; }
        public float Speed { get; set; }
        public float InitialSpeed { get; set; }
        public float FinalSpeed { get; set; }
        public float FieldE0 { get; set; }
        public float FieldE8 { get; set; }
        public float FieldEC { get; set; }
        public float FieldF0 { get; set; }

        private ModelInstance? _trailModel;

        public BeamProjectileEntity() : base(EntityType.BeamProjectile)
        {
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (DrawFuncId == 0 || DrawFuncId == 3 || DrawFuncId == 6 || DrawFuncId == 7 || DrawFuncId == 10)
            {
                _trailModel = Read.GetModelInstance("trail");
            }
            else if (DrawFuncId == 1 || DrawFuncId == 2)
            {
                _trailModel = Read.GetModelInstance("electroTrail");
            }
        }

        public override bool Process(Scene scene)
        {
            if (Lifespan <= 0)
            {
                return false;
            }
            Lifespan -= scene.FrameTime;
            if (Flags.HasFlag(BeamFlags.Collided))
            {
                return true;
            }
            Age += scene.FrameTime;
            BackPosition = Position;
            // the game does this every other frame at 30 fps and keeps 5 past positions; we do it every other frame at 60 fps and keep 10,
            // and use only every other position to draw each trail segment, which results in the beam trail updating at the same frequency
            // (relative to the projectile) and having the same amount of smear as in the game
            if (scene.FrameCount % 2 == 0)
            {
                for (int i = 9; i > 0; i--)
                {
                    PastPositions[i] = PastPositions[i - 1];
                }
                PastPositions[0] = Position;
            }
            if (Flags.HasFlag(BeamFlags.Homing) && Flags.HasFlag(BeamFlags.Bit06))
            {
                if (Target != null)
                {
                    Position = Target.Position;
                }
                else
                {
                    Velocity /= 4;
                }
            }
            else
            {
                Position += Velocity;
                Velocity += Acceleration;
                Debug.Assert(SpeedDecayTime >= 0);
                if (SpeedDecayTime > 0 && Age <= SpeedDecayTime)
                {
                    float magnitude = Velocity.Length;
                    if (magnitude > 0)
                    {
                        Speed = GetInterpolatedValue(SpeedInterpolation, InitialSpeed, FinalSpeed, Age / SpeedDecayTime);
                        Velocity *= Speed / magnitude;
                    }
                }
            }
            // todo: beam SFX, node refs
            // btodo: target, homing, collision stuff
            if (Effect != null)
            {
                for (int i = 0; i < Effect.Elements.Count; i++)
                {
                    EffectElementEntry element = Effect.Elements[i];
                    element.Position = Position;
                    element.Transform = Transform.ClearScale();
                }
            }
            // btodo: expiry/collision
            if (Lifespan <= 0 && DrawFuncId == 8)
            {
                // btodo: this should really be spawned after a collision or when max dist is reached
                SpawnSniperBeam(scene);
            }
            return base.Process(scene);
        }

        private float GetInterpolatedValue(int type, float value1, float value2, float ratio)
        {
            if (type == 3)
            {
                // binary
                return ratio > 1 ? value2 : value1;
            }
            ratio = Math.Clamp(ratio, 0, 1);
            if (type == 0)
            {
                // lerp
                return value1 + (value2 - value1) * ratio;
            }
            if (type == 1)
            {
                // sin 1
                return value1 + (value2 - value1) * ((MathF.Sin(270 - 180 * ratio) + 1) / 2);
            }
            if (type == 2)
            {
                // sin 2
                return value1 + (value2 - value1) * (MathF.Sin(270 - 90 * ratio) + 1);
            }
            return 0;
        }

        public override void GetDrawInfo(Scene scene)
        {
            if (DrawFuncId == 0)
            {
                Draw00(scene);
            }
            else if (DrawFuncId == 1)
            {
                Draw01(scene);
            }
            else if (DrawFuncId == 2)
            {
                Draw02(scene);
            }
            else if (DrawFuncId == 3)
            {
                Draw03(scene);
            }
            else if (DrawFuncId == 6)
            {
                Draw06(scene);
            }
            else if (DrawFuncId == 7)
            {
                Draw07(scene);
            }
            else if (DrawFuncId == 9)
            {
                Draw09(scene);
            }
            else if (DrawFuncId == 10)
            {
                Draw10(scene);
            }
            else if (DrawFuncId == 17)
            {
                Draw17(scene);
            }
        }

        // Power Beam
        private void Draw00(Scene scene)
        {
            if (!Flags.HasFlag(BeamFlags.Collided))
            {
                scene.AddSingleParticle(SingleType.Fuzzball, Position, Color, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail1(Fixed.ToFloat(122), scene);
        }

        // uncharged Volt Driver
        private void Draw01(Scene scene)
        {
            DrawTrail1(Fixed.ToFloat(614), scene);
        }

        // charged Volt Driver
        private void Draw02(Scene scene)
        {
            DrawTrail2(Fixed.ToFloat(1024), 5, scene);
        }

        // non-affinity Judicator
        private void Draw03(Scene scene)
        {
            if (!Flags.HasFlag(BeamFlags.Collided))
            {
                base.GetDrawInfo(scene);
            }
            DrawTrail2(Fixed.ToFloat(204), 5, scene);
        }

        // enemy tear/Judicator
        private void Draw06(Scene scene)
        {
            if (!Flags.HasFlag(BeamFlags.Collided))
            {
                scene.AddSingleParticle(SingleType.Fuzzball, Position, Vector3.One, alpha: 1, scale: 1 / 4f);
            }
            // sktodo: draw trail 3
        }

        // Missile
        private void Draw07(Scene scene)
        {
            if (!Flags.HasFlag(BeamFlags.Collided))
            {
                scene.AddSingleParticle(SingleType.Fuzzball, Position, Vector3.One, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail2(Fixed.ToFloat(204), 5, scene);
        }

        // Shock Coil
        private void Draw09(Scene scene)
        {
            if (!Flags.HasFlag(BeamFlags.Collided))
            {
                if (Target != null)
                {
                    // sktodo: draw Shock Coil
                }
                else
                {
                    // todo: only draw if owner is main player
                }
            }
        }

        // Battlehammer
        private void Draw10(Scene scene)
        {
            DrawTrail2(Fixed.ToFloat(81), 2, scene);
        }

        // green energy beam
        private void Draw17(Scene scene)
        {
            if (!Flags.HasFlag(BeamFlags.Collided))
            {
                base.GetDrawInfo(scene);
            }
        }

        private void DrawTrail1(float height, Scene scene)
        {
            Debug.Assert(_trailModel != null);
            Texture texture = _trailModel.Model.Recolors[0].Textures[0];
            float uvS = (texture.Width - (1 / 16f)) / texture.Width;
            float uvT = (texture.Height - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(8);
            uvsAndVerts[0] = Vector3.Zero;
            uvsAndVerts[1] = new Vector3(Position.X - BackPosition.X, Position.Y - BackPosition.Y - height, Position.Z - BackPosition.Z);
            uvsAndVerts[2] = new Vector3(0, uvT, 0);
            uvsAndVerts[3] = new Vector3(Position.X - BackPosition.X, height + Position.Y - BackPosition.Y, Position.Z - BackPosition.Z);
            uvsAndVerts[4] = new Vector3(uvS, 0, 0);
            uvsAndVerts[5] = new Vector3(0, -height, 0);
            uvsAndVerts[6] = new Vector3(uvS, uvT, 0);
            uvsAndVerts[7] = new Vector3(0, height, 0);
            Material material = _trailModel.Model.Materials[0];
            // sktodo: make sure this is already bound, or just store it in the constructor
            int bindingId = scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            float alpha = Math.Clamp(Lifespan * 30 * 8, 0, 31) / 31;
            scene.AddRenderItem(RenderItemType.Trail1, alpha, scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(BackPosition), uvsAndVerts, bindingId);
        }

        private void DrawTrail2(float height, int segments, Scene scene)
        {
            if (segments < 2)
            {
                return;
            }
            if (segments > PastPositions.Count / 2)
            {
                segments = PastPositions.Count / 2;
            }

            Debug.Assert(_trailModel != null);
            Texture texture = _trailModel.Model.Recolors[0].Textures[0];
            float uvT = (texture.Height - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(4 * segments);
            for (int i = 0; i < segments; i++)
            {
                float uvS = 0;
                if (i > 0)
                {
                    uvS = (texture.Width / (float)(segments - 1) * i - (1 / 16f)) / texture.Width;
                }
                Vector3 vec = PastPositions[i * 2] - PastPositions[0];
                uvsAndVerts[4 * i] = new Vector3(uvS, 0, 0);
                uvsAndVerts[4 * i + 1] = new Vector3(vec.X, vec.Y - height, vec.Z);
                uvsAndVerts[4 * i + 2] = new Vector3(uvS, uvT, 0);
                uvsAndVerts[4 * i + 3] = new Vector3(vec.X, vec.Y + height, vec.Z);
            }
            Material material = _trailModel.Model.Materials[0];
            // sktodo: make sure this is already bound, or just store it in the constructor
            int bindingId = scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            float alpha = Math.Clamp(Lifespan * 30 * 8, 0, 31) / 31;
            scene.AddRenderItem(RenderItemType.Trail2, alpha, scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[0]), uvsAndVerts, bindingId);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            if (DrawFuncId == 17)
            {
                Matrix4 transform = Transform.ClearScale();
                float scale = Vector3.Distance(Position, BackPosition);
                transform.Row2.Xyz *= scale;
                transform.Row3.Xyz = BackPosition;
                return transform;
            }
            return base.GetModelTransform(inst, index);
        }

        public override void Destroy(Scene scene)
        {
            if (Effect != null)
            {
                scene.DetachEffectEntry(Effect, setExpired: true);
            }
            Owner = null;
            Effect = null;
            Target = null;
            RicochetWeapon = null;
            _trailModel = null;
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
                    Vector3 effVec2 = GetCrossVector(effVec1);
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
            float speed = GetAmount(weapon.UnchargedSpeed, weapon.MinChargeSpeed, weapon.ChargedSpeed) / 4096f / 2; // todo: FPS stuff
            float finalSpeed = GetAmount(weapon.UnchargedFinalSpeed, weapon.MinChargeFinalSpeed, weapon.ChargedFinalSpeed) / 4096f / 2;
            float speedDecayTime = weapon.SpeedDecayTimes[charged ? 1 : 0] * (1 / 30f);
            ushort speedInterpolation = weapon.SpeedInterpolations[charged ? 1 : 0];
            float gravity = GetAmount(weapon.UnchargedGravity, weapon.MinChargeGravity, weapon.ChargedGravity) / 4096f;
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
            ushort colorValue = weapon.Colors[charged ? 1 : 0];
            float red = ((colorValue >> 0) & 0x1F) / 31f;
            float green = ((colorValue >> 5) & 0x1F) / 31f;
            float blue = ((colorValue >> 10) & 0x1F) / 31f;
            var color = new Vector3(red, green, blue);
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
                beam._models.Clear();
                beam.Weapon = weapon.Weapon;
                beam.WeaponType = weapon.WeaponType;
                beam.Flags = flags;
                beam.Age = 0;
                beam.InitialSpeed = beam.Speed = speed;
                beam.FinalSpeed = finalSpeed;
                beam.SpeedDecayTime = speedDecayTime;
                beam.SpeedInterpolation = speedInterpolation;
                beam.Homing = homing;
                beam.DrawFuncId = drawFuncId;
                beam.Color = color;
                beam.CollisionEffect = colEffect;
                beam.DamageDirType = dmgDirType;
                beam.SplashDamageType = splashDmgType;
                beam.FieldE0 = fieldE0;
                beam.SpawnPosition = beam.BackPosition = beam.Position = position;
                for (int j = 0; j < 10; j++)
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
                    beam.SpawnIceWave(weapon, chargePct, scene);
                    // we don't actually "spawn" the beam projectile
                    beam.Velocity = beam.Acceleration = Vector3.Zero;
                    beam.Flags = BeamFlags.Collided;
                    beam.Lifespan = 0;
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
                if (beam.DrawFuncId == 3)
                {
                    beam.Flags |= BeamFlags.HasModel;
                    beam._models.Add(Read.GetModelInstance("iceShard"));
                }
                else if (beam.DrawFuncId == 17)
                {
                    beam.Flags |= BeamFlags.HasModel;
                    beam._models.Add(Read.GetModelInstance("energyBeam"));
                    // btodo: animation frame stuff
                }
                else
                {
                    int effectId = Metadata.BeamDrawEffects[beam.DrawFuncId];
                    if (effectId != 0)
                    {
                        Vector3 effVec1 = beam.Vec1;
                        Vector3 effVec2 = GetCrossVector(effVec1);
                        Matrix4 transform = GetTransformMatrix(effVec2, effVec1);
                        transform.Row3.Xyz = beam.Position;
                        beam.Effect = scene.SpawnEffectGetEntry(effectId, transform);
                        for (int j = 0; j < beam.Effect.Elements.Count; j++)
                        {
                            EffectElementEntry element = beam.Effect.Elements[j];
                            element.Flags |= 0x80000; // set bit 19 (lifetime extension)
                        }
                    }
                }
                // btodo: homing/target stuff
                scene.AddEntity(beam);
            }
            return true;
        }

        private void SpawnIceWave(WeaponInfo weapon, float chargePct, Scene scene)
        {
            float angle = chargePct <= 0
                ? weapon.UnchargedSpread
                : weapon.MinChargeSpread + ((weapon.ChargedSpread - weapon.MinChargeSpread) * chargePct);
            angle /= 4096f;
            Debug.Assert(angle == 60);
            // todo: collision check with player and halfturret
            Vector3 vec1 = Vec1;
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
            Matrix4 transform = Matrix4.CreateScale(MaxDistance) * GetTransformMatrix(vec2, vec1);
            transform.Row3.Xyz = Position;
            var ent = BeamEffectEntity.Create(new BeamEffectEntityData(type: 0, noSplat: false, transform), scene);
            if (ent != null)
            {
                scene.AddEntity(ent);
            }
        }

        private void SpawnSniperBeam(Scene scene)
        {
            // following what the game does, but this should always be the same as SpawnPosition
            Vector3 spawnPos = PastPositions[9];
            Vector3 vec1 = Position - spawnPos;
            float magnitude = vec1.Length;
            if (magnitude > 0)
            {
                vec1.Normalize();
                Vector3 vec2 = GetCrossVector(vec1);
                Matrix4 transform = GetTransformMatrix(vec2, vec1);
                transform.Row3.Xyz = spawnPos;
                var ent = BeamEffectEntity.Create(new BeamEffectEntityData(type: 1, noSplat: false, transform), scene);
                if (ent != null)
                {
                    ent.Scale = new Vector3(1, magnitude, 1);
                    scene.AddEntity(ent);
                }
            }
        }

        private static Vector3 GetCrossVector(Vector3 vec1)
        {
            if (vec1.Z <= Fixed.ToFloat(-3686) || vec1.Z >= Fixed.ToFloat(3686))
            {
                return Vector3.Cross(Vector3.UnitX, vec1).Normalized();
            }
            return Vector3.Cross(Vector3.UnitZ, vec1).Normalized();
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
