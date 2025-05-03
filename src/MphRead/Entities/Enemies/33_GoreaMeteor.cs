using System;
using System.Collections.Generic;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy33Entity : GoreaEnemyEntityBase
    {
        private PlayerEntity? _target = null;
        private EffectEntry? _effect = null;
        private Vector3 _effectUp;
        private Vector3 _effectFacing;

        private int _shakeTimer = 0;
        private float _field1A0 = 0;
        private float _field1A4 = 0;
        private float _field1AC = 0;
        private Vector3 _basePos;
        private float _field1B0 = 0;
        private int _field1B4 = 0;
        private int _field1B6 = 0;
        private int _field1B8 = 0;
        private float _field1BC = 0; // angle increment
        private float _field1BE = 0; // angle
        private int _itemChance1 = 0;
        private int _itemChance2 = 0;
        private int _itemChance3 = 0;
        private int _itemChance4 = 0;
        private byte _field1C4 = 0;
        private byte _field1C5 = 0;
        private bool _flag = false;

        public Enemy33Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            _stateProcesses = new Action[4]
            {
                State00, State01, State02, State03
            };
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy31Entity owner)
            {
                Flags |= EnemyFlags.Visible;
                Flags |= EnemyFlags.NoHomingNc;
                Flags &= ~EnemyFlags.Invincible;
                Flags |= EnemyFlags.CollidePlayer;
                Flags |= EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.NoMaxDistance;
                Flags |= EnemyFlags.OnRadar;
                SetTransform(owner.FacingVector, owner.UpVector, owner.Position);
                _basePos = _prevPos = Position;
                _boundingRadius = 1;
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, 1);
                _health = _healthMax = 8;
                _effectUp = Vector3.UnitY;
                _effectFacing = Vector3.UnitZ;
                _effect = SpawnEffectGetEntry(79, Position, _effectFacing, _effectUp, extensionFlag: true); // goreaMeteor
                _field1A0 = 2;
                _field1A4 = 0.125f;
                _field1AC = 0;
                _field1B0 = 1;
                _field1B4 = 15;
                _field1B6 = 390 * 2; // todo: FPS stuff
                _field1B8 = 150 * 2; // todo: FPS stuff
                _field1BC = 12 / 2; // todo: FPS stuff
                _itemChance1 = 40;
                _itemChance2 = 0;
                _itemChance3 = 0;
                _itemChance4 = 60;
                _field1C4 = 5 * 2; // todo: FPS stuff
                _flag = true;
                _target = PlayerEntity.Main;
            }
        }

        public void InitializePosition(Vector3 position)
        {
            _basePos = _prevPos = Position = position;
        }

        protected override void EnemyProcess()
        {
            if (!Flags.TestFlag(EnemyFlags.Visible))
            {
                return;
            }
            if (_effect != null)
            {
                _effect.Transform(_effectFacing, _effectUp, Position);
            }
            UpdateRotation();
            CallStateProcess();
            UpdateSpeed();
            CheckCollision();
            CheckHitPlayer();
            UpdatePosition();
            if (_health > 0)
            {
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK2_LOOP2, loop: true);
            }
        }

        private void UpdateRotation()
        {
            _field1BE += _field1BC;
            if (_field1BE >= 360)
            {
                _field1BE -= 360;
            }
            var axis = Vector3.Cross(_effectUp, _effectFacing);
            var mtx = Matrix4.CreateFromAxisAngle(axis, MathHelper.DegreesToRadians(_field1BE));
            Vector3 up = Matrix.Vec3MultMtx3(_effectUp, mtx);
            Vector3 facing = Matrix.Vec3MultMtx3(_effectFacing, mtx);
            SetTransform(facing, up, Position);
        }

        private void UpdateSpeed()
        {
            if (_target == null)
            {
                return;
            }
            Vector3 speed = _target.Position - Position;
            if (speed.LengthSquared > 1 / 128f)
            {
                speed = speed.Normalized();
                _effectFacing = speed;
                _effectUp = Enemy31Entity.Func21418EC(_effectFacing, _effectUp);
                speed *= _field1A4;
            }
            _speed = speed / 2; // todo: FPS stuff
        }

        private void CheckCollision()
        {
            if (_health == 0)
            {
                return;
            }
            Vector3 travel = _prevPos - Position;
            CollisionResult discard = default;
            if (travel.LengthSquared > 1 / 128f
                && CollisionDetection.CheckBetweenPoints(_prevPos, Position, TestFlags.Beams, _scene, ref discard))
            {
                CheckExplosionDamage();
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK2_DIE_SCR, sourceOnly: true);
                // facing and up are swapped
                SpawnEffect(178, Position, _effectUp, _effectFacing); // goreaMeteorHit
            }
        }

        private void CheckHitPlayer()
        {
            if (_health != 0 && _target != null && HitPlayers[_target.SlotIndex])
            {
                Explode(176); // goreaMeteorDamage
            }
        }

        private void UpdatePosition()
        {
            _basePos += _speed;
            if (_shakeTimer > 0)
            {
                float x = ((int)Rng.GetRandomInt2(512) - 256) / 4096f;
                float y = ((int)Rng.GetRandomInt2(512) - 256) / 4096f;
                float z = ((int)Rng.GetRandomInt2(512) - 256) / 4096f;
                Position = new Vector3(_basePos.X + x, _basePos.Y + y, _basePos.Z + z);
                _shakeTimer--;
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health > 0)
            {
                _shakeTimer = 30 * 2; // todo: FPS stuff
                SpawnEffect(176, Position); // goreaMeteorDamage
                Func2140E44();
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK2_HIT);
            }
            else
            {
                SpawnItemDrop();
                Explode(177); // goreaMeteorDestroy
            }
            return false;
        }

        // todo: member name
        private void Func2140E44()
        {
            _timeSinceDamage = 0;
            _flag = true;
        }

        private void SpawnItemDrop()
        {
            bool spawn = true;
            ItemType itemType = ItemType.None;
            int chance1 = _itemChance1;
            int chance2 = chance1 + _itemChance2;
            int chance3 = chance2 + _itemChance3;
            int chance4 = chance3 + _itemChance4;
            uint rand = Rng.GetRandomInt2(chance4);
            if (rand >= chance3)
            {
                spawn = false;
            }
            else if (rand >= chance2)
            {
                itemType = ItemType.UASmall;
            }
            else if (rand >= chance1)
            {
                itemType = ItemType.MissileSmall;
            }
            else
            {
                itemType = ItemType.HealthSmall;
            }
            if (spawn)
            {
                int despawnTime = 300 * 2; // todo: FPS stuff
                var item = new ItemInstanceEntity(new ItemInstanceEntityData(Position, itemType, despawnTime), NodeRef, _scene);
                _scene.AddEntity(item);
            }
        }

        private void Explode(int effectId)
        {
            _soundSource.PlaySfx(SfxId.GOREA2_ATTACK2_DIE_SCR, sourceOnly: true);
            CheckExplosionDamage();
            SpawnEffect(effectId, Position);
            _soundSource.StopAllSfx();
            _health = 0;
            Flags &= ~EnemyFlags.Visible;
            Flags &= ~EnemyFlags.CollidePlayer;
            Flags &= ~EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.Invincible;
            Position = Position.WithY(524288);
            _speed = Vector3.Zero;
            if (_effect != null)
            {
                _scene.UnlinkEffectEntry(_effect);
                _effect = null;
            }
        }

        private void CheckExplosionDamage()
        {
            if (_target == null)
            {
                return;
            }
            Vector3 toTarget = _target.Position - Position;
            float distance = toTarget.Length;
            if (distance >= _field1A0)
            {
                return;
            }
            CollisionResult result = default;
            var limitMin = new Vector3(Position.X - _field1AC, Position.Y - _field1AC, Position.Z - _field1AC);
            var limitMax = new Vector3(Position.X + _field1AC, Position.Y + _field1AC, Position.Z + _field1AC);
            IReadOnlyList<CollisionCandidate> candidates
                = CollisionDetection.GetCandidatesForLimits(null, Vector3.Zero, 0, limitMin, limitMax, includeEntities: false, _scene);
            if (CollisionDetection.CheckBetweenPoints(candidates, _prevPos, _target.Position, TestFlags.Beams, _scene, ref result))
            {
                return;
            }
            int damage = _field1B4;
            float dirMag = _field1B0;
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                float factor = Math.Clamp(distance / _field1A0, 0, 1);
                damage = (int)(damage - damage * factor);
                dirMag = (int)(dirMag - dirMag * factor);
            }
            if (distance > 1 / 128f)
            {
                toTarget = toTarget.Normalized();
            }
            else
            {
                toTarget = Vector3.UnitY;
            }
            toTarget *= dirMag;
            _target.TakeDamage(damage, DamageFlags.NoDmgInvuln, toTarget, this);
        }

        protected override bool EnemyGetDrawInfo()
        {
            if (_scene.ProcessFrame && _flag)
            {
                _timeSinceDamage = 4 * 2; // todo: FPS stuff
            }
            DrawGeneric();
            return true;
        }

        private void State00()
        {
            CallSubroutine(Metadata.Enemy33Subroutines, this);
        }

        private void State01()
        {
            CallSubroutine(Metadata.Enemy33Subroutines, this);
        }

        private void State02()
        {
            if (_field1C4 > 0) // always true
            {
                if (_field1C5 != 0)
                {
                    _field1C5--;
                }
                if (_field1C5 == 0)
                {
                    _field1C5 = _field1C4;
                    _flag = !_flag;
                    if (_flag)
                    {
                        Func2140E44();
                    }
                }
            }
            CallSubroutine(Metadata.Enemy33Subroutines, this);
        }

        private void State03()
        {
            CallSubroutine(Metadata.Enemy33Subroutines, this);
        }

        private bool Behavior00()
        {
            if (_field1B8 > 0)
            {
                _field1B8--;
            }
            if (_field1B8 == 0)
            {
                Explode(178); // goreaMeteorHit
                return true;
            }
            return false;
        }

        private bool Behavior01()
        {
            return true;
        }

        // todo: this is the same as Behavior00 except field1B6 is used
        private bool Behavior02()
        {
            if (_field1B6 > 0)
            {
                _field1B6--;
            }
            if (_field1B6 == 0)
            {
                Explode(178); // goreaMeteorHit
                return true;
            }
            return false;
        }

        private bool Behavior03()
        {
            if (_target == null)
            {
                return false;
            }
            CollisionResult discard = default;
            return CollisionDetection.CheckSphereOverlapVolume(_target.Volume, Position, _field1A0, ref discard);
        }

        #region Boilerplate

        public static bool Behavior00(Enemy33Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy33Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy33Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy33Entity enemy)
        {
            return enemy.Behavior03();
        }

        #endregion
    }
}
