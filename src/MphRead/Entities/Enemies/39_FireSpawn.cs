using System;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy39Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private int _animFrameCount = 0;
        private CollisionVolume _activeVolume; // todo: visualize
        private CollisionVolume _locationVolume;
        private readonly EquipInfo[] _equipInfo = new EquipInfo[2];
        private int _wristId = 1;
        private int _tangibilityTimer = 0;
        private int _attackDelay = 0;
        private uint _attackCount = 0;
        private uint _diveTimer = 0;
        private int _surfaceDirection = 1;
        private EffectEntry? _effectEntry = null;
        private Node? _wristNodeL = null;
        private Node? _wristNodeR = null;
        private readonly Vector3[] _wristPos = new Vector3[2];
        private Enemy50Entity? _hitZone = null;
        private int _ammo0 = 1000;
        private int _ammo1 = 1000;

        public Enemy39Values Values { get; private set; }

        public Enemy39Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[6]
            {
                State0, State1, State2, State3, State4, State5
            };
        }

        private static readonly int[] _recolors = new int[11]
        {
            0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1
        };

        protected override void EnemyInitialize()
        {
            int version = (int)_spawner.Data.Fields.S06.EnemyVersion;
            Recolor = _recolors[version];
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.OnRadar;
            Flags &= ~EnemyFlags.CollidePlayer;
            Vector3 position = _data.Spawner.Position;
            Matrix4 transform = GetTransformMatrix((PlayerEntity.Main.Position - position).Normalized(), Vector3.UnitY);
            transform.Row3.Xyz = position;
            Transform = transform;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, Fixed.ToFloat(13516), Fixed.ToFloat(11059)), 0.5f);
            ModelInstance inst = SetUpModel("LavaDemon", animIndex: 1, AnimFlags.Paused);
            Values = Metadata.Enemy39Values[(int)_spawner.Data.Fields.S06.EnemySubtype];
            _health = _healthMax = Values.HealthMax;
            _animFrameCount = inst.AnimInfo.FrameCount[0]; // just set to get the frame count, I guess
            _activeVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume2, Position);
            _locationVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume1, Position);
            Metadata.LoadEffectiveness(Values.Effectiveness, BeamEffectiveness);
            _scanId = Values.ScanId;
            WeaponInfo weapon = Weapons.EnemyWeapons[version];
            _equipInfo[0] = new EquipInfo(weapon, _beams);
            _equipInfo[1] = new EquipInfo(weapon, _beams);
            _equipInfo[0].GetAmmo = () => _ammo0;
            _equipInfo[0].SetAmmo = (newAmmo) => _ammo0 = newAmmo;
            _equipInfo[1].GetAmmo = () => _ammo1;
            _equipInfo[1].SetAmmo = (newAmmo) => _ammo1 = newAmmo;
            _equipInfo[0].UnchargedDamage = Values.BeamDamage;
            _equipInfo[0].SplashDamage = Values.SplashDamage;
            _equipInfo[1].UnchargedDamage = Values.BeamDamage;
            _equipInfo[1].SplashDamage = Values.SplashDamage;
            _attackDelay = Values.AttackDelay * 2; // todo: FPS stuff
            _attackCount = Values.AttackCountMin + Rng.GetRandomInt2((uint)(Values.AttackCountMax + 1 - Values.AttackCountMin));
            HealthbarMessageId = 4; // fire spawn
            if (_spawner.Data.Fields.S06.EnemySubtype == 1)
            {
                inst.Model.Materials[0].Ambient = new ColorRgb(18, 27, 31); // should really be based on version/texture
                HealthbarMessageId = 5; // ice spawn
            }
            inst.SetAnimation(3, AnimFlags.Paused);
            _wristNodeL = inst.Model.GetNodeByName("Wrist_L");
            _wristNodeR = inst.Model.GetNodeByName("Wrist_R");
            _hitZone = EnemySpawnEntity.SpawnEnemy(this, EnemyType.HitZone, NodeRef, _scene) as Enemy50Entity;
            if (_hitZone != null)
            {
                _scene.AddEntity(_hitZone);
                _hitZone.Transform = Transform.ClearScale();
                Metadata.LoadEffectiveness(Values.Effectiveness, _hitZone.BeamEffectiveness);
                _hitZone.Flags |= EnemyFlags.Invincible;
                _hitZone.Flags &= ~EnemyFlags.CollidePlayer;
                _hitZone.Flags &= ~EnemyFlags.CollideBeam;
                var cylPos = new Vector3(0, Fixed.ToFloat(-2867), 0);
                var hurtVolume = new CollisionVolume(Vector3.UnitY, cylPos, Fixed.ToFloat(10649), Fixed.ToFloat(13844));
                _hitZone.SetUp(1, hurtVolume, 1);
            }
        }

        protected override void EnemyProcess()
        {
            ContactDamagePlayer(Values.ContactDamage, knockback: true);
            if (_state1 == 4)
            {
                Debug.Assert(_wristNodeL != null);
                Debug.Assert(_wristNodeR != null);
                // the game set node_anim_ignore_root and tracks these in a different way
                _wristPos[0] = _wristNodeL.Animation.Row3.Xyz;
                _wristPos[1] = _wristNodeR.Animation.Row3.Xyz;
            }
            CallStateProcess();
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health == 0)
            {
                _soundSource.PlaySfx(SfxId.LAVA_DEMON_DIE_SCR, noUpdate: true);
                if (_effectEntry != null)
                {
                    _scene.UnlinkEffectEntry(_effectEntry);
                    _effectEntry = null;
                }
                if (_hitZone != null)
                {
                    _hitZone.SetHealth(0);
                    _hitZone = null;
                }
            }
            return false;
        }

        // todo: function names
        private void State0()
        {
            // the Y component should really be set to zero before normalization -- this causes transform squashing
            Vector3 facing = (PlayerEntity.Main.Position - Position).Normalized().WithY(0);
            Matrix4 transform = GetTransformMatrix(facing, Vector3.UnitY);
            transform.Row3.Xyz = Position;
            Transform = transform;
            CallSubroutine(Metadata.Enemy39Subroutines, this);
        }

        private void State1()
        {
            CallSubroutine(Metadata.Enemy39Subroutines, this);
        }

        private void State2()
        {
            if (_tangibilityTimer == 5 * 2) // todo: FPS stuff
            {
                Debug.Assert(_hitZone != null);
                _hitZone.Flags |= EnemyFlags.CollidePlayer;
                _hitZone.Flags |= EnemyFlags.CollideBeam;
                // todo?: main player slot index for consistency?
                _hitZone.HitPlayers[0] = true;
                Flags |= EnemyFlags.CollidePlayer;
                Flags |= EnemyFlags.CollideBeam;
                HitPlayers[0] = true; // also here
            }
            if (_tangibilityTimer <= 5 * 2)
            {
                _tangibilityTimer++;
            }
            State0();
        }

        private void State3()
        {
            State0();
        }

        private void State4()
        {
            State0();
            if (_attackCount > 0 && _animFrameCount > 0 && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0)
            {
                ModelInstance model = _models[0];
                AnimationInfo anim = model.AnimInfo;
                if (anim.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _soundSource.PlaySfx(SfxId.LAVA_DEMON_ATTACK_SCR);
                    if (anim.Index[0] == 1)
                    {
                        model.SetAnimation(0, AnimFlags.NoLoop);
                        _animFrameCount = anim.FrameCount[0];
                        _wristId = 0;
                    }
                    else if (anim.Index[0] == 0)
                    {
                        model.SetAnimation(1, AnimFlags.NoLoop);
                        _animFrameCount = anim.FrameCount[0];
                        _wristId = 1;
                    }
                }
                if (_animFrameCount == 53)
                {
                    CreateEffect();
                }
                else if (_animFrameCount == 25)
                {
                    _attackCount--;
                    _attackDelay = Values.AttackDelay * 2; // todo: FPS stuff
                    Vector3 dir = PlayerEntity.Main.Position.AddY(0.5f) - _wristPos[_wristId];
                    dir = dir.Normalized();
                    EquipInfo equipInfo = _equipInfo[_wristId];
                    equipInfo.UnchargedDamage = Values.BeamDamage;
                    equipInfo.SplashDamage = Values.SplashDamage;
                    equipInfo.HeadshotDamage = Values.BeamDamage;
                    BeamProjectileEntity.Spawn(this, equipInfo, _wristPos[_wristId], dir, BeamSpawnFlags.None, NodeRef, _scene);
                    if (_effectEntry != null)
                    {
                        _scene.DetachEffectEntry(_effectEntry, setExpired: true);
                        _effectEntry = null;
                    }
                }
                else if (_animFrameCount >= 26 && _animFrameCount <= 52)
                {
                    if (_effectEntry != null)
                    {
                        _effectEntry.Transform(_wristPos[_wristId], Transform.ClearScale());
                    }
                }
                _animFrameCount--;
            }
        }

        private void CreateEffect()
        {
            Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
            transform.Row3.Xyz = _wristPos[_wristId];
            int effectId = _spawner.Data.Fields.S06.EnemySubtype == 1 ? 96 : 94; // iceDemonHurl, lavaDemonHurl
            _effectEntry = _scene.SpawnEffectGetEntry(effectId, transform);
            if (_effectEntry != null)
            {
                _effectEntry.SetElementExtension(true);
            }
        }

        private void State5()
        {
            if (_tangibilityTimer == 18 * 2) // todo: FPS stuff
            {
                Debug.Assert(_hitZone != null);
                _hitZone.Flags &= ~EnemyFlags.CollidePlayer;
                _hitZone.Flags &= ~EnemyFlags.CollideBeam;
                _hitZone.ClearHitPlayers();
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                ClearHitPlayers();
            }
            if (_tangibilityTimer <= 18 * 2)
            {
                _tangibilityTimer++;
            }
            State0();
        }

        private bool Behavior0()
        {
            if (!_activeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            ChooseSurfaceLocation();
            _diveTimer = Values.DiveTimerMin + Rng.GetRandomInt2((uint)(Values.DiveTimerMax + 1 - Values.DiveTimerMin));
            _diveTimer *= 2; // todo: FPS stuff
            return true;
        }

        private void ChooseSurfaceLocation()
        {
            float distance = 0;
            if (_locationVolume.Type == VolumeType.Cylinder)
            {
                uint radius = (uint)MathF.Round(_locationVolume.CylinderRadius * 4096);
                distance = Rng.GetRandomInt2(radius) / 4096f;
            }
            else if (_locationVolume.Type == VolumeType.Sphere)
            {
                uint radius = (uint)MathF.Round(_locationVolume.SphereRadius * 4096);
                distance = Rng.GetRandomInt2(radius) / 4096f;
            }
            var vec = new Vector3(distance, 0, 0);
            _surfaceDirection *= -1;
            float angle = Rng.GetRandomInt2(0xB4000) / 4096f; // [0-180)
            vec = Matrix.Vec3MultMtx3(vec, Matrix4.CreateRotationY(MathHelper.DegreesToRadians(angle * _surfaceDirection)));
            Vector3 position = Vector3.Zero;
            if (_locationVolume.Type == VolumeType.Cylinder)
            {
                position = new Vector3(
                    _locationVolume.CylinderPosition.X + vec.X,
                    Position.Y,
                    _locationVolume.CylinderPosition.Z + vec.Z
                );
            }
            else if (_locationVolume.Type == VolumeType.Sphere)
            {
                position = new Vector3(
                    _locationVolume.SpherePosition.X + vec.X,
                    Position.Y,
                    _locationVolume.SpherePosition.Z + vec.Z
                );
            }
            Position = position;
        }

        private bool Behavior1()
        {
            if (_attackDelay > 0)
            {
                _attackDelay--;
                return false;
            }
            _models[0].SetAnimation(1, AnimFlags.NoLoop);
            _attackDelay = Values.AttackDelay * 2; // todo: FPS stuff
            _soundSource.PlaySfx(SfxId.LAVA_DEMON_ATTACK_SCR);
            return true;
        }

        private bool Behavior2()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
            transform.Row3.Xyz = Position;
            int effectId = _spawner.Data.Fields.S06.EnemySubtype == 1 ? 133 : 93; // iceDemonDive, lavaDemonDive
            _scene.SpawnEffect(effectId, transform);
            _models[0].SetAnimation(3, AnimFlags.Paused);
            _tangibilityTimer = 0;
            return true;
        }

        private bool Behavior3()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.CollideBeam;
            Flags &= ~EnemyFlags.Invincible;
            _tangibilityTimer = 0;
            return true;
        }

        private bool Behavior4()
        {
            if (_diveTimer > 0)
            {
                _diveTimer--;
                return false;
            }
            Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
            transform.Row3.Xyz = Position;
            int effectId = _spawner.Data.Fields.S06.EnemySubtype == 1 ? 132 : 95; // iceDemonRise, lavaDemonRise
            _scene.SpawnEffect(effectId, transform);
            _models[0].SetAnimation(3, AnimFlags.NoLoop);
            _soundSource.PlaySfx(SfxId.LAVA_DEMON_APPEAR_SCR);
            return true;
        }

        private bool Behavior5()
        {
            if (_attackCount > 0)
            {
                return false;
            }
            return StartSubmerge();
        }

        private bool Behavior6()
        {
            if (_activeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            return StartSubmerge();
        }

        private bool StartSubmerge()
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _animFrameCount = _models[0].AnimInfo.FrameCount[0];
            _soundSource.PlaySfx(SfxId.LAVA_DEMON_DISAPPEAR_SCR);
            _models[0].SetAnimation(2, AnimFlags.NoLoop);
            _attackCount = Values.AttackCountMin + Rng.GetRandomInt2((uint)(Values.AttackCountMax + 1 - Values.AttackCountMin));
            _wristId = 1;
            _tangibilityTimer = 0;
            Flags |= EnemyFlags.Invincible;
            return true;
        }

        public override void Destroy()
        {
            if (_effectEntry != null)
            {
                _scene.UnlinkEffectEntry(_effectEntry);
                _effectEntry = null;
            }
            base.Destroy();
        }

        #region Boilerplate

        public static bool Behavior0(Enemy39Entity enemy)
        {
            return enemy.Behavior0();
        }

        public static bool Behavior1(Enemy39Entity enemy)
        {
            return enemy.Behavior1();
        }

        public static bool Behavior2(Enemy39Entity enemy)
        {
            return enemy.Behavior2();
        }

        public static bool Behavior3(Enemy39Entity enemy)
        {
            return enemy.Behavior3();
        }

        public static bool Behavior4(Enemy39Entity enemy)
        {
            return enemy.Behavior4();
        }

        public static bool Behavior5(Enemy39Entity enemy)
        {
            return enemy.Behavior5();
        }

        public static bool Behavior6(Enemy39Entity enemy)
        {
            return enemy.Behavior6();
        }

        #endregion
    }

    public struct Enemy39Values
    {
        public ushort HealthMax { get; set; }
        public ushort BeamDamage { get; set; }
        public ushort SplashDamage { get; set; }
        public ushort ContactDamage { get; set; }
        public short Unused8 { get; set; }
        public ushort AttackDelay { get; set; }
        public ushort AttackCountMin { get; set; }
        public ushort AttackCountMax { get; set; }
        public ushort DiveTimerMin { get; set; }
        public ushort DiveTimerMax { get; set; }
        public int Unused14 { get; set; }
        public short Unused18 { get; set; }
        public short ScanId { get; set; }
        public int Effectiveness { get; set; }
    }
}
