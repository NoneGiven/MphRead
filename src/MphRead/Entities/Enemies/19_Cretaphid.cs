using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;


namespace MphRead.Entities.Enemies
{
    public class Enemy19Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private int _subtype = 0;
        private ushort _field2BC = 0;
        private ushort _flashTimer = 0;
        private readonly ushort[,] _field264 = new ushort[3, 4];
        private int _eyeStartIndex = 0;
        private int _eyeEndIndex = 0;
        public int SegmentIndex { get; set; }
        private ushort _field2B6 = 0;
        private ushort _field2B8 = 0;
        private ushort _field2BA = 0;

        private readonly EquipInfo[] _equipInfo = new EquipInfo[2];
        private int _ammo0 = 1000;
        private int _ammo1 = 1000;

        public Enemy19Values Values { get; private set; }
        private readonly SegmentInfo[] _segments = new SegmentInfo[3];
        private readonly Enemy20Entity[] _eyes = new Enemy20Entity[_eyeCount];
        private Enemy21Entity _crystal = null!;
        private ModelInstance _beamModel = null!;
        private ModelInstance _beamColModel = null!;

        private const ushort _flashReset = 10;
        private const ushort _flashTime = 5;
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
            // sktodo: need to make sure player position is set at the door by this points
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
            _field2BC = Values.Field2;
            _flashTimer = _flashReset;
            ModelInstance inst = SetUpModel("CylinderBoss", animIndex: 2);
            inst.NodeAnimIgnoreRoot = true; // sktodo: ?
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
            _segments[0].JointNode = inst.Model.GetNodeByName("Upper_joint")!;
            _segments[1].JointNode = inst.Model.GetNodeByName("Mid_joint")!;
            _segments[2].JointNode = inst.Model.GetNodeByName("Lower_joint")!;
            _segments[0].Field4 = Values.Field40;
            _segments[0].Field8 = Values.Field4C;
            _segments[0].FieldC = Values.Seg0FieldC;
            _segments[0].Field10 = Values.Seg0Field10;
            _segments[0].Field14 = Values.Seg0Field14;
            _segments[0].Field1E = 1;
            _segments[1].Field4 = Values.Field44;
            _segments[1].Field8 = Values.Field50;
            _segments[1].FieldC = Values.Seg0FieldC;
            _segments[1].Field10 = Values.Seg0Field10;
            _segments[1].Field14 = Values.Seg0Field14;
            _segments[1].Field1E = -1;
            _segments[2].Field4 = Values.Field48;
            _segments[2].Field8 = Values.Field54;
            _segments[2].FieldC = Values.Seg0FieldC;
            _segments[2].Field10 = Values.Seg0Field10;
            _segments[2].Field14 = Values.Seg0Field14;
            _segments[2].Field1E = 1;
            _field264[0, 0] = Values.Seg0Value0;
            _field264[0, 1] = Values.Seg0Value1;
            _field264[0, 2] = Values.Seg0Value2;
            _field264[0, 3] = Values.Seg0Value3;
            _field264[1, 0] = Values.Seg1Value0;
            _field264[1, 1] = Values.Seg1Value1;
            _field264[1, 2] = Values.Seg1Value2;
            _field264[1, 3] = Values.Seg1Value3;
            _field264[2, 0] = Values.Seg2Value0;
            _field264[2, 1] = Values.Seg2Value1;
            _field264[2, 2] = Values.Seg2Value2;
            _field264[2, 3] = Values.Seg2Value3;
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
            SetSegment0();
            _field2B6 = _field264[SegmentIndex, 0];
            _field2B8 = _field264[SegmentIndex, 1];
            _field2BA = _field264[SegmentIndex, 2];
            Sub2135F54();
            return true;
        }

        private static readonly IReadOnlyList<string> _eyeNodes = new string[_eyeCount]
        {
            "torret_bone_2", "torret_bone_3", "torret_bone_4", "torret_bone_5",
            "torret_bone_6", "torret_bone_7", "torret_bone_8", "torret_bone_9",
            "torret_bone_10", "torret_bone_11", "torret_bone_12", "torret_bone_13"
        };

        // sktodo: combine with RespawnEyes?
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
                Node node = _models[0].Model.GetNodeByName(_eyeNodes[i])!;
                eye.SetUp(node, Values.EyeScanId, Values.EyeEffectiveness, Values.EyeHealth, Position);
            }
        }

        private void SpawnCrystal()
        {
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.CretaphidCrystal, NodeRef, _scene) is not Enemy21Entity crystal)
            {
                return;
            }
            _crystal = crystal;
            Node node = _models[0].Model.GetNodeByName("Crystal_joint")!;
            crystal.SetUp(node, Values.CrystalScanId, Values.CrystalEffectiveness, Values.CrystalHealth, Position);
        }

        private void SetSegment0()
        {
            SegmentIndex = 0;
            // sktodo: globals
            _eyeStartIndex = 0;
            _eyeEndIndex = 2;
        }

        private void SetSegment1()
        {
            SegmentIndex = 1;
            // sktodo: globals
            _eyeStartIndex = 3;
            _eyeEndIndex = 6;
        }

        private void SetSegment2()
        {
            SegmentIndex = 2;
            // sktodo: globals
            _eyeStartIndex = 7;
            _eyeEndIndex = 11;
        }

        protected override void EnemyProcess()
        {
            // sktodo
            base.EnemyProcess();
        }

        private void Sub2135F54()
        {
            if (SegmentIndex == 0)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity eye = _eyes[i];
                    eye.UpdateState(Values.Field82[i]);
                    eye.Field187 = Values.Field8E[i];
                    uint rand = Rng.GetRandomInt2(Values.FieldA6[i] + 1 - Values.Field9A[i]);
                    eye.Field18A = (byte)(Values.Field9A[i] + rand);
                    eye.Field18C = (byte)Values.FieldB2[i];
                    eye.Field18E = eye.Field18C;
                }
            }
            else if (SegmentIndex == 1)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity eye = _eyes[i];
                    eye.UpdateState(Values.Field12A[i]);
                    eye.Field187 = Values.Field136[i];
                    uint rand = Rng.GetRandomInt2(Values.Field14E[i] + 1 - Values.Field142[i]);
                    eye.Field18A = (byte)(Values.Field142[i] + rand);
                    eye.Field18C = (byte)Values.Field15A[i];
                    eye.Field18E = eye.Field18C;
                }
            }
            else if (SegmentIndex == 2)
            {
                for (int i = 0; i < _eyeCount; i++)
                {
                    Enemy20Entity eye = _eyes[i];
                    eye.UpdateState(Values.Field1D2[i]);
                    eye.Field187 = Values.Field1DE[i];
                    uint rand = Rng.GetRandomInt2(Values.Field1F6[i] + 1 - Values.Field1EA[i]);
                    eye.Field18A = (byte)(Values.Field1EA[i] + rand);
                    eye.Field18C = (byte)Values.Field202[i];
                    eye.Field18E = eye.Field18C;
                }
            }
        }

        private void State0()
        {
            CallSubroutine(Metadata.Enemy19Subroutines, this);
        }

        private bool Behavior00()
        {
            // sktodo
            return true;
        }

        private bool Behavior01()
        {
            // sktodo
            return true;
        }

        private bool Behavior02()
        {
            // sktodo
            return true;
        }

        private bool Behavior03()
        {
            // sktodo
            return true;
        }

        private bool Behavior04()
        {
            // sktodo
            return true;
        }

        private bool Behavior05()
        {
            // sktodo
            return true;
        }

        private bool Behavior06()
        {
            // sktodo
            return true;
        }

        private bool Behavior07()
        {
            // sktodo
            return true;
        }

        private bool Behavior08()
        {
            // sktodo
            return true;
        }

        private bool Behavior09()
        {
            // sktodo
            return true;
        }

        private bool Behavior10()
        {
            // sktodo
            return true;
        }

        private bool Behavior11()
        {
            // sktodo
            return true;
        }

        private bool Behavior12()
        {
            // sktodo
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

        private struct SegmentInfo
        {
            public int Angle { get; set; }
            public int Field4 { get; set; }
            public int Field8 { get; set; }
            public int FieldC { get; set; }
            public int Field10 { get; set; }
            public int Field14 { get; set; }
            public Node JointNode { get; set; }
            public ushort Unused1C { get; set; }
            public sbyte Field1E { get; set; }
            public byte Field1F { get; set; }
        }
    }

    public struct Enemy19Values
    {
        public ushort CrystalHealth { get; set; }
        public ushort Field2 { get; set; }
        public ushort Seg0Value3 { get; set; }
        public ushort Seg1Value3 { get; set; }
        public ushort Seg2Value3 { get; set; }
        public ushort Seg0Value1 { get; set; }
        public ushort Seg1Value1 { get; set; }
        public ushort Seg2Value1 { get; set; }
        public ushort Seg0Value0 { get; set; }
        public ushort Seg1Value0 { get; set; }
        public ushort Seg2Value0 { get; set; }
        public ushort Seg0Value2 { get; set; }
        public ushort Seg1Value2 { get; set; }
        public ushort Seg2Value2 { get; set; }
        public ushort[] CrystalBeamDamage { get; init; } // 3
        public ushort[] EyeBeamDamage { get; init; } // 3
        public ushort[] EyeSplashDamage { get; init; } // 3
        public ushort[] EyeContactDamage { get; init; } // 3
        public int Field34 { get; set; }
        public int Field38 { get; set; }
        public int Field3C { get; set; }
        public int Field40 { get; set; }
        public int Field44 { get; set; }
        public int Field48 { get; set; }
        public int Field4C { get; set; }
        public int Field50 { get; set; }
        public int Field54 { get; set; }
        public int Seg0Field10 { get; set; }
        public int Seg1Field10 { get; set; }
        public int Seg2Field10 { get; set; }
        public int Seg0FieldC { get; set; }
        public int Seg1FieldC { get; set; }
        public int Seg2FieldC { get; set; }
        public int Seg0Field14 { get; set; }
        public int Seg1Field14 { get; set; }
        public int Seg2Field14 { get; set; }
        public ushort EyeHealth { get; set; }
        public byte ItemChanceHealth { get; set; }
        public byte ItemChanceMissile { get; set; }
        public byte ItemChanceUa { get; set; }
        public byte ItemChanceNone { get; set; }
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
        public byte ItemChanceA { get; set; }
        public byte ItemChanceB { get; set; }
        public byte ItemChanceC { get; set; }
        public byte ItemChanceD { get; set; }
        public ushort Padding27E { get; set; }
        public int CollisionRadius { get; set; }
        public ushort ScanId { get; set; }
        public ushort CrystalScanId { get; set; }
        public uint CrystalEffectiveness { get; set; }
        public int EyeScanId { get; set; }
        public uint EyeEffectiveness { get; set; }
    }
}
