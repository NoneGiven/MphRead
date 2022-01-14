using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Threading;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        public CameraInfo CameraInfo { get; } = new CameraInfo();
        public CameraType CameraType { get; private set; } = CameraType.First;
        private Vector3 _field544;

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
                Vector3 target = CameraInfo.Position + FacingVector;
                CameraInfo.Target = Position + (target - Position) * pct;
            }
            else
            {
                CameraInfo.Position = position;
                CameraInfo.Target = CameraInfo.Position + FacingVector;
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
            // sktodo
        }

        private void UpdateCameraThird2()
        {
            // sktodo
        }

        private void UpdateCameraFree()
        {
            // sktodo
        }

        private void UpdateCameraSpectator()
        {
            // camtodo
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
            // todo: set many other fields used by radar etc.
            FacingVector = FacingVector.Normalized();
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
