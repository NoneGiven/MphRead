using System;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy02Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private readonly Vector3[] _idlePoints = new Vector3[4];
        private int _field170 = 0;
        private byte _field1A4 = 0;
        private byte _drainDamageTimer = 0;
        private float _field1B8 = 0; // angle
        private Vector3 _field1BC;
        private bool _field1D0 = false;
        private bool _hitByBomb = false;

        public bool Field1D0 => _field1D0;

        public Enemy02Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action[11]
            {
                State00, State01, State02, State03, State04, State05, State06, State07, State08, State09, State10
            };
        }

        protected override bool EnemyInitialize()
        {
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            Flags &= ~EnemyFlags.CollidePlayer;
            EnemySpawnFields03 fields = _spawner.Data.Fields.S03;
            SetTransform(fields.Facing.ToFloatVector().WithY(0).Normalized(), Vector3.UnitY,
                _spawner.Data.Header.Position.ToFloatVector() + fields.Position.ToFloatVector().AddY(0.5f));
            _boundingRadius = 1;
            _hurtVolumeInit = new CollisionVolume(fields.Volume0);
            SetUpModel(Metadata.EnemyModelNames[2]); // sktodo: animation? revisit after implementing Metroid from FH
            _idlePoints[0] = Position;
            _idlePoints[1] = Position;
            _idlePoints[2] = Position;
            _idlePoints[3] = Position;
            _field1BC = Position;
            float idleX = fields.IdleRange.X.FloatValue;
            float idleZ = fields.IdleRange.Z.FloatValue;
            Vector3 facing = FacingVector;
            _idlePoints[1].X += facing.X * idleZ;
            _idlePoints[1].Z += facing.Z * idleZ;
            _idlePoints[2].X += facing.X * idleZ - facing.Z * idleX;
            _idlePoints[2].Z += facing.Z * idleZ + facing.X * idleX;
            _idlePoints[3].X -= facing.Z * idleX;
            _idlePoints[3].Z += facing.X * idleX;
            Func21648A4();
            return true;
        }

        // todo: function names
        private void Func21648A4()
        {
            if ((_state1 == 0 || _state1 == 7 || _state1 == 10) && _state2 != 0 && _state2 != 7 && _state2 != 10)
            {
                int count = 0;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.EnemyInstance && entity is Enemy02Entity temroid && temroid.Field1D0)
                    {
                        count++;
                    }
                }
                if (count >= 3)
                {
                    _state2 = _state1;
                    return;
                }
                _field1D0 = true;
            }
            else if (_state1 != 0 && _state1 != 7 && _state1 != 10 && (_state2 == 0 || _state2 == 7 || _state2 == 10))
            {
                _field1D0 = false;
            }
            if (_state2 == 0)
            {
                _models[0].SetAnimation(0);
                _speed = Vector3.Zero;
            }
            else if (_state2 == 1)
            {
                _models[0].SetAnimation(1);
            }
            else if (_state2 == 2)
            {
                // todo: stop SFX
                _models[0].SetAnimation(10, AnimFlags.NoLoop);
                _speed = Vector3.Zero;
            }
            else if (_state2 == 3)
            {
                _models[0].SetAnimation(11, AnimFlags.NoLoop);
                _field1B8 = 0;
                _field1BC = Position;
                Vector3 facing = (PlayerEntity.Main.Position - Position).AddY(0.5f).Normalized();
                SetTransform(facing, UpVector, Position);
                _speed = -(facing * 0.3f).WithY(0) / 2; // todo: FPS stuff
            }
            else if (_state2 == 4)
            {
                _models[0].SetAnimation(12, AnimFlags.NoLoop);
                Vector3 facing = (PlayerEntity.Main.Position - Position).AddY(0.5f).Normalized();
                _speed = (facing * 0.3f).WithY(0) / 2; // todo: FPS stuff   
            }
            else if (_state2 == 5)
            {
                _models[0].SetAnimation(3);
                Vector3 facing = (PlayerEntity.Main.Position - Position).AddY(0.5f).Normalized();
                _speed = facing / 2 / 2; // todo: FPS stuff
                _field170 = 20 * 2; // todo: FPS stuff
            }
            else if (_state2 == 6)
            {
                if (_models[0].AnimInfo.Index[0] == 7)
                {
                    _models[0].SetAnimation(9, AnimFlags.NoLoop);
                }
                else
                {
                    _models[0].SetAnimation(8, AnimFlags.NoLoop);
                }
            }
            else if (_state2 == 7)
            {
                _models[0].SetAnimation(0);
            }
            else if (_state2 == 8)
            {
                Vector3 facing;
                Vector3 playerFacing = PlayerEntity.Main.FacingVector;
                if (PlayerEntity.Main.IsAltForm)
                {
                    _models[0].SetAnimation(7);
                    facing = -playerFacing.WithY(0);
                    if (facing.X == 0 && facing.Z == 0)
                    {
                        facing.X = 1;
                    }
                }
                else
                {
                    _models[0].SetAnimation(4);
                    facing = -playerFacing;
                }
                SetTransform(facing.Normalized(), UpVector, Position);
                _speed = Vector3.Zero;
                _hitByBomb = false;
                _field170 = 150 * 2; // todo: FPS stuff
                _drainDamageTimer = 0;
            }
            else if (_state2 == 9)
            {
                _models[0].SetAnimation(15, AnimFlags.NoLoop);
                _speed = Vector3.Zero;
            }
            else if (_state2 == 10)
            {
                _models[0].SetAnimation(2);
                _speed = Vector3.Zero;
            }
        }

        private static bool FloatEqual(float a, float b)
        {
            return MathF.Abs(a - b) >= 1 / 4096f;
        }

        public void Func216469C(Vector3 point1, Vector3 point2, float sign)
        {
            Vector3 between = (point1 - point2).Normalized();
            Vector3 facing = FacingVector;
            if (FloatEqual(facing.X, between.X) || FloatEqual(facing.Y, between.Y) || FloatEqual(facing.Z, between.Z))
            {
                Vector3 prevFacing = facing;
                facing.X += (between.X - facing.X) / 8; // sktodo: FPS?
                facing.Z += (between.Z - facing.Z) / 8;
                if (facing.X == 0 && facing.Z == 0)
                {
                    facing.X = prevFacing.X;
                    facing.Z = prevFacing.Z;
                }
                facing.Y = between.Y;
                facing = facing.Normalized();
                if (FloatEqual(facing.X, prevFacing.X) && FloatEqual(facing.Z, prevFacing.Z))
                {
                    facing.X += 0.125f; // sktodo: FPS?
                    facing.Z -= 0.125f;
                    if (facing.X == 0 && facing.Z == 0)
                    {
                        facing.X += 0.125f; // sktodo: FPS?
                        facing.Z -= 0.125f;
                    }
                    facing = facing.Normalized();
                }
                _speed = sign * (facing * 0.1f / 2); // todo: FPS stuff
                facing.Y = 0;
                SetTransform(facing.Normalized(), UpVector, Position);
            }
        }

        protected override void Detach()
        {
            if (PlayerEntity.Main.AttachedEnemy == this)
            {
                PlayerEntity.Main.AttachedEnemy = null;
            }
            _field1D0 = false;
        }

        public void UpdateAttached(PlayerEntity player)
        {
            Vector3 position = player.Position + player.FacingVector / 2; // todo: use camera info pos
            SetTransform(-player.FacingVector, UpVector, position);
        }

        protected override void EnemyProcess()
        {
            CallStateProcess();
            if (_state1 != 8)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Door)
                    {
                        continue;
                    }
                    var door = (DoorEntity)entity;
                    Vector3 doorFacing = door.FacingVector;
                    Vector3 between = Position - door.LockPosition;
                    float magSqr = between.LengthSquared;
                    if (magSqr < door.RadiusSquared + _boundingRadius)
                    {
                        float dist = door.RadiusSquared + _boundingRadius - magSqr;
                        if (dist > 0)
                        {
                            if (Vector3.Dot(between, doorFacing) < 0)
                            {
                                dist *= -1;
                            }
                            Position += doorFacing * dist;
                            // todo: FPS stuff
                            Vector3 speed = _speed * 2;
                            float dot = -Vector3.Dot(speed, doorFacing);
                            if (dot > 0)
                            {
                                speed += doorFacing * dot / 2;
                                if (_state2 == 5)
                                {
                                    _field170 = 0;
                                }
                            }
                        }
                    }
                }
                var results = new CollisionResult[8];
                int colCount = CollisionDetection.CheckInRadius(Position, _boundingRadius, limit: 8,
                    getSimpleNormal: false, TestFlags.None, _scene, results);
                for (int i = 0; i < colCount; i++)
                {
                    CollisionResult result = results[i];
                    float dist;
                    if (result.Field0 == 0)
                    {
                        dist = _boundingRadius + result.Plane.W - Vector3.Dot(Position, result.Plane.Xyz);
                    }
                    else
                    {
                        dist = _boundingRadius - result.Field14;
                    }
                    if (dist > 0)
                    {
                        Position += result.Plane.Xyz * dist;
                        // todo: FPS stuff
                        Vector3 speed = _speed * 2;
                        float dot = -Vector3.Dot(speed, result.Plane.Xyz);
                        if (dot > 0)
                        {
                            speed += result.Plane.Xyz * dot / 2;
                            if (_state2 == 5)
                            {
                                _field170 = 0;
                            }
                        }
                    }
                }
            }
        }

        private void State00()
        {
            Vector3 between = _idlePoints[_field1A4] - Position;
            if (between.LengthSquared < 1 && ++_field1A4 >= 4)
            {
                _field1A4 = 0;
            }
            Func216469C(_idlePoints[_field1A4], Position, sign: 1);
            State02();
        }

        private void State01()
        {
            Func216469C(_scene.CameraPosition, PlayerEntity.Main.Position, sign: 1);
            State02();
        }

        private void State02()
        {
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(15, DamageFlags.None, direction: null, this);
            }
            if (CallSubroutine(Metadata.Enemy02Subroutines, this))
            {
                Func21648A4();
            }
        }

        private void UpdateHeight(float sign)
        {
            _field1B8 += sign * (45f / _models[0].AnimInfo.FrameCount[0] / 2); // todo: FPS stuff
            if (_field1B8 >= 360)
            {
                _field1B8 = 0;
            }
            _speed.Y = _field1BC.Y + MathF.Sin(_field1B8) / 2 - Position.Y;
        }

        private void State03()
        {
            UpdateHeight(sign: 1);
            State02();
        }

        private void State04()
        {
            UpdateHeight(sign: -1);
            State02();
        }

        private void State05()
        {
            if (CallSubroutine(Metadata.Enemy02Subroutines, this))
            {
                Func21648A4();
            }
        }

        private void State06()
        {
            Func216469C(Position, _idlePoints[0], sign: -1);
            if (CallSubroutine(Metadata.Enemy02Subroutines, this))
            {
                Func21648A4();
            }
        }

        private void State07()
        {
            Func216469C(_idlePoints[0], Position, sign: 1);
            State02();
        }

        private void State08()
        {
            int animId = _models[0].AnimInfo.Index[0];
            Vector3 facing = FacingVector;
            Vector3 playerPos = PlayerEntity.Main.Position;
            Vector3 playerFacing = PlayerEntity.Main.FacingVector;
            if (PlayerEntity.Main.IsMorphing)
            {
                if (facing.Y != 0)
                {
                    if (facing.X == 0 && facing.Z == 0)
                    {
                        facing.X = 1;
                    }
                }
                if (animId == 4 || animId == 14)
                {
                    _models[0].SetAnimation(13, AnimFlags.NoLoop);
                }
                SetTransform(facing.Normalized(), UpVector, playerPos.AddY(0.625f));
            }
            else if (PlayerEntity.Main.IsUnmorphing)
            {
                if (facing.Y == 0)
                {
                    facing.Y = -playerFacing.Y;
                }
                if (animId == 7 || animId == 13)
                {
                    _models[0].SetAnimation(14, AnimFlags.NoLoop);
                }
                AnimationInfo animInfo = _models[0].AnimInfo;
                int frameCount = animInfo.FrameCount[0];
                int animFrame = animInfo.Frame[0];
                Vector3 cameraPos = playerPos; // todo: use player camera info
                Vector3 postion = (playerPos.AddY(0.625f) * (frameCount - animFrame) + (cameraPos + playerFacing / 2) * animFrame) / frameCount;
                SetTransform(facing.Normalized(), UpVector, postion);
            }
            else if (PlayerEntity.Main.IsAltForm)
            {
                if (facing.Y != 0)
                {
                    if (facing.X == 0 && facing.Z == 0)
                    {
                        facing.X = 1;
                    }
                }
                if (animId == 4 || animId == 13 && _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _models[0].SetAnimation(7);
                }
                SetTransform(facing.Normalized(), UpVector, playerPos.AddY(0.625f));
            }
            else
            {
                if (facing.Y == 0)
                {
                    facing.Y = -playerFacing.Y;
                }
                if (animId == 7 || animId == 14 && _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _models[0].SetAnimation(4);
                }
                SetTransform(facing.Normalized(), UpVector, Position);
            }
            if (_drainDamageTimer > 0)
            {
                _drainDamageTimer--;
            }
            else
            {
                PlayerEntity.Main.TakeDamage(2, DamageFlags.NoDmgInvuln, direction: null, this);
                _drainDamageTimer = 8 * 2; // todo: FPS stuff
            }
            if (CallSubroutine(Metadata.Enemy02Subroutines, this))
            {
                PlayerEntity.Main.AttachedEnemy = null;
                Func21648A4();
            }
        }

        private void State09()
        {
            if (CallSubroutine(Metadata.Enemy02Subroutines, this))
            {
                Func21648A4();
            }
        }

        private void State10()
        {
            State02();
        }

        private bool Behavior00()
        {
            return _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended);
        }

        private bool Behavior01()
        {
            return PlayerEntity.Main.AttachedEnemy == null;
        }

        private bool Behavior02()
        {
            if (_field170 > 0)
            {
                _field170--;
                return false;
            }
            return true;
        }

        private bool Behavior03()
        {
            if (_health == 0 || !HitPlayers[PlayerEntity.Main.SlotIndex] || PlayerEntity.Main.AttachedEnemy != null)
            {
                return false;
            }
            PlayerEntity.Main.AttachedEnemy = this;
            return true;
        }

        private bool Behavior04()
        {
            return _framesSinceDamage == 1;
        }

        private bool Behavior05()
        {
            CollisionResult result = default;
            if (CollisionDetection.CheckBetweenPoints(Position, PlayerEntity.Main.Position, TestFlags.None, _scene, ref result))
            {
                return false;
            }
            Vector3 between = _scene.CameraPosition - Position;
            return between.LengthSquared < 42.25f;
        }

        private bool Behavior06()
        {
            Vector3 between = _idlePoints[0] - Position;
            return between.LengthSquared < 1;
        }

        private bool Behavior07()
        {
            if (!_hitByBomb)
            {
                return false;
            }
            _hitByBomb = false;
            return true;
        }

        private bool Behavior08()
        {
            return PlayerEntity.Main.Health == 0;
        }

        private bool Behavior09()
        {
            CollisionResult result = default;
            return CollisionDetection.CheckBetweenPoints(Position, PlayerEntity.Main.Position, TestFlags.None, _scene, ref result);
        }

        private bool Behavior10()
        {
            Vector3 between = PlayerEntity.Main.Position - Position;
            return between.LengthSquared < 100;
        }

        private bool Behavior11()
        {
            return PlayerEntity.Main.AttachedEnemy != null;
        }

        private bool Behavior12()
        {
            Vector3 between = PlayerEntity.Main.Position - Position;
            return between.LengthSquared < 25;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy02Entity enemy)
        {
            return enemy.Behavior00();
        }

        public static bool Behavior01(Enemy02Entity enemy)
        {
            return enemy.Behavior01();
        }

        public static bool Behavior02(Enemy02Entity enemy)
        {
            return enemy.Behavior02();
        }

        public static bool Behavior03(Enemy02Entity enemy)
        {
            return enemy.Behavior03();
        }

        public static bool Behavior04(Enemy02Entity enemy)
        {
            return enemy.Behavior04();
        }

        public static bool Behavior05(Enemy02Entity enemy)
        {
            return enemy.Behavior05();
        }

        public static bool Behavior06(Enemy02Entity enemy)
        {
            return enemy.Behavior06();
        }

        public static bool Behavior07(Enemy02Entity enemy)
        {
            return enemy.Behavior07();
        }

        public static bool Behavior08(Enemy02Entity enemy)
        {
            return enemy.Behavior08();
        }

        public static bool Behavior09(Enemy02Entity enemy)
        {
            return enemy.Behavior09();
        }

        public static bool Behavior10(Enemy02Entity enemy)
        {
            return enemy.Behavior10();
        }

        public static bool Behavior11(Enemy02Entity enemy)
        {
            return enemy.Behavior11();
        }

        public static bool Behavior12(Enemy02Entity enemy)
        {
            return enemy.Behavior12();
        }

        #endregion
    }
}
