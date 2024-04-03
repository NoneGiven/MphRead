using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy30Entity : GoreaEnemyEntityBase
    {
        private readonly EnemySpawnEntity _spawner = null!;
        private int _index = 0;
        private int _field184 = 0;
        private int _field186 = 0;

        public Enemy30Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected override void EnemyInitialize()
        {
            InitializeCommon(_spawner);
            Flags |= EnemyFlags.OnRadar;
            Flags &= ~EnemyFlags.NoHomingCo;
            _health = 15;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, 1);
            SetUpModel("PowerBomb");
            _owner = null;
        }

        protected override void EnemyProcess()
        {
            if (Flags.TestFlag(EnemyFlags.Visible))
            {
                if (_health > 0 && HitPlayers[PlayerEntity.Main.SlotIndex])
                {
                    DieAndSpawnEffect(164); // goreaCrystalHit
                }
                if (_health > 0)
                {
                    CollisionResult discard = default;
                    Vector3 travel = _prevPos - Position;
                    if (travel.LengthSquared > 1 / 128f
                        && CollisionDetection.CheckBetweenPoints(_prevPos, Position, TestFlags.Beams, _scene, ref discard))
                    {
                        DieAndSpawnEffect(164); // goreaCrystalHit
                    }
                }
            }
        }

        private void DieAndSpawnEffect(int effectId)
        {
            SpawnEffect(effectId, Position);
            Vector3 between = PlayerEntity.Main.Position - Position;
            float distance = between.Length;
            if (distance < 2) // 8192
            {
                CollisionResult discard = default;
                var limitMin = new Vector3(Position.X - 2, Position.Y - 2, Position.Z - 2);
                var limitMax = new Vector3(Position.X + 2, Position.Y + 2, Position.Z + 2);
                IReadOnlyList<CollisionCandidate> candidates
                    = CollisionDetection.GetCandidatesForLimits(null, Vector3.Zero, 0, limitMin, limitMax, includeEntities: false, _scene);
                if (!CollisionDetection.CheckBetweenPoints(candidates, _prevPos, Position, TestFlags.Beams, _scene, ref discard))
                {
                    int damage = 15;
                    float force = 1;
                    if (!HitPlayers[PlayerEntity.Main.SlotIndex])
                    {
                        float factor = Math.Clamp(distance / 2, 0, 1);
                        damage -= (int)MathF.Round(damage - 15 * factor);
                        force -= factor;
                    }
                    if (distance > 1 / 128f)
                    {
                        between = between.Normalized() * force;
                    }
                    else
                    {
                        between = Vector3.UnitY * force;
                    }
                    PlayerEntity.Main.TakeDamage(damage, DamageFlags.NoDmgInvuln, between, this);
                }
            }
            _soundSource.PlaySfx(SfxId.GOREA_ATTACK3B, sourceOnly: true);
            _health = 0;
            Flags &= ~EnemyFlags.Visible;
            Flags &= ~EnemyFlags.CollidePlayer;
            Flags &= ~EnemyFlags.CollideBeam;
            Flags &= ~EnemyFlags.OnRadar;
            Flags |= EnemyFlags.Invincible;
            Position = Position.WithY(524288); // 0x7FFFFFFF
            _speed = Vector3.Zero;
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health == 0)
            {
                if (_owner != null)
                {
                    _scene.SendMessage(Message.Destroyed, this, _owner, 0, 0);
                }
                bool spawn = false;
                ItemType itemType = ItemType.None;
                uint rand = Rng.GetRandomInt2(190);
                if (rand < 10)
                {
                    spawn = true;
                    itemType = ItemType.HealthSmall;
                }
                else if (rand < 70)
                {
                    spawn = true;
                    itemType = ItemType.UASmall;
                }
                if (spawn)
                {
                    int despawnTime = 300 * 2; // todo: FPS stuff
                    var item = new ItemInstanceEntity(new ItemInstanceEntityData(Position, itemType, despawnTime), NodeRef, _scene);
                    _scene.AddEntity(item);
                }
            }
            return false;
        }

        public void Explode()
        {
            DieAndSpawnEffect(75); // goreaCrystalExplode
        }
    }
}
