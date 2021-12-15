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

        public Enemy02Entity(EnemyInstanceEntityData data) : base(data)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
            _stateProcesses = new Action<Scene>[11]
            {
                State00, State01, State02, State03, State04, State05, State06, State07, State08, State09, State10
            };
        }

        protected override bool EnemyInitialize(Scene scene)
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
            Func21648A4(scene);
            return true;
        }

        // todo: function names
        private void Func21648A4(Scene scene)
        {
            if ((_state1 == 0 || _state1 == 7 || _state1 == 10) && _state2 != 0 && _state2 != 7 && _state2 != 10)
            {
                int count = 0;
                for (int i = 0; i < scene.Entities.Count; i++)
                {
                    EntityBase entity = scene.Entities[i];
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
                Vector3 facing = (scene.CameraPosition - Position).Normalized(); // todo: use player position with Y + 0.5f
                SetTransform(facing, UpVector, Position);
                _speed = -(facing * 0.3f).WithY(0) / 2; // todo: FPS stuff
            }
            else if (_state2 == 4)
            {
                _models[0].SetAnimation(12, AnimFlags.NoLoop);
                Vector3 facing = (scene.CameraPosition - Position).Normalized(); // todo: use player position with Y + 0.5f
                _speed = (facing * 0.3f).WithY(0) / 2; // todo: FPS stuff   
            }
            else if (_state2 == 5)
            {
                _models[0].SetAnimation(3);
                Vector3 facing = (scene.CameraPosition - Position).Normalized(); // todo: use player position with Y + 0.5f
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
                bool altForm = false; // todo: check flags and get from main player
                Vector3 playerFacing = Vector3.UnitZ;
                if (altForm)
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
            // todo: update main player's attached enemy reference
            _field1D0 = false;
        }

        public void UpdateAttached(PlayerEntity player)
        {
            // todo: this
        }

        protected override void EnemyProcess(Scene scene)
        {
            CallStateProcess(scene);
            // todo: handle collision with doors
            var results = new CollisionResult[8];
            int colCount = CollisionDetection.CheckInRadius(Position, _boundingRadius, limit: 8,
                getSimpleNormal: false, TestFlags.None, scene, results);

            // sktodo: convert this to float math
            static float DoThing(float value, float factor)
            {
                int v31 = (int)(factor * 4096);
                int v39 = (int)(value * 4096);
                int v40 = v39 * v31;
                int v41 = (int)((ulong)(v39 * (long)v31) >> 32);
                int v42;
                if (v41 < 0)
                {
                    v42 = -((-v40 >> 12) | (-1048576 * (v41 + (v40 != 0 ? 1 : 0))));
                }
                else
                {
                    v42 = (v40 >> 12) | (v41 << 20);
                }
                return v42 / 4096f;
            }

            for (int i = 0; i < results.Length; i++)
            {
                CollisionResult result = results[i];
                float rad;
                if (result.Field0 == 0)
                {
                    rad = _boundingRadius + result.Plane.W - Vector3.Dot(Position, result.Plane.Xyz);
                }
                else
                {
                    rad = _boundingRadius - result.Field14;
                }
                if (rad > 0)
                {
                    var b = new Vector3(DoThing(result.Plane.X, rad), DoThing(result.Plane.Y, rad), DoThing(result.Plane.Z, rad));
                    Position += b;
                    float v44 = -Vector3.Dot(_speed, result.Plane.Xyz);
                    if (v44 > 0)
                    {
                        var c = new Vector3(DoThing(result.Plane.X, v44), DoThing(result.Plane.Y, v44), DoThing(result.Plane.Z, v44));
                        _speed += c;
                        if (_state2 == 5)
                        {
                            _field170 = 0;
                        }
                    }
                }
            }
        }

        private void State00(Scene scene)
        {
            Vector3 between = _idlePoints[_field1A4] - Position;
            if (between.LengthSquared < 1 && ++_field1A4 >= 4)
            {
                _field1A4 = 0;
            }
            Func216469C(_idlePoints[_field1A4], Position, sign: 1);
            State02(scene);
        }

        private void State01(Scene scene)
        {
            Func216469C(scene.CameraPosition, Position, sign: 1); // todo: use main player position
            State02(scene);
        }

        private void State02(Scene scene)
        {
            if ((HitPlayers & 1) != 0) // todo: use player slot index
            {
                // todo: damage player
            }
            if (CallSubroutine(Metadata.Enemy02Subroutines, this, scene))
            {
                Func21648A4(scene);
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

        private void State03(Scene scene)
        {
            UpdateHeight(sign: 1);
            State02(scene);
        }

        private void State04(Scene scene)
        {
            UpdateHeight(sign: -1);
            State02(scene);
        }

        private void State05(Scene scene)
        {
            if (CallSubroutine(Metadata.Enemy02Subroutines, this, scene))
            {
                Func21648A4(scene);
            }
        }

        private void State06(Scene scene)
        {
            Func216469C(Position, _idlePoints[0], sign: -1);
            if (CallSubroutine(Metadata.Enemy02Subroutines, this, scene))
            {
                Func21648A4(scene);
            }
        }

        private void State07(Scene scene)
        {
            Func216469C(_idlePoints[0], Position, sign: 1);
            State02(scene);
        }

        private void State08(Scene scene)
        {
            int animId = _models[0].AnimInfo.Index[0];
            Vector3 facing = FacingVector;
            // todo: get from main player
            bool morphing = false;
            bool unmorphing = false;
            bool altForm = false;
            Vector3 playerPos = scene.CameraPosition;
            Vector3 playerFacing = Vector3.UnitZ;
            if (morphing)
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
            else if (unmorphing)
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
                Vector3 cameraPos = scene.CameraPosition; // todo: use player camera info
                Vector3 postion = (playerPos.AddY(0.625f) * (frameCount - animFrame) + (cameraPos + playerFacing / 2) * animFrame) / frameCount;
                SetTransform(facing.Normalized(), UpVector, postion);
            }
            else if (altForm)
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
                // todo: damage player
                _drainDamageTimer = 8 * 2; // todo: FPS stuff
            }
            if (CallSubroutine(Metadata.Enemy02Subroutines, this, scene))
            {
                // todo: update main player's attached enemy reference
                Func21648A4(scene);
            }
        }

        private void State09(Scene scene)
        {
            if (CallSubroutine(Metadata.Enemy02Subroutines, this, scene))
            {
                Func21648A4(scene);
            }
        }

        private void State10(Scene scene)
        {
            State02(scene);
        }

        private bool Behavior00(Scene scene)
        {
            return _models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended);
        }

        private bool Behavior01(Scene scene)
        {
            return true; // todo: return whether main player has no attached enemy
        }

        private bool Behavior02(Scene scene)
        {
            if (_field170 > 0)
            {
                _field170--;
                return false;
            }
            return true;
        }

        private bool Behavior03(Scene scene)
        {
            // todo: use player slot index
            // todo: also test if player has an attached enemy
            if ((HitPlayers & 1) == 0 || _health == 0)
            {
                return false;
            }
            // todo: update main player's attached enemy reference
            return true;
        }

        private bool Behavior04(Scene scene)
        {
            return _framesSinceDamage == 1;
        }

        private bool Behavior05(Scene scene)
        {
            // todo: use player position
            CollisionResult result = default;
            if (CollisionDetection.CheckBetweenPoints(Position, scene.CameraPosition, TestFlags.None, scene, ref result))
            {
                return false;
            }
            Vector3 between = scene.CameraPosition - Position;
            return between.LengthSquared < 42.25f;
        }

        private bool Behavior06(Scene scene)
        {
            Vector3 between = _idlePoints[0] - Position;
            return between.LengthSquared < 1;
        }

        private bool Behavior07(Scene scene)
        {
            if (!_hitByBomb)
            {
                return false;
            }
            _hitByBomb = false;
            return true;
        }

        private bool Behavior08(Scene scene)
        {
            return false; // todo: return whether the player's health is 0
        }

        private bool Behavior09(Scene scene)
        {
            // todo: use player position
            CollisionResult result = default;
            return CollisionDetection.CheckBetweenPoints(Position, scene.CameraPosition, TestFlags.None, scene, ref result);
        }

        private bool Behavior10(Scene scene)
        {
            // todo: use player position
            Vector3 between = scene.CameraPosition - Position;
            return between.LengthSquared < 100;
        }

        private bool Behavior11(Scene scene)
        {
            return false; // todo: return whether main player has an attached enemy
        }

        private bool Behavior12(Scene scene)
        {
            // todo: use player position
            Vector3 between = scene.CameraPosition - Position;
            return between.LengthSquared < 25;
        }

        #region Boilerplate

        public static bool Behavior00(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior00(scene);
        }

        public static bool Behavior01(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior01(scene);
        }

        public static bool Behavior02(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior02(scene);
        }

        public static bool Behavior03(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior03(scene);
        }

        public static bool Behavior04(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior04(scene);
        }

        public static bool Behavior05(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior05(scene);
        }

        public static bool Behavior06(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior06(scene);
        }

        public static bool Behavior07(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior07(scene);
        }

        public static bool Behavior08(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior08(scene);
        }

        public static bool Behavior09(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior09(scene);
        }

        public static bool Behavior10(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior10(scene);
        }

        public static bool Behavior11(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior11(scene);
        }

        public static bool Behavior12(Enemy02Entity enemy, Scene scene)
        {
            return enemy.Behavior12(scene);
        }

        #endregion
    }
}
