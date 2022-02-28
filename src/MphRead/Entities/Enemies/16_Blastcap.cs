using System;
using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy16Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private ushort _agitateTimer = 0;
        private ushort _cloudTick = 0;
        private ushort _cloudTimer = 0;
        private bool _initialCloudHit = false;

        private const float _nearRadius = 8;
        private const float _cloudRadius = 2;

        public Enemy16Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[4]
            {
                State0, State1, State2, State3
            };
        }

        protected override bool EnemyInitialize()
        {
            EntityDataHeader header = _spawner.Data.Header;
            SetTransform(header.FacingVector.ToFloatVector(), Vector3.UnitY, header.Position.ToFloatVector());
            _health = _healthMax = 12;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _agitateTimer = 60 * 2; // todo: FPS stuff
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S00.Volume0);
            SetUpModel(Metadata.EnemyModelNames[16], animIndex: 2);
            _cloudTimer = 150 * 2; // todo: FPS stuff
            return true;
        }

        protected override void EnemyProcess()
        {
            CallStateProcess();
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health == 0)
            {
                _health = 1;
                _state2 = 3;
                _subId = _state2;
                Flags |= EnemyFlags.Invincible;
                Flags &= ~EnemyFlags.Visible;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                _scene.SpawnEffect(4, Vector3.UnitX, Vector3.UnitY, Position); // blastCapBlow
                if (!_initialCloudHit)
                {
                    float radii = PlayerEntity.Main.Volume.SphereRadius + _cloudRadius;
                    if ((Position - PlayerEntity.Main.Volume.SpherePosition).LengthSquared < radii * radii)
                    {
                        _initialCloudHit = true;
                        PlayerEntity.Main.TakeDamage(2, DamageFlags.NoDmgInvuln, null, this);
                    }
                }
            }
            return false;
        }

        public void State0()
        {
            CallSubroutine(Metadata.Enemy16Subroutines, this);
        }

        public void State1()
        {
            State0();
        }

        public void State2()
        {
            State0();
        }

        public void State3()
        {
            _cloudTick++;
            State0();
        }

        private bool Behavior00()
        {
            if (_cloudTick % (10 * 2) != 0) // todo: FPS stuff
            {
                return false;
            }
            float radii = PlayerEntity.Main.Volume.SphereRadius + _cloudRadius;
            if ((Position - PlayerEntity.Main.Volume.SpherePosition).LengthSquared < radii * radii)
            {
                _initialCloudHit = true;
                PlayerEntity.Main.TakeDamage(2, DamageFlags.None, null, this);
                return true;
            }
            return false;
        }

        private bool Behavior01()
        {
            if (_cloudTimer > 0)
            {
                _cloudTimer--;
            }
            else
            {
                _health = 0;
            }
            return false;
        }

        private bool Behavior02()
        {
            float radii = PlayerEntity.Main.Volume.SphereRadius + _nearRadius;
            if ((Position - PlayerEntity.Main.Volume.SpherePosition).LengthSquared < radii * radii)
            {
                return false;
            }
            _models[0].SetAnimation(2);
            return true;
        }

        private bool Behavior03()
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return false;
            }
            _initialCloudHit = true;
            PlayerEntity.Main.TakeDamage(2, DamageFlags.None, null, this);
            TakeDamage(100, source: null);
            return true;
        }

        private bool Behavior04()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _models[0].SetAnimation(2);
            return true;
        }

        private bool Behavior05()
        {
            float radii = PlayerEntity.Main.Volume.SphereRadius + _nearRadius;
            if ((Position - PlayerEntity.Main.Volume.SpherePosition).LengthSquared < radii * radii)
            {
                _models[0].SetAnimation(1);
                return true;
            }
            return false;
        }

        private bool Behavior06()
        {
            if (_agitateTimer > 0)
            {
                _agitateTimer--;
                return false;
            }
            _soundSource.PlaySfx(SfxId.BLASTCAP_AGITATE);
            _agitateTimer = 60 * 2; // todo: FPS stuff
            _models[0].SetAnimation(0, AnimFlags.NoLoop);
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy16Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy16Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy16Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy16Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy16Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy16Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy16Entity enemy)
        {
            return enemy.Behavior06();
        }

        #endregion
    }
}
