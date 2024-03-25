using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy44Entity : EnemyInstanceEntity
    {
        private readonly Enemy41Entity _slench;
        private ModelInstance _model = null!;
        private int _index = 0;
        private ushort _healthForTurretUpdate = 0;
        private int _timer = 0;

        public Enemy44Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var spawner = data.Spawner as Enemy41Entity;
            Debug.Assert(spawner != null);
            _slench = spawner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Enemy44Values GetValues()
        {
            return Metadata.Enemy44Values[_slench.Subtype * 3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Enemy44Values GetPhaseValues()
        {
            return Metadata.Enemy44Values[_slench.Subtype * 3 + _slench.Phase];
        }

        protected override void EnemyInitialize()
        {
            // the game incorrectly indexes by the synapse field at the same offset as intended indexer (the owner's subtype),
            // which is currently 0, but it doesn't matter since all the scan IDs are 0 in all the Enemy44Values
            _scanId = GetValues().ScanId;
            // phase values are obtained, but the phase is always 0 at this point
            Enemy44Values values = GetPhaseValues();
            _index = _slench.SynapseIndex;
            _boundingRadius = Fixed.ToFloat(values.ColRadius);
            UpdateCollisionVolume(_boundingRadius);
            float angle;
            if (_index == 0)
            {
                // sin = 0, cos = 4096
                // sin = 0, cos = 1
                // 0 rad = 0 deg
                angle = 0;
            }
            else if (_index == 1)
            {
                // sin = 3547, cos = -2048
                // sin = 0.8659668, cos = -0.5
                // 2.0944245 rad = 120.00168 deg
                angle = 120;
            }
            else if (_index == 2)
            {
                // sin = -3547, cos = -2048
                // sin = -0.8659668, cos = -0.5
                // -2.0944245 rad = -120.00168 deg
                angle = -120;
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
            Vector3 facing = _slench.FacingVector;
            Vector3 up = _slench.UpVector;
            var rotMtx = Matrix4.CreateFromAxisAngle(facing, MathHelper.DegreesToRadians(angle));
            up = Matrix.Vec3MultMtx3(up, rotMtx).Normalized();
            SetTransform(facing, up, _slench.Position);
            _health = _healthMax = values.Health;
            _healthForTurretUpdate = _health;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoMaxDistance;
            HealthbarMessageId = 2;
            Metadata.LoadEffectiveness(Metadata.SlenchSynapseEffectiveness[_slench.Subtype], BeamEffectiveness);
            ChangeState(0);
            // default (though the game doesn't have one): if (_scene.RoomId == 35) // UNIT1_B1
            string model = "BigEyeSynapse_01";
            if (_scene.RoomId == 82) // UNIT4_B1
            {
                model = "BigEyeSynapse_02";
            }
            else if (_scene.RoomId == 64) // UNIT2_B2
            {
                model = "BigEyeSynapse_03";
            }
            else if (_scene.RoomId == 76) // UNIT3_B2
            {
                model = "BigEyeSynapse_04";
            }
            _model = SetUpModel(model);
        }

        private void UpdateCollisionVolume(float radius)
        {
            _hurtVolumeInit = new CollisionVolume(new Vector3(0, -5.5f, -3.25f), radius); // -22528, -13312
        }

        public void ChangeState(byte state)
        {
            Enemy44Values values = GetPhaseValues();
            switch (state)
            {
            case 0:
                Flags &= ~EnemyFlags.Visible;
                break;
            case 1:
                Flags |= EnemyFlags.Visible;
                _health = _healthMax = values.Health;
                UpdateCollisionVolume(Fixed.ToFloat(values.ColRadius));
                _model.SetAnimation(0, AnimFlags.NoLoop);
                _soundSource.PlaySfx(SfxId.BIGEYE_SYNAPSE_REGEN_SCR, recency: Single.MaxValue, sourceOnly: true);
                break;
            case 2:
                Flags &= ~EnemyFlags.Invincible;
                _timer = values.HealTimer * 2; // todo: FPS stuff
                _model.SetAnimation(1);
                break;
            case 3:
                Flags |= EnemyFlags.Invincible;
                _model.SetAnimation(2, AnimFlags.NoLoop);
                break;
            case 4:
                Flags |= EnemyFlags.Invincible;
                _model.SetAnimation(4, AnimFlags.NoLoop);
                Vector3 spawnPos = Matrix.Vec3MultMtx4(_hurtVolumeInit.SpherePosition, Transform);
                _scene.SpawnEffect(135, FacingVector, UpVector, spawnPos); // synapseKill
                break;
            case 5:
                Flags &= ~EnemyFlags.Visible;
                Flags |= EnemyFlags.Invincible;
                SetTurretActive(false);
                _timer = values.ReappearTimer * 2; // todo: FPS stuff
                break;
            }
            _state2 = state;
        }

        private void SetTurretActive(bool activate)
        {
            Enemy45Entity? turret = FindTurret();
            if (turret != null)
            {
                Message message = activate ? Message.ActivateTurret : Message.DeactivateTurret;
                _scene.SendMessage(message, this, turret, 0, 0);
            }
        }

        private Enemy45Entity? FindTurret()
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.EnemyInstance
                    && entity is Enemy45Entity turret && turret.Index == _index)
                {
                    return turret;
                }
            }
            return null;
        }

        protected override void EnemyProcess()
        {
            Enemy44Values values = GetPhaseValues();
            if (_state1 == 1)
            {
                if (_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    SetTurretActive(true);
                    ChangeState(2);
                }
            }
            else if (_state1 == 2)
            {
                if (_health < values.Health && _timer != 0)
                {
                    if (--_timer == 0 && ++_health < values.Health)
                    {
                        _timer = values.HealTimer * 2; // todo: FPS stuff
                    }
                }
            }
            else if (_state1 == 3)
            {
                if (_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    // damaged animation done playing after hit (still alive)
                    ChangeState(2);
                }
            }
            else if (_state1 == 4)
            {
                if (_model.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    ChangeState(5);
                }
            }
            else if (_state1 == 5)
            {
                if (_slench.Func2136550() && _timer != 0 && --_timer == 0)
                {
                    ChangeState(1);
                }
            }
            if (_health != _healthForTurretUpdate)
            {
                Enemy45Entity? turret = FindTurret();
                if (turret != null)
                {
                    // todo: FPS stuff, eventually
                    int frameCount = turret.GetMaxFrameCount(); // original max frame count of the turret's animation
                    if (frameCount == 0)
                    {
                        // the game doesn't do this, but it's useful for testing
                        return;
                    }
                    int segments = values.Health / frameCount; // segments of health so one segment = one light on the turret
                    int increment = (_health - _healthForTurretUpdate) / segments; // how many more or less lights the turret needs to enable
                    if (increment != 0)
                    {
                        _healthForTurretUpdate += (ushort)(increment * segments);
                        int param1;
                        Message message;
                        if (increment < 0)
                        {
                            param1 = -increment;
                            message = Message.DecreaseTurretLights;
                        }
                        else
                        {
                            param1 = increment;
                            message = Message.IncreaseTurretLights;
                        }
                        // game doesn't set the sender
                        _scene.SendMessage(message, this, turret, param1, 0);
                    }
                }
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_state1 == 2 || _state2 == 2)
            {
                Effectiveness effectiveness = Effectiveness.Normal;
                if (source?.Type == EntityType.BeamProjectile)
                {
                    var beamSource = (BeamProjectileEntity)source;
                    effectiveness = GetEffectiveness(beamSource.Beam);
                }
                if (effectiveness != Effectiveness.Zero)
                {
                    if (_health != 0)
                    {
                        _soundSource.PlaySfx(SfxId.BIGEYE_OPEN);
                    }
                    else
                    {
                        _soundSource.PlaySfx(SfxId.BIGEYE_SYNAPSE_DIE_SCR);
                    }
                }
                if (_health != 0)
                {
                    ChangeState(3);
                }
                else
                {
                    ChangeState(4);
                }
            }
            if (_health == 0)
            {
                _health = 1;
            }
            return false;
        }
    }

    public readonly struct Enemy44Values
    {
        public ushort ScanId { get; init; }
        public ushort Health { get; init; }
        public ushort HealTimer { get; init; }
        public ushort ReappearTimer { get; init; }
        public int ColRadius { get; init; }
        public ushort Magic { get; init; } // 0xBEEF
        public ushort PaddingE { get; init; }
    }
}
