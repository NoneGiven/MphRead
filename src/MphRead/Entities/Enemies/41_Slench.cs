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
        private byte _stateAfterSlam = 0;
        public int Subtype => _subtype;
        public int Phase => _phase;
        private SlenchState State => (SlenchState)_state1;

        private float _targetAngle = 0;
        private float _targetX = 0;
        private float _targetZ = 0;
        private Vector3 _targetHorizontal; // contains previous two fields and Y = 0
        private ushort _recoilTimer = 0;
        private ushort _slamTimer = 0;
        private float _wobbleAngle = 0;
        private float _shieldOffset = 0;
        public float ShieldOffset => _shieldOffset;
        private Vector3 _startPos;
        private Vector3 _detachedPosition;
        private Vector3 _detachedFacing;
        private float _floatBaseY;
        private float _patternAngle;
        private float _dropSpeed = 0;
        // these two vecs are used for various purposes, such as retaining an original position
        // while the current position shakes around. sometimes positions, sometimes vectors.
        private Vector3 _destVec1;
        private Vector3 _destVec2;

        private int _staticShotCounter = 0;
        private int _wobbleTimer = 0; // note: these two fields are timer_1 in-game
        private int _staticShotCooldown = 0;
        private int _roamTimer = 0; // note: these two fields are timer_2 in-game
        private int _staticShotTimer = 0;
        private int _deathTimer = 0; // note: these two fields are timer_3 in-game
        private int _rollTimer = 0;
        private bool _hitFloor = false;

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
            SlenchFlags = SlenchFlags.Floating;
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
            _recoilTimer = 1000;
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
            _targetHorizontal = new Vector3(_targetX, 0, _targetZ);
            _startPos = position;
            _detachedPosition = facing * 3 + position;
            _detachedFacing = _detachedPosition + facing;
            ChangeState(SlenchState.Initial);
            if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.SlenchShield, NodeRef, _scene) is not Enemy42Entity shield)
            {
                return;
            }
            _scene.AddEntity(shield);
            _shield = shield;
            for (int i = 0; i < _synapseCount; i++)
            {
                // the game uses timer_3 as a temp for this
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
            _hitFloor = false;
            SlenchFlags &= ~SlenchFlags.TargetingPlayer;
            SlenchFlags &= ~SlenchFlags.Vulnerable;
            SlenchFlags &= ~SlenchFlags.Wobbling;
            Enemy41Values phaseValues = GetPhaseValues();
            switch (state)
            {
            case SlenchState.Initial:
            case SlenchState.Slam:
            case SlenchState.SlamReturn:
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
                _slamTimer = 0;
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
                _patternAngle = 0;
                _floatBaseY = 0;
                _roamTimer = phaseValues.RoamTime * 2; // todo: FPS stuff
                _slamTimer = 0;
                SlenchFlags &= ~SlenchFlags.Rolling;
                SlenchFlags &= ~SlenchFlags.Floating;
                SlenchFlags &= ~SlenchFlags.Bouncy;
                SlenchFlags &= ~SlenchFlags.PatternFlip1;
                SlenchFlags &= ~SlenchFlags.PatternFlip2;
                if (_subtype == 3)
                {
                    CloseEye();
                    SlenchFlags |= SlenchFlags.Rolling;
                    _rollTimer = phaseValues.RollTime;
                }
                else
                {
                    OpenEye();
                    SlenchFlags |= SlenchFlags.Floating;
                }
                break;
            case SlenchState.RollingDone:
                CloseEye();
                _patternAngle = 0;
                SlenchFlags &= ~SlenchFlags.Rolling;
                SlenchFlags &= ~SlenchFlags.Bouncy;
                SlenchFlags &= ~SlenchFlags.PatternFlip1;
                SlenchFlags &= ~SlenchFlags.PatternFlip2;
                SlenchFlags |= SlenchFlags.Floating;
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
            case SlenchState.SlamReady:
                _soundSource.PlaySfx(SfxId.BIGEYE_ATTACK3_SCR);
                CloseEye();
                _wobbleTimer = 0;
                _stateAfterSlam = _state1;
                break;
            case SlenchState.Dead:
                _deathTimer = 36 * 2; // todo: FPS stuff
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
            if (SlenchFlags.TestFlag(SlenchFlags.Wobbling))
            {
                _wobbleAngle += 20 / 2; // todo: FPS stuff
                if (_wobbleAngle >= 360)
                {
                    _wobbleAngle -= 360;
                }
                Vector3 axis = facing;
                var rotMtx = Matrix4.CreateFromAxisAngle(axis, MathHelper.DegreesToRadians(_wobbleAngle));
                playerTarget += Matrix.Vec3MultMtx3(up, rotMtx).Normalized() * 2;
            }
            if (SlenchFlags.TestFlag(SlenchFlags.TargetingPlayer))
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
                        SetRecoilTargetVecs();
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
                if (SlenchFlags.TestFlag(SlenchFlags.Floating))
                {
                    // cosine result in 0, 0.4, 0, -0.4, 0 over a period of 16f at 30 fps (0.5333 seconds)
                    // angle is between 0 and 360 over that time, which is 675 degrees per second
                    float increment = MathF.Cos(MathHelper.DegreesToRadians(_scene.GlobalElapsedTime / 675f)) * 0.4f;
                    float y = _detachedPosition.Y + increment;
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
                    // 410 -- we're inverting the sign to get the right direction,
                    // but not adjusting for FPS here because constant accel is used
                    DropToFloor(-0.1f);
                }
            }
            ProcessRecoil();
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
                            _destVec2 = vecA * randf + vecB;
                        }
                        RotateToTarget(_destVec2, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2); // todo: FPS stuff
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
                        SetRecoilTargetVecs();
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
                    || RotateToTarget(_detachedFacing, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2) // todo: FPS stuff
                    && MoveToPosition(_detachedPosition, Fixed.ToFloat(phaseValues.MoveIncrement1) / 2)) // todo: FPS stuff
                {
                    _dropSpeed = 0;
                    SlenchFlags &= ~SlenchFlags.Rolling;
                    SlenchFlags &= ~SlenchFlags.Bouncy;
                    SlenchFlags |= SlenchFlags.Floating;
                    ChangeState(SlenchState.Detach);
                }
            }
            else if (State == SlenchState.Return)
            {
                if (_damageEffect != null && _model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _model.SetAnimation(0);
                }
                if (CheckPosAgainstCurrent(_detachedPosition))
                {
                    ChangeState(SlenchState.Attach);
                }
                else if (RotateToTarget(_detachedPosition, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2)) // todo: FPS stuff
                {
                    MoveToPosition(_detachedPosition, Fixed.ToFloat(phaseValues.MoveIncrement2) / 2); // todo: FPS stuff
                }
            }
            else if (State == SlenchState.Attach)
            {
                if (RotateToTarget(_detachedFacing, Fixed.ToFloat(phaseValues.AngleIncrement1) / 2) // todo: FPS stuff
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
            else if (State == SlenchState.RollingDone)
            {
                Vector3 pos = _detachedPosition.WithY(_floatBaseY);
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
                    if (_roamTimer == 0 || --_roamTimer != 0)
                    {
                        // below 1/4th of phase health
                        if (_health <= phaseHealth + _healthMax / 12)
                        {
                            SlenchFlags |= SlenchFlags.Wobbling;
                        }
                        if (_subtype == 3)
                        {
                            if (SlenchFlags.TestFlag(SlenchFlags.Rolling))
                            {
                                if (_rollTimer == 0 || --_rollTimer != 0)
                                {
                                    if (_hitFloor)
                                    {
                                        _soundSource.PlaySfx(SfxId.SPIRE_ROLL, loop: true);
                                        if (_floatBaseY == 0)
                                        {
                                            _floatBaseY = (_detachedPosition.Y + Position.Y) / 2;
                                        }
                                        _patternAngle += Fixed.ToFloat(phaseValues.RollingAngleInc) / 2; // todo: FPS stuff
                                        if (_patternAngle >= 360)
                                        {
                                            SlenchFlags ^= SlenchFlags.PatternFlip1;
                                            _patternAngle -= 360;
                                        }
                                        Vector3 vecA;
                                        float angle;
                                        if (SlenchFlags.TestFlag(SlenchFlags.PatternFlip1))
                                        {
                                            vecA = _targetHorizontal * -1;
                                            angle = 360 - _patternAngle;
                                        }
                                        else
                                        {
                                            vecA = _targetHorizontal;
                                            angle = _patternAngle + 180;
                                        }
                                        float factor = Fixed.ToFloat(phaseValues.RollingSpeed);
                                        Vector3 vecB = vecA * factor + _detachedPosition;
                                        (float sin, float cos) = MathF.SinCos(MathHelper.DegreesToRadians(angle));
                                        var mtx = new Matrix3(
                                            cos, 0, -sin,
                                            0, 1, 0,
                                            sin, 0, cos
                                        );
                                        Vector3 newPos = _targetHorizontal * mtx * factor + vecB;
                                        Position = newPos.WithY(Position.Y);
                                        _targetAngle += Fixed.ToFloat(phaseValues.RollingAngleInc) / 2; // todo: FPS stuff
                                        if (_targetAngle < 0)
                                        {
                                            _targetAngle += 360;
                                        }
                                        Vector3 vecC = (Position - vecB).WithY(0).Normalized();
                                        if (SlenchFlags.TestFlag(SlenchFlags.PatternFlip1))
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
                                    else if (_dropSpeed == 0)
                                    {
                                        _hitFloor = true;
                                    }
                                }
                                else // roll timer decremented to zero
                                {
                                    _soundSource.StopSfx(SfxId.SPIRE_ROLL);
                                    ChangeState(SlenchState.RollingDone);
                                }
                            }
                            else // not rolling
                            {
                                _patternAngle += Fixed.ToFloat(phaseValues.FloatingAngleInc) / 2; // todo: FPS stuff
                                if (_patternAngle >= 360)
                                {
                                    SlenchFlags ^= SlenchFlags.PatternFlip1;
                                    _patternAngle -= 360;
                                }
                                float factor = Fixed.ToFloat(phaseValues.FloatingSpeed);
                                float angle;
                                Vector3 vecA;
                                if (SlenchFlags.TestFlag(SlenchFlags.PatternFlip1))
                                {
                                    angle = 540 - _patternAngle;
                                    vecA = new Vector3(
                                        _detachedPosition.X + _targetHorizontal.X * factor,
                                        _floatBaseY,
                                        _detachedPosition.Z + _targetHorizontal.Z * factor
                                    );
                                }
                                else
                                {
                                    angle = _patternAngle;
                                    vecA = new Vector3(
                                        _detachedPosition.X - _targetHorizontal.X * factor,
                                        _floatBaseY,
                                        _detachedPosition.Z - _targetHorizontal.Z * factor
                                    );
                                }
                                Vector3 vecB = Vector3.Cross(_targetHorizontal, Vector3.UnitY).Normalized();
                                var rotMtx = Matrix4.CreateFromAxisAngle(vecB, MathHelper.DegreesToRadians(angle));
                                Position = Matrix.Vec3MultMtx3(_targetHorizontal, rotMtx) * factor + vecA;
                                if (RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement4) / 2)) // todo: FPS stuff
                                {
                                    SlenchFlags |= SlenchFlags.TargetingPlayer;
                                }
                                else
                                {
                                    SlenchFlags &= ~SlenchFlags.TargetingPlayer;
                                }
                                SetUpSlam(playerTarget);
                            }
                        }
                        else // not subtype 3
                        {
                            _patternAngle += Fixed.ToFloat(phaseValues.FloatingAngleInc) / 2; // todo: FPS stuff
                            if (_patternAngle >= 360)
                            {
                                SlenchFlags ^= SlenchFlags.PatternFlip1;
                                _patternAngle -= 360;
                            }
                            Vector3 vecA;
                            float angle = _patternAngle + 180;
                            if (SlenchFlags.TestFlag(SlenchFlags.PatternFlip1))
                            {
                                vecA = Vector3.Cross(_targetHorizontal, Vector3.UnitY).Normalized();
                                angle = 180 - _patternAngle;
                            }
                            else
                            {
                                vecA = Vector3.Cross(Vector3.UnitY, _targetHorizontal).Normalized();
                            }
                            vecA *= Fixed.ToFloat(phaseValues.FloatingSpeed);
                            Vector3 vecB = vecA + _detachedPosition;
                            var rotMtx = Matrix4.CreateFromAxisAngle(_targetHorizontal, MathHelper.DegreesToRadians(angle));
                            vecA = Matrix.Vec3MultMtx3(vecA, rotMtx);
                            Position = vecA + vecB;
                            if (_subtype != 0)
                            {
                                float increment = Fixed.ToFloat(phaseValues.MoveIncrement3) / 2; // todo: FPS stuff
                                _floatBaseY += SlenchFlags.TestFlag(SlenchFlags.PatternFlip2) ? -increment : increment;
                                if (_floatBaseY < 0 || _floatBaseY >= Fixed.ToFloat(phaseValues.RollTime))
                                {
                                    SlenchFlags ^= SlenchFlags.PatternFlip2;
                                }
                                Position += _targetHorizontal * _floatBaseY;
                                if (_subtype == 2)
                                {
                                    SetUpSlam(playerTarget);
                                }
                            }
                            if (!RotateToTarget(playerTarget, Fixed.ToFloat(phaseValues.AngleIncrement4) / 2)) // todo: FPS stuff
                            {
                                SlenchFlags &= ~SlenchFlags.TargetingPlayer;
                            }
                            else
                            {
                                uint rand = Rng.GetRandomInt2(100);
                                if (rand >= 50)
                                {
                                    SlenchFlags &= ~SlenchFlags.TargetingPlayer;
                                }
                                else
                                {
                                    SlenchFlags |= SlenchFlags.TargetingPlayer;
                                }
                            }
                        }
                    }
                    else // roam timer decremented to zero
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
            else if (State == SlenchState.SlamReady)
            {
                _destVec1 = playerTarget;
                Position = _destVec2; // holds position value before any wobbling
                RotateToTarget(_destVec1, Fixed.ToFloat(phaseValues.AngleIncrement5) / 2); // todo: FPS stuff
                int time = 360 / phaseValues.WobbleRotInc * phaseValues.WobbleCycles;
                if (++_wobbleTimer >= time * 2) // todo: FPS stuff
                {
                    ChangeState(SlenchState.Slam);
                }
                else
                {
                    // the game has to convert this to fx32 here
                    _wobbleAngle += phaseValues.WobbleRotInc / 2f; // todo: FPS stuff
                    if (_wobbleAngle >= 360)
                    {
                        _wobbleAngle -= 360;
                    }
                    // max wobble/shake distance decreases as the timer runs out, at 1/2 the proportion
                    float maxDist = Fixed.ToFloat(phaseValues.MaxWobbleDist);
                    float factor = maxDist - _wobbleTimer * maxDist / (time * 2 * 2); // todo: FPS stuff
                    if (factor < 0.01f) // 41
                    {
                        factor = 0.01f;
                    }
                    var rotMtx = Matrix4.CreateFromAxisAngle(facing, MathHelper.DegreesToRadians(_wobbleAngle));
                    Position = Matrix.Vec3MultMtx3(up, rotMtx) * factor + _destVec2;
                }
            }
            else if (State == SlenchState.Slam)
            {
                if (MoveToPosition(_destVec1, Fixed.ToFloat(phaseValues.MoveIncrement4) / 2)) // todo: FPS stuff
                {
                    ChangeState(SlenchState.SlamReturn);
                }
            }
            else if (State == SlenchState.SlamReturn)
            {
                if (MoveToPosition(_destVec2, Fixed.ToFloat(phaseValues.MoveIncrement5) / 2)) // todo: FPS stuff
                {
                    ChangeState((SlenchState)_stateAfterSlam);
                }
            }
            else if (State == SlenchState.Dead)
            {
                if (_deathTimer != 0)
                {
                    _deathTimer--;
                }
                if (_deathTimer == 0)
                {
                    _health = 0;
                    Flags &= ~EnemyFlags.Invincible;
                }
            }
            UpdateFacing();
        }

        private void DropToFloor(float step)
        {
            float max = 1.1f * 30;
            (_dropSpeed, float displacement) = ConstantAcceleration(step, _dropSpeed, -max, max); // -4506, 4506
            if (!CheckCollision(Vector3.UnitY, displacement))
            {
                Position = Position.AddY(displacement);
            }
            else
            {
                _dropSpeed = -_dropSpeed / (SlenchFlags.TestFlag(SlenchFlags.Bouncy) ? 1 : 2);
                if (_dropSpeed > -0.05f * 30 && _dropSpeed < 0.05f * 30) // -205, 205
                {
                    _dropSpeed = 0;
                }
            }
        }

        private bool CheckCollision(Vector3 vec, float dist)
        {
            CollisionResult res = default;
            Vector3 position = Position;
            float factor = dist + _shieldOffset * (dist >= 0 ? 1 : -1);
            if (vec != Vector3.Zero)
            {
                Vector3 dest = vec * factor + position;
                if (CollisionDetection.CheckBetweenPoints(position, dest, TestFlags.None, _scene, ref res))
                {
                    return true;
                }
            }
            if (vec.X != 0 || vec.Z != 0)
            {
                Vector3 dest = vec * factor + position.AddY(_shieldOffset / -2);
                if (CollisionDetection.CheckBetweenPoints(position, dest, TestFlags.None, _scene, ref res))
                {
                    return true;
                }
                dest = vec * factor + position.AddY(_shieldOffset * -1);
                if (CollisionDetection.CheckBetweenPoints(position, dest, TestFlags.None, _scene, ref res))
                {
                    return true;
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
                    Vector3 cylTop = position + vec * dist;
                    if (CollisionDetection.CheckCylinderIntersectPlane(position, cylTop, plane, ref res) && res.Distance < 2)
                    {
                        between = res.Position - door.LockPosition;
                        if (between.LengthSquared < door.RadiusSquared)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void SetRecoilTargetVecs()
        {
            _recoilTimer = 0;
            _destVec2 = Position;
            _destVec1 = (FacingVector * -1).Normalized();
        }

        private void ProcessRecoil()
        {
            if (_recoilTimer >= 10 * 2) // todo: FPS stuff
            {
                return;
            }
            float factor = 1;
            if (_recoilTimer <= 4 * 2 + 1) // todo: FPS stuff
            {
                factor = _recoilLut[_recoilTimer];
            }
            else // 5 - 9
            {
                // the game has a bad macro expansion or something here with a conversion to float,
                // but the end result is just getting diff as fx32 to do the following factor calc
                float diff = 9 * 2 + 1 - _recoilTimer; // todo: FPS stuff
                factor = diff / (5 * 2); // todo: FPS stuff
            }
            Position = _destVec1 * factor + _destVec2;
            _recoilTimer++;
        }

        private static readonly IReadOnlyList<float> _recoilLut = new float[10]
        {
            // 0x666, 0xB33, 0xD9A, 0xE66, 0x1000
            // todo: FPS stuff; plausible intermediate values added
            // original: 0.4f, 0.7f, 0.85f, 0.9f, 1.0f
            0.2f, 0.4f, 0.55f, 0.7f, 0.775f, 0.85f, 0.875f, 0.9f, 0.95f, 1.0f
        };

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

        private bool SetUpSlam(Vector3 target)
        {
            // todo: maybe make a bugfix for the jumping/snapping before slamming?
            Enemy41Values phaseValues = GetPhaseValues();
            if (_slamTimer < phaseValues.SlamDelay * 2) // todo: FPS stuff
            {
                _slamTimer++;
            }
            else
            {
                Vector3 between = target - Position;
                if (between.Length <= Fixed.ToFloat(phaseValues.SlamRange))
                {
                    float radius = _shieldOffset * 0.75f; // 3072
                    target -= Position;
                    float length = target.Length;
                    if (length > radius)
                    {
                        _destVec2 = Position;
                        target = target.Normalized();
                        _destVec1 = target * (length - radius) + Position;
                        _slamTimer = 0;
                        ChangeState(SlenchState.SlamReady);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckPosAgainstCurrent(Vector3 pos)
        {
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

    [Flags]
    public enum SlenchFlags : ushort
    {
        None = 0x0,
        EyeClosed = 0x1,
        Rolling = 0x2,
        Floating = 0x4,
        Bouncy = 0x8, // never set
        PatternFlip1 = 0x10,
        PatternFlip2 = 0x20,
        Detached = 0x40,
        TargetingPlayer = 0x80,
        Vulnerable = 0x100,
        Wobbling = 0x200,
        Unused10 = 0x400,
        Unused11 = 0x800,
        Unused12 = 0x1000,
        Unused13 = 0x2000,
        Unused14 = 0x4000,
        Unused15 = 0x8000
    }

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
        RollingDone = 9,
        Roam = 10,
        SlamReady = 11,
        Slam = 12,
        SlamReturn = 13,
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
        public int RoamTime { get; init; }
        public int MoveIncrement3 { get; init; }
        public int RollTime { get; init; } // also used for the post-V1 floating pattern
        public int FloatingAngleInc { get; init; }
        public int RollingAngleInc { get; init; }
        public int FloatingSpeed { get; init; }
        public int RollingSpeed { get; init; }
        public int AngleIncrement5 { get; init; }
        public int SlamRange { get; init; }
        public int MoveIncrement4 { get; init; }
        public int MoveIncrement5 { get; init; }
        public ushort SlamDelay { get; init; }
        public byte WobbleCycles { get; init; } // number of complete rotation of wobble vector
        public byte WobbleRotInc { get; init; } // angle step to rotate wobble vector
        public int MaxWobbleDist { get; init; } // max wobble/shake distance
        public ushort Magic { get; init; } // 0xBEEF
        public ushort Padding5E { get; init; }
    }
}
