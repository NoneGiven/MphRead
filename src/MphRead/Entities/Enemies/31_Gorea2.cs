using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy31Entity : GoreaEnemyEntityBase
    {
        private readonly EnemySpawnEntity _spawner = null!;
        private Node _headNode = null!;
        private ModelInstance _laserModel = null!;
        private Enemy32Entity _sealSphere = null!;
        public Gorea2Flags GoreaFlags { get; set; }

        private Vector3 _teleportDestination;
        private Vector3 _field1E8;
        private Vector3 _laserTargetPos;
        private Vector3 _laserColNormal;
        private Vector3 _field20C;
        private int _field22C = 0;
        private int _field22E = 0;
        private int _field230 = 0;
        private int _field232 = 0;
        private int _field234 = 0;
        private int _field236 = 0;
        private int _field23C = 0;
        private float _field23E = 0; // angle
        private float _field240 = 0; // angle
        private byte _field242 = 0;
        private byte _field243 = 0;
        private byte _field244 = 0;
        public byte Field244 => _field244;

        private readonly Vector3 _spawnerField28;
        private readonly float _spawnerField34 = 0;
        private readonly float _spawnerField38 = 0;

        private TriggerVolumeEntity? _currentTrigger = null;
        private EffectEntry? _chargeEffect = null;
        private EffectEntry? _colEffect = null;
        private EffectEntry? _flashEffect = null;

        public Enemy31Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _spawnerField28 = spawner.Data.Fields.S12.Field28.ToFloatVector();
            _spawnerField34 = spawner.Data.Fields.S12.Field34.FloatValue;
            _spawnerField38 = spawner.Data.Fields.S12.Field38.FloatValue;
            _stateProcesses = new Action[19]
            {
                State00, State01, State02, State03, State04,
                State05, State06, State07, State08, State09,
                State10, State11, State12, State13, State14,
                State15, State16, State17, State18
            };
        }

        protected override void EnemyInitialize()
        {
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags &= ~EnemyFlags.Invincible;
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.NoMaxDistance;
            Flags |= EnemyFlags.OnRadar;
            HealthbarMessageId = 3;
            SetTransform(_spawner.FacingVector, Vector3.UnitY, _spawner.Position);
            _prevPos = Position;
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, 1);
            Scale = new Vector3(2); // 8192
            _health = _healthMax = 65535;
            _model = SetUpModel("Gorea2_lod0");
            _model.NodeAnimIgnoreRoot = true;
            _model.Model.ComputeNodeMatrices(index: 0);
            _model.SetAnimation(7);
            _laserModel = SetUpModel("goreaLaser");
            _laserModel.Active = false;
            _headNode = _model.Model.GetNodeByName("Head")!;
            _laserColNormal = UpVector;
            _field20C = Position;
            _field22C = 90 * 2; // todo: FPS stuff
            _field22E = 120 * 2; // todo: FPS stuff
            _field234 = 22 * 2; // todo: FPS stuff
            _field23E = 180;
            _field244 = 0x7F;
            GoreaFlags |= Gorea2Flags.Bit6;
            GoreaFlags |= Gorea2Flags.Bit17;
            InitTrigger();
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaSealSphere2, NodeRef, _scene) is Enemy32Entity sphere)
            {
                _scene.AddEntity(sphere);
                _sealSphere = sphere;
            }
        }

        protected override void EnemyProcess()
        {
            if (!Flags.TestFlag(EnemyFlags.Visible))
            {
                return;
            }
            bool flagSet = GoreaFlags.TestFlag(Gorea2Flags.Bit11);
            if (!flagSet)
            {
                UpdateLaserTargeting();
                CheckLaserHit();
                CheckPlayerCollision();
                if (_field236 > 0)
                {
                    _field236--;
                }
            }
            if (CheckFacingAngle(-1, PlayerEntity.Main.Position))
            {
                _sealSphere.UpdateVisibility();
            }
            CallStateProcess();
            if (!flagSet)
            {
                UpdateMaterialColors();
                Func213D7C4();
                Func213D194();
            }
            Func213D5D0();
        }

        private void CheckPlayerCollision()
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return;
            }
            Vector3 between = PlayerEntity.Main.Position - Position;
            PlayerEntity.Main.Speed += between / 4 / 2; // todo: FPS stuff
            PlayerEntity.Main.TakeDamage(10, DamageFlags.None, direction: null, source: this);
        }

        private void CreateTeleportEffect(bool useNode)
        {
            Vector3 spawnPos = _sealSphere.Position;
            if (useNode)
            {
                Matrix4 transform = GetNodeTransform(this, _sealSphere.AttachNode);
                spawnPos = transform.Row3.Xyz + _teleportDestination;
            }
            SpawnEffect(80, spawnPos); // goreaTeleport
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            _health = UInt16.MaxValue;
            return false;
        }

        protected override bool EnemyGetDrawInfo()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.LaserActive))
            {
                DrawLaser();
            }
            UpdateChestMaterial();
            _timeSinceDamage = 510;
            _lightOverride = true;
            DrawGeneric();
            _lightOverride = false;
            return true;
        }

        private void UpdateChestMaterial()
        {
            // caled from draw
            if (!_scene.ProcessFrame)
            {
                return;
            }
            Material material = _model.Model.GetMaterialByName("ChestCore")!;
            var white = new ColorRgb(31, 31, 31);
            int maxFrame = 10 * 2; // todo: FPS stuff
            int frame = _sealSphere.DamageTimer;
            IncrementMaterialColors(material, white, white, frame, maxFrame);
        }

        private void DrawLaser()
        {
            Vector3 laserVec = _laserTargetPos - _sealSphere.Position;
            float length = laserVec.Length;
            if (length <= 1 / 128f)
            {
                return;
            }
            laserVec = laserVec.Normalized();
            Vector3 unitVec = Vector3.UnitY;
            float dot = MathF.Abs(Vector3.Dot(unitVec, laserVec));
            if (dot > Fixed.ToFloat(4065))
            {
                unitVec = Vector3.UnitZ;
            }
            var cross1 = Vector3.Cross(laserVec, unitVec);
            var cross2 = Vector3.Cross(cross1, laserVec);
            Matrix4 transform = GetTransformMatrix(laserVec, cross2);
            transform.Row2.Xyz *= length;
            transform.Row3.Xyz = _sealSphere.Position;
            UpdateTransforms(_laserModel, transform, recolor: 0);
            GetDrawItems(_laserModel, 0);
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Destroyed)
            {
                if (info.Sender != null && info.Sender.Type == EntityType.EnemyInstance
                    && ((EnemyInstanceEntity)info.Sender).EnemyType == EnemyType.GoreaMeteor
                    && _field242 > 0)
                {
                    _field242--;
                }
            }
            else if (info.Message == Message.Gorea2Trigger)
            {
                if (info.Sender != null && info.Sender.Type == EntityType.TriggerVolume)
                {
                    var trigger = (TriggerVolumeEntity)info.Sender;
                    int value1 = (int)(GoreaFlags & (Gorea2Flags.Bit14 | Gorea2Flags.Bit15)) >> 14;
                    int value2 = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
                    if (GoreaFlags.TestFlag(Gorea2Flags.Bit10) || value1 == 1)
                    {
                        return;
                    }
                    if (value2 == 0 && _field22C == 0)
                    {
                        Teleport(trigger, null);
                    }
                    else if (value2 == 3)
                    {
                        if (trigger == _currentTrigger)
                        {
                            _field22C = 90 * 2; // todo: FPS stuff
                        }
                        else
                        {
                            Teleport(trigger, null);
                        }
                    }
                }
            }
        }

        private void Teleport(TriggerVolumeEntity? trigger, Vector3? position)
        {
            GoreaFlags &= ~(Gorea2Flags.Bit4 | Gorea2Flags.Bit5);
            GoreaFlags |= Gorea2Flags.Bit4;
            GoreaFlags |= Gorea2Flags.Bit6;
            _field22C = 15 * 2; // todo: FPS stuff
            _currentTrigger = trigger;
            if (trigger != null)
            {
                _teleportDestination = trigger.Volume.GetCenter();
            }
            else if (position != null)
            {
                _teleportDestination = position.Value;
            }
            _soundSource.PlaySfx(SfxId.GOREA2_TELEPORT_OUT);
            CreateTeleportEffect(useNode: false);
        }

        private void UpdateLaserTargeting()
        {
            if (!GoreaFlags.TestFlag(Gorea2Flags.LaserActive))
            {
                return;
            }
            UpdateAnimFrames(_laserModel);
            Vector3 posToPlayer = PlayerEntity.Main.Position - _laserTargetPos;
            if (posToPlayer.Length <= 0.125f)
            {
                _laserTargetPos = PlayerEntity.Main.Position;
                GoreaFlags |= Gorea2Flags.LaserOnTarget;
            }
            else
            {
                _laserTargetPos += posToPlayer * (1 / (12 * 2f)); // todo: FPS stuff
                GoreaFlags &= ~Gorea2Flags.LaserOnTarget;
            }
            Vector3 spherePos = _sealSphere.Position;
            CollisionResult res = default;
            bool blocked = CollisionDetection.CheckBetweenPoints(spherePos, _laserTargetPos, TestFlags.None, _scene, ref res);
            if (blocked)
            {
                GoreaFlags |= Gorea2Flags.LaserBlocked;
                _laserTargetPos = res.Position;
                _laserColNormal = res.Plane.Xyz;
            }
            else
            {
                GoreaFlags &= ~Gorea2Flags.LaserBlocked;
                Vector3 laserVec = _laserTargetPos - spherePos;
                if (laserVec.LengthSquared > 0)
                {
                    laserVec = laserVec.Normalized();
                    _laserTargetPos += laserVec * (1 / (12 * 2f)); // todo: FPS stuff
                }
            }
        }

        // todo: member name
        private void CheckLaserHit()
        {
            if (!GoreaFlags.TestFlag(Gorea2Flags.LaserActive))
            {
                return;
            }
            Vector3 spherePos = _sealSphere.Position;
            bool laserHit = GoreaFlags.TestFlag(Gorea2Flags.LaserOnTarget);
            if (!laserHit)
            {
                CollisionResult discard = default;
                if ((_laserTargetPos - spherePos).Length < 0.5f)
                {
                    // bugfix?: doesn't this mean a hit would regitser if the laser were immediately blocked by a wall?
                    laserHit = true;
                }
                else if (CollisionDetection.CheckCylinderOverlapVolume(PlayerEntity.Main.Volume,
                    spherePos, _laserTargetPos, radius: 0.5f, ref discard))
                {
                    laserHit = true;
                }
            }
            if (laserHit)
            {
                Vector3? direction = null;
                Vector3 between = Func204D518(_laserTargetPos - spherePos, PlayerEntity.Main.UpVector);
                if (between.LengthSquared > 1 / 128f)
                {
                    between = between.Normalized();
                    direction = between * (1 / 30f);
                }
                PlayerEntity.Main.TakeDamage(55, DamageFlags.None, direction, this);
            }
        }

        private static readonly IReadOnlyList<string> _lightMaterialNames
            = ["light1", "Light2", "Light3", "Light4", "Light5", "Light6"];

        private static readonly IReadOnlyList<string> _allMaterialNames
            = ["BackTarget", "Eye", "Head1", "HeadFullLit", "Torso", "ChestCore", "light1", "Light2", "Light3", "Light4", "Light5", "Light6"];

        // todo: once material colors (and alpha) are all using floats early, update these functions to use floats
        // (which will enable actual interpolation and remove the need to check the frame count parity)
        private void UpdateMaterialColors()
        {
            if (_scene.FrameCount == 0 || _scene.FrameCount % 2 != 0) // todo: FPS stuff
            {
                return;
            }
            // light1, Light2, Light3, Light4, Light5, Light6
            for (int i = 0; i < _lightMaterialNames.Count; i++)
            {
                ColorRgb color = ((1 << i) & _field244) != 0 ? new ColorRgb(31, 31, 31) : new ColorRgb();
                UpdateMaterialColor(_model.Model.GetMaterialByName(_lightMaterialNames[i])!, color);
            }
        }

        private void UpdateMaterialColor(Material material, ColorRgb color)
        {
            byte red = material.Diffuse.Red;
            if (red < color.Red)
            {
                red++;
            }
            else if (red > color.Red)
            {
                red--;
            }
            byte green = material.Diffuse.Green;
            if (green < color.Green)
            {
                green++;
            }
            else if (green > color.Green)
            {
                green--;
            }
            byte blue = material.Diffuse.Blue;
            if (blue < color.Blue)
            {
                blue++;
            }
            else if (blue > color.Blue)
            {
                blue--;
            }
            material.Diffuse = new ColorRgb(red, green, blue);
        }

        // todo: member name
        private void Func213D194()
        {
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
            if (value == 0)
            {
                if (_field22C > 0)
                {
                    _field22C--;
                }
            }
            else if (value == 1)
            {
                Func213D3C8();
            }
            else if (value == 2)
            {
                Func213D30C();
            }
            else if (value == 3)
            {
                Func213D204();
            }
        }

        private void InterpolateAlpha(int frame, int frameCount, int color)
        {
            if (frame >= 0 && frame <= frameCount)
            {
                byte alpha = _model.Model.GetMaterialByName(_allMaterialNames[0])!.Alpha;
                byte value = InterpolateColor(frame, frameCount, color - alpha);
                for (int i = 0; i < _allMaterialNames.Count; i++)
                {
                    Material material = _model.Model.GetMaterialByName(_allMaterialNames[i])!;
                    material.Alpha = (byte)(alpha + value);
                }
            }
        }

        // todo: member name
        private void Func213D3C8()
        {
            if (_field22C > 0)
            {
                _field22C--;
            }
            InterpolateAlpha(15 * 2 - _field22C, 15 * 2, 0); // todo: FPS stuff
            if (_field22C == 0)
            {
                _soundSource.PlaySfx(SfxId.GOREA2_TELEPORT_IN_SCR);
                if (GoreaFlags.TestFlag(Gorea2Flags.Bit13))
                {
                    GoreaFlags &= ~Gorea2Flags.Bit13;
                    _sealSphere.Flags &= ~EnemyFlags.Invincible;
                }
                GoreaFlags &= ~(Gorea2Flags.Bit4 | Gorea2Flags.Bit5);
                GoreaFlags |= Gorea2Flags.Bit5;
                _field22C = 15 * 2; // todo: FPS stuff
                _field20C = _teleportDestination;
                Vector3 facing = (PlayerEntity.Main.Position - _field20C).WithY(0);
                if (facing.LengthSquared > 1 / 128f)
                {
                    facing = facing.Normalized();
                }
                else
                {
                    facing = Vector3.UnitZ;
                }
                SetTransform(facing, UpVector, Position);
                CreateTeleportEffect(true);
            }
        }

        // todo: member name
        private void Func213D30C()
        {
            if (_field22C > 0)
            {
                _field22C--;
            }
            InterpolateAlpha(15 * 2 - _field22C, 15 * 2, 31); // todo: FPS stuff
            if (_field22C == 0)
            {
                _soundSource.PlaySfx(SfxId.GOREA2_TELEPORT_IN_SCR);
                if (GoreaFlags.TestFlag(Gorea2Flags.Bit13))
                {
                    GoreaFlags &= ~Gorea2Flags.Bit13;
                    _sealSphere.Flags &= ~EnemyFlags.Invincible;
                }
                if (GoreaFlags.TestFlag(Gorea2Flags.Bit6))
                {
                    GoreaFlags |= Gorea2Flags.Bit4 | Gorea2Flags.Bit5;
                }
                else
                {
                    GoreaFlags &= ~(Gorea2Flags.Bit4 | Gorea2Flags.Bit5);
                }
                _field22C = 90 * 2; // todo: FPS stuff
            }
        }

        // todo: member name
        private void Func213D204()
        {
            if (_field23C > 0)
            {
                _field23C--;
            }
            InterpolateAlpha(15 * 2 - _field23C, 15 * 2, 31); // todo: FPS stuff
            if (_field22C > 0)
            {
                _field22C--;
            }
            _field1E8 = SeekTargetSetAnim(_field1E8, _models[0].AnimInfo.Index[0]);
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit13))
            {
                GoreaFlags &= ~Gorea2Flags.Bit13;
                _sealSphere.Flags &= ~EnemyFlags.Invincible;
            }
            if (_field22C == 0)
            {
                GoreaFlags &= ~(Gorea2Flags.Bit4 | Gorea2Flags.Bit5);
                if (!GoreaFlags.TestFlag(Gorea2Flags.Bit10))
                {
                    GoreaFlags |= Gorea2Flags.Bit4;
                    _field22C = 15 * 2; // todo: FPS stuff
                    _teleportDestination = Func21405FC();
                    CreateTeleportEffect(useNode: false);
                }
                GoreaFlags &= ~Gorea2Flags.Bit6;
            }
        }

        // todo: member name
        private void Func213D5D0()
        {
            UpdateHoverPosition();
            _field20C += _speed; // todo: FPS stuff?
        }

        // todo: this is pretty jerky
        private void UpdateHoverPosition()
        {
            _field23E += 4;
            if (_field23E >= 360)
            {
                _field23E -= 360;
                GoreaFlags ^= Gorea2Flags.Bit7;
            }
            float v5 = MathF.Sin(MathHelper.DegreesToRadians(_field23E)) * 1.5f;
            float v6 = _field23E / 360 * 3;
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit7))
            {
                v6 = 3 - v6;
            }
            _field240 += 2;
            if (_field240 >= 360)
            {
                _field240 -= 360;
            }
            float rand1 = ((int)Rng.GetRandomInt2(227) - 113) / 4096f;
            var mtx = Matrix4.CreateFromAxisAngle(FacingVector, MathHelper.DegreesToRadians(_field240));
            Vector3 vec = Matrix.Vec3MultMtx3(Vector3.Cross(UpVector, FacingVector), mtx);
            Position = _field20C + vec * (v6 - 1.5f - rand1);
            float rand2 = ((int)Rng.GetRandomInt2(227) - 113) / 4096f;
            vec = Matrix.Vec3MultMtx3(UpVector, mtx);
            Position += vec * (v5 - rand2);
        }

        private void State00()
        {
            if (_model.AnimInfo.Index[0] != 9)
            {
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK1A);
                _model.SetAnimation(9, AnimFlags.NoLoop);
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                _model.SetAnimation(7);
                GoreaFlags |= Gorea2Flags.Bit17;
                _sealSphere.Flags &= ~EnemyFlags.Invincible;
            }
        }

        private void State01()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit17))
            {
                GoreaFlags &= ~Gorea2Flags.Bit17;
                int value = (int)(GoreaFlags & (Gorea2Flags.Bit14 | Gorea2Flags.Bit15)) >> 14;
                _field230 = value == 0 ? 90 * 2 : 0; // todo: FPS stuff
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
            }
        }

        private void State02()
        {
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
            }
        }

        private void State03()
        {
            Vector3 effectPos = _sealSphere.Position;
            if (_chargeEffect == null)
            {
                _chargeEffect = SpawnEffectGetEntry(210, effectPos, extensionFlag: true); // goreaLaserCharge
            }
            if (_chargeEffect != null)
            {
                _chargeEffect.Transform(FacingVector, UpVector, effectPos);
            }
            Matrix4 transform = GetNodeTransform(this, _headNode);
            effectPos = transform.Row3.Xyz;
            if (_flashEffect == null)
            {
                _flashEffect = SpawnEffectGetEntry(104, effectPos, extensionFlag: false); // goreaEyeFlash
            }
            if (_flashEffect != null)
            {
                _flashEffect.Transform(FacingVector, UpVector, effectPos);
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                if (_chargeEffect != null)
                {
                    _scene.DetachEffectEntry(_chargeEffect, setExpired: true);
                    _chargeEffect = null;
                }
                if (_flashEffect != null)
                {
                    _scene.DetachEffectEntry(_flashEffect, setExpired: true);
                    _flashEffect = null;
                }
                GoreaFlags |= Gorea2Flags.Bit17;
            }
        }

        private void State04()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit17))
            {
                GoreaFlags &= ~Gorea2Flags.Bit17;
                GoreaFlags |= Gorea2Flags.LaserActive;
                _field230 = 45 * 2; // todo: FPS stuff
                _laserTargetPos = _sealSphere.Position.AddY(-25);
                UpdateLaserTargeting();
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK1B, loop: true);
            }
            if (GoreaFlags.TestFlag(Gorea2Flags.LaserBlocked) && _colEffect == null)
            {
                _colEffect = SpawnEffectGetEntry(224, _laserTargetPos, extensionFlag: true); // goreaLaserCol
            }
            if (_colEffect != null)
            {
                Vector3 effectFacing = Func21418EC(_laserColNormal, FacingVector);
                _colEffect.Transform(effectFacing, _laserColNormal, _laserTargetPos);
            }
            if (_model.AnimInfo.Index[0] == 0 && IsAtEndFrame())
            {
                _model.SetAnimation(7);
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags &= ~Gorea2Flags.LaserActive;
                _soundSource.StopSfx(SfxId.GOREA2_ATTACK1B);
                GoreaFlags |= Gorea2Flags.Bit17;
                if (_colEffect != null)
                {
                    _scene.DetachEffectEntry(_colEffect, setExpired: true);
                    _colEffect = null;
                }
            }
        }

        // todo: member name
        public static Vector3 Func21418EC(Vector3 vec1, Vector3 vec2)
        {
            var cross = Vector3.Cross(vec1, vec2);
            if (cross.LengthSquared <= 1 / 128f)
            {
                cross = Vector3.Cross(vec1, Vector3.UnitX);
                if (cross.LengthSquared <= 1 / 128f)
                {
                    cross = Vector3.Cross(vec1, Vector3.UnitY);
                    if (cross.LengthSquared <= 1 / 128f)
                    {
                        cross = Vector3.Cross(vec1, Vector3.UnitZ);
                        if (cross.LengthSquared <= 1 / 128f)
                        {
                            // the game might return an uninitialized/previous value here
                            return Vector3.Zero;
                        }
                    }
                }
            }
            return Vector3.Cross(cross.Normalized(), vec1);
        }

        private void State05()
        {
            State02();
        }

        private void State06()
        {
            if (_model.AnimInfo.Frame[0] == 27 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                ShootMeteor();
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
                _model.SetAnimation(7);
            }
        }

        private void ShootMeteor()
        {
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaMeteor, NodeRef, _scene) is Enemy33Entity meteor)
            {
                _scene.AddEntity(meteor);
                _field242++;
                string nodeName = GoreaFlags.TestFlag(Gorea2Flags.Bit8) ? "L_BodySpike" : "R_BodySpike";
                Node node = _model.Model.GetNodeByName(nodeName)!;
                Matrix4 transform = GetNodeTransform(this, node);
                Vector3 position = transform.Row3.Xyz;
                meteor.InitializePosition(position);
                SpawnEffect(174, position); // goreaMeteorLaunch
            }
        }

        private void State07()
        {
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                _field234 = 22 * 2; // todo: FPS stuff
                GoreaFlags |= Gorea2Flags.Bit17;
            }
        }

        private void State08()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit17))
            {
                GoreaFlags &= ~Gorea2Flags.Bit17;
                int value = (int)(GoreaFlags & (Gorea2Flags.Bit14 | Gorea2Flags.Bit15)) >> 14;
                _field230 = value == 0 ? 90 * 2 : 0; // todo: FPS stuff
            }
            if (_field236 == 0)
            {
                uint timer = Rng.GetRandomInt2(300) + 300;
                _field236 = (int)timer * 2; // todo: FPS stuff
                SearchAndTeleport(mode: 1, checkCollision: false);
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
                GoreaFlags |= Gorea2Flags.Bit14 | Gorea2Flags.Bit15;
                _field22C = _field236;
                if (_field23C == 0)
                {
                    _field23C = Math.Max(_field22C, 15 * 2); // todo: FPS stuff
                }
            }
        }

        private void SearchAndTeleport(int mode, bool checkCollision)
        {
            TriggerVolumeEntity? trigger = FindTrigger(mode, checkCollision);
            if (trigger != null)
            {
                Teleport(trigger, null);
            }
            else
            {
                float y = Position.Y;
                Vector3 position = (-Position).WithY(y);
                Teleport(null, position);
            }
        }

        private TriggerVolumeEntity? FindTrigger(int mode, bool checkCollision)
        {
            TriggerVolumeEntity? minTrigger = null;
            TriggerVolumeEntity? maxTrigger = null;
            TriggerVolumeEntity? randTrigger = null;
            float minDist = Single.MaxValue;
            float maxDist = 0;
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit14 | Gorea2Flags.Bit15)) >> 14;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.TriggerVolume)
                {
                    continue;
                }
                var trigger = (TriggerVolumeEntity)entity;
                if (trigger.Data.ParentMessage != Message.Gorea2Trigger
                    && trigger.Data.ChildMessage != Message.Gorea2Trigger)
                {
                    continue;
                }
                if (value != 1 && randTrigger == null)
                {
                    randTrigger = trigger;
                }
                if (trigger == _currentTrigger)
                {
                    continue;
                }
                Vector3 triggerPos = trigger.Data.Header.Position.ToFloatVector();
                Vector3 between = PlayerEntity.Main.Position - triggerPos;
                float lengthSquared = between.LengthSquared;
                bool randomChance = (Rng.GetRandomInt2(255) & 1) != 0;
                if (value != 1 || triggerPos.Y >= _spawnerField38)
                {
                    if (lengthSquared < minDist)
                    {
                        minTrigger = trigger;
                        minDist = lengthSquared;
                    }
                    if (lengthSquared > maxDist)
                    {
                        maxTrigger = trigger;
                        maxDist = lengthSquared;
                    }
                    if (randomChance && (value != 1 || randTrigger == null))
                    {
                        randTrigger = trigger;
                    }
                }
            }
            TriggerVolumeEntity? chosen = null;
            if (mode == 0)
            {
                chosen = randTrigger;
            }
            else if (mode == 1)
            {
                chosen = maxTrigger;
            }
            else if (mode == 2)
            {
                chosen = minTrigger;
            }
            if (chosen != null)
            {
                Vector3 chosenPos = chosen.Data.Header.Position.ToFloatVector();
                CollisionResult discard = default;
                if (checkCollision && CollisionDetection.CheckBetweenPoints(chosenPos, PlayerEntity.Main.Position,
                    TestFlags.None, _scene, ref discard))
                {
                    return null;
                }
                return chosen;
            }
            return null;
        }

        private void State09()
        {
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                SearchAndTeleport(mode: 1, checkCollision: false);
                uint timer = Rng.GetRandomInt2(300) + 300;
                _field236 = (int)timer * 2; // todo: FPS stuff
                GoreaFlags |= Gorea2Flags.Bit13;
                GoreaFlags |= Gorea2Flags.Bit17;
                _model.SetAnimation(7);
            }
        }

        private void State10()
        {
            State02();
        }

        private void State11()
        {
            State03();
        }

        private void State12()
        {
            State04();
        }

        private void State13()
        {
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
                _field236 = 0;
            }
        }

        private void State14()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit17))
            {
                GoreaFlags &= ~Gorea2Flags.Bit16;
                GoreaFlags &= ~Gorea2Flags.Bit17;
                _model.SetAnimation(8, AnimFlags.NoLoop);
            }
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
                SearchAndTeleport(mode: 0, checkCollision: false);
                _model.SetAnimation(7);
            }
        }

        private void State15()
        {
            if (CallSubroutine(Metadata.Enemy31Subroutines, this))
            {
                GoreaFlags |= Gorea2Flags.Bit17;
                _model.SetAnimation(7);
            }
        }

        private void State16()
        {
            State15();
        }

        private void State17()
        {
            if (!GoreaFlags.TestFlag(Gorea2Flags.Bit12))
            {
                GoreaFlags |= Gorea2Flags.Bit12;
                _scanId = 0;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.Invincible;
                Flags |= EnemyFlags.NoHomingNc;
                Flags |= EnemyFlags.NoHomingCo;
                Flags &= ~EnemyFlags.OnRadar;
                _health = 1;
                _sealSphere.SetDead();
                // todo: movie/credits
                _scene.SetFade(FadeType.FadeOutWhite, length: 60 / 30f, overwrite: true, AfterFade.EnterShip);
            }
            if (Behavior03())
            {
                _model.SetAnimation(7);
            }
        }

        private void State18()
        {
            State02();
            // in-game, Behavior01 (the only one under State18) sets the next ID directly on the
            // metadata struct. we have to wait for CallSubroutine to finish first, then set it here.
            _state2 = _field243;
        }

        private void InitTrigger()
        {
            var volume = new CollisionVolume(_spawnerField28, _spawnerField34);
            if (volume.TestPoint(Position))
            {
                Func213D7C4();
            }
            else
            {
                TriggerVolumeEntity? found = null;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.TriggerVolume)
                    {
                        var trigger = (TriggerVolumeEntity)entity;
                        if ((trigger.Data.ParentMessage == Message.Gorea2Trigger
                            || trigger.Data.ChildMessage == Message.Gorea2Trigger)
                            && trigger.Volume.TestPoint(Position))
                        {
                            found = trigger;
                            break;
                        }
                    }
                }
                if (found != null)
                {
                    _currentTrigger = found;
                    GoreaFlags |= Gorea2Flags.Bit4 | Gorea2Flags.Bit5;
                    _field22C = 90 * 2; // tood: FPS stuff
                }
            }
        }

        // todo: member name
        private void Func213D7C4()
        {
            if (!GoreaFlags.TestAny(Gorea2Flags.Bit4 | Gorea2Flags.Bit5 | Gorea2Flags.Bit10))
            {
                Func213D80C();
            }
        }

        // todo: member name
        private void Func213D80C()
        {
            Vector3 vec = Func21405FC();
            Vector3 between = _field20C - vec;
            if (between.LengthSquared > 1 / (3 * 2f)) // todo: FPS stuff
            {
                vec = Func213F9B8(vec).Normalized();
                _field20C += vec * (1 / (3 * 2f)); // todo: FPS stuff
            }
            else
            {
                _field20C = vec;
            }
            Vector3 toPos = (_field20C - _spawnerField28).Normalized();
            _field20C = _spawnerField28 + toPos * _spawnerField34; // sktodo: FPS stuff?
            Vector3 toPlayer = Position - PlayerEntity.Main.Position;
            if (toPlayer.LengthSquared > 1 / 128f)
            {
                toPlayer = toPlayer.Normalized();
            }
            Func213D974(toPlayer);
        }

        // todo: member name
        private Vector3 Func213F9B8(Vector3 vec)
        {
            Vector3 between1 = vec - _field20C;
            Vector3 between2 = _field20C - _spawnerField28;
            vec = Func204D518(between1, between2);
            if (vec.LengthSquared >= 1 / 128f)
            {
                return vec;
            }
            return Func213FA58();
        }

        // todo: member name (note: this is copied from Quadtroid)
        private Vector3 Func204D518(Vector3 vec, Vector3 axis)
        {
            return vec - Func204D57C(vec, axis);
        }

        // todo: member name (note: this is copied from Quadtroid)
        private Vector3 Func204D57C(Vector3 vec, Vector3 axis)
        {
            float dot1 = Vector3.Dot(axis, axis);
            if (dot1 == 0)
            {
                return Vector3.Zero;
            }
            float dot2 = Vector3.Dot(vec, axis);
            return axis * dot2 / dot1;
        }

        private Vector3 Func213FA58()
        {
            Vector3 between = _field20C - _spawnerField28;
            Vector3 unitVec = Vector3.UnitY;
            Vector3 vec = Func204D518(between, unitVec);
            if (vec.LengthSquared < 1 / 128f)
            {
                unitVec = Vector3.UnitX;
                vec = Func204D518(between, unitVec);
            }
            return Vector3.Cross(vec, unitVec);
        }

        // todo: member name
        private void Func213D974(Vector3 toPlayer)
        {
            if (!PlayerEntity.Main.Field6D0)
            {
                toPlayer = -toPlayer;
            }
            Vector3 vec = Func204D518(toPlayer, UpVector);
            if (vec.LengthSquared > 0)
            {
                _field1E8 = vec.Normalized();
                _field1E8 = SeekTargetSetAnim(_field1E8, _models[0].AnimInfo.Index[0]);
            }
        }

        // todo: member name
        private Vector3 Func21405FC()
        {
            Vector3 toPos = PlayerEntity.Main.Position - _spawnerField28;
            if (toPos.LengthSquared <= 1 / 128f)
            {
                // the game might return an uninitialized/previous value here
                return Vector3.Zero;
            }
            toPos = toPos.Normalized();
            return Func204E2A8(_spawnerField28, toPos, _spawnerField34);
        }

        // todo: member name
        private Vector3 Func204E2A8(Vector3 vec1, Vector3 vec2, float a4)
        {
            float dot1 = vec2.LengthSquared;
            float v10 = -(dot1 * -(a4 * a4) * 4);
            if (v10 < 0)
            {
                // the game might return an uninitialized/previous value here
                return Vector3.Zero;
            }
            float v11 = 0;
            if (v10 > 4)
            {
                float v12 = MathF.Sqrt(v10);
                float v15 = v12 / (dot1 * 2);
                float v17 = -v12 / (dot1 * 2);
                v11 = v15;
                if (v17 > 0 && v17 < v15)
                {
                    v11 = v17;
                }
            }
            return vec1 + vec2 * v11;
        }

        // todo: member name, flag names
        public bool Func214080C()
        {
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
            return value == 1 || value == 2;
        }

        public void UpdatePhase()
        {
            if (_sealSphere.Damage <= 210)
            {
                GoreaFlags &= ~(Gorea2Flags.Bit14 | Gorea2Flags.Bit15);
            }
            else if (_sealSphere.Damage <= 503)
            {
                GoreaFlags &= ~(Gorea2Flags.Bit14 | Gorea2Flags.Bit15);
                GoreaFlags |= Gorea2Flags.Bit14;
            }
            else
            {
                GoreaFlags &= ~(Gorea2Flags.Bit14 | Gorea2Flags.Bit15);
                GoreaFlags |= Gorea2Flags.Bit15;
            }
        }

        private bool BehaviorXX()
        {
            return AnimationEnded();
        }

        private bool Behavior00()
        {
            return true;
        }

        private bool Behavior01()
        {
            // see explanation in State18
            return true;
        }

        private bool Behavior02()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit11))
            {
                SpawnEffect(72, _sealSphere.Position); // goreaBallExplode2
                // mustodo: stop music
                _soundSource.PlaySfx(SfxId.GOREA2_DEATH_SCR, sourceOnly: true);
                return true;
            }
            return false;
        }

        private bool Behavior03()
        {
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
            return value == 0 || value == 3;
        }

        private bool Behavior04()
        {
            if (GoreaFlags.TestFlag(Gorea2Flags.Bit16))
            {
                _field243 = _state1;
                return true;
            }
            return false;
        }

        private bool Behavior05()
        {
            int v5 = _sealSphere.Damage switch
            {
                > 720 => 6,
                > 600 => 5,
                > 480 => 4,
                > 360 => 3,
                > 240 => 2,
                > 120 => 1,
                _ => 0,
            };
            if (v5 != 0 && (_field244 & (1 << (v5 - 1))) != 0)
            {
                _field244 &= (byte)~(2 * (1 << (v5 - 1)) - 1);
                _field232 = 0;
                _field243 = _state1;
                _soundSource.StopSfx(SfxId.GOREA2_DAMAGE1);
                _soundSource.PlaySfx(SfxId.GOREA2_DAMAGE2_SCR);
                return true;
            }
            return false;
        }

        private bool Behavior06()
        {
            if (_field232 > 0)
            {
                _field232--;
                if (_field232 == 0)
                {
                    // this returns 32, instead of 8, for 0 (which the game might also do?), but we clamp to 6 anyway
                    int index = System.Numerics.BitOperations.TrailingZeroCount(_field244);
                    if (index > 6)
                    {
                        index = 6;
                    }
                    if (index != 0)
                    {
                        _field244 |= (byte)(1 << (index - 1));
                        // bugfix/feature?: due to the fact that field232 is never set to a non-zero value,
                        // this code never executes and regen never occurs -- we could implement it
                        _field232 = 0;
                        _sealSphere.Damage = 120 * (index - 1);
                        UpdatePhase();
                        _soundSource.PlaySfx(SfxId.GOREA2_REGEN);
                        _model.SetAnimation(7, AnimFlags.NoLoop);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Behavior07()
        {
            if (_models[0].AnimInfo.Index[0] == 0)
            {
                return _models[0].AnimInfo.Frame[0] >= 26;
            }
            if (_models[0].AnimInfo.Index[0] != 4)
            {
                _model.SetAnimation(4, AnimFlags.NoLoop);
            }
            else if (IsAtEndFrame())
            {
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK1A);
                _model.SetAnimation(0, AnimFlags.NoLoop);
            }
            return false;
        }

        private bool Behavior08()
        {
            return !_sealSphere.Visible;
        }

        private bool Behavior09()
        {
            if (Func2140844())
            {
                return false;
            }
            if (Behavior11())
            {
                _field234 = 22 * 2; // todo: FPS stuff
            }
            else
            {
                if (_field234 > 0)
                {
                    _field234--;
                }
                if (_field234 == 0)
                {
                    int value = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
                    if (value == 0 || value == 3)
                    {
                        TriggerVolumeEntity? trigger = FindTrigger(mode: 2, checkCollision: true);
                        if (trigger != null)
                        {
                            Teleport(trigger, null);
                            _field234 = _state1;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // todo: member name
        private bool Func2140844()
        {
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit14 | Gorea2Flags.Bit15)) >> 14;
            if (value != 0 && GoreaFlags.TestFlag(Gorea2Flags.Bit9))
            {
                return PlayerEntity.Main.CurrentWeapon == BeamType.OmegaCannon;
            }
            return false;
        }

        private bool Behavior10()
        {
            if (_field230 > 0)
            {
                _field230--;
            }
            if (_field230 == 0)
            {
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK1B);
                GoreaFlags &= ~Gorea2Flags.LaserActive;
                _field230 = 0;
                return true;
            }
            return false;
        }

        private bool Behavior11()
        {
            return _sealSphere.Visible;
        }

        private bool Behavior12()
        {
            if (_field230 > 0)
            {
                _field230--;
            }
            if (_field230 == 0)
            {
                float dist = (PlayerEntity.Main.Position - Position).Length;
                if (dist < (PlayerEntity.Main.Field6D0 ? 130 : 50))
                {
                    return true;
                }
            }
            return false;
        }

        private bool Behavior13()
        {
            if (Func2140844() && !Func214080C())
            {
                _sealSphere.Flags |= EnemyFlags.Invincible;
                _model.SetAnimation(10, AnimFlags.NoLoop);
                SpawnEffect(225, _sealSphere.Position); // goreaHurt
                GoreaFlags |= Gorea2Flags.Bit10;
                return true;
            }
            return false;
        }

        private bool Behavior14()
        {
            if (Behavior11())
            {
                return false;
            }
            bool result = Func2140390();
            if (result)
            {
                _soundSource.PlaySfx(SfxId.GOREA2_ATTACK2_SCR);
            }
            return result;
        }

        // todo: member name
        private bool Func2140390()
        {
            if (Func21403FC())
            {
                if (_field22E > 0)
                {
                    _field22E--;
                }
                if (_field22E == 0 && _field242 < 2)
                {
                    _field22E = 120 * 2; // todo: FPS stuff
                    Func2140414();
                    _field243 = _state1;
                    return true;
                }
            }
            return false;
        }

        // todo: member name
        private bool Func21403FC()
        {
            int value = (int)(GoreaFlags & (Gorea2Flags.Bit14 | Gorea2Flags.Bit15)) >> 14;
            return value != 0;
        }

        // todo: member name
        private void Func2140414()
        {
            int animId = 3;
            GoreaFlags &= ~Gorea2Flags.Bit8;
            if (Rng.GetRandomInt2(2) == 1)
            {
                GoreaFlags |= Gorea2Flags.Bit8;
                animId = 2;
            }
            _model.SetAnimation(animId, AnimFlags.NoLoop);
        }

        private bool Behavior15()
        {
            if (_sealSphere.Visible)
            {
                return !Func214080C();
            }
            return false;
        }

        private bool Behavior16()
        {
            bool result = !Func2140844();
            if (result)
            {
                GoreaFlags &= ~Gorea2Flags.Bit10;
            }
            return result;
        }

        // todo: this is the same as Behavior09, except a different
        // function is called at at the start and checkCollision is false
        private bool Behavior17()
        {
            if (Func21403FC())
            {
                return false;
            }
            if (Behavior11())
            {
                _field234 = 22 * 2; // todo: FPS stuff
            }
            else
            {
                if (_field234 > 0)
                {
                    _field234--;
                }
                if (_field234 == 0)
                {
                    int value = (int)(GoreaFlags & (Gorea2Flags.Bit4 | Gorea2Flags.Bit5)) >> 4;
                    if (value == 0 || value == 3)
                    {
                        TriggerVolumeEntity? trigger = FindTrigger(mode: 2, checkCollision: false);
                        if (trigger != null)
                        {
                            Teleport(trigger, null);
                            _field234 = _state1;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        #region Boilerplate

        public static bool BehaviorXX(Enemy31Entity enemy)
        {
            return enemy.BehaviorXX();
        }

        public static bool Behavior00(Enemy31Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy31Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy31Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy31Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy31Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy31Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy31Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy31Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy31Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy31Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy31Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy31Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy31Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy31Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy31Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy31Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy31Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy31Entity enemy)
        {
            return enemy.Behavior17();
        }

        #endregion
    }

    [Flags]
    public enum Gorea2Flags : uint
    {
        None = 0,
        Bit0 = 1,
        LaserActive = 2,
        LaserBlocked = 4,
        LaserOnTarget = 8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        Bit23 = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }
}
