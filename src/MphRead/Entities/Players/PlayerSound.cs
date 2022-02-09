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
                _soundSource.PlaySfx(id);
            }
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
                _soundSource.PlaySfx(sfx, ignoreParams: true);
            }
        }

        private void PlayBeamShotSfx(BeamType beam, bool charged, bool continuous, bool homing, float a3)
        {
            StopBeamChargeSfx(beam);
            if (continuous)
            {
                // sfxtodo: use this value for DGN (set + initial on new, update on existing)
                a3 = homing ? a3 + 0x3FFF : 0;
                _soundSource.PlaySfx(Metadata.BeamSfx[(int)beam, (int)BeamSfx.Shot], single: true);
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
                _soundSource.PlaySfx(sfx, loop: true, single: true);
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
