using System;
using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy45Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private int _index = 0;
        public int Index => _index;
        private CollisionVolume _volume;
        private ModelInstance _model = null!;
        private Enemy45Values _values;

        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;
        private int _shotCooldown = 0;
        private int _shotsRemaining = 0;
        private int _salvoCooldown = 0;

        // the only used interval, 1 causes the animation to update every other frame in-game.
        // other values would be possible to slow down the animation further.
        private int _animInterval = 1;
        private int _animDelayTimer = 1;
        private bool _animReverse = false;
        private bool _animating = false;
        private int _animFrameCount = 0;

        public Enemy45Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[4]
            {
                State0, State0, State2, State0
            };
        }

        protected override void EnemyInitialize()
        {
            _state1 = _state2 = 3;
            SetTransform(_spawner.FacingVector, _spawner.UpVector, _spawner.Position);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 2;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S10.Volume1);
            _volume = CollisionVolume.Move(_spawner.Data.Fields.S10.Volume0, Position);
            _values = Metadata.Enemy45Values[(int)_spawner.Data.Fields.S10.EnemySubtype];
            _health = _healthMax = _values.Health;
            Metadata.LoadEffectiveness(_values.Effectiveness, BeamEffectiveness);
            _scanId = _values.ScanId;
            _ammo = 1000;
            UpdateShotCount();
            WeaponInfo weapon = Weapons.EnemyWeapons[(int)_spawner.Data.Fields.S10.EnemyVersion];
            weapon.UnchargedDamage = _values.Damage;
            _equipInfo = new EquipInfo(weapon, _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
            _index = _spawner.Data.Fields.S10.Index;
            _subId = _state1;
            _model = SetUpModel("BigEyeTurret", 0, AnimFlags.NoLoop | AnimFlags.Paused);
            _animFrameCount = _model.AnimInfo.FrameCount[0] - 1;
            _animDelayTimer = _animInterval;
        }

        private void UpdateShotCount()
        {
            int min = _values.MinShots;
            int max = _values.MaxShots;
            _shotsRemaining = (int)(min + Rng.GetRandomInt2(max + 1 - min));
        }

        protected override void EnemyProcess()
        {
            if (_state1 != 3)
            {
                UpdateAnimationFrame();
                ContactDamagePlayer(_values.ContactDamage, knockback: true);
                CallStateProcess();
            }
        }

        private void UpdateAnimationFrame()
        {
            if (!_animating || _scene.FrameCount == 0 || _scene.FrameCount % 2 != 0) // todo: FPS stuff
            {
                return;
            }
            if (!_animReverse)
            {
                int frame = _model.AnimInfo.Frame[0];
                if (frame >= _animFrameCount)
                {
                    if (frame > _animFrameCount)
                    {
                        _model.AnimInfo.Frame[0] = _animFrameCount;
                    }
                    _animating = false;
                }
                else if (_animDelayTimer != 0)
                {
                    _animDelayTimer--;
                }
                else
                {
                    _model.AnimInfo.Frame[0] = frame + 1;
                    _animDelayTimer = _animInterval;
                }
            }
            else if (_model.AnimInfo.Frame[0] != 0)
            {
                if (_animDelayTimer != 0)
                {
                    _animDelayTimer--;
                }
                else
                {
                    _model.AnimInfo.Frame[0] -= 1;
                    _animDelayTimer = _animInterval;
                }
            }
            else
            {
                SetAnimation();
            }
        }

        public int GetMaxFrameCount()
        {
            return _model.AnimInfo.FrameCount[0] - 1;
        }

        private void SetAnimation()
        {
            _animInterval = 1;
            _animReverse = false;
            _animating = true;
        }

        private void SetAnimationReverse()
        {
            _animInterval = 1;
            _animReverse = true;
            _animating = true;
        }

        // duplicated for 1 and 3
        private void State0()
        {
            CallSubroutine(Metadata.Enemy45Subroutines, this);
        }

        private void State2()
        {
            if (_shotsRemaining != 0 && _shotCooldown != 0)
            {
                _shotCooldown--;
            }
            else
            {
                Vector3 target = PlayerEntity.Main.Position.AddY(0.5f);
                Vector3 spawnDir = (target - Position).Normalized();
                _soundSource.PlaySfx(SfxId.TURRET_ATTACK);
                _equipInfo.Weapon.UnchargedDamage = _values.Damage;
                _equipInfo.Weapon.HeadshotDamage = _values.Damage;
                SetAnimationReverse();
                BeamProjectileEntity.Spawn(this, _equipInfo, Position, spawnDir, BeamSpawnFlags.None, _scene);
                _shotsRemaining--;
                _shotCooldown = _values.ShotCooldown * 2; // todo: FPS stuff
            }
            CallSubroutine(Metadata.Enemy45Subroutines, this);
        }

        private void SpawnChargeEffect()
        {
            _scene.SpawnEffect(109, Vector3.UnitX, Vector3.UnitY, Position); // eyeTurretCharge
        }

        private bool Behavior00()
        {
            if (PlayerEntity.Main.Health == 0 || !_volume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            _soundSource.PlaySfx(SfxId.TURRET_LOCK_ON);
            SpawnChargeEffect();
            return true;
        }

        private bool Behavior01()
        {
            return false;
        }

        private bool Behavior02()
        {
            return !_volume.TestPoint(PlayerEntity.Main.Position);
        }

        private bool Behavior03()
        {
            if (_shotsRemaining != 0)
            {
                return false;
            }
            SpawnChargeEffect();
            UpdateShotCount();
            return true;
        }

        private bool Behavior04()
        {
            if (_salvoCooldown != 0)
            {
                _salvoCooldown--;
                return false;
            }
            _salvoCooldown = _values.SalvoCooldown * 2; // todo: FPS stuff
            return true;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.ActivateTurret)
            {
                _subId = _state2 = 0;
                SetAnimation();
            }
            else if (info.Message == Message.DeactivateTurret)
            {
                _subId = _state2 = 3;
            }
            else if (info.Message == Message.DecreaseTurretLights)
            {
                if (_animFrameCount != 0)
                {
                    _animFrameCount -= (int)info.Param1;
                }
                if (!_animating)
                {
                    _model.AnimInfo.Frame[0] = _animFrameCount;
                }
            }
            else if (info.Message == Message.IncreaseTurretLights)
            {
                int maxFrame = _model.AnimInfo.FrameCount[0] - 1;
                if (_animFrameCount < maxFrame)
                {
                    _animFrameCount += (int)info.Param1;
                }
                if (_animFrameCount > maxFrame)
                {
                    _animFrameCount = maxFrame;
                }
                if (!_animating)
                {
                    _model.AnimInfo.Frame[0] = _animFrameCount;
                }
            }
        }

        #region Boilerplate

        public static bool Behavior00(Enemy45Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy45Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy45Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy45Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy45Entity enemy)
        {
            return enemy.Behavior04();
        }

        #endregion
    }

    public readonly struct Enemy45Values
    {
        public ushort Health { get; init; }
        public ushort Damage { get; init; }
        public ushort Unused4 { get; init; }
        public ushort ContactDamage { get; init; }
        public ushort ShotCooldown { get; init; }
        public ushort SalvoCooldown { get; init; }
        public ushort MinShots { get; init; }
        public ushort MaxShots { get; init; }
        public int ScanId { get; init; }
        public int Effectiveness { get; init; }
    }
}
