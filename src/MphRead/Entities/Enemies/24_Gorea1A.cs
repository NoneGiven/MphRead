using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Effects;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class GoreaEnemyEntityBase : EnemyInstanceEntity
    {
        protected ModelInstance _model = null!;

        protected const SetFlags _animSetNoMat = SetFlags.Texture | SetFlags.Texcoord | SetFlags.Unused | SetFlags.Node;

        public GoreaEnemyEntityBase(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        // skdebug
        private bool _write = true;

        // skdebug
        protected void Write(string message)
        {
            if (_write)
            {
                Debug.Write(message);
            }
        }

        // skdebug
        protected void WriteLine(string message)
        {
            if (_write)
            {
                Debug.WriteLine(message);
            }
        }

        public void InitializeCommon(EnemySpawnEntity spawner)
        {
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 3;
            SetTransform(spawner.FacingVector, Vector3.UnitY, spawner.Position);
            _boundingRadius = 1;
            _healthMax = _health = UInt16.MaxValue;
        }

        protected bool AnimationEnded()
        {
            return _model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended);
        }

        protected void SpawnEffect(int effectId, Vector3 position)
        {
            SpawnEffect(effectId, position, Vector3.UnitX, Vector3.UnitY);
        }

        protected void SpawnEffect(int effectId, Vector3 position, Vector3 facing, Vector3 up)
        {
            _scene.SpawnEffect(effectId, facing, up, position);
        }

        protected EffectEntry? SpawnEffectGetEntry(int effectId, Vector3 position, bool extensionFlag)
        {
            EffectEntry? effect = _scene.SpawnEffectGetEntry(effectId, Vector3.UnitX, Vector3.UnitY, position);
            if (effect != null)
            {
                effect.ResetElements(_scene.ElapsedTime);
                effect.SetElementExtension(extensionFlag);
            }
            return effect;
        }

        protected ulong? _lastNodeTransformUpdate = null;

        // the game returns a kind of awkward in-between value where it uses the node transform that was animated as of the previous frame,
        // but multiplies in the parent transform that has (likely) already been updated on the current frame. this would annoying for
        // us to replicate, so instead we just do an early model update here and grab everything as of its current/final values for the frame.
        // in theory, this might not reflect the actual final position of things, but in practice it is since Gorea1A/1B/2 will have finished
        // processing before the head/arm/leg/sphere is calling this to update itself. a counter is used to prevent redundant updates on one frame,
        // which does assume that everything is already final on the parent as of the time when this function is first called.
        // there will be one more (likely redundant) update to the animation during the normal draw info call.
        protected Matrix4 GetNodeTransform(GoreaEnemyEntityBase entity, Node node)
        {
            if (entity._lastNodeTransformUpdate != _scene.FrameCount)
            {
                entity._lastNodeTransformUpdate = _scene.FrameCount;
                ModelInstance? model = entity.GetModels().FirstOrDefault();
                Debug.Assert(model != null && model.Model.Nodes.Contains(node));
                Matrix4 transform = entity.GetModelTransform(model, index: 0);
                model.Model.AnimateNodes(index: 0, false, transform, model.Model.Scale, model.AnimInfo);
            }
            return node.Animation;
        }

        // this also requires calling the animation early, but with some non-final values. this will be called once
        // within the enemy in question, and then possibly overwritten later when subsequent entities call the above function.
        protected void TransformHurtVolumeToNode(Node node, Vector3 offset)
        {
            Debug.Assert(_model != null && _model.Model.Nodes.Contains(node));
            _model.Model.AnimateNodes(index: 0, false, Matrix4.Identity, _model.Model.Scale, _model.AnimInfo);
            Matrix4 transform = GetTransformMatrix(FacingVector, UpVector);
            Vector3 position = Matrix.Vec3MultMtx4(offset, node.Animation * Matrix4.CreateScale(Scale) * transform);
            _hurtVolumeInit = new CollisionVolume(position, _hurtVolumeInit.SphereRadius);
        }

        protected bool SeekTargetFacing(Vector3 target, float angle)
        {
            angle = MathHelper.DegreesToRadians(angle / 2f); // todo: FPS stuff
            (float sin, float cos) = MathF.SinCos(angle);
            Vector3 facing = FacingVector;
            float dot = Vector3.Dot(target, facing);
            if (MathF.Abs(cos - dot) > Fixed.ToFloat(7))
            {
                var cross = Vector3.Cross(target, facing);
                var rotY = Matrix4.CreateRotationY(angle * (cross.Y > 0 ? -1 : 1));
                facing = Matrix.Vec3MultMtx4(facing, rotY).Normalized();
                SetTransform(facing, UpVector, Position);
                return false;
            }
            SetTransform(target.Normalized(), UpVector, Position);
            return true;
        }

        protected Vector3 SeekTargetSetAnim(Vector3 target, int index)
        {
            return SeekTargetSetAnim(target, index, slot: 0, setFlags: SetFlags.Unused, useSlot: true);
        }

        protected Vector3 SeekTargetSetAnim(Vector3 target, int index, int slot, SetFlags setFlags)
        {
            return SeekTargetSetAnim(target, index, slot, setFlags, useSlot: true);
        }

        private Vector3 SeekTargetSetAnim(Vector3 target, int index, int slot, SetFlags setFlags, bool useSlot)
        {
            if (target != Vector3.Zero && SeekTargetFacing(target, 3))
            {
                target = Vector3.Zero;
                if (useSlot)
                {
                    EnsureAnimation(index, slot, setFlags);
                }
                else
                {
                    EnsureAnimation(index);
                }
            }
            if (target == Vector3.Zero)
            {
                Vector3 between = (PlayerEntity.Main.Position - Position).WithY(0);
                if (between.LengthSquared > 1 / 128f)
                {
                    between = between.Normalized();
                    if (Vector3.Dot(between, FacingVector) < Fixed.ToFloat(3547))
                    {
                        target = between;
                        if (useSlot)
                        {
                            EnsureAnimation(index, slot, setFlags);
                        }
                        else
                        {
                            EnsureAnimation(index);
                        }
                    }
                }
            }
            return target;
        }

        protected bool CheckFacingAngle(float minCos, Vector3 position)
        {
            Vector3 facing = FacingVector.WithY(0);
            if (facing.LengthSquared > 1 / 128f)
            {
                facing = facing.Normalized();
                Vector3 between = (position - Position).WithY(0);
                if (between.LengthSquared > 1 / 128f)
                {
                    between = between.Normalized();
                    if (Vector3.Dot(between, facing) > minCos)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected void EnsureAnimation(int index, AnimFlags animFlags = AnimFlags.None)
        {
            if (_model.AnimInfo.Index[0] != index)
            {
                _model.SetAnimation(index, animFlags);
            }
        }

        protected void EnsureAnimation(int index, int slot, SetFlags setFlags, AnimFlags animFlags = AnimFlags.None)
        {
            if (_model.AnimInfo.Index[slot] != index)
            {
                _model.SetAnimation(index, slot, setFlags, animFlags);
            }
        }

        protected bool IsAtEndFrame()
        {
            return _scene.FrameCount > 0 && _scene.FrameCount % 2 == 0 // todo: FPS stuff
                && _model.AnimInfo.Frame[0] >= _model.AnimInfo.FrameCount[0] - 1;
        }

        protected void IncrementMaterialColors(Material material, ColorRgb ambient, ColorRgb diffuse, int frame, int frameCount)
        {
            material.Ambient = new ColorRgb(
                (byte)(material.Ambient.Red + InterpolateColor(frame, frameCount, ambient.Red - material.Ambient.Red)),
                (byte)(material.Ambient.Green + InterpolateColor(frame, frameCount, ambient.Green - material.Ambient.Green)),
                (byte)(material.Ambient.Blue + InterpolateColor(frame, frameCount, ambient.Blue - material.Ambient.Blue)));
            material.Diffuse = new ColorRgb(
                (byte)(material.Diffuse.Red + InterpolateColor(frame, frameCount, diffuse.Red - material.Diffuse.Red)),
                (byte)(material.Diffuse.Green + InterpolateColor(frame, frameCount, diffuse.Green - material.Diffuse.Green)),
                (byte)(material.Diffuse.Blue + InterpolateColor(frame, frameCount, diffuse.Blue - material.Diffuse.Blue)));
        }

        // todo: once material colors (and alpha) are all using floats early, update this math to use floats
        protected byte InterpolateColor(int frame, int frameCount, int color)
        {
            int diff = frameCount - frame;
            if (frame >= 0 && frame <= frameCount && diff != 0)
            {
                return (byte)MathF.Round(color / (float)diff);
            }
            return (byte)color;
        }

        protected bool _lightOverride = false;

        protected override LightInfo GetLightInfo()
        {
            if (_lightOverride)
            {
                return new LightInfo(_scene.Light1Vector, _scene.Light1Color, _scene.Light2Vector, Vector3.One);
            }
            return base.GetLightInfo();
        }
    }

    public class Enemy24Entity : GoreaEnemyEntityBase
    {
        private readonly EnemySpawnEntity _spawner = null!;
        public EnemySpawnEntity Spawner => _spawner;
        private Gorea1AFlags _goreaFlags;
        private ModelInstance _regenModel = null!;
        private Node _spineNode = null!;
        private CollisionVolume _volume; // arena center platform
        private Enemy28Entity _gorea1B = null!;
        private Enemy25Entity _head = null!;
        private int _armBits = 0;
        private readonly Enemy26Entity[] _arms = new Enemy26Entity[2];
        private readonly Enemy27Entity[] _legs = new Enemy27Entity[3];

        private IReadOnlyList<ColorRgb> _colors = null!;
        public IReadOnlyList<ColorRgb> Colors => _colors;

        private int _weaponIndex = 5;
        public int WeaponIndex => _weaponIndex;
        private float _speedFactor = 0;
        private Vector3 _targetFacing;
        private int _field23C = 0; // sktodo: field names
        private int _field23E = 0;
        private int _field240 = 0;
        private int _field244 = 0;
        private byte _nextState = 0;
        private int _field242 = 0;

        public Enemy24Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[15]
            {
                State00, State01, State02, State03, State04,
                State05, State06, State07, State08, State09,
                State10, State11, State12, State13, State14
            };
        }

        protected override void EnemyInitialize()
        {
            InitializeCommon(_spawner);
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.OnRadar;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, Fixed.ToFloat(4098));
            Scale = new Vector3(1.5f); // 6144
            _model = SetUpModel("Gorea1A_lod0");
            _model.NodeAnimIgnoreRoot = true;
            _model.Model.ComputeNodeMatrices(index: 0);
            _model.SetAnimation(17, 0, _animSetNoMat);
            _model.SetAnimation(26, 1, SetFlags.Material);
            _regenModel = SetUpModel("goreaArmRegen");
            _regenModel.Active = false;
            _spineNode = _model.Model.GetNodeByName("Spine_02")!;
            _volume = CollisionVolume.Move(new CollisionVolume(
                _spawner.Data.Fields.S11.Sphere1Position.ToFloatVector(),
                _spawner.Data.Fields.S11.Sphere1Radius.FloatValue), Position);
            UpdateSpeed();
            _field23C = 210 * 2; // todo: FPS stuff
            _field23E = 510 * 2; // todo: FPS stuff
            _field240 = (int)(Rng.GetRandomInt2(90) + 150) * 2; // todo: FPS stuff
            _goreaFlags |= Gorea1AFlags.Bit0;
            _goreaFlags |= Gorea1AFlags.Bit2;
            ResetMaterialColors();
            SpawnHead();
            SpawnArms();
            SpawnLegs();
            SpawnGorea1B();
            ChangeWeapon();
        }

        private void SpawnHead()
        {
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaHead, NodeRef, _scene) is Enemy25Entity head)
            {
                _scene.AddEntity(head);
                _head = head;
            }
        }

        private void SpawnArms()
        {
            for (int i = 0; i < 2; i++)
            {
                if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaArm, NodeRef, _scene) is Enemy26Entity arm)
                {
                    arm.Index = i;
                    _scene.AddEntity(arm);
                    _arms[i] = arm;
                    _armBits |= 1 << i;
                }
            }
        }

        private void SpawnLegs()
        {
            for (int i = 0; i < 3; i++)
            {
                if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaLeg, NodeRef, _scene) is Enemy27Entity leg)
                {
                    leg.Index = i;
                    _scene.AddEntity(leg);
                    _legs[i] = leg;
                }
            }
        }

        private void SpawnGorea1B()
        {
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.Gorea1B, NodeRef, _scene) is Enemy28Entity gorea1B)
            {
                _scene.AddEntity(gorea1B);
                _gorea1B = gorea1B;
            }
        }

        public void Activate()
        {
            _scanId = Metadata.EnemyScanIds[(int)EnemyType];
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags |= EnemyFlags.OnRadar;
            _targetFacing = Vector3.Zero;
            _head.Flags |= EnemyFlags.CollidePlayer;
            _head.Flags |= EnemyFlags.CollideBeam;
            _head.Flags |= EnemyFlags.NoHomingNc;
            _head.Flags |= EnemyFlags.NoHomingCo;
            for (int i = 0; i < 2; i++)
            {
                _armBits |= 1 << i;
                _arms[i].Activate();
            }
            _speed = Vector3.Zero;
            _model.SetAnimation(0, 0, _animSetNoMat, AnimFlags.NoLoop);
            _soundSource.PlaySfx(SfxId.GOREA_REGEN_ARM_SCR);
            for (int i = 0; i < 3; i++)
            {
                Enemy27Entity leg = _legs[i];
                leg.Flags |= EnemyFlags.CollidePlayer;
                leg.Flags |= EnemyFlags.CollideBeam;
                leg.Flags |= EnemyFlags.NoHomingNc;
                leg.Flags |= EnemyFlags.NoHomingCo;
                leg.SetKneeNode(this);
            }
            _goreaFlags |= Gorea1AFlags.Bit2;
            UpdateSpeed();
            SetTransform(_gorea1B.FacingVector, UpVector, _gorea1B.Position);
        }

        private void ChangeWeapon()
        {
            if (++_weaponIndex >= 6)
            {
                _weaponIndex = 0;
            }
            _colors = Metadata.Enemy24Colors[_weaponIndex];
            // todo: update music tracks
            WeaponInfo weapon = Weapons.GoreaWeapons[_weaponIndex];
            int effectiveness = Metadata.GoreaEffectiveness[_weaponIndex];
            for (int i = 0; i < 2; i++)
            {
                Enemy26Entity arm = _arms[i];
                arm.UpdateWeapon(weapon);
                Metadata.LoadEffectiveness(effectiveness, arm.BeamEffectiveness);
            }
            Metadata.LoadEffectiveness(effectiveness, _gorea1B.SealSphere.BeamEffectiveness);
        }

        private readonly IReadOnlyList<string> _armMatNames = new string[6 * 2]
        {
            "L_Bisep", "L_GunArm", "L_GunTipBottom", "L_GunTipSide", "L_GunTipTop", "L_ShoulderTarget",
            "R_Bisep", "R_GunArm", "R_GunTipBottom", "R_GunTipSide", "R_GunTipTop", "R_ShoulderTarget"
        };

        private readonly IReadOnlyList<string> _bodyMatNames1 = new string[10]
        {
            "ChestMembrane", "Eye", "Head1", "L_GunArm", "L_Bisep",
            "R_GunArm", "R_Bisep", "Legs", "Torso", "Shoulder"
        };

        private readonly IReadOnlyList<string> _bodyMatNames2 = new string[2]
        {
            "ChestCore", "HeadFullLit"
        };

        protected override void EnemyProcess()
        {
            WriteLine("---------------------------------");
            WriteLine($"frame {_scene.FrameCount}");
            if (!Flags.TestFlag(EnemyFlags.Visible))
            {
                return;
            }
            if (_field23E >= 0 && (_arms[0].ArmFlags.TestFlag(GoreaArmFlags.Bit0)
                || _arms[1].ArmFlags.TestFlag(GoreaArmFlags.Bit0)))
            {
                _field23E--;
            }
            if (_field240 >= 0 && (_armBits & 3) == 3)
            {
                _field240--;
            }
            IncrementAllMaterialColors();
            CheckPlayerCollision();
            CallStateProcess();
            if (Active)
            {
                UpdateAnimFrames(_model);
            }
            // sktodo: this seems much better aligned with the offset in X instead of Z. confirm if it's actually off-center in-game
            TransformHurtVolumeToNode(_spineNode, new Vector3(0, Fixed.ToFloat(970), Fixed.ToFloat(622)));
        }

        protected override bool BaseProcess()
        {
            // animation frames are updated before moving the volume
            return true;
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            _health = UInt16.MaxValue;
            return true;
        }

        private void IncrementAllMaterialColors()
        {
            // todo: FPS stuff
            int frame = _model.AnimInfo.Frame[0] * 2 + (int)(_scene.FrameCount % 2);
            int frameCount = _model.AnimInfo.FrameCount[0] * 2;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 2; j < 5; j++)
                {
                    Material material = _model.Model.GetMaterialByName(_armMatNames[i * 6 + j])!;
                    IncrementMaterialColors(material, _colors[0], _colors[1], frame, frameCount);
                }
            }
            for (int i = 0; i < 10; i++)
            {
                Material material = _model.Model.GetMaterialByName(_bodyMatNames1[i])!;
                IncrementMaterialColors(material, _colors[2], _colors[3], frame, frameCount);
            }
            for (int i = 0; i < 2; i++)
            {
                Material material = _model.Model.GetMaterialByName(_bodyMatNames2[i])!;
                IncrementMaterialColors(material, _colors[0], _colors[1], frame, frameCount);
            }
        }

        // sktodo: this is the same as the leg with the values baked in -- share code?
        private void CheckPlayerCollision()
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return;
            }
            Vector3 between = (PlayerEntity.Main.Position - Position).WithY(0);
            between = between.LengthSquared > 1 / 128f
                ? between.Normalized()
                : FacingVector;
            PlayerEntity.Main.Speed += between / 4;
            PlayerEntity.Main.TakeDamage(10, DamageFlags.None, null, this);
        }

        private void State00()
        {
            WriteLine("state 0");
            if (_goreaFlags.TestFlag(Gorea1AFlags.Bit2))
            {
                for (int i = 0; i < 2; i++)
                {
                    _arms[i].Flags |= EnemyFlags.Invincible;
                }
                _goreaFlags &= ~Gorea1AFlags.Bit2;
                _model.SetAnimation(0, 0, _animSetNoMat, AnimFlags.NoLoop);
                _head.RespawnFlashEffect();
            }
            if (_goreaFlags.TestFlag(Gorea1AFlags.Bit0))
            {
                _goreaFlags &= ~Gorea1AFlags.Bit0;
                SpawnEffect(175, Position); // goreaReveal
            }
            if (_model.AnimInfo.Index[0] == 0)
            {
                UpdateArmMaterialAlpha();
                if (AnimationEnded())
                {
                    if (_gorea1B.PhasesLeft != 3)
                    {
                        ChangeWeapon();
                        // todo: update music tracks
                    }
                    _soundSource.PlaySfx(SfxId.GOREA_ROAR_SCR);
                    _model.SetAnimation(22, 0, _animSetNoMat, AnimFlags.NoLoop);
                }
            }
            if (CallSubroutine(Metadata.Enemy24Subroutines, this))
            {
                for (int i = 0; i < 2; i++)
                {
                    _arms[i].Flags &= ~EnemyFlags.Invincible;
                }
                _field240 = (int)(Rng.GetRandomInt2(90) + 150) * 2; // todo: FPS stuff
            }
        }

        private void UpdateArmMaterialAlpha()
        {
            for (int i = 0; i < 2; i++)
            {
                Material? material = _model.Model.GetMaterialByName(i == 0 ? "L_Bisep" : "R_Bisep")!;
                if (material.CurrentAlpha < 1) // sktodo: make sure checking this works
                {
                    int frame = _model.AnimInfo.Frame[0] - 10;
                    if (frame >= 0)
                    {
                        float alpha = frame / (float)(_model.AnimInfo.FrameCount[0] - 11) * 31;
                        SetArmMaterialAlpha(i, (byte)MathF.Round(alpha));
                    }
                }
            }
        }

        private void SetArmMaterialAlpha(int index, byte alpha)
        {
            for (int i = index * 6; i < index * 6 + 6; i++)
            {
                _model.Model.GetMaterialByName(_armMatNames[i])!.Alpha = alpha;
            }
        }

        private void State01()
        {
            WriteLine("state 1");
            if (_targetFacing != Vector3.Zero)
            {
                EnsureAnimation(24, 0, _animSetNoMat);
                if (SeekTargetFacing(_targetFacing, 3))
                {
                    _targetFacing = Vector3.Zero;
                }
            }
            else
            {
                EnsureAnimation(17, 0, _animSetNoMat);
                UpdateTargetFacing();
            }
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private bool UpdateTargetFacing()
        {
            Vector3 between = (PlayerEntity.Main.Position - Position).WithY(0);
            if (between.LengthSquared > 1 / 128f)
            {
                _targetFacing = between.Normalized();
                return true;
            }
            return false;
        }

        // sktodo (document): walking
        private void State02()
        {
            WriteLine("state 2");
            if (UpdateTargetFacing())
            {
                SeekTargetFacing(_targetFacing, 3);
            }
            _speed = FacingVector.WithY(0) * _speedFactor / 2; // todo: FPS stuff
            EnsureAnimation(25, 0, _animSetNoMat);
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        // sktodo (document): sprinting
        private void State03()
        {
            WriteLine("state 3");
            _targetFacing = SeekTargetSetAnim(_targetFacing, _model.AnimInfo.Index[0], slot: 0, _animSetNoMat);
            _speed = FacingVector * _speedFactor * 5 / 2; // 20480 // todo: FPS stuff
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private void State04()
        {
            WriteLine("state 4");
            _targetFacing = SeekTargetSetAnim(_targetFacing, index: 16, slot: 0, _animSetNoMat);
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private static readonly IReadOnlyList<int> _shotEffects = new int[6]
        {
            // goreaFireJak, goreaFireElc, goreaFireMrt,
            // goreaFireIce, goreaFireSnp, goreaFireGst
            54, 51, 55, 53, 56, 52
        };

        private static readonly IReadOnlyList<BeamType> _beamTypes = new BeamType[6]
        {
            BeamType.Battlehammer,
            BeamType.VoltDriver,
            BeamType.Magmaul,
            BeamType.Judicator,
            BeamType.Imperialist,
            BeamType.PowerBeam
        };

        private void State05()
        {
            WriteLine("state 5");
            for (int i = 0; i < 2; i++)
            {
                Enemy26Entity arm = _arms[i];
                if (CheckTargeting(arm))
                {
                    GetArmAim(arm, out Vector3 spawnPos, out Vector3 spawnDir);
                    arm.Ammo = 65535;
                    BeamProjectileEntity.Spawn(arm, arm.EquipInfo, spawnPos, spawnDir, BeamSpawnFlags.None, _scene);
                    CreateShotEffectLoose(arm, _shotEffects[WeaponIndex]);
                    StopBeamChargeSfx(_beamTypes[WeaponIndex]);
                    PlayBeamShotSfx(_beamTypes[WeaponIndex], charged: arm.ArmFlags.TestFlag(GoreaArmFlags.Bit2));
                }
            }
            _targetFacing = SeekTargetSetAnim(_targetFacing, _model.AnimInfo.Index[0], slot: 0, _animSetNoMat);
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private bool CheckTargeting(Enemy26Entity arm)
        {
            int frame = _model.AnimInfo.Frame[0] * 2 + (int)(_scene.FrameCount % 2); // todo: FPS stuff
            if (WeaponIndex == 0 || WeaponIndex == 5)
            {
                if (arm.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
                {
                    return false;
                }
                bool shoot = false;
                if (arm.ArmFlags.TestFlag(GoreaArmFlags.Bit2))
                {
                    // todo: FPS stuff
                    int shotCooldown = arm.EquipInfo.Weapon.ShotCooldown * 2;
                    int autoCooldown = arm.EquipInfo.Weapon.AutofireCooldown * 2;
                    if (shotCooldown <= frame && frame <= autoCooldown
                        && (WeaponIndex == 5 || (frame % 8) == 7)) // the game does every 4, we do every 8
                    {
                        shoot = true;
                    }
                    if (_field242 > 0)
                    {
                        if (frame == autoCooldown
                            && _field242 > _model.AnimInfo.FrameCount[0] * 2 - (shotCooldown + autoCooldown))
                        {
                            _model.AnimInfo.Frame[0] = shotCooldown / 2;
                        }
                        _field242--;
                    }
                }
                else if (arm.ArmFlags.TestFlag(GoreaArmFlags.Bit1) && arm.Cooldown == frame)
                {
                    shoot = true;
                }
                return shoot;
            }
            if (!arm.ArmFlags.TestFlag(GoreaArmFlags.Bit0)
                && arm.ArmFlags.TestAny(GoreaArmFlags.Bit1 | GoreaArmFlags.Bit2)
                && frame == arm.Cooldown)
            {
                return true;
            }
            return false;
        }

        private void CreateShotEffectLoose(Enemy26Entity arm, int effectId)
        {
            arm.GetElbowNodeVectors(out Vector3 spawnPos, out Vector3 spawnFacing, out Vector3 spawnUp); // swap up and facing
            spawnFacing = spawnFacing.Normalized();
            spawnUp = spawnUp.Normalized();
            spawnPos += spawnFacing * Fixed.ToFloat(8343);
            _scene.SpawnEffect(effectId, spawnFacing, spawnUp, spawnPos);
        }

        private void GetArmAim(Enemy26Entity arm, out Vector3 position, out Vector3 direction)
        {
            arm.GetElbowNodeVectors(out position, out direction, out _);
            //direction = direction.Normalized();
            position += direction * Fixed.ToFloat(8343);
            Vector3 playerPosition = PlayerEntity.Main.Position.AddY(0.5f);
            if (!HalfturretEntity.UpdateAim(position, playerPosition, arm.EquipInfo, out direction))
            {
                _goreaFlags |= Gorea1AFlags.Bit4;
            }
        }

        private int GetBeamChargeSfx(BeamType beam)
        {
            if (beam == BeamType.Missile)
            {
                return Metadata.HunterSfx[0, (int)HunterSfx.MissileCharge];
            }
            return Metadata.BeamSfx[(int)beam, (int)BeamSfx.Charge];
        }

        private void PlayBeamChargeSfx(BeamType beam)
        {
            int sfx = GetBeamChargeSfx(beam);
            if (sfx != -1)
            {
                _soundSource.PlaySfx(sfx, loop: true);
            }
        }

        private void StopBeamChargeSfx(BeamType beam)
        {
            int sfx = GetBeamChargeSfx(beam);
            if (sfx != -1)
            {
                _soundSource.StopSfx(sfx);
            }
        }

        private void PlayBeamShotSfx(BeamType beam, bool charged)
        {
            StopBeamChargeSfx(beam);
            BeamSfx sfx;
            if (charged)
            {
                sfx = beam == Weapons.AffinityWeapons[0] ? BeamSfx.AffinityChargeShot : BeamSfx.ChargeShot;
            }
            else
            {
                sfx = BeamSfx.Shot;
            }
            int id = Metadata.BeamSfx[(int)beam, (int)sfx];
            // todo: this is a hack so the Shock Coil SFX doesn't play every frame. I'm not sure what
            // the game is doing, since other beam SFX do overlap, but Shock Coil's shot SFX
            // (using Power Beam sounds) only plays again after the previous one has finished.
            if (id != -1 && (_weaponIndex != 5 || _soundSource.CountSourcePlayingSfx(id) == 0))
            {
                _soundSource.PlaySfx(id);
            }
        }

        private void State06()
        {
            WriteLine("state 6");
            _targetFacing = SeekTargetSetAnim(_targetFacing, _model.AnimInfo.Index[0], slot: 0, _animSetNoMat);
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private static readonly IReadOnlyList<int> _weaponAnimIds = new int[6]
        {
             11, 3, 7, 4, 12, 14
        };

        private void State07()
        {
            WriteLine("state 7");
            int anim = _model.AnimInfo.Index[0];
            if (!_weaponAnimIds.Contains(anim) && anim != 8 && anim != 9 && anim != 10 && anim != 13)
            {
                ChangeWeapon();
                _model.SetAnimation(13, 0, _animSetNoMat, AnimFlags.NoLoop);
                _speed = Vector3.Zero;
            }
            if (CallSubroutine(Metadata.Enemy24Subroutines, this))
            {
                _soundSource.PlaySfx(SfxId.GOREA_WEAPON_SWITCH_SCR);
            }
        }

        // sktodo: (document) slam attack
        private void State08()
        {
            WriteLine("state 8");
            if (_model.AnimInfo.Frame[0] == 60 && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                if (GetHorizontalToPlayer(25, out Vector3 between, out float distance)) // 102400
                {
                    if (distance > 1 / 128f)
                    {
                        between = between.Normalized();
                    }
                    between = (between * 1.5f).AddY(Fixed.ToFloat(682)); // 6144
                    PlayerEntity.Main.TakeDamage(40, DamageFlags.None, between, this);
                    PlayerEntity.Main.CameraInfo.SetShake(0.75f); // 3072
                }
                SpawnEffect(71, Position); // goreaSlam
            }
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private bool GetHorizontalToPlayer(float maxDistance, out Vector3 between, out float distance)
        {
            between = Vector3.Zero;
            distance = 0;
            if (PlayerEntity.Main.Health > 0)
            {
                between = PlayerEntity.Main.Position - Position;
                distance = between.WithY(0).LengthSquared;
                if (distance < maxDistance)
                {
                    return true;
                }
            }
            return false;
        }

        private void State09()
        {
            WriteLine("state 9");
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private void State10()
        {
            WriteLine("state 10");
            UpdateArmMaterialAlpha();
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private void State11()
        {
            WriteLine("state 11");
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        // sktodo: (document) swipe attack?
        private void State12()
        {
            WriteLine("state 12");
            if (_model.AnimInfo.Frame[0] == 24 && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                if (GetHorizontalToPlayer(37.5f, out Vector3 between, out float distance)) // 153600
                {
                    if (distance > 1 / 128f)
                    {
                        between = between.Normalized();
                    }
                    between = (between * 1.5f).AddY(Fixed.ToFloat(682)); // 6144
                    PlayerEntity.Main.TakeDamage(25, DamageFlags.None, between, this);
                    PlayerEntity.Main.CameraInfo.SetShake(0.75f); // 3072
                }
            }
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        // sktodo: (document) switching to Gorea1B
        private void State13()
        {
            WriteLine("state 13");
            if (Flags.TestFlag(EnemyFlags.Visible))
            {
                _scanId = 0;
                Flags &= ~EnemyFlags.Visible;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.NoHomingNc;
                Flags |= EnemyFlags.NoHomingCo;
                Flags &= ~EnemyFlags.OnRadar;
                _field23C = 210 * 2; // todo: FPS stuff
                _field23E = 510 * 2; // todo: FPS stuff
                _field240 = (int)(Rng.GetRandomInt2(90) + 150) * 2; // todo: FPS stuff
                _head.Flags &= ~EnemyFlags.Visible;
                _head.Flags &= ~EnemyFlags.CollidePlayer;
                _head.Flags &= ~EnemyFlags.CollideBeam;
                _head.Flags |= EnemyFlags.NoHomingNc;
                _head.Flags |= EnemyFlags.NoHomingCo;
                for (int i = 0; i < 2; i++)
                {
                    Enemy26Entity arm = _arms[i];
                    arm.Flags &= ~EnemyFlags.Visible;
                    arm.Flags &= ~EnemyFlags.CollidePlayer;
                    arm.Flags &= ~EnemyFlags.CollideBeam;
                    arm.Flags |= EnemyFlags.NoHomingNc;
                    arm.Flags |= EnemyFlags.NoHomingCo;
                    arm.Flags |= EnemyFlags.Invincible;
                }
                _gorea1B.Activate();
                for (int i = 0; i < 3; i++)
                {
                    _legs[i].SetKneeNode(_gorea1B);
                }
            }
            CallSubroutine(Metadata.Enemy24Subroutines, this);
        }

        private void State14()
        {
            WriteLine("state 14");
            // in-game, Behavior02 (the only one under State14) sets the next ID directly on the
            // metadata struct. we have to wait for CallSubroutine to finish first, then set it here.
            CallSubroutine(Metadata.Enemy24Subroutines, this);
            _state2 = _nextState;
        }

        private bool Behavior00()
        {
            if (!AnimationEnded())
            {
                WriteLine("behavior 00 false");
                return false;
            }
            _model.SetAnimation(17, 0, _animSetNoMat);
            if (_state1 == 9)
            {
                _targetFacing = Vector3.Zero;
            }
            WriteLine("behavior 00 true");
            return true;
        }

        private bool Behavior01()
        {
            WriteLine("behavior 01 true");
            return true;
        }

        private bool Behavior02()
        {
            // see explanation in State14
            WriteLine("behavior 02 true");
            return true;
        }

        private bool Behavior03()
        {
            WriteLine($"behavior 03 {(_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended) ? "true" : "false")}");
            return AnimationEnded();
        }

        private bool Behavior04()
        {
            // look for arms with the dead flag that aren't marked as dead in the bits
            int index = 0;
            while ((_armBits & (1 << index)) == 0 || !_arms[index].ArmFlags.TestFlag(GoreaArmFlags.Bit0))
            {
                if (++index >= 2)
                {
                    WriteLine("behavior 04 false");
                    return false;
                }
            }
            _armBits &= ~(1 << index);
            _model.SetAnimation(20 + index, 0, _animSetNoMat, AnimFlags.NoLoop);
            if (index == 0)
            {
                _goreaFlags |= Gorea1AFlags.Bit3;
            }
            else
            {
                _goreaFlags &= ~Gorea1AFlags.Bit3;
            }
            _speed = Vector3.Zero;
            SetArmMaterialAlpha(index, 0);
            Vector3 spawnPos = _arms[index].Position;
            SpawnEffect(45, spawnPos); // goreaShoulderKill
            StopShots(index, detach: false);
            if (_armBits != 0)
            {
                uint rand = Rng.GetRandomInt2(100);
                ItemType itemType = rand >= 80 ? ItemType.UABig : ItemType.HealthBig;
                int despawnTime = 300 * 2; // todo: FPS stuff
                var item = new ItemInstanceEntity(new ItemInstanceEntityData(spawnPos, itemType, despawnTime), NodeRef, _scene);
                _scene.AddEntity(item);
            }
            WriteLine("behavior 04 true");
            return true;
        }

        private void StopShots(int index, bool detach)
        {
            _arms[index].StopShotEffect(detach);
            StopBeamChargeSfx(_beamTypes[WeaponIndex]);
        }

        private bool Behavior05()
        {
            if (_model.AnimInfo.Frame[0] >= _model.AnimInfo.FrameCount[0] - 1
                && _scene.FrameCount > 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                ushort charge = Weapons.GoreaWeapons[WeaponIndex].FullCharge;
                for (int i = 0; i < 2; i++)
                {
                    Enemy26Entity arm = _arms[i];
                    arm.EquipInfo.ChargeLevel = (ushort)(charge * 2);// todo: FPS stuff
                    arm.ArmFlags |= GoreaArmFlags.Bit1;
                }
                _model.SetAnimation(_weaponAnimIds[WeaponIndex], 0, _animSetNoMat);
                WriteLine("behavior 05 true");
                return true;
            }
            WriteLine("behavior 05 false");
            return false;
        }

        private bool Behavior06()
        {
            if (_armBits == 0 && _model.AnimInfo.Frame[0] == 5
                && _scene.FrameCount > 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                int anim = _goreaFlags.TestFlag(Gorea1AFlags.Bit3) ? 5 : 6;
                SetFlags setFlags = SetFlags.Texture | SetFlags.Texcoord | SetFlags.Unused | SetFlags.Node | SetFlags.Material;
                ModelInstance model = _gorea1B.GetModels()[0];
                model.SetAnimation(anim, 0, setFlags, AnimFlags.NoLoop);
                UpdateAnimFrames(model);
                model.AnimInfo.Frame[0] = 5;
                // todo: update music tracks
                _soundSource.PlaySfx(SfxId.GOREA_TRANSFORM1_SCR);
                WriteLine("behavior 06 true");
                return true;
            }
            WriteLine("behavior 05 false");
            return false;
        }

        private bool Behavior07()
        {
            if (CheckOffsetOutsideVolume(_speed))
            {
                _speed = Vector3.Zero;
                bool update = true;
                if (_state1 == 3)
                {
                    if (_nextState == 5)
                    {
                        StartShots();
                        _field23C = (int)(Rng.GetRandomInt2(60) + 90) * 2; // todo: FPS stuff
                        update = false;
                    }
                    else if (_nextState == 3)
                    {
                        _nextState = 4;
                    }
                }
                if (update)
                {
                    _model.SetAnimation(17, 0, _animSetNoMat);
                    StopAndSetUp();
                }
                WriteLine("behavior 07 true");
                return true;
            }
            WriteLine("behavior 07 false");
            return false;
        }

        private bool CheckOffsetOutsideVolume(Vector3 offset)
        {
            return !_volume.TestPoint(Position + offset);
        }

        private void StopAndSetUp()
        {
            _goreaFlags &= ~Gorea1AFlags.Bit4;
            Enemy26Entity armL = _arms[0];
            Enemy26Entity armR = _arms[1];
            armL.EquipInfo.ChargeLevel = 0;
            armR.EquipInfo.ChargeLevel = 0;
            WeaponInfo weapon = Weapons.GoreaWeapons[WeaponIndex];
            armL.Cooldown = weapon.ShotCooldown * 2; // todo: FPS stuff
            armR.Cooldown = weapon.AutofireCooldown * 2; // todo: FPS stuff
            _head.RespawnFlashEffect();
            _field23C = 60 * 2; // todo: FPS stuff
        }

        private static readonly IReadOnlyList<int> _chargeChances = new int[6]
        {
            40, 100, 40, 30, 100, 100
        };

        private void StartShots()
        {
            bool charge = Rng.GetRandomInt2(100) < _chargeChances[WeaponIndex];
            Enemy26Entity armL = _arms[0];
            Enemy26Entity armR = _arms[1];
            if (charge)
            {
                armL.ArmFlags |= GoreaArmFlags.Bit2;
                armR.ArmFlags |= GoreaArmFlags.Bit2;
                WeaponInfo weapon = Weapons.GoreaWeapons[WeaponIndex];
                // probably no point in setting this; it gets set on both arms before shooting
                armL.EquipInfo.ChargeLevel = (ushort)(weapon.FullCharge * 2); // todo: FPS stuff
                // todo: is it a bug that ShotCooldown is used for the right arm instead of AutofireCooldown?
                armL.Cooldown = weapon.ShotCooldown * 2; // todo: FPS stuff
                armR.Cooldown = weapon.ShotCooldown * 2; // todo: FPS stuff
                _field242 = (int)(Rng.GetRandomInt2(30) + 60) * 2; // todo: FPS stuff
                _model.SetAnimation(15, 0, _animSetNoMat);
                if (!armL.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
                {
                    CreateShotEffectAttached(0);
                }
                if (!armR.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
                {
                    CreateShotEffectAttached(1);
                }
                _nextState = 6;
                PlayBeamChargeSfx(_beamTypes[WeaponIndex]);
            }
            else
            {
                armL.ArmFlags &= ~GoreaArmFlags.Bit2;
                armR.ArmFlags &= ~GoreaArmFlags.Bit2;
                armL.EquipInfo.ChargeLevel = 0;
                armR.EquipInfo.ChargeLevel = 0;
                armL.Cooldown = 6 * 2; // todo: FPS stuff
                armR.Cooldown = 6 * 2; // todo: FPS stuff
                SetShotAnimation();
                _nextState = 5;
            }
        }

        private void CreateShotEffectAttached(int index)
        {
            StopShots(index, detach: true);
            _arms[index].SpawnShotEffect(_shotEffects[WeaponIndex]);
        }

        private void SetShotAnimation()
        {
            Enemy26Entity armL = _arms[0];
            Enemy26Entity armR = _arms[1];
            int animId;
            if (armL.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
            {
                armL.ArmFlags &= ~GoreaArmFlags.Bit1;
                armR.ArmFlags |= GoreaArmFlags.Bit1;
                animId = 10;
            }
            else if (armR.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
            {
                // todo: is it a bug that only one arm's flags are updated?
                // (seems like the right arm's bit might need to be cleared here)
                armL.ArmFlags |= GoreaArmFlags.Bit1;
                animId = 9;
            }
            else
            {
                uint rand = Rng.GetRandomInt2(3);
                if (rand == 0)
                {
                    armL.ArmFlags |= GoreaArmFlags.Bit1;
                    armR.ArmFlags &= ~GoreaArmFlags.Bit1;
                    animId = 9;
                }
                else if (rand == 1)
                {
                    armL.ArmFlags &= ~GoreaArmFlags.Bit1;
                    armR.ArmFlags |= GoreaArmFlags.Bit1;
                    animId = 10;
                }
                else // if (rand == 2)
                {
                    armL.ArmFlags |= GoreaArmFlags.Bit1;
                    armR.ArmFlags |= GoreaArmFlags.Bit1;
                    animId = 8;
                }
            }
            _model.SetAnimation(animId, 0, _animSetNoMat, AnimFlags.NoLoop);
        }

        private bool Behavior08()
        {
            if (_field23C > 0)
            {
                _field23C--;
            }
            if (_field23C == 0)
            {
                _speed = Vector3.Zero;
                if (_nextState == 5)
                {
                    StartShots();
                    _field23C = (int)(Rng.GetRandomInt2(60) + 90) * 2; // todo: FPS stuff
                }
                else
                {
                    if (_nextState == 3)
                    {
                        _nextState = 4;
                    }
                    _model.SetAnimation(17, 0, _animSetNoMat);
                    StopAndSetUp();
                }
                WriteLine("behavior 08 true");
                return true;
            }
            WriteLine("behavior 08 false");
            return false;
        }

        private bool Behavior09()
        {
            if (PlayerEntity.Main.Flags1.TestFlag(PlayerFlags1.AltForm)
                && GetHorizontalToPlayer(25, out _, out _)) // 102400
            {
                _speed = Vector3.Zero;
                _nextState = _state1;
                StopBeamChargeSfx(_beamTypes[WeaponIndex]);
                _soundSource.PlaySfx(SfxId.GOREA_ROAR_SCR);
                _model.SetAnimation(13, 0, _animSetNoMat, AnimFlags.NoLoop);
                WriteLine("behavior 09 true");
                return true;
            }
            WriteLine("behavior 09 false");
            return false;
        }

        private bool Behavior10()
        {
            if (!PlayerEntity.Main.Flags1.TestFlag(PlayerFlags1.AltForm)
                && GetHorizontalToPlayer(37.5f, out Vector3 between, out float distance)) // 153600
            {
                _speed = Vector3.Zero;
                SetSwingAnimation(between, distance);
                StopBeamChargeSfx(_beamTypes[WeaponIndex]);
                _soundSource.PlaySfx(SfxId.GOREA_ARM_SWING_ATTACK_SCR);
                WriteLine("behavior 10 true");
                return true;
            }
            WriteLine("behavior 10 false");
            return false;
        }

        private void SetSwingAnimation(Vector3 between, float distance)
        {
            Enemy26Entity armL = _arms[0];
            Enemy26Entity armR = _arms[1];
            int animId = 5;
            if (armL.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
            {
                animId = 6;
            }
            else if (!armR.ArmFlags.TestFlag(GoreaArmFlags.Bit0) && distance > 1 / 128f)
            {
                between = between.WithY(0).Normalized();
                Vector3 facing = FacingVector;
                facing = new Vector3(facing.Z, 0, -facing.X);
                if (facing.LengthSquared > 1 / 128f)
                {
                    facing = facing.Normalized();
                    float dot = Vector3.Dot(facing, between);
                    if (dot <= Fixed.ToFloat(-214)
                        || dot < Fixed.ToFloat(214) && Rng.GetRandomInt2(255) % 2 != 0)
                    {
                        animId = 6;
                    }
                }
            }
            _model.SetAnimation(animId, 0, _animSetNoMat, AnimFlags.NoLoop);
        }

        private bool Behavior11()
        {
            Vector3 between = PlayerEntity.Main.Position - Position;
            if (between.LengthSquared > 19 * 19) // 1478656
            {
                if (_field244 > 0)
                {
                    // sktodo: is this timer ever set?
                    _field244--;
                }
                if (_field244 == 0 && TrySprintingRoomInVolume())
                {
                    WriteLine("behavior 11 true");
                    return true;
                }
            }
            WriteLine("behavior 11 false");
            return false;
        }

        private bool TrySprintingRoomInVolume()
        {
            // check distance moved at sprinting speed in one second
            float factor = _speedFactor * 5 * 30;
            Vector3 offset = FacingVector * factor;
            if (!CheckOffsetOutsideVolume(offset) && UpdateTargetFacing())
            {
                _nextState = _state1;
                _field244 = 0;
                _field23C = 150 * 2; // todo: FPS stuff
                _model.SetAnimation(23, 0, _animSetNoMat);
                return true;
            }
            return false;
        }

        private bool Behavior12()
        {
            if (_field23C > 0)
            {
                _field23C--;
            }
            if (_field23C <= 0)
            {
                StartShots();
                _field23C = (int)(Rng.GetRandomInt2(60) + 90) * 2; // todo: FPS stuff
                WriteLine("behavior 12 true");
                return true;
            }
            WriteLine("behavior 12 false");
            return false;
        }

        private bool Behavior13()
        {
            if (AnimationEnded() && _goreaFlags.TestFlag(Gorea1AFlags.Bit4) && TrySprintingRoomInVolume())
            {
                StopBeamChargeSfx(_beamTypes[WeaponIndex]);
                WriteLine("behavior 13 true");
                return true;
            }
            WriteLine("behavior 13 false");
            return false;
        }

        private bool Behavior14()
        {
            if (AnimationEnded())
            {
                Write("behavior 14: ");
                return Behavior09();
            }
            WriteLine("behavior 14 false");
            return false;
        }

        private bool Behavior15()
        {
            if (_field23C > 0)
            {
                _field23C--;
            }
            if (_field23C > 0 || !IsAtEndFrame())
            {
                WriteLine("behavior 15 false");
                return false;
            }
            _model.SetAnimation(17, 0, _animSetNoMat);
            StopShots(index: 0, detach: true);
            StopShots(index: 1, detach: true);
            _field23C = 210 * 2; // todo: FPS stuff
            WriteLine("behavior 15 true");
            return true;
        }

        private bool Behavior16()
        {
            if (IsAtEndFrame())
            {
                StartShots();
                WriteLine("behavior 16 true");
                return true;
            }
            WriteLine("behavior 16 false");
            return false;
        }

        private bool Behavior17()
        {
            if (_head.Damage >= 1000)
            {
                _head.Damage = 0;
                _nextState = _state1;
                StopBeamChargeSfx(_beamTypes[WeaponIndex]);
                _model.SetAnimation(19, 0, _animSetNoMat, AnimFlags.NoLoop);
                WriteLine("behavior 17 true");
                return true;
            }
            WriteLine("behavior 17 false");
            return false;
        }

        private bool Behavior18()
        {
            if (_targetFacing != Vector3.Zero || !CheckFacingAngle(-1, PlayerEntity.Main.Position))
            {
                WriteLine("behavior 18 false");
                return false;
            }
            _field23C = 60 * 2; // todo: FPS stuff
            WriteLine("behavior 18 true");
            return true;
        }

        // sktodo: (document) at least one use is timer for giving up on turning to face the player in state 1
        // sktodo: also timer for stopping when initially walking toward player in state 2 (called via Behavior22)
        private bool Behavior19()
        {
            if (--_field23C > 0)
            {
                WriteLine("behavior 19 false");
                return false;
            }
            _speed = Vector3.Zero;
            _model.SetAnimation(17, 0, _animSetNoMat);
            StopAndSetUp();
            WriteLine("behavior 19 true");
            return true;
        }

        private bool Behavior20()
        {
            if (_field23E >= 0)
            {
                WriteLine("behavior 20 false");
                return false;
            }
            _field23E = 510 * 2; // todo: FPS stuff
            if (!_arms[0].ArmFlags.TestFlag(GoreaArmFlags.Bit0) && !_arms[1].ArmFlags.TestFlag(GoreaArmFlags.Bit0))
            {
                WriteLine("behavior 20 false");
                return false;
            }
            _soundSource.PlaySfx(SfxId.GOREA_REGEN_ARM_SCR);
            RegenerateArms();
            WriteLine("behavior 20 true");
            return true;
        }

        private void RegenerateArms()
        {
            for (int i = 0; i < 2; i++)
            {
                Enemy26Entity arm = _arms[i];
                arm.Damage = 0;
                if (arm.ArmFlags.TestFlag(GoreaArmFlags.Bit0))
                {
                    int anim = 1;
                    _armBits |= 1 << i;
                    arm.ScanId = Metadata.EnemyScanIds[(int)EnemyType.GoreaArm];
                    arm.Flags |= EnemyFlags.CollidePlayer;
                    arm.Flags |= EnemyFlags.CollideBeam;
                    arm.Flags &= ~EnemyFlags.Invincible;
                    arm.Flags |= EnemyFlags.NoHomingNc;
                    arm.Flags &= ~EnemyFlags.NoHomingCo;
                    arm.ArmFlags &= ~GoreaArmFlags.Bit0;
                    arm.RegenTimer = 60 * 2; // todo: FPS stuff
                    if (i == 1)
                    {
                        anim = 2;
                    }
                    _model.SetAnimation(anim, 0, _animSetNoMat, AnimFlags.NoLoop);
                }
            }
            _speed = Vector3.Zero;
        }

        private bool Behavior21()
        {
            if (_field240 < 0)
            {
                _field240 = (int)(Rng.GetRandomInt2(90) + 150) * 2; // todo: FPS stuff
                WriteLine("behavior 21 true");
                return true;
            }
            WriteLine("behavior 21 false");
            return false;
        }

        private bool Behavior22()
        {
            Write("behavior 22: ");
            return Behavior19();
        }

        private void UpdateSpeed()
        {
            int phase = 0;
            if (_gorea1B != null)
            {
                phase = 3 - _gorea1B.PhasesLeft;
                if (phase > 2)
                {
                    phase = 2;
                }
            }
            _speedFactor = _speedFactors[phase];
        }

        private static readonly IReadOnlyList<float> _speedFactors = new float[3]
        {
            Fixed.ToFloat(341), Fixed.ToFloat(426), Fixed.ToFloat(568)
        };

        protected override bool EnemyGetDrawInfo()
        {
            DrawArmRegen();
            UpdateArmMaterials();
            _lightOverride = true;
            DrawGeneric();
            // sktodo: does this happen?
            if (_gorea1B.Flags.TestFlag(EnemyFlags.Visible))
            {
                _gorea1B.DrawSelf();
                _gorea1B.Flags &= ~EnemyFlags.Visible;
                _gorea1B.Flags &= ~EnemyFlags.OnRadar;
            }
            _lightOverride = false;
            return true;
        }

        // sktodo: this has yet to be tested
        private void DrawArmRegen()
        {
            for (int i = 0; i < 2; i++)
            {
                Enemy26Entity arm = _arms[i];
                if (arm.RegenTimer != 0)
                {
                    arm.DrawRegen(_regenModel);
                    // caled from draw
                    if (_scene.ProcessFrame && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
                    {
                        _regenModel.UpdateAnimFrames();
                    }
                    break;
                }
            }
        }

        private void UpdateArmMaterials()
        {
            // caled from draw
            if (!_scene.ProcessFrame)
            {
                return;
            }
            var white = new ColorRgb(31, 31, 31);
            for (int i = 0; i < 2; i++)
            {
                Enemy26Entity arm = _arms[i];
                string matName = i == 0 ? "L_ShoulderTarget" : "R_ShoulderTarget";
                Material material = _model.Model.GetMaterialByName(matName)!;
                int maxFrame = 10 * 2; // todo: FPS stuff
                int frame = maxFrame - arm.ColorTimer;
                IncrementMaterialColors(material, white, white, frame, maxFrame);
                if (frame == maxFrame)
                {
                    material.AnimationFlags &= ~MatAnimFlags.DisableColor;
                }
                else
                {
                    material.AnimationFlags |= MatAnimFlags.DisableColor;
                }
            }
        }

        private void ResetMaterialColors()
        {
            for (int i = 0; i < _model.Model.Materials.Count; i++)
            {
                Material material = _model.Model.Materials[i];
                if (material.Name == "L_ShoulderTarget" || material.Name == "R_ShoulderTarget")
                {
                    material.AnimationFlags |= MatAnimFlags.DisableAlpha;
                }
                else if (material.Name != "BackTarget")
                {
                    material.AnimationFlags |= MatAnimFlags.DisableColor;
                    material.Ambient = new ColorRgb(0, 0, 0);
                    material.Diffuse = new ColorRgb(0, 0, 0);
                }
            }
        }

        #region Boilerplate

        public static bool Behavior00(Enemy24Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy24Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy24Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy24Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy24Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy24Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy24Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy24Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy24Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy24Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy24Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy24Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy24Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy24Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy24Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy24Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy24Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy24Entity enemy)
        {
            return enemy.Behavior17();
        }

        public static bool Behavior18(Enemy24Entity enemy)
        {
            return enemy.Behavior18();
        }

        public static bool Behavior19(Enemy24Entity enemy)
        {
            return enemy.Behavior19();
        }

        public static bool Behavior20(Enemy24Entity enemy)
        {
            return enemy.Behavior20();
        }

        public static bool Behavior21(Enemy24Entity enemy)
        {
            return enemy.Behavior21();
        }

        public static bool Behavior22(Enemy24Entity enemy)
        {
            return enemy.Behavior22();
        }

        #endregion
    }

    [Flags]
    public enum Gorea1AFlags : byte
    {
        None = 0x0,
        Bit0 = 0x1,
        Bit1 = 0x2,
        Bit2 = 0x4,
        Bit3 = 0x8,
        Bit4 = 0x10,
        Unused5 = 0x20,
        Unused6 = 0x40,
        Unused7 = 0x80
    }
}
