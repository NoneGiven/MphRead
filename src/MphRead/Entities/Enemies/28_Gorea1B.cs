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
        private readonly Enemy30Entity?[] _trocra = new Enemy30Entity?[30];
        private CollisionVolume _volume;
        private Gorea1BFlags _goreaFlags;

        private int _phasesLeft = 0;
        public int PhasesLeft => _phasesLeft;
        private Vector3 _targetFacing;
        private int _field1C8 = 0;
        private int _field1CA = 0;
        private int _field1CC = 0;

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
            // state 5: seeking with grapple beam
            // state 6: grabbing player with grapple beam
            // state 7: swinging player with grapple beam
            // state 8: raising player up with grapple beam
            // state 9: slamming player down with grapple beam
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
                _trickModel = SetUpModel("goreaMindTrick");
                _grappleModel = SetUpModel("goreaGrappleBeam");
                _trickModel.Active = false;
                _grappleModel.Active = false;
                // sktodo: field names (stretching speed and/or things those lines)
                _field21E = 120 * 2; // todo: FPS stuff
                _field24 = 0.65f; // 2662
                _field28 = 1 / 3f; // 1365
                _field30 = 1; // 4096
                _field34 = 0.25f; // 1024 // todo: FPS stuff
                ResetMaterialColors();
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
            UpdateGrappleDrawValues();
            Func213B90C();
            if (AnimationEnded())
            {
                _model.SetAnimation(3, 0, SetFlags.All);
            }
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void UpdateGrappleDrawValues()
        {
            if (_grappling)
            {
                _grappleVecs[0] = _sealSphere.Position;
                UpdateGrappleDrawMatrix();
                UpdateGrappleDrawInt();
            }
        }

        private void UpdateGrappleDrawMatrix()
        {
            // todo: implement "count_1" to support multiple grapple beams?
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
        private void UpdateGrappleDrawInt()
        {
            if ((int)_field38 < _grappleVecs.Length - 1)
            {
                _field38 += _field34 / 2; // todo: FPS stuff
            }
            _grappleInt += _field28 / 2; // todo: FPS stuff
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
        // (and then later from us iterating from 0 -> field38 in the draw loop)
        private Vector3 Func213C458(float index)
        {
            Vector3 result = _grappleVecs[(int)index];
            float fractional = index % 1;
            if (fractional != 0)
            {
                Vector3 between = _grappleVecs[(int)index + 1] - result;
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
            UpdateGrappleDrawValues();
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
                        _grappleVecs[j] += remaining / 2; // todo: FPS stuff
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
            Func213B678();
            UpdateGrappleDrawValues();
            Vector3 between;
            if (_goreaFlags.TestFlag(Gorea1BFlags.Bit3))
            {
                between = _grappleVecs[0] - _grappleVecs[1];
            }
            else
            {
                between = _grappleVecs[1] - _grappleVecs[0];
            }
            between = between.WithY(0).WithZ(-between.Z);
            if (between.LengthSquared > 1 / 128f)
            {
                _field10 = between.Normalized();
            }
            // sktodo: I'm not changing the multiplication by 0.04 since I think the above will usually cause _field10 to get a fresh value every frame?
            // need to double check how/when field10 is actually used, might have to change this and the speed part too idk yet
            _field10 *= 0.04f; // 163
            _field10 += _speed;
            Func213B7E0();
            TickGrappleDamage();
            if (_field1CC > 0)
            {
                _field1CC--;
            }
            if (_field1CC == 0)
            {
                _field1CC = 60 * 2; // todo: FPS stuff
                _goreaFlags ^= Gorea1BFlags.Bit3;
                _soundSource.PlaySfx(SfxId.GOREA_ATTACK2B_SCR);
            }
            if (_speed != Vector3.Zero && CheckMovementOutsideVolume())
            {
                _speed = Vector3.Zero;
            }
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private bool CheckMovementOutsideVolume()
        {
            return !_volume.TestPoint(Position + _speed);
        }

        private void State08()
        {
            UpdateGrappleDrawValues();
            _field10 = new Vector3(0, 1 / 15f, 0); // 273
            Func213B678();
            Func213B7E0();
            TickGrappleDamage();
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State09()
        {
            UpdateGrappleDrawValues();
            Func213C4DC();
            TickGrappleDamage();
            Func213C624(Fixed.ToFloat(2986));
            _field10 = new Vector3(0, -1 / 1.5f, 0); // -2730
            Vector3 between = (_grappleVecs[0] - _grappleVecs[1]).Normalized();
            if (Vector3.Dot(between, _field10.Normalized()) < Fixed.ToFloat(-3956))
            {
                Vector3 vec = (_volume.SpherePosition - Position).WithY(0);
                if (vec.LengthSquared >= 1 / 128f)
                {
                    vec = vec.Normalized();
                }
                else
                {
                    vec = FacingVector;
                }
                _field10 = vec * (-1 / 1.5f); // -2730
            }
            PlayerEntity.Main.Position = _grappleVecs[^1].AddY(-0.05f); // 204
            PlayerEntity.Main.CameraInfo.SetShake(1 / 128f); // 32
            CallSubroutine(Metadata.Enemy28Subroutines, this);
        }

        private void State10()
        {
            if (CallSubroutine(Metadata.Enemy28Subroutines, this))
            {
                StopGrappling();
            }
        }

        private void StopGrappling()
        {
            _goreaFlags &= ~Gorea1BFlags.Bit1;
            if (_grappleEffect != null)
            {
                _scene.UnlinkEffectEntry(_grappleEffect);
                _grappleEffect = null;
            }
            PlayerEntity.Main.SetBipedStuck(false);
            if (_grappling)
            {
                _grappling = false;
                _soundSource.PlaySfx(SfxId.GOREA_TENTACLE_DIE_SCR);
                int index = (int)_field38;
                if (index >= 2)
                {
                    Vector3 prev = _grappleVecs[index - 1];
                    for (int i = index - 2; i >= 0; i--)
                    {
                        Vector3 cur = _grappleVecs[i];
                        if (i % 2 == 1)
                        {
                            Vector3 between = prev - cur;
                            if (between.LengthSquared > 1 / 128f)
                            {
                                between = between.Normalized();
                                Vector3 unitVec = Vector3.UnitY;
                                float dot = MathF.Abs(Vector3.Dot(unitVec, between));
                                if (dot > Fixed.ToFloat(4065))
                                {
                                    unitVec = Vector3.UnitZ;
                                }
                                SpawnEffect(180, cur, between, unitVec); // goreaGrappleDie
                            }
                        }
                        prev = cur;
                    }
                }
            }
        }

        private void State11()
        {
            Func213B2B4();
            Func213AF2C();
            Func2139F54();
            _speed = FacingVector.WithY(0) * Fixed.ToFloat(54) / 2; // todo: FPS stuff
            if (_speed != Vector3.Zero && CheckMovementOutsideVolume())
            {
                _speed = Vector3.Zero;
            }
            CallSubroutine(Metadata.Enemy28Subroutines, this);
            // sktodo: make sure this ends? it should end as soon as it's not being called
            _soundSource.PlayEnvironmentSfx(9); // GOREA_ATTACK3_LOOP
        }

        // sktodo: member name
        private void Func213B2B4()
        {
            if (_field1CC <= 0)
            {
                return;
            }
            bool trocrasAlive = false;
            for (int i = 0; i < _trocra.Length; i++)
            {
                if (_trocra[i] != null)
                {
                    trocrasAlive = true;
                    break;
                }
            }
            if (!trocrasAlive)
            {
                _field1CA = 0;
            }
            else if (_field1CA > 0)
            {
                _field1CA--;
            }
            if (_field1CA == 0)
            {
                _field1CA = 30 * 2; // todo: FPS stuff
                Func213B348();
            }
        }

        // sktodo: member name
        private void Func213B348()
        {
            int firstDeadIndex = -1;
            for (int i = 0; i < _trocra.Length; i++)
            {
                if (_trocra[i] == null)
                {
                    firstDeadIndex = i;
                    break;
                }
            }
            // we want to get one with Field186 == 0 and within a certain angle range,
            // but will settle for anything with Field186 == 0 if the second condition can't be satisfied
            if (firstDeadIndex >= 0)
            {
                int i;
                for (i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.EnemyInstance && entity is Enemy30Entity enemy && enemy.State == 0)
                    {
                        _trocra[firstDeadIndex] = enemy;
                        break;
                    }
                }
                if (_trocra[firstDeadIndex] != null) // only true if just set above
                {
                    // pick up where we left off in the list
                    for (i = i + 1; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type == EntityType.EnemyInstance && entity is Enemy30Entity enemy && enemy.State == 0)
                        {
                            Vector3 between = enemy.Position - Position;
                            if (between.LengthSquared > 1 / 128f && Vector3.Dot(between.Normalized(), FacingVector) < 0)
                            {
                                _trocra[firstDeadIndex] = enemy;
                                break;
                            }
                        }
                    }
                    Enemy30Entity trocra = _trocra[firstDeadIndex]!;
                    trocra.Gorea1B = this;
                    trocra.Index = firstDeadIndex;
                    trocra.State = 1;
                }
            }
        }

        // sktodo: member name
        private void Func213AF2C()
        {
            for (int i = 0; i < _trocra.Length; i++)
            {
                Enemy30Entity? trocra = _trocra[i];
                if (trocra != null)
                {
                    if (trocra.State == 1 && !Func213B188(trocra))
                    {
                        trocra.Field184 = 45 * 2; // todo: FPS stuff
                        trocra.State = 2;
                        trocra.Field174 = trocra.Position;
                    }
                    else if (trocra.State == 2)
                    {
                        trocra.Field174 += _speed; // sktodo: FPS stuff?
                        if (trocra.Field184 > 0)
                        {
                            trocra.Field184--;
                            trocra.Position = new Vector3(
                                trocra.Field174.X + Fixed.ToFloat(Rng.GetRandomInt2(4096) - 2048),
                                trocra.Field174.Y + Fixed.ToFloat(Rng.GetRandomInt2(4096) - 2048),
                                trocra.Field174.Z + Fixed.ToFloat(Rng.GetRandomInt2(4096) - 2048)
                            );
                        }
                        // sktodo (document): throwing after timer
                        if (trocra.Field184 == 0)
                        {
                            Vector3 speed = PlayerEntity.Main.Position - trocra.Position;
                            if (speed.LengthSquared >= 1 / 128f)
                            {
                                speed = speed.Normalized() * 0.7f; // 2867
                            }
                            trocra.SetSpeed(speed / 2); // todo: FPS stuff
                            trocra.State = 3;
                            _soundSource.PlaySfx(SfxId.GOREA_ATTACK3A);
                            _trocra[i] = null;
                        }
                    }
                }
            }
        }

        // sktodo: member name
        private bool Func213B188(Enemy30Entity trocra)
        {
            Vector3 between = (trocra.Position - Position).WithY(0);
            if (between.LengthSquared > 1 / 128f)
            {
                Vector3 position = (_volume.SpherePosition + between.Normalized() * 5).AddY(10);
                Vector3 direction = position - trocra.Position;
                if (direction.LengthSquared > 1 / 128f)
                {
                    direction = direction.Normalized();
                    trocra.Position += (direction * (1 / 7f)) / 2; // todo: FPS stuff
                    return true;
                }
            }
            return false;
        }

        private void State12()
        {
            if (!Flags.TestFlag(EnemyFlags.Visible))
            {
                return;
            }
            int anim = _model.AnimInfo.Index[0];
            if (AnimationEnded())
            {
                if (anim == 4)
                {
                    _model.SetAnimation(2, 0, SetFlags.All, AnimFlags.NoLoop);
                }
                else if (anim == 2)
                {
                    Material material = _model.Model.GetMaterialByName("HeadFullLit")!;
                    _sealSphere.Ambient = material.Ambient;
                    _sealSphere.Diffuse = material.Diffuse;
                    _model.SetAnimation(8, 0, SetFlags.All, AnimFlags.NoLoop);
                }
                else if (anim == 8)
                {
                    // only gets called once, since the only sub in this state always returns true
                    Deactivate();
                    CallSubroutine(Metadata.Enemy28Subroutines, this);
                }
            }
            else if (anim == 8 && _model.AnimInfo.Frame[0] == 46
                && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                PlayerEntity.Main.CameraInfo.SetShake(0.75f); // 3072
            }
        }

        private void Deactivate()
        {
            _scanId = 0;
            Flags &= ~EnemyFlags.Visible;
            Flags &= ~EnemyFlags.CollidePlayer;
            Flags &= ~EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags &= ~EnemyFlags.OnRadar;
            for (int i = 0; i < _trocra.Length; i++)
            {
                _trocra[i]?.Explode();
                _trocra[i] = null;
            }
            _field1CA = 30 * 2; // todo: FPS stuff
            _goreaFlags &= ~Gorea1BFlags.Bit0;
            _sealSphere.Deactivate();
            _field234 = 0;
            _gorea1A.Activate();
        }

        private void State13()
        {
            if (!_goreaFlags.TestFlag(Gorea1BFlags.Bit5))
            {
                _goreaFlags |= Gorea1BFlags.Bit5;
                PlayerEntity.Main.SetBipedStuck(false);
                _scanId = 0;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.Invincible;
                Flags |= EnemyFlags.NoHomingNc;
                Flags |= EnemyFlags.NoHomingCo;
                Flags &= ~EnemyFlags.OnRadar;
                _phasesLeft = 0;
                _goreaFlags |= Gorea1BFlags.Bit0;
                _sealSphere.SetScanId(0);
                _sealSphere.Flags &= ~EnemyFlags.Visible;
                _sealSphere.Flags &= ~EnemyFlags.CollidePlayer;
                _sealSphere.Flags &= ~EnemyFlags.CollideBeam;
                _sealSphere.Flags |= EnemyFlags.Invincible;
                _sealSphere.Flags |= EnemyFlags.NoHomingNc;
                _sealSphere.Flags |= EnemyFlags.NoHomingCo;
                _field234 = 0;
                _gorea1A.Position = Position;
                _gorea1A.Flags &= ~EnemyFlags.Visible;
                _gorea1A.Flags &= ~EnemyFlags.CollidePlayer;
                _gorea1A.Flags &= ~EnemyFlags.CollideBeam;
                _gorea1A.Flags |= EnemyFlags.Invincible;
                _gorea1A.Flags |= EnemyFlags.NoHomingNc;
                _gorea1A.Flags |= EnemyFlags.NoHomingCo;
                _gorea1A.Flags &= ~EnemyFlags.OnRadar;
            }
            if (IsAtEndFrame())
            {
                EnsureAnimation(3, 0, SetFlags.All, AnimFlags.NoLoop);
            }
        }

        public bool BehaviorXX()
        {
            return AnimationEnded();
        }

        public bool Behavior00()
        {
            _field1CC = (int)(Rng.GetRandomInt2(150) + 150) * 2; // todo: FPS stuff
            return true;
        }

        public bool Behavior01()
        {
            return _volume.TestPoint(Position);
        }

        public bool Behavior02()
        {
            if (_phasesLeft <= 0)
            {
                SpawnEffect(72, _sealSphere.Position); // goreaBallExplode2
                _model.SetAnimation(4, 0, SetFlags.All, AnimFlags.NoLoop);
                StopGrappling();
                DeactivateAllTrocraSpawns();
                DestroyAllTrocras();
                // todo: movie and/or credits stuff
                GameState.StorySave.CheckpointRoomId = -1;
                GameState.StorySave.CheckpointEntityId = -1;
                if ((GameState.StorySave.TriggerState[1] & 0x10) != 0 || Cheats.AlwaysFightGorea2)
                {
                    GameState.TransitionRoomId = 92; // Gorea_b2
                }
                else
                {
                    GameState.TransitionRoomId = _scene.RoomId;
                }
                _scene.SetFade(FadeType.FadeOutWhite, length: 45 / 30f, overwrite: true, AfterFade.AfterMovie);
                return true;
            }
            return false;
        }

        private void DeactivateAllTrocraSpawns()
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.EnemySpawn && entity is EnemySpawnEntity spawner
                    && spawner.Data.EnemyType == EnemyType.Trocra)
                {
                    _scene.SendMessage(Message.SetActive, this, spawner, 0, 0);
                }
            }
        }

        private void DestroyAllTrocras()
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.EnemyInstance && entity is Enemy30Entity trocra)
                {
                    trocra.Explode();
                }
            }
        }

        public bool Behavior03()
        {
            if (_sealSphere.Damage < 1000 * (4 - _phasesLeft))
            {
                return false;
            }
            StopGrappling();
            if (--_phasesLeft <= 0)
            {
                _soundSource.PlaySfx(SfxId.GOREA_1B_DIE2_SCR);
                return false;
            }
            _model.SetAnimation(4, 0, SetFlags.All, AnimFlags.NoLoop);
            _sealSphere.Flags |= EnemyFlags.Invincible;
            SpawnEffect(42, _sealSphere.Position); // goreaBallExplode
            for (int i = 0; i < _trocra.Length; i++)
            {
                _trocra[i]?.Explode();
                _trocra[i] = null;
            }
            _field1CA = 30 * 2; // todo: FPS stuff
            _soundSource.PlaySfx(SfxId.GOREA_1B_DIE_SCR);
            _soundSource.PlaySfx(SfxId.GOREA_TRANSFORM2_SCR);
            return true;
        }

        public bool Behavior04()
        {
            _field21E--;
            if (_field21E <= 0)
            {
                _field21E = 120 * 2; // todo: FPS stuff
                _soundSource.PlaySfx(SfxId.GOREA_ATTACK2);
                return true;
            }
            return false;
        }

        public bool Behavior05()
        {
            if (_field1CC > 0)
            {
                _field1CC--;
            }
            if (_field1CC == 0)
            {
                for (int i = 0; i < _trocra.Length; i++)
                {
                    if (_trocra[i] != null)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public bool Behavior06()
        {
            _field21E--;
            if (_field21E <= 0)
            {
                StopGrappling();
                _field1CC = (int)(Rng.GetRandomInt2(150) + 150) * 2; // todo: FPS stuff
                return true;
            }
            return false;
        }

        public bool Behavior07()
        {
            if (_goreaFlags.TestFlag(Gorea1BFlags.Bit1))
            {
                _soundSource.PlaySfx(SfxId.GOREA_ATTACK2A);
                return true;
            }
            return false;
        }

        public bool Behavior08()
        {
            return _sealSphere.Damage - _field234 >= 35;
        }

        public bool Behavior09()
        {
            if (PlayerEntity.Main.Position.Y - Position.Y < 10)
            {
                return false;
            }
            _field30 = 0.9f; // 3686
            _field1CA = 150 * 2; // todo: FPS stuff
            _field1CC = 30 * 2; // todo: FPS stuff
            _goreaFlags ^= Gorea1BFlags.Bit3;
            Vector3 toCenter = (PlayerEntity.Main.Position - _volume.SpherePosition).WithY(0);
            if (toCenter.LengthSquared > 1 / 128f)
            {
                _speed = toCenter.Normalized() * Fixed.ToFloat(68) / 2; // todo: FPS stuff
                if (CheckMovementOutsideVolume())
                {
                    _speed = Vector3.Zero;
                }
            }
            _soundSource.PlaySfx(SfxId.GOREA_ATTACK2B_SCR);
            return true;
        }

        public bool Behavior10()
        {
            if (_field1CA > 0)
            {
                _field1CA--;
            }
            if (_field1CA == 0)
            {
                _field30 = Fixed.ToFloat(3973);
                _speed = Vector3.Zero;
                return true;
            }
            return false;
        }

        public bool Behavior11()
        {
            if (PlayerEntity.Main.Position.Y - Position.Y < 22.5f)
            {
                return false;
            }
            _field30 = Fixed.ToFloat(4046);
            _speed = Vector3.Zero;
            return true;
        }

        public bool Behavior12()
        {
            bool collided = false;
            CollisionResult result = default;
            if (CollisionDetection.CheckBetweenPoints(PlayerEntity.Main.PrevPosition, PlayerEntity.Main.Position,
                TestFlags.None, _scene, ref result))
            {
                PlayerEntity.Main.Position = result.Position;
                PlayerEntity.Main.HandleCollision(result);
                collided = true;
            }
            else
            {
                float spawnerY = _gorea1A.Spawner.Data.Header.Position.ToFloatVector().Y;
                float yDiff = PlayerEntity.Main.Position.Y - spawnerY;
                if (yDiff < 0)
                {
                    // player is clipping below the spawner (ground) height, process collision at the ground
                    result = default;
                    result.Field0 = 0;
                    result.Plane = new Vector4(0, 1, 0, spawnerY);
                    result.Field14 = -yDiff;
                    result.Position = PlayerEntity.Main.Position.WithY(spawnerY);
                    PlayerEntity.Main.HandleCollision(result);
                    collided = true;
                }
            }
            if (collided)
            {
                StopGrappling();
                _soundSource.PlaySfx(SfxId.GOREA_ATTACK2C_SCR);
                PlayerEntity.Main.TakeDamage(30, DamageFlags.NoDmgInvuln, null, this);
                PlayerEntity.Main.CameraInfo.SetShake(0.75f); // 3072
                _field1CC = (int)(Rng.GetRandomInt2(150) + 150) * 2; // todo: FPS stuff
            }
            return collided;
        }

        public bool Behavior13()
        {
            if (PlayerEntity.Main.Health != 0 && !_goreaFlags.TestFlag(Gorea1BFlags.Bit2)
                && CheckFacingAngle(-1, PlayerEntity.Main.Position))
            {
                return true;
            }
            return false;
        }

        public bool Behavior14()
        {
            if (Vector3.Distance(_sealSphere.Position, PlayerEntity.Main.Position) < 20
                && CheckFacingAngle(-1, PlayerEntity.Main.Position))
            {
                _field21E = 120 * 2; // todo: FPS stuff
                return true;
            }
            return false;
        }

        public bool Behavior15()
        {
            if (CheckMovementOutsideVolume())
            {
                _speed = Vector3.Zero;
                _field21E = 120 * 2; // todo: FPS stuff
                return true;
            }
            return false;
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            _health = UInt16.MaxValue;
            return false;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Destroyed && info.Sender.Type == EntityType.EnemyInstance
                && info.Sender is Enemy30Entity trocra)
            {
                _trocra[trocra.Index] = null;
            }
        }

        private readonly IReadOnlyList<string> _bodyMatNames1 = new string[6]
        {
            "ChestMembrane", "Eye", "Head1", "Legs", "Torso", "Shoulder"
        };

        private readonly IReadOnlyList<string> _bodyMatNames2 = new string[2]
        {
            "ChestCore", "HeadFullLit"
        };

        private void ResetMaterialColors()
        {
            for (int i = 0; i < _model.Model.Materials.Count; i++)
            {
                Material material = _model.Model.Materials[i];
                if (material.Name != "L_ShoulderTarget" && material.Name != "R_ShoulderTarget" && material.Name != "BackTarget")
                {
                    material.AnimationFlags |= MatAnimFlags.DisableColor;
                }
            }
        }

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
            if (_scene.ProcessFrame)
            {
                Material material = _model.Model.GetMaterialByName("ChestCore")!;
                int maxFrame = 10 * 2; // todo: FPS stuff
                int frame = maxFrame - _sealSphere.DamageTimer;
                IncrementMaterialColors(material, _sealSphere.Ambient, _sealSphere.Diffuse, frame, maxFrame);
            }
            _lightOverride = true;
            DrawGeneric();
            _lightOverride = false;
            if (_goreaFlags.TestFlag(Gorea1BFlags.Bit1))
            {
                TransformGrappleEffect();
            }
            DrawMindTricks();
            if (_grappling)
            {
                DrawGrappleBeam();
                if (_scene.ProcessFrame && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
                {
                    _grappleModel.UpdateAnimFrames();
                }
            }
            return true;
        }

        private void TransformGrappleEffect()
        {
            // caled from draw
            if (_grappleEffect != null && _scene.ProcessFrame)
            {
                Vector3 last = _grappleVecs[^1] * 0.5f;
                Vector3 prev = _grappleVecs[^2] * 0.5f;
                _grappleEffect.Transform(UpVector, FacingVector, prev + last); // swap facing and up
            }
        }

        private void DrawMindTricks()
        {
            // caled from draw
            if (_scene.ProcessFrame && _scene.FrameCount != 0 && _scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                _trickModel.UpdateAnimFrames();
            }
            for (int i = 0; i < _trocra.Length; i++)
            {
                Enemy30Entity? trocra = _trocra[i];
                if (trocra != null)
                {
                    Vector3 between = trocra.Position - _sealSphere.Position;
                    float distance = between.Length;
                    if (distance > 1 / 128f)
                    {
                        between = between.Normalized();
                        Vector3 unitVec = Vector3.UnitY;
                        float dot = MathF.Abs(Vector3.Dot(unitVec, between));
                        if (dot > Fixed.ToFloat(4065))
                        {
                            unitVec = Vector3.UnitZ;
                        }
                        Matrix4 transform = GetTransformMatrix(between, unitVec, _sealSphere.Position);
                        transform.Row2.X *= distance;
                        transform.Row2.Y *= distance;
                        transform.Row2.Z *= distance;
                        UpdateTransforms(_trickModel, transform, recolor: 0);
                        GetDrawItems(_trickModel, 0);
                    }
                }
            }
        }

        private void DrawGrappleBeam()
        {
            Vector3 vec = Func213BF7C();
            Vector3 between = vec - _grappleVecs[0];
            if (between.LengthSquared > 1 / 128f)
            {
                var cross1 = Vector3.Cross(Vector3.UnitY, between);
                if (cross1.LengthSquared > 1 / 128f)
                {
                    cross1 = cross1.Normalized();
                    var cross2 = Vector3.Cross(between, cross1);
                    cross2 = cross2.Normalized();
                    cross1 = Matrix.Vec3MultMtx4(cross1, _grappleMtx);
                    cross2 = Matrix.Vec3MultMtx4(cross2, _grappleMtx);
                    Vector3 pos = default;
                    Func213C238(0, ref pos);
                    for (float index = 1; index < _field38; index += 1)
                    {
                        DrawGrappleSegment(ref pos, index, cross2, cross1);
                    }
                    DrawGrappleSegment(ref pos, _field38, cross2, cross1);
                }
            }
        }

        // sktodo: member name
        // sktodo: (document) coming up with successive vectors based on the fractional index
        private void Func213C238(float index, ref Vector3 pos)
        {
            Vector3 first = _grappleVecs[0];
            Vector3 last = _grappleVecs[^1];
            var axis = new Vector3(first.Z - last.Z, 0, last.X - first.X);
            if (axis.LengthSquared <= 1 / 128f)
            {
                return;
            }
            axis = axis.Normalized();
            float angle = (index + _grappleInt) * (65535 / 24f) / 16 * (360 / 4096f);
            float factor = MathF.Sin(MathHelper.DegreesToRadians(angle)) * _field24;
            axis *= factor;
            axis = Matrix.Vec3MultMtx4(axis, _grappleMtx);
            Vector3 vec = Func213C458(index);
            pos = vec + axis;
        }

        // sktodo: parameter and variable names
        private void DrawGrappleSegment(ref Vector3 pos, float index, Vector3 a5, Vector3 a6)
        {
            Vector3 vec = default;
            Func213C238(index, ref vec);
            var transform = new Matrix4(
                new Vector4(a6, 0),
                new Vector4(a5, 0),
                new Vector4(vec - pos, 0),
                new Vector4(pos, 1)
            );
            UpdateTransforms(_grappleModel, transform, recolor: 0);
            GetDrawItems(_grappleModel, 0);
            pos = vec;
        }

        #region Boilerplate

        public static bool BehaviorXX(Enemy28Entity enemy)
        {
            return enemy.BehaviorXX();
        }

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
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80
    }
}
