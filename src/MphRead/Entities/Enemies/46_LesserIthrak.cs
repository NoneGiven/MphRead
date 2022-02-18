using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy46Entity : EnemyInstanceEntity
    {
        protected readonly EnemySpawnEntity _spawner;
        private CollisionVolume _homeVolume;
        private CollisionVolume _rangeVolume;
        private CollisionVolume _warnVolume;
        private Enemy50Entity? _hitZone = null;

        private Vector3 _moveTarget;
        private Vector3 _moveStart;
        private float _dropAngleSign = 1;
        private float _recoilAngleSign = 1;
        private float _moveDistSqr = 0;

        private Vector3 _targetVec;
        private float _aimAngleStep = 0;
        private ushort _stepCount = 0; // also used as a timer

        private ushort _delayTimer = 0;
        private ushort _moveTimer = 0; // basically a timeout
        private Vector3 _acceleration;
        private bool _wallCol = false;
        private bool _groundCol = false;
        private bool _reachingTarget = false;

        private readonly float _stepDistance = 0.5f / 2; // todo: FPS stuff
        private readonly float _accelSteps = 10 * 2; // todo: FPS stuff

        private bool AnimEnded => _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended);

        protected Material _mouthMaterial = null!;

        public Enemy46Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[20]
            {
                State00, State01, State02, State03, State04, State05, State06, State07, State08, State09,
                State10, State11, State12, State13, State14, State15, State16, State17, State18, State19
            };
        }

        protected override bool EnemyInitialize()
        {
            EnemySpawnEntityData data = _spawner.Data;
            Setup(data.Header.Position.ToFloatVector(), data.Header.FacingVector.ToFloatVector(), effectiveness: 0x5555,
                data.Fields.S00.Volume0, data.Fields.S00.Volume2, data.Fields.S00.Volume1, data.Fields.S00.Volume3);
            return true;
        }

        protected void Setup(Vector3 position, Vector3 facing, int effectiveness, RawCollisionVolume hurtVolume,
            RawCollisionVolume volume1, RawCollisionVolume volume2, RawCollisionVolume volume3)
        {
            SetTransform(facing.Normalized(), Vector3.UnitY, position);
            _health = _healthMax = 85;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _boundingRadius = 0.5f;
            _hurtVolumeInit = new CollisionVolume(hurtVolume);
            _homeVolume = CollisionVolume.Move(volume1, Position);
            _rangeVolume = CollisionVolume.Move(volume2, Position);
            _warnVolume = CollisionVolume.Move(volume3, Position);
            ModelInstance inst = SetUpModel(Metadata.EnemyModelNames[(int)EnemyType]);
            inst.SetAnimation(5, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            inst.SetAnimation(15, slot: 1, SetFlags.Texcoord);
            for (int i = 0; i < inst.Model.Materials.Count; i++)
            {
                Material material = inst.Model.Materials[i];
                if (material.Name == "Mouth_tga")
                {
                    _mouthMaterial = material;
                    break;
                }
            }
            _delayTimer = 30 * 2; // todo: FPS stuff
            _moveStart = Position;
            _moveTimer = 600 * 2; // todo: FPS stuff
            Metadata.LoadEffectiveness(effectiveness, BeamEffectiveness);
            SpawnHitZone();
        }

        private void SetNodeAnim(int id, AnimFlags flags = AnimFlags.None)
        {
            _models[0].SetAnimation(id, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, flags);
        }

        private void SpawnHitZone()
        {
            _hitZone = EnemySpawnEntity.SpawnEnemy(this, EnemyType.HitZone, NodeRef, _scene) as Enemy50Entity;
            if (_hitZone != null)
            {
                _scene.AddEntity(_hitZone);
                _hitZone.Transform = GetTransformMatrix(FacingVector, Vector3.UnitY, Position);
                Metadata.LoadEffectiveness(0xFFFF, _hitZone.BeamEffectiveness);
                _hitZone.HitPlayers[0] = true;
                var hurtVolume = new CollisionVolume(new Vector3(0, 0.93f, -0.8f), 0.6f);
                _hitZone.SetUp(_health, hurtVolume, boundingRadius: 1);
            }
        }

        protected override void EnemyProcess()
        {
            if (_state1 == 9)
            {
                if (_speed.LengthSquared <= Fixed.ToFloat(50) / 2) // todo: FPS stuff
                {
                    _speed = Vector3.Zero;
                }
                else
                {
                    _speed.X -= _acceleration.X / 2; // todo: FPS stuff
                    _speed.Z -= _acceleration.Z / 2; // todo: FPS stuff
                }
            }
            else if (_state1 == 3 || _state1 == 4 || _state1 == 5 || _state1 == 6 || _state1 == 8)
            {
                Vector3 facing = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
                SetTransform(facing, Vector3.UnitY, Position);
            }
            if (_state1 != 0 && _state1 != 1 && _state1 != 2)
            {
                if (_state1 != 7 && _state1 != 19)
                {
                    HandleBlockingCollision(Position.AddY(0.5f), _hurtVolume, updateSpeed: true, ref _groundCol, ref _wallCol);
                }
                if (_state1 != 7 && _state1 != 8)
                {
                    ContactDamagePlayer(15, knockback: true);
                }
            }
            CallStateProcess();
            if (_state1 != 0 && _state1 != 1 && _state1 != 2 && !_groundCol)
            {
                _speed.Y -= Fixed.ToFloat(100) / 4; // todo: FPS stuff
            }
        }

        // dropping, returning to home, roaming
        private void PickMoveTarget(CollisionVolume volume)
        {
            _moveStart = Position;
            Vector3 moveTarget;
            if (volume.Type == VolumeType.Cylinder || volume.Type == VolumeType.Sphere)
            {
                float radius = volume.Type == VolumeType.Cylinder ? volume.CylinderRadius : volume.SphereRadius;
                Vector3 pos = volume.Type == VolumeType.Cylinder ? volume.CylinderPosition : volume.SpherePosition;
                float dist = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(radius)));
                var vec = new Vector3(dist, 0, 0);
                _dropAngleSign *= -1;
                float randAngle = Fixed.ToFloat(Rng.GetRandomInt2(0xB4000)) * _dropAngleSign; // [0-180)
                var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(randAngle));
                vec = Matrix.Vec3MultMtx3(vec, rotY);
                moveTarget = new Vector3(
                    pos.X + vec.X,
                    Position.Y - 20,
                    pos.Z + vec.Z
                );
            }
            else
            {
                Debug.Assert(volume.Type == VolumeType.Box);
                float distX = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(volume.BoxDot1)));
                float distZ = Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(volume.BoxDot3)));
                moveTarget = new Vector3(
                    volume.BoxVector1.X * distX + volume.BoxVector3.X * distZ + volume.BoxPosition.X,
                    Position.Y - 20,
                    volume.BoxVector1.Z * distX + volume.BoxVector3.Z * distZ + volume.BoxPosition.Z
                );
            }
            CollisionResult result = default;
            CollisionDetection.CheckBetweenPoints(Position, moveTarget, TestFlags.None, _scene, ref result);
            _moveTarget = moveTarget.WithY(result.Position.Y);
            _moveDistSqr = (_moveTarget - Position).WithY(0).LengthSquared;
        }

        private bool HandleCollision(Vector3 testPos)
        {
            _groundCol = false;
            testPos = testPos.AddY(0.5f);
            var results = new CollisionResult[30];
            int count = CollisionDetection.CheckInRadius(testPos, _boundingRadius, limit: 30,
                getSimpleNormal: false, TestFlags.None, _scene, results);
            if (count == 0)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                CollisionResult result = results[i];
                float v12;
                if (result.Field0 != 0)
                {
                    v12 = _boundingRadius - result.Field14;
                }
                else
                {
                    v12 = _boundingRadius + result.Plane.W - Vector3.Dot(testPos, result.Plane.Xyz);
                }
                if (v12 > 0)
                {
                    Position += result.Plane.Xyz * v12;
                    if (result.Plane.Y >= 0.1f || result.Plane.Y <= -0.1f)
                    {
                        _groundCol = true;
                    }
                    else
                    {
                        _wallCol = true;
                    }
                    float dot = Vector3.Dot(_speed, result.Plane.Xyz);
                    if (dot < 0)
                    {
                        _speed += result.Plane.Xyz * -dot;
                    }
                }
            }
            return true;
        }

        protected virtual void CallSubroutine()
        {
            CallSubroutine(Metadata.Enemy46Subroutines, this);
        }

        private void State00()
        {
            CallSubroutine();
        }

        private void State01()
        {
            CallSubroutine();
        }

        private void State02()
        {
            CallSubroutine();
        }

        private void State03()
        {
            Vector3 facing = FacingVector;
            _speed.X = facing.X * 0.15f / 2; // todo: FPS stuff
            _speed.Z = facing.Z * 0.15f / 2; // todo: FPS stuff
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_WALK, loop: true);
            CallSubroutine();
        }

        private void State04()
        {
            CallSubroutine();
        }

        private void State05()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State06()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State07()
        {
            CallSubroutine();
        }

        private void State08()
        {
            _speed.X = 0;
            _speed.Z = 0;
            if (_delayTimer == 15 * 2) // todo: FPS stuff
            {
                SetNodeAnim(2, AnimFlags.NoLoop);
            }
            else if (_delayTimer == 0)
            {
                PlayerEntity.Main.TakeDamage(10, DamageFlags.NoDmgInvuln, _speed * 2, this); // todo: FPS stuff
                _delayTimer = 30 * 2; // todo: FPS stuff
            }
            if (_delayTimer > 0)
            {
                _delayTimer--;
            }
            CallSubroutine();
        }

        private void State09()
        {
            CallSubroutine();
        }

        private void State10()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State11()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State12()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State13()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State14()
        {
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_WALK, loop: true);
            CallSubroutine();
        }

        private void State15()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State16()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State17()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State18()
        {
            _speed.X = 0;
            _speed.Z = 0;
            CallSubroutine();
        }

        private void State19()
        {
            CallSubroutine();
        }

        protected bool Behavior00()
        {
            if (!AnimEnded)
            {
                return false;
            }
            SetNodeAnim(16);
            PickMoveTarget(_homeVolume);
            _targetVec = (_moveTarget - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _stepCount = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _stepCount;
            return true;
        }

        protected bool Behavior01()
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            SetNodeAnim(13);
            Vector3 facing = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            SetTransform(facing, Vector3.UnitY, Position);
            _speed = new Vector3(facing.X * 0.15f, 0, facing.Z * 0.15f);
            _speed /= 2; // todo: FPS stuff
            _stepCount = 3 * 2; // todo: FPS stuff
            return true;
        }

        // player in warning range
        protected bool Behavior02()
        {
            if (!_warnVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            SetNodeAnim(6);
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_WARN);
            return true;
        }

        protected bool Behavior03()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _stepCount, _aimAngleStep))
            {
                return false;
            }
            SetNodeAnim(13);
            Vector3 facing = FacingVector;
            _speed = new Vector3(facing.X * 0.15f, 0, facing.Z * 0.15f);
            _speed /= 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior04()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _stepCount, _aimAngleStep))
            {
                return false;
            }
            SetNodeAnim(13);
            Vector3 facing = FacingVector;
            _speed = new Vector3(facing.X * 0.15f, 0, facing.Z * 0.15f);
            _speed /= 2; // todo: FPS stuff
            _stepCount = 50 * 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior05()
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            if (_state1 == 2)
            {
                if (HandleCollision(Position))
                {
                    SetNodeAnim(14);
                    _moveTarget.Y = Position.Y;
                    _speed.X = 0;
                    _speed.Z = 0;
                    _stepCount = 60 * 2; // todo: FPS stuff
                    _soundSource.PlaySfx(SfxId.HANGING_TERROR_SCREAM_SCR);
                    return true;
                }
            }
            else if (_state1 == 7)
            {
                Vector3 facing = FacingVector;
                Vector3 destPos = Position + facing * 2;
                if (HandleCollision(Position) || HandleCollision(destPos))
                {
                    SetNodeAnim(10, AnimFlags.NoLoop);
                    _speed = new Vector3(facing.X * 0.18f, 0, facing.Z * 0.18f);
                    _speed /= 2; // todo: FPS stuff
                    _acceleration = _speed / _accelSteps;
                    return true;
                }
            }
            return false;
        }

        protected bool Behavior06()
        {
            if (!AnimEnded)
            {
                return false;
            }
            SetNodeAnim(16);
            _targetVec = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _stepCount = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _stepCount;
            return true;
        }

        protected bool Behavior07()
        {
            if (!HandleCollision(Position))
            {
                return false;
            }
            SetNodeAnim(13);
            Vector3 facing = FacingVector;
            _speed = new Vector3(facing.X * 0.15f, 0, facing.Z * 0.15f);
            _speed /= 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior08()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _stepCount, _aimAngleStep))
            {
                return false;
            }
            SetNodeAnim(14, AnimFlags.NoLoop);
            return true;
        }

        protected bool Behavior09()
        {
            if (!AnimEnded)
            {
                return false;
            }
            SetNodeAnim(4, AnimFlags.NoLoop);
            _wallCol = false;
            return true;
        }

        protected bool Behavior10()
        {
            if (!SeekTargetFacing(_targetVec, Vector3.UnitY, ref _stepCount, _aimAngleStep))
            {
                return false;
            }
            SetNodeAnim(14, AnimFlags.NoLoop);
            // in-game, 47 calls stop_sfx here while 46 does not
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_SCREAM_SCR);
            _stepCount = 50 * 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior11()
        {
            PlayerEntity mainPlayer = PlayerEntity.Main;
            CollisionVolume playerCol = mainPlayer.Volume;
            Debug.Assert(playerCol.Type == VolumeType.Sphere);
            playerCol.SpherePosition.Y += Fixed.ToFloat(1000);
            playerCol.SphereRadius += Fixed.ToFloat(1000);
            CollisionResult result = default;
            if (!CollisionDetection.CheckVolumesOverlap(playerCol, _hurtVolume, ref result))
            {
                return false;
            }
            mainPlayer.HandleCollision(result);
            _speed.X = 0;
            _speed.Z = 0;
            SetNodeAnim(9, AnimFlags.NoLoop);
            mainPlayer.TakeDamage(15, DamageFlags.None, _speed * 2, this); // todo: FPS stuff
            return true;
        }

        protected bool Behavior12()
        {
            if (!AnimEnded)
            {
                return false;
            }
            _stepCount = 50 * 2; // todo: FPS stuff
            Vector3 facing = FacingVector;
            _speed = new Vector3(facing.X * 0.15f, 0, facing.Z * 0.15f);
            _speed /= 2; // todo: FPS stuff
            SetNodeAnim(13);
            return true;
        }

        protected bool Behavior13()
        {
            Vector3 playerPos = PlayerEntity.Main.Position;
            if (!_rangeVolume.TestPoint(playerPos) || !_rangeVolume.TestPoint(Position))
            {
                return false;
            }
            SetNodeAnim(16);
            _targetVec = (playerPos - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _stepCount = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _stepCount;
            _speed = Vector3.Zero;
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            return true;
        }

        protected bool Behavior14()
        {
            if (_warnVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            SetNodeAnim(5);
            return true;
        }

        protected bool Behavior15()
        {
            if (!_rangeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            PickMoveTarget(_homeVolume);
            Vector3 travel = _moveTarget - Position;
            float distance = travel.Length;
            _stepCount = (ushort)((distance / _stepDistance) + 1);
            _speed = travel * (_stepDistance / distance);
            SetNodeAnim(12, AnimFlags.NoLoop);
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_DROP);
            return true;
        }

        private void StartRecoil()
        {
            float factor = Fixed.ToFloat(500);
            Vector3 facing = FacingVector;
            var vec = new Vector3(-facing.X * factor, 0, -facing.Z * factor);
            _recoilAngleSign *= -1;
            float randAngle = (Fixed.ToFloat(Rng.GetRandomInt2(0x1E000)) + 30) * _recoilAngleSign; // [30-60)
            var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(randAngle));
            vec = Matrix.Vec3MultMtx3(vec, rotY);
            _speed = new Vector3(vec.X, Fixed.ToFloat(1000), vec.Z);
            _speed /= 2; // todo: FPS stuff
        }

        protected bool Behavior16()
        {
            if ((Position - PlayerEntity.Main.Position).LengthSquared >= 1.5f * 1.5f)
            {
                return false;
            }
            Vector3 destPos = Position + FacingVector * -2;
            CollisionResult discard = default;
            if (CollisionDetection.CheckBetweenPoints(Position, destPos, TestFlags.None, _scene, ref discard))
            {
                return false;
            }
            StartRecoil();
            SetNodeAnim(7, AnimFlags.NoLoop);
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            return true;
        }

        // start lunge
        protected bool Behavior17()
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            Vector3 facing = FacingVector;
            _speed = facing * Fixed.ToFloat(1800);
            _speed.Y = Fixed.ToFloat(600);
            _speed /= 2; // todo: FPS stuff
            _stepCount = 7 * 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior18()
        {
            Vector3 between = Position - PlayerEntity.Main.Position;
            float distSqr = between.LengthSquared;
            if (distSqr <= 1.5f * 1.5f || distSqr >= 2 * 2)
            {
                return false;
            }
            _stepCount = 0;
            _speed = Vector3.Zero;
            _delayTimer = 15 * 2; // todo: FPS stuff
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            return true;
        }

        protected bool Behavior19()
        {
            if (_reachingTarget)
            {
                SetNodeAnim(16);
                PickMoveTarget(_homeVolume);
                _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
                _speed = Vector3.Zero;
                _targetVec = (_moveTarget - Position).WithY(0).Normalized();
                float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
                _stepCount = 10 * 2; // todo: FPS stuff
                _aimAngleStep = angle / _stepCount;
                _reachingTarget = false;
                return true;
            }
            Vector3 nextPos = Position + _speed;
            Vector3 travel = _moveStart - nextPos;
            if (travel.LengthSquared > _moveDistSqr)
            {
                _speed = nextPos - Position;
                _reachingTarget = true;
            }
            return false;
        }

        protected bool Behavior20()
        {
            if (_moveTimer > 0)
            {
                _moveTimer--;
                return false;
            }
            SetNodeAnim(16);
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            _speed = Vector3.Zero;
            _targetVec = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            float angle = MathHelper.RadiansToDegrees(MathF.Acos(Vector3.Dot(FacingVector, _targetVec)));
            _stepCount = 10 * 2; // todo: FPS stuff
            _aimAngleStep = angle / _stepCount;
            _moveTimer = 600 * 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior21()
        {
            if (_delayTimer > 0 || (Position - PlayerEntity.Main.Position).LengthSquared <= 2 * 2)
            {
                return false;
            }
            SetNodeAnim(13);
            Vector3 facing = (PlayerEntity.Main.Position - Position).WithY(0).Normalized();
            SetTransform(facing, Vector3.UnitY, Position);
            _speed = new Vector3(facing.X * 0.15f, 0, facing.Z * 0.15f);
            _speed /= 2; // todo: FPS stuff
            _delayTimer = 30 * 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior22()
        {
            // todo: FPS stuff
            // --> in addition to the timer, we have to halve the random chance
            if (_delayTimer < 15 * 2 || Rng.GetRandomInt2(0x64000) >= 2048 / 2)
            {
                return false;
            }
            StartRecoil();
            SetNodeAnim(7, AnimFlags.NoLoop);
            return true;
        }

        protected bool Behavior23()
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return false;
            }
            SetNodeAnim(8);
            _stepCount = 12 * 2; // todo: FPS stuff
            return true;
        }

        protected bool Behavior24()
        {
            Vector3 between = Position - PlayerEntity.Main.Position;
            float distSqr = between.LengthSquared;
            if (distSqr <= 3.5f * 3.5f || distSqr >= 5 * 5)
            {
                return false;
            }
            SetNodeAnim(1);
            _speed = Vector3.Zero;
            _stepCount = 38 * 2; // todo: FPS stuff
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_ATTACK1_SCR);
            return true;
        }

        protected bool Behavior25()
        {
            if (_rangeVolume.TestPoint(Position) && _rangeVolume.TestPoint(PlayerEntity.Main.Position))
            {
                return false;
            }
            SetNodeAnim(14, AnimFlags.NoLoop);
            _soundSource.StopSfx(SfxId.HANGING_TERROR_WALK);
            _soundSource.PlaySfx(SfxId.HANGING_TERROR_SCREAM_SCR);
            _speed = Vector3.Zero;
            return true;
        }

        protected virtual void UpdateMouthMaterial()
        {
            _mouthMaterial.Diffuse = new ColorRgb(31, 31, 31);
        }

        protected override bool EnemyGetDrawInfo()
        {
            UpdateMouthMaterial();
            return false;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy46Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy46Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy46Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy46Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy46Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy46Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy46Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy46Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy46Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy46Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy46Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy46Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy46Entity enemy)
        {
            return enemy.Behavior12();
        }

        public static bool Behavior13(Enemy46Entity enemy)
        {
            return enemy.Behavior13();
        }

        public static bool Behavior14(Enemy46Entity enemy)
        {
            return enemy.Behavior14();
        }

        public static bool Behavior15(Enemy46Entity enemy)
        {
            return enemy.Behavior15();
        }

        public static bool Behavior16(Enemy46Entity enemy)
        {
            return enemy.Behavior16();
        }

        public static bool Behavior17(Enemy46Entity enemy)
        {
            return enemy.Behavior17();
        }

        public static bool Behavior18(Enemy46Entity enemy)
        {
            return enemy.Behavior18();
        }

        public static bool Behavior19(Enemy46Entity enemy)
        {
            return enemy.Behavior19();
        }

        public static bool Behavior20(Enemy46Entity enemy)
        {
            return enemy.Behavior20();
        }

        public static bool Behavior21(Enemy46Entity enemy)
        {
            return enemy.Behavior21();
        }

        public static bool Behavior22(Enemy46Entity enemy)
        {
            return enemy.Behavior22();
        }

        public static bool Behavior23(Enemy46Entity enemy)
        {
            return enemy.Behavior23();
        }

        public static bool Behavior24(Enemy46Entity enemy)
        {
            return enemy.Behavior24();
        }

        public static bool Behavior25(Enemy46Entity enemy)
        {
            return enemy.Behavior25();
        }

        #endregion
    }
}
