using System;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy49Entity : EnemyInstanceEntity
    {
        private Vector3 _vec1;
        private Vector3 _vec2;
        private Vector3 _fieldPosition;
        private Vector3 _targetPosition;
        private readonly ForceFieldEntity _forceField;
        private byte _shotFrames = 0;
        private EquipInfo? _equipInfo;
        private int _ammo = -1;
        private Vector3 _ownSpeed; // todo: revisit this?

        // todo?: technically this has a custom draw function, but I don't think we need it (unless it's possible to observe the damage flash)
        public Enemy49Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as ForceFieldEntity;
            Debug.Assert(spawner != null);
            _forceField = spawner;
        }

        protected override bool EnemyInitialize()
        {
            Vector3 position = _forceField.Data.Header.Position.ToFloatVector();
            _fieldPosition = position;
            _vec1 = _forceField.Data.Header.UpVector.ToFloatVector();
            _vec2 = _forceField.Data.Header.FacingVector.ToFloatVector();
            position += _vec2 * Fixed.ToFloat(409);
            SetTransform(_vec2, _vec1, position);
            Flags |= EnemyFlags.NoMaxDistance;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.NoBombDamage;
            _health = _healthMax = 1;
            _boundingRadius = 0.5f;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, _boundingRadius);
            ClearEffectiveness();
            switch (_forceField.Data.Type)
            {
            case 0:
                SetEffectiveness(BeamType.PowerBeam, Effectiveness.Normal);
                break;
            case 1:
                SetEffectiveness(BeamType.VoltDriver, Effectiveness.Normal);
                break;
            case 2:
                SetEffectiveness(BeamType.Missile, Effectiveness.Normal);
                break;
            case 3:
                SetEffectiveness(BeamType.Battlehammer, Effectiveness.Normal);
                break;
            case 4:
                SetEffectiveness(BeamType.Imperialist, Effectiveness.Normal);
                break;
            case 5:
                SetEffectiveness(BeamType.Judicator, Effectiveness.Normal);
                break;
            case 6:
                SetEffectiveness(BeamType.Magmaul, Effectiveness.Normal);
                break;
            case 7:
                SetEffectiveness(BeamType.ShockCoil, Effectiveness.Normal);
                break;
            case 8:
                Flags &= ~EnemyFlags.NoBombDamage;
                break;
            };
            SetUpModel("ForceFieldLock");
            Recolor = _forceField.Recolor;
            _equipInfo = new EquipInfo(Weapons.Weapons1P[(int)_forceField.Data.Type], _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
            return true;
        }

        private void ClearEffectiveness()
        {
            for (int i = 0; i < BeamEffectiveness.Length; i++)
            {
                BeamEffectiveness[i] = Effectiveness.Zero;
            }
        }

        private void SetEffectiveness(BeamType type, Effectiveness effectiveness)
        {
            int index = (int)type;
            Debug.Assert(index < BeamEffectiveness.Length);
            BeamEffectiveness[index] = effectiveness;
        }

        protected override void EnemyProcess()
        {
            // this is called twice per tick, so the animation plays twice as fast
            if (Active)
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    UpdateAnimFrames(_models[i]);
                }
            }
            if (Vector3.Dot(PlayerEntity.Main.CameraInfo.Position - _fieldPosition, _vec2) < 0)
            {
                _vec2 *= -1;
                Vector3 position = _fieldPosition + _vec2 * Fixed.ToFloat(409);
                SetTransform(_vec2, _vec1, position);
                _prevPos = Position;
            }
            if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                if (_shotFrames > 0)
                {
                    _shotFrames--;
                    Debug.Assert(_equipInfo != null);
                    Vector3 spawnDir = (_targetPosition - Position).Normalized();
                    Vector3 spawnPos = Position + spawnDir * 0.1f;
                    BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, spawnDir, BeamSpawnFlags.None, _scene);
                }
                if (_shotFrames == 0)
                {
                    _models[0].SetAnimation(0);
                }
            }
            float width = _forceField.Width - 0.3f;
            float height = _forceField.Height - 0.3f;
            Vector3 between = Position - _fieldPosition;
            float rightPct = Vector3.Dot(between, _forceField.FieldRightVector) / width;
            float upPct = Vector3.Dot(between, _forceField.FieldUpVector) / height;
            // percentage of the lock's distance toward the "bounding oval"
            float pct = rightPct * rightPct + upPct * upPct;
            if (pct >= 1)
            {
                float dot1 = Vector3.Dot(between, _forceField.FieldFacingVector);
                between = (between - _forceField.FieldFacingVector * dot1).Normalized();
                float dot2 = Vector3.Dot(_ownSpeed, between) * 2;
                _ownSpeed -= between * dot2;
                float inv = 1 / MathF.Sqrt(pct);
                float rf = rightPct * inv * width;
                float uf = upPct * inv * height;
                Position = new Vector3(
                    _fieldPosition.X + _forceField.FieldRightVector.X * rf + _forceField.FieldUpVector.X * uf,
                    _fieldPosition.Y + _forceField.FieldRightVector.Y * rf + _forceField.FieldUpVector.Y * uf,
                    _fieldPosition.Z + _forceField.FieldRightVector.Z * rf + _forceField.FieldUpVector.Z * uf
                );
            }
            float magSqr = _ownSpeed.X * _ownSpeed.X + _ownSpeed.Y * _ownSpeed.Y + _ownSpeed.Z * _ownSpeed.Z;
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
                            _ownSpeed = new Vector3(
                                _forceField.FieldUpVector.X * randUp + _forceField.FieldRightVector.X * randRight,
                                _forceField.FieldUpVector.Y * randUp + _forceField.FieldRightVector.Y * randRight,
                                _forceField.FieldUpVector.Z * randUp + _forceField.FieldRightVector.Z * randRight
                            );
                        }
                    }
                    else
                    {
                        _models[0].SetAnimation(1, AnimFlags.NoLoop);
                    }
                }
            }
            else if (_scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                _ownSpeed *= Fixed.ToFloat(3973);
            }
            _speed = _ownSpeed / 2; // todo: FPS stuff
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health > 0)
            {
                if (source?.Type == EntityType.BeamProjectile)
                {
                    LockHit(source);
                }
            }
            else
            {
                _scene.SendMessage(Message.Unlock, this, _owner, 0, 0);
            }
            return false;
        }

        public void LockHit(EntityBase source)
        {
            var beam = (BeamProjectileEntity)source;
            if (_shotFrames == 0 && GetEffectiveness(beam.Beam) == Effectiveness.Zero && beam.Owner == PlayerEntity.Main)
            {
                _shotFrames = _forceField.Data.Type == 7 ? (byte)(30 * 2) : (byte)1; // todo: FPS stuff
                beam.Owner.GetPosition(out _targetPosition);
                _models[0].SetAnimation(2, AnimFlags.NoLoop);
            }
        }
    }
}
