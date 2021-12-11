using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy49Entity : EnemyInstanceEntity
    {
        private Vector3 _vec1;
        private Vector3 _vec2;
        private Vector3 _initialPosition;
        private readonly ForceFieldEntity _forceField;
        private byte _shotFrames = 0;

        // todo?: technically this has a custom draw function, but I don't think we need it (unless it's possible to observe the damage flash)
        public Enemy49Entity(EnemyInstanceEntityData data) : base(data)
        {
            var spawner = data.Spawner as ForceFieldEntity;
            Debug.Assert(spawner != null);
            _forceField = spawner;
        }

        public override bool EnemyInitialize()
        {
            Vector3 position = _forceField.Data.Header.Position.ToFloatVector();
            _vec1 = _forceField.Data.Header.UpVector.ToFloatVector();
            _vec2 = _forceField.Data.Header.FacingVector.ToFloatVector();
            position += _vec2 * Fixed.ToFloat(409);
            SetTransform(_vec2, _vec1, position);
            _initialPosition = Position;
            Flags |= EnemyFlags.NoMaxDistance;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.NoBombDamage;
            _health = _healthMax = 1;
            _boundingRadius = 0.5f;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, _boundingRadius);
            // todo: effectiveness
            SetUpModel("ForceFieldLock");
            Recolor = _forceField.Recolor;
            // todo: equip info fields
            return true;
        }

        public override void EnemyProcess(Scene scene)
        {
            // this is called twice per tick, so the animation plays twice as fast
            if (Active)
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    UpdateAnimFrames(_models[i], scene);
                }
            }
            if (Vector3.Dot(scene.CameraPosition - _initialPosition, _vec2) < 0)
            {
                _vec2 *= -1;
                Vector3 position = _initialPosition + _vec2 * Fixed.ToFloat(409);
                SetTransform(_vec2, _vec1, position);
                _prevPos = Position;
            }
            if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                if (_shotFrames > 0)
                {
                    _shotFrames--;
                    // todo: shoot beams
                }
                else
                {
                    _models[0].SetAnimation(0);
                } 
            }
            float width = _forceField.Width - 0.3f;
            float height = _forceField.Height- 0.3f;
            Vector3 between = Position - _initialPosition;
            float rightPct = Vector3.Dot(between, _forceField.RightVector) / width;
            float upPct = Vector3.Dot(between, _forceField.UpVector) / height;
            // percentage of the lock's distance toward the "bounding oval"
            float pct = rightPct * rightPct + upPct * upPct;
            if (pct >= 1)
            {
                float dot1 = Vector3.Dot(between, _forceField.FacingVector);
                between = (between - _forceField.FacingVector * dot1).Normalized();
                float dot2 = Vector3.Dot(_speed, between) * 2;
                _speed -= between * dot2;
                float inv = 1 / MathF.Sqrt(pct);
                float rf = rightPct * inv * width;
                float uf = upPct * inv * height;
                Position = new Vector3(
                    _initialPosition.X + _forceField.RightVector.X * rf + _forceField.UpVector.X * uf,
                    _initialPosition.Y + _forceField.RightVector.Y * rf + _forceField.UpVector.Y * uf,
                    _initialPosition.Z + _forceField.RightVector.Z * rf + _forceField.UpVector.Z * uf
                );
            }
            float magSqr = _speed.X * _speed.X + _speed.Y * _speed.Y + _speed.Z * _speed.Z;
            if (magSqr <= 0.0004f)
            {
                if (_shotFrames == 0)
                {
                    if (_models[0].AnimInfo.Index[0] == 1)
                    {
                        if (_models[0].AnimInfo.Frame[0] >= 10)
                        {
                            float randRight = Rng.GetRandomInt2(0x666) / 4096f - 0.2f;
                            float randUp = Rng.GetRandomInt2(0x666) / 4096f - 0.2f;
                            _speed = new Vector3(
                                _forceField.UpVector.X * randUp + _forceField.RightVector.X * randRight,
                                _forceField.UpVector.Y * randUp + _forceField.RightVector.Y * randRight,
                                _forceField.UpVector.Z * randUp + _forceField.RightVector.Z * randRight
                            );
                        }
                    }
                    else
                    {
                        _models[0].SetAnimation(1, AnimFlags.NoLoop);
                    } 
                }
            }
            else
            {
                _speed *= Fixed.ToFloat(3973);
            }
        }
    }
}
