using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy12Entity : EnemyInstanceEntity
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
        private Vector3 _field19C; // todo: field names
        private Vector3 _field1A8;
        private int _volumeCheckDelay = 0;
        private bool _seekingVolume = false;
        private CollisionVolume _homeVolume;

        private ushort _extendTimer = 0;

        public Enemy12Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            // technically this enemy has one state function and one behavior, but they're no-ops
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        // todo: identical to Zoomer except for a few values
        protected override void EnemyInitialize()
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
            SetUpModel(Metadata.EnemyModelNames[12], animIndex: 2);
            _extendTimer = 60 * 2; // todo: FPS stuff
            _field19C = _field1A8 = up;
            _angleInc = Fixed.ToFloat(Rng.GetRandomInt2(0x7000)) + 3;
            _angleInc /= 2; // todo: FPS stuff
            _maxAngle = Fixed.ToFloat(Rng.GetRandomInt2(0)) + 60;
            _angleCos = MathF.Cos(MathHelper.DegreesToRadians(_angleInc));
            _homeVolume = CollisionVolume.Move(SpawnFields.Volume1, _spawner.Data.Header.Position.ToFloatVector());
            _direction = Vector3.Cross(facing, up).Normalized();
        }

        private void SetAnimation(GeemerAnim anim, AnimFlags flags = AnimFlags.None)
        {
            _models[0].SetAnimation((int)anim, flags);
        }

        protected override void EnemyProcess()
        {
            ContactDamagePlayer(15, knockback: true);
            var anim = (GeemerAnim)_models[0].AnimInfo.Index[0];
            if (anim == GeemerAnim.WiggleRetracted)
            {
                float radii = PlayerEntity.Main.Volume.SphereRadius + 1.5f;
                if ((Position - PlayerEntity.Main.Volume.SpherePosition).LengthSquared < radii * radii)
                {
                    _soundSource.PlaySfx(SfxId.GEEMER_EXTEND);
                    SetAnimation(GeemerAnim.Extend, AnimFlags.NoLoop);
                    _extendTimer = 0;
                    _speed = Vector3.Zero;
                    return;
                }
            }
            else if (anim == GeemerAnim.WiggleExtended)
            {
                float radii = PlayerEntity.Main.Volume.SphereRadius + 1.5f;
                if ((Position - PlayerEntity.Main.Volume.SpherePosition).LengthSquared >= radii * radii)
                {
                    _soundSource.PlaySfx(SfxId.GEEMER_RETRACT);
                    _speed = Vector3.Zero;
                    SetAnimation(GeemerAnim.Retract, AnimFlags.NoLoop);
                    return;
                }
            }
            else if (anim == GeemerAnim.Extend)
            {
                if (_extendTimer > 0)
                {
                    _extendTimer--;
                }
                else if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    SetAnimation(GeemerAnim.WiggleExtended);
                }
                return;
            }
            else if (anim == GeemerAnim.Retract)
            {
                if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    SetAnimation(GeemerAnim.WiggleRetracted);
                }
                return;
            }
            // todo: everything after this is the same as Zoomer (without the hit_player or sub call at the end)
            _soundSource.PlaySfx(SfxId.ZOOMER_IDLE_LOOP, loop: true);
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
                    if (Vector3.Dot(_field19C, result.Plane.Xyz) < Fixed.ToFloat(4094)
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
                    _field1A8 = vec;
                }
                Vector3 upVec = UpVector + (_field1A8 - UpVector) * (Fixed.ToFloat(819) / 2); // todo: FPS stuff
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
                float dot = Vector3.Dot(_field1A8, UpVector);
                if (dot >= Fixed.ToFloat(3712))
                {
                    _speed += FacingVector * (Fixed.ToFloat(204) / 2); // todo: FPS stuff
                    if (dot >= Fixed.ToFloat(4095))
                    {
                        _field19C = _field1A8;
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
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (source?.Type == EntityType.BeamProjectile)
            {
                var beam = (BeamProjectileEntity)source;
                if (BeamEffectiveness[(int)beam.Beam] == Effectiveness.Zero)
                {
                    if (_models[0].AnimInfo.Index[0] == 2)
                    {
                        _soundSource.PlaySfx(SfxId.GEEMER_EXTEND);
                        SetAnimation(GeemerAnim.Extend, AnimFlags.NoLoop);
                        _extendTimer = 60 * 2; // todo: FPS stuff
                        _speed = Vector3.Zero;
                    }
                    else if (_models[0].AnimInfo.Index[0] == 1)
                    {
                        _extendTimer = 60 * 2; // todo: FPS stuff
                    }
                }
            }
            return false;
        }

        public enum GeemerAnim
        {
            None = -1,
            Retract = 0,
            Extend = 1,
            WiggleRetracted = 2,
            WiggleExtended = 3
        }
    }
}
