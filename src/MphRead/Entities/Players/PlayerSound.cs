using System.Diagnostics;

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
            if (id == -1) // sfxtodo: or if sound is paused for jingle
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
            // sfxtodo: don't update if sound is paused for jingle
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

        private void UpdateAltMovemenetSfx()
        {
            float prevAmount = _altMoveSfxAmount;
            float newAmount;
            if (!Flags1.TestFlag(PlayerFlags1.Grounded))
            {
                newAmount = ExponentialDecay(0.5f, prevAmount);
            }
            else if (_scene.FrameCount % 2 == 0) // todo: FPS stuff
            {
                newAmount = 0xFFFF * _hSpeedMag / Fixed.ToFloat(Values.AltMinHSpeed);
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
                newAmount = _altMoveSfxAmount;
            }
            if (newAmount < 1000)
            {
                newAmount = 0;
            }
            _altMoveSfxAmount = newAmount;
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
            if (sfxId != -1)
            {
                _soundSource.PlaySfx(sfxId, loop: true, amountA: newAmount);
            }
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

        public void UpdateSounds()
        {
            if (_damageSfxTimer > 0)
            {
                _damageSfxTimer -= _scene.FrameTime;
                if (_damageSfxTimer < 0)
                {
                    _damageSfxTimer = 0;
                }
            }
        }
    }
}
