using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Collision;
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
        public Vector3[] PastPositions { get; } = new Vector3[10];
        public Vector3 VecCross { get; set; }

        public int DrawFuncId { get; set; }
        public float Age { get; set; }
        public float Lifespan { get; set; }

        public Vector3 Color { get; set; }
        public byte CollisionEffect { get; set; }
        public byte DamageDirType { get; set; }
        public byte SplashDamageType { get; set; }
        public float Homing { get; set; }

        public Vector3 Direction { get; set; }
        public Vector3 Up { get; set; }

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
        public EquipInfo? Equip { get; set; }

        public float DamageInterpolation { get; set; }
        public int SpeedInterpolation { get; set; }
        public float SpeedDecayTime { get; set; }
        public float Speed { get; set; }
        public float InitialSpeed { get; set; }
        public float FinalSpeed { get; set; }
        public float DamageDirMag { get; set; }
        public float RicochetLossH { get; set; }
        public float RicochetLossV { get; set; }
        public float CylinderRadius { get; set; }

        private static readonly EquipInfo _ricochetEquip = new EquipInfo();

        private ModelInstance? _trailModel;
        private int _bindingId = 0;

        public BeamProjectileEntity() : base(EntityType.BeamProjectile)
        {
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            // model will be loaded and bound by scene setup
            if (DrawFuncId == 0 || DrawFuncId == 3 || DrawFuncId == 6 || DrawFuncId == 7 || DrawFuncId == 10 || DrawFuncId == 12)
            {
                _trailModel = Read.GetModelInstance("trail");
            }
            else if (DrawFuncId == 1 || DrawFuncId == 2)
            {
                _trailModel = Read.GetModelInstance("electroTrail");
            }
            else if (DrawFuncId == 9)
            {
                _trailModel = Read.GetModelInstance("arcWelder");
            }
            if (_trailModel != null)
            {
                Material material = _trailModel.Model.Materials[0];
                _bindingId = scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            }
        }

        public override bool Process(Scene scene)
        {
            if (Lifespan <= 0)
            {
                return false;
            }
            Lifespan -= scene.FrameTime;
            if (Flags.TestFlag(BeamFlags.Collided))
            {
                return true;
            }
            bool firstFrame = Age == 0;
            if (Flags.TestFlag(BeamFlags.Continuous) && Age > 0)
            {
                // avoid any frame time issues by just getting rid of continuous beams as soon as they're no longer being replaced
                // --> in game they stick around for a frame or two, but their lifespan will have already made it so they can't interact
                return false;
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
            if (Flags.TestFlag(BeamFlags.Homing) && Flags.TestFlag(BeamFlags.Continuous))
            {
                if (Target != null)
                {
                    Position = Target.Position;
                }
                else
                {
                    Velocity /= 4f;
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
            if (!Flags.TestFlag(BeamFlags.Continuous) || firstFrame)
            {
                CheckCollision(scene);
            }
            // btodo: target/homing stuff
            if (Flags.TestFlag(BeamFlags.HasModel))
            {
                UpdateAnimFrames(_models[0], scene);
            }
            else if (Effect != null)
            {
                for (int i = 0; i < Effect.Elements.Count; i++)
                {
                    EffectElementEntry element = Effect.Elements[i];
                    element.Position = Position;
                    element.Transform = Transform.ClearScale();
                }
            }
            if (Lifespan <= 0)
            {
                CollisionResult colRes = default;
                colRes.Plane = new Vector4(-Direction);
                colRes.Position = Position;
                SpawnCollisionEffect(colRes, noSplat: true, scene);
                OnCollision(colRes, scene);
                // btodo: sfx etc.
            }
            return true;
        }

        private void CheckCollision(Scene scene)
        {
            CollisionResult anyRes = default;
            EntityBase? colWith = null;
            bool noColEff = false;
            float minDist = 2f;
            if (MaxDistance > 0)
            {
                Vector3 frontTravel = Position - SpawnPosition;
                float dist = frontTravel.Length;
                if (dist >= MaxDistance)
                {
                    Vector3 backTravel = BackPosition - SpawnPosition;
                    float dot = Vector3.Dot(frontTravel, backTravel);
                    float pct = 1;
                    if (Fixed.ToFloat(Fixed.ToInt(dist)) != Fixed.ToFloat(Fixed.ToInt(dot)))
                    {
                        pct = (MaxDistance - dot) / (dist - dot);
                    }
                    if (pct < 2)
                    {
                        minDist = pct;
                        anyRes.Position = new Vector3(
                            BackPosition.X + (Position.X - BackPosition.X) * pct,
                            BackPosition.Y + (Position.Y - BackPosition.Y) * pct,
                            BackPosition.Z + (Position.Z - BackPosition.Z) * pct
                        );
                        if (DrawFuncId == 4)
                        {
                            // uncharged Magmaul
                            anyRes.Plane = Vector4.UnitY;
                        }
                        else
                        {
                            anyRes.Plane = new Vector4(-Direction);
                        }
                        noColEff = true;
                    }
                }
            }
            if (Flags.TestFlag(BeamFlags.SurfaceCollision))
            {
                // btodo: collide with doors and force fields
                CollisionResult colRes = default;
                if (CollisionDetection.CheckBetweenPoints(BackPosition, Position, TestFlags.AffectsBeams, scene, ref colRes)
                    && colRes.Distance < minDist)
                {
                    float dot = Vector3.Dot(BackPosition, colRes.Plane.Xyz) - colRes.Plane.W;
                    if (dot >= 0)
                    {
                        minDist = colRes.Distance;
                        anyRes = colRes;
                    }
                }
            }
            Debug.Assert(Owner != null);
            if (Owner.Type != EntityType.EnemyInstance)
            {
                // btodo: collide with enemies
            }
            // btodo: collide with players, collide with enemy beams
            if (minDist >= 0 && minDist <= 1)
            {
                float amt = Fixed.ToFloat(204);
                Position = new Vector3(
                    anyRes.Position.X + anyRes.Plane.X * amt,
                    anyRes.Position.Y + anyRes.Plane.Y * amt,
                    anyRes.Position.Z + anyRes.Plane.Z * amt
                );
                bool ricochet = true;
                // btodo: sfx and stuff
                if (colWith != null)
                {
                    // btodo: handle collision with entities
                }
                else
                {
                    bool reflected = anyRes.Flags.TestFlag(CollisionFlags.ReflectBeams);
                    // btodo: if colliding with platform or object, check beam reflection
                    if ((!Flags.TestFlag(BeamFlags.Ricochet) && !reflected)
                        || DrawFuncId == 8 || anyRes.Terrain >= Terrain.Acid)
                    {
                        if (!noColEff || Flags.TestFlag(BeamFlags.ForceEffect))
                        {
                            bool noSplat = anyRes.Terrain == Terrain.Lava; // btodo: or if entity collision
                            SpawnCollisionEffect(anyRes, noSplat, scene);
                        }
                        OnCollision(anyRes, scene);
                        ricochet = false;
                    }
                }
                if (ricochet)
                {
                    ProcessRicochet(anyRes, scene);
                }
                if (DrawFuncId == 8)
                {
                    SpawnSniperBeam(scene);
                }
            }
        }

        private void ProcessRicochet(CollisionResult colRes, Scene scene)
        {
            float dot1 = Vector3.Dot(Velocity, colRes.Plane.Xyz);
            Velocity = new Vector3(
                (Velocity.X - 2 * colRes.Plane.X * dot1) * RicochetLossH,
                (Velocity.Y - 2 * colRes.Plane.Y * dot1) * RicochetLossV,
                (Velocity.Z - 2 * colRes.Plane.Z * dot1) * RicochetLossH
            );
            Speed = Velocity.Length;
            float dot2 = Vector3.Dot(Direction, colRes.Plane.Xyz);
            Direction = new Vector3(
                Direction.X - 2 * colRes.Plane.X * dot2,
                Direction.Y - 2 * colRes.Plane.Y * dot2,
                Direction.Z - 2 * colRes.Plane.Z * dot2
            );
            float dot3 = Vector3.Dot(colRes.Position, colRes.Plane.Xyz);
            float factor = dot3 - colRes.Plane.W - 0.01f;
            BackPosition = Position = new Vector3(
                colRes.Position.X + colRes.Plane.X * factor,
                colRes.Position.Y + colRes.Plane.Y * factor,
                colRes.Position.Z + colRes.Plane.Z * factor
            );
            // hack to fix past positions after ricochet
            for (int i = 9; i > 0; i--)
            {
                PastPositions[i] = PastPositions[i - 1];
            }
            PastPositions[0] = Position;
            if (scene.FrameCount % 2 == 0)
            {
                for (int i = 9; i > 0; i--)
                {
                    PastPositions[i] = PastPositions[i - 1];
                }
                PastPositions[0] = Position;
            }
            Console.WriteLine(scene.FrameCount % 2);
            // btodo: sfx
        }

        public void OnCollision(CollisionResult colRes, Scene scene)
        {
            if (Effect != null)
            {
                scene.DetachEffectEntry(Effect, setExpired: true);
                Effect = null;
            }
            if (SplashDamage > 0 && colRes.Terrain <= Terrain.Lava)
            {
                Debug.Assert(Equip != null);
                Debug.Assert(Owner != null);
                // btodo: splash damage
                if (Weapon == BeamType.OmegaCannon)
                {
                    scene.SetFade(FadeType.FadeInWhite, 15 * (1 / 30f), overwrite: false);
                }
                if (RicochetWeapon != null)
                {
                    // btodo: don't spawn ricochet when hitting player
                    Vector3 factor = Velocity * 7;
                    float dot = Vector3.Dot(colRes.Plane.Xyz, factor);
                    // colRes.Plane.X + factor.X - colRes.Plane.X * 2 * dot
                    Vector3 spawnDir = new Vector3(
                        colRes.Plane.X + factor.X - colRes.Plane.X * 2 * dot,
                        colRes.Plane.Y + factor.Y - colRes.Plane.Y * 2 * dot,
                        colRes.Plane.Z + factor.Z - colRes.Plane.Z * 2 * dot
                    ).Normalized();
                    _ricochetEquip.Beams = Equip.Beams;
                    _ricochetEquip.Weapon = RicochetWeapon;
                    BeamSpawnFlags flags = BeamSpawnFlags.None;
                    if (Flags.TestFlag(BeamFlags.Charged))
                    {
                        flags |= BeamSpawnFlags.Charged;
                    }
                    Spawn(Owner, _ricochetEquip, colRes.Position, spawnDir, flags, scene);
                }
            }
            if (!Flags.TestFlag(BeamFlags.Continuous))
            {
                Flags |= BeamFlags.Collided;
                Lifespan = 4 * (1 / 30f); // todo: frame time stuff
                Velocity = Vector3.Zero;
            }
            if (Owner != null)
            {
                // todo: send event
            }
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
            else if (DrawFuncId == 6 || DrawFuncId == 12)
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
            if (!Flags.TestFlag(BeamFlags.Collided))
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
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                base.GetDrawInfo(scene);
            }
            DrawTrail2(Fixed.ToFloat(204), 5, scene);
        }

        // enemy tear/Judicator
        private void Draw06(Scene scene)
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                scene.AddSingleParticle(SingleType.Fuzzball, Position, Vector3.One, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail3(Fixed.ToFloat(204), scene);
        }

        // Missile
        private void Draw07(Scene scene)
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                scene.AddSingleParticle(SingleType.Fuzzball, Position, Vector3.One, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail2(Fixed.ToFloat(204), 5, scene);
        }

        // Shock Coil
        private void Draw09(Scene scene)
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                if (Target != null)
                {
                    DrawTrail4(Fixed.ToFloat(614), 2048, 10, scene);
                }
                else
                {
                    // todo: only draw if owner is main player
                    DrawTrail4(Fixed.ToFloat(102), 1433, 5, scene);
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
            if (!Flags.TestFlag(BeamFlags.Collided))
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
            float alpha = Math.Clamp(Lifespan * 30 * 8, 0, 31) / 31;
            scene.AddRenderItem(RenderItemType.TrailSingle, alpha, scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(BackPosition), uvsAndVerts, _bindingId);
        }

        private void DrawTrail2(float height, int segments, Scene scene)
        {
            Debug.Assert(_trailModel != null);
            if (segments < 2)
            {
                return;
            }
            if (segments > PastPositions.Length / 2)
            {
                segments = PastPositions.Length / 2;
            }
            int count = 4 * segments;
            Texture texture = _trailModel.Model.Recolors[0].Textures[0];
            float uvT = (texture.Height - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(count);
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
            float alpha = Math.Clamp(Lifespan * 30 * 8, 0, 31) / 31;
            scene.AddRenderItem(RenderItemType.TrailMulti, alpha, scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[0]), uvsAndVerts, _bindingId, count);
        }

        private void DrawTrail3(float height, Scene scene)
        {
            Debug.Assert(_trailModel != null);
            Texture texture = _trailModel.Model.Recolors[0].Textures[0];
            float uvS2 = (texture.Width - (1 / 16f)) / texture.Width;
            float uvT2 = (texture.Height / 4f - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(8);
            uvsAndVerts[0] = Vector3.Zero;
            uvsAndVerts[1] = new Vector3(0, -height, 0);
            uvsAndVerts[2] = new Vector3(0, uvT2, 0);
            uvsAndVerts[3] = new Vector3(0, height, 0);
            uvsAndVerts[4] = new Vector3(uvS2, 0, 0);
            uvsAndVerts[5] = new Vector3(
                PastPositions[8].X - PastPositions[0].X,
                PastPositions[8].Y - PastPositions[0].Y - height,
                PastPositions[8].Z - PastPositions[0].Z
            );
            uvsAndVerts[6] = new Vector3(uvS2, uvT2, 0);
            uvsAndVerts[7] = new Vector3(PastPositions[8].X - PastPositions[0].X,
                PastPositions[8].Y - PastPositions[0].Y + height,
                PastPositions[8].Z - PastPositions[0].Z
            );
            Material material = _trailModel.Model.Materials[0];
            float alpha = Math.Clamp(Lifespan * 30 * 8, 0, 31) / 31;
            scene.AddRenderItem(RenderItemType.TrailSingle, alpha, scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[0]), uvsAndVerts, _bindingId);
        }

        private void DrawTrail4(float height, uint range, int segments, Scene scene)
        {
            Debug.Assert(_trailModel != null);
            if (segments < 2)
            {
                return;
            }
            int count = 4 * segments;

            int frames = (int)scene.FrameCount / 2;
            uint rng = (uint)(frames + (int)(Position.X * 4096));
            int index = frames & 15;
            float halfRange = range / 4096f / 2;
            Vector3 vec = PastPositions[8] - Position;
            Texture texture = _trailModel.Model.Recolors[0].Textures[0];
            float uvT = (texture.Height - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(count);
            for (int i = 0; i < segments; i++)
            {
                float uvS = 0;
                int factor = index + i;
                if (factor > 0)
                {
                    uvS = (2 * texture.Width / (float)(segments - 1) * factor - (1 / 16f)) / texture.Width;
                }

                float pct = (float)i / segments - 1;

                // todo?: not sure if dividing by 4 is strictly correct here
                float x = vec.X * pct + Velocity.X / 4 * pct * (1 - pct);
                float y = vec.Y * pct + Velocity.Y / 4 * pct * (1 - pct);
                float z = vec.Z * pct + Velocity.Z / 4 * pct * (1 - pct);

                if (i > 0 && i < segments - 1)
                {
                    x += Rng.CallRng(ref rng, range) / 4096f - halfRange;
                    y += Rng.CallRng(ref rng, range) / 4096f - halfRange;
                    z += Rng.CallRng(ref rng, range) / 4096f - halfRange;
                }

                uvsAndVerts[4 * i] = new Vector3(uvS, 0, 0);
                uvsAndVerts[4 * i + 1] = new Vector3(x, y - height, z);
                uvsAndVerts[4 * i + 2] = new Vector3(uvS, uvT, 0);
                uvsAndVerts[4 * i + 3] = new Vector3(x, y + height, z);
            }

            Material material = _trailModel.Model.Materials[0];
            scene.AddRenderItem(RenderItemType.TrailMulti, alpha: 1, scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[8]), uvsAndVerts, _bindingId, count);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            if (DrawFuncId == 3)
            {
                Matrix4 transform = GetTransformMatrix(Direction, Up);
                transform.Row3.Xyz = Position;
                return transform;
            }
            if (DrawFuncId == 17)
            {
                Matrix4 transform = Transform;
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
            Equip = null;
            _trailModel = null;
        }

        private static BeamProjectileEntity ChooseBeamSlot(EquipInfo equip, EntityBase owner)
        {
            if (equip.Weapon.Flags.TestFlag(WeaponFlags.Continuous))
            {
                for (int i = 0; i < equip.Beams.Length; i++)
                {
                    BeamProjectileEntity beam = equip.Beams[i];
                    if (beam.Flags.TestFlag(BeamFlags.Continuous) && beam.WeaponType == equip.Weapon.WeaponType
                        && beam.Owner == owner && beam.Lifespan < equip.Weapon.UnchargedLifespan)
                    {
                        return beam;
                    }
                }
            }
            for (int i = 0; i < equip.Beams.Length; i++)
            {
                BeamProjectileEntity beam = equip.Beams[i];
                if (beam.Lifespan <= 0)
                {
                    return beam;
                }
                if (beam.Flags.TestFlag(BeamFlags.Continuous) && beam.WeaponType == equip.Weapon.WeaponType
                    && beam.Owner == owner && beam.Lifespan < equip.Weapon.UnchargedLifespan)
                {
                    return beam;
                }
            }
            return equip.Beams[^1];
        }

        public static void Spawn(EntityBase owner, EquipInfo equip, Vector3 position, Vector3 direction, BeamSpawnFlags spawnFlags, Scene scene)
        {
            WeaponInfo weapon = equip.Weapon;
            bool charged = false;
            float chargePct = 0;
            if (weapon.Flags.TestFlag(WeaponFlags.CanCharge))
            {
                if (weapon.Flags.TestFlag(WeaponFlags.PartialCharge))
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
            if (!spawnFlags.TestFlag(BeamSpawnFlags.NoMuzzle))
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
                    scene.SpawnEffect(effectId - 3, transform);
                }
            }
            int projectiles = (int)GetAmount(weapon.Projectiles, weapon.MinChargeProjectiles, weapon.ChargedProjectiles);
            if (projectiles <= 0)
            {
                return;
            }

            bool instantAoe = (charged && weapon.Flags.TestFlag(WeaponFlags.AoeCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.AoeUncharged));

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
            if (charged || spawnFlags.TestFlag(BeamSpawnFlags.Charged))
            {
                flags |= BeamFlags.Charged;
            }
            if ((charged && weapon.Flags.TestFlag(WeaponFlags.RicochetCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.RicochetUncharged)))
            {
                flags |= BeamFlags.Ricochet;
            }
            if ((charged && weapon.Flags.TestFlag(WeaponFlags.SelfDamageCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.SelfDamageUncharged)))
            {
                flags |= BeamFlags.SelfDamage;
            }
            if ((charged && weapon.Flags.TestFlag(WeaponFlags.ForceEffectCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.ForceEffectUncharged)))
            {
                flags |= BeamFlags.ForceEffect;
            }
            if ((charged && weapon.Flags.TestFlag(WeaponFlags.DestroyableCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.DestroyableUncharged)))
            {
                flags |= BeamFlags.Destroyable;
            }
            if ((charged && weapon.Flags.TestFlag(WeaponFlags.LifeDrainCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.LifeDrainUncharged)))
            {
                flags |= BeamFlags.Destroyable;
            }
            byte drawFuncId = weapon.DrawFuncIds[charged ? 1 : 0];
            ushort colorValue = weapon.Colors[charged ? 1 : 0];
            float red = ((colorValue >> 0) & 0x1F) / 31f;
            float green = ((colorValue >> 5) & 0x1F) / 31f;
            float blue = ((colorValue >> 10) & 0x1F) / 31f;
            var color = new Vector3(red, green, blue);
            byte colEffect = weapon.CollisionEffects[charged ? 1 : 0];
            byte dmgDirType = weapon.DmgDirTypes[charged ? 1 : 0];
            float dmgDirMag = GetAmount(weapon.UnchargedDmgDirMag, weapon.MinChargeDmgDirMag, weapon.ChargedDmgDirMag) / 4096f;
            int damage = (int)GetAmount(weapon.UnchargedDamage, weapon.MinChargeDamage, weapon.ChargedDamage);
            int hsDamage = (int)GetAmount(weapon.HeadshotDamage, weapon.MinChargeHeadshotDamage, weapon.ChargedHeadshotDamage);
            int splashDmg = (int)GetAmount(weapon.SplashDamage, weapon.MinChargeSplashDamage, weapon.ChargedSplashDamage);
            float scale = GetAmount(weapon.UnchargedScale, weapon.MinChargeScale, weapon.ChargedScale);
            byte splashDmgType = weapon.SplashDamageTypes[charged ? 1 : 0];
            if (spawnFlags.TestFlag(BeamSpawnFlags.DoubleDamage))
            {
                damage *= 2;
                hsDamage *= 2;
                splashDmg *= 2;
            }
            else if (spawnFlags.TestFlag(BeamSpawnFlags.PrimeHunter))
            {
                damage = 150 * damage / 100;
                hsDamage = 150 * hsDamage / 100;
                splashDmg = 150 * splashDmg / 100;
            }
            if (weapon.Weapon == BeamType.Imperialist && !equip.Flags.TestFlag(EquipFlags.Zoomed))
            {
                damage /= 2;
                hsDamage /= 2;
                splashDmg /= 2;
            }
            // btodo: Shock Coil damage based on frame stuff
            ushort damageInterpolation = weapon.DamageInterpolations[charged ? 1 : 0];
            float maxDist = GetAmount(weapon.UnchargedDistance, weapon.MinChargeDistance, weapon.ChargedDistance) / 4096f;
            Affliction afflictions = weapon.Afflictions[charged ? 1 : 0];
            float cylinderRadius = GetAmount(weapon.UnchargedCylRadius, weapon.MinChargeCylRadius, weapon.ChargedCylRadius);
            float lifespan = GetAmount(weapon.UnchargedLifespan, weapon.MinChargeLifespan, weapon.ChargedLifespan) * (1 / 30f);
            if (weapon.Flags.TestFlag(WeaponFlags.Continuous))
            {
                flags |= BeamFlags.Continuous;
            }
            if (weapon.Flags.TestFlag(WeaponFlags.SurfaceCollision))
            {
                flags |= BeamFlags.SurfaceCollision;
            }
            uint radiusIndex = (((uint)weapon.Flags) >> (charged ? 26 : 24)) & 3; // bits 24/25 or 26/27
            flags = (BeamFlags)((ushort)flags | (radiusIndex << 9)); // bits 9/10
            float ricochetLossH = GetAmount(weapon.UnchargedRicochetLossH, weapon.MinChargeRicochetLossH, weapon.ChargedRicochetLossH) / 4096f;
            float ricochetLossV = GetAmount(weapon.UnchargedRicochetLossV, weapon.MinChargeRicochetLossV, weapon.ChargedRicochetLossV) / 4096f;
            int maxSpread = (int)GetAmount(weapon.UnchargedSpread, weapon.MinChargeSpread, weapon.ChargedSpread);
            WeaponInfo? ricochetWeapon = charged ? weapon.ChargedRicochetWeapon : weapon.UnchargedRicochetWeapon;
            Vector3 vec1 = direction;
            Vector3 vecC;
            if (vec1.X != 0 || vec1.Z != 0)
            {
                vecC = new Vector3(vec1.Z, 0, -vec1.X).Normalized();
            }
            else
            {
                vecC = Vector3.UnitX;
            }
            Vector3 vec2 = Vector3.Cross(vec1, vecC).Normalized();
            Vector3 velocity = Vector3.Zero;
            if (maxSpread <= 0)
            {
                velocity = direction * speed;
            }
            for (int i = 0; i < projectiles; i++)
            {
                BeamProjectileEntity beam = ChooseBeamSlot(equip, owner);
                if (beam.Lifespan > 0 && !beam.Flags.TestFlag(BeamFlags.Collided))
                {
                    CollisionResult colRes = default;
                    colRes.Position = beam.Position;
                    colRes.Plane = new Vector4(-beam.Direction);
                    beam.RicochetWeapon = null;
                    beam.OnCollision(colRes, scene);
                }
                beam.Destroy(scene);
                scene.RemoveEntity(beam);
                beam._models.Clear();
                // btodo: draw gun smoke
                if (!charged)
                {
                    equip.SmokeLevel += weapon.SmokeShotAmount;
                    if (equip.SmokeLevel > weapon.SmokeStart)
                    {
                        equip.SmokeLevel = weapon.SmokeStart;
                    }
                }
                beam.Owner = owner;
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
                // btodo: load all collision effects, splat effects, etc. in room setup
                beam.CollisionEffect = colEffect;
                beam.DamageDirType = dmgDirType;
                beam.SplashDamageType = splashDmgType;
                beam.DamageDirMag = dmgDirMag;
                beam.SpawnPosition = beam.BackPosition = beam.Position = position;
                for (int j = 0; j < 10; j++)
                {
                    beam.PastPositions[j] = position;
                }
                // btodo: transform
                beam.Direction = vec1;
                beam.Up = vec2;
                beam.VecCross = vecC;
                beam.Damage = damage;
                beam.HeadshotDamage = hsDamage;
                beam.SplashDamage = splashDmg;
                beam.BeamScale = scale;
                beam.DamageInterpolation = damageInterpolation;
                beam.MaxDistance = maxDist;
                beam.Afflictions = afflictions;
                beam.CylinderRadius = cylinderRadius;
                beam.Lifespan = lifespan;
                beam.RicochetLossH = ricochetLossH;
                beam.RicochetLossV = ricochetLossV;
                beam.RicochetWeapon = ricochetWeapon;
                beam.Equip = equip;
                // todo: game state max damage stuff (efficiency?)
                if (instantAoe)
                {
                    beam.SpawnIceWave(weapon, chargePct, scene);
                    // we don't actually "spawn" the beam projectile
                    beam.Velocity = beam.Acceleration = Vector3.Zero;
                    beam.Flags = BeamFlags.Collided;
                    beam.Lifespan = 0;
                    beam.Destroy(scene);
                    return;
                }
                if (maxSpread > 0)
                {
                    float angle1 = MathHelper.DegreesToRadians(Rng.GetRandomInt2((uint)maxSpread) / 4096f);
                    float angle2 = MathHelper.DegreesToRadians(Rng.GetRandomInt2(0x168000) / 4096f);
                    float sin1 = MathF.Sin(angle1);
                    float cos1 = MathF.Cos(angle1);
                    float sin2 = MathF.Sin(angle2);
                    float cos2 = MathF.Cos(angle2);
                    velocity.X = direction.X * cos1 + (beam.Up.X * cos2 + beam.VecCross.X * sin2) * sin1;
                    velocity.Y = direction.Y * cos1 + (beam.Up.Y * cos2 + beam.VecCross.Y * sin2) * sin1;
                    velocity.Z = direction.Z * cos1 + (beam.Up.Z * cos2 + beam.VecCross.Z * sin2) * sin1;
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
                    ModelInstance model = Read.GetModelInstance("energyBeam");
                    model.SetAnimation(0);
                    beam._models.Add(model);
                    Matrix4 transform = GetTransformMatrix(beam.Direction, beam.Up);
                    transform.Row3.Xyz = position;
                    beam.Transform = transform;
                    model.AnimInfo.Frame[0] = (int)scene.FrameCount / 2 % model.AnimInfo.FrameCount[0];
                }
                else
                {
                    int effectId = Metadata.BeamDrawEffects[beam.DrawFuncId];
                    if (effectId != 0)
                    {
                        Vector3 effVec1 = beam.Direction;
                        Vector3 effVec2 = GetCrossVector(effVec1);
                        Matrix4 transform = GetTransformMatrix(effVec2, effVec1);
                        transform.Row3.Xyz = beam.Position;
                        beam.Effect = scene.SpawnEffectGetEntry(effectId, transform);
                        for (int j = 0; j < beam.Effect.Elements.Count; j++)
                        {
                            EffectElementEntry element = beam.Effect.Elements[j];
                            element.Flags |= EffElemFlags.ElementExtension;
                        }
                    }
                }
                // btodo: the latter two parts of this condition are just one code path
                if (beam.Flags.TestFlag(BeamFlags.Homing)
                    && weapon.Flags.TestFlag(WeaponFlags.Continuous) && beam.WeaponType == BeamType.Platform)
                {
                    // btodo: check for entities of other types
                    // btodo: implement the "get alive" check
                    float tolerance = weapon.HomingTolerance / 4096f;
                    float homingDistance = tolerance;
                    for (int j = 0; j < scene.Entities.Count; j++)
                    {
                        EntityBase entity = scene.Entities[j];
                        if (entity.Type == EntityType.Platform && entity != beam.Owner)
                        {
                            var platform = (PlatformEntity)entity;
                            if (platform.Flags.TestFlag(PlatformFlags.BeamTarget))
                            {
                                Vector3 between = platform.Position - beam.Position;
                                float dot1 = Vector3.Dot(between, between);
                                float range = weapon.HomingRange / 4096f;
                                if (dot1 <= range * range)
                                {
                                    float distance = between.Length;
                                    if (distance > 0)
                                    {
                                        float dot2 = Vector3.Dot(between, beam.Velocity.Normalized());
                                        float div1 = dot2 / distance;
                                        if (div1 >= homingDistance)
                                        {
                                            float div2 = distance / range;
                                            if (div2 > 1)
                                            {
                                                div2 = 1;
                                            }
                                            if (div1 >= tolerance + div2 * (Fixed.ToFloat(4094) - tolerance))
                                            {
                                                beam.Target = entity;
                                                homingDistance = div1;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                scene.AddEntity(beam);
            }
        }

        private void SpawnIceWave(WeaponInfo weapon, float chargePct, Scene scene)
        {
            float angle = chargePct <= 0
                ? weapon.UnchargedSpread
                : weapon.MinChargeSpread + ((weapon.ChargedSpread - weapon.MinChargeSpread) * chargePct);
            angle /= 4096f;
            Debug.Assert(angle == 60);
            // btodo: collision check with player and halfturret
            Vector3 vec1 = Direction;
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

        private void SpawnCollisionEffect(CollisionResult colRes, bool noSplat, Scene scene)
        {
            if (CollisionEffect != 255)
            {
                if (PlayerEntity.PlayerCount > 2 && CollisionEffect == 4)
                {
                    // powerBeam (4 - 3 = 1)
                    noSplat = true;
                }
                var spawnPos = new Vector3(
                    colRes.Position.X + colRes.Plane.X / 8,
                    colRes.Position.Y + colRes.Plane.Y / 8,
                    colRes.Position.Z + colRes.Plane.Z / 8
                );
                Vector3 vec1 = WeaponType == BeamType.Imperialist ? -Direction : colRes.Plane.Xyz;
                // btodo: handle entity collision
                Vector3 vec2 = GetCrossVector(vec1);
                Matrix4 transform = GetTransformMatrix(vec2, vec1);
                transform.Row3.Xyz = spawnPos;
                // somehwat redundant logic, game uses "511" bits which accomplish the same thing as this terrain type check
                if (scene.GameMode != GameMode.SinglePlayer || colRes.Terrain <= Terrain.Lava)
                {
                    var ent = BeamEffectEntity.Create(new BeamEffectEntityData(CollisionEffect, noSplat, transform), scene);
                    if (ent != null)
                    {
                        if (SplashDamage > 0)
                        {
                            ent.Scale = Scale;
                        }
                        scene.AddEntity(ent);
                    }
                }
                byte splatEffect = _terSplat1P[(int)Weapon][(int)colRes.Terrain];
                if (scene.GameMode == GameMode.SinglePlayer && splatEffect != 255)
                {
                    splatEffect += 3;
                    var ent = BeamEffectEntity.Create(new BeamEffectEntityData(splatEffect, noSplat, transform), scene);
                    if (ent != null)
                    {
                        scene.AddEntity(ent);
                    }
                }
            }
        }

        private static readonly IReadOnlyList<IReadOnlyList<byte>> _terSplat1P
            = new List<IReadOnlyList<byte>>()
            {
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 },
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 },
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 },
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 },
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 },
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 },
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 },
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 },
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 },
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 },
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 }
            };

        private void SpawnSniperBeam(Scene scene)
        {
            // following what the game does, but this should always be the same as SpawnPosition
            Vector3 spawnPos = PastPositions[8];
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
        Ricochet = 0x8,
        SelfDamage = 0x10,
        ForceEffect = 0x20,
        Continuous = 0x40,
        Destroyable = 0x80,
        HasModel = 0x100,
        RadiusIndex1 = 0x200, // pair with bit 10: index 0-3 of radius for enemy beam collision with player beams
        RadiusIndex2 = 0x400,
        LifeDrain = 0x800,
        SurfaceCollision = 0x1000
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
