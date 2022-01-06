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
        public BeamType Beam { get; set; }
        public BeamType BeamKind { get; set; }

        public Vector3 Velocity { get; set; }
        public Vector3 Acceleration { get; set; } // only used for gravity
        public Vector3 BackPosition { get; set; }
        public Vector3 SpawnPosition { get; set; }
        public Vector3[] PastPositions { get; } = new Vector3[10];

        public int DrawFuncId { get; set; }
        public float Age { get; set; }
        public float Lifespan { get; set; }
        public ulong Parity { get; set; } // skdebug?

        public Vector3 Color { get; set; }
        public byte CollisionEffect { get; set; }
        public byte DamageDirType { get; set; }
        public byte SplashDamageType { get; set; }
        public float Homing { get; set; }

        public Vector3 Direction { get; set; }
        public Vector3 Right { get; set; }
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

        public int DamageInterpolation { get; set; }
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

        public BeamProjectileEntity(Scene scene) : base(EntityType.BeamProjectile, scene)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
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
                _bindingId = _scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            }
        }

        public override bool Process()
        {
            if (Lifespan <= 0)
            {
                return false;
            }
            Lifespan -= _scene.FrameTime;
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
            Age += _scene.FrameTime;
            BackPosition = Position;
            // the game does this every other frame at 30 fps and keeps 5 past positions; we do it every other frame at 60 fps and keep 10,
            // and use only every other position to draw each trail segment, which results in the beam trail updating at the same frequency
            // (relative to the projectile) and having the same amount of smear as in the game
            // todo?: might need to revisit
            // --> observed homing missile trail flickering(?) when curved, and judicator trail getting more opaque on final collision
            if (_scene.FrameCount % 2 == 0)
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
            // todo: positional audio (w/ BeamKind check), node refs
            if (Target != null)
            {
                for (int i = 0; i < _scene.MessageQueue.Count; i++)
                {
                    MessageInfo info = _scene.MessageQueue[i];
                    if (info.Message == Message.Destroyed && info.ExecuteFrame == _scene.FrameCount && info.Sender == Target)
                    {
                        Target = null;
                        break;
                    }
                }
            }
            if (!Flags.TestFlag(BeamFlags.Continuous) || firstFrame)
            {
                CheckCollision();
            }
            if (Flags.TestFlag(BeamFlags.Homing) && !Flags.TestFlag(BeamFlags.Continuous) && Target != null)
            {
                Target.GetPosition(out Vector3 targetPos);
                Vector3 acceleration = targetPos - Position;
                if (acceleration != Vector3.Zero)
                {
                    acceleration = acceleration.Normalized();
                }
                else
                {
                    acceleration = Vector3.UnitX;
                }
                acceleration *= Speed;
                if (Vector3.Dot(acceleration, Velocity) >= 0)
                {
                    acceleration -= Velocity;
                    float accelMag = acceleration.Length;
                    if (accelMag > Homing)
                    {
                        acceleration *= Homing / accelMag;
                    }
                    Velocity += acceleration;
                }
                else
                {
                    Target = null;
                }
            }
            // btodo: homing SFX
            if (Flags.TestFlag(BeamFlags.HasModel))
            {
                UpdateAnimFrames(_models[0]);
            }
            else if (Effect != null)
            {
                Effect.Transform(Position, Transform.ClearScale());
            }
            if (Lifespan <= 0)
            {
                CollisionResult colRes = default;
                colRes.Plane = new Vector4(-Direction);
                colRes.Position = Position;
                SpawnCollisionEffect(colRes, noSplat: true);
                OnCollision(colRes, colWith: null);
                // btodo: sfx etc.
            }
            return true;
        }

        private void CheckCollision()
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
                CollisionResult colRes = default;
                if (CollisionDetection.CheckBetweenPoints(BackPosition, Position, TestFlags.AffectsBeams, _scene, ref colRes)
                    && colRes.Distance < minDist)
                {
                    float dot = Vector3.Dot(BackPosition, colRes.Plane.Xyz) - colRes.Plane.W;
                    if (dot >= 0)
                    {
                        minDist = colRes.Distance;
                        anyRes = colRes;
                    }
                }
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Door)
                    {
                        continue;
                    }
                    var door = (DoorEntity)entity;
                    if (door.Flags.TestFlag(DoorFlags.Open))
                    {
                        continue;
                    }
                    Vector3 doorFacing = door.FacingVector;
                    Vector3 lockPos = door.LockPosition;
                    var plane = new Vector4(doorFacing, 0);
                    if (Vector3.Dot(BackPosition - lockPos, doorFacing) < 0)
                    {
                        plane *= -1;
                    }
                    Vector3 wvec = plane.Xyz * (lockPos + 0.4f * plane.Xyz);
                    plane.W = wvec.X + wvec.Y + wvec.Z;
                    if (CollisionDetection.CheckCylinderIntersectPlane(BackPosition, Position, plane, ref colRes)
                        && colRes.Distance < minDist)
                    {
                        Vector3 between = colRes.Position - lockPos;
                        if (between.LengthSquared < door.RadiusSquared)
                        {
                            minDist = colRes.Distance;
                            anyRes = colRes;
                            colWith = door;
                            noColEff = false;
                            anyRes.Field0 = 0;
                            anyRes.Plane = plane;
                            anyRes.Flags = CollisionFlags.None;
                        }
                    }
                }
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.ForceField)
                    {
                        continue;
                    }
                    // todo: some of these properties are compatible with the entity moving, some aren't
                    var forceField = (ForceFieldEntity)entity;
                    if (forceField.Active
                        && CollisionDetection.CheckCylinderIntersectPlane(BackPosition, Position, forceField.Plane, ref colRes)
                        && colRes.Distance < minDist)
                    {
                        Vector3 between = colRes.Position - forceField.Position;
                        float dot = Vector3.Dot(between, forceField.FieldUpVector);
                        if (dot <= forceField.Height && dot >= -forceField.Height)
                        {
                            dot = Vector3.Dot(between, forceField.FieldRightVector);
                            if (dot <= forceField.Width && dot >= -forceField.Width)
                            {
                                minDist = colRes.Distance;
                                anyRes = colRes;
                                colWith = forceField;
                                noColEff = false;
                                anyRes.Field0 = 0;
                                anyRes.Plane = forceField.Plane;
                            }
                        }
                    }
                }
            }
            Debug.Assert(Owner != null);
            if (Owner.Type != EntityType.EnemyInstance)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.EnemyInstance)
                    {
                        continue;
                    }
                    CollisionResult res = default;
                    var enemy = (EnemyInstanceEntity)entity;
                    if (enemy.Flags.TestFlag(EnemyFlags.CollideBeam)
                        && CollisionDetection.CheckCylinderOverlapVolume(enemy.HurtVolume, BackPosition, Position, CylinderRadius, ref res))
                    {
                        if (Beam == BeamType.OmegaCannon && enemy.EnemyType == EnemyType.GoreaMeteor)
                        {
                            enemy.TakeDamage(500, this);
                        }
                        else if (res.Distance < minDist)
                        {
                            minDist = res.Distance;
                            anyRes = res;
                            colWith = enemy;
                            noColEff = false;
                        }
                    }
                }
            }
            bool hitHalfturret = false;
            // todo: visualize player collision (and rename some "pickup" fields)
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player || Owner == entity)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health == 0)
                {
                    continue;
                }
                bool hasHalfturret = player.Hunter == Hunter.Weavel && player.Flags2.TestFlag(PlayerFlags2.Halfturret);
                if ((Owner == player || hasHalfturret && Owner == player.Halfturret)
                    && (!Flags.TestFlag(BeamFlags.SelfDamage) || Age < 1 / 30f * 4))
                {
                    continue;
                }
                bool hitPlayer = false;
                CollisionResult playerRes = default;
                float radii = player.Volume.SphereRadius + CylinderRadius;
                if (player.IsAltForm)
                {
                    if (player.Hunter == Hunter.Kanden)
                    {
                        if (CollisionDetection.CheckCylinderOverlapSphere(BackPosition, Position,
                            player.KandenSegPos[2], 1.6f, ref playerRes))
                        {
                            if (CollisionDetection.CheckCylinderOverlapSphere(BackPosition, Position,
                                player.Volume.SpherePosition, radii, ref playerRes)
                                || CollisionDetection.CheckCylinderOverlapSphere(BackPosition, Position,
                                    player.KandenSegPos[1], radii, ref playerRes)
                                || CollisionDetection.CheckCylinderOverlapSphere(BackPosition, Position,
                                    player.KandenSegPos[2], radii, ref playerRes)
                                || CollisionDetection.CheckCylinderOverlapSphere(BackPosition, Position,
                                    player.KandenSegPos[3], radii, ref playerRes))
                            {
                                hitPlayer = true;
                            }
                        }
                    }
                    else
                    {
                        if (CollisionDetection.CheckCylinderOverlapSphere(BackPosition, Position,
                            player.Volume.SpherePosition, radii, ref playerRes))
                        {
                            hitPlayer = true;
                        }
                    }
                }
                else
                {
                    float minY = Fixed.ToFloat(player.Values.MinPickupHeight);
                    Vector3 playerBottom = player.Position.AddY(minY);
                    float dot = Fixed.ToFloat(player.Values.MaxPickupHeight) - minY;
                    if (CollisionDetection.CheckCylindersOverlap(BackPosition, Position, playerBottom, Vector3.UnitY,
                        dot, radii, ref playerRes))
                    {
                        hitPlayer = true;
                    }
                }
                if (hitPlayer && playerRes.Distance < minDist)
                {
                    minDist = playerRes.Distance;
                    anyRes = playerRes;
                    colWith = player;
                    noColEff = false;
                    hitHalfturret = false;
                }
                // todo?: else wifi check
                if (hasHalfturret && Owner != player.Halfturret)
                {
                    // btodo: collide with halfturret
                }
            }
            // btodo: collide with enemy beams
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
                    // btodo: handle collision with enemy beams
                    if (colWith.Type == EntityType.Player || colWith.Type == EntityType.Halfturret)
                    {
                        PlayerEntity player;
                        if (colWith.Type == EntityType.Player)
                        {
                            player = (PlayerEntity)colWith;
                        }
                        else
                        {
                            player = (PlayerEntity)colWith; // btodo: use halfturret owner
                        }
                        DamageFlags damageFlags = DamageFlags.NoDmgInvuln;
                        if (player.BeamEffectiveness[(int)Beam] == Effectiveness.Zero)
                        {
                            if (!_scene.Multiplayer && Owner == PlayerEntity.Main)
                            {
                                Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY, player.Position);
                                EffectEntry effect = _scene.SpawnEffectGetEntry(115, transform); // ineffectivePsycho
                                effect.SetReadOnlyField(0, 1); // radius
                                _scene.DetachEffectEntry(effect, setExpired: false);
                            }
                        }
                        else
                        {
                            if (hitHalfturret)
                            {
                                damageFlags |= DamageFlags.Halfturret;
                            }
                            Vector3 damageDir = GetDamageDirection(anyRes.Position, player.Position);
                            float damage = 0;
                            uint wholeDamage = 0;
                            bool isHeadshot = false;
                            if (!player.IsAltForm
                                && anyRes.Position.Y - player.Position.Y >= Fixed.ToFloat(player.Values.MaxPickupHeight) - 0.3f)
                            {
                                if (Beam == BeamType.Imperialist)
                                {
                                    isHeadshot = true;
                                }
                                else
                                {
                                    Vector3 travel = Position - SpawnPosition;
                                    isHeadshot = travel.LengthSquared <= 15 * 15;
                                }
                            }
                            if (isHeadshot)
                            {
                                if (MaxDistance > 0)
                                {
                                    float pct = Vector3.Distance(Position, SpawnPosition) / MaxDistance;
                                    damage = GetInterpolatedValue(DamageInterpolation, HeadshotDamage, 0, pct);
                                }
                                else
                                {
                                    damage = HeadshotDamage;
                                }
                                if (MathF.Abs(Damage - HeadshotDamage) > 1 / 4096f)
                                {
                                    damageFlags |= DamageFlags.Headshot;
                                }
                            }
                            else
                            {
                                if (MaxDistance > 0)
                                {
                                    float pct = Vector3.Distance(Position, SpawnPosition) / MaxDistance;
                                    damage = GetInterpolatedValue(DamageInterpolation, Damage, 0, pct);
                                }
                                else
                                {
                                    damage = Damage;
                                }
                            }
                            wholeDamage = (uint)Math.Clamp(damage, 0, Int32.MaxValue);
                            if (wholeDamage != 0 && (Beam != BeamType.ShockCoil || _scene.FrameCount % 2 == Parity)) // todo: FPS stuff
                            {
                                player.TakeDamage(wholeDamage, damageFlags, damageDir, this);
                            }
                            if (Flags.TestFlag(BeamFlags.LifeDrain) && Owner.Type == EntityType.Player)
                            {
                                var ownerPlayer = (PlayerEntity)Owner;
                                if (!ownerPlayer.IsPrimeHunter && ownerPlayer.TeamIndex != player.TeamIndex)
                                {
                                    // GainHealth checks if the player is alive
                                    player.GainHealth(wholeDamage);
                                }
                            }
                            if (!player.IsMainPlayer || player.IsAltForm || player.IsMorphing)
                            {
                                SpawnCollisionEffect(anyRes, noSplat: true);
                            }
                        }
                        OnCollision(anyRes, colWith);
                        // todo: update SFX
                        ricochet = false;
                    }
                    else if (colWith.Type == EntityType.EnemyInstance)
                    {
                        var enemy = (EnemyInstanceEntity)colWith;
                        if (enemy.GetEffectiveness(Beam) == Effectiveness.Zero && (enemy.EnemyType == EnemyType.FireSpawn
                            || (enemy.Owner as EnemyInstanceEntity)?.EnemyType == EnemyType.FireSpawn))
                        {
                            // when ineffective + FireSpawn or HitZone owned by FireSpawn
                            Vector3 facing = enemy.Transform.Row2.Xyz.Normalized();
                            float w = Vector3.Dot(facing, enemy.Position + facing * Fixed.ToFloat(0x3800));
                            anyRes.Plane = new Vector4(facing, w);
                            float dot = Vector3.Dot(Position, facing);
                            anyRes.Position = new Vector3(
                                Position.X + facing.X * (dot - w),
                                Position.Y + facing.Y * (dot - w),
                                Position.Z + facing.Z * (dot - w)
                            );
                            ProcessRicochet(anyRes);
                        }
                        else
                        {
                            float damage = Damage;
                            if (MaxDistance > 0)
                            {
                                float pct = Vector3.Distance(Position, SpawnPosition) / MaxDistance;
                                damage = GetInterpolatedValue(DamageInterpolation, Damage, 0, pct);
                            }
                            if (damage > 0)
                            {
                                if (Beam != BeamType.ShockCoil || _scene.FrameCount % 2 == Parity) // todo: FPS stuff
                                {
                                    enemy.TakeDamage((uint)damage, this);
                                    SpawnCollisionEffect(anyRes, noSplat: true);
                                }
                                OnCollision(anyRes, colWith);
                                // todo: update SFX
                            }
                        }
                        ricochet = false;
                    }
                    else if (colWith.Type == EntityType.Door)
                    {
                        var door = (DoorEntity)colWith;
                        SpawnCollisionEffect(anyRes, noSplat: true);
                        OnCollision(anyRes, colWith);
                        // todo: update SFX
                        if (Owner?.Type == EntityType.Player)
                        {
                            var player = (PlayerEntity)Owner;
                            if (player.IsMainPlayer || PlayerEntity.FreeCamera) // skdebug
                            {
                                if (door.Flags.TestFlag(DoorFlags.Locked) && !door.Flags.TestFlag(DoorFlags.ShowLock))
                                {
                                    if (door.Data.PaletteId == (int)Beam)
                                    {
                                        door.Unlock(updateState: true, sfxBool: true);
                                    }
                                    else if (!_scene.Multiplayer)
                                    {
                                        // todo: handle messages like this
                                        _scene.SendMessage(Message.ShowWarning, this, null, 40, 90 * 2, 5 * 2); // todo: FPS stuff
                                    }
                                }
                                // todo: don't do this if in room transition
                                door.Flags |= DoorFlags.ShotOpen;
                            }
                        }
                        ricochet = false;
                    }
                    else if (colWith.Type == EntityType.ForceField)
                    {
                        var forceField = (ForceFieldEntity)colWith;
                        if (!Flags.TestFlag(BeamFlags.Ricochet))
                        {
                            SpawnCollisionEffect(anyRes, noSplat: true);
                            OnCollision(anyRes, colWith);
                            // todo: update SFX
                            forceField.Lock?.LockHit(this);
                            ricochet = false;
                        }
                    }
                }
                else
                {
                    // collided with room, platform, or object collision
                    bool reflected = anyRes.Flags.TestFlag(CollisionFlags.ReflectBeams);
                    if (anyRes.EntityCollision != null)
                    {
                        _scene.SendMessage(Message.BeamCollideWith, this, anyRes.EntityCollision.Entity, anyRes, 0);
                        anyRes.EntityCollision.Entity.CheckBeamReflection(ref reflected);
                    }
                    if ((!Flags.TestFlag(BeamFlags.Ricochet) && !reflected)
                        || DrawFuncId == 8 || anyRes.Terrain >= Terrain.Acid)
                    {
                        if (!noColEff || Flags.TestFlag(BeamFlags.ForceEffect))
                        {
                            bool noSplat = anyRes.Terrain == Terrain.Lava || anyRes.EntityCollision != null;
                            SpawnCollisionEffect(anyRes, noSplat);
                        }
                        // todo: play SFX
                        OnCollision(anyRes, colWith: null);
                        ricochet = false;
                    }
                }
                if (ricochet)
                {
                    ProcessRicochet(anyRes);
                }
                if (DrawFuncId == 8)
                {
                    SpawnSniperBeam();
                }
            }
        }

        private void ProcessRicochet(CollisionResult colRes)
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
            float factor = 0.01f - (dot3 - colRes.Plane.W);
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
            if (_scene.FrameCount % 2 == 0)
            {
                for (int i = 9; i > 0; i--)
                {
                    PastPositions[i] = PastPositions[i - 1];
                }
                PastPositions[0] = Position;
            }
            // btodo: sfx
        }

        public void OnCollision(CollisionResult colRes, EntityBase? colWith)
        {
            if (Effect != null) // game also checks the HasModel flag, but it's either-or
            {
                _scene.DetachEffectEntry(Effect, setExpired: true);
                Effect = null;
            }
            if (SplashDamage > 0 && colRes.Terrain <= Terrain.Lava)
            {
                Debug.Assert(Equip != null);
                Debug.Assert(Owner != null);
                CheckSplashDamage(colWith);
                // skdebug
                if (Beam == BeamType.OmegaCannon)
                {
                    _scene.SetFade(FadeType.FadeInWhite, 15 * (1 / 30f), overwrite: false);
                }
                // note: when hitting halfturret, colWith has been replaced with the turret's owning player by this point
                if (RicochetWeapon != null && (colWith == null || colWith.Type != EntityType.Player))
                {
                    Vector3 factor = Velocity * 7;
                    float dot = Vector3.Dot(colRes.Plane.Xyz, factor);
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
                    Spawn(Owner, _ricochetEquip, colRes.Position, spawnDir, flags, _scene);
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

        private void CheckSplashDamage(EntityBase? colWith)
        {
            // todo: iterate and check players
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.EnemyInstance && entity != colWith)
                {
                    var enemy = (EnemyInstanceEntity)entity;
                    if (enemy.Flags.TestFlag(EnemyFlags.CollideBeam))
                    {
                        CollisionResult res = default;
                        float dist = Vector3.Distance(enemy.Position, Position);
                        if (dist < BeamScale
                            && !CollisionDetection.CheckBetweenPoints(Position, enemy.Position, TestFlags.AffectsBeams, _scene, ref res))
                        {
                            float damage = GetInterpolatedValue(SplashDamageType, SplashDamage, 0, dist / BeamScale);
                            enemy.TakeDamage((uint)damage, this);
                            if (Owner != null)
                            {
                                _scene.SendMessage(Message.Impact, this, Owner, enemy, 0);
                                // todo: stop beam SFX
                            }
                        }
                    }
                }
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

        public override void GetDrawInfo()
        {
            if (DrawFuncId == 0)
            {
                Draw00();
            }
            else if (DrawFuncId == 1)
            {
                Draw01();
            }
            else if (DrawFuncId == 2)
            {
                Draw02();
            }
            else if (DrawFuncId == 3)
            {
                Draw03();
            }
            else if (DrawFuncId == 6 || DrawFuncId == 12)
            {
                Draw06();
            }
            else if (DrawFuncId == 7)
            {
                Draw07();
            }
            else if (DrawFuncId == 9)
            {
                Draw09();
            }
            else if (DrawFuncId == 10)
            {
                Draw10();
            }
            else if (DrawFuncId == 17)
            {
                Draw17();
            }
        }

        // Power Beam
        private void Draw00()
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                _scene.AddSingleParticle(SingleType.Fuzzball, Position, Color, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail1(Fixed.ToFloat(122));
        }

        // uncharged Volt Driver
        private void Draw01()
        {
            DrawTrail1(Fixed.ToFloat(614));
        }

        // charged Volt Driver
        private void Draw02()
        {
            DrawTrail2(Fixed.ToFloat(1024), 5);
        }

        // non-affinity Judicator
        private void Draw03()
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                base.GetDrawInfo();
            }
            DrawTrail2(Fixed.ToFloat(204), 5);
        }

        // enemy tear/Judicator
        private void Draw06()
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                _scene.AddSingleParticle(SingleType.Fuzzball, Position, Vector3.One, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail3(Fixed.ToFloat(204));
        }

        // Missile
        private void Draw07()
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                _scene.AddSingleParticle(SingleType.Fuzzball, Position, Vector3.One, alpha: 1, scale: 1 / 4f);
            }
            DrawTrail2(Fixed.ToFloat(204), 5);
        }

        // Shock Coil
        private void Draw09()
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                if (Target != null)
                {
                    DrawTrail4(Fixed.ToFloat(614), 2048, 10);
                }
                else
                {
                    // todo: only draw if owner is main player
                    DrawTrail4(Fixed.ToFloat(102), 1433, 5);
                }
            }
        }

        // Battlehammer
        private void Draw10()
        {
            DrawTrail2(Fixed.ToFloat(81), 2);
        }

        // green energy beam
        private void Draw17()
        {
            if (!Flags.TestFlag(BeamFlags.Collided))
            {
                base.GetDrawInfo();
            }
        }

        private void DrawTrail1(float height)
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
            _scene.AddRenderItem(RenderItemType.TrailSingle, alpha, _scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(BackPosition), uvsAndVerts, _bindingId);
        }

        private void DrawTrail2(float height, int segments)
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
            _scene.AddRenderItem(RenderItemType.TrailMulti, alpha, _scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[0]), uvsAndVerts, _bindingId, trailCount: count);
        }

        private void DrawTrail3(float height)
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
            _scene.AddRenderItem(RenderItemType.TrailSingle, alpha, _scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[0]), uvsAndVerts, _bindingId);
        }

        private void DrawTrail4(float height, uint range, int segments)
        {
            Debug.Assert(_trailModel != null);
            if (segments < 2)
            {
                return;
            }
            int count = 4 * segments;

            int frames = (int)_scene.FrameCount / 2;
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
            _scene.AddRenderItem(RenderItemType.TrailMulti, alpha: 1, _scene.GetNextPolygonId(), Color, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(PastPositions[8]), uvsAndVerts, _bindingId, trailCount: count);
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

        public override void Destroy()
        {
            if (Effect != null)
            {
                _scene.DetachEffectEntry(Effect, setExpired: true);
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
                    if (beam.Flags.TestFlag(BeamFlags.Continuous) && beam.BeamKind == equip.Weapon.BeamKind
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
                if (beam.Flags.TestFlag(BeamFlags.Continuous) && beam.BeamKind == equip.Weapon.BeamKind
                    && beam.Owner == owner && beam.Lifespan < equip.Weapon.UnchargedLifespan)
                {
                    return beam;
                }
            }
            return equip.Beams[^1];
        }

        public static BeamResultFlags Spawn(EntityBase owner, EquipInfo equip, Vector3 position, Vector3 direction,
            BeamSpawnFlags spawnFlags, Scene scene)
        {
            BeamResultFlags result = BeamResultFlags.Spawned;
            WeaponInfo weapon = equip.Weapon;
            bool charged = false;
            float chargePct = 0;
            if (weapon.Flags.TestFlag(WeaponFlags.CanCharge))
            {
                // todo: FPS stuff
                if (weapon.Flags.TestFlag(WeaponFlags.PartialCharge))
                {
                    if (equip.ChargeLevel >= weapon.MinCharge * 2)
                    {
                        charged = true;
                        chargePct = (equip.ChargeLevel - weapon.MinCharge * 2) / (float)(weapon.FullCharge * 2 - weapon.MinCharge * 2);
                    }
                }
                else if (equip.ChargeLevel >= weapon.FullCharge * 2)
                {
                    charged = true;
                    chargePct = 1;
                }
            }
            float GetAmount(int unchargedAmt, int minChargeAmt, int fullChargeAmt)
            {
                return chargePct <= 0 ? unchargedAmt : minChargeAmt + ((fullChargeAmt - minChargeAmt) * chargePct);
            }
            int cost = (int)GetAmount(weapon.AmmoCost, weapon.MinChargeCost, weapon.ChargeCost);
            if (weapon.Flags.TestFlag(WeaponFlags.Continuous))
            {
                // btodo: Shock Coil frame stuff for ammo cost
            }
            int ammo = equip.GetAmmo?.Invoke() ?? -1;
            if (ammo >= 0 && cost > ammo)
            {
                return BeamResultFlags.NoSpawn;
            }
            equip.SetAmmo?.Invoke(ammo - cost);
            if (!spawnFlags.TestFlag(BeamSpawnFlags.NoMuzzle))
            {
                byte effectId = weapon.MuzzleEffects[charged ? 1 : 0];
                if (effectId != 255)
                {
                    Debug.Assert(effectId >= 3);
                    Vector3 effUp = direction;
                    Vector3 effFacing = GetCrossVector(effUp);
                    Matrix4 transform = GetTransformMatrix(effFacing, effUp);
                    transform.Row3.Xyz = position;
                    // the game does this by spawning a CBeamEffect, but that's unncessary for muzzle effects
                    scene.SpawnEffect(effectId - 3, transform);
                }
            }
            int projectiles = (int)GetAmount(weapon.Projectiles, weapon.MinChargeProjectiles, weapon.ChargedProjectiles);
            if (projectiles <= 0)
            {
                return result;
            }

            bool instantAoe = (charged && weapon.Flags.TestFlag(WeaponFlags.AoeCharged))
                || (!charged && weapon.Flags.TestFlag(WeaponFlags.AoeUncharged));

            BeamFlags flags = BeamFlags.None;
            float speed = GetAmount(weapon.UnchargedSpeed, weapon.MinChargeSpeed, weapon.ChargedSpeed) / 4096f;
            float finalSpeed = GetAmount(weapon.UnchargedFinalSpeed, weapon.MinChargeFinalSpeed, weapon.ChargedFinalSpeed) / 4096f;
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
                flags |= BeamFlags.LifeDrain;
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
            if (weapon.Beam == BeamType.Imperialist && !equip.Zoomed)
            {
                damage /= 2;
                hsDamage /= 2;
                splashDmg /= 2;
            }
            ushort damageInterpolation = weapon.DamageInterpolations[charged ? 1 : 0];
            float maxDist = GetAmount(weapon.UnchargedDistance, weapon.MinChargeDistance, weapon.ChargedDistance) / 4096f;
            Affliction afflictions = weapon.Afflictions[charged ? 1 : 0];
            float cylinderRadius = GetAmount(weapon.UnchargedCylRadius, weapon.MinChargeCylRadius, weapon.ChargedCylRadius) / 4096f;
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
            Vector3 dirVec = direction;
            Vector3 rightVec;
            if (dirVec.X != 0 || dirVec.Z != 0)
            {
                rightVec = new Vector3(dirVec.Z, 0, -dirVec.X).Normalized();
            }
            else
            {
                rightVec = Vector3.UnitX;
            }
            Vector3 upVec = Vector3.Cross(dirVec, rightVec).Normalized();
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
                    beam.OnCollision(colRes, colWith: null);
                }
                beam.Destroy();
                scene.RemoveEntity(beam);
                beam._models.Clear();
                if (!charged)
                {
                    equip.SmokeLevel += weapon.SmokeShotAmount;
                    if (equip.SmokeLevel > weapon.SmokeStart)
                    {
                        equip.SmokeLevel = weapon.SmokeStart;
                    }
                }
                beam.Owner = owner;
                beam.Beam = weapon.Beam;
                beam.BeamKind = weapon.BeamKind;
                beam.Flags = flags;
                beam.Age = 0;
                beam.Parity = scene.FrameCount % 2; // todo: FPS stuff
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
                beam.Direction = dirVec;
                beam.Right = rightVec;
                beam.Up = upVec;
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
                    beam.SpawnIceWave(weapon, chargePct);
                    // we don't actually "spawn" the beam projectile
                    beam.Velocity = beam.Acceleration = Vector3.Zero;
                    beam.Flags = BeamFlags.Collided;
                    beam.Lifespan = 0;
                    beam.Destroy();
                    return result;
                }
                if (maxSpread > 0)
                {
                    float angle1 = MathHelper.DegreesToRadians(Rng.GetRandomInt2((uint)maxSpread) / 4096f);
                    float angle2 = MathHelper.DegreesToRadians(Rng.GetRandomInt2(0x168000) / 4096f);
                    float sin1 = MathF.Sin(angle1);
                    float cos1 = MathF.Cos(angle1);
                    float sin2 = MathF.Sin(angle2);
                    float cos2 = MathF.Cos(angle2);
                    velocity.X = direction.X * cos1 + (beam.Up.X * cos2 + beam.Right.X * sin2) * sin1;
                    velocity.Y = direction.Y * cos1 + (beam.Up.Y * cos2 + beam.Right.Y * sin2) * sin1;
                    velocity.Z = direction.Z * cos1 + (beam.Up.Z * cos2 + beam.Right.Z * sin2) * sin1;
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
                        Vector3 effUp = beam.Direction;
                        Vector3 effFacing = GetCrossVector(effUp);
                        Matrix4 transform = GetTransformMatrix(effFacing, effUp);
                        transform.Row3.Xyz = beam.Position;
                        beam.Effect = scene.SpawnEffectGetEntry(effectId, transform);
                        beam.Effect.SetElementExtension(true);
                    }
                }
                Debug.Assert(beam.Target == null);
                if (beam.Flags.TestFlag(BeamFlags.Homing))
                {
                    if (CheckHomingTargets(beam, weapon, scene))
                    {
                        result |= BeamResultFlags.Homing;
                    }
                    if (beam.Beam == BeamType.ShockCoil && owner.Type == EntityType.Player)
                    {
                        var ownerPlayer = (PlayerEntity)owner;
                        if ((scene.Multiplayer || !ownerPlayer.IsBot) && ownerPlayer.ShockCoilTarget == beam.Target)
                        {
                            // todo: FPS stuff
                            ushort timer = ownerPlayer.ShockCoilTimer;
                            if (timer >= 120 * 2)
                            {
                                beam.Damage += 4;
                                beam.SplashDamage += 4;
                                beam.HeadshotDamage += 4;
                            }
                            else
                            {
                                beam.Damage += timer / (30 * 2);
                                beam.SplashDamage += timer / (30 * 2);
                                beam.HeadshotDamage += timer / (30 * 2);
                            }
                        }
                    }
                }
                scene.AddEntity(beam);
            }
            return result;
        }

        private static readonly IReadOnlyList<EntityType> _homingTargetTypes = new EntityType[5]
        {
            EntityType.Player,
            EntityType.Halfturret,
            EntityType.EnemyInstance,
            EntityType.Door,
            EntityType.Platform
        };

        private static bool CheckHomingTargets(BeamProjectileEntity beam, WeaponInfo weapon, Scene scene)
        {
            bool result = false;
            Debug.Assert(beam.Owner != null);
            float tolerance = Fixed.ToFloat(weapon.HomingTolerance);
            float curDiv = tolerance;
            for (int i = 0; i < _homingTargetTypes.Count; i++)
            {
                EntityType type = _homingTargetTypes[i];
                if (type == EntityType.EnemyInstance
                    && (beam.Owner.Type == EntityType.EnemyInstance || beam.Owner.Type == EntityType.Platform))
                {
                    continue;
                }
                for (int j = 0; j < scene.Entities.Count; j++)
                {
                    EntityBase entity = scene.Entities[j];
                    if (entity.Type != type || entity == beam.Owner || !entity.GetTargetable())
                    {
                        continue;
                    }
                    bool tryTarget = false;
                    if (type == EntityType.Player)
                    {
                        var player = (PlayerEntity)entity;
                        if (beam.Owner.Type != EntityType.Player)
                        {
                            tryTarget = true;
                        }
                        else
                        {
                            var ownerPlayer = (PlayerEntity)beam.Owner;
                            tryTarget = player.TeamIndex != ownerPlayer.TeamIndex;
                        }
                    }
                    else if (type == EntityType.Halfturret)
                    {
                        // btodo: check halfturret owner
                    }
                    else if (type == EntityType.EnemyInstance)
                    {
                        var enemy = (EnemyInstanceEntity)entity;
                        EnemyFlags flags = enemy.Flags;
                        if (flags.TestFlag(EnemyFlags.CollideBeam)
                            && (!flags.TestFlag(EnemyFlags.NoHomingNc) || beam.Flags.TestFlag(BeamFlags.Continuous))
                            && (!flags.TestFlag(EnemyFlags.NoHomingCo) || !beam.Flags.TestFlag(BeamFlags.Continuous)))
                        {
                            tryTarget = true;
                        }
                    }
                    else if (type == EntityType.Platform)
                    {
                        var platform = (PlatformEntity)entity;
                        tryTarget = platform.Flags.TestFlag(PlatformFlags.BeamTarget);
                    }
                    else // if (type == EntityType.Door)
                    {
                        tryTarget = true;
                    }
                    if (tryTarget)
                    {
                        entity.GetPosition(out Vector3 position);
                        Vector3 between = position - beam.Position;
                        float distSqr = Vector3.Dot(between, between);
                        float range = Fixed.ToFloat(weapon.HomingRange);
                        if ((weapon.Flags.TestFlag(WeaponFlags.Continuous) && beam.BeamKind == BeamType.Platform
                            || distSqr <= range * range) && distSqr > 0)
                        {
                            float dist = MathF.Sqrt(distSqr);
                            Debug.Assert(beam.Velocity != Vector3.Zero);
                            float dot = Vector3.Dot(between, beam.Velocity.Normalized());
                            float div1 = dot / dist;
                            if (div1 >= curDiv)
                            {
                                if (weapon.Flags.TestFlag(WeaponFlags.Continuous))
                                {
                                    bool canTarget = false;
                                    if (type == EntityType.Player)
                                    {
                                        var player = (PlayerEntity)entity;
                                        if (!player.IsAltForm && !player.IsMorphing || div1 >= Fixed.ToFloat(4006))
                                        {
                                            canTarget = true;
                                        }
                                    }
                                    else
                                    {
                                        canTarget = true;
                                    }
                                    if (canTarget)
                                    {
                                        float div2 = Math.Min(dist / range, 1);
                                        if (div1 >= tolerance + div2 * (Fixed.ToFloat(4094) - tolerance))
                                        {
                                            curDiv = div1;
                                            beam.Target = entity;
                                            result = true;
                                        }
                                    }
                                }
                                else
                                {
                                    curDiv = div1;
                                    beam.Target = entity;
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        private void SpawnIceWave(WeaponInfo weapon, float chargePct)
        {
            float angle = chargePct <= 0
                ? weapon.UnchargedSpread
                : weapon.MinChargeSpread + ((weapon.ChargedSpread - weapon.MinChargeSpread) * chargePct);
            angle /= 4096f;
            Debug.Assert(angle == 60);
            CheckIceWaveCollision(angle);
            Vector3 up = Direction;
            Vector3 facing;
            if (up.X != 0 || up.Z != 0)
            {
                var temp = Vector3.Cross(Vector3.UnitY, up);
                facing = Vector3.Cross(up, temp).Normalized();
            }
            else
            {
                var temp = Vector3.Cross(Vector3.UnitX, up);
                facing = Vector3.Cross(up, temp).Normalized();
            }
            Matrix4 transform = Matrix4.CreateScale(MaxDistance) * GetTransformMatrix(facing, up);
            transform.Row3.Xyz = Position;
            var ent = BeamEffectEntity.Create(new BeamEffectEntityData(type: 0, noSplat: false, transform), _scene);
            if (ent != null)
            {
                _scene.AddEntity(ent);
            }
        }

        // todo: visualize (also shadow freeze bug)
        private void CheckIceWaveCollision(float angle)
        {
            float angleCos = MathF.Cos(MathHelper.DegreesToRadians(angle));
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player || Owner == entity)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health == 0)
                {
                    continue;
                }
                // bug: the beam's up vector is factored out in order to do a lateral distance check (where "lateral" is relative to
                // the beam's orientation), but that same stripped vector is used for the angle check below. the result is that instead
                // of checking in a 60 degree cone, a 60 degree wedge of a cylinder with infinite height is checked (again, where
                // "height" is rleative to the beam) -- this results in the shadow freeze glitch
                // fix: use the normalized between vector with all its components in the dot product check
                Vector3 between = player.Position - Position;
                float dot = Vector3.Dot(between, Up);
                between += Up * -dot;
                float mag = between.Length;
                if (mag < MaxDistance)
                {
                    between /= mag;
                    if (Vector3.Dot(between, Direction) > angleCos)
                    {
                        Vector3 dir = GetDamageDirection(Position, player.Position);
                        player.TakeDamage((int)Damage, DamageFlags.NoDmgInvuln, dir, this);
                    }
                }
                if (player.Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    // btodo: check for collision with halfturret
                }
            }
        }

        private Vector3 GetDamageDirection(Vector3 beamPos, Vector3 targetPos)
        {
            if (DamageDirType == 1)
            {
                // multiply velocity (or unit Y if not moving) by magnitude -- unused?
                Vector3 direction = Vector3.UnitY;
                if (Velocity != Vector3.Zero)
                {
                    direction = Velocity.Normalized();
                }
                return direction * DamageDirMag;
            }
            if (DamageDirType == 2)
            {
                // normalize vector between, halve Y, minimum 0.03 Y (or 0.03 Y if not moving), multiply by magnitude
                Vector3 direction = targetPos - beamPos;
                if (direction != Vector3.Zero)
                {
                    direction = direction.Normalized();
                    direction.Y /= 2;
                    if (direction.Y < 0.03f)
                    {
                        direction.Y = 0.03f;
                    }
                }
                else
                {
                    direction = new Vector3(0, 0.03f, 0);
                }
                return direction * DamageDirMag;
            }
            if (DamageDirType == 3)
            {
                // normalize horizontal vector between, multiply by magnitude
                Vector3 direction = (targetPos - beamPos).WithY(0);
                if (direction != Vector3.Zero)
                {
                    direction = direction.Normalized();
                    direction *= DamageDirMag;
                }
                return direction;
            }
            if (DamageDirType == 4)
            {
                //unit Y multiplied by magnitude -- unused?
                return new Vector3(0, DamageDirMag, 0);
            }
            return Vector3.Zero;
        }

        private void SpawnCollisionEffect(CollisionResult colRes, bool noSplat)
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
                Vector3 up = Beam == BeamType.Imperialist ? -Direction : colRes.Plane.Xyz;
                if (colRes.EntityCollision != null)
                {
                    spawnPos = Matrix.Vec3MultMtx4(spawnPos, colRes.EntityCollision.Inverse1);
                    up = Matrix.Vec3MultMtx3(up, colRes.EntityCollision.Inverse1);
                }
                Vector3 facing = GetCrossVector(up);
                Matrix4 transform = GetTransformMatrix(facing, up);
                transform.Row3.Xyz = spawnPos;
                // the game uses BeamKind against "511" bits which accomplish the same thing as this terrain type check
                if (_scene.GameMode != GameMode.SinglePlayer || colRes.Terrain <= Terrain.Lava)
                {
                    var ent = BeamEffectEntity.Create(
                        new BeamEffectEntityData(CollisionEffect, noSplat, transform, colRes.EntityCollision), _scene);
                    if (ent != null)
                    {
                        if (SplashDamage > 0)
                        {
                            ent.Scale = Scale;
                        }
                        _scene.AddEntity(ent);
                    }
                }
                // there are actually effect IDs to cover platform/enemy beams in these arrays (although most are 255)
                byte splatEffect = _terSplat1P[(int)BeamKind][(int)colRes.Terrain];
                if (_scene.GameMode == GameMode.SinglePlayer && splatEffect != 255)
                {
                    splatEffect += 3;
                    var ent = BeamEffectEntity.Create(
                        new BeamEffectEntityData(splatEffect, noSplat, transform, colRes.EntityCollision), _scene);
                    if (ent != null)
                    {
                        _scene.AddEntity(ent);
                    }
                }
            }
        }

        public void SpawnDamageEffect(Effectiveness effectiveness)
        {
            if (effectiveness == Effectiveness.Normal || effectiveness == Effectiveness.Double)
            {
                int effectId = 0;
                Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
                transform.Row3.Xyz = Position;
                if (effectiveness == Effectiveness.Double)
                {
                    // 20 - sprEffectivePB
                    // 21 - sprEffectiveElectric
                    // 22 - sprEffectiveMsl
                    // 23 - sprEffectiveJack
                    // 24 - sprEffectiveSniper
                    // 25 - sprEffectiveIce
                    // 26 - sprEffectiveMortar
                    // 27 - sprEffectiveGhost
                    effectId = (int)Beam + 20;
                }
                else if (!_scene.Multiplayer)
                {
                    // 12 - effectiveHitPB
                    // 13 - effectiveHitElectric
                    // 14 - effectiveHitMsl
                    // 15 - effectiveHitJack
                    // 16 - effectiveHitSniper
                    // 17 - effectiveHitIce
                    // 18 - effectiveHitMortar
                    // 19 - effectiveHitGhost
                    effectId = (int)Beam + 12;
                }
                else
                {
                    // 154 - mpEffectivePB
                    // 155 - mpEffectiveElectric
                    // 156 - mpEffectiveMsl
                    // 157 - mpEffectiveJack
                    // 158 - mpEffectiveSniper
                    // 159 - mpEffectiveIce
                    // 160 - mpEffectiveMortar
                    // 161 - mpEffectiveGhost
                    effectId = (int)Beam + 154;
                }
                if (effectId > 0)
                {
                    _scene.SpawnEffect(effectId, transform);
                }
            }
        }

        private static readonly IReadOnlyList<IReadOnlyList<byte>> _terSplat1P
            = new List<IReadOnlyList<byte>>()
            {
                // metal, orange holo, green holo, blue holo, ice, snow, sand, rock, lava, acid, Gorea, unknown
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 }, // Power Beam
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 }, // Volt Driver
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 }, // Missile
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 }, // Battlehammer
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 }, // Imperialist
                new List<byte>() { 255, 99, 121, 122, 123, 126, 125, 124, 100, 142, 141, 140 }, // Judicator
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 }, // Magmaul
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 }, // Shock Coil
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 }, // Omega Cannon
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 }, // Platform
                new List<byte>() { 255, 255, 255, 255, 255, 255, 255, 255, 255, 142, 141, 140 } // Enemy
            };

        private void SpawnSniperBeam()
        {
            // following what the game does, but this should always be the same as SpawnPosition
            Vector3 spawnPos = PastPositions[8];
            Vector3 up = Position - spawnPos;
            float magnitude = up.Length;
            if (magnitude > 0)
            {
                up.Normalize();
                Vector3 facing = GetCrossVector(up);
                Matrix4 transform = GetTransformMatrix(facing, up);
                transform.Row3.Xyz = spawnPos;
                var ent = BeamEffectEntity.Create(new BeamEffectEntityData(type: 1, noSplat: false, transform), _scene);
                if (ent != null)
                {
                    ent.Scale = new Vector3(1, magnitude, 1);
                    _scene.AddEntity(ent);
                }
            }
        }

        private static Vector3 GetCrossVector(Vector3 up)
        {
            if (up.Z <= Fixed.ToFloat(-3686) || up.Z >= Fixed.ToFloat(3686))
            {
                return Vector3.Cross(Vector3.UnitX, up).Normalized();
            }
            return Vector3.Cross(Vector3.UnitZ, up).Normalized();
        }

        public static void StopChargeSfx(BeamType beam, Hunter hunter)
        {
            //  todo: SFX stuff
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

    [Flags]
    public enum BeamResultFlags : byte
    {
        NoSpawn = 0x0,
        Spawned = 0x1,
        Homing = 0x2 // for continuous only
    }
}
