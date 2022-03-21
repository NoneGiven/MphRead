using System;
using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy18Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private Enemy18Values _values;
        private PlayerEntity? _target = null;
        private CollisionVolume _rangeVolume;

        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;
        private ushort _shotCount = 0;
        private ushort _shotTimer = 0;
        private ushort _delayTimer = 0;

        private Vector3 _initialFacing;
        private Vector3 _aimVec;
        // for patrol aiming
        private float _angleIncYSign = 1;
        private float _angleIncXSign = 1;
        private float _angleY = 0;
        private float _angleX = 0;
        // for aiming at target 
        private Vector3 _targetVec;
        private Vector3 _crossVec;
        private float _aimAngleStep = 0;
        private ushort _aimSteps = 0;

        private Node _rotNode = null!;
        private Vector3 _rotNodePos;

        public Enemy18Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[5]
            {
                State0, State1, State2, State3, State4
            };
        }

        private static readonly int[] _recolors = new int[11]
        {
            0, 0, 0, 1, 2, 0, 0, 0, 0, 0, 0
        };

        protected override void EnemyInitialize()
        {
            int version = (int)_spawner.Data.Fields.S06.EnemyVersion;
            Recolor = _recolors[version];
            Vector3 facing = _spawner.FacingVector;
            SetTransform(facing, _spawner.UpVector, _spawner.Position);
            ModelInstance inst = SetUpModel(Metadata.EnemyModelNames[18]);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S06.Volume0);
            _values = Metadata.Enemy18Values[(int)_spawner.Data.Fields.S06.EnemySubtype];
            _health = _healthMax = _values.HealthMax;
            Metadata.LoadEffectiveness(_values.Effectiveness, BeamEffectiveness);
            _scanId = _values.ScanId;
            _rangeVolume = CollisionVolume.Move(_spawner.Data.Fields.S06.Volume1, Position);
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            _shotTimer = (ushort)(_values.ShotCooldown * 2); // todo: FPS stuff
            _rotNode = inst.Model.GetNodeByName("Door_Rot")!;
            _rotNodePos = Position;
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            _initialFacing = facing;
            _aimVec = facing;
            WeaponInfo weapon = Weapons.EnemyWeapons[version];
            weapon.UnchargedDamage = _values.BeamDamage;
            weapon.SplashDamage = _values.SplashDamage;
            _equipInfo = new EquipInfo(weapon, _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
        }

        protected override void EnemyProcess()
        {
            // todo?: owner ent col (although it's unused)
            ContactDamagePlayer();
            if (_target != null)
            {
                for (int i = 0; i < _scene.MessageQueue.Count; i++)
                {
                    MessageInfo info = _scene.MessageQueue[i];
                    if (info.Message == Message.Destroyed && info.ExecuteFrame == _scene.FrameCount && info.Sender == _target)
                    {
                        _target = null;
                        break;
                    }
                }
            }
            CallStateProcess();
        }

        private void ContactDamagePlayer()
        {
            if (_target != null && HitPlayers[_target.SlotIndex])
            {
                Vector3 between = _target.Volume.SpherePosition - Position;
                float mag = between.Length * 5;
                _target.Speed = _target.Speed.AddX(between.X / mag).AddZ(between.Z / mag);
                _target.TakeDamage(_values.ContactDamage, DamageFlags.None, null, this);
            }
        }

        private void State0()
        {
            if (_angleIncYSign == -1)
            {
                if (_angleY < Fixed.ToFloat(_values.MinAngleY))
                {
                    _angleIncYSign = 1;
                }
            }
            else if (_angleY > Fixed.ToFloat(_values.MaxAngleY))
            {
                _angleIncYSign = -1;
            }
            if (_angleIncXSign == -1)
            {
                if (_angleX < Fixed.ToFloat(_values.MinAngleX))
                {
                    _angleIncXSign = 1;
                }
            }
            else if (_angleX > Fixed.ToFloat(_values.MaxAngleX))
            {
                _angleIncXSign = -1;
            }
            _angleY += Fixed.ToFloat(_values.AngleIncY) * _angleIncYSign / 2; // todo: FPS stuff
            _angleX += Fixed.ToFloat(_values.AngleIncX) * _angleIncXSign / 2; // todo: FPS stuff
            CallSubroutine(Metadata.Enemy18Subroutines, this);
        }

        private void State1()
        {
            CallSubroutine(Metadata.Enemy18Subroutines, this);
        }

        private void UpdateAimVec()
        {
            if (_target != null)
            {
                _aimVec = _target.Position - _rotNodePos;
            }
            _aimVec = _aimVec.Normalized();
        }

        private void State2()
        {
            UpdateAimVec();
            CallSubroutine(Metadata.Enemy18Subroutines, this);
        }

        private void State3()
        {
            UpdateAimVec();
            if (_shotCount > 0 && _shotTimer > 0)
            {
                _shotTimer--;
            }
            else if (_target != null)
            {
                Vector3 targetPos = _target.Position.AddY(0.5f);
                Vector3 spawnVec = (targetPos - _rotNodePos).Normalized();
                Vector3 spawnPos = spawnVec * Fixed.ToFloat(_values.ShotOffset) + _rotNodePos;
                // todo: play SFX
                _equipInfo.Weapon.UnchargedDamage = _values.BeamDamage;
                _equipInfo.Weapon.SplashDamage = _values.SplashDamage;
                _equipInfo.Weapon.HeadshotDamage = _values.BeamDamage;
                BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, spawnVec, BeamSpawnFlags.None, _scene);
                _shotCount--;
                _shotTimer = (ushort)(_values.ShotCooldown * 2); // todo: FPS stuff
            }
            CallSubroutine(Metadata.Enemy18Subroutines, this);
        }

        private void State4()
        {
            CallSubroutine(Metadata.Enemy18Subroutines, this);
        }

        private bool Behavior00()
        {
            if (_target == null)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (player.IsBot && !_scene.Multiplayer || player.Health == 0 || !_rangeVolume.TestPoint(player.Position)
                        || _scene.GameMode == GameMode.BountyTeams && player.TeamIndex == 0)
                    {
                        continue;
                    }
                    _target = player;
                    _targetVec = (player.Position - Position).Normalized();
                    _aimSteps = 20 * 2; // todo: FPS stuff
                    float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(_aimVec, _targetVec)));
                    _aimAngleStep = angle / _aimSteps;
                    _crossVec = Vector3.Cross(_aimVec, _targetVec).Normalized();
                    // todo: play SFX
                    return true;
                }
            }
            return false;
        }

        private bool Behavior01()
        {
            if (!SeekTargetVector(_targetVec, ref _aimVec, _crossVec, ref _aimSteps, _aimAngleStep))
            {
                return false;
            }
            _angleX = 0;
            _angleY = 0;
            return true;
        }

        private bool Behavior02()
        {
            if (_target == null)
            {
                return true;
            }
            if (_rangeVolume.TestPoint(_target.Position))
            {
                return false;
            }
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                // bug?: condition to ignore 1P bots is missing
                if (player.Health == 0 || !_rangeVolume.TestPoint(player.Position)
                    || _scene.GameMode == GameMode.BountyTeams && player.TeamIndex == 0)
                {
                    continue;
                }
                _target = player;
                return false;
            }
            _targetVec = _initialFacing;
            _aimSteps = 20 * 2; // todo: FPS stuff
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(_aimVec, _targetVec)));
            _aimAngleStep = angle / _aimSteps;
            _crossVec = Vector3.Cross(_aimVec, _targetVec).Normalized();
            _target = null;
            return true;
        }

        private bool Behavior03()
        {
            if (_shotCount > 0)
            {
                return false;
            }
            _shotCount = (ushort)(_values.MinShots + Rng.GetRandomInt2(_values.MaxShots + 1 - _values.MinShots));
            return true;
        }

        private bool Behavior04()
        {
            return SeekTargetVector(_targetVec, ref _aimVec, _crossVec, ref _aimSteps, _aimAngleStep);
        }

        private bool Behavior05()
        {
            if (_delayTimer > 0)
            {
                _delayTimer--;
                return false;
            }
            _delayTimer = (ushort)(_values.DelayTime * 2); // todo: FPS stuff
            return true;
        }

        protected override bool EnemyGetDrawInfo()
        {
            // todo: is_visible
            ModelInstance inst = _models[0];
            Model model = inst.Model;
            AnimationInfo animInfo = inst.AnimInfo;
            if (_timeSinceDamage < 5 * 2) // todo: FPS stuff
            {
                PaletteOverride = Metadata.RedPalette;
            }
            Vector3 upVector = UpVector;
            Matrix4 aimTransform;
            if (_state1 == 0)
            {
                // patrol aiming
                var rotX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_angleX));
                var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_angleY));
                aimTransform = rotX * rotY;
                _aimVec = Matrix.Vec3MultMtx4(_initialFacing, aimTransform);
                var transpose = Matrix4.Transpose(Transform.ClearTranslation());
                aimTransform *= transpose;
            }
            else
            {
                // aiming at target
                aimTransform = GetTransformMatrix(_aimVec, upVector);
                var transpose = Matrix4.Transpose(Transform.ClearTranslation());
                aimTransform *= transpose;
                aimTransform.Row3.Xyz = Vector3.Zero;
            }
            _rotNode.AfterTransform = aimTransform;
            model.AnimateNodes2(index: 0, false, Matrix4.Identity, Vector3.One, animInfo);
            _rotNode.AfterTransform = null;
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes[i];
                node.Animation *= Transform; // todo?: could do this in the shader
            }
            model.UpdateMatrixStack();
            UpdateMaterials(inst, Recolor);
            if (IsVisible(NodeRef))
            {
                GetDrawItems(inst, 0);
            }
            PaletteOverride = null;
            _rotNodePos = _rotNode.Animation.Row3.Xyz;
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy18Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy18Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy18Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy18Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy18Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy18Entity enemy)
        {
            return enemy.Behavior05();
        }

        #endregion
    }

    public struct Enemy18Values
    {
        public ushort HealthMax { get; set; }
        public ushort BeamDamage { get; set; }
        public ushort SplashDamage { get; set; }
        public ushort ContactDamage { get; set; }
        public int MinAngleY { get; set; }
        public int MaxAngleY { get; set; }
        public int AngleIncY { get; set; }
        public int MinAngleX { get; set; }
        public int MaxAngleX { get; set; }
        public int AngleIncX { get; set; }
        public ushort ShotCooldown { get; set; }
        public ushort DelayTime { get; set; }
        public ushort MinShots { get; set; }
        public ushort MaxShots { get; set; }
        public int Unused28 { get; set; }
        public ushort Unused2C { get; set; }
        public ushort Unused2E { get; set; }
        public int ShotOffset { get; set; }
        public int ScanId { get; set; }
        public int Effectiveness { get; set; }
    }
}
