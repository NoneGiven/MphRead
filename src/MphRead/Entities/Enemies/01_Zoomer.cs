using System;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy01Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private EnemySpawnEntityData SpawnData => _spawner.Data;
        private EnemySpawnFields00 SpawnFields => _spawner.Data.Fields.S00;
        private Vector3 _direction;
        private float _angleInc = 0;
        private float _curAngle = 0;
        private float _maxAngle = 0;
        private float _angleCos = 0;
        private Vector3 _intendedDir;
        // sktodo: implement vector visualization with line loops and use it to classify these fields
        private Vector3 _field1A0;
        private Vector3 _field1AC;
        private int _volumeCheckDelay = 0;
        private bool _seekingVolume = false;
        private CollisionVolume _homeVolume;

        public Enemy01Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            // technically this enemy has one state function and one behavior, but they're no-ops
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected override bool EnemyInitialize()
        {
            var facing = new Vector3(Rng.GetRandomInt2(4096) / 4096f, 0, Rng.GetRandomInt2(4096) / 4096f);
            if (facing.X == 0 && facing.Z == 0)
            {
                facing = _spawner.Transform.Row2.Xyz;
            }
            facing = facing.Normalized(); // the game doesn't do this
            var up = new Vector3(0, _spawner.Transform.Row1.Y, 0);
            SetTransform(facing, up, _spawner.Position);
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.OnRadar;
            _health = _healthMax = 12;
            _boundingRadius = 0.25f;
            _hurtVolumeInit = new CollisionVolume(SpawnFields.Volume0);
            SetUpModel(Metadata.EnemyModelNames[1]);
            _field1A0 = _field1AC = up;
            _angleInc = Fixed.ToFloat(Rng.GetRandomInt2(0x3000)) + 3;
            _angleInc /= 2; // todo: FPS stuff
            _maxAngle = Fixed.ToFloat(Rng.GetRandomInt2(0)) + 40;
            _angleCos = MathF.Cos(MathHelper.DegreesToRadians(_angleInc));
            _homeVolume = CollisionVolume.Move(SpawnFields.Volume1, _spawner.Data.Header.Position.ToFloatVector());
            _direction = Vector3.Cross(facing, up).Normalized();
            return true;
        }

        protected override void EnemyProcess()
        {
            // todo?: owner ent col (although it's unused)
            ModelInstance model = _models[0];
            if (model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended) && model.AnimInfo.Index[0] != 0)
            {
                model.SetAnimation(0);
            }
            // todo: play SFX
            if (!_seekingVolume)
            {
                if (_volumeCheckDelay > 0)
                {
                    _volumeCheckDelay--;
                }
                else if (!_homeVolume.TestPoint(Position))
                {
                    _volumeCheckDelay = 16 * 2; // todo: FPS stuff
                    Vector3 facing = (Position - SpawnData.Header.Position.ToFloatVector()).WithY(0);
                    if (facing.X == 0 && facing.Z == 0)
                    {
                        facing = FacingVector.WithY(0);
                        if (facing.X == 0 && facing.Z == 0)
                        {
                            facing = SpawnData.Header.FacingVector.ToFloatVector().WithY(0);
                        }
                    }
                    _intendedDir = Vector3.Cross(UpVector, facing).Normalized();
                    if (Vector3.Dot(_intendedDir, _direction) < Fixed.ToFloat(-4091))
                    {
                        _direction = RotateVector(_direction, UpVector, 1 / 2f); // todo: FPS stuff
                    }
                    _seekingVolume = true;
                }
            }
            Vector3 testPos = Position + UpVector * _boundingRadius;
            var results = new CollisionResult[8];
            int colCount = CollisionDetection.CheckInRadius(testPos, _boundingRadius, limit: 8,
                getSimpleNormal: true, TestFlags.None, _scene, results);
            if (colCount > 0)
            {
                Vector3 facing = FacingVector;
                Vector3 vec = Vector3.Zero;
                for (int i = 0; i < colCount; i++)
                {
                    CollisionResult result = results[i];
                    float dot = Vector3.Dot(testPos, result.Plane.Xyz) - result.Plane.W;
                    float radMinusDot = _boundingRadius - dot;
                    if (radMinusDot > 0 && radMinusDot < _boundingRadius && result.Field0 == 0 && Vector3.Dot(result.Plane.Xyz, _speed) < 0)
                    {
                        // sktodo: convert this to float math
                        int rmd = (int)(radMinusDot * 4096);
                        float DoThing(float value)
                        {
                            int n = (int)(value * 4096);
                            int v20 = n * rmd;
                            int v21 = (int)((ulong)(n * (long)rmd) >> 32);
                            int v22;
                            if (v21 < 0)
                            {
                                v22 = -((-v20 >> 12) | (-1048576 * (v21 + (v20 != 0 ? 1 : 0))));
                            }
                            else
                            {
                                v22 = (v20 >> 12) | (v21 << 20);
                            }
                            return v22 / 4096f;
                        }
                        var b = new Vector3(DoThing(result.Plane.X), DoThing(result.Plane.Y), DoThing(result.Plane.Z));
                        testPos += b;
                    }
                    float facingDot = Vector3.Dot(result.Plane.Xyz, facing);
                    if (Vector3.Dot(_field1A0, result.Plane.Xyz) < Fixed.ToFloat(4094)
                        && (dot < _boundingRadius - Fixed.ToFloat(408) && facingDot >= Fixed.ToFloat(-143)
                         || dot >= _boundingRadius - Fixed.ToFloat(408) && facingDot <= Fixed.ToFloat(143)))
                    {
                        vec += result.Plane.Xyz;
                    }
                }
                Vector3 position = testPos - UpVector * _boundingRadius;
                if (vec != Vector3.Zero)
                {
                    vec = vec.Normalized();
                    _field1AC = vec;
                }
                Vector3 upVec = UpVector + (_field1AC - UpVector) * (Fixed.ToFloat(819) / 2); // todo: FPS stuff
                upVec = upVec.Normalized();
                Vector3 facingVec = Vector3.Cross(upVec, _direction).Normalized();
                SetTransform(facingVec, upVec, position);
            }
            _speed = UpVector * (Fixed.ToFloat(-245) / 2); // todo: FPS stuff
            if (_seekingVolume)
            {
                _direction += (_intendedDir - _direction) * (Fixed.ToFloat(819) / 2); // todo: FPS stuff
                _direction = _direction.Normalized();
                if (Vector3.Dot(_intendedDir, _direction) > _angleCos)
                {
                    _direction = _intendedDir;
                    _seekingVolume = false;
                    _curAngle = 0;
                }
            }
            else if (colCount > 0)
            {
                float dot = Vector3.Dot(_field1AC, UpVector);
                if (dot >= Fixed.ToFloat(3712))
                {
                    _speed += FacingVector * (Fixed.ToFloat(204) / 2); // todo: FPS stuff
                    if (dot >= Fixed.ToFloat(4095))
                    {
                        _field1A0 = _field1AC;
                        _curAngle += _angleInc;
                        if (_curAngle > _maxAngle || _curAngle < -_maxAngle)
                        {
                            _angleInc *= -1;
                        }
                        Vector3 direction = RotateVector(FacingVector, UpVector, _angleInc);
                        _direction = Vector3.Cross(direction, UpVector).Normalized();
                    }
                }
            }
            ContactDamagePlayer(15, knockback: true);
        }
    }
}
