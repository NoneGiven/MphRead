using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy41Entity : EnemyInstanceEntity
    {
        private readonly EnemySpawnEntity _spawner;
        public SlenchFlags SlenchFlags { get; private set; }
        private int _subtype = 0;
        private int _phase = 0;
        private byte _stateAfterSlam = 0; // sktodo: rename if this isn't right
        public int Subtype => _subtype;
        public int Phase => _phase;
        private SlenchState State => (SlenchState)_state1;

        private float _targetAngle = 0;
        private float _targetX = 0;
        private float _targetZ = 0;
        private ushort _field196 = 0;
        private ushort _field198 = 0;
        private float _field208 = 0;
        private Vector3 _field1F8;
        private float _shieldOffset = 0;
        public float ShieldOffset => _shieldOffset;
        private Vector3 _startPos;
        private Vector3 _field1E0;
        private Vector3 _field1EC;
        private float _field18C;
        private float _field204;
        private Vector3 _field1BC;
        private Vector3 _field1D4;
        private float _field218 = 0;

        private int _staticShotCounter = 0;
        private int _field19C_B = 0; // note: these two fields are the same in-game
        private int _staticShotCooldown = 0;
        private int _field1A0_B = 0; // note: these two fields are the same in-game
        private bool _field1A3 = false;
        private int _rollTimer = 0;
        private int _staticShotTimer = 0;
        private int _field19E_B = 0; // note: these two fields are the same in-game

        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;
        private int _shotCooldown = 0;

        private ModelInstance _model = null!;
        private Enemy42Entity? _shield = null!;
        private readonly Enemy44Entity[] _synapses = new Enemy44Entity[_synapseCount];
        public int SynapseIndex { get; private set; }

        private EffectEntry? _shotEffect = null;
        private EffectEntry? _shieldEffect1 = null;
        private EffectEntry? _shieldEffect2 = null;
        private EffectEntry? _damageEffect = null;

        private const ushort _synapseCount = 3;

        public Enemy41Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as EnemySpawnEntity;
            Debug.Assert(spawner != null);
            _spawner = spawner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Enemy41Values GetValues()
        {
            return Metadata.Enemy41Values[_subtype * 3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Enemy41Values GetPhaseValues()
        {
            return Metadata.Enemy41Values[_subtype * 3 + _phase];
        }

        protected override void EnemyInitialize()
        {
            // default: if (_scene.RoomId == 35) // UNIT1_B1
            SlenchFlags = SlenchFlags.Bit2;
            if (_scene.RoomId == 82) // UNIT4_B1
            {
                _subtype = 1;
            }
            else if (_scene.RoomId == 64) // UNIT2_B2
            {
                _subtype = 2;
            }
            else if (_scene.RoomId == 76) // UNIT3_B2
            {
                _subtype = 3;
                SlenchFlags = SlenchFlags.Rolling;
            }
            Vector3 position = _data.Spawner.Position;
            Vector3 facing = _data.Spawner.FacingVector;
            Vector3 up = _data.Spawner.UpVector;
            Matrix4 transform = GetTransformMatrix(facing, up);
            transform.Row3.Xyz = position;
            Transform = transform;
            Enemy41Values values = GetValues();
            _health = _healthMax = (ushort)values.Health;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.NoMaxDistance;
            Flags |= EnemyFlags.OnRadar;
            HealthbarMessageId = 2;
            Metadata.LoadEffectiveness(Metadata.SlenchEffectiveness[_subtype], BeamEffectiveness);
            _hurtVolumeInit = new CollisionVolume(_spawner.Data.Fields.S00.Volume0);
            _boundingRadius = _hurtVolumeInit.SphereRadius;
            _hurtVolumeInit = new CollisionVolume(_hurtVolumeInit.SpherePosition, 2.9f); // 11878
            _shieldOffset = _hurtVolumeInit.SphereRadius;
            _model = SetUpModel("BigEyeBall", animIndex: 10, animFlags: AnimFlags.NoLoop);
            _field196 = 1000;
            // initializing to Slench Tear here, but it will get updated later
            WeaponInfo slenchTear = Weapons.BossWeapons[3];
            _equipInfo = new EquipInfo(slenchTear, _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
            if (facing.Y >= -0.5f)
            {
                Vector3 normalized = facing.WithY(0).Normalized();
                _targetX = normalized.X;
                _targetZ = normalized.Z;
            }
            else
            {
                Vector3 normalized = up.WithY(0).Normalized();
                _targetX = normalized.X;
                _targetZ = normalized.Z;
                _targetAngle = 90;
            }
            UpdateFacing();
            _field1F8 = new Vector3(_targetX, 0, _targetZ);
            _startPos = position;
            _field1E0 = facing * 3 + position;
            _field1EC = _field1E0 + facing;
            ChangeState(SlenchState.Initial);
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.SlenchShield, NodeRef, _scene) is not Enemy42Entity shield)
            {
                return;
            }
            _scene.AddEntity(shield);
            _shield = shield;
            for (int i = 0; i < _synapseCount; i++)
            {
                // the game uses _field19E/_field19E_A/timer1 as a temp for this
                SynapseIndex = i;
                if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.SlenchSynapse, NodeRef, _scene) is not Enemy44Entity synapse)
                {
                    return;
                }
                _scene.AddEntity(synapse);
                _synapses[i] = synapse;
            }
            UpdateScanId(values.ScanId1);
        }

        private void UpdateFacing()
        {
            var facing = new Vector3(_targetX, 0, _targetZ);
            var axis = new Vector3(_targetZ, 0, _targetX * -1);
            var rotMtx = Matrix4.CreateFromAxisAngle(axis, MathHelper.DegreesToRadians(_targetAngle));
            facing = Matrix.Vec3MultMtx3(facing, rotMtx).Normalized();
            Vector3 up = Vector3.Cross(facing, axis).Normalized();
            Matrix4 transform = GetTransformMatrix(facing, up);
            transform.Row3.Xyz = Position;
            Transform = transform;
        }

        private void ChangeState(SlenchState state)
        {
            _field1A3 = false;
            SlenchFlags &= ~SlenchFlags.Bit7;
            SlenchFlags &= ~SlenchFlags.Vulnerable;
            SlenchFlags &= ~SlenchFlags.Bit9;
            Enemy41Values phaseValues = GetPhaseValues();
            switch (state)
            {
            case SlenchState.Initial:
            case SlenchState.State12:
            case SlenchState.State13:
                CloseEye();
                break;
            case SlenchState.Intro:
                _soundSource.PlaySfx(SfxId.BIGEYE_INTRO_SCR);
                SlenchFlags &= ~SlenchFlags.Detached;
                SlenchFlags |= SlenchFlags.EyeClosed;
                _model.SetAnimation(13, AnimFlags.NoLoop);
                break;
            case SlenchState.ShieldRaise:
                _shieldEffect2 = _scene.SpawnEffectGetEntry(69, Vector3.UnitX, Vector3.UnitY, Position); // eyeShieldCharge
                break;
            case SlenchState.Idle:
                CloseEye();
                int min = phaseValues.MinStaticShotTimer * 2; // todo: FPS stuff
                int max = phaseValues.MaxStaticShotTimer * 2; // todo: FPS stuff
                _staticShotTimer = (int)(min + Rng.GetRandomInt2(max - min));
                break;
            case SlenchState.ShootTear:
                _soundSource.PlaySfx(SfxId.BIGEYE_ATTACK1A_SCR);
                SlenchFlags &= ~SlenchFlags.Detached;
                OpenEye();
                _staticShotTimer = 0;
                _staticShotCooldown = 0;
                _staticShotCounter = 0;
                Vector3 up = FacingVector; // swap facing and up
                Vector3 facing;
                if (up.Z <= -0.9f || up.Z >= 0.9f) // 3686
                {
                    facing = Vector3.Cross(Vector3.UnitX, up).Normalized();
                }
                else
                {
                    facing = Vector3.Cross(Vector3.UnitZ, up).Normalized();
                }
                _shotEffect = _scene.SpawnEffectGetEntry(81, facing, up, Position); // tearChargeUp
                _shotEffect?.SetElementExtension(false);
                break;
            case SlenchState.ShieldLower:
                CloseEye();
                _field198 = 0;
                UpdateScanId(GetValues().ScanId2);
                break;
            case SlenchState.Return:
                CloseEye();
                SlenchFlags &= ~SlenchFlags.Detached;
                break;
            case SlenchState.Attach:
                _soundSource.PlaySfx(SfxId.BIGEYE_ATTACH_SCR);
                if (_damageEffect != null)
                {
                    _scene.DetachEffectEntry(_damageEffect, setExpired: false);
                    _damageEffect = null;
                }
                UpdateScanId(GetValues().ScanId1);
                break;
            case SlenchState.Detach:
                SlenchFlags |= SlenchFlags.Detached;
                _field204 = 0;
                _field18C = 0;
                _field1A0_B = phaseValues.Field28; // sktodo: FPS stuff
                _field198 = 0;
                SlenchFlags &= ~SlenchFlags.Rolling;
                SlenchFlags &= ~SlenchFlags.Bit2;
                SlenchFlags &= ~SlenchFlags.Bit3;
                SlenchFlags &= ~SlenchFlags.Bit4;
                SlenchFlags &= ~SlenchFlags.Bit5;
                if (_subtype == 3)
                {
                    CloseEye();
                    SlenchFlags |= SlenchFlags.Rolling;
                    _rollTimer = phaseValues.RollTime;
                }
                else
                {
                    OpenEye();
                    SlenchFlags |= SlenchFlags.Bit2;
                }
                break;
            case SlenchState.State9:
                CloseEye();
                _field204 = 0;
                SlenchFlags &= ~SlenchFlags.Rolling;
                SlenchFlags &= ~SlenchFlags.Bit2;
                SlenchFlags &= ~SlenchFlags.Bit3;
                SlenchFlags &= ~SlenchFlags.Bit4;
                SlenchFlags &= ~SlenchFlags.Bit5;
                SlenchFlags |= SlenchFlags.Bit2;
                SlenchFlags |= SlenchFlags.Vulnerable;
                break;
            case SlenchState.Roam:
                SlenchFlags |= SlenchFlags.Detached;
                if (_subtype == 3 && SlenchFlags.TestFlag(SlenchFlags.Rolling))
                {
                    CloseEye();
                }
                else
                {
                    OpenEye();
                    SlenchFlags |= SlenchFlags.Vulnerable;
                }
                break;
            case SlenchState.State11:
                _soundSource.PlaySfx(SfxId.BIGEYE_ATTACK3_SCR);
                CloseEye();
                _field19C_B = 0;
                _stateAfterSlam = _state1;
                break;
            case SlenchState.Dead:
                _field19E_B = 36; // sktodo: FPS stuff
                CloseEye();
                _scene.SpawnEffect(206, FacingVector, UpVector, Position); // eyeFinalKill
                break;
            }
            _state2 = (byte)state;
        }

        private void UpdateScanId(int scanId)
        {
            _scanId = scanId;
            _shield?.UpdateScanId(scanId);
        }

        private void OpenEye()
        {
            if (SlenchFlags.TestFlag(SlenchFlags.EyeClosed))
            {
                SlenchFlags &= ~SlenchFlags.EyeClosed;
                _soundSource.PlaySfx(SfxId.BIGEYE_OPEN);
                int animIndex = SlenchFlags.TestFlag(SlenchFlags.Detached) ? 4 : 10;
                _model.SetAnimation(animIndex, AnimFlags.NoLoop);
            }
        }

        private void CloseEye()
        {
            if (!SlenchFlags.TestFlag(SlenchFlags.EyeClosed))
            {
                SlenchFlags |= SlenchFlags.EyeClosed;
                _soundSource.PlaySfx(SfxId.BIGEYE_CLOSE);
                int animIndex = SlenchFlags.TestFlag(SlenchFlags.Detached) ? 2 : 8;
                _model.SetAnimation(animIndex, AnimFlags.NoLoop);
            }
        }

        protected override void EnemyProcess()
        {
            Enemy41Values phaseValues = GetPhaseValues();
            Vector3 facing = FacingVector;
            Vector3 up = UpVector;
            Vector3 playerTarget = PlayerEntity.Main.Volume.SpherePosition.AddY(0.5f);
            if (SlenchFlags.TestFlag(SlenchFlags.Bit9))
            {
                _field208 += 20 / 2; // todo: FPS stuff
                if (_field208 >= 360)
                {
                    _field208 -= 360;
                }
                Vector3 axis = facing;
                var rotMtx = Matrix4.CreateFromAxisAngle(axis, MathHelper.DegreesToRadians(_field208));
                playerTarget += Matrix.Vec3MultMtx3(up, rotMtx).Normalized() * 2;
            }
            if (SlenchFlags.TestFlag(SlenchFlags.Bit7))
            {
                if (_shotCooldown > 0)
                {
                    _shotCooldown--;
                }
                else
                {
                    _equipInfo.Weapon = Weapons.BossWeapons[4 + _subtype];
                    Vector3 spawnPos = facing * _shieldOffset + Position;
                    BeamResultFlags result = BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, facing, BeamSpawnFlags.None, _scene);
                    if (result != BeamResultFlags.NoSpawn)
                    {
                        Func2136788();
                        _soundSource.PlaySfx(SfxId.BIGEYE_ATTACK2);
                        _shotCooldown = _equipInfo.Weapon.ShotCooldown * 2; // todo: FPS stuff
                    }
                }
            }
            if (HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                PlayerEntity.Main.TakeDamage(35, DamageFlags.None, facing, this);
            }
            if (State == SlenchState.Roam)
            {
                // sktodo: fields -- field1E0 is the "base" hovering position?
                // oscillating y value is added to its y, then collision is checked between there and the shield radius for a bit of a buffer
                // not sure when/if x and z are updated or used atm
                if (SlenchFlags.TestFlag(SlenchFlags.Bit2))
                {
                    // cosine result in 0, 0.4, 0, -0.4, 0 over a period of 16f at 30 fps (0.5333 seconds)
                    // angle is between 0 and 360 over that time, which is 675 degrees per second
                    float increment = MathF.Cos(MathHelper.DegreesToRadians(_scene.GlobalElapsedTime / 675f)) * 0.4f;
                    float y = _field1E0.Y + increment;
                    Vector3 position = Position.WithY(y);
                    Vector3 target = Position.WithY(y + _shieldOffset * (increment >= 0 ? 1 : -1));
                    CollisionResult discard = default;
                    if (!CollisionDetection.CheckBetweenPoints(position, target, TestFlags.None, _scene, ref discard))
                    {
                        Position = position; // only changing Y
                    }
                }
                else
                {
                    Func21365B8(0.1f); // 410
                }
            }
            Func213669C();
            if (IsStaticState() && AreAllSynapsesDead())
            {
                ChangeState(SlenchState.ShieldLower);
            }
            if (IsStaticState() && State != SlenchState.Initial && State != SlenchState.Intro)
            {
                // finished intro or starting new phase, activate synpases
                // todo?: technically FPS stuff here since it activates them on successive frames
                for (int i = 0; i < _synapseCount; i++)
                {
                    Enemy44Entity synapse = _synapses[i];
                    if (synapse.State == SynapseState.Initial)
                    {
                        synapse.ChangeState(SynapseState.Appear);
                    }
                    // note that its state is not updated immediately by ChangeState
                    if (synapse.State == SynapseState.Initial || synapse.State == SynapseState.Appear)
                    {
                        break;
                    }
                }
            }
            _damageEffect?.Transform(Position, Transform);
            if (AreAllSynapsesDead())
            {
                if (_shieldEffect1 != null)
                {
                    _scene.DetachEffectEntry(_shieldEffect1, setExpired: false);
                    _shieldEffect1 = null;
                    _shieldEffect2 = _scene.SpawnEffectGetEntry(83, Vector3.UnitX, Vector3.UnitY, Position); // eyeShieldDefeat
                }
            }
            else if (_state1 >= 3 && _shieldEffect1 == null) // not Initial, Intro, or ShieldRaise
            {
                _shieldEffect1 = _scene.SpawnEffectGetEntry(82, Vector3.UnitX, Vector3.UnitY, Position); // eyeShield
                _shieldEffect1?.SetElementExtension(true);
            }
            if (State == SlenchState.Initial)
            {
                if ((playerTarget - Position).LengthSquared <= 16 * 16)
                {
                    ChangeState(SlenchState.Intro);
                }
            }
            else if (State == SlenchState.Intro)
            {
                if (_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    SlenchFlags &= ~SlenchFlags.EyeClosed;
                    SlenchFlags &= ~SlenchFlags.Detached;
                    ChangeState(SlenchState.ShieldRaise);
                }
            }
            else if (State == SlenchState.ShieldRaise)
            {
                if (_shieldEffect2?.IsFinished == true)
                {
                    _scene.DetachEffectEntry(_shieldEffect2, setExpired: false);
                    _shieldEffect2 = null;
                    ChangeState(SlenchState.Idle);
                }
            }
            else if (State == SlenchState.Idle)
            {
                if (_staticShotTimer > 0 && --_staticShotTimer > 0)
                {
                    RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement2) / 2); // todo: FPS stuff
                }
                else
                {
                    ChangeState(SlenchState.ShootTear);
                }
            }
            else if (State == SlenchState.ShootTear)
            {
                if (_staticShotTimer < 36 * 2) // todo: FPS stuff
                {
                    _staticShotTimer++;
                    if (_staticShotTimer < 16 * 2) // todo: FPS stuff
                    {
                        RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement3) / 2); // todo: FPS stuff
                    }
                    else
                    {
                        if (_staticShotTimer == 16 * 2) // todo: FPS stuff
                        {
                            // the game has a bad macro expansion here where the angle is chosen and converted to fx32,
                            // and is likely supposed to be used from there as-is, but instead the fx32 value gets converted
                            // to float representation and has 0.5 added to it before being converted back to int representation.
                            // the result is no change since the decimals just get truncated when it's converted back.
                            // on top of that, there's an additional bug where the RNG is called twice, with the first value
                            // being tested to go into another branch of the bad macro, where the RNG is called again to
                            // actually get the angle value. in the other code path, which has a 1/360 chance of being taken,
                            // 0.5 is subtracted from the angle instead. except in the case of 0, this results in the
                            // angle being 1 fx32 less (1/4096f). we don't do any of that, but we will still call RNG twice.
                            Rng.GetRandomInt2(360);
                            float angle = Rng.GetRandomInt2(360);
                            var rotMtx = Matrix4.CreateFromAxisAngle(facing, MathHelper.DegreesToRadians(angle));
                            Vector3 vecA = Matrix.Vec3MultMtx3(up, rotMtx);
                            Vector3 vecB = facing * 4 + Position;
                            float randf = Rng.GetRandomInt2(0x2000) / 4096f + 2; // [2.0, 4.0)
                            _field1D4 = vecA * randf + vecB;
                        }
                        RotateToTarget(_field1D4, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2); // todo: FPS stuff
                    }
                }
                else if (_staticShotCooldown != 0)
                {
                    _staticShotCooldown--;
                    RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement3) / 2); // todo: FPS stuff
                }
                else if (_staticShotCounter < phaseValues.StaticShotCount)
                {
                    _staticShotCounter++;
                    _staticShotCooldown = phaseValues.StaticShotCooldown * 2; // todo: FPS stuff
                    _equipInfo.Weapon = Weapons.BossWeapons[3]; // tear
                    if (_staticShotCounter > 1)
                    {
                        _soundSource.PlaySfx(SfxId.MISSILE);
                    }
                    Vector3 spawnPos = facing * _shieldOffset + Position;
                    BeamResultFlags result = BeamProjectileEntity.Spawn(this, _equipInfo, spawnPos, facing, BeamSpawnFlags.None, _scene);
                    if (result != BeamResultFlags.NoSpawn)
                    {
                        Func2136788();
                    }
                }
                if (_shotEffect != null)
                {
                    _shotEffect.Transform(up, facing, Position); // swap facing and up
                    if (_shotEffect.IsFinished)
                    {
                        _scene.DetachEffectEntry(_shotEffect, setExpired: false);
                        _shotEffect = null;
                    }
                }
                if (_shotEffect == null && _staticShotCounter == phaseValues.StaticShotCount)
                {
                    ChangeState(SlenchState.Idle);
                }
            }
            else if (State == SlenchState.ShieldLower)
            {
                if (_shieldEffect2 != null)
                {
                    _shieldEffect2.Transform(Position, Transform);
                    if (_shieldEffect2.IsFinished)
                    {
                        _scene.DetachEffectEntry(_shieldEffect2, setExpired: false);
                        _shieldEffect2 = null;
                        _soundSource.PlaySfx(SfxId.BIGEYE_DETACH);
                    }
                }
                else if (_subtype == 3
                    || RotateToTarget(_field1EC, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2) // todo: FPS stuff
                    && MoveToPosition(_field1E0, Fixed.ToFloat(phaseValues.MoveIncrement1) / 2)) // todo: FPS stuff
                {
                    _field218 = 0;
                    SlenchFlags &= ~SlenchFlags.Rolling;
                    SlenchFlags &= ~SlenchFlags.Bit3;
                    SlenchFlags |= SlenchFlags.Bit2;
                    ChangeState(SlenchState.Detach);
                }
            }
            else if (State == SlenchState.Return)
            {
                if (_damageEffect != null && _model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _model.SetAnimation(0);
                }
                if (CheckPosAgainstCurrent(_field1E0))
                {
                    ChangeState(SlenchState.Attach);
                }
                else if (RotateToTarget(_field1E0, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2)) // todo: FPS stuff
                {
                    MoveToPosition(_field1E0, Fixed.ToFloat(phaseValues.MoveIncrement2) / 2); // todo: FPS stuff
                }
            }
            else if (State == SlenchState.Attach)
            {
                if (RotateToTarget(_field1EC, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2) // todo: FPS stuff
                    && MoveToPosition(_startPos, Fixed.ToFloat(phaseValues.MoveIncrement2) / 2)) // todo: FPS stuff
                {
                    for (int i = 0; i < _synapseCount; i++)
                    {
                        Enemy44Entity synapse = _synapses[i];
                        synapse.ChangeState(SynapseState.Initial);
                    }
                    ChangeState(SlenchState.ShieldRaise);
                }
            }
            else if (State == SlenchState.Detach)
            {
                ChangeState(SlenchState.Roam);
            }
            else if (State == SlenchState.State9)
            {
                Vector3 pos = _field1E0.WithY(_field18C);
                if (CheckPosAgainstCurrent(pos))
                {
                    ChangeState(SlenchState.Roam);
                }
                else if (RotateToTarget(pos, Fixed.ToFloat(phaseValues.AngleIncrement4) / 2)) // todo: FPS stuff
                {
                    MoveToPosition(pos, Fixed.ToFloat(phaseValues.MoveIncrement3) / 2); // todo: FPS stuff
                }
            }
            else if (State == SlenchState.Roam)
            {
                int phaseHealth = _healthMax / 3 * (2 - _phase);
                if (_health > phaseHealth)
                {
                    if (_field1A0_B == 0 || --_field1A0_B != 0)
                    {
                        // below 1/4th of phase health
                        if (_health <= phaseHealth + _healthMax / 12)
                        {
                            SlenchFlags |= SlenchFlags.Bit9;
                        }
                        if (_subtype == 3)
                        {
                            if (SlenchFlags.TestFlag(SlenchFlags.Rolling))
                            {
                                // sktodo: FPS stuff, underflow?
                                if (_rollTimer == 0 || --_rollTimer != 0)
                                {
                                    if (_field1A3)
                                    {
                                        _soundSource.PlaySfx(SfxId.SPIRE_ROLL, loop: true);
                                        if (_field18C == 0)
                                        {
                                            _field18C = (_field1E0.Y - Position.Y) / 2;
                                        }
                                        // sktodo: FPS stuff
                                        _field204 += Fixed.ToFloat(phaseValues.Field38);
                                        if (_field204 >= 360)
                                        {
                                            SlenchFlags ^= SlenchFlags.Bit4;
                                            _field204 -= 360;
                                        }
                                        Vector3 vecA;
                                        float angle;
                                        if (SlenchFlags.TestFlag(SlenchFlags.Bit4))
                                        {
                                            vecA = _field1F8 * -1;
                                            angle = 360 - _field204;
                                        }
                                        else
                                        {
                                            vecA = _field1F8;
                                            angle = _field204 + 180;
                                        }
                                        float factor = Fixed.ToFloat(phaseValues.Field40);
                                        Vector3 vecB = vecA * factor + _field1E0;
                                        (float sin, float cos) = MathF.SinCos(MathHelper.DegreesToRadians(angle));
                                        var mtx = new Matrix3(
                                            cos, 0, -sin,
                                            0, 1, 0,
                                            sin, 0, cos
                                        );
                                        Vector3 newPos = _field1F8 * mtx * factor + vecB;
                                        Position = newPos.WithY(Position.Y);
                                        _targetAngle += Fixed.ToFloat(phaseValues.Field38); // sktodo: FPS stuff
                                        if (_targetAngle < 0)
                                        {
                                            _targetAngle += 360;
                                        }
                                        Vector3 vecC = (Position - vecB).WithY(0).Normalized();
                                        if (SlenchFlags.TestFlag(SlenchFlags.Bit4))
                                        {
                                            _targetZ = vecC.X;
                                            _targetX = -vecC.Z;
                                        }
                                        else
                                        {
                                            _targetZ = -vecC.X;
                                            _targetX = vecC.Z;
                                        }
                                    }
                                    else if (_field218 == 0)
                                    {
                                        _field1A3 = true;
                                    }
                                }
                                else // roll timer decremented to zero
                                {
                                    _soundSource.StopSfx(SfxId.SPIRE_ROLL);
                                    ChangeState(SlenchState.State9);
                                }
                            }
                            else // not rolling
                            {
                                _field204 += Fixed.ToFloat(phaseValues.Field34); // sktodo: FPS stuff
                                if (_field204 >= 360)
                                {
                                    SlenchFlags ^= SlenchFlags.Bit4;
                                    _field204 -= 360;
                                }
                                float factor = Fixed.ToFloat(phaseValues.Field3C);
                                float angle;
                                Vector3 vecA;
                                if (SlenchFlags.TestFlag(SlenchFlags.Bit4))
                                {
                                    angle = 540 - _field204;
                                    vecA = new Vector3(
                                        _field1E0.X - _field1F8.X * factor,
                                        _field18C,
                                        _field1E0.Z - _field1F8.Z * factor
                                    );
                                }
                                else
                                {
                                    angle = _field204;
                                    vecA = new Vector3(
                                        _field1E0.X + _field1F8.X * factor,
                                        _field18C,
                                        _field1E0.Z + _field1F8.Z * factor
                                    );
                                }
                                Vector3 vecB = Vector3.Cross(_field1F8, Vector3.UnitY).Normalized();
                                var rotMtx = Matrix4.CreateFromAxisAngle(vecB, MathHelper.DegreesToRadians(angle));
                                Position = Matrix.Vec3MultMtx3(_field1F8, rotMtx) * factor + vecA;
                                if (RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement4) / 2)) // todo: FPS stuff
                                {
                                    SlenchFlags |= SlenchFlags.Bit7;
                                }
                                else
                                {
                                    SlenchFlags &= ~SlenchFlags.Bit7;
                                }
                                Func21367EC(playerTarget, a3: true);
                            }
                        }
                        else // not subtype 3
                        {
                            _field204 += Fixed.ToFloat(phaseValues.Field34) / 2; // todo: FPS stuff
                            if (_field204 >= 360)
                            {
                                SlenchFlags ^= SlenchFlags.Bit4;
                                _field204 -= 360;
                            }
                            Vector3 vecA;
                            float angle = _field204 + 180;
                            if (SlenchFlags.TestFlag(SlenchFlags.Bit4))
                            {
                                vecA = Vector3.Cross(_field1F8, Vector3.UnitY).Normalized();
                                angle = 180 - _field204;
                            }
                            else
                            {
                                vecA = Vector3.Cross(Vector3.UnitY, _field1F8).Normalized();
                            }
                            vecA *= Fixed.ToFloat(phaseValues.Field3C);
                            Vector3 vecB = vecA + _field1E0;
                            var rotMtx = Matrix4.CreateFromAxisAngle(_field1F8, MathHelper.DegreesToRadians(angle));
                            vecA = Matrix.Vec3MultMtx3(vecA, rotMtx);
                            Position = vecA + vecB;
                            if (_subtype != 0)
                            {
                                // sktodo: FPS stuff etc.
                                float increment = Fixed.ToFloat(phaseValues.MoveIncrement3);
                                _field18C += SlenchFlags.TestFlag(SlenchFlags.Bit5) ? -increment : increment;
                                if (_field18C < 0 || _field18C >= Fixed.ToFloat(phaseValues.RollTime))
                                {
                                    SlenchFlags ^= SlenchFlags.Bit5;
                                }
                                Position += _field1F8 * _field18C;
                                if (_subtype == 2)
                                {
                                    Func21367EC(playerTarget, a3: true);
                                }
                            }
                            if (!RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement4) / 2)) // todo: FPS stuff
                            {
                                SlenchFlags &= ~SlenchFlags.Bit7;
                            }
                            else
                            {
                                uint rand = Rng.GetRandomInt2(100);
                                if (rand >= 50)
                                {
                                    SlenchFlags &= ~SlenchFlags.Bit7;
                                }
                                else
                                {
                                    SlenchFlags |= SlenchFlags.Bit7;
                                }
                            }
                        }
                    }
                    else // field1A0 decremented to zero
                    {
                        ChangeState(SlenchState.Return);
                    }
                }
                else // below phase health
                {
                    int effectId = 205; // eyeKill3
                    if (_phase == 0)
                    {
                        effectId = 203; // eyeKill1
                    }
                    else if (_phase == 1)
                    {
                        effectId = 204; // eyeKill2
                    }
                    _scene.SpawnEffect(effectId, Transform);
                    _damageEffect = _scene.SpawnEffectGetEntry(201, Transform); // eyeDamageLoop
                    _damageEffect?.SetElementExtension(true);
                    _phase++;
                    _soundSource.PlaySfx(SfxId.BIGEYE_DIE_SCR);
                    ChangeState(SlenchState.Return);
                }
            }
            else if (State == SlenchState.State11)
            {
                _field1BC = playerTarget;
                Position = _field1D4;
                RotateToTarget(_field1BC, Fixed.ToFloat(phaseValues.Field44) / 2); // todo: FPS stuff
                int div = 360 / phaseValues.Field57 * phaseValues.Field56;
                if (++_field19C_B >= div) // sktodo: FPS stuff
                {
                    ChangeState(SlenchState.State12);
                }
                else
                {
                    // sktodo: FPS stuff
                    _field208 += phaseValues.Field57; // the game has to convert this to fx32 here
                    if (_field208 >= 360)
                    {
                        _field208 -= 360;
                    }
                    var rotMtx = Matrix4.CreateFromAxisAngle(facing, MathHelper.DegreesToRadians(_field208));
                    // sktodo: variable names (percentage stuff), convert to float math earlier?
                    int value = phaseValues.Field58 - _field19C_B * phaseValues.Field57 / div * 2;
                    if (value < 41)
                    {
                        value = 41;
                    }
                    float factor = Fixed.ToFloat(value);
                    Position = Matrix.Vec3MultMtx3(up, rotMtx) * factor + _field1D4;
                }
            }
            else if (State == SlenchState.State12)
            {
                if (MoveToPosition(_field1BC, Fixed.ToFloat(phaseValues.MoveIncrement4) / 2)) // todo: FPS stuff
                {
                    ChangeState(SlenchState.State13);
                }
            }
            else if (State == SlenchState.State13)
            {
                if (MoveToPosition(_field1D4, Fixed.ToFloat(phaseValues.MoveIncrement5) / 2)) // todo: FPS stuff
                {
                    ChangeState((SlenchState)_stateAfterSlam);
                }
            }
            else if (State == SlenchState.Dead)
            {
                if (_field19E_B != 0)
                {
                    _field19E_B--;
                }
                if (_field19E_B == 0)
                {
                    _health = 0;
                    Flags &= ~EnemyFlags.Invincible;
                }
            }
            UpdateFacing();
        }

        // sktodo: function name
        private void Func2136788()
        {
            _field196 = 0;
            _field1D4 = Position;
            _field1BC = (FacingVector * -1).Normalized();
        }

        // sktodo: function name (also, this could be inlined in EnemyProcess)
        private void Func21365B8(float value)
        {
            _field218 -= value;
            _field218 = Math.Clamp(_field218, -1.1f, 1.1f); // -4506, 4506
            if (Func21374C4(Vector3.UnitY, _field218))
            {
                Position = Position.AddY(_field218);
            }
            else
            {
                _field218 = -_field218 / (SlenchFlags.TestFlag(SlenchFlags.Bit3) ? 1 : 2);
                if (_field218 > -0.05f && _field218 < 0.05f) // -205, 205
                {
                    _field218 = 0;
                }
            }
        }

        // sktodo: function name (also, this could be inlined in Func21365B8)
        // sktodo: parameter/variable names (could just remove the parameters and use UnitY and _field218)
        private bool Func21374C4(Vector3 vec, float a3)
        {
            CollisionResult res = default;
            Vector3 position = Position;
            float v3 = a3 + _shieldOffset * (a3 >= 0 ? 1 : -1);
            if (vec != Vector3.Zero)
            {
                Vector3 dest = vec * v3 + position;
                if (CollisionDetection.CheckBetweenPoints(position, dest, TestFlags.None, _scene, ref res))
                {
                    return false;
                }
            }
            if (vec.X != 0 || vec.Z != 0)
            {
                // todo: unnecessary? not sure what this will catch that the next one wouldn't
                Vector3 dest = vec * v3 + position.AddY(_shieldOffset / -2);
                if (CollisionDetection.CheckBetweenPoints(position, dest, TestFlags.None, _scene, ref res))
                {
                    return false;
                }
                dest = vec * v3 + position.AddY(_shieldOffset * -1);
                if (CollisionDetection.CheckBetweenPoints(position, dest, TestFlags.None, _scene, ref res))
                {
                    return false;
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
                    Vector3 doorFacing = door.FacingVector;
                    var plane = new Vector4(doorFacing);
                    Vector3 between = position - door.LockPosition;
                    if (Vector3.Dot(between, doorFacing) < 0)
                    {
                        plane *= -1;
                    }
                    // 1638
                    plane.W = (plane.X * 0.4f + door.LockPosition.X) * plane.X
                        + (plane.Y * 0.4f + door.LockPosition.Y) * plane.Y
                        + (plane.Z * 0.4f + door.LockPosition.Z) * plane.Z;
                    Vector3 cylTop = position + vec * a3;
                    if (CollisionDetection.CheckCylinderIntersectPlane(position, cylTop, plane, ref res) && res.Distance < 2)
                    {
                        between = res.Position - door.LockPosition;
                        if (between.LengthSquared < door.RadiusSquared)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        // sktodo: field name (and move to metadata?)
        private static readonly IReadOnlyList<float> _field2138398 = new float[5]
        {
            // 0x666, 0xB33, 0xD9A, 0xE66, 0x1000
            0.4f, 0.7f, 0.85f, 0.9f, 1.0f
            // + 0.3 +0.15 +0.05 + 0.1
        };

        // sktodo: function name
        private void Func213669C()
        {
            int field196 = _field196 / 2; // sktodo: FPS stuff -- always calculate instead of LUT?
            if (field196 >= 10)
            {
                return;
            }
            float factor = 1;
            if (field196 <= 4) // 0 - 4
            {
                factor = _field2138398[field196];
            }
            else if (field196 <= 9) // 5 - 9
            {
                // the game has a bad macro expansion or something here with a conversion to float,
                // but the end result is just getting diff as fx32 to do the following factor calc
                float diff = 9 - field196;
                factor = diff * (1 / 5f);
            }
            Position = _field1BC * factor + _field1D4;
            _field196++;
        }

        private bool IsStaticState()
        {
            // true if Initial, Intro, ShieldRaise, Idle, or ShootTear
            // false if ShieldLower or anything that comes after it
            return _state1 < 5;
        }

        private bool AreAllSynapsesDead()
        {
            // returns true if all synpases have state 5, false otherwise
            for (int i = 0; i < _synapseCount; i++)
            {
                if (_synapses[i].State != SynapseState.Dead)
                {
                    return false;
                }
            }
            return true;
        }

        public bool CanSynapsesRespawn()
        {
            if (!IsStaticState())
            {
                return false;
            }
            for (int i = 0; i < _synapseCount; i++)
            {
                Enemy44Entity synapse = _synapses[i];
                if (synapse.State != SynapseState.Dying && synapse.State != SynapseState.Dead)
                {
                    return true;
                }
            }
            return false;
        }

        // sktodo: function name
        // sktodo: parameter names
        private bool Func21367EC(Vector3 vec, bool a3)
        {
            Enemy41Values phaseValues = GetPhaseValues();
            if (_field198 < phaseValues.Field58)
            {
                // sktodo: probably FPS stuff? I'm guessing this is wait time before lunging
                _field198++;
            }
            else if (a3)
            {
                Vector3 between = vec - Position;
                if (between.Length <= phaseValues.Field48)
                {
                    float radius = _shieldOffset * 0.75f; // 3072
                    vec -= Position;
                    float length = vec.Length;
                    if (length > radius)
                    {
                        _field1D4 = Position;
                        vec = vec.Normalized();
                        _field1BC = vec * (length - radius) + Position;
                        _field198 = 0;
                        ChangeState(SlenchState.State11);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckPosAgainstCurrent(Vector3 pos)
        {
            // sktodo: add tolerance? even in-game, the lack thereof might be why there's that stuck bug?
            return pos == Position;
        }

        private bool MoveToPosition(Vector3 position, float increment)
        {
            Vector3 between = position - Position;
            if (between.LengthSquared > increment * increment)
            {
                between = between.Normalized();
                Position += between * increment;
                return false;
            }
            Position = position;
            return true;
        }

        private bool RotateToTarget(Vector3 target, float increment)
        {
            // this function doesn't need floating point tolerance because in both cases
            // (setting the angle here, and setting the two fields in the other function),
            // it has a case to sets to exactly the target when it gets close enough
            float angle = 0;
            if (Position.Y != target.Y)
            {
                float x = target.X - Position.X;
                float z = target.Z - Position.Z;
                float sqrt = MathF.Sqrt(x * x + z * z);
                float y = Position.Y - target.Y;
                float atan = MathHelper.RadiansToDegrees(MathF.Atan2(y, sqrt));
                angle = atan + (atan < 0 ? 360 : 0);
            }
            float targetAngle = _targetAngle;
            if (targetAngle < angle)
            {
                float diff = angle - targetAngle;
                if (angle - targetAngle >= 180) // 737280
                {
                    if (360 - diff <= increment) // 1474560
                    {
                        targetAngle = angle;
                    }
                    else
                    {
                        targetAngle -= increment;
                    }
                }
                else if (diff <= increment)
                {
                    targetAngle = angle;
                }
                else
                {
                    targetAngle += increment;
                }
            }
            else if (targetAngle > angle)
            {
                float diff = targetAngle - angle;
                if (targetAngle - angle >= 180) // 737280
                {
                    if (360 - diff <= increment) // 1474560
                    {
                        targetAngle = angle;
                    }
                    else
                    {
                        targetAngle += increment;
                    }
                }
                else if (diff <= increment)
                {
                    targetAngle = angle;
                }
                else
                {
                    targetAngle -= increment;
                }
            }
            if (targetAngle >= 360) // 1474560
            {
                targetAngle -= 360;
            }
            _targetAngle = targetAngle;
            return RotateToTargetHorizontal(target, increment) && targetAngle == angle;
        }

        private bool RotateToTargetHorizontal(Vector3 target, float increment)
        {
            Vector3 between = target - Position;
            if (between.X == 0 && between.Z == 0)
            {
                return true;
            }
            between = between.WithY(0).Normalized();
            Vector3 fields = new Vector3(_targetX, 0, _targetZ).Normalized();
            Vector3 between2 = fields - between;
            (float sin, float cos) = MathF.SinCos(MathHelper.DegreesToRadians(increment));
            if (between2.X * between2.X + between2.Z * between2.Z <= (1 - cos) * (1 - cos) + sin * sin)
            {
                _targetX = between.X;
                _targetZ = between.Z;
                return true;
            }
            var cross = Vector3.Cross(between, fields);
            if (cross.Y > 0)
            {
                sin *= -1;
            }
            var mtx = new Matrix3(
                cos, 0, -sin,
                0, 1, 0,
                sin, 0, cos
            );
            fields *= mtx;
            fields = fields.Normalized();
            _targetX = fields.X;
            _targetZ = fields.Z;
            return false;
        }

        public bool ShieldTakeDamage(EntityBase? source)
        {
            return EnemyTakeDamage(source);
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_subtype == 3 && SlenchFlags.TestFlag(SlenchFlags.Rolling) && source?.Type == EntityType.Bomb)
            {
                // sktodo: test underflow issue or whatever
                // --> depending on the situation, it might matter what data type we make this
                _rollTimer -= 30 * 2; // todo: FPS stuff
                if (_rollTimer == 0)
                {
                    _rollTimer = 1; // todo: FPS stuff?
                }
            }
            if (Flags.TestFlag(EnemyFlags.Invincible))
            {
                if (!AreAllSynapsesDead() && State != SlenchState.Initial && State != SlenchState.Intro
                    && State != SlenchState.ShieldRaise && source?.Type == EntityType.BeamProjectile)
                {
                    var beam = (BeamProjectileEntity)source;
                    Vector3 up = (beam.Position - Position).Normalized();
                    Vector3 facing;
                    if (up.Z <= -0.9f || up.Z >= 0.9f) // 3686
                    {
                        facing = Vector3.Cross(Vector3.UnitX, up).Normalized();
                    }
                    else
                    {
                        facing = Vector3.Cross(Vector3.UnitZ, up).Normalized();
                    }
                    _scene.SpawnEffect(70, facing, up, Position); // eyeShieldHit
                    _soundSource.PlaySfx(SfxId.BIGEYE_DEFLECT);
                }
                return true;
            }
            _timeSinceDamage = 0;
            _soundSource.PlaySfx(SfxId.BIGEYE_DAMAGE);
            int animIndex = 11;
            if (SlenchFlags.TestFlag(SlenchFlags.EyeClosed))
            {
                animIndex = 12;
            }
            else if (SlenchFlags.TestFlag(SlenchFlags.Detached))
            {
                animIndex = 6;
            }
            _model.SetAnimation(animIndex, AnimFlags.NoLoop);
            if (_health == 0)
            {
                _health = 1;
                if (State != SlenchState.Dead)
                {
                    ChangeState(SlenchState.Dead);
                    _soundSource.PlaySfx(SfxId.BIGEYE_DIE_SCR, noUpdate: true, recency: Single.MaxValue, sourceOnly: true);
                }
                // todo: movie transition stuff
                if (PlayerEntity.Main.Health > 0)
                {
                    GameState.StorySave.CheckpointRoomId = -1;
                    GameState.StorySave.CheckpointEntityId = -1;
                    GameState.TransitionRoomId = _scene.RoomId;
                    _scene.SetFade(FadeType.FadeOutWhite, length: 40 / 30f, overwrite: true, AfterFade.AfterMovie);
                }
            }
            return false;
        }
    }

    // sktodo: flag names
    [Flags]
    public enum SlenchFlags : ushort
    {
        None = 0x0,
        EyeClosed = 0x1,
        Rolling = 0x2,
        Bit2 = 0x4,
        Bit3 = 0x8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Detached = 0x40,
        Bit7 = 0x80,
        Vulnerable = 0x100,
        Bit9 = 0x200,
        Unused10 = 0x400,
        Unused11 = 0x800,
        Unused12 = 0x1000,
        Unused13 = 0x2000,
        Unused14 = 0x4000,
        Unused15 = 0x8000
    }

    // sktodo: state names
    public enum SlenchState : byte
    {
        Initial = 0,
        Intro = 1,
        ShieldRaise = 2,
        Idle = 3,
        ShootTear = 4,
        ShieldLower = 5,
        Return = 6,
        Attach = 7,
        Detach = 8,
        State9 = 9,
        Roam = 10,
        State11 = 11,
        State12 = 12,
        State13 = 13,
        Dead = 14
    }

    public readonly struct Enemy41Values
    {
        public ushort ScanId1 { get; init; }
        public ushort ScanId2 { get; init; }
        public int AngleIncrement1 { get; init; }
        public int Health { get; init; }
        public int AngleIncrement2 { get; init; }
        public ushort MinStaticShotTimer { get; init; }
        public ushort MaxStaticShotTimer { get; init; }
        public short StaticShotCooldown { get; init; }
        public byte StaticShotCount { get; init; }
        public byte Padding17 { get; init; }
        public int AngleIncrement3 { get; init; }
        public int MoveIncrement1 { get; init; }
        public int MoveIncrement2 { get; init; }
        public int AngleIncrement4 { get; init; }
        public int Field28 { get; init; }
        public int MoveIncrement3 { get; init; }
        public int RollTime { get; init; }
        public int Field34 { get; init; }
        public int Field38 { get; init; }
        public int Field3C { get; init; }
        public int Field40 { get; init; }
        public int Field44 { get; init; }
        public int Field48 { get; init; }
        public int MoveIncrement4 { get; init; }
        public int MoveIncrement5 { get; init; }
        public ushort Field54 { get; init; }
        public byte Field56 { get; init; }
        public byte Field57 { get; init; }
        public int Field58 { get; init; }
        public ushort Magic { get; init; } // 0xBEEF
        public ushort Padding5E { get; init; }
    }
}
