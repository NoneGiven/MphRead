using System;
using System.Collections.Generic;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        public CameraInfo CameraInfo { get; } = new CameraInfo();
        public CameraType CameraType { get; private set; } = CameraType.First;
        private Vector3 _field544;
        private float _field554 = 0;
        private float _field558 = 0;
        private float _field68C = 0;
        private float _field690 = 0;

        private void SwitchCamera(CameraType type, Vector3 facing)
        {
            if (type == CameraType.Third1)
            {
                CameraInfo.Target = CameraInfo.Position + facing;
                CameraInfo.Position -= facing / 64;
            }
            else if (type == CameraType.Free)
            {
                CameraInfo.Target = facing;
                if (CameraType == CameraType.First)
                {
                    Vector3 camVec = CameraInfo.Position - CameraInfo.Target;
                    if (camVec != Vector3.Zero)
                    {
                        camVec = camVec.Normalized();
                    }
                    else
                    {
                        camVec = Vector3.UnitX;
                    }
                    CameraInfo.Position += camVec / 64;
                }
            }
            CameraType = type;
            _field544 = CameraInfo.Position;
            _viewSwayTimer = (ushort)(Values.ViewSwayTime * 2 - _viewSwayTimer); // todo: FPS stuff
            CameraInfo.Shake = 0;
        }

        private void UpdateCamera()
        {
            CameraInfo.PrevPosition = CameraInfo.Position;
            if (_viewSwayTimer < Values.ViewSwayTime * 2) // todo: FPS stuff
            {
                _viewSwayTimer++;
                if (!IsAltForm && _viewSwayTimer == Values.ViewSwayTime * 2)
                {
                    SetGunAnimation(GunAnimation.UpDown, AnimFlags.NoLoop);
                }
            }
            // todo: only update camera if not cam seq, or cam seq but not main player
            if (CameraType == CameraType.Third1)
            {
                UpdateCameraThird1();
            }
            else if (CameraType == CameraType.Third2)
            {
                UpdateCameraThird2();
            }
            else if (CameraType == CameraType.Free)
            {
                UpdateCameraFree();
            }
            else if (CameraType == CameraType.Spectator)
            {
                UpdateCameraSpectator();
            }
            else // if (CameraType == CameraType.First)
            {
                UpdateCameraFirst();
            }
            CameraInfo.Update();
        }

        private void UpdateCameraFirst()
        {
            Vector3 position = Position;
            if (!_field6D0)
            {
                position.Y += Fixed.ToFloat(Values.AimYOffset) + MathF.Cos(MathHelper.DegreesToRadians(_gunViewBob)) * _walkViewBob;
            }
            if (_timeStanding < 9 * 2) // todo: FPS stuff
            {
                float angle = MathHelper.DegreesToRadians(360 * _timeStanding / (9 * 2)); // todo: FPS stuff
                position.Y += MathF.Cos(angle) * _field44C - _field44C;
            }
            float maxSway = Values.ViewSwayTime * 2;
            if (_viewSwayTimer < maxSway) // todo: FPS stuff
            {
                float pct = _viewSwayTimer / (float)maxSway;
                CameraInfo.Position = _field544 + (position - _field544) * pct;
                Vector3 target = CameraInfo.Position + _facingVector;
                CameraInfo.Target = Position + (target - Position) * pct;
            }
            else
            {
                CameraInfo.Position = position;
                CameraInfo.Target = CameraInfo.Position + _facingVector;
            }
            CameraInfo.Target.Y += Fixed.ToFloat(Values.Field118) * MathF.Sin(MathHelper.DegreesToRadians(_field688));
            if (MathF.Abs(_field684) >= 1 / 4096f)
            {
                Vector3 toTarget = CameraInfo.Target - CameraInfo.Position;
                Vector3 upVec = new Vector3(-toTarget.Z, 0, toTarget.X).Normalized();
                float factor = Fixed.ToFloat(Values.Field118) * MathF.Sin(MathHelper.DegreesToRadians(_field684));
                CameraInfo.UpVector = new Vector3(upVec.X * factor, 1, upVec.Z * factor);
            }
            else
            {
                CameraInfo.UpVector = Vector3.UnitY;
            }
            if (EquipInfo.Zoomed && Flags1.TestFlag(PlayerFlags1.Walking))
            {
                CameraInfo.Target.Y += MathF.Cos(MathHelper.DegreesToRadians(_gunViewBob)) * 0.025f;
            }
        }

        private void UpdateCameraThird1()
        {
            float v5;
            float v6;
            float v7;
            if (Flags1.TestFlag(PlayerFlags1.NoUnmorph))
            {
                v5 = Fixed.ToFloat(Values.Field78);
                v6 = Fixed.ToFloat(Values.Field7C);
                v7 = Fixed.ToFloat(Values.Field80);
            }
            else
            {
                v5 = 1.5f;
                v6 = 0.7f;
                v7 = 0.5f;
            }
            CameraInfo.Target.X = Volume.SpherePosition.X;
            CameraInfo.Target.Y -= v7;
            CameraInfo.Target.Y += (Volume.SpherePosition.Y - CameraInfo.Target.Y) / 2;
            CameraInfo.Target.Z = Volume.SpherePosition.Z;
            if (MorphCamera != null)
            {
                CameraInfo.Position = MorphCamera.Position;
                return;
            }
            Vector3 posVec;
            if (_jumpPadControlLock > 0)
            {
                Vector3 camVec = (CameraInfo.Position - CameraInfo.Target).WithY(0).Normalized();
                posVec = new Vector3(
                    CameraInfo.Target.X + camVec.X,
                    CameraInfo.Target.Y + v6,
                    CameraInfo.Target.Z + camVec.Z
                );
            }
            else if (_field551 <= 1)
            {
                Vector3 camVec = (CameraInfo.Position - CameraInfo.Target).Normalized();
                posVec = new Vector3(
                    CameraInfo.Target.X + camVec.X * v5,
                    CameraInfo.Position.Y,
                    CameraInfo.Target.Z + camVec.Z * v5
                );
            }
            else
            {
                Vector3 camVec;
                if (_viewSwayTimer >= Values.ViewSwayTime * 2) // todo: FPS stuff
                {
                    camVec = (CameraInfo.Position - CameraInfo.Target).WithY(0);
                }
                else
                {
                    camVec = -CameraInfo.FacingVector.WithY(0);
                    _field544 += (Position - PrevPosition) / 2; // sktodo: FPS stuff?
                }
                camVec = camVec.Normalized();
                posVec = new Vector3(
                    CameraInfo.Target.X + camVec.X * v5,
                    CameraInfo.Target.Y + v6,
                    CameraInfo.Target.Z + camVec.Z * v5
                );
            }
            CameraInfo.Target.Y += v7;
            if (_viewSwayTimer < Values.ViewSwayTime * 2) // todo: FPS stuff
            {
                float pct = _viewSwayTimer / (Values.ViewSwayTime * 2); // todo: FPS stuff
                CameraInfo.Position = _field544 / 2 + (posVec - _field544) * pct;
                Vector3 facingVec = CameraInfo.Position + CameraInfo.FacingVector;
                CameraInfo.Target = facingVec + (CameraInfo.Target - facingVec) * pct;
            }
            else
            {
                float factor = Fixed.ToFloat(Values.Field84);
                CameraInfo.Position += (posVec - CameraInfo.Position) * factor; // sktodo: FPS stuff?
            }
            if (_field553 > 0)
            {
                _field553--;
            }
            if ((CameraInfo.Position - Volume.SpherePosition).LengthSquared >= 6 * 6)
            {
                _field551 = 255;
            }
            else
            {
                float speedMagSqr = Speed.LengthSquared;
                if (speedMagSqr > Fixed.ToFloat(36) && _field551 != 255)
                {
                    _field551++;
                }
                bool blocked1 = false;
                bool blocked2 = false;
                bool blocked4 = false;
                bool blocked8 = false;
                Vector3 point1 = Volume.SpherePosition;
                Vector3 point2 = CameraInfo.Position;
                float margin = Volume.SphereRadius;
                IReadOnlyList<CollisionCandidate> candidates = CollisionDetection.GetCandidatesForLimits(point1, point2,
                    margin, null, Vector3.Zero, includeEntities: true, _scene);
                float v35 = CameraInfo.Field50 * margin;
                float v36 = CameraInfo.Field54 * margin;
                point1 = CameraInfo.Position.AddX(v35).AddZ(v36);
                point2 = Volume.SpherePosition.AddX(v35).AddZ(v36);
                CollisionResult res = default;
                if (CollisionDetection.CheckBetweenPoints(candidates, point1, point1, TestFlags.Players, _scene, ref res))
                {
                    blocked1 = true;
                }
                point1 = CameraInfo.Position.AddX(-v35).AddZ(-v36);
                point2 = Volume.SpherePosition.AddX(-v35).AddZ(-v36);
                if (CollisionDetection.CheckBetweenPoints(candidates, point1, point1, TestFlags.Players, _scene, ref res))
                {
                    blocked2 = true;
                }
                point1 = CameraInfo.Position + CameraInfo.UpVector * margin;
                point2 = Volume.SpherePosition + CameraInfo.UpVector * margin;
                if (CollisionDetection.CheckBetweenPoints(candidates, point1, point1, TestFlags.Players, _scene, ref res))
                {
                    blocked4 = true;
                    _field551 = 0;
                }
                point1 = CameraInfo.Position - CameraInfo.UpVector * (margin / 2);
                point2 = Volume.SpherePosition - CameraInfo.UpVector * (margin / 2);
                if (CollisionDetection.CheckBetweenPoints(candidates, point1, point1, TestFlags.Players, _scene, ref res))
                {
                    blocked8 = true;
                    _field551 = 0;
                }
                float max = Fixed.ToFloat(100);
                if (speedMagSqr > max) // sktodo: FPS stuff?
                {
                    speedMagSqr = max;
                }
                if (!blocked1 || _field558 <= 0 && blocked2)
                {
                    if (!blocked2)
                    {
                        _field558 = 0;
                    }
                    else
                    {
                        if (_field558 > 0)
                        {
                            _field558 = 0;
                        }
                        _field558 -= Fixed.ToFloat(Values.Field88) * speedMagSqr / max;
                        if (_field558 < -Fixed.ToFloat(Values.Field8C))
                        {
                            _field558 = -Fixed.ToFloat(Values.Field8C);
                        }
                        float angle = MathHelper.DegreesToRadians(_field558 * 22.5f); // 360 / 16 = 22.5
                        float cos;
                        float sin;
                        if (angle <= 0)
                        {
                            cos = MathF.Cos(angle);
                            sin = MathF.Sin(angle);
                        }
                        else
                        {
                            cos = MathF.Cos(-angle);
                            sin = -MathF.Sin(-angle);
                        }
                        Vector3 v119 = CameraInfo.Position - CameraInfo.Target;
                        float x = v119.X;
                        float z = v119.Z;
                        v119.X = x * cos + z * sin;
                        v119.Z = x * -sin + z * cos;
                        CameraInfo.Position = v119 + CameraInfo.Target;
                    }
                }
                else
                {
                    // todo?: similar to above except for some signs/comparisons
                    if (_field558 < 0)
                    {
                        _field558 = 0;
                    }
                    _field558 += Fixed.ToFloat(Values.Field88) * speedMagSqr / max;
                    if (_field558 > Fixed.ToFloat(Values.Field8C))
                    {
                        _field558 = Fixed.ToFloat(Values.Field8C);
                    }
                    float angle = MathHelper.DegreesToRadians(_field558 * 22.5f); // 360 / 16 = 22.5
                    float cos;
                    float sin;
                    if (angle <= 0)
                    {
                        cos = MathF.Cos(angle);
                        sin = MathF.Sin(angle);
                    }
                    else
                    {
                        cos = MathF.Cos(-angle);
                        sin = -MathF.Sin(-angle);
                    }
                    Vector3 v219 = CameraInfo.Position - CameraInfo.Target;
                    float x = v219.X;
                    float z = v219.Z;
                    v219.X = x * cos + z * sin;
                    v219.Z = x * -sin + z * cos;
                    CameraInfo.Position = v219 + CameraInfo.Target;
                }
                if (!blocked8 || _field554 <= 0 && blocked4)
                {
                    if (!blocked4)
                    {
                        _field554 = 0;
                    }
                    else
                    {
                        if (_field558 > 0) // bug?: seems like this should have been _field554?
                        {
                            _field558 = 0;
                        }
                        _field554 -= Fixed.ToFloat(Values.Field88) * speedMagSqr / max;
                        if (_field554 < -Fixed.ToFloat(Values.Field8C))
                        {
                            _field554 = -Fixed.ToFloat(Values.Field8C);
                        }
                        float angle = MathHelper.DegreesToRadians(_field554);
                        float cos;
                        float sin;
                        if (angle <= 0)
                        {
                            cos = MathF.Cos(angle);
                            sin = MathF.Sin(angle);
                        }
                        else
                        {
                            cos = MathF.Cos(-angle);
                            sin = -MathF.Sin(-angle);
                        }
                        Vector3 v319 = CameraInfo.Position - CameraInfo.Target;
                        float x = v319.X;
                        float y = v319.Y;
                        float z = v319.Z;
                        v319.X = CameraInfo.UpVector.X * sin + x * cos;
                        v319.Y = CameraInfo.UpVector.Y * sin + y * cos;
                        v319.Z = CameraInfo.UpVector.Z * sin + z * cos;
                        CameraInfo.Position = v319 + CameraInfo.Target;
                    }
                }
                else
                {
                    // todo?: similar to above except for some signs/comparisons
                    if (_field558 < 0) // bug?: seems like this should have been _field554?
                    {
                        _field558 = 0;
                    }
                    _field554 += Fixed.ToFloat(Values.Field88) * speedMagSqr / max;
                    if (_field554 > Fixed.ToFloat(Values.Field8C))
                    {
                        _field554 = Fixed.ToFloat(Values.Field8C);
                    }
                    float angle = MathHelper.DegreesToRadians(_field554);
                    float cos;
                    float sin;
                    if (angle <= 0)
                    {
                        cos = MathF.Cos(angle);
                        sin = MathF.Sin(angle);
                    }
                    else
                    {
                        cos = MathF.Cos(-angle);
                        sin = -MathF.Sin(-angle);
                    }
                    Vector3 v419 = CameraInfo.Position - CameraInfo.Target;
                    float x = v419.X;
                    float y = v419.Y;
                    float z = v419.Z;
                    v419.X = CameraInfo.UpVector.X * sin + x * cos;
                    v419.Y = CameraInfo.UpVector.Y * sin + y * cos;
                    v419.Z = CameraInfo.UpVector.Z * sin + z * cos;
                    CameraInfo.Position = v419 + CameraInfo.Target;
                }
                point1 = CameraInfo.PrevPosition;
                point2 = CameraInfo.Position;
                margin = Fixed.ToFloat(Values.Field90);
                candidates = CollisionDetection.GetCandidatesForLimits(point1, point2,
                    margin, null, Vector3.Zero, includeEntities: true, _scene);
                var results = new CollisionResult[8];
                int count = CollisionDetection.CheckSphereBetweenPoints(candidates, point1, point2, margin,
                    limit: 8, includeOffset: true, TestFlags.Players, _scene, results);
                bool v85 = false;
                for (int i = 0; i < count; i++)
                {
                    CollisionResult result = results[i];
                    if (result.Field0 == 0)
                    {
                        float dot = -Vector3.Dot(CameraInfo.Position, result.Plane.Xyz) - result.Plane.W - margin;
                        if (dot < 0)
                        {
                            CameraInfo.Position += result.Plane.Xyz * dot;
                            v85 = true;
                        }
                    }
                }
                if (v85)
                {
                    Vector3 toTarget = CameraInfo.Target - CameraInfo.Position;
                    for (int i = 0; i < count; i++)
                    {
                        CollisionResult result = results[i];
                        if (result.Field0 == 1 && Vector3.Dot(toTarget, result.Plane.Xyz) >= 0)
                        {
                            float dot = -Vector3.Dot(CameraInfo.Position, result.Plane.Xyz) - result.Plane.W - margin;
                            if (dot < 0)
                            {
                                CameraInfo.Position += result.Plane.Xyz * dot;
                            }
                        }
                    }
                }
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Door)
                    {
                        continue;
                    }
                    var door = (DoorEntity)entity;
                    if (door.Flags.TestFlag(DoorFlags.Open))
                    {
                        continue;
                    }
                    Vector3 toLock = Position - door.LockPosition; // player pos, not cam info pos
                    Vector3 doorFacing = door.FacingVector;
                    Vector4 doorPlane;
                    if (Vector3.Dot(toLock, doorFacing) >= 0)
                    {
                        doorPlane = new Vector4(doorFacing);
                    }
                    else
                    {
                        doorPlane = new Vector4(-doorFacing);
                    }
                    Vector3 wvec = doorPlane.Xyz * (door.LockPosition + 0.4f * doorPlane.Xyz);
                    doorPlane.W = wvec.X + wvec.Y + wvec.Z;
                    CollisionResult planeRes = default;
                    if (CollisionDetection.CheckCylinderIntersectPlane(Position, CameraInfo.Position, doorPlane, ref planeRes))
                    {
                        if ((planeRes.Position - door.LockPosition).LengthSquared < door.RadiusSquared + 1)
                        {
                            float dot = Vector3.Dot(CameraInfo.Position, doorPlane.Xyz) - doorPlane.W;
                            if (dot <= 0)
                            {
                                dot += 0.1f;
                                CameraInfo.Position -= doorPlane.Xyz * dot;
                            }
                        }
                    }
                }
                if (!blocked1 && !blocked2 && !blocked4 && !blocked8)
                {
                    _field552 = 255;
                }
            }
            CollisionResult targResult = default;
            if (CollisionDetection.CheckBetweenPoints(CameraInfo.Target, CameraInfo.Position,
                TestFlags.Players, _scene, ref targResult))
            {
                if (_field552 < 15 * 2) // todo: FPS stuff
                {
                    _field552++;
                }
                else
                {
                    Vector3 between = CameraInfo.Position - CameraInfo.Target;
                    CameraInfo.Position = CameraInfo.Target + between * targResult.Distance;
                }
            }
            else
            {
                _field552 = 0;
            }
        }

        private void UpdateCameraThird2()
        {
            // sknext
        }

        private void UpdateCameraFree()
        {
            // sknext
        }

        private void UpdateCameraSpectator()
        {
            // camtodo
        }

        public void RefreshExternalCamera()
        {
            Flags1 |= PlayerFlags1.AltDirOverride;
            _timeSinceMorphCamera = 0;
        }

        public void ResumeOwnCamera()
        {
            if (CameraType == CameraType.Third1)
            {
                CameraInfo.Target = Position;
                CameraInfo.Position = CameraInfo.Target;
                CameraInfo.Position.X -= _field80 * Fixed.ToFloat(Values.Field78);
                CameraInfo.Position.Z -= _field84 * Fixed.ToFloat(Values.Field78);
                _field544 = CameraInfo.Position;
                CameraInfo.PrevPosition = CameraInfo.Position;
                CameraInfo.Target.Y += Fixed.ToFloat(Values.Field80);
            }
            else
            {
                _field68C = Fixed.ToFloat(Values.Field80);
                _field690 = Fixed.ToFloat(Values.Field78);
                CameraInfo.Target = Position.AddY(_field68C + Fixed.ToFloat(Values.AltColYPos));
                CameraInfo.Position = CameraInfo.Target - _facingVector * _field690;
                _field544 = CameraInfo.Position;
                CameraInfo.PrevPosition = CameraInfo.Position;
            }
        }
    }

    public class CameraInfo
    {
        public Vector3 Position;
        public Vector3 PrevPosition;
        public Vector3 Target;
        public Vector3 UpVector;
        public Vector3 FacingVector;
        public float Fov;
        public float Shake;
        public Matrix4 ViewMatrix;
        public float Field48;
        public float Field4C;
        public float Field50;
        public float Field54;

        private bool _shake = true;

        public void Reset()
        {
            PrevPosition = Vector3.UnitZ;
            Position = PrevPosition;
            Target = Vector3.Zero;
            UpVector = Vector3.UnitY;
            Fov = 39 * 2;
        }

        public void Update()
        {
            Vector3 toTarget = Target - Position;
            var camUp = Vector3.Cross(toTarget, Vector3.Cross(UpVector, toTarget));
            // todo: FPS stuff
            if (Shake > 0 && _shake)
            {
                Target.X += Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(Shake))) - Shake / 2;
                Target.Y += Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(Shake))) - Shake / 2;
                Target.Z += Fixed.ToFloat(Rng.GetRandomInt2(Fixed.ToInt(Shake))) - Shake / 2;
                if (toTarget.X * (Target.X - Position.X) + toTarget.Z * (Target.Z - Position.Z) < 0)
                {
                    Target.X = Position.X + toTarget.X / 2;
                    Target.Z = Position.Z + toTarget.Z / 2;
                }
                Shake *= 0.85f;
                if (Shake < 0.01f)
                {
                    Shake = 0;
                }
            }
            _shake = !_shake;
            FacingVector = Target - Position;
            float facingX = FacingVector.X;
            float facingZ = FacingVector.Z;
            float hMag = MathF.Sqrt(facingX * facingX + facingZ * facingZ);
            FacingVector = FacingVector.Normalized();
            Field48 = facingX / hMag;
            Field4C = facingZ / hMag;
            Field50 = Field4C;
            Field54 = -Field48;
            ViewMatrix = Matrix4.LookAt(Position, Target, camUp);
            // todo?: set transposes and stuff
        }

        public void SetShake(float value)
        {
            if (Shake < value)
            {
                Shake = value;
            }
        }
    }

    public enum CameraType
    {
        First = 0,
        Third1 = 1,
        Third2 = 2,
        Free = 3,
        Spectator = 4
    }
}
