using System;
using System.Buffers;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Entities.Enemies;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class BombEntity : EntityBase
    {
        public BombFlags Flags { get; private set; }
        public PlayerEntity Owner { get; private set; } = null!;
        public BombType BombType { get; private set; }
        public int BombIndex { get; set; }

        private EntityBase? _target = null;
        private Vector3 _speed = Vector3.Zero;

        public int Countdown { get; set; }
        public float Radius { get; set; }
        public float SelfRadius { get; set; }
        public ushort Damage { get; set; }
        public ushort EnemyDamage { get; set; }

        public EffectEntry? Effect { get; private set; }
        private ModelInstance? _trailModel = null;
        private int _bindingId = 0;

        public BombEntity(Scene scene) : base(EntityType.Bomb, scene)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            int effectId = 0;
            if (BombType == BombType.Stinglarva)
            {
                SetUpModel("KandenAlt_TailBomb");
                Flags |= BombFlags.HasModel;
                Countdown = 43 * 2; // todo: FPS stuff
            }
            else if (BombType == BombType.Lockjaw)
            {
                if (Recolor == 0)
                {
                    _trailModel = Read.GetModelInstance("arcWelder");
                }
                else
                {
                    _trailModel = Read.GetModelInstance("arcWelder1");
                }
                Countdown = 900 * 2;
                // bombStartSylux, bombStartSyluxR, bombStartSyluxP, bombStartSyluxW, bombStartSyluxO, or bombStartSyluxG
                effectId = Metadata.SyluxBombEffects[Recolor];
                if (Owner.SyluxBombCount == 1)
                {
                    CollisionResult colRes = default;
                    BombEntity firstBomb = Owner.SyluxBombs[0]!;
                    Vector3 between = firstBomb.Position - Position;
                    if (between.LengthSquared >= 100 || CollisionDetection.CheckBetweenPoints(firstBomb.Position, Position,
                        TestFlags.Players, _scene, ref colRes))
                    {
                        Countdown = 1;
                        firstBomb.Countdown = 1;
                    }
                }
            }
            else if (BombType == BombType.MorphBall)
            {
                Countdown = 43 * 2;
                effectId = _scene.Multiplayer && PlayerEntity.PlayerCount > 2 ? 119 : 9; // bombStartMP or bombStart
            }
            if (effectId != 0)
            {
                Effect = _scene.SpawnEffectGetEntry(effectId, Transform);
                Effect?.SetElementExtension(true);
            }
            if (_trailModel != null)
            {
                int recolor = Recolor;
                if (Recolor > 0)
                {
                    recolor--;
                }
                Material material = _trailModel.Model.Materials[0];
                _bindingId = _scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, recolor);
            }
        }

        public override bool Process()
        {
            EntityBase? hitEntity = null;
            _soundSource.Update(Position, rangeIndex: 5);
            // sfxtodo: if node ref is not active, set sound volume override to 0
            if (Countdown > 0)
            {
                Countdown--;
            }
            if (Countdown == 0)
            {
                Flags |= BombFlags.Exploding;
            }
            if (!Flags.TestFlag(BombFlags.Exploded))
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (player == Owner || player.Health == 0 || player.TeamIndex == Owner.TeamIndex)
                    {
                        continue;
                    }
                    if (player.CheckHitByBomb(this, halfturret: false))
                    {
                        hitEntity = player;
                        Flags |= BombFlags.Exploding;
                    }
                    if (player.Flags2.TestFlag(PlayerFlags2.Halfturret) && player.CheckHitByBomb(this, halfturret: true))
                    {
                        hitEntity = player;
                        Flags |= BombFlags.Exploding;
                    }
                    if (_target != null)
                    {
                        continue;
                    }
                    if (BombType == BombType.Lockjaw)
                    {
                        LockjawCheckTargeting(player, ref hitEntity);
                    }
                    else if (BombType == BombType.Stinglarva)
                    {
                        Vector3 between = player.Position - Position;
                        if (between.LengthSquared < 5 * 5)
                        {
                            _target = player;
                            _speed = FacingVector * 0.3f;
                        }
                    }
                }
                if (BombType == BombType.Stinglarva && _target == null)
                {
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Halfturret)
                        {
                            continue;
                        }
                        var halfturret = (HalfturretEntity)entity;
                        Vector3 between = halfturret.Position - Position;
                        if (between.LengthSquared < 5 * 5)
                        {
                            _target = halfturret;
                            _speed = FacingVector * 0.3f;
                        }
                    }
                }
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.EnemyInstance)
                    {
                        continue;
                    }
                    var enemy = (EnemyInstanceEntity)entity;
                    if (enemy.Flags.TestFlag(EnemyFlags.CollideBeam) && (enemy.EnemyType != EnemyType.Temroid || enemy.StateA != 8)
                        && enemy.CheckHitByBomb(this))
                    {
                        hitEntity = enemy;
                        Flags |= BombFlags.Exploding;
                    }
                }
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.EnemyInstance)
                    {
                        continue;
                    }
                    var enemy = (EnemyInstanceEntity)entity;
                    if (enemy.Flags.TestFlag(EnemyFlags.CollideBeam) && enemy.EnemyType == EnemyType.Temroid && enemy.StateA == 8
                        && ((Enemy02Entity)enemy).CheckTemroidHitByBomb(this))
                    {
                        hitEntity = enemy;
                    }
                }
                if (Owner.IsAltForm)
                {
                    Owner.CheckHitByBomb(this, halfturret: false);
                }
                if (Flags.TestFlag(BombFlags.Exploding))
                {
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Door)
                        {
                            continue;
                        }
                        var door = (DoorEntity)entity;
                        Vector3 doorFacing = door.FacingVector;
                        Vector3 between = Position - door.LockPosition;
                        float dot = Vector3.Dot(doorFacing, between);
                        float radius = SelfRadius + 0.4f;
                        if (dot < radius && dot > -radius)
                        {
                            between -= doorFacing * dot;
                            if (between.LengthSquared <= door.RadiusSquared)
                            {
                                if (door.Flags.TestFlag(DoorFlags.Locked) && door.Data.PaletteId == 8)
                                {
                                    door.Unlock(updateState: true, sfxBool: true);
                                }
                                door.Flags |= DoorFlags.ShotOpen;
                            }
                        }
                    }
                }
                if (BombType == BombType.Lockjaw && BombIndex == 0 && Owner.SyluxBombCount == 3
                    && _target == null && hitEntity == null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        BombEntity? bomb = Owner.SyluxBombs[i];
                        Debug.Assert(bomb != null);
                        bomb.Countdown = 1;
                        bomb._target = Owner;
                    }
                }
            }
            if (_target != null)
            {
                if (_target.GetTargetable())
                {
                    ProcessTargeting();
                }
                else
                {
                    _target = null;
                }
            }
            if (BombType == BombType.Lockjaw)
            {
                if (Owner.Health == 0)
                {
                    Flags |= BombFlags.Exploding;
                    Countdown = 0;
                }
                if (hitEntity != null)
                {
                    for (int i = 0; i < Owner.SyluxBombCount; i++)
                    {
                        BombEntity? bomb = Owner.SyluxBombs[i];
                        Debug.Assert(bomb != null);
                        bomb._target = hitEntity;
                        if (bomb.Countdown > 22 * 2) // the game compares against 22.5
                        {
                            bomb.Countdown = 22 * 2; // todo: FPS stuff
                        }
                    }
                }
            }
            if (Flags.TestFlag(BombFlags.Exploding))
            {
                Flags &= ~BombFlags.Exploding;
                if (Flags.TestFlag(BombFlags.Exploded))
                {
                    return false;
                }
                Flags |= BombFlags.Exploded;
                _target = null;
                _models.Clear();
                if (Effect != null)
                {
                    _scene.UnlinkEffectEntry(Effect);
                    Effect = null;
                }
                if (BombType == BombType.Stinglarva)
                {
                    _scene.SpawnEffect(128, Transform); // bombKanden
                }
                else if (BombType == BombType.Lockjaw)
                {
                    _scene.SpawnEffect(146, Transform); // bombSylux
                }
                else if (BombType == BombType.MorphBall)
                {
                    _scene.SpawnEffect(145, Transform); // bombBlue
                }
                Countdown = 0;
                _soundSource.StopSfx(SfxId.KANDEN_ALT_ATTACK);
                _soundSource.StopSfx(SfxId.MORPH_BALL_BOMB_PLACE);
                _soundSource.PlaySfx(SfxId.MORPH_BALL_BOMB);
                if (hitEntity == null)
                {
                    // if an entity was hit, this message was sent elsewhere
                    _scene.SendMessage(Message.Impact, this, Owner, 0, 0); // the game doesn't set anything as sender
                }
            }
            if (Effect != null)
            {
                Effect.Transform(Position, Transform);
            }
            return base.Process();
        }

        // todo: visualize
        private void LockjawCheckTargeting(PlayerEntity player, ref EntityBase? hitEntity)
        {
            Vector3 targetPos;
            if (player.IsAltForm)
            {
                targetPos = player.Volume.SpherePosition;
            }
            else
            {
                targetPos = player.Position.AddY(Fixed.ToFloat(player.Values.MinPickupHeight));
            }
            float cylHeight = Fixed.ToFloat(player.Values.MaxPickupHeight) - Fixed.ToFloat(player.Values.MinPickupHeight);
            CollisionResult discard = default;
            bool lineHitPlayer = false;
            bool lineHitHalfturret = false;
            if (BombIndex == 1)
            {
                BombEntity? bombZero = Owner.SyluxBombs[0];
                Debug.Assert(bombZero != null);
                if (player.IsAltForm && CollisionDetection.CheckCylinderOverlapSphere(Position, bombZero.Position,
                        targetPos, player.Volume.SphereRadius, ref discard))
                {
                    // alt form between b1 and b0
                    lineHitPlayer = true;
                }
                else if (!player.IsAltForm && CollisionDetection.CheckCylindersOverlap(Position, bombZero.Position,
                        targetPos, Vector3.UnitY, cylHeight, player.Volume.SphereRadius, ref discard))
                {
                    // biped between b1 and b0
                    lineHitPlayer = true;
                }
                else if (player.Flags2.TestFlag(PlayerFlags2.Halfturret) && CollisionDetection
                    .CheckCylinderOverlapSphere(Position, bombZero.Position, player.Halfturret.Position, 0.45f, ref discard))
                {
                    // halfturret between b1 and b0
                    lineHitHalfturret = true;
                }
            }
            else if (BombIndex == 2)
            {
                BombEntity? bombZero = Owner.SyluxBombs[0];
                BombEntity? bombOne = Owner.SyluxBombs[1];
                Debug.Assert(bombZero != null);
                Debug.Assert(bombOne != null);
                if (player.IsAltForm && CollisionDetection.CheckCylinderOverlapSphere(Position, bombZero.Position,
                        targetPos, player.Volume.SphereRadius, ref discard))
                {
                    // alt form between b2 and b0
                    lineHitPlayer = true;
                }
                else if (!player.IsAltForm && CollisionDetection.CheckCylindersOverlap(Position, bombZero.Position,
                        targetPos, Vector3.UnitY, cylHeight, player.Volume.SphereRadius, ref discard))
                {
                    // biped between b2 and b0
                    lineHitPlayer = true;
                }
                if (player.IsAltForm && CollisionDetection.CheckCylinderOverlapSphere(Position, bombOne.Position,
                        targetPos, player.Volume.SphereRadius, ref discard))
                {
                    // alt form between b2 and b1
                    lineHitPlayer = true;
                }
                else if (!player.IsAltForm && CollisionDetection.CheckCylindersOverlap(Position, bombOne.Position,
                        targetPos, Vector3.UnitY, cylHeight, player.Volume.SphereRadius, ref discard))
                {
                    // biped between b2 and b1
                    lineHitPlayer = true;
                }
                else if (player.Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    // halfturret between b2 and b0
                    if (CollisionDetection.CheckCylinderOverlapSphere(Position, bombZero.Position,
                        player.Halfturret.Position, 0.45f, ref discard))
                    {
                        // halfturret between b2 and b0
                        lineHitHalfturret = true;
                    }
                    else if (CollisionDetection.CheckCylinderOverlapSphere(Position, bombOne.Position,
                        player.Halfturret.Position, 0.45f, ref discard))
                    {
                        // halfturret between b2 and b1
                        lineHitHalfturret = true;
                    }
                }
            }
            else if (Owner.SyluxBombCount == 3)
            {
                Debug.Assert(BombIndex == 0);
                if (LockjawCheckSnare(player.Position))
                {
                    // player in snare
                    hitEntity = player;
                    for (int i = 0; i < Owner.SyluxBombCount; i++)
                    {
                        BombEntity? bomb = Owner.SyluxBombs[i];
                        Debug.Assert(bomb != null);
                        // todo: if 1P bot and encounter state, use alternate damage value
                        // else...
                        bomb.Damage = 60;
                        bomb.EnemyDamage = 60;
                    }
                }
                else if (player.Flags2.TestFlag(PlayerFlags2.Halfturret) && LockjawCheckSnare(player.Halfturret.Position))
                {
                    // halfturret in snare
                    hitEntity = player.Halfturret;
                    for (int i = 0; i < Owner.SyluxBombCount; i++)
                    {
                        BombEntity? bomb = Owner.SyluxBombs[i];
                        Debug.Assert(bomb != null);
                        bomb.Damage = 60;
                        bomb.EnemyDamage = 60;
                    }
                }
                return;
            }
            if (lineHitPlayer)
            {
                Debug.Assert(!lineHitHalfturret);
                hitEntity = player;
                // todo: if 1P bot and encounter state, use alternate damage value
                // else...
                player.TakeDamage(20, DamageFlags.NoDmgInvuln, null, this);
            }
            else if (lineHitHalfturret)
            {
                hitEntity = player.Halfturret;
                player.TakeDamage(20, DamageFlags.NoDmgInvuln | DamageFlags.Halfturret, null, this);
            }
        }

        private bool LockjawCheckSnare(Vector3 position)
        {
            BombEntity? bombZero = Owner.SyluxBombs[0];
            BombEntity? bombOne = Owner.SyluxBombs[1];
            BombEntity? bombTwo = Owner.SyluxBombs[2];
            Debug.Assert(bombZero != null);
            Debug.Assert(bombOne != null);
            Debug.Assert(bombTwo != null);
            Vector3 zeroToOne = bombOne.Position - bombZero.Position;
            Vector3 oneToTwo = bombTwo.Position - bombOne.Position;
            Vector3 cross1 = Vector3.Cross(oneToTwo, zeroToOne).Normalized();
            Vector3 zeroToPosition = position - bombZero.Position;
            float dot = Vector3.Dot(cross1, zeroToPosition);
            if (dot > -0.75f && dot < 0.75f)
            {
                var cross2 = Vector3.Cross(zeroToPosition, zeroToOne);
                if (Vector3.Dot(cross2, cross1) > 0)
                {
                    Vector3 oneToPosition = position - bombOne.Position;
                    var cross3 = Vector3.Cross(oneToPosition, oneToTwo);
                    if (Vector3.Dot(cross3, cross1) > 0)
                    {
                        Vector3 twoToPosition = position - bombTwo.Position;
                        Vector3 twoToZero = bombZero.Position - bombTwo.Position;
                        var cross4 = Vector3.Cross(twoToPosition, twoToZero);
                        if (Vector3.Dot(cross4, cross1) > 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void ProcessTargeting()
        {
            Debug.Assert(_target != null);
            _target.GetPosition(out Vector3 targetPos);
            Vector3 prevPos = Position;
            Vector3 newSpeed;
            if (BombType == BombType.Lockjaw)
            {
                Vector3 between = targetPos - Position;
                float magSqr = between.LengthSquared;
                if (magSqr > 0)
                {
                    between /= MathF.Sqrt(magSqr);
                }
                newSpeed = _speed + (between - _speed) * 0.15f;
            }
            else
            {
                Debug.Assert(BombType == BombType.Stinglarva);
                Vector3 between = (targetPos - Position).WithY(0);
                float hMagSqr = between.X * between.X + between.Z * between.Z;
                if (hMagSqr > 0)
                {
                    between /= MathF.Sqrt(hMagSqr);
                }
                float deltaX = (between.X - _speed.X) * 0.05f;
                float deltaZ = (between.Z - _speed.Z) * 0.05f;
                newSpeed = new Vector3(_speed.X + deltaX, _speed.Y - 0.05f, _speed.Z + deltaZ);
            }
            _speed += (newSpeed - _speed) / 2; // todo: FPS stuff
            Position += _speed / 2; // todo: FPS stuff
            var results = new CollisionResult[8];
            int count = CollisionDetection.CheckSphereBetweenPoints(prevPos, Position, 0.4f, limit: 8,
                includeOffset: false, TestFlags.None, _scene, results);
            for (int i = 0; i < count; i++)
            {
                CollisionResult result = results[i];
                Debug.Assert(result.Field0 == 0);
                float dotw = result.Plane.W - Vector3.Dot(Position, result.Plane.Xyz) + 0.4f;
                if (dotw > 0)
                {
                    Position += result.Plane.Xyz * dotw;
                    float dot = Vector3.Dot(_speed / 2, result.Plane.Xyz); // todo: FPS stuff
                    if (dot < 0)
                    {
                        _speed += result.Plane.Xyz * -dot;
                    }
                }
            }
            if (BombType != BombType.Lockjaw && (_speed.X != 0 || _speed.Z != 0))
            {
                SetTransform(_speed.Normalized(), UpVector, Position);
            }
        }

        public override void GetDrawInfo()
        {
            // todo: is_visible
            if (BombType == BombType.Lockjaw)
            {
                if (BombIndex == 1)
                {
                    DrawLockjawTrail(Position, Owner.SyluxBombs[0]!.Position, Fixed.ToFloat(614), 10);
                }
                else if (BombIndex == 2)
                {
                    DrawLockjawTrail(Position, Owner.SyluxBombs[1]!.Position, Fixed.ToFloat(614), 10);
                    DrawLockjawTrail(Position, Owner.SyluxBombs[0]!.Position, Fixed.ToFloat(614), 10);
                }
            }
            base.GetDrawInfo();
        }

        private void DrawLockjawTrail(Vector3 point1, Vector3 point2, float height, int segments)
        {
            Debug.Assert(_trailModel != null);
            if (segments < 2)
            {
                return;
            }
            int count = 4 * segments;
            int recolor = Recolor;
            if (Recolor > 0)
            {
                recolor--;
            }
            Vector3 vec = point2 - point1;
            Texture texture = _trailModel.Model.Recolors[recolor].Textures[0];
            float uvT = (texture.Height - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(count);
            for (int i = 0; i < segments; i++)
            {
                float uvS = 0;
                if (i > 0)
                {
                    uvS = (texture.Width / (float)(segments - 1) * i - (1 / 16f)) / texture.Width;
                }
                float pct = i * (1f / (segments - 1));
                float x = vec.X * pct;
                float y = vec.Y * pct;
                float z = vec.Z * pct;
                if (i > 0 && i < segments - 1)
                {
                    x += Rng.GetRandomInt1(0x800) / 4096f - 0.25f;
                    y += Rng.GetRandomInt1(0x800) / 4096f - 0.25f;
                    z += Rng.GetRandomInt1(0x800) / 4096f - 0.25f;
                }
                uvsAndVerts[4 * i] = new Vector3(uvS, 0, 0);
                uvsAndVerts[4 * i + 1] = new Vector3(x, y - height, z);
                uvsAndVerts[4 * i + 2] = new Vector3(uvS, uvT, 0);
                uvsAndVerts[4 * i + 3] = new Vector3(x, y + height, z);
            }
            Material material = _trailModel.Model.Materials[0];
            _scene.AddRenderItem(RenderItemType.TrailMulti, alpha: 1, _scene.GetNextPolygonId(), Vector3.One, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(point1), uvsAndVerts, _bindingId, trailCount: count);
        }

        public override void Destroy()
        {
            // todo: SFX and stuff
            if (BombType == BombType.Lockjaw)
            {
                for (int i = BombIndex; i < Owner.SyluxBombCount - 1; i++)
                {
                    BombEntity? bomb = Owner.SyluxBombs[i + 1];
                    Debug.Assert(bomb != null);
                    Owner.SyluxBombs[i] = bomb;
                    bomb.BombIndex = i;
                }
                Owner.SyluxBombCount--;
            }
            _models.Clear();
            _trailModel = null;
            if (Effect != null)
            {
                _scene.UnlinkEffectEntry(Effect);
            }
            Effect = null;
            Owner = null!;
            _scene.UnlinkBomb(this);
        }

        public void PlaySpawnSfx()
        {
            _soundSource.Update(Position, rangeIndex: 5);
            // sfxtodo: if node ref is not active, set sound volume override to 0
            SfxId sfx = BombType == BombType.Stinglarva ? SfxId.KANDEN_ALT_ATTACK : SfxId.MORPH_BALL_BOMB_PLACE;
            _soundSource.PlaySfx(sfx);
        }

        public static BombEntity? Spawn(PlayerEntity owner, Matrix4 transform, Scene scene)
        {
            BombType type = BombType.MorphBall;
            if (owner.Hunter == Hunter.Kanden)
            {
                type = BombType.Stinglarva;
            }
            else if (owner.Hunter == Hunter.Sylux)
            {
                type = BombType.Lockjaw;
            }
            BombEntity? bomb = scene.InitBomb();
            if (bomb == null)
            {
                Debug.Assert(false, "Failed to spawn bomb");
                return null;
            }
            bomb.Owner = owner;
            bomb.BombType = type;
            bomb.Transform = transform;
            bomb.Recolor = owner.Recolor;
            bomb.Flags = BombFlags.None;
            bomb.NodeRef = NodeRef.None;
            scene.AddEntity(bomb);
            return bomb;
        }
    }

    [Flags]
    public enum BombFlags : byte
    {
        None = 0x0,
        Exploding = 0x1,
        Exploded = 0x2,
        HasModel = 0x4
    }
}
