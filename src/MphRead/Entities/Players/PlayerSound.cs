using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Sound;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private void PlayHunterSfx(HunterSfx sfx)
        {
            int id = Metadata.HunterSfx[(int)Hunter, (int)sfx];
            if (id == -1)
            {
                if (!_scene.Multiplayer || Hunter != Hunter.Guardian || sfx != HunterSfx.Spawn) // todo: MP1P
                {
                    return;
                }
                id = Metadata.HunterSfx[(int)Hunter.Samus, (int)sfx];
            }
            if (sfx == HunterSfx.Death && IsMainPlayer)
            {
                _soundSource.PlayFreeSfx(id);
            }
            else
            {
                float recency = -1;
                if (sfx == HunterSfx.Damage)
                {
                    recency = 5 / 30f;
                }
                // the game only does this if not Guardian, but the SFX switched to are the same there anyway
                if (!IsMainPlayer)
                {
                    if (sfx == HunterSfx.Damage)
                    {
                        id = Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.DamageEnemy];
                    }
                    else if (sfx == HunterSfx.Death)
                    {
                        id = Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.DeathEnemy];
                    }
                }
                _soundSource.PlaySfx(id, recency: recency, sourceOnly: true);
            }
        }

        private int PlayMissileSfx(HunterSfx sfx)
        {
            int id = Metadata.HunterSfx[(int)Hunter, (int)sfx];
            if (id == -1 || Sfx.TimedSfxMute > 0)
            {
                return -1;
            }
            return _soundSource.PlayFreeSfx(id);
        }

        private float _damageSfxTimer = 0;

        private void PlayRandomDamageSfx()
        {
            Debug.Assert(IsMainPlayer);
            if (Hunter == Hunter.Samus && _damageSfxTimer == 0)
            {
                // 369 - DAMAGE2
                // 370 - DAMAGE3
                // 371 - DAMAGE4
                uint sfx = Rng.GetRandomInt1(3) + 369;
                _soundSource.PlaySfx((int)sfx);
                _damageSfxTimer = 90 / 30f;
            }
        }

        private void PlayBeamEmptySfx(BeamType beam)
        {
            int sfx = Metadata.BeamSfx[(int)beam, (int)BeamSfx.Empty];
            if (sfx != -1)
            {
                _soundSource.PlaySfx(sfx, noUpdate: true);
            }
        }

        private void PlayBeamShotSfx(BeamType beam, bool charged, bool continuous, bool homing, float amountA)
        {
            StopBeamChargeSfx(beam);
            if (continuous)
            {
                amountA = homing ? amountA + 0x3FFF : 0;
                _soundSource.PlaySfx(Metadata.BeamSfx[(int)beam, (int)BeamSfx.Shot], loop: true, amountA: amountA);
                return;
            }
            BeamSfx sfx;
            if (charged)
            {
                sfx = beam == Weapons.AffinityWeapons[(int)Hunter] ? BeamSfx.AffinityChargeShot : BeamSfx.ChargeShot;
            }
            else
            {
                sfx = Hunter == Hunter.Weavel && beam == BeamType.Battlehammer ? BeamSfx.AffinityChargeShot : BeamSfx.Shot;
            }
            int id = Metadata.BeamSfx[(int)beam, (int)sfx];
            if (id != -1)
            {
                _soundSource.PlaySfx(id);
            }
        }

        private int GetBeamChargeSfx(BeamType beam)
        {
            if (beam == BeamType.Missile)
            {
                return Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.MissileCharge];
            }
            if (beam == BeamType.Judicator && Hunter == Hunter.Noxus)
            {
                return (int)SfxId.SHOTGUN_CHARGE1_NOX;
            }
            return Metadata.BeamSfx[(int)beam, (int)BeamSfx.Charge];
        }

        private void PlayBeamChargeSfx(BeamType beam)
        {
            int sfx = GetBeamChargeSfx(beam);
            if (sfx != -1)
            {
                _soundSource.PlaySfx(sfx, loop: true);
            }
        }

        private void StopBeamChargeSfx(BeamType beam)
        {
            int sfx = GetBeamChargeSfx(beam);
            if (sfx != -1)
            {
                _soundSource.StopSfx(sfx);
            }
        }

        public void StopContinuousBeamSfx(BeamType beam)
        {
            int sfx = Metadata.BeamSfx[(int)beam, (int)BeamSfx.Shot];
            _soundSource.StopSfx(sfx);
            sfx = Metadata.BeamSfx[(int)beam, (int)BeamSfx.AffinityChargeShot];
            _soundSource.StopSfx(sfx);
        }

        private int _healthSfxHandle = -1;

        private void UpdateHealthSfx(int health)
        {
            if (Sfx.TimedSfxMute > 0)
            {
                return;
            }
            if (health > 0 && health < 25)
            {
                if (!_soundSource.IsHandlePlaying(_healthSfxHandle))
                {
                    _healthSfxHandle = _soundSource.PlayFreeSfx(SfxId.ENERGY_ALARM);
                }
            }
            else if (_healthSfxHandle != -1)
            {
                _soundSource.StopSfxByHandle(_healthSfxHandle);
                _healthSfxHandle = -1;
            }
        }

        private void UpdateWalkingSfx()
        {
            if (!Flags1.TestFlag(PlayerFlags1.MovingBiped) || _hSpeedMag <= 0)
            {
                _walkSfxTimer = 10 / 30;
                _walkSfxIndex = 0;
                return;
            }
            _walkSfxTimer += _scene.FrameTime;
            int sfxId = -1;
            if (_walkSfxTimer >= 15 / 30f)
            {
                if (_walkSfxIndex == 0)
                {
                    sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Walk1];
                    _walkSfxIndex = 1;
                }
            }
            if (_walkSfxTimer >= 25 / 30f)
            {
                Debug.Assert(_walkSfxIndex == 1);
                sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Walk2];
                _walkSfxTimer = 5 / 30f;
                _walkSfxIndex = 0;
            }
            if (_standTerrain == Terrain.Lava && Hunter != Hunter.Spire)
            {
                sfxId = -1;
            }
            float amountB = Rng.GetRandomInt1(0x7FFF) * 2;
            if (sfxId != -1)
            {
                _soundSource.PlaySfx(sfxId, amountA: 0xFFFF, amountB: amountB);
            }
        }

        private int GetAltMovementSfx()
        {
            int sfxId;
            if (Hunter == Hunter.Samus)
            {
                sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Roll];
            }
            else if (Hunter == Hunter.Trace)
            {
                sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.TraceAlt];
            }
            else
            {
                sfxId = Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.Roll];
            }
            return sfxId;
        }

        private void UpdateAltMovementSfx()
        {
            float newAmount = 0xFFFF * _hSpeedMag / Fixed.ToFloat(Values.AltMinHSpeed);
            UpdateMovementSfxAmount(newAmount);
            int sfxId = GetAltMovementSfx();
            if (sfxId != -1)
            {
                _soundSource.PlaySfx(sfxId, loop: true, amountA: _moveSfxAmount);
            }
        }

        private void UpdateSlidingSfx(float newAmount)
        {
            UpdateMovementSfxAmount(newAmount);
            int sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Slide];
            if (sfxId != -1)
            {
                _soundSource.PlaySfx(sfxId, loop: true, amountA: _moveSfxAmount);
            }
        }

        private void UpdateMovementSfxAmount(float newAmount)
        {
            float prevAmount = _moveSfxAmount;
            if (!Flags1.TestFlag(PlayerFlags1.Grounded))
            {
                newAmount = ExponentialDecay(0.5f, prevAmount);
            }
            else if (_scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                if (newAmount < prevAmount)
                {
                    newAmount = prevAmount + (newAmount - prevAmount) / 4;
                }
                else
                {
                    newAmount = prevAmount + (newAmount - prevAmount) / 2;
                }
            }
            else
            {
                newAmount = _moveSfxAmount;
            }
            if (newAmount < 1000)
            {
                newAmount = 0;
            }
            _moveSfxAmount = newAmount;
        }

        private void StopTerrainSfx(Terrain prevTerrain)
        {
            int curSfx;
            int prevSfx;
            if (Hunter == Hunter.Samus)
            {
                curSfx = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Roll];
                prevSfx = Metadata.TerrainSfx[(int)prevTerrain, (int)TerrainSfx.Roll];
            }
            else if (Hunter == Hunter.Trace)
            {
                curSfx = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.TraceAlt];
                prevSfx = Metadata.TerrainSfx[(int)prevTerrain, (int)TerrainSfx.TraceAlt];
            }
            else
            {
                curSfx = Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.Roll];
                prevSfx = curSfx;
            }
            if (curSfx != prevSfx && prevSfx != -1)
            {
                _soundSource.StopSfx(prevSfx);
            }
            curSfx = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Slide];
            prevSfx = Metadata.TerrainSfx[(int)prevTerrain, (int)TerrainSfx.Slide];
            if (curSfx != prevSfx && prevSfx != -1)
            {
                _soundSource.StopSfx(prevSfx);
            }
        }

        private void StopAltFormSfx()
        {
            if (IsAltForm)
            {
                int sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Slide];
                if (sfxId != -1)
                {
                    _soundSource.StopSfx(sfxId);
                }
            }
            else
            {
                _soundSource.StopSfx(SfxId.NOX_TOP_ATTACK2);
                _soundSource.StopSfx(SfxId.NOX_TOP_ENERGY_DRAIN2);
                int sfxId = GetAltMovementSfx();
                if (sfxId != -1)
                {
                    _soundSource.StopSfx(sfxId);
                }
            }
        }

        private void PlayLandingSfx()
        {
            int sfxId = Metadata.TerrainSfx[(int)_standTerrain, (int)TerrainSfx.Land];
            float amountA = 0xFFFF * _timeBeforeLanding / (90f * 2); // todo: FPS stuff
            _soundSource.PlaySfx(sfxId, amountA: amountA);
        }

        private void UpdateBurningSfx(bool burning)
        {
            float prevAmount = _burnSfxAmount;
            float newAmount = 0xFFFF;
            if (!burning)
            {
                newAmount = ExponentialDecay(0.875f, prevAmount);
                if (newAmount < 50)
                {
                    newAmount = 0;
                }
            }
            if (newAmount > 0)
            {
                _burnSfxAmount = newAmount;
                _soundSource.PlaySfx(SfxId.DGN_LAVA_DAMAGE, loop: true, amountA: newAmount);
            }
            else if (prevAmount > 0)
            {
                _burnSfxAmount = 0;
                _soundSource.StopSfx(SfxId.DGN_LAVA_DAMAGE);
            }
        }

        private static readonly IReadOnlyList<SfxId> _dblDamageIds = new SfxId[3]
        {
            SfxId.DBL_DAMAGE_A, SfxId.DBL_DAMAGE_B, SfxId.DBL_DAMAGE_C
        };

        public bool _dblDamageSfxMuted = false;
        private int _dblDamageSfxHandle = -1;
        private SfxId _dblDamageSfxId = SfxId.None;

        private void UpdateDoubleDamageSfx(int index, bool play)
        {
            if (index != -1)
            {
                if (play)
                {
                    if (_dblDamageSfxHandle != -1)
                    {
                        _soundSource.StopSfxByHandle(_dblDamageSfxHandle);
                        _dblDamageSfxHandle = -1;
                    }
                    _dblDamageSfxId = _dblDamageIds[index];
                }
                else
                {
                    if (_dblDamageSfxHandle != -1)
                    {
                        _soundSource.StopSfxByHandle(_dblDamageSfxHandle);
                    }
                    _dblDamageSfxHandle = -1;
                    _dblDamageSfxId = SfxId.None;
                }
            }
            else if (_dblDamageSfxHandle != -1)
            {
                _soundSource.StopSfxByHandle(_dblDamageSfxHandle);
            }
        }

        private static readonly IReadOnlyList<SfxId> _cloakSfxIds = new SfxId[3]
        {
            SfxId.CLOAK_A, SfxId.CLOAK_B, SfxId.CLOAK_C
        };

        private bool _cloakSfxMuted = false;
        private int _cloakSfxHandle = -1;
        private SfxId _cloakSfxId = SfxId.None;

        private void UpdateCloakSfx(int index, bool play)
        {
            if (index != -1)
            {
                if (play)
                {
                    if (_cloakSfxHandle != -1)
                    {
                        _soundSource.StopSfxByHandle(_cloakSfxHandle);
                        _cloakSfxHandle = -1;
                    }
                    _cloakSfxId = _cloakSfxIds[index];
                }
                else
                {
                    if (_cloakSfxHandle != -1)
                    {
                        _soundSource.StopSfxByHandle(_cloakSfxHandle);
                    }
                    _cloakSfxHandle = -1;
                    _cloakSfxId = SfxId.None;
                }
            }
            else if (_cloakSfxHandle != -1)
            {
                _soundSource.StopSfxByHandle(_cloakSfxHandle);
            }
        }

        private bool _flagCarrySfxOn = false;
        private bool _flagCarrySfxMuted = false;
        private int _flagCarrySfxHandle = -1;

        public void StartFlagCarrySfx()
        {
            _flagCarrySfxOn = true;
        }

        public void StopFlagCarrySfx()
        {
            _soundSource.StopSfxByHandle(_flagCarrySfxHandle);
            _flagCarrySfxHandle = -1;
            _flagCarrySfxOn = false;
        }

        private readonly SoundSource _timedSfxSource = new SoundSource();
        private float _sfxStopTimer = 0;
        public float ForceFieldSfxTimer = 0;
        public float DoorUnlockSfxTimer = 0;
        public float DoorChimeSfxTimer = 0;
        private readonly bool[] _scanSfxOn = new bool[3];
        private readonly int[] _scanSfxHandles = new int[3] { -1, -1, -1 };
        private static readonly IReadOnlyList<SfxId> _scanSfxIds = new SfxId[3]
        {
            SfxId.SCAN_VISOR_ON, SfxId.SCAN_STATUS_BAR, SfxId.SCAN_VISOR_LOOP
        };

        private void UpdateScanSfx(int index, bool enable)
        {
            if (index == -1)
            {
                for (int i = 0; i < _scanSfxOn.Length; i++)
                {
                    if (_scanSfxOn[i])
                    {
                        _soundSource.StopSfxByHandle(_scanSfxHandles[i]);
                        _scanSfxHandles[i] = -1;
                    }
                }
                if (!enable)
                {
                    for (int i = 0; i < _scanSfxOn.Length; i++)
                    {
                        _scanSfxOn[i] = false;
                    }
                }
            }
            else if (enable)
            {
                _scanSfxOn[index] = true;
            }
            else
            {
                _soundSource.StopSfxByHandle(_scanSfxHandles[index]);
                _scanSfxHandles[index] = -1;
                _scanSfxOn[index] = false;
            }
        }

        public void StopTimedSfx()
        {
            if (Sfx.TimedSfxMute == 0)
            {
                // the game stops the double damage and cloak SFX, but we just mute them
                _dblDamageSfxMuted = true;
                _cloakSfxMuted = true;
                if (_flagCarrySfxOn)
                {
                    _flagCarrySfxOn = false;
                    _flagCarrySfxMuted = true;
                }
                UpdateHealthSfx(health: 0);
                // the game also suspends the weapon alarm SFX here
                UpdateScanSfx(index: -1, enable: true);
            }
            Sfx.TimedSfxMute++;
        }

        public void RestartTimedSfx()
        {
            if (--Sfx.TimedSfxMute <= 0)
            {
                _dblDamageSfxMuted = false;
                _cloakSfxMuted = false;
                Sfx.TimedSfxMute = 0;
                if (_flagCarrySfxMuted)
                {
                    _flagCarrySfxMuted = false;
                    _flagCarrySfxOn = true;
                }
                // the game also restarts the weapon alarm SFX here
            }
        }

        public void StopLongSfx()
        {
            StopTimedSfx();
            if (Sfx.LongSfxMute == 0)
            {
                Sfx.SfxMute = true;
                Sfx.Instance.StopEnvironmentSfx();
            }
            Sfx.LongSfxMute++;
        }

        public void RestartLongSfx()
        {
            RestartTimedSfx();
            if (--Sfx.LongSfxMute <= 0)
            {
                Sfx.LongSfxMute = 0;
                // the game does this along with the timed SFX,
                // even though it's the long SFX suppression that sets this true
                Sfx.SfxMute = false;
            }
        }

        public void UpdateTimedSounds()
        {
            _timedSfxSource.Update(Position, rangeIndex: -1);
            if (_sfxStopTimer > 0)
            {
                _sfxStopTimer -= _scene.FrameTime;
                if (_sfxStopTimer <= 0)
                {
                    _sfxStopTimer = 0;
                    StopLongSfx();
                }
            }
            if (_damageSfxTimer > 0)
            {
                _damageSfxTimer -= _scene.FrameTime;
                if (_damageSfxTimer < 0)
                {
                    _damageSfxTimer = 0;
                }
            }
            if (_dblDamageSfxId != SfxId.None && !_soundSource.IsHandlePlaying(_dblDamageSfxHandle) && !_dblDamageSfxMuted)
            {
                _dblDamageSfxHandle = _soundSource.PlayFreeSfx(_dblDamageSfxId);
            }
            if (_cloakSfxId != SfxId.None && !_soundSource.IsHandlePlaying(_cloakSfxHandle) && !_cloakSfxMuted)
            {
                _cloakSfxHandle = _soundSource.PlayFreeSfx(_cloakSfxId);
            }
            if (_flagCarrySfxOn && !_soundSource.IsHandlePlaying(_flagCarrySfxHandle))
            {
                _flagCarrySfxHandle = _soundSource.PlayFreeSfx(SfxId.FLAG_CARRIED);
            }
            // the game plays the unused WEAPON_ALARM alarm SFX here
            for (int i = 0; i < _scene.MessageQueue.Count; i++)
            {
                MessageInfo message = _scene.MessageQueue[i];
                if (message.ExecuteFrame != _scene.FrameCount)
                {
                    continue;
                }
                if (message.Message == Message.PlaySfxScript)
                {
                    int id = (int)message.Param1;
                    if (id == -1)
                    {
                        DoorChimeSfxTimer = 2 / 30f;
                    }
                    else if (id <= 104)
                    {
                        _timedSfxSource.PlaySfx(id | 0x4000, recency: 0, sourceOnly: true);
                    }
                }
                else if (message.Message == Message.UpdateMusic)
                {
                    // mustodo: update music
                }
            }
            // sfxtodo: escape sequence and pause stuff
            if (Sfx.TimedSfxMute == 0)
            {
                for (int i = 0; i < _scanSfxOn.Length; i++)
                {
                    if (_scanSfxOn[i] && _scanSfxHandles[i] == -1)
                    {
                        _scanSfxHandles[i] = _soundSource.PlayFreeSfx(_scanSfxIds[i]);
                    }
                }
            }
            if (Sfx.LongSfxMute == 0 && DoorUnlockSfxTimer > 0)
            {
                DoorUnlockSfxTimer -= _scene.FrameTime;
                if (DoorUnlockSfxTimer <= 1 / 30f)
                {
                    DoorUnlockSfxTimer = 0;
                    if (_soundSource.CountPlayingSfx(SfxId.UNLOCK_ANIM) == 0)
                    {
                        _soundSource.PlayFreeSfx(SfxId.UNLOCK_ANIM);
                    }
                }
            }
            if (DoorChimeSfxTimer > 0)
            {
                DoorChimeSfxTimer -= _scene.FrameTime;
                if (DoorChimeSfxTimer <= 1 / 30f)
                {
                    DoorChimeSfxTimer = 0;
                    if (Sfx.TimedSfxMute == 0 && (CameraSequence.Current == null || !CameraSequence.Current.BlockInput)
                        && _soundSource.CountPlayingSfx(SfxId.DOOR_UNLOCK) == 0)
                    {
                        // the game doesn't check whether the cam seq blocks input
                        _soundSource.PlayFreeSfx(SfxId.DOOR_UNLOCK);
                    }
                }
            }
            if (ForceFieldSfxTimer > 0)
            {
                ForceFieldSfxTimer -= _scene.FrameTime;
                if (ForceFieldSfxTimer <= 0)
                {
                    ForceFieldSfxTimer = 0;
                    if (Sfx.TimedSfxMute == 0)
                    {
                        _timedSfxSource.PlaySfx(SfxId.GEN_OFF, recency: 5 / 30f, sourceOnly: true);
                    }
                }
            }
            // sfxtodo: play/stop scroll SFX
        }
    }
}
