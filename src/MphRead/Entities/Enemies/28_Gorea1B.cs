using System;
using System.Collections.Generic;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy28Entity : GoreaEnemyEntityBase
    {
        private Enemy24Entity _gorea1A = null!;
        private Enemy29Entity _sealSphere = null!;
        public Enemy29Entity SealSphere => _sealSphere;
        private Node _spineNode = null!;
        private readonly Enemy30Entity[] _trocra = new Enemy30Entity[30];
        private CollisionVolume _volume;
        private Gorea1BFlags _goreaFlags;

        private int _phasesLeft = 0;
        public int PhasesLeft => _phasesLeft;
        private Vector3 _targetFacing;
        private int _field1C8 = 0;
        private int _field1CA = 0;

        // sktodo: field names, documentation, whatever (grapple segments)
        private readonly Vector3[] _grappleVecs = new Vector3[24];
        private Matrix4 _grappleMtx = Matrix4.Identity;
        private float _grappleInt = 0;
        private Vector3 _field10;
        private float _field24 = 0;
        private float _field28 = 0;
        private float _field30 = 0;
        private float _field34 = 0;
        private float _field38 = 0;

        public bool _grappling = false;
        public float _field21C = 0;
        public float _field21E = 0; // sktodo: grapple timer?
        private float _field224 = 0;
        private int _field234 = 0;

        private ModelInstance _trickModel = null!;
        private ModelInstance _grappleModel = null!;
        private EffectEntry? _grappleEffect = null;

        public Enemy28Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            _stateProcesses = new Action[14]
            {
                State00, State01, State02, State03, State04,
                State05, State06, State07, State08, State09,
                State10, State11, State12, State13
            };
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy24Entity owner)
            {
                _gorea1A = owner;
                InitializeCommon(owner.Spawner);
                Flags |= EnemyFlags.OnRadar;
                Flags |= EnemyFlags.Invincible;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                _scanId = 0;
                _state1 = _state2 = 0;
                SetTransform(owner.FacingVector, UpVector, owner.Position);
                _prevPos = Position;
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, Fixed.ToFloat(4098));
                Scale = owner.Scale;
                _model = SetUpModel("Gorea1B_lod0");
                _model.NodeAnimIgnoreRoot = true;
                _model.Model.ComputeNodeMatrices(index: 0);
                _model.SetAnimation(3, AnimFlags.NoLoop);
                _spineNode = _model.Model.GetNodeByName("Spine_02")!;
                _volume = CollisionVolume.Move(new CollisionVolume(
                    owner.Spawner.Data.Fields.S11.Sphere2Position.ToFloatVector(),
                    owner.Spawner.Data.Fields.S11.Sphere2Radius.FloatValue), Position);
                _field1C8 = (int)(Rng.GetRandomInt2(13) + 7) * 2; // todo: FPS stuff
                _field1CA = 30 * 2; // todo: FPS stuff
                _phasesLeft = 3;
                if (Rng.GetRandomInt2(255) % 2 != 0)
                {
                    _goreaFlags |= Gorea1BFlags.Bit3;
                }
                if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaSealSphere1, NodeRef, _scene) is Enemy29Entity sealSphere)
                {
                    _scene.AddEntity(sealSphere);
                    _sealSphere = sealSphere;
                }
                _trickModel = Read.GetModelInstance("goreaMindTrick");
                _grappleModel = Read.GetModelInstance("goreaGrappleBeam");
                _field21E = 120 * 2; // todo: FPS stuff
                _field24 = 0.65f; // 2662
                _field28 = 1 / 3f; // 1365
                _field30 = 1; // 4096
                _field34 = 0.25f; // 1024
            }
        }

        public void Activate()
        {
            _scanId = Metadata.EnemyScanIds[(int)EnemyType];
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags |= EnemyFlags.OnRadar;
            _targetFacing = Vector3.Zero;
            _goreaFlags |= Gorea1BFlags.Bit4;
            _sealSphere.Activate();
            ActivateTrocraSpawns();
            UpdateMaterials();
            SetTransform(_gorea1A.FacingVector, UpVector, _gorea1A.Position);
        }

        private void ActivateTrocraSpawns()
        {
            int count = _phasesLeft == 3 ? 1 : 2;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.EnemySpawn && entity is EnemySpawnEntity spawner
                    && spawner.Data.EnemyType == EnemyType.Trocra && !spawner.Flags.TestFlag(SpawnerFlags.Active))
                {
                    _scene.SendMessage(Message.Activate, this, spawner, 0, 0);
                    if (_phasesLeft != 1 && --count == 0)
                    {
                        break;
                    }
                }
            }
        }

        protected override void EnemyProcess()
        {
            if (Flags.TestFlag(EnemyFlags.Visible))
            {
                bool flagSet = _goreaFlags.TestFlag(Gorea1BFlags.Bit0); // sktodo: variable name
                if (!flagSet)
                {
                    CheckPlayerCollision();
                }
                CallStateProcess();
                if (!flagSet)
                {
                    TransformHurtVolumeToNode(_spineNode, new Vector3(0, Fixed.ToFloat(970), Fixed.ToFloat(622)));
                }
            }
        }

        private void CheckPlayerCollision()
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return;
            }
            // todo: is it a bug that this doesn't normalize the vector like 1A and legs do?
            Vector3 between = (PlayerEntity.Main.Position - Position).WithY(0);
            PlayerEntity.Main.Speed += between / 4;
            PlayerEntity.Main.TakeDamage(15, DamageFlags.None, null, this);
        }

        private void State00()
        {
            int anim = _model.AnimInfo.Index[0];
            if ((anim == 5 || anim == 6) && AnimationEnded())
            {
                var white = new ColorRgb(31, 31, 31);
                _sealSphere.Ambient = white;
                _sealSphere.Diffuse = white;
                _model.SetAnimation(7, 0, SetFlags.All, AnimFlags.NoLoop);
            }
            if (CallSubroutine(Metadata.Enemy28Subroutines, this))
            {
                _sealSphere.Flags &= ~EnemyFlags.Invincible;
                _model.SetAnimation(3, 0, SetFlags.All);
            }
        }

        private void State01()
        {
            Vector3 toCenter = (_volume.SpherePosition - Position).WithY(0);
            if (toCenter.LengthSquared > 1 / 128f)
            {
                _speed = toCenter.Normalized() * Fixed.ToFloat(109) / 2; // todo: FPS stuff
            }
            UpdateTargetFacing();
            if (CallSubroutine(Metadata.Enemy28Subroutines, this))
            {
                _speed = Vector3.Zero;
            }
        }

        private void UpdateTargetFacing()
        {
            if (!_goreaFlags.TestFlag(Gorea1BFlags.Bit2))
            {
                _targetFacing = (PlayerEntity.Main.Position - Position).WithY(0);
                if (_targetFacing.LengthSquared > 1 / 128f)
                {
                    _goreaFlags |= Gorea1BFlags.Bit2;
                    _targetFacing = _targetFacing.Normalized();
                }
                else
                {
                    _goreaFlags &= ~Gorea1BFlags.Bit2;
                    _targetFacing = Vector3.Zero;
                }
            }
            else if (SeekTargetFacing(_targetFacing, 3))
            {
                _goreaFlags &= ~Gorea1BFlags.Bit2;
                _targetFacing = Vector3.Zero;
            }
        }

        private void State02()
        {
            EnsureAnimation(3);
            UpdateTargetFacing();
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State03()
        {
            Func2139F54();
            _speed = FacingVector.WithY(0) * Fixed.ToFloat(54) / 2; // todo: FPS stuff
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        // sktodo: member name
        private void Func2139F54()
        {
            _targetFacing = (PlayerEntity.Main.Position - Position).WithY(0);
            if (_targetFacing.LengthSquared > 1 / 128f)
            {
                _targetFacing = _targetFacing.Normalized();
                Func2139FE8();
            }
        }

        // sktodo: member name
        private void Func2139FE8()
        {
            Vector3 facing = FacingVector;
            if (Vector3.Dot(_targetFacing, facing) < Fixed.ToFloat(4090))
            {
                float angle = MathHelper.DegreesToRadians(3);
                var cross = Vector3.Cross(_targetFacing, facing);
                var rotY = Matrix4.CreateRotationY(angle * (cross.Y > 0 ? -1 : 1));
                facing = Matrix.Vec3MultMtx4(facing, rotY).Normalized();
                SetTransform(facing, UpVector, Position);
                // sktodo: (document) rotating the vector to the end point by +/-3 degrees, then updating all the other vecs
                if (_grappling)
                {
                    Vector3 first = _grappleVecs[0];
                    Vector3 last = _grappleVecs[^1];
                    Vector3 between = last - first;
                    between = Matrix.Vec3MultMtx4(between, rotY);
                    last = between + first;
                    Func213C8C4(last);
                }
            }
            else
            {
                SetTransform(_targetFacing, UpVector, Position);
            }
        }

        // sktodo: member name
        // sktodo: (document) setting up equal segments in a straight line between first and (updated) last
        // --> each successive vector is from the starting point to one point further; they're not end to end
        private void Func213C8C4(Vector3 last)
        {
            Vector3 first = _grappleVecs[0];
            Vector3 between = last - first;
            for (int i = 0; i < _grappleVecs.Length; i++)
            {
                float factor = i / (float)(_grappleVecs.Length - 1);
                _grappleVecs[i] = first + between * factor;
            }
        }

        private void State04()
        {
            // sktodo
            if (!_grappling)
            {
                _speed = Vector3.Zero;
                _model.SetAnimation(1, 0, SetFlags.All, AnimFlags.NoLoop);
                Func213BCB8();
            }
            else if (AnimationEnded())
            {
                _model.SetAnimation(3, 0, SetFlags.All);
            }
            Func2139F54();
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        // sktodo: member name
        private void Func213BCB8()
        {
            _grappling = true;
            _field21E = 120 * 2; // todo: FPS stuff
            Vector3 target = PlayerEntity.Main.Position.AddY(0.05f);
            Func213C8C4(target);
            _field38 = 0;
        }

        private void State05()
        {
            Func2139F54();
            Func213B4A0();
            Func213B90C();
            if (AnimationEnded())
            {
                _model.SetAnimation(3, 0, SetFlags.All);
            }
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        // sktodo: member name
        private void Func213B4A0()
        {
            if (_grappling)
            {
                _grappleVecs[0] = _sealSphere.Position;
                Func213B4F8();
                Func213C814();
            }
        }

        // sktodo: member name
        private void Func213B4F8()
        {
            Vector3 between = _grappleVecs[^1] - _grappleVecs[0];
            if (between.LengthSquared > 1 / 128f)
            {
                between = between.Normalized();
                _field21C += 1.5f / 2; // todo: FPS stuff // sktodo: confirm
                if (_field21C >= 360)
                {
                    _field21C -= 360;
                }
                _grappleMtx = Matrix4.CreateFromAxisAngle(between, MathHelper.DegreesToRadians(_field21C));
            }
            else
            {
                _grappleMtx = Matrix4.Identity;
            }
        }

        // sktodo: member name
        private void Func213C814()
        {
            if ((int)_field38 < _grappleVecs.Length - 1)
            {
                _field38 += _field34;
            }
            _grappleInt += _field28;
            if ((int)Math.Round(_grappleInt) > _grappleVecs.Length)
            {
                _grappleInt -= _grappleInt % 1;
            }
        }

        // sktodo: member name
        private void Func213B90C()
        {
            Vector3 pos = Func213BF7C(); // sktodo: variable name
            Vector3 between = PlayerEntity.Main.Position.AddY(0.05f) - pos;
            if (between.LengthSquared >= 0.25f * 0.25f)
            {
                between = between.Normalized();
                _grappleVecs[^1] += between * 0.3f / 2; // 1228 // todo: FPS stuff // sktodo: confirm
                CollisionResult result = default;
                if (CollisionDetection.CheckBetweenPoints(_grappleVecs[0], _grappleVecs[^1], TestFlags.None, _scene, ref result))
                {
                    Vector3 toCollision = result.Position - _grappleVecs[0];
                    if (toCollision.Length > 20) // 81920
                    {
                        toCollision = toCollision.Normalized() * 20;
                    }
                    // the game does not do the following assignment, making the collision check pointless, but it was probably supposed to.
                    // it doesn't really matter in practice, since there should never be collision in the way of the grapple beam.
                    // sktodo: add a bugfix
                    _grappleVecs[^1] = _grappleVecs[0] + toCollision;
                }
                Func213C8C4(_grappleVecs[^1]);
            }
            else
            {
                _field234 = _sealSphere.Damage;
                _field38 = _grappleVecs.Length - 1;
                Func213C8C4(pos);
                _goreaFlags |= Gorea1BFlags.Bit1;
                PlayerEntity.Main.SetBipedStuck(true);
                Func213BC3C();
                if (_grappleEffect != null)
                {
                    _scene.UnlinkEffectEntry(_grappleEffect);
                    _grappleEffect = null;
                }
                _grappleEffect = SpawnEffectGetEntry(148, _grappleVecs[^1], extensionFlag: true); // grappleEnd
                PlayerEntity.Main.CameraInfo.SetShake(1 / 6f); // 682
                if (PlayerEntity.Main.Flags1.TestFlag(PlayerFlags1.AltForm) || PlayerEntity.Main.Flags1.TestFlag(PlayerFlags1.Morphing))
                {
                    PlayerEntity.Main.ExitAltForm();
                }
                _field224 = 0;
                // sktodo: variable names
                float v9 = (10 - (PlayerEntity.Main.Position.Y - Position.Y)) / 24;
                float v10 = v9 * 30 * 30;
                if (v10 != 0)
                {
                    float dist = Vector3.Distance(_grappleVecs[0], _grappleVecs[^1]);
                    _field224 = (Fixed.ToFloat(2986) - dist) / v10;
                }
            }
        }

        // sktodo: member name
        private Vector3 Func213BF7C()
        {
            return Func213C458(_field38);
        }

        // sktodo: member name
        // sktodo: (document) interpolating vector based on "index and percentage to next index" from e.g. field38
        private Vector3 Func213C458(float value)
        {
            Vector3 result = _grappleVecs[(int)value];
            float fractional = value % 1;
            if (fractional != 0)
            {
                Vector3 between = _grappleVecs[(int)value + 1] - result;
                result += between * fractional;
            }
            return result;
        }

        // sktodo: member name
        private void Func213BC3C()
        {
            float dist = Vector3.Distance(_grappleVecs[0], _grappleVecs[^1]);
            // sktodo: variable names
            float v4 = 1;
            if (dist > 14) // 57344
            {
                float v5 = dist / 17.5f; // 71680
                if (v5 > 1)
                {
                    v5 = 1;
                }
                v4 = 1 - v5 * 0.07f; // 287
            }
            _field30 = v4;
        }

        private void State06()
        {
            Func213B4A0();
            _field10 = new Vector3(0, 1 / 30f, 0); // 136
            Func213B678();
            Func213B7E0();
            TickGrappleDamage();
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        // sktodo: member name
        private void Func213B678()
        {
            if (PlayerEntity.Main.Position.Y <= Position.Y)
            {
                return;
            }
            float dist = Vector3.Distance(_grappleVecs[0], _grappleVecs[1]);
            // sktodo: FPS stuff?
            if (dist > 1 / 128f)
            {
                // sktodo: variable names
                float v6 = (10 - (PlayerEntity.Main.Position.Y - Position.Y)) * 30;
                if (v6 > 0)
                {
                    float v7 = Func213B784();
                    _field224 = (17.5f - v7) / v6;
                    float v8 = MathF.Abs(dist - Fixed.ToFloat(2986));
                    float v12 = v8 > MathF.Abs(_field224) ? dist + _field224 : Fixed.ToFloat(2986);
                    Func213C624(v12);
                }
            }
            PlayerEntity.Main.Position = _grappleVecs[^1].AddY(-0.05f); // 204
        }

        // sktodo: member name
        private float Func213B784()
        {
            float sum = 0;
            for (int i = 1; i < _grappleVecs.Length; i++)
            {
                sum += Vector3.Distance(_grappleVecs[i], _grappleVecs[i - 1]);
            }
            return sum;
        }

        // sktodo: member name
        private void Func213C624(float factor)
        {
            for (int i = 1; i < _grappleVecs.Length; i++)
            {
                Vector3 between = _grappleVecs[i] - _grappleVecs[i - 1];
                if (between.LengthSquared > 1 / 128f)
                {
                    Vector3 scaled = between.Normalized() * factor;
                    Vector3 remaining = scaled - between;
                    for (int j = i; j < _grappleVecs.Length; j++)
                    {
                        _grappleVecs[j] += remaining;
                    }
                }
            }
        }

        // sktodo: member name
        private void Func213B7E0()
        {
            Func213C4DC();
            float dist = Vector3.Distance(_grappleVecs[0], _grappleVecs[1]);
            Func213C624(dist);
            Vector3 playerPos = _grappleVecs[^1].AddY(-0.05f); // 204
            Vector3 between = _grappleVecs[^1] - _grappleVecs[^2];
            if (between.LengthSquared > 1 / 128f)
            {
                between = between.Normalized();
                playerPos += between * -0.5f;
            }
            PlayerEntity.Main.Position = playerPos;
            PlayerEntity.Main.CameraInfo.SetShake(0);
        }

        // sktodo: member name
        private void Func213C4DC()
        {
            float dist = Vector3.Distance(_grappleVecs[0], _grappleVecs[1]);
            _grappleVecs[1] += _field10 / 2; // todo: FPS stuff
            if (dist > 1 / 128f)
            {
                Vector3 start = _grappleVecs[1] - _grappleVecs[0];
                for (int i = 2; i < _grappleVecs.Length; i++)
                {
                    Vector3 between = _grappleVecs[i] - _grappleVecs[i - 1];
                    if (between.LengthSquared > 1 / 128f)
                    {
                        Vector3 result = Func204D57C(between, start);
                        Vector3 update = _grappleVecs[i];
                        update += (result - between) * _field30 / 2; // todo: FPS stuff // sktodo: confirm
                        _grappleVecs[i] = update;
                        start = update - _grappleVecs[i - 1];
                    }
                }
            }
        }

        // sktodo: member name (note: this is copied from Quadtroid)
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

        private void TickGrappleDamage()
        {
            if (_field1C8 > 0)
            {
                _field1C8--;
            }
            if (_field1C8 == 0)
            {
                _field1C8 = (int)(Rng.GetRandomInt2(13) + 7) * 2; // todo: FPS stuff
                PlayerEntity.Main.TakeDamage(2, DamageFlags.NoDmgInvuln, null, this);
                SpawnEffect(179, PlayerEntity.Main.Position); // goreaGrappleDamage
            }
        }

        private void State07()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State08()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State09()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State10()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State11()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State12()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State13()
        {
            // sktodo
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        public bool Behavior00()
        {
            // sktodo
            return false;
        }

        public bool Behavior01()
        {
            // sktodo
            return false;
        }

        public bool Behavior02()
        {
            // sktodo
            return false;
        }

        public bool Behavior03()
        {
            // sktodo
            return false;
        }

        public bool Behavior04()
        {
            // sktodo
            return false;
        }

        public bool Behavior05()
        {
            // sktodo
            return false;
        }

        public bool Behavior06()
        {
            // sktodo
            return false;
        }

        public bool Behavior07()
        {
            // sktodo
            return false;
        }

        public bool Behavior08()
        {
            // sktodo
            return false;
        }

        public bool Behavior09()
        {
            // sktodo
            return false;
        }

        public bool Behavior10()
        {
            // sktodo
            return false;
        }

        public bool Behavior11()
        {
            // sktodo
            return false;
        }

        public bool Behavior12()
        {
            // sktodo
            return false;
        }

        public bool Behavior13()
        {
            // sktodo
            return false;
        }

        public bool Behavior14()
        {
            // sktodo
            return false;
        }

        public bool Behavior15()
        {
            // sktodo
            return false;
        }

        private readonly IReadOnlyList<string> _bodyMatNames1 = new string[6]
        {
            "ChestMembrane", "Eye", "Head1", "Legs", "Torso", "Shoulder"
        };

        private readonly IReadOnlyList<string> _bodyMatNames2 = new string[2]
        {
            "ChestCore", "HeadFullLit"
        };

        private void UpdateMaterials()
        {
            for (int i = 0; i < 6; i++)
            {
                Material material = _model.Model.GetMaterialByName(_bodyMatNames1[i])!;
                material.Ambient = _gorea1A.Colors[2];
                material.Diffuse = _gorea1A.Colors[3];
            }
            for (int i = 0; i < 2; i++)
            {
                Material material = _model.Model.GetMaterialByName(_bodyMatNames2[i])!;
                material.Ambient = _gorea1A.Colors[0];
                material.Diffuse = _gorea1A.Colors[1];
            }
            _sealSphere.Ambient = _gorea1A.Colors[0];
            _sealSphere.Diffuse = _gorea1A.Colors[1];
        }

        public void DrawSelf()
        {
            DrawGeneric();
        }

        protected override bool EnemyGetDrawInfo()
        {
            // sktodo: draw mind trick/grapple
            return true;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy28Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy28Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy28Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy28Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy28Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy28Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy28Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy28Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy28Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy28Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy28Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy28Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy28Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy28Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy28Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy28Entity enemy)
        {
            return enemy.Behavior15();
        }

        #endregion
    }

    [Flags]
    public enum Gorea1BFlags : byte
    {
        None = 0x0,
        Bit0 = 0x1,
        Bit1 = 0x2,
        Bit2 = 0x4,
        Bit3 = 0x8,
        Bit4 = 0x10,
        Bit05 = 0x20,
        Bit06 = 0x40,
        Bit07 = 0x80
    }
}
