using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy37Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        private CollisionVolume _volume1;
        private Vector3 _rightVector;
        private Vector3 _field1B8;
        private Vector3 _field1D0;
        private Vector3 _field1DC;
        private Vector3 _field1E8;
        private Vector3 _field224;

        private ushort _prevHealth = 0;
        private ushort _damageTaken = 0;
        private PlayerEntity? _target = null;
        private QuadtroidFlags _flags = QuadtroidFlags.None;
        private bool _hitByBomb = false;
        private bool _hitByBeam = false;
        private int _field238 = 0;
        private int _field23C = 0;

        private Action? _field234 = null;

        public Enemy37Entity(EnemyInstanceEntityData data, Scene scene) : base(data, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        protected override bool EnemyInitialize()
        {
            Flags |= EnemyFlags.Visible; // bug?: doesn't set OnRadar
            _state1 = _state2 = 1;
            Vector3 up = _spawner.Data.Header.UpVector.ToFloatVector();
            Vector3 facing = _spawner.Data.Header.FacingVector.ToFloatVector();
            Vector3 position = _spawner.Data.Header.Position.ToFloatVector();
            facing = RotateVectorRandom(facing, up);
            SetTransform(facing, up.Normalized(), position);
            _prevPos = position;
            _boundingRadius = 0.25f;
            _drawScale = 1.5f;
            _health = _healthMax = 120;
            _prevHealth = _health;
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S00.Volume0);
            _volume1 = CollisionVolume.Move(_spawner.Data.Fields.S00.Volume1, Position);
            _rightVector = Vector3.Cross(facing, up).Normalized();
            _field238 = ((int)Rng.GetRandomInt2(60) + 45) * 2; // todo: FPS stuff
            _field23C = ((int)Rng.GetRandomInt2(60) + 90) * 2; // todo: FPS stuff
            ModelInstance inst = SetUpModel(Metadata.EnemyModelNames[37]);
            inst.SetAnimation(4, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            inst.SetAnimation(6, slot: 1, SetFlags.Texcoord);
            return true;
        }

        protected override void EnemyProcess()
        {
            if (_state1 == 0)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                Func214E750(); // the game checks this return value, but it's always 0
                Func214E668(Fixed.ToFloat(218));
                Func214DC90();
                Func214EF68(Func214EDD0);
            }
            else if (_state1 == 1)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                if (!Func214E5EC() && !Func214E58C())
                {
                    Func214E668(Fixed.ToFloat(218));
                    Func214DC90();
                    if (Func214D6E0(PlayerEntity.Main) && PlayerEntity.Main.AttachedEnemy == null
                        && Func214D690(PlayerEntity.Main) && Func214D828(PlayerEntity.Main))
                    {
                        _target = PlayerEntity.Main;
                        Func214E1C0(_target);
                        Func214EC08();
                    }
                    Func214EF68(Func214EDD0);
                }
            }
            else if (_state1 == 2)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                _speed = UpVector * Fixed.ToFloat(-307);
                _speed /= 2; // todo: FPS stuff
                Func214D954();
                if (Func214E110())
                {
                    Func214ED20();
                }
                Func214EF68(Func214ED78);
            }
            else if (_state1 == 3)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                if (!Func214E5EC())
                {
                    Func214E668(Fixed.ToFloat(218));
                    if (CheckInVolume())
                    {
                        Func214EDD0();
                    }
                    Func214EF68(Func214ED20);
                }
            }
            else if (_state1 == 4)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                _speed = UpVector * Fixed.ToFloat(-307);
                _speed /= 2; // todo: FPS stuff
                Func214E708(Func214EC60);
                Func214EF68(Func214EDD0);
            }
            else if (_state1 == 5)
            {
                Func214DCB8();
                UpdateCollision();
                _speed = UpVector * Fixed.ToFloat(-307);
                _speed /= 2; // todo: FPS stuff
                Func214E708(Func214EDD0);
                Func214EF68(Func214EDD0);
            }
            else if (_state1 == 6)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                _speed = UpVector * Fixed.ToFloat(-307);
                _speed /= 2; // todo: FPS stuff
                Func214D954();
                if (Func214E110())
                {
                    Func214EDD0();
                }
                Func214EF68(Func214EC60);
            }
            else if (_state1 == 7)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                _speed = UpVector * Fixed.ToFloat(-307);
                _speed /= 2; // todo: FPS stuff
                Func214D954();
                if (Func214E110())
                {
                    Func214EB84();
                }
                Func214EF68(Func214EC60);
            }
            else if (_state1 == 8)
            {
                // todo: play SFX
                Func214DCB8();
                UpdateCollision();
                Func214E668(Fixed.ToFloat(654));
                Func214DC90();
                Func214E4D8(_target);
                if (!Func214D5B8(Func214D828) && Func214D65C(_target) && _flags.TestFlag(QuadtroidFlags.Bit6))
                {
                    Func214EB08();
                }
                Func214EEC4();
            }
            else if (_state1 == 9)
            {
                _hitByBeam = false;
                _hitByBomb = false;
                Func214DCB8();
                UpdateCollision();
                Func214E4D8(_target);
                _speed = UpVector * Fixed.ToFloat(-307);
                _speed /= 2; // todo: FPS stuff
                Func214E708(Func214E9F4);
            }
            else if (_state1 == 10)
            {
                _hitByBeam = false;
                _hitByBomb = false;
                Func214DC90();
                if (Func214DF58())
                {
                    Func214E4D8(_target);
                }
                if (Func214DAF8())
                {
                    Func214E994();
                }
                if (Func214D5B8(Func214D828))
                {
                    Func214DAB0(Vector3.UnitY);
                }
                else if (!Func214D65C(_target))
                {
                    Func214EB84();
                }
                Func214E708(Func214EDD0);
            }
            else if (_state1 == 11)
            {
                Debug.Assert(_target != null);
                _hitByBeam = false;
                _hitByBomb = false;
                Func214D9F8();
                if (_target.Flags1.TestFlag(PlayerFlags1.Morphing))
                {
                    Func214E8D4();
                }
            }
            else if (_state1 == 12)
            {
                Debug.Assert(_target != null);
                _hitByBeam = false;
                _hitByBomb = false;
                Position = _target.FacingVector * Fixed.ToFloat(819) + _target.Position;
                Func214E708(Func214E82C);
            }
            else if (_state1 == 13)
            {
                Debug.Assert(_target != null);
                Func214D9F8();
                Func214D864(_target);
                if (_target.IsUnmorphing)
                {
                    Func214E4D8(_target);
                    Func214E7B8();
                }
                if (Func214EE28())
                {
                    Vector3 facing = FacingVector;
                    _speed = new Vector3(0, Fixed.ToFloat(218), 0);
                    _speed += facing * Fixed.ToFloat(-364);
                    _speed /= 2; // todo: FPS stuff
                    _field224 = new Vector3(0, Fixed.ToFloat(-17), 0);
                    _field224 += facing * Fixed.ToFloat(5);
                }
            }
            else if (_state1 == 14)
            {
                _hitByBeam = false;
                _hitByBomb = false;
                Func214DE50();
                Func214E708(Func214E92C);
            }
            else if (_state1 == 15)
            {
                UpdateCollision();
                _hitByBeam = false;
                _hitByBomb = false;
                Func214E708(Func214E788);
            }
            else if (_state1 == 16)
            {
                UpdateCollision();
                _hitByBeam = false;
                _hitByBomb = false;
                Func214E708(Func214EDD0);
            }
            else if (_state1 == 17)
            {
                UpdateCollision();
                Func214E708(Func214EDD0);
                Func214EF68(Func214EDD0);
            }
            else if (_state1 == 18)
            {
                UpdateCollision();
                _speed += _field224 / 4; // todo: FPS stuff
                Func214E708(Func214EDD0);
                _hitByBeam = false;
                _hitByBomb = false;
            }
        }

        private void Func214EF68(Action func)
        {
            if (_hitByBeam || _hitByBomb)
            {
                _hitByBeam = false;
                _hitByBomb = false;
                if (_damageTaken >= 25)
                {
                    _models[0].SetAnimation(7, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                    _state1 = _state2 = 17;
                    // todo: stop SFX
                    _speed = Vector3.Zero;
                    Func214D9B0();
                    _field234 = func;
                }
            }
        }

        private void Func214E708(Action func)
        {
            if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                if (_field234 != null)
                {
                    _field234.Invoke();
                    _field234 = null;
                }
                else
                {
                    func.Invoke();
                }
            }
        }

        private bool Func214D5B8(Func<PlayerEntity, bool> func)
        {
            if (_target == null)
            {
                return true;
            }
            bool targetDead = _target.Health == 0;
            bool targetHasAttached = _target.AttachedEnemy != null;
            bool targetAttachedIsNotSelf = _target.AttachedEnemy != this;
            if (!func(_target) || targetDead || targetHasAttached && targetAttachedIsNotSelf)
            {
                Func214D9B0();
                Func214EDD0();
                return true;
            }
            return false;
        }

        private void Func214DCB8()
        {
            PlayerEntity player = PlayerEntity.Main;
            if (HitPlayers[player.SlotIndex])
            {
                Vector3 between = player.Position - Position;
                player.Speed += between / 4;
                player.TakeDamage(3, DamageFlags.None, null, this);
            }
        }

        private void UpdateCollision()
        {
            _flags &= ~QuadtroidFlags.Bit6;
            if (HandleCollision(ref _field1D0, _field1DC, _rightVector, 0.2f))
            {
                _flags |= QuadtroidFlags.Bit6;
            }
        }

        private bool HandleCollision(ref Vector3 dest, Vector3 someVec, Vector3 right, float dist)
        {
            Vector3 up = UpVector;
            dest = up / 2;
            Vector3 testVec = up * dist;
            Vector3 testPos = Position + testVec;
            var results = new CollisionResult[8];
            int count = CollisionDetection.CheckInRadius(testPos, _boundingRadius, limit: 8, getSimpleNormal: false,
                TestFlags.None, _scene, results);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    CollisionResult result = results[i];
                    float v37;
                    if (result.Field0 != 0)
                    {
                        v37 = _boundingRadius - result.Field14;
                    }
                    else
                    {
                        v37 = _boundingRadius + result.Plane.W - Vector3.Dot(testPos, result.Plane.Xyz);
                    }
                    Vector3 posDelta = result.Plane.Xyz * v37;
                    if (Vector3.Dot(posDelta, _speed) <= 0)
                    {
                        if (Vector3.Dot(someVec, result.Plane.Xyz) < Fixed.ToFloat(4094))
                        {
                            dest += result.Plane.Xyz;
                        }
                        testPos += posDelta;
                    }
                }
                Vector3 newPosition = testPos - testVec;
                if (dest != Vector3.Zero)
                {
                    dest = dest.Normalized();
                }
                else
                {
                    dest = someVec;
                }
                Vector3 newUp = dest - up;
                newUp = newUp * Fixed.ToFloat(409) + up;
                newUp = newUp.Normalized();
                Vector3 newFacing = Vector3.Cross(newUp, right).Normalized();
                SetTransform(newFacing, newUp, newPosition);
                return true;
            }
            return false;
        }

        private void Func214E750()
        {
            if (--_field238 <= 0)
            {
                Func214EDD0();
            }
        }

        private void Func214E668(float a2)
        {
            Vector3 up = UpVector;
            _speed = up * Fixed.ToFloat(-307);
            float dot = Vector3.Dot(_field1D0, up);
            if (dot >= Fixed.ToFloat(1731))
            {
                _speed += FacingVector * a2;
                if (dot >= Fixed.ToFloat(4094))
                {
                    _field1DC = _field1D0;
                }
            }
            _speed /= 2; // todo: FPS stuff
        }

        private void Func214DC90()
        {
            if (!CheckInVolume())
            {
                Func214DBDC();
            }
        }

        private bool Func214E5EC()
        {
            _field238--;
            if (_field238 == 0)
            {
                _field238 = ((int)Rng.GetRandomInt2(60) + 45) * 2; // todo: FPS stuff
                _field1E8 = RotateVectorRandom(FacingVector, UpVector);
                Func214E444(_field1E8, state: 4, animId: 10, AnimFlags.NoLoop);
                return true;
            }
            return false;
        }

        private bool Func214E58C()
        {
            if (_field23C > 0)
            {
                _field23C--;
            }
            if (_field23C == 0)
            {
                _field23C = ((int)Rng.GetRandomInt2(60) + 90) * 2; // todo: FPS stuff
                Func214ECB8();
                return true;
            }
            return false;
        }

        private bool Func214D6E0(PlayerEntity player)
        {
            Vector3 facing = FacingVector;
            Vector3 between = Position - player.Position;
            if (between.LengthSquared > Fixed.ToFloat(32))
            {
                Vector3 vec = Func204D518(between, UpVector);
                if (vec.LengthSquared > Fixed.ToFloat(32) && Vector3.Dot(facing, vec.Normalized()) > -1)
                {
                    vec = Func204D518(between, _rightVector);
                    if (vec.LengthSquared > Fixed.ToFloat(32) && Vector3.Dot(facing, vec.Normalized()) > -1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Func214D690(PlayerEntity player)
        {
            CollisionResult discard = default;
            return player.Health > 0 && CollisionDetection.CheckSphereOverlapVolume(player.Volume, Position, 10, ref discard);
        }

        private bool Func214D828(PlayerEntity player)
        {
            return player.Health > 0 && _volume1.TestPoint(player.Position);
        }

        private void Func214EC08()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(12, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 7;
        }

        private void Func214D954()
        {
            if (MathF.Abs(Vector3.Dot(_field1E8, UpVector)) >= Fixed.ToFloat(71))
            {
                _field1E8 = Func204D518(_field1E8, UpVector).Normalized();
            }
        }

        private bool Func214E110()
        {
            bool result = false;
            Vector3 up = UpVector;
            Vector3 facing = FacingVector;
            if (Vector3.Dot(_field1E8, facing) >= Fixed.ToFloat(4034))
            {
                facing = _field1E8;
                result = true;
            }
            else
            {
                float angle = _flags.TestFlag(QuadtroidFlags.Bit3) ? -10 : 10;
                facing = RotateVector(facing, up, angle).Normalized();
            }
            SetTransform(facing, up, Position);
            _rightVector = Vector3.Cross(facing, up).Normalized();
            return result;
        }

        private void Func214ED20()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(4, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 3;
        }

        private void Func214EDD0()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(4, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 1;
        }

        private void Func214EB84()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _flags &= ~QuadtroidFlags.Bit1;
            _flags &= ~QuadtroidFlags.Bit2;
            _models[0].SetAnimation(9, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 8;
            _field238 = ((int)Rng.GetRandomInt2(15) + 30) * 2; // todo: FPS stuff
        }

        private void Func214E4D8(PlayerEntity? player)
        {
            if (player != null)
            {
                Vector3 up = UpVector;
                Vector3 between = player.Position - Position;
                between = Func204D518(between, up);
                if (between.LengthSquared > Fixed.ToFloat(32))
                {
                    Vector3 facing = between.Normalized();
                    _rightVector = Vector3.Cross(facing, up).Normalized();
                    SetTransform(facing, up, Position);
                }
            }
        }

        private bool Func214D65C(PlayerEntity? player)
        {
            Debug.Assert(player != null);
            CollisionResult discard = default;
            return CollisionDetection.CheckSphereOverlapVolume(player.Volume, Position, 4, ref discard);
        }

        private void Func214EB08()
        {
            _flags |= QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(11, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _state1 = _state2 = 9;
            // todo: stop SFX
            _speed = Vector3.Zero;
        }

        private void Func214EEC4()
        {
            if (_hitByBeam && _damageTaken >= 25)
            {
                _hitByBeam = false;
                _models[0].SetAnimation(7, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                _state1 = _state2 = 17;
                // todo: stop SFX
                _speed = Vector3.Zero;
                Func214D9B0();
            }
        }

        private bool Func214DF58()
        {
            if (_flags.TestFlag(QuadtroidFlags.Bit1) || _flags.TestFlag(QuadtroidFlags.Bit2))
            {
                if (_flags.TestFlag(QuadtroidFlags.Bit1))
                {
                    float angle = _flags.TestFlag(QuadtroidFlags.Bit3) ? -10 : 10;
                    Vector3 facing = FacingVector;
                    if (Vector3.Dot(_field1E8, facing) >= Fixed.ToFloat(4034))
                    {
                        facing = _field1E8;
                        _flags &= ~QuadtroidFlags.Bit1;
                    }
                    else
                    {
                        facing = RotateVector(facing, _field1B8, angle).Normalized();
                    }
                    Vector3 up = RotateVector(UpVector, _field1B8, angle).Normalized();
                    SetTransform(facing, up, Position);
                }
                if (_flags.TestFlag(QuadtroidFlags.Bit2))
                {
                    Vector3 facing = FacingVector;
                    Vector3 up = UpVector;
                    if (Vector3.Dot(up, Vector3.UnitY) >= Fixed.ToFloat(4034))
                    {
                        up = Vector3.UnitY;
                        _flags &= ~QuadtroidFlags.Bit2;
                    }
                    else
                    {
                        float angle = Func214D500(up, Vector3.UnitY, facing) ? -10 : 10;
                        up = RotateVector(up, facing, angle).Normalized();
                    }
                    SetTransform(facing, up, Position);
                }
                _rightVector = Vector3.Cross(FacingVector, UpVector).Normalized();
                return false;
            }
            return true;
        }

        private bool Func214DAF8()
        {
            Debug.Assert(_target != null);
            Vector3 targetPos;
            if (_target.IsAltForm)
            {
                targetPos = _target.Position.AddY(0.625f);
            }
            else
            {
                // todo: use cam info pos with Y - 0.5f
                targetPos = _target.Position;
            }
            Vector3 between = targetPos - Position;
            if (between.LengthSquared > 0.375f)
            {
                between = between.Normalized();
                Position += between * Fixed.ToFloat(872);
                return false;
            }
            return true;
        }

        private void Func214E994()
        {
            Debug.Assert(_target != null);
            _field238 = 8 * 2; // todo: FPS stuff
            _flags &= ~QuadtroidFlags.Bit1;
            _flags &= ~QuadtroidFlags.Bit2;
            if (_target.IsAltForm)
            {
                Func214E82C();
            }
            else
            {
                Func214E92C();
            }
        }

        private void Func214DAB0(Vector3 vec)
        {
            Vector3 up = vec.Normalized();
            Vector3 facing = Vector3.Cross(up, _rightVector).Normalized();
            SetTransform(facing, up, Position);
        }

        private void Func214D9F8()
        {
            Debug.Assert(_target != null);
            // todo: play SFX
            if (--_field238 < 0)
            {
                _target.TakeDamage(2, DamageFlags.NoDmgInvuln, null, this);
                _field238 = 8 * 2; // todo: FPS stuff
            }
            // the game sets the player's model transform (if in alt form) to the enemy's here
        }

        private void Func214E8D4()
        {
            _flags |= QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(5, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _state1 = _state2 = 12;
        }

        private void Func214D864(PlayerEntity player)
        {
            if (player.IsAltForm)
            {
                Vector3 position = player.Position;
                if (player.Speed != Vector3.Zero)
                {
                    position.Y += 0.4f;
                    if (_models[0].AnimInfo.Index[0] != 12)
                    {
                        _models[0].SetAnimation(12, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                    }
                }
                else
                {
                    position.Y -= 0.25f;
                    if (_models[0].AnimInfo.Index[0] != 1)
                    {
                        _models[0].SetAnimation(1, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
                    }
                }
                Position = position;
            }
        }

        private void Func214E7B8()
        {
            _flags |= QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(5, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node,
                AnimFlags.NoLoop | AnimFlags.Reverse);
            _state1 = _state2 = 14;
            _field224 = Position;
            Func214E4D8(_target);
        }

        private bool Func214EE28()
        {
            if (_hitByBomb)
            {
                _hitByBomb = false;
                _models[0].SetAnimation(8, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
                _state1 = _state2 = 18;
                // todo: stop SFX
                _speed = Vector3.Zero;
                _flags |= QuadtroidFlags.Bit7;
                Func214D9B0();
                return true;
            }
            return false;
        }

        private void Func214DE50()
        {
            Debug.Assert(_target != null);
            Vector3 pos = _target.Position; // todo: use cam info pos
            Vector3 targetFacing = _target.FacingVector;
            Vector3 targetUp = _target.FacingVector;
            Vector3 targetRight = Vector3.Cross(targetUp, targetFacing).Normalized();
            pos += targetFacing * 0.74f;
            pos += targetUp * -1.1f;
            pos += targetRight * Fixed.ToFloat(97);
            Vector3 vec = pos - _field224;
            AnimationInfo animInfo = _target.BipedModel2.AnimInfo;
            float div = animInfo.Frame[0] / (float)animInfo.FrameCount[0];
            Position = vec * div + _field224;
        }

        private void Func214ED78()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(12, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 2;
        }

        private void Func214EC60()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(12, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 6;
        }

        private void Func214E9F4()
        {
            _field224 = Position;
            if (_target != null)
            {
                Vector3 between = _target.Position - Position;
                if (between.X * between.X + between.Z * between.Z > Fixed.ToFloat(32))
                {
                    Func214E314(between.Normalized());
                }
            }
            _models[0].SetAnimation(3, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _flags |= QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit6;
            _flags &= ~QuadtroidFlags.Bit0;
            _state1 = _state2 = 10;
            _speed = Vector3.Zero;
            // todo: play SFX
        }

        private void Func214E314(Vector3 vec)
        {
            Vector3 facing = FacingVector;
            _field1E8 = vec;
            _field1B8 = Vector3.Cross(_field1E8, facing);
            if (_field1B8.LengthSquared > Fixed.ToFloat(32))
            {
                _field1B8 = _field1B8.Normalized();
                if (facing.Y > 0)
                {
                    _field1B8 *= -1;
                }
                _flags |= QuadtroidFlags.Bit1;
                _flags |= QuadtroidFlags.Bit2;
                _flags &= ~QuadtroidFlags.Bit3;
                _flags &= ~QuadtroidFlags.Bit4;
                if (Func214D500(facing, _field1E8, _field1B8))
                {
                    _flags |= QuadtroidFlags.Bit3;
                }
                if (Func214D500(UpVector, Vector3.UnitY, facing))
                {
                    _flags |= QuadtroidFlags.Bit4;
                }
            }
        }

        private void Func214E82C()
        {
            Debug.Assert(_target != null);
            // the game sets the enemy's model transform to the player's here
            _target.AttachedEnemy = this;
            _models[0].SetAnimation(1, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 13;
            Position = _target.Position.AddY(-0.25f);
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
        }

        private void Func214E92C()
        {
            Debug.Assert(_target != null);
            _target.AttachedEnemy = this;
            _flags |= QuadtroidFlags.Bit7;
            _flags |= QuadtroidFlags.Bit0;
            _models[0].SetAnimation(0, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node);
            _state1 = _state2 = 11;
        }

        private void Func214E788()
        {
            Func214DBDC();
            if (_state1 == 15)
            {
                _state1 = _state2 = 1;
            }
            // this seems like a bug, but this is only called in state 15, which is not used
            Position = Position.WithY(0.625f);
        }

        private void Func214DBDC()
        {
            Vector3 toSpawn = (_spawner.Data.Header.Position.ToFloatVector() - Position).Normalized();
            Vector3 vec = Func204D518(toSpawn, UpVector);
            if (vec.LengthSquared > Fixed.ToFloat(32))
            {
                Func214E444(vec.Normalized(), state: 2, animId: 12, AnimFlags.None);
            }
        }

        private void Func214ECB8()
        {
            _flags &= ~QuadtroidFlags.Bit7;
            _flags &= ~QuadtroidFlags.Bit0;
            _models[0].SetAnimation(11, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, AnimFlags.NoLoop);
            _state1 = _state2 = 5;
            _speed = Vector3.Zero;
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (source != null && !_flags.TestFlag(QuadtroidFlags.Bit7))
            {
                if (source.Type == EntityType.BeamProjectile)
                {
                    _hitByBeam = true;
                    if (CheckInVolume())
                    {
                        Func214E1C0(source);
                    }
                }
                else if (source.Type == EntityType.Bomb)
                {
                    _hitByBomb = true;
                    Func214E1C0(PlayerEntity.Main);
                }
            }
            if (_health == 0)
            {
                Func214D9B0();
            }
            if (_prevHealth > _health)
            {
                _damageTaken = (ushort)(_prevHealth - _health);
            }
            _prevHealth = _health;
            return false;
        }

        public void UpdateAttached(PlayerEntity player)
        {
            if (player.IsMorphing)
            {
                return;
            }
            Vector3 position = player.Position; // todo: use cam info pos
            Vector3 facing = player.FacingVector;
            position += facing * 0.74f;
            facing *= -1;
            Vector3 up = player.UpVector;
            position += up * -1.1f;
            Vector3 right = Vector3.Cross(facing, up).Normalized();
            position += right * Fixed.ToFloat(97);
            SetTransform(facing, up, position);
        }

        private Vector3 RotateVectorRandom(Vector3 vec, Vector3 axis)
        {
            float angle = Fixed.ToFloat(Rng.GetRandomInt2(359));
            vec = RotateVector(vec, axis, angle);
            return Func204D518(vec, axis).Normalized();
        }

        // todo: function names
        private Vector3 Func204D518(Vector3 vec, Vector3 axis)
        {
            return vec - Func204D57C(vec, axis);
        }

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

        private void Func214E1C0(EntityBase entity)
        {
            Vector3 vec = Vector3.Zero;
            if (entity.Type == EntityType.Player)
            {
                vec = entity.Position - Position;
            }
            else if (entity.Type == EntityType.BeamProjectile)
            {
                var beam = (BeamProjectileEntity)entity;
                vec = -beam.Direction;
            }
            if (vec.LengthSquared > Fixed.ToFloat(32))
            {
                vec = Func204D518(vec, UpVector);
                if (vec.LengthSquared > Fixed.ToFloat(32))
                {
                    vec = vec.Normalized();
                    if (Vector3.Dot(FacingVector, vec) < Fixed.ToFloat(4034))
                    {
                        Func214E444(vec, state: 6, animId: 12, AnimFlags.None);
                    }
                }
            }
        }

        private bool CheckInVolume()
        {
            return _volume1.TestPoint(Position);
        }

        private void Func214D9B0()
        {
            // todo: stop SFX
            if (_target != null)
            {
                _target.AttachedEnemy = null;
                _target = null;
            }
            _flags &= ~QuadtroidFlags.Bit0;
        }

        private void Func214E444(Vector3 vec, byte state, int animId, AnimFlags animFlags)
        {
            _flags &= ~QuadtroidFlags.Bit3;
            bool result = Func214D500(FacingVector, vec, UpVector);
            if (result)
            {
                _flags |= QuadtroidFlags.Bit3;
            }
            _field1E8 = vec;
            _state1 = _state2 = state;
            _speed = Vector3.Zero;
            _models[0].SetAnimation(animId, slot: 0, SetFlags.Texture | SetFlags.Material | SetFlags.Node, animFlags);
        }

        private bool Func214D500(Vector3 vec1, Vector3 vec2, Vector3 vec3)
        {
            var cross = Vector3.Cross(vec2, vec1);
            if (cross.LengthSquared > Fixed.ToFloat(32) && Vector3.Dot(cross.Normalized(), vec3) > 0)
            {
                return true;
            }
            return false;
        }

        protected override bool EnemyGetDrawInfo()
        {
            IReadOnlyList<Material> materials = _models[0].Model.Materials;
            if (_state1 == 11)
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    materials[i].Lighting = 0;
                }
            }
            DrawGeneric();
            if (_state1 == 11)
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    Material material = materials[i];
                    material.Lighting = material.InitLighting;
                }
            }
            return true;
        }

        [Flags]
        private enum QuadtroidFlags
        {
            None = 0,
            Bit0 = 1,
            Bit1 = 2,
            Bit2 = 4,
            Bit3 = 8,
            Bit4 = 0x10,
            Bit5 = 0x20,
            Bit6 = 0x40,
            Bit7 = 0x80
        }
    }
}
