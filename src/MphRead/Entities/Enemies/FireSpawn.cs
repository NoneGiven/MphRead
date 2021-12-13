using System;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy39Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private int _animFrameCount = 0;
        private CollisionVolume _activeVolume;
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

        public Enemy39Values Values { get; private set; }

        public Enemy39Entity(EnemyInstanceEntityData data) : base(data)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected override bool EnemyInitialize(Scene scene)
        {
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.OnRadar;
            Flags &= ~EnemyFlags.CollidePlayer;
            Vector3 position = _data.Spawner.Position;
            Transform = GetTransformMatrix((scene.CameraPosition - position).Normalized(), Vector3.UnitY); // todo: use player position
            Position = position;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, Fixed.ToFloat(13516), Fixed.ToFloat(11059)), 0.5f);
            ModelInstance inst = SetUpModel("LavaDemon");
            Recolor = (int)_spawner.Data.Fields.S06.EnemySubtype;
            Values = Metadata.Enemy39Values[Recolor];
            _health = _healthMax = Values.HealthMax;
            inst.SetAnimation(1, AnimFlags.Paused); // just setting to get the frame count, I guess
            _animFrameCount = inst.AnimInfo.FrameCount[0];
            _activeVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume2, Position);
            _locationVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume1, Position);
            Metadata.LoadEffectiveness(Values.Effectiveness, BeamEffectiveness);
            // todo: scan ID
            // todo: ammo pointer
            WeaponInfo weapon = Weapons.EnemyWeapons[(int)_spawner.Data.Fields.S06.EnemyVersion];
            weapon.UnchargedDamage = Values.BeamDamage;
            weapon.SplashDamage = Values.SplashDamage;
            _equipInfo[0] = new EquipInfo(weapon, _beams);
            _equipInfo[1] = new EquipInfo(weapon, _beams);
            _attackDelay = Values.AttackDelay * 2; // todo: FPS stuff
            _attackCount = Values.AttackCountMin + Rng.GetRandomInt2((uint)(Values.AttackCountMax + 1 - Values.AttackCountMin));
            // todo: healthbar name
            if (_spawner.Data.Fields.S06.EnemySubtype == 1)
            {
                inst.Model.Materials[0].Ambient = new ColorRgb(148, 222, 255);
            }
            inst.SetAnimation(3, AnimFlags.Paused);
            for (int i = 0; i < inst.Model.Nodes.Count; i++)
            {
                Node node = inst.Model.Nodes[i];
                if (node.Name == "Wrist_L")
                {
                    _wristNodeL = node;
                }
                else if (node.Name == "Wrist_R")
                {
                    _wristNodeR = node;
                }
            }
            _hitZone = EnemySpawnEntity.SpawnEnemy(this, EnemyType.HitZone) as Enemy50Entity;
            if (_hitZone != null)
            {
                scene.AddEntity(_hitZone);
                _hitZone.Transform = Transform.ClearScale();
                Metadata.LoadEffectiveness(Values.Effectiveness, _hitZone.BeamEffectiveness);
                _hitZone.Flags |= EnemyFlags.Invincible;
                _hitZone.Flags &= ~EnemyFlags.CollidePlayer;
                _hitZone.Flags &= ~EnemyFlags.CollideBeam;
                var cylPos = new Vector3(0, Fixed.ToFloat(-2867), 0);
                var hurtVolume = new CollisionVolume(Vector3.UnitY, cylPos, Fixed.ToFloat(10649), Fixed.ToFloat(13844));
                _hitZone.SetUp(1, hurtVolume, 1);
            }
            return true;
        }

        protected override void EnemyProcess(Scene scene)
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
            CallStateProcess(scene);
        }

        // todo: function names
        private void State0(Scene scene)
        {
            Vector3 position = Position;
            Vector3 facing = (scene.CameraPosition - position).Normalized().WithY(0); // todo: use player position
            Transform = GetTransformMatrix(facing, Vector3.UnitY);
            Position = position;
            CallSubroutine(Metadata.Enemy39Subroutines, this, scene);
        }

        private void State1(Scene scene)
        {
            CallSubroutine(Metadata.Enemy39Subroutines, this, scene);
        }

        private void State2(Scene scene)
        {
            if (_tangibilityTimer == 5 * 2) // todo: FPS stuff
            {
                Debug.Assert(_hitZone != null);
                _hitZone.Flags |= EnemyFlags.CollidePlayer;
                _hitZone.Flags |= EnemyFlags.CollideBeam;
                _hitZone.HitPlayers = 1;
                Flags |= EnemyFlags.CollidePlayer;
                Flags |= EnemyFlags.CollideBeam;
                HitPlayers = 1;
            }
            if (_tangibilityTimer <= 5 * 2)
            {
                _tangibilityTimer++;
            }
            State0(scene);
        }

        private void State3(Scene scene)
        {
            State0(scene);
        }

        private void State4(Scene scene)
        {
            State0(scene);
            if (_attackCount > 0 && _animFrameCount > 0 && scene.FrameCount != 0 && scene.FrameCount % 2 == 0)
            {
                ModelInstance model = _models[0];
                AnimationInfo anim = model.AnimInfo;
                if (anim.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    if (anim.Index[0] == 1)
                    {
                        // todo: play SFX
                        model.SetAnimation(0, AnimFlags.NoLoop);
                        _animFrameCount = anim.FrameCount[0];
                        _wristId = 0;
                    }
                    else if (anim.Index[0] == 0)
                    {
                        // todo: play SFX
                        model.SetAnimation(1, AnimFlags.NoLoop);
                        _animFrameCount = anim.FrameCount[0];
                        _wristId = 1;
                    }
                }
                if (_animFrameCount == 53)
                {
                    CreateEffect(scene);
                }
                else if (_animFrameCount == 25)
                {
                    _attackCount--;
                    _attackDelay = Values.AttackDelay * 2; // todo: FPS stuff
                    Vector3 dir = scene.CameraPosition - _wristPos[_wristId]; // todo: use player position + 0.5 Y
                    dir = dir.Normalized();
                    EquipInfo equipInfo = _equipInfo[_wristId];
                    equipInfo.Weapon.UnchargedDamage = Values.BeamDamage;
                    equipInfo.Weapon.SplashDamage = Values.SplashDamage;
                    equipInfo.Weapon.HeadshotDamage = Values.BeamDamage;
                    BeamProjectileEntity.Spawn(this, equipInfo, _wristPos[_wristId], dir, BeamSpawnFlags.None, scene);
                    if (_effectEntry != null)
                    {
                        scene.DetachEffectEntry(_effectEntry, setExpired: true);
                        _effectEntry = null;
                    }
                }
                else if (_animFrameCount >= 26 && _animFrameCount <= 52)
                {
                    if (_effectEntry != null)
                    {
                        for (int i = 0; i < _effectEntry.Elements.Count; i++)
                        {
                            Matrix4 transform = Transform.ClearScale();
                            transform.Row3.Xyz = _wristPos[_wristId];
                            EffectElementEntry element = _effectEntry.Elements[i];
                            element.Position = _wristPos[_wristId];
                            element.Transform = transform;
                        }
                    }
                }
                _animFrameCount--;
            }
        }

        private void CreateEffect(Scene scene)
        {
            Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
            transform.Row3.Xyz = _wristPos[_wristId];
            int effectId = _spawner.Data.Fields.S06.EnemySubtype == 1 ? 96 : 94; // iceDemonHurl, lavaDemonHurl
            _effectEntry = scene.SpawnEffectGetEntry(effectId, transform);
            if (_effectEntry != null)
            {
                for (int i = 0; i < _effectEntry.Elements.Count; i++)
                {
                    EffectElementEntry element = _effectEntry.Elements[i];
                    element.Flags |= EffElemFlags.ElementExtension;
                }
            }
        }

        private void State5(Scene scene)
        {
            if (_tangibilityTimer == 18 * 2) // todo: FPS stuff
            {
                Debug.Assert(_hitZone != null);
                _hitZone.Flags &= ~EnemyFlags.CollidePlayer;
                _hitZone.Flags &= ~EnemyFlags.CollideBeam;
                _hitZone.HitPlayers = 0;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                HitPlayers = 0;
            }
            if (_tangibilityTimer <= 18 * 2)
            {
                _tangibilityTimer++;
            }
            State0(scene);
        }

        private bool Behavior0(Scene scene)
        {
            if (!_activeVolume.TestPoint(scene.CameraPosition)) // todo: use player position
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

        private bool Behavior1(Scene scene)
        {
            if (_attackDelay > 0)
            {
                _attackDelay--;
                return false;
            }
            _models[0].SetAnimation(1, AnimFlags.NoLoop);
            _attackDelay = Values.AttackDelay * 2; // todo: FPS stuff
            // todo: play SFX
            return true;
        }

        private bool Behavior2(Scene scene)
        {
            if (!_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
            transform.Row3.Xyz = Position;
            int effectId = _spawner.Data.Fields.S06.EnemySubtype == 1 ? 133 : 93; // iceDemonDive, lavaDemonDive
            scene.SpawnEffect(effectId, transform);
            _models[0].SetAnimation(3, AnimFlags.Paused);
            _tangibilityTimer = 0;
            return true;
        }

        private bool Behavior3(Scene scene)
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

        private bool Behavior4(Scene scene)
        {
            if (_diveTimer > 0)
            {
                _diveTimer--;
                return false;
            }
            Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
            transform.Row3.Xyz = Position;
            int effectId = _spawner.Data.Fields.S06.EnemySubtype == 1 ? 132 : 95; // iceDemonRise, lavaDemonRise
            scene.SpawnEffect(effectId, transform);
            _models[0].SetAnimation(3, AnimFlags.NoLoop);
            return true;
        }

        private bool Behavior5(Scene scene)
        {
            if (_attackCount > 0)
            {
                return false;
            }
            return StartSubmerge();
        }

        private bool Behavior6(Scene scene)
        {
            if (_activeVolume.TestPoint(scene.CameraPosition)) // todo: use player position
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
            // todo: play SFX
            _models[0].SetAnimation(2, AnimFlags.NoLoop);
            _attackCount = Values.AttackCountMin + Rng.GetRandomInt2((uint)(Values.AttackCountMax + 1 - Values.AttackCountMin));
            _wristId = 1;
            _tangibilityTimer = 0;
            Flags |= EnemyFlags.Invincible;
            return true;
        }

        #region Boilerplate
        private void CallStateProcess(Scene scene)
        {
            if (_state1 == 0)
            {
                State0(scene);
            }
            else if (_state1 == 1)
            {
                State1(scene);
            }
            else if (_state1 == 2)
            {
                State2(scene);
            }
            else if (_state1 == 3)
            {
                State3(scene);
            }
            else if (_state1 == 4)
            {
                State4(scene);
            }
            else if (_state1 == 5)
            {
                State5(scene);
            }
        }

        public static bool Behavior1(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior1(scene);
        }

        public static bool Behavior2(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior2(scene);
        }

        public static bool Behavior3(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior3(scene);
        }

        public static bool Behavior4(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior4(scene);
        }

        public static bool Behavior5(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior5(scene);
        }

        public static bool Behavior6(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior6(scene);
        }

        public static bool Behavior0(Enemy39Entity enemy, Scene scene)
        {
            return enemy.Behavior0(scene);
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
