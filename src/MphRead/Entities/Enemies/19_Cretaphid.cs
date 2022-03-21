using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MphRead.Formats.Collision;
using MphRead.Formats.Culling;
using MphRead.Sound;
using OpenTK.Mathematics;


namespace MphRead.Entities.Enemies
{
    public class Enemy19Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private int _subtype = 0;
        private int _crystalDownTimer = 0;
        private ushort _flashTimer = 0;
        private readonly int[,] _phaseValues = new int[3, 4];
        private int _eyeStartIndex = 0;
        private int _eyeEndIndex = 0;
        private int _eyeBurnIndex = 0;
        private float _eyeBurnUpdateTimer = 0;
        public int PhaseIndex { get; private set; }
        private int _crystalShotDelay = 0;
        private int _crystalShotTimer = 0;
        private int _crystalUpTimer = 0;

        public EquipInfo[] EquipInfo { get; } = new EquipInfo[2];
        private int _ammo0 = 1000;
        private int _ammo1 = 1000;

        private EntityCollision? _parentEntCol = null;
        private Matrix4 _invTransform = Matrix4.Identity;

        public Enemy19Values Values { get; private set; }
        public SegmentInfo[] Segments { get; } = new SegmentInfo[3];
        private readonly Enemy20Entity?[] _eyes = new Enemy20Entity?[_eyeCount];
        private Enemy21Entity? _crystal = null!;
        private ModelInstance _model = null!;
        public ModelInstance BeamModel { get; private set; } = null!;
        public ModelInstance BeamColModel { get; private set; } = null!;
        public SoundSource SounceSource => _soundSource;

        private const ushort _flashPeriod = 10 * 2; // todo: FPS stuff
        private const ushort _flashLength = 5 * 2; // todo: FPS stuff
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
            Vector3 facing = Vector3.UnitZ;
            if (position != PlayerEntity.Main.Position)
            {
                facing = (PlayerEntity.Main.Position - position).WithY(0).Normalized();
            }
            Matrix4 transform = GetTransformMatrix(facing, Vector3.UnitY);
            transform.Row3.Xyz = position;
            Transform = transform;
            if (_data.Spawner is EnemySpawnEntity spawner && spawner.ParentEntCol != null)
            {
                _parentEntCol = spawner.ParentEntCol;
                _invTransform = _transform * spawner.ParentEntCol.Inverse2;
            }
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
            _crystalDownTimer = Values.PhaseFlashTime * 2; // todo: FPS stuff
            _flashTimer = _flashPeriod;
            _model = SetUpModel("CylinderBoss", animIndex: 2);
            _model.NodeAnimIgnoreRoot = true;
            _model.Model.ComputeNodeMatrices(index: 0);
            if (_subtype == 0)
            {
                BeamModel = SetUpModel("cylBossLaser");
            }
            else if (_subtype == 2)
            {
                BeamModel = SetUpModel("cylBossLaserY");
            }
            else if (_subtype == 3)
            {
                BeamModel = SetUpModel("cylBossLaserG");
            }
            BeamColModel = SetUpModel("cylBossLaserColl");
            Segments[0] = new SegmentInfo();
            Segments[1] = new SegmentInfo();
            Segments[2] = new SegmentInfo();
            Segments[0].JointNode = _model.Model.GetNodeByName("Upper_joint")!;
            Segments[1].JointNode = _model.Model.GetNodeByName("Mid_joint")!;
            Segments[2].JointNode = _model.Model.GetNodeByName("Lower_joint")!;
            Segments[0].AngleStep = Fixed.ToFloat(Values.Seg0AngleStep) / 2; // todo: FPS stuff
            Segments[0].BeamAngle = Fixed.ToFloat(Values.Seg0BeamStartAngle);
            Segments[0].BeamAngleMax = Fixed.ToFloat(Values.Seg0BeamAngleMax);
            Segments[0].BeamAngleMin = Fixed.ToFloat(Values.Seg0BeamAngleMin);
            Segments[0].BeamAngleStep = Fixed.ToFloat(Values.Seg0BeamAngleStep) / 2; // todo: FPS stuff
            Segments[0].SpinDirection = 1;
            Segments[1].AngleStep = Fixed.ToFloat(Values.Seg1AngleStep) / 2; // todo: FPS stuff
            Segments[1].BeamAngle = Fixed.ToFloat(Values.Seg1BeamStartAngle);
            Segments[1].BeamAngleMax = Fixed.ToFloat(Values.Seg1BeamAngleMax);
            Segments[1].BeamAngleMin = Fixed.ToFloat(Values.Seg1BeamAngleMin);
            Segments[1].BeamAngleStep = Fixed.ToFloat(Values.Seg1BeamAngleStep) / 2; // todo: FPS stuff
            Segments[1].SpinDirection = -1;
            Segments[2].AngleStep = Fixed.ToFloat(Values.Seg2AngleStep) / 2; // todo: FPS stuff
            Segments[2].BeamAngle = Fixed.ToFloat(Values.Seg2BeamStartAngle);
            Segments[2].BeamAngleMax = Fixed.ToFloat(Values.Seg2BeamAngleMax);
            Segments[2].BeamAngleMin = Fixed.ToFloat(Values.Seg2BeamAngleMin);
            Segments[2].BeamAngleStep = Fixed.ToFloat(Values.Seg2BeamAngleStep) / 2; // todo: FPS stuff
            Segments[2].SpinDirection = 1;
            _phaseValues[0, 0] = Values.Phase0CrystalShotDelay * 2; // todo: FPS stuff
            _phaseValues[0, 1] = Values.Phase0CrystalShotTime * 2; // todo: FPS stuff
            _phaseValues[0, 2] = Values.Phase0CrystalUpTime * 2; // todo: FPS stuff
            _phaseValues[0, 3] = Values.Phase0CrystalHealth;
            _phaseValues[1, 0] = Values.Phase1CrystalShotDelay * 2; // todo: FPS stuff
            _phaseValues[1, 1] = Values.Phase1CrystalShotTime * 2; // todo: FPS stuff
            _phaseValues[1, 2] = Values.Phase1CrystalUpTime * 2; // todo: FPS stuff
            _phaseValues[1, 3] = Values.Phase1CrystalHealth;
            _phaseValues[2, 0] = Values.Phase2CrystalShotDelay * 2; // todo: FPS stuff
            _phaseValues[2, 1] = Values.Phase2CrystalShotTime * 2; // todo: FPS stuff
            _phaseValues[2, 2] = Values.Phase2CrystalUpTime * 2; // todo: FPS stuff
            _phaseValues[2, 3] = Values.Phase2CrystalHealth;
            _eyeStartIndex = 3;
            _eyeEndIndex = 6;
            _eyeBurnIndex = _eyeStartIndex;
            _eyeBurnUpdateTimer = 1 / 30f;
            SpawnEyes();
            SpawnCrystal();
            WeaponInfo laserWeapon = Weapons.BossWeapons[1];
            WeaponInfo plasmaWeapon = Weapons.BossWeapons[2];
            EquipInfo[0] = new EquipInfo(laserWeapon, _beams);
            EquipInfo[1] = new EquipInfo(plasmaWeapon, _beams);
            EquipInfo[0].GetAmmo = () => _ammo0;
            EquipInfo[0].SetAmmo = (newAmmo) => _ammo0 = newAmmo;
            EquipInfo[1].GetAmmo = () => _ammo1;
            EquipInfo[1].SetAmmo = (newAmmo) => _ammo1 = newAmmo;
            EquipInfo[0].ChargeLevel = laserWeapon.FullCharge;
            EquipInfo[1].ChargeLevel = plasmaWeapon.FullCharge;
            SetPhase0();
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime);
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay);
            _crystalUpTimer = GetPhaseValue(PhaseValue.CrystalUpTime);
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
        private int GetPhaseValue(PhaseValue value)
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
                _scene.AddEntity(eye);
                _eyes[i] = eye;
                eye.EyeIndex = i;
                eye.BeamColliding = true;
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
                    _scene.AddEntity(newEye);
                    eye = newEye;
                    _eyes[i] = eye;
                    eye.EyeIndex = i;
                    eye.BeamColliding = false;
                    Node node = _model.Model.GetNodeByName(_eyeNodes[i])!;
                    eye.SetUp(node, Values.EyeScanId, Values.EyeEffectiveness, Values.EyeHealth, Position, radius: 0.5f);
                }
                else if (_subtype == 0)
                {
                    eye.EyeActive = true;
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
            _scene.AddEntity(crystal);
            _crystal = crystal;
            Node node = _model.Model.GetNodeByName("Crystal_joint")!;
            crystal.SetUp(node, Values.CrystalScanId, Values.CrystalEffectiveness, Values.CrystalHealth, Position);
        }

        private void SetPhase0()
        {
            PhaseIndex = 0;
            _eyeStartIndex = 0;
            _eyeEndIndex = 2;
        }

        private void SetPhase1()
        {
            PhaseIndex = 1;
            _eyeStartIndex = 3;
            _eyeEndIndex = 6;
        }

        private void SetPhase2()
        {
            PhaseIndex = 2;
            _eyeStartIndex = 7;
            _eyeEndIndex = 11;
        }

        protected override void EnemyProcess()
        {
            if (_parentEntCol != null)
            {
                Transform = _invTransform * _parentEntCol.Transform;
            }
            // top, middle, bottom
            for (int i = 0; i < 3; i++)
            {
                SegmentInfo segment = Segments[i];
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
            _eyeBurnUpdateTimer -= _scene.FrameTime;
            if (_eyeBurnUpdateTimer <= 0)
            {
                _eyeBurnIndex++;
                if (_eyeBurnIndex > _eyeEndIndex)
                {
                    _eyeBurnIndex = _eyeStartIndex;
                }
                Enemy20Entity? eye = _eyes[_eyeBurnIndex];
                if (eye != null)
                {
                    eye.SpawnBurn = true;
                }
                _eyeBurnUpdateTimer = 1 / 30f;
            }
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(10, DamageFlags.None, direction: FacingVector, this);
            }
            CallStateProcess();
            // the game does this in the draw function
            if (_state1 == 3 || _state1 == 16 || _state1 == 26)
            {
                if (_flashTimer > 0)
                {
                    _flashTimer--;
                    if (_flashTimer == 0)
                    {
                        _flashTimer = _flashPeriod;
                    }
                }
            }
        }

        public void Sub2135F54()
        {
            if (PhaseIndex == 0)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity? eye = _eyes[i];
                    Debug.Assert(eye != null);
                    eye.UpdateState(Values.Phase0EyeState[i]);
                    eye.BeamType = Values.Phase0BeamType[i];
                    uint rand = Rng.GetRandomInt2(Values.Phase0BeamSpawnMax[i] + 1 - Values.Phase0BeamSpawnMin[i]);
                    eye.BeamSpawnCount = (ushort)(Values.Phase0BeamSpawnMin[i] + rand);
                    eye.BeamSpawnCooldown = Values.Phase0BeamCooldown[i] * 2; // todo: FPS stuff
                    eye.BeamSpawnTimer = eye.BeamSpawnCooldown;
                }
            }
            else if (PhaseIndex == 1)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity? eye = _eyes[i];
                    Debug.Assert(eye != null);
                    eye.UpdateState(Values.Phase1EyeState[i]);
                    eye.BeamType = Values.Phase1BeamType[i];
                    uint rand = Rng.GetRandomInt2(Values.Phase1BeamSpawnMax[i] + 1 - Values.Phase1BeamSpawnMin[i]);
                    eye.BeamSpawnCount = (ushort)(Values.Phase1BeamSpawnMin[i] + rand);
                    eye.BeamSpawnCooldown = Values.Phase1BeamCooldown[i] * 2; // todo: FPS stuff
                    eye.BeamSpawnTimer = eye.BeamSpawnCooldown;
                }
            }
            else if (PhaseIndex == 2)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity? eye = _eyes[i];
                    Debug.Assert(eye != null);
                    eye.UpdateState(Values.Phase2EyeState[i]);
                    eye.BeamType = Values.Phase2BeamType[i];
                    uint rand = Rng.GetRandomInt2(Values.Phase2BeamSpawnMax[i] + 1 - Values.Phase2BeamSpawnMin[i]);
                    eye.BeamSpawnCount = (ushort)(Values.Phase2BeamSpawnMin[i] + rand);
                    eye.BeamSpawnCooldown = Values.Phase2BeamCooldown[i] * 2; // todo: FPS stuff
                    eye.BeamSpawnTimer = eye.BeamSpawnCooldown;
                }
            }
        }

        public void Sub213619C(Enemy20Entity eye)
        {
            int index = eye.EyeIndex;
            if (PhaseIndex == 0)
            {
                uint rand = Rng.GetRandomInt2(Values.Phase0BeamSpawnMax[index] + 1 - Values.Phase0BeamSpawnMin[index]);
                eye.BeamSpawnCount = (ushort)(Values.Phase0BeamSpawnMin[index] + rand);
            }
            else if (PhaseIndex == 1)
            {
                uint rand = Rng.GetRandomInt2(Values.Phase1BeamSpawnMax[index] + 1 - Values.Phase1BeamSpawnMin[index]);
                eye.BeamSpawnCount = (ushort)(Values.Phase1BeamSpawnMin[index] + rand);
            }
            else if (PhaseIndex == 2)
            {
                uint rand = Rng.GetRandomInt2(Values.Phase2BeamSpawnMax[index] + 1 - Values.Phase2BeamSpawnMin[index]);
                eye.BeamSpawnCount = (ushort)(Values.Phase2BeamSpawnMin[index] + rand);
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
                    _crystalDownTimer = 35 * 2; // todo: FPS stuff
                    _model.SetAnimation(0);
                    _scene.SpawnEffect(74, Vector3.UnitX, Vector3.UnitY, _crystal.Position); // cylCrystalKill3
                    _soundSource.StopAllSfx();
                    _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_DIE); // empty
                    _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_CRYSTAL_SCR); // empty
                }
                // sktodo: play movie (i.e. fade out and pick up where the movie leaves off)
                // sktodo: make sure all eyes are despawned and platform is no longer damaging
                // (platform also probably resets to the center of the room?)
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
                _crystal.SetHealth((ushort)GetPhaseValue(PhaseValue.CrystalHealth));
                SetPhase1();
            }
            else if (PhaseIndex == 1)
            {
                _crystal.SetHealth((ushort)GetPhaseValue(PhaseValue.CrystalHealth));
                SetPhase2();
            }
            Sub2135F54();
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime);
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay);
            _crystalUpTimer = GetPhaseValue(PhaseValue.CrystalUpTime);
            return true;
        }

        private bool Behavior01()
        {
            if (_crystalDownTimer > 0)
            {
                _crystalDownTimer--;
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
                        eye.EyeActive = false;
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
            if (_crystalDownTimer > 0)
            {
                _crystalDownTimer--;
                return false;
            }
            _model.SetAnimation(1, AnimFlags.NoLoop);
            _crystalDownTimer = Values.PhaseFlashTime * 2; // todo: FPS stuff
            _flashTimer = _flashPeriod;
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
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime);
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay);
            _crystalUpTimer = GetPhaseValue(PhaseValue.CrystalUpTime);
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
            _crystalShotDelay = GetPhaseValue(PhaseValue.CrystalShotDelay);
            return true;
        }

        private bool Behavior12()
        {
            if (_crystalShotTimer > 0)
            {
                _crystalShotTimer--;
                return false;
            }
            _crystalShotTimer = GetPhaseValue(PhaseValue.CrystalShotTime);
            Debug.Assert(_crystal != null);
            _scene.SpawnEffect(67, Vector3.UnitX, Vector3.UnitY, _crystal.Position); // cylCrystalShot
            _crystal.SpawnBeam(Values.CrystalBeamDamage[PhaseIndex]);
            _soundSource.PlaySfx(SfxId.CYLINDER_BOSS_ATTACK2);
            return true;
        }

        public void UpdateTransforms(bool rootPosition)
        {
            for (int i = 0; i < 3; i++)
            {
                SegmentInfo segment = Segments[i];
                float angle = MathHelper.DegreesToRadians(segment.Angle);
                segment.JointNode.AfterTransform = Matrix4.CreateRotationX(angle);
            }
            _model.Model.AnimateNodes2(index: 0, false, Matrix4.Identity, Vector3.One, _model.AnimInfo);
            if (rootPosition)
            {
                var transform = Matrix4.CreateTranslation(Position);
                for (int i = 0; i < _model.Model.Nodes.Count; i++)
                {
                    Node node = _model.Model.Nodes[i];
                    node.Animation *= transform;
                }
            }
        }

        public void ResetTransforms()
        {
            for (int i = 0; i < 3; i++)
            {
                Segments[i].JointNode.AfterTransform = null;
            }
        }

        protected override bool EnemyGetDrawInfo()
        {
            if (_health == 0 || !Flags.TestFlag(EnemyFlags.Visible))
            {
                return true;
            }
            if (_flashTimer < _flashLength && (_state1 == 3 || _state1 == 16 || _state1 == 26))
            {
                PaletteOverride = Metadata.WhitePalette;
            }
            UpdateTransforms(rootPosition: true);
            _model.Model.UpdateMatrixStack();
            UpdateMaterials(_model, Recolor);
            GetDrawItems(_model, 0);
            ResetTransforms();
            PaletteOverride = null;
            return true;
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

        public class SegmentInfo
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
        public ushort Phase0CrystalShotDelay { get; init; }
        public ushort Phase1CrystalShotDelay { get; init; }
        public ushort Phase2CrystalShotDelay { get; init; }
        public ushort Phase0CrystalUpTime { get; init; }
        public ushort Phase1CrystalUpTime { get; init; }
        public ushort Phase2CrystalUpTime { get; init; }
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
        public byte[] Phase0EyeState { get; init; } // 12
        public byte[] Phase0BeamType { get; init; } // 12
        public byte[] Phase0BeamSpawnMin { get; init; } // 12
        public byte[] Phase0BeamSpawnMax { get; init; } // 12
        public ushort[] Phase0BeamCooldown { get; init; } // 12
        public ushort[] Phase0EyeStateTimer0 { get; init; } // 12
        public ushort[] Phase0EyeStateTimer1 { get; init; } // 12
        public ushort[] Phase0EyeStateTimer2 { get; init; } // 12
        public ushort[] Phase0EyeStateTimer3 { get; init; } // 12
        public byte[] Phase1EyeState { get; init; } // 12
        public byte[] Phase1BeamType { get; init; } // 12
        public byte[] Phase1BeamSpawnMin { get; init; } // 12
        public byte[] Phase1BeamSpawnMax { get; init; } // 12
        public ushort[] Phase1BeamCooldown { get; init; } // 12
        public ushort[] Phase1EyeStateTimer0 { get; init; } // 12
        public ushort[] Phase1EyeStateTimer1 { get; init; } // 12
        public ushort[] Phase1EyeStateTimer2 { get; init; } // 12
        public ushort[] Phase1EyeStateTimer3 { get; init; } // 12
        public byte[] Phase2EyeState { get; init; } // 12
        public byte[] Phase2BeamType { get; init; } // 12
        public byte[] Phase2BeamSpawnMin { get; init; } // 12
        public byte[] Phase2BeamSpawnMax { get; init; } // 12
        public ushort[] Phase2BeamCooldown { get; init; } // 12
        public ushort[] Phase2EyeStateTimer0 { get; init; } // 12
        public ushort[] Phase2EyeStateTimer1 { get; init; } // 12
        public ushort[] Phase2EyeStateTimer2 { get; init; } // 12
        public ushort[] Phase2EyeStateTimer3 { get; init; } // 12
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
