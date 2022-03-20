using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;


namespace MphRead.Entities.Enemies
{
    public class Enemy19Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private int _subtype = 0;
        private int _phaseFlashTimer = 0; // sktodo: differentiate these fields and make sure _flashTimer updates correctly
        private ushort _flashTimer = 0;
        private readonly ushort[,] _phaseValues = new ushort[3, 4];
        private int _eyeStartIndex = 0;
        private int _eyeEndIndex = 0;
        public int PhaseIndex { get; private set; }
        private int _crystalShotDelay = 0;
        private int _crystalShotTimer = 0;
        private int _crystalUpTimer = 0;

        private readonly EquipInfo[] _equipInfo = new EquipInfo[2];
        private int _ammo0 = 1000;
        private int _ammo1 = 1000;

        public Enemy19Values Values { get; private set; }
        private readonly SegmentInfo[] _segments = new SegmentInfo[3];
        private readonly Enemy20Entity?[] _eyes = new Enemy20Entity?[_eyeCount];
        private Enemy21Entity? _crystal = null!;
        private ModelInstance _model = null!;
        private ModelInstance _beamModel = null!;
        private ModelInstance _beamColModel = null!;

        private const ushort _flashReset = 10 * 2; // todo: FPS stuff
        private const ushort _flashTime = 5 * 2; // todo: FPS stuff
        private const ushort _eyeCount = 12;

        public Enemy19Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[27]
            {
                State0, State0, State0, State0, State0,
                State0, State0, State0, State0, State0,
                State0, State0, State0, State0, State0,
                State0, State0, State0, State0, State0, State0,
                State0, State0, State0, State0, State0, State0
            };
        }

        protected override bool EnemyInitialize()
        {
            Vector3 position = _data.Spawner.Position;
            // sktodo: need to make sure player position is set at the door by this point?
            Vector3 facing = Vector3.UnitZ;
            if (position != PlayerEntity.Main.Position)
            {
                facing = (PlayerEntity.Main.Position - position).WithY(0).Normalized();
            }
            Matrix4 transform = GetTransformMatrix(facing, Vector3.UnitY);
            transform.Row3.Xyz = position;
            Transform = transform;
            _health = _healthMax = 100;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.OnRadar;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 1;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S05.Volume0);
            _subtype = (int)_spawner.Data.Fields.S05.EnemySubtype;
            Values = Metadata.Enemy19Values[_subtype];
            _scanId = Values.ScanId;
            _phaseFlashTimer = Values.PhaseFlashTime * 2; // todo: FPS stuff
            _flashTimer = _flashReset;
            _model = SetUpModel("CylinderBoss", animIndex: 2);
            _model.NodeAnimIgnoreRoot = true; // sktodo: ?
            if (_subtype == 0)
            {
                _beamModel = SetUpModel("cylBossLaser");
            }
            else if (_subtype == 1)
            {
                _beamModel = SetUpModel("cylBossLaserY");
            }
            else if (_subtype == 2)
            {
                _beamModel = SetUpModel("cylBossLaserG");
            }
            _beamColModel = SetUpModel("cylBossLaserColl");
            _segments[0] = new SegmentInfo();
            _segments[1] = new SegmentInfo();
            _segments[2] = new SegmentInfo();
            _segments[0].JointNode = _model.Model.GetNodeByName("Upper_joint")!;
            _segments[1].JointNode = _model.Model.GetNodeByName("Mid_joint")!;
            _segments[2].JointNode = _model.Model.GetNodeByName("Lower_joint")!;
            _segments[0].AngleStep = Fixed.ToFloat(Values.Seg0AngleStep);
            _segments[0].BeamAngle = Fixed.ToFloat(Values.Seg0BeamStartAngle);
            _segments[0].BeamAngleMax = Fixed.ToFloat(Values.Seg0BeamAngleMax);
            _segments[0].BeamAngleMin = Fixed.ToFloat(Values.Seg0BeamAngleMin);
            _segments[0].BeamAngleStep = Fixed.ToFloat(Values.Seg0BeamAngleStep);
            _segments[0].SpinDirection = 1;
            _segments[1].AngleStep = Fixed.ToFloat(Values.Seg1AngleStep);
            _segments[1].BeamAngle = Fixed.ToFloat(Values.Seg1BeamStartAngle);
            _segments[1].BeamAngleMax = Fixed.ToFloat(Values.Seg1BeamAngleMax);
            _segments[1].BeamAngleMin = Fixed.ToFloat(Values.Seg1BeamAngleMin);
            _segments[1].BeamAngleStep = Fixed.ToFloat(Values.Seg1BeamAngleStep);
            _segments[1].SpinDirection = -1;
            _segments[2].AngleStep = Fixed.ToFloat(Values.Seg2AngleStep);
            _segments[2].BeamAngle = Fixed.ToFloat(Values.Seg2BeamStartAngle);
            _segments[2].BeamAngleMax = Fixed.ToFloat(Values.Seg2BeamAngleMax);
            _segments[2].BeamAngleMin = Fixed.ToFloat(Values.Seg2BeamAngleMin);
            _segments[2].BeamAngleStep = Fixed.ToFloat(Values.Seg2BeamAngleStep);
            _segments[2].SpinDirection = 1;
            _phaseValues[0, 0] = Values.Seg0Value0;
            _phaseValues[0, 1] = Values.Phase0CrystalShotTime;
            _phaseValues[0, 2] = Values.Seg0Value2;
            _phaseValues[0, 3] = Values.Phase0CrystalHealth;
            _phaseValues[1, 0] = Values.Seg1Value0;
            _phaseValues[1, 1] = Values.Phase1CrystalShotTime;
            _phaseValues[1, 2] = Values.Seg1Value2;
            _phaseValues[1, 3] = Values.Phase1CrystalHealth;
            _phaseValues[2, 0] = Values.Seg2Value0;
            _phaseValues[2, 1] = Values.Phase2CrystalShotTime;
            _phaseValues[2, 2] = Values.Seg2Value2;
            _phaseValues[2, 3] = Values.Phase2CrystalHealth;
            // sktodo: globals
            _eyeStartIndex = 3;
            _eyeEndIndex = 6;
            SpawnEyes();
            SpawnCrystal();
            WeaponInfo laserWeapon = Weapons.BossWeapons[1];
            WeaponInfo plasmaWeapon = Weapons.BossWeapons[2];
            _equipInfo[0] = new EquipInfo(laserWeapon, _beams);
            _equipInfo[1] = new EquipInfo(plasmaWeapon, _beams);
            _equipInfo[0].GetAmmo = () => _ammo0;
            _equipInfo[0].SetAmmo = (newAmmo) => _ammo0 = newAmmo;
            _equipInfo[1].GetAmmo = () => _ammo1;
            _equipInfo[1].SetAmmo = (newAmmo) => _ammo1 = newAmmo;
            _equipInfo[0].ChargeLevel = laserWeapon.FullCharge;
            _equipInfo[1].ChargeLevel = plasmaWeapon.FullCharge;
            SetPhase0();
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime) * 2; // todo: FPS stuff
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay) * 2; // todo: FPS stuff
            _crystalUpTimer = GetPhaseValue(PhaseValue.CrystalUpTime) * 2; // todo: FPS stuff
            Sub2135F54();
            return true;
        }

        private enum PhaseValue
        {
            CrystalShotDelay = 0,
            CrystalShotTime = 1,
            CrystalUpTime = 2,
            CrystalHealth = 3
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetPhaseValue(PhaseValue value)
        {
            return _phaseValues[PhaseIndex, (int)value];
        }

        private static readonly IReadOnlyList<string> _eyeNodes = new string[_eyeCount]
        {
            "torret_bone_2", "torret_bone_3", "torret_bone_4", "torret_bone_5",
            "torret_bone_6", "torret_bone_7", "torret_bone_8", "torret_bone_9",
            "torret_bone_10", "torret_bone_11", "torret_bone_12", "torret_bone_13"
        };

        private void SpawnEyes()
        {
            for (int i = 0; i < _eyeCount; i++)
            {
                if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.CretaphidEye, NodeRef, _scene) is not Enemy20Entity eye)
                {
                    break;
                }
                _eyes[i] = eye;
                eye.EyeIndex = i;
                eye.Flag = true;
                Node node = _model.Model.GetNodeByName(_eyeNodes[i])!;
                eye.SetUp(node, Values.EyeScanId, Values.EyeEffectiveness, Values.EyeHealth, Position, radius: 1);
            }
        }

        private void RespawnEyes()
        {
            for (int i = 0; i < _eyeCount; i++)
            {
                Enemy20Entity? eye = _eyes[i];
                if (eye == null)
                {
                    if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.CretaphidEye, NodeRef, _scene) is not Enemy20Entity newEye)
                    {
                        return;
                    }
                    eye = newEye;
                    _eyes[i] = eye;
                    eye.EyeIndex = i;
                    eye.Flag = false;
                    Node node = _model.Model.GetNodeByName(_eyeNodes[i])!;
                    eye.SetUp(node, Values.EyeScanId, Values.EyeEffectiveness, Values.EyeHealth, Position, radius: 0.5f);
                }
                else if (_subtype == 0)
                {
                    eye.Field189 = 1;
                }
                eye.UpdateState(9);
            }
        }

        private void SpawnCrystal()
        {
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.CretaphidCrystal, NodeRef, _scene) is not Enemy21Entity crystal)
            {
                return;
            }
            _crystal = crystal;
            Node node = _model.Model.GetNodeByName("Crystal_joint")!;
            crystal.SetUp(node, Values.CrystalScanId, Values.CrystalEffectiveness, Values.CrystalHealth, Position);
        }

        private void SetPhase0()
        {
            PhaseIndex = 0;
            // sktodo: globals
            _eyeStartIndex = 0;
            _eyeEndIndex = 2;
        }

        private void SetPhase1()
        {
            PhaseIndex = 1;
            // sktodo: globals
            _eyeStartIndex = 3;
            _eyeEndIndex = 6;
        }

        private void SetPhase2()
        {
            PhaseIndex = 2;
            // sktodo: globals
            _eyeStartIndex = 7;
            _eyeEndIndex = 11;
        }

        protected override void EnemyProcess()
        {
            // sktodo: linked entity
            // top, middle, bottom
            for (int i = 0; i < 3; i++)
            {
                SegmentInfo segment = _segments[i];
                segment.Angle += segment.SpinDirection * segment.AngleStep;
                if (segment.Angle >= 360)
                {
                    segment.Angle -= 360;
                }
                else if (segment.Angle < 0)
                {
                    segment.Angle += 360;
                }
                if (segment.InvertBeamRotation)
                {
                    if (segment.BeamAngle >= segment.BeamAngleMax)
                    {
                        segment.InvertBeamRotation = false;
                    }
                    else
                    {
                        segment.BeamAngle += segment.BeamAngleStep;
                    }
                }
                else if (segment.BeamAngle <= segment.BeamAngleMin)
                {
                    segment.InvertBeamRotation = true;
                }
                else
                {
                    segment.BeamAngle -= segment.BeamAngleStep;
                }
            }
            // sktodo: globals
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(10, DamageFlags.None, direction: FacingVector, this);
            }
            CallStateProcess();
        }

        private void Sub2135F54()
        {
            if (PhaseIndex == 0)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity? eye = _eyes[i];
                    Debug.Assert(eye != null);
                    eye.UpdateState(Values.Field82[i]);
                    eye.Field187 = Values.Field8E[i];
                    uint rand = Rng.GetRandomInt2(Values.FieldA6[i] + 1 - Values.Field9A[i]);
                    eye.Field18A = (byte)(Values.Field9A[i] + rand);
                    eye.Field18C = (byte)Values.FieldB2[i];
                    eye.Field18E = eye.Field18C;
                }
            }
            else if (PhaseIndex == 1)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity? eye = _eyes[i];
                    Debug.Assert(eye != null);
                    eye.UpdateState(Values.Field12A[i]);
                    eye.Field187 = Values.Field136[i];
                    uint rand = Rng.GetRandomInt2(Values.Field14E[i] + 1 - Values.Field142[i]);
                    eye.Field18A = (byte)(Values.Field142[i] + rand);
                    eye.Field18C = (byte)Values.Field15A[i];
                    eye.Field18E = eye.Field18C;
                }
            }
            else if (PhaseIndex == 2)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity? eye = _eyes[i];
                    Debug.Assert(eye != null);
                    eye.UpdateState(Values.Field1D2[i]);
                    eye.Field187 = Values.Field1DE[i];
                    uint rand = Rng.GetRandomInt2(Values.Field1F6[i] + 1 - Values.Field1EA[i]);
                    eye.Field18A = (byte)(Values.Field1EA[i] + rand);
                    eye.Field18C = (byte)Values.Field202[i];
                    eye.Field18E = eye.Field18C;
                }
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Destroyed)
            {
                if (info.Sender is not EnemyInstanceEntity enemy)
                {
                    return;
                }
                if (enemy.EnemyType == EnemyType.CretaphidEye)
                {
                    var eye = (Enemy20Entity)enemy;
                    _eyes[eye.EyeIndex] = null;
                }
                else if (enemy.EnemyType == EnemyType.CretaphidCrystal)
                {
                    _crystal = null;
                }
            }
            else if (info.Message == Message.SetActive)
            {
                Debug.Assert((int)info.Param1 == 0);
                if (info.Sender is EnemyInstanceEntity enemy
                    && enemy.EnemyType == EnemyType.CretaphidCrystal && _state1 != 26)
                {
                    Debug.Assert(_crystal != null);
                    _state2 = 26;
                    _subId = _state2;
                    _phaseFlashTimer = 35 * 2; // todo: FPS stuff
                    _model.SetAnimation(0);
                    _scene.SpawnEffect(74, Vector3.UnitX, Vector3.UnitY, _crystal.Position); // cylCrystalKill3
                    _soundSource.StopAllSfx();
                    _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_DIE); // empty
                    _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_CRYSTAL_SCR); // empty
                }
                // sktodo: play movie (i.e. fade out and pick up where the movie leaves off)
            }
        }

        private void State0()
        {
            CallSubroutine(Metadata.Enemy19Subroutines, this);
        }

        private bool Behavior00()
        {
            if (!_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _model.SetAnimation(2);
            RespawnEyes();
            Debug.Assert(_crystal != null);
            if (PhaseIndex == 0)
            {
                _crystal.SetHealth(GetPhaseValue(PhaseValue.CrystalHealth));
                SetPhase1();
            }
            else if (PhaseIndex == 1)
            {
                _crystal.SetHealth(GetPhaseValue(PhaseValue.CrystalHealth));
                SetPhase2();
            }
            Sub2135F54();
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime) * 2; // todo: FPS stuff
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay) * 2; // todo: FPS stuff
            _crystalUpTimer = GetPhaseValue(PhaseValue.CrystalUpTime) * 2; // todo: FPS stuff
            return true;
        }

        private bool Behavior01()
        {
            if (_phaseFlashTimer > 0)
            {
                _phaseFlashTimer--;
                return false;
            }
            Debug.Assert(_crystal != null);
            _crystal.SetHealth(0);
            return true;
        }

        private bool Behavior02()
        {
            bool allEyesDestroyed = true;
            if (_subtype == 0)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    if (_eyes[i] != null && (i < _eyeStartIndex || i > _eyeEndIndex))
                    {
                        allEyesDestroyed = false;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    if (_eyes[i] != null)
                    {
                        allEyesDestroyed = false;
                        break;
                    }
                }
            }
            if (allEyesDestroyed)
            {
                if (_subtype == 0)
                {
                    for (int i = _eyeStartIndex; i <= _eyeEndIndex; i++)
                    {
                        Enemy20Entity? eye = _eyes[i];
                        Debug.Assert(eye != null);
                        eye.Field189 = 0;
                        eye.UpdateState(5);
                    }
                }
                _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_CRYSTAL_UP);
                _model.SetAnimation(4, AnimFlags.NoLoop);
                Debug.Assert(_crystal != null);
                _crystal.Flags &= ~EnemyFlags.Invincible;
                return true;
            }
            return false;
        }

        private bool Behavior03()
        {
            if (_phaseFlashTimer > 0)
            {
                _phaseFlashTimer--;
                return false;
            }
            _model.SetAnimation(1, AnimFlags.NoLoop);
            _phaseFlashTimer = Values.PhaseFlashTime * 2; // todo: FPS stuff
            _flashTimer = _flashReset;
            return true;
        }

        private bool Behavior04()
        {
            if (!_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _model.SetAnimation(3);
            if (_crystal != null)
            {
                _scene.SpawnEffect(65, Vector3.UnitX, Vector3.UnitY, _crystal.Position); // cylCrystalCharge
            }
            return true;
        }

        private bool Behavior05()
        {
            return Vector3.DistanceSquared(PlayerEntity.Main.Position, Position) < Fixed.ToFloat(610352);
        }

        private bool Behavior06()
        {
            if (!_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                return false;
            }
            _model.SetAnimation(2);
            RespawnEyes();
            if (PhaseIndex == 0)
            {
                SetPhase0();
            }
            else if (PhaseIndex == 1)
            {
                SetPhase1();
            }
            else if (PhaseIndex == 2)
            {
                SetPhase2();
            }
            Sub2135F54();
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime) * 2; // todo: FPS stuff
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay) * 2; // todo: FPS stuff
            _crystalUpTimer = GetPhaseValue(PhaseValue.CrystalUpTime) * 2; // todo: FPS stuff
            return true;
        }

        private bool Behavior07()
        {
            return true;
        }

        private bool Behavior08()
        {
            return true;
        }

        private bool Behavior09()
        {
            if (PhaseIndex == 2 && _crystal == null)
            {
                Flags &= ~EnemyFlags.Invincible;
                _health = 0;
                return true;
            }
            Debug.Assert(_crystal != null);
            if (_crystal.Health > GetPhaseValue(PhaseValue.CrystalHealth))
            {
                return false;
            }
            if (PhaseIndex == 2)
            {
                _scene.SpawnEffect(74, Vector3.UnitX, Vector3.UnitY, _crystal.Position); // cylCrystalKill3
                Flags &= ~EnemyFlags.Invincible;
                _health = 0;
            }
            else
            {
                _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_CRYSTAL_DOWN);
                _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_CRYSTAL_SCR);
                _model.SetAnimation(0);
                int effectId = PhaseIndex == 1 ? 73 : 66; // cylCrystalKill2 / cylCrystalKill
                _scene.SpawnEffect(effectId, Vector3.UnitX, Vector3.UnitY, _crystal.Position);
                _crystal.Flags |= EnemyFlags.Invincible;
            }
            return true;
        }

        private bool Behavior10()
        {
            if (_crystalUpTimer > 0)
            {
                _crystalUpTimer--;
                return false;
            }
            _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_CRYSTAL_DOWN);
            _model.SetAnimation(1, AnimFlags.NoLoop);
            Debug.Assert(_crystal != null);
            _crystal.Flags |= EnemyFlags.Invincible;
            return true;
        }

        private bool Behavior11()
        {
            if (_crystalShotDelay > 0)
            {
                _crystalShotDelay--;
                return false;
            }
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay) * 2; // todo: FPS stuff
            return true;
        }

        private bool Behavior12()
        {
            if (_crystalShotTimer > 0)
            {
                _crystalShotTimer--;
                return false;
            }
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime) * 2; // todo: FPS stuff
            Debug.Assert(_crystal != null);
            _scene.SpawnEffect(67, Vector3.UnitX, Vector3.UnitY, _crystal.Position); // cylCrystalShot
            _crystal.SpawnBeam(Values.CrystalBeamDamage[PhaseIndex]);
            _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_ATTACK2);
            return true;
        }

        protected override bool EnemyGetDrawInfo()
        {
            // sktodo
            return base.EnemyGetDrawInfo();
        }

        #region Boilerplate

        public static bool Behavior00(Enemy19Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy19Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy19Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy19Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy19Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy19Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy19Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy19Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy19Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy19Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy19Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy19Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy19Entity enemy)
        {
            return enemy.Behavior12();
        }

        #endregion

        private class SegmentInfo
        {
            public float Angle { get; set; }
            public float AngleStep { get; set; }
            public float BeamAngle { get; set; }
            public float BeamAngleMax { get; set; }
            public float BeamAngleMin { get; set; }
            public float BeamAngleStep { get; set; }
            public Node JointNode { get; set; } = null!;
            public ushort Unused1C { get; set; }
            public sbyte SpinDirection { get; set; }
            public bool InvertBeamRotation { get; set; }
        }
    }

    public readonly struct Enemy19Values
    {
        public ushort CrystalHealth { get; init; }
        public ushort PhaseFlashTime { get; init; }
        public ushort Phase0CrystalHealth { get; init; }
        public ushort Phase1CrystalHealth { get; init; }
        public ushort Phase2CrystalHealth { get; init; }
        public ushort Phase0CrystalShotTime { get; init; }
        public ushort Phase1CrystalShotTime { get; init; }
        public ushort Phase2CrystalShotTime { get; init; }
        public ushort Seg0Value0 { get; init; }
        public ushort Seg1Value0 { get; init; }
        public ushort Seg2Value0 { get; init; }
        public ushort Seg0Value2 { get; init; }
        public ushort Seg1Value2 { get; init; }
        public ushort Seg2Value2 { get; init; }
        public ushort[] CrystalBeamDamage { get; init; } // 3
        public ushort[] EyeBeamDamage { get; init; } // 3
        public ushort[] EyeSplashDamage { get; init; } // 3
        public ushort[] EyeContactDamage { get; init; } // 3
        public int Unused34 { get; init; }
        public int Unused38 { get; init; }
        public int Unused3C { get; init; }
        public int Seg0AngleStep { get; init; }
        public int Seg1AngleStep { get; init; }
        public int Seg2AngleStep { get; init; }
        public int Seg0BeamStartAngle { get; init; }
        public int Seg1BeamStartAngle { get; init; }
        public int Seg2BeamStartAngle { get; init; }
        public int Seg0BeamAngleMin { get; init; }
        public int Seg1BeamAngleMin { get; init; }
        public int Seg2BeamAngleMin { get; init; }
        public int Seg0BeamAngleMax { get; init; }
        public int Seg1BeamAngleMax { get; init; }
        public int Seg2BeamAngleMax { get; init; }
        public int Seg0BeamAngleStep { get; init; }
        public int Seg1BeamAngleStep { get; init; }
        public int Seg2BeamAngleStep { get; init; }
        public ushort EyeHealth { get; init; }
        public byte ItemChanceHealth { get; init; }
        public byte ItemChanceMissile { get; init; }
        public byte ItemChanceUa { get; init; }
        public byte ItemChanceNone { get; init; }
        public byte[] Field82 { get; init; } // 12
        public byte[] Field8E { get; init; } // 12
        public byte[] Field9A { get; init; } // 12
        public byte[] FieldA6 { get; init; } // 12
        public ushort[] FieldB2 { get; init; } // 12
        public ushort[] FieldCA { get; init; } // 12
        public ushort[] FieldE2 { get; init; } // 12
        public ushort[] FieldFA { get; init; } // 12
        public ushort[] Field112 { get; init; } // 12
        public byte[] Field12A { get; init; } // 12
        public byte[] Field136 { get; init; } // 12
        public byte[] Field142 { get; init; } // 12
        public byte[] Field14E { get; init; } // 12
        public ushort[] Field15A { get; init; } // 12
        public ushort[] Field172 { get; init; } // 12
        public ushort[] Field18A { get; init; } // 12
        public ushort[] Field1A2 { get; init; } // 12
        public ushort[] Field1BA { get; init; } // 12
        public byte[] Field1D2 { get; init; } // 12
        public byte[] Field1DE { get; init; } // 12
        public byte[] Field1EA { get; init; } // 12
        public byte[] Field1F6 { get; init; } // 12
        public ushort[] Field202 { get; init; } // 12
        public ushort[] Field21A { get; init; } // 12
        public ushort[] Field232 { get; init; } // 12
        public ushort[] Field24A { get; init; } // 12
        public ushort[] Field262 { get; init; } // 12
        public byte ItemChanceA { get; init; } // set at runtime
        public byte ItemChanceB { get; init; } // set at runtime
        public byte ItemChanceC { get; init; } // set at runtime
        public byte ItemChanceD { get; init; } // set at runtime
        public ushort Padding27E { get; init; }
        public int CollisionRadius { get; init; }
        public ushort ScanId { get; init; }
        public ushort CrystalScanId { get; init; }
        public uint CrystalEffectiveness { get; init; }
        public int EyeScanId { get; init; }
        public uint EyeEffectiveness { get; init; }
    }
}
