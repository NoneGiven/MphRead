using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities.Enemies;
using MphRead.Formats;
using MphRead.Formats.Culling;
using MphRead.Sound;
using MphRead.Text;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        public override bool Process()
        {
            bool result = ProcessPlayer();
            SetTransform(_facingVector, _upVector, Position);
            return result;
        }

        public bool ProcessPlayer()
        {
            if (_scene.Multiplayer && !LoadFlags.TestFlag(LoadFlags.Connected) && LoadFlags.TestFlag(LoadFlags.WasConnected))
            {
                LoadFlags |= LoadFlags.Disconnected;
                LoadFlags &= ~LoadFlags.Active;
            }
            if (!LoadFlags.TestFlag(LoadFlags.Active))
            {
                if (_scene.Multiplayer)
                {
                    return false;
                }
                return LoadFlags.TestFlag(LoadFlags.SlotActive);
            }
            // todo?: something with wifi lockjaw bomb
            if (Flags2.TestFlag(PlayerFlags2.UnequipOmegaCannon))
            {
                UnequipOmegaCannon();
                Flags2 &= ~PlayerFlags2.UnequipOmegaCannon;
            }
            if (IsBot)
            {
                // todo: bot stuff
            }
            // display swap update happens here for main player
            if (IsMainPlayer && CameraSequence.Current?.BlockInput == true)
            {
                Controls.ClearAll();
            }
            PrevPosition = Position;
            PrevSpeed = Speed;
            Flags1 &= ~PlayerFlags1.AltFormPrevious;
            if (Flags1.TestFlag(PlayerFlags1.AltForm))
            {
                Flags1 |= PlayerFlags1.AltFormPrevious;
            }
            Flags1 &= ~PlayerFlags1.MovingBiped;
            Flags1 &= ~PlayerFlags1.ShotCharged;
            Flags1 &= ~PlayerFlags1.ShotMissile;
            Flags1 &= ~PlayerFlags1.ShotUncharged;
            _crushBits = 0;
            if (_respawnTimer > 0)
            {
                _respawnTimer--;
                if ((_scene.GameMode == GameMode.Survival || _scene.GameMode == GameMode.SurvivalTeams)
                    && GameState.TeamDeaths[SlotIndex] > GameState.PointGoal)
                {
                    if (IsMainPlayer)
                    {
                        QueueHudMessage(128, 152, 1 / 1000f, 0, 243); // you lost all your lives! you're out of the game
                    }
                    if (_respawnTimer == 0)
                    {
                        _respawnTimer = 1;
                    }
                }
            }
            if (_health == 0)
            {
                if (_respawnTimer == 0 && EnemySpawner == null)
                {
                    if (_scene.Room?.LoadEntityId >= 0)
                    {
                        TeleporterEntity? targetTeleporter = null;
                        for (int i = 0; i < _scene.Entities.Count; i++)
                        {
                            EntityBase entity = _scene.Entities[i];
                            if (entity.Type == EntityType.Teleporter)
                            {
                                var teleporter = (TeleporterEntity)entity;
                                if (teleporter.Data.LoadIndex == _scene.Room.LoadEntityId)
                                {
                                    targetTeleporter = teleporter;
                                    break;
                                }
                            }
                        }
                        if (targetTeleporter != null)
                        {
                            targetTeleporter.SetTriggered();
                            Spawn(targetTeleporter.Position, targetTeleporter.FacingVector,
                                targetTeleporter.UpVector, targetTeleporter.NodeRef, respawn: true);
                            if (GameState.TransitionAltForm)
                            {
                                TrySwitchForms(force: true);
                                UpdateForm(altForm: true);
                                // unstick Halfturret after teleporting (the game doesn't do this)
                                if (Halfturret.Health > 0)
                                {
                                    Halfturret.ResetGroundedState();
                                }
                                ResumeOwnCamera();
                                HudOnMorphStart();
                            }
                        }
                        else
                        {
                            DoorEntity? targetDoor = null;
                            for (int i = 0; i < _scene.Entities.Count; i++)
                            {
                                EntityBase entity = _scene.Entities[i];
                                if (entity.Type == EntityType.Door)
                                {
                                    var door = (DoorEntity)entity;
                                    if (door.Data.OutConnectorId == _scene.Room.LoadEntityId)
                                    {
                                        targetDoor = door;
                                        break;
                                    }
                                }
                            }
                            if (targetDoor != null)
                            {
                                Vector3 facing = targetDoor.FacingVector;
                                Vector3 position = targetDoor.Position + facing * 2;
                                Spawn(position, facing, targetDoor.UpVector, targetDoor.NodeRef, respawn: true);
                            }
                        }
                        _scene.Room.LoadEntityId = -1;
                        GameState.TransitionAltForm = false;
                    }
                    else
                    {
                        int time = GetTimeUntilRespawn();
                        if (IsMainPlayer && _scene.Multiplayer) // todo: and some global is not set
                        {
                            // press FIRE to begin / press FIRE to respawn
                            int messageId = CameraSequence.Current?.IsIntro == true ? 245 : 244;
                            if (!Bugfixes.NoStrayRespawnText || time > 0
                                || _scene.GameMode != GameMode.Survival && _scene.GameMode != GameMode.SurvivalTeams)
                            {
                                QueueHudMessage(128, 162, 1 / 1000f, 0, messageId);
                                if (time < 150 * 2) // todo: FPS stuff
                                {
                                    string message = Text.Strings.GetHudMessage(246); // SPAWNING IN %d...
                                    int seconds = (time + 30 * 2) / (30 * 2); // todo: FPS stuff
                                    QueueHudMessage(128, 152, 1 / 1000f, 0, message.Replace("%d", seconds.ToString()));
                                }
                            }
                        }
                        if (!_scene.Multiplayer || Controls.Shoot.IsDown || time <= 0 || IsBot) // todo: or forced
                        {
                            // todo?: something with wi-fi
                            // else...
                            PlayerSpawnEntity? respawn = GetRespawnPoint();
                            if (respawn != null)
                            {
                                Vector3 position = ForcedSpawnPos ?? respawn.Position;
                                Spawn(position, respawn.FacingVector, respawn.UpVector, respawn.NodeRef, respawn: true);
                            }
                        }
                    }
                }
            }
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
            if (IsMainPlayer && !IsAltForm)
            {
                _soundSource.Update(Position, rangeIndex: -1);
            }
            else
            {
                int rangeIndex = 1;
                if (!_scene.Multiplayer && Hunter == Hunter.Guardian) // todo: MP1P
                {
                    rangeIndex = 21;
                }
                _soundSource.Update(Position, rangeIndex);
            }
            if (IsMainPlayer)
            {
                UpdateHealthSfx(_health);
            }
            else
            {
                UpdateNodeRefVolume();
            }
            if (_damageInvulnTimer > 0)
            {
                _damageInvulnTimer--;
            }
            if (_spawnInvulnTimer > 0)
            {
                _spawnInvulnTimer--;
            }
            if (_disruptedTimer > 0)
            {
                _disruptedTimer--;
            }
            if (_scene.GameMode == GameMode.Survival || _scene.GameMode == GameMode.SurvivalTeams)
            {
                if (Flags2.TestFlag(PlayerFlags2.RadarReveal))
                {
                    Flags2 |= PlayerFlags2.RadarRevealPrevious;
                }
                Flags2 &= ~PlayerFlags2.RadarReveal;
                if (_health == 0)
                {
                    _hidingTimer = 0;
                }
                else
                {
                    if (GameState.RadarPlayers)
                    {
                        if (IsMainPlayer)
                        {
                            QueueHudMessage(128, 170, 1 / 1000f, 0, 247); // FACE OFF!
                        }
                        _hidingTimer = 0;
                    }
                    else
                    {
                        int revealTime = (PlayerCount > 2 ? 600 : 300) * 2; // todo: FPS stuff
                        Vector3 moved = Position - IdlePosition;
                        if (moved.LengthSquared >= 25)
                        {
                            // todo: FPS stuff
                            _hidingTimer = (ushort)(_hidingTimer > 35 * 2 ? _hidingTimer - 35 * 2 : 0);
                            if (_hidingTimer < revealTime && _hidingTimer > revealTime - 35 * 2)
                            {
                                // give the player at least a second before they're revealed again
                                _hidingTimer = (ushort)(revealTime - 35 * 2);
                            }
                        }
                        if (_hidingTimer < revealTime + 150 * 2) // todo: FPS stuff
                        {
                            _hidingTimer++;
                        }
                        if (_hidingTimer >= revealTime)
                        {
                            Flags2 |= PlayerFlags2.RadarReveal;
                            if (IsMainPlayer && (_scene.FrameCount & (8 * 2)) == 0) // todo: FPS stuff
                            {
                                QueueHudMessage(128, 150, 1 / 1000f, 0, 248); // position revealed!
                                QueueHudMessage(128, 160, 1 / 1000f, 0, 249); // RETURN TO BATTLE!
                            }
                        }
                    }
                }
            }
            if (_timeSinceHitTarget != UInt16.MaxValue && ++_timeSinceHitTarget > 1)
            {
                _shockCoilTimer = 0;
                _shockCoilTarget = null;
                if (_timeSinceHitTarget >= 210 * 2) // todo: FPS stuff
                {
                    _lastTarget = null;
                }
            }
            if (_timeSinceShot != UInt16.MaxValue)
            {
                _timeSinceShot++;
            }
            if (_altAttackCooldown > 0)
            {
                _altAttackCooldown--;
            }
            if (_jumpPadControlLock > 0)
            {
                _jumpPadControlLock--;
            }
            if (_jumpPadControlLock == 0)
            {
                _lastJumpPad = null;
            }
            if (_jumpPadControlLockMin > 0)
            {
                _jumpPadControlLockMin--;
            }
            if (_timeSinceJumpPad != UInt16.MaxValue)
            {
                _timeSinceJumpPad++;
            }
            if (Hunter == Hunter.Samus)
            {
                if (_bombRefillTimer > 0)
                {
                    _bombRefillTimer--;
                }
                else
                {
                    _bombAmmo = 3;
                }
            }
            else if (Hunter == Hunter.Sylux)
            {
                if (_bombCooldown > 0)
                {
                    _bombAmmo = 0;
                }
                else
                {
                    _bombAmmo = (byte)(3 - SyluxBombCount);
                }
            }
            else
            {
                _bombAmmo = 1;
            }
            if (_bombCooldown > 0)
            {
                _bombCooldown--;
                if (Hunter == Hunter.Kanden && _bombCooldown == 10 * 2) // todo: FPS stuff
                {
                    _altModel.SetAnimation((int)KandenAltAnim.TailIn, AnimFlags.NoLoop);
                }
            }
            // todo?: FH leftover ammo recharge stuff
            if (_timeSinceMorphCamera != UInt16.MaxValue)
            {
                _timeSinceMorphCamera++;
            }
            if (Hunter == Hunter.Sylux && _bombOveruse > 0)
            {
                _bombOveruse--;
            }
            if (_deathaltTimer > 0)
            {
                _deathaltTimer--;
                if (_deathaltEffect == null)
                {
                    int effectId = 181; // deathBall
                    _deathaltEffect = _scene.SpawnEffectGetEntry(effectId, Vector3.UnitX, Vector3.UnitY, _volume.SpherePosition);
                    _deathaltEffect?.SetElementExtension(true);
                }
                else
                {
                    _deathaltEffect.Transform(Vector3.UnitY, Vector3.UnitX, _volume.SpherePosition);
                }
            }
            else if (_deathaltEffect != null)
            {
                _scene.UnlinkEffectEntry(_deathaltEffect);
                _deathaltEffect = null;
            }
            if (Flags2.TestFlag(PlayerFlags2.Cloaking))
            {
                Debug.Assert(_cloakTimer != 0);
                _cloakTimer--;
                if (_cloakTimer > 0)
                {
                    _targetAlpha = 3 / 31f;
                    if (IsMainPlayer)
                    {
                        if (_cloakTimer == 210 * 2) // todo: FPS stuff
                        {
                            UpdateCloakSfx(index: 1, play: true);
                        }
                        else if (_cloakTimer == 120 * 2) // todo: FPS stuff
                        {
                            UpdateCloakSfx(index: 2, play: true);
                        }
                    }
                }
                else
                {
                    Flags2 &= ~PlayerFlags2.Cloaking;
                    _targetAlpha = 1;
                    _soundSource.PlayFreeSfx(SfxId.CLOAK_OFF);
                    if (IsMainPlayer)
                    {
                        UpdateCloakSfx(index: 0, play: false);
                    }
                }
            }
            else
            {
                _targetAlpha = 1;
                if ((Hunter == Hunter.Trace || IsPrimeHunter) && _hSpeedMag < 0.05f && Speed.Y < 0.05f && Speed.Y > -0.05f)
                {
                    if (_cloakTimer >= 30 * 2) // todo: FPS stuff
                    {
                        if (Hunter == Hunter.Trace && IsAltForm)
                        {
                            // todo: set to 5/31 if recent touch input
                            _targetAlpha = 1 / 31f;
                        }
                        else if (CurrentWeapon == BeamType.Imperialist)
                        {
                            _targetAlpha = 5 / 31f;
                        }
                    }
                    else
                    {
                        _cloakTimer++;
                    }
                }
                else
                {
                    _cloakTimer = 0;
                }
            }
            // todo: if bot stuff, set _targetAlpha to 0
            if (_health > 0)
            {
                if (Flags2.TestFlag(PlayerFlags2.Cloaking) || !Flags2.TestFlag(PlayerFlags2.AltAttack))
                {
                    if (_curAlpha < _targetAlpha)
                    {
                        _curAlpha += 2 / 31f / 2; // todo: FPS stuff
                        if (_curAlpha > _targetAlpha)
                        {
                            _curAlpha = _targetAlpha;
                        }
                    }
                    else if (_curAlpha > _targetAlpha)
                    {
                        _curAlpha -= 1 / 31f / 2; // todo: FPS stuff
                        if (_curAlpha < _targetAlpha)
                        {
                            _curAlpha = _targetAlpha;
                        }
                    }
                }
                else
                {
                    _cloakTimer = 0;
                    _curAlpha = 1;
                    _targetAlpha = 1;
                }
            }
            else if (IsAltForm || IsMorphing)
            {
                _curAlpha -= 2 / 31f / 2; // todo FPS stuff
                if (_curAlpha < 0)
                {
                    _curAlpha = 0;
                }
            }
            int ammo = EquipInfo.GetAmmo?.Invoke() ?? -1;
            if (ammo >= 0 && ammo < EquipInfo.Weapon.AmmoCost)
            {
                int slot = 0;
                int priority = 0;
                for (int i = 0; i < 3; i++)
                {
                    BeamType slotWeap = _weaponSlots[i];
                    if (slotWeap != BeamType.None)
                    {
                        WeaponInfo slotInfo = Weapons.Current[(int)slotWeap];
                        if (slotInfo.Priority > priority && ammo >= slotInfo.AmmoCost)
                        {
                            priority = slotInfo.Priority;
                            slot = i;
                        }
                    }
                }
                if (IsMainPlayer)
                {
                    ShowNoAmmoMessage();
                }
                TryEquipWeapon(_weaponSlots[slot]);
            }
            ProcessInput();
            if (Flags1.TestFlag(PlayerFlags1.Boosting) && _hSpeedMag <= Fixed.ToFloat(Values.AltMinHSpeed))
            {
                Flags1 &= ~PlayerFlags1.Boosting;
            }
            if (Flags1.TestFlag(PlayerFlags1.Walking))
            {
                _gunViewBob += 14 / 2f; // todo: FPS stuff
                if (_gunViewBob > 450)
                {
                    _gunViewBob -= 180;
                }
                if (_walkViewBob < Fixed.ToFloat(Values.WalkBobMax))
                {
                    _walkViewBob += 1 / 2f; // todo: FPS stuff
                    if (_walkViewBob > Fixed.ToFloat(Values.WalkBobMax))
                    {
                        _walkViewBob = Fixed.ToFloat(Values.WalkBobMax);
                    }
                }
            }
            else
            {
                if (_walkViewBob > 0)
                {
                    _walkViewBob -= Fixed.ToFloat(Values.WalkBobMax) / 32 / 2f; // todo: FPS stuff
                    if (_walkViewBob < 0)
                    {
                        _walkViewBob = 0;
                    }
                }
                if (_gunViewBob >= 360)
                {
                    _gunViewBob += 14 / 2f; // todo: FPS stuff
                    if (_gunViewBob > 450)
                    {
                        _gunViewBob = 450;
                    }
                }
                else
                {
                    _gunViewBob -= 14 / 2f; // todo: FPS stuff
                    if (_gunViewBob < 270)
                    {
                        _gunViewBob = 270;
                    }
                }
            }
            // todo: FPS stuff
            if (_healthRecovery > 0)
            {
                if (_tickedHealthRecovery)
                {
                    _tickedHealthRecovery = false;
                }
                else
                {
                    if (_healthRecovery <= 3)
                    {
                        _health += _healthRecovery;
                        _healthRecovery = 0;
                    }
                    else
                    {
                        _health += 3;
                        _healthRecovery -= 3;
                        _scrollSfxTimer = 2 / 30f;
                    }
                    if (_health > _healthMax)
                    {
                        _health = _healthMax;
                    }
                }
            }
            else
            {
                _tickedHealthRecovery = false;
            }
            for (int i = 0; i < 2; i++)
            {
                if (_ammoRecovery[i] > 0)
                {
                    if (_tickedAmmoRecovery[i])
                    {
                        _tickedAmmoRecovery[i] = false;
                    }
                    else
                    {
                        if (_ammoRecovery[i] <= 3)
                        {
                            // the game stops the scroll SFX here, which is unnecessary
                            _ammo[i] += _ammoRecovery[i];
                            _ammoRecovery[i] = 0;
                        }
                        else
                        {
                            _ammo[i] += 3;
                            _ammoRecovery[i] -= 3;
                            _scrollSfxTimer = 2 / 30f;
                        }
                        if (_ammo[i] > _ammoMax[i])
                        {
                            _ammo[i] = _ammoMax[i];
                        }
                    }
                }
                else
                {
                    _tickedAmmoRecovery[i] = false;
                }
            }
            if (_health > 0)
            {
                if (Flags1.TestFlag(PlayerFlags1.Grounded) && !Flags1.TestFlag(PlayerFlags1.GroundedPrevious))
                {
                    PlayLandingSfx();
                }
                if (IsAltForm)
                {
                    UpdateAltMovementSfx();
                }
            }
            UpdateGunAnimation();
            UpdateAnimFrames(_gunModel);
            if (IsMainPlayer && _gunModel.AnimInfo.Frame[0] == 15 && _scene.FrameCount % 2 == 0 // todo: FPS stuff
                && (GunAnimation == GunAnimation.Unknown9 || GunAnimation == GunAnimation.MissileShot))
            {
                _soundSource.StopSfxByHandle(_missileSfxHandle);
                _missileSfxHandle = PlayMissileSfx(HunterSfx.MissileOpen);
            }
            PickUpItems();
            if (!IsAltForm)
            {
                UpdateAimVecs();
            }
            if (_muzzleEffect != null)
            {
                int id = Metadata.MuzzleEffectIds[(int)BeamType.ShockCoil];
                if (_muzzleEffect.IsFinished || _muzzleEffect.EffectId == id && !Flags1.TestFlag(PlayerFlags1.ShotUncharged))
                {
                    _scene.UnlinkEffectEntry(_muzzleEffect);
                    _muzzleEffect = null;
                }
                else if (IsMainPlayer)
                {
                    _muzzleEffect.Transform(_gunVec2, _gunVec1, _muzzlePos);
                }
            }
            if (EquipInfo.ChargeLevel < EquipInfo.Weapon.MinCharge * 2) // todo: FPS stuff
            {
                if (_chargeEffect != null)
                {
                    _scene.UnlinkEffectEntry(_chargeEffect);
                    _chargeEffect = null;
                }
            }
            else
            {
                if (EquipInfo.ChargeLevel == EquipInfo.Weapon.MinCharge * 2) // todo: FPS stuff
                {
                    if (_chargeEffect != null)
                    {
                        _scene.UnlinkEffectEntry(_chargeEffect);
                        _chargeEffect = null;
                    }
                    int effectId = Metadata.ChargeEffectIds[(int)CurrentWeapon];
                    _chargeEffect = _scene.SpawnEffectGetEntry(effectId, _gunVec2, _gunVec1, _muzzlePos);
                    if (!IsMainPlayer && _chargeEffect != null)
                    {
                        _chargeEffect.SetDrawEnabled(false);
                    }
                    Flags2 &= ~PlayerFlags2.ChargeEffect;
                }
                else if (EquipInfo.ChargeLevel == EquipInfo.Weapon.FullCharge * 2) // todo: FPS stuff
                {
                    if (!Flags2.TestFlag(PlayerFlags2.ChargeEffect))
                    {
                        if (_chargeEffect != null)
                        {
                            _scene.UnlinkEffectEntry(_chargeEffect);
                            _chargeEffect = null;
                        }
                        int effectId = Metadata.ChargeLoopEffectIds[(int)CurrentWeapon];
                        _chargeEffect = _scene.SpawnEffectGetEntry(effectId, _gunVec2, _gunVec1, _muzzlePos);
                        if (_chargeEffect != null)
                        {
                            _chargeEffect.SetElementExtension(true);
                            Flags2 |= PlayerFlags2.ChargeEffect;
                            if (!IsMainPlayer)
                            {
                                _chargeEffect.SetDrawEnabled(false);
                            }
                        }
                    }
                    CameraInfo.SetShake(0.023f);
                }
                if (IsMainPlayer && _chargeEffect != null)
                {
                    _chargeEffect.Transform(_gunVec2, _gunVec1, _muzzlePos);
                }
            }
            if (_frozenTimer == 0)
            {
                UpdateAnimFrames(_bipedModel1);
                UpdateAnimFrames(_bipedModel2);
            }
            if ((IsAltForm || IsMorphing) && _frozenTimer == 0)
            {
                UpdateAnimFrames(_altModel);
            }
            if (_boostEffect != null)
            {
                _boostEffect.Transform(_gunVec2, _facingVector, Position);
                if (!IsAltForm && !IsMorphing)
                {
                    _scene.UnlinkEffectEntry(_boostEffect);
                    _boostEffect = null;
                }
                else if (!Flags1.TestFlag(PlayerFlags1.Boosting))
                {
                    if (_boostEffect.IsFinished)
                    {
                        _scene.UnlinkEffectEntry(_boostEffect);
                        _boostEffect = null;
                    }
                    else
                    {
                        _boostEffect.SetElementExtension(false);
                    }
                }
            }
            if (_furlEffect != null)
            {
                _furlEffect.Transform(_gunVec2, _facingVector, Position);
                if (!IsAltForm && !IsMorphing || _furlEffect.IsFinished)
                {
                    _scene.UnlinkEffectEntry(_furlEffect);
                    _furlEffect = null;
                }
            }
            if (Hunter == Hunter.Samus)
            {
                UpdateMorphBallTrail();
            }
            if (_timeSinceDamage != UInt16.MaxValue)
            {
                _timeSinceDamage++;
            }
            if (_timeSincePickup != UInt16.MaxValue)
            {
                _timeSincePickup++;
            }
            if (_timeSinceHeal != UInt16.MaxValue)
            {
                _timeSinceHeal++;
            }
            if (!Flags1.TestFlag(PlayerFlags1.Standing) && _timeSinceStanding != UInt16.MaxValue)
            {
                _timeSinceStanding++;
            }
            if (_field449 != UInt16.MaxValue)
            {
                _field449++;
            }
            // todo: check input
            _timeSinceInput = 0;
            if (_aimY < 60 && _aimY > -60 && !EquipInfo.Zoomed && _health > 0)
            {
                if (_timeSinceInput == Values.SwayStartTime * 2) // todo: FPS stuff
                {
                    _field40C = 0;
                    float factor1 = (Rng.GetRandomInt2(Values.SwayLimit) - Values.SwayLimit / 2) / 4096f;
                    float factor2 = (Rng.GetRandomInt2(Values.SwayLimit) - Values.SwayLimit / 2) / 4096f;
                    _field410 = _facingVector;
                    _field41C = _field410;
                    _field428 = _field410;
                    _field41C += _gunVec2 * factor1 + _upVector * factor2;
                }
                else if (_timeSinceInput > Values.SwayStartTime * 2) // todo: FPS stuff
                {
                    _field40C += 1 / (Values.SwayIncrement / 4096f) / 2; // todo: FPS stuff
                    if (_field40C >= 1)
                    {
                        _field40C = 0;
                        float factor1 = (Rng.GetRandomInt2(Values.SwayLimit) - Values.SwayLimit / 2) / 4096f;
                        float factor2 = (Rng.GetRandomInt2(Values.SwayLimit) - Values.SwayLimit / 2) / 4096f;
                        _field410 = _field41C;
                        _field41C = _field428;
                        _field41C += _gunVec2 * factor1 + _upVector * factor2;
                    }
                    float angle = 180 * _field40C + 180;
                    float factor = (MathF.Cos(MathHelper.DegreesToRadians(angle)) + 1) / 2;
                    _facingVector = _field410 + (_field41C - _field410) * factor;
                    _facingVector = _facingVector.Normalized();
                }
            }
            if (AttachedEnemy != null && !IsAltForm && (_bipedModel2.AnimInfo.Index[0] != (int)PlayerAnimation.Unmorph
                || _bipedModel2.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended)))
            {
                if (AttachedEnemy.EnemyType == EnemyType.Temroid)
                {
                    ((Enemy02Entity)AttachedEnemy).UpdateAttached(this);
                }
                else if (AttachedEnemy.EnemyType == EnemyType.Quadtroid)
                {
                    ((Enemy37Entity)AttachedEnemy).UpdateAttached(this);
                }
            }
            if (EquipInfo.SmokeLevel < EquipInfo.Weapon.SmokeStart * 2) // todo: FPS stuff
            {
                if (EquipInfo.SmokeLevel > EquipInfo.Weapon.SmokeMinimum * 2) // todo: FPS stuff
                {
                    if (Flags1.TestFlag(PlayerFlags1.DrawGunSmoke) && _smokeAlpha < 1)
                    {
                        _smokeAlpha = Math.Min(_smokeAlpha + 1 / 31f / 2, 1); // todo: FPS stuff
                    }
                }
                else if (_smokeAlpha > 0)
                {
                    _smokeAlpha = Math.Max(_smokeAlpha - 1 / 31f / 2, 0); // todo: FPS stuff
                }
                else if (Flags1.TestFlag(PlayerFlags1.DrawGunSmoke))
                {
                    EquipInfo.SmokeLevel = 0;
                    Flags1 &= ~PlayerFlags1.DrawGunSmoke;
                }
            }
            else
            {
                if (!Flags1.TestFlag(PlayerFlags1.DrawGunSmoke))
                {
                    Flags1 |= PlayerFlags1.DrawGunSmoke;
                    _gunSmokeModel.SetAnimation(0);
                }
                if (_smokeAlpha < 1)
                {
                    _smokeAlpha = Math.Min(_smokeAlpha + 1 / 31f / 2, 1); // todo: FPS stuff
                }
            }
            if (Flags1.TestFlag(PlayerFlags1.DrawGunSmoke))
            {
                UpdateAnimFrames(_gunSmokeModel);
            }
            if (_health == 0)
            {
                if (_timeSinceDead != UInt16.MaxValue)
                {
                    _timeSinceDead++;
                }
                if (!_scene.Multiplayer && IsMainPlayer && _deathCountdown > 0)
                {
                    _deathCountdown -= _scene.FrameTime;
                    float pct = (150 / 30f - _deathCountdown) / (150 / 30f);
                    CameraInfo.SetShake(0.15f * pct);
                    if (_lostOctolithEnemyIndex != -1 && _deathCountdown <= 119 / 30f)
                    {
                        if (!_deathLostOctolithSfxPlayed)
                        {
                            // the game does this in the draw function with 119 ticks left
                            _soundSource.PlayFreeSfx(SfxId.DIE_LOSE_CRYSTAL);
                            _deathLostOctolithSfxPlayed = true;
                        }
                        if (_deathCountdown <= 90 / 30f)
                        {
                            // the game does this in the process frame function with 90 ticks left
                            if (!_deathLostOctolithDialogShown)
                            {
                                // HUNTER HAS TAKEN AN OCTOLITH
                                ShowDialog(DialogType.Hud, messageId: 117, param1: 90, param2: 1);
                                _deathLostOctolithDialogShown = true;
                            }
                        }
                        else if (_deathCountdown >= 117 / 30f)
                        {
                            // this is done between 119 and 117, so we don't need a boolean to simulate calling on a specific frame
                            CameraInfo.SetShake(0.4f);
                        }
                        (_lostOctolithSpeed, float displacement) = Drag(0.88f, _lostOctolithSpeed);
                        if (!IsAltForm)
                        {
                            _lostOctolithDrawPos = _lostOctolithDrawPos
                                .AddX(_field70 * displacement).AddZ(_field74 * displacement);
                        }
                        else
                        {
                            _lostOctolithDrawPos = _lostOctolithDrawPos
                                .AddX(-_field80 * displacement).AddZ(-_field84 * displacement);
                        }
                    }
                    if (!IsAltForm)
                    {
                        _facingVector.Y = ExponentialDecay(0.9f, _facingVector.Y);
                        _facingVector = _facingVector.Normalized();
                        _gunVec1 = _facingVector;
                    }
                    if (_deathCountdown <= 1 / 30f && !_deathProcessed)
                    {
                        _deathProcessed = true;
                        Flags2 |= PlayerFlags2.HideModel;
                        // make the visor layer show up
                        Flags1 &= ~PlayerFlags1.AltForm;
                        Flags1 &= ~PlayerFlags1.Morphing;
                        Flags1 &= ~PlayerFlags1.Unmorphing;
                        if (_lostOctolithEnemyIndex != -1)
                        {
                            int octolithCount = System.Numerics.BitOperations.PopCount(GameState.StorySave.CurrentOctoliths);
                            uint lostNum = Rng.GetRandomInt2(octolithCount);
                            uint curNum = 0;
                            for (int i = 0; i < 8; i++)
                            {
                                if ((GameState.StorySave.CurrentOctoliths & (1 << i)) != 0)
                                {
                                    if (curNum == lostNum)
                                    {
                                        int hunter = (int)_players[_lostOctolithEnemyIndex].Hunter;
                                        GameState.StorySave.CurrentOctoliths &= (ushort)~(1 << i);
                                        GameState.StorySave.LostOctoliths = GameState.StorySave.LostOctoliths
                                            & (uint)(~(15 << (4 * i)) | (hunter << (4 * i)));
                                        GameState.StorySave.AreaHunters[_scene.AreaId / 2] &= (byte)~(1 << hunter);
                                        break;
                                    }
                                    curNum++;
                                }
                            }
                        }
                    }
                }
                else if (_respawnTimer <= 1)
                {
                    Flags2 |= PlayerFlags2.HideModel;
                }
            }
            if (!EquipInfo.Zoomed)
            {
                float currentFov = CameraInfo.Fov;
                float normalFov = Fixed.ToFloat(Values.NormalFov) * 2;
                float diff = normalFov - currentFov;
                if (MathF.Abs(diff) >= 0.1f * 2)
                {
                    currentFov += diff / 4; // todo: FPS stuff (balances out here)
                    CameraInfo.Fov = currentFov;
                }
                else
                {
                    CameraInfo.Fov = normalFov;
                }
            }
            if (_bipedModel2.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                if (IsMorphing)
                {
                    Flags1 &= ~PlayerFlags1.Morphing;
                    UpdateForm(altForm: true);
                }
                else if (IsUnmorphing)
                {
                    if (IsMainPlayer && CameraSequence.Current != null)
                    {
                        CameraSequence.Current.InitialCamInfo.NodeRef = NodeRef;
                    }
                    else
                    {
                        CameraInfo.NodeRef = NodeRef;
                    }
                    Flags1 &= ~PlayerFlags1.Unmorphing;
                    if (_burnTimer > 0)
                    {
                        CreateBurnEffect();
                    }
                }
            }
            UpdateLightSources(_volume.SpherePosition);
            // todo?: if wifi and not main player
            // else...
            if (NodeRef != NodeRef.None)
            {
                int index = Flags1.TestFlag(PlayerFlags1.AltFormPrevious) ? 2 : 0;
                Vector3 prevPos = PrevPosition + PlayerVolumes[(int)Hunter, index].SpherePosition;
                index = IsAltForm ? 2 : 0;
                Vector3 curPos = Position + PlayerVolumes[(int)Hunter, index].SpherePosition;
                NodeRef = _scene.UpdateNodeRef(NodeRef, prevPos, curPos);
                if (CameraSequence.Current == null || !IsMainPlayer)
                {
                    if (CameraType == CameraType.Free)
                    {
                        CameraInfo.NodeRef = _scene.UpdateNodeRef(CameraInfo.NodeRef, CameraInfo.PrevPosition, CameraInfo.Position);
                    }
                    else if (CameraType != CameraType.Spectator)
                    {
                        CameraInfo.NodeRef = _scene.UpdateNodeRef(NodeRef, curPos, CameraInfo.Position);
                    }
                }
                // todo?: something if wifi
            }
            if (Flags1.TestFlag(PlayerFlags1.Standing) && (_timeStanding == 0 || _scene.FrameCount % (4 * 2) == 0) // todo: FPS stuff
                && (Flags1.TestFlag(PlayerFlags1.OnAcid) || (Flags1.TestFlag(PlayerFlags1.OnLava) && Hunter != Hunter.Spire)))
            {
                DamageFlags flags = DamageFlags.IgnoreInvuln;
                if (Flags1.TestFlag(PlayerFlags1.OnLava))
                {
                    flags |= DamageFlags.NoSfx;
                }
                TakeDamage(1, flags, direction: null, source: null);
            }
            Debug.Assert(_scene.Room != null);
            if (_scene.Multiplayer && _scene.Room.Meta.HasLimits)
            {
                if (Position.Y < _scene.Room.Meta.PlayerMin.Y)
                {
                    TakeDamage(0, DamageFlags.Death, direction: null, source: null);
                }
                Position = Vector3.Clamp(Position, _scene.Room.Meta.PlayerMin.WithY(Position.Y), _scene.Room.Meta.PlayerMax);
            }
            if (Position.Y < _scene.Room.Meta.KillHeight)
            {
                TakeDamage(0, DamageFlags.Death, direction: null, source: null);
            }
            // todo: update license stats
            Flags2 &= ~PlayerFlags2.NoFormSwitch;
            if (_doubleDmgTimer > 0)
            {
                _doubleDmgTimer--;
                if (IsMainPlayer)
                {
                    // the game checks this last, so it might create and destroy the effect on the same frame
                    if (_doubleDmgTimer == 0)
                    {
                        UpdateDoubleDamageSfx(index: 0, play: false);
                        if (_doubleDmgEffect != null)
                        {
                            _scene.UnlinkEffectEntry(_doubleDmgEffect);
                            _doubleDmgEffect = null;
                        }
                    }
                    else
                    {
                        if (!IsAltForm && !IsMorphing && !IsUnmorphing)
                        {
                            if (_doubleDmgEffect != null)
                            {
                                _doubleDmgEffect.Transform(_upVector, _gunVec1, _muzzlePos);
                            }
                            else
                            {
                                _doubleDmgEffect = _scene.SpawnEffectGetEntry(244, _upVector, _gunVec1, _muzzlePos); // doubleDamageGun
                                _doubleDmgEffect?.SetElementExtension(true);
                            }
                        }
                        else if (_doubleDmgEffect != null)
                        {
                            _scene.UnlinkEffectEntry(_doubleDmgEffect);
                            _doubleDmgEffect = null;
                        }
                        if (_doubleDmgTimer == 210 * 2) // todo: FPS stuff
                        {
                            UpdateDoubleDamageSfx(index: 1, play: true);
                            UpdateDoubleDamageSpeed(2);
                        }
                        else if (_doubleDmgTimer == 120 * 2) // todo: FPS stuff
                        {
                            UpdateDoubleDamageSfx(index: 2, play: true);
                            UpdateDoubleDamageSpeed(3);
                        }
                    }
                }
            }
            if (_burnTimer > 0)
            {
                _burnTimer--;
                if (_burnTimer % (8 * 2) == 0) // todo:FPS stuff
                {
                    TakeDamage(1, DamageFlags.NoSfx | DamageFlags.Burn | DamageFlags.NoDmgInvuln, direction: null, _burnedBy);
                }
                if (_burnEffect != null)
                {
                    if (CameraSequence.Current?.BlockInput == true)
                    {
                        _scene.UnlinkEffectEntry(_burnEffect);
                        _burnEffect = null;
                    }
                    else if (!IsMainPlayer || IsAltForm || IsMorphing)
                    {
                        var facing = new Vector3(_field70, 0, _field74);
                        _burnEffect.Transform(facing, Vector3.UnitY, _volume.SpherePosition);
                    }
                    else
                    {
                        _burnEffect.Transform(_upVector, _gunVec1, _muzzlePos);
                    }
                }
            }
            else if (_burnEffect != null)
            {
                _scene.UnlinkEffectEntry(_burnEffect);
                _burnEffect = null;
            }
            // todo?: something for wifi
            return true;
        }

        public void ActivateJumpPad(JumpPadEntity jumpPad, Vector3 vector, ushort lockTime)
        {
            if (_timeSinceJumpPad > 5 * 2) // todo: FPS stuff
            {
                _soundSource.PlaySfx(SfxId.JUMP_PAD);
            }
            Speed = vector;
            _jumpPadAccel = vector;
            _lastJumpPad = jumpPad;
            Flags1 |= PlayerFlags1.UsedJumpPad;
            lockTime *= 2; // todo: FPS stuff
            _jumpPadControlLock = lockTime;
            _jumpPadControlLockMin = Math.Max(lockTime, (ushort)(5 * 2)); // todo: FPS stuff
            _timeSinceJumpPad = 0;
            Flags1 &= ~PlayerFlags1.UsedJump;
            Flags1 |= PlayerFlags1.Standing;
            if (IsAltForm)
            {
                float accelY = _jumpPadAccel.Y;
                float altGrav = Fixed.ToFloat(Values.AltAirGravity);
                float bipedGrav = Fixed.ToFloat(Values.BipedGravity);
                float altFactor = -accelY / altGrav;
                float bipedFactor = -accelY / bipedGrav;
                float lockInc = ((accelY * bipedFactor) + (bipedGrav * (bipedFactor * bipedFactor) / 2)
                    - ((accelY * altFactor) + (altGrav * (altFactor * altFactor) / 2)))
                    / accelY + 2;
                _jumpPadControlLock += (ushort)(lockInc * 2); // todo: FPS stuff
            }
        }

        private void PickUpItems()
        {
            if (_health == 0 || (IsBot && !_scene.Multiplayer) || IgnoreItemPickups
                || IsMainPlayer && CameraSequence.Current?.BlockInput == true)
            {
                return;
            }
            // todo: visualize
            float distSqr = _volume.SphereRadius + 0.45f;
            distSqr *= distSqr;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.ItemInstance)
                {
                    continue;
                }
                var item = (ItemInstanceEntity)entity;
                bool inRange = false;
                if (IsAltForm)
                {
                    Vector3 between = item.Position - _volume.SpherePosition;
                    if (Vector3.Dot(between, between) < distSqr)
                    {
                        inRange = true;
                    }
                }
                else
                {
                    Vector3 between = item.Position - Position;
                    Vector3 lateral = between.WithY(0);
                    if (Vector3.Dot(lateral, lateral) < distSqr
                        && between.Y >= Fixed.ToFloat(Values.MinPickupHeight)
                        && between.Y <= Fixed.ToFloat(Values.MaxPickupHeight))
                    {
                        inRange = true;
                    }
                }
                if (!inRange)
                {
                    continue;
                }

                void PlaySfx(SfxId sfx)
                {
                    if (IsMainPlayer && Sfx.TimedSfxMute == 0)
                    {
                        _soundSource.PlayFreeSfx(sfx);
                    }
                }

                bool pickedUp = false;
                switch (item.ItemType)
                {
                case ItemType.HealthMedium:
                case ItemType.HealthSmall:
                case ItemType.HealthBig:
                    if (!IsPrimeHunter)
                    {
                        pickedUp = true;
                        _timeSinceHeal = 0;
                        GainHealth(_healthPickupAmounts[(int)item.ItemType]);
                        if (Sfx.TimedSfxMute == 0)
                        {
                            PlaySfx(item.ItemType == ItemType.HealthSmall ? SfxId.POWER_UP1 : SfxId.POWER_UP2);
                        }
                    }
                    break;
                case ItemType.UASmall:
                case ItemType.UABig:
                case ItemType.MissileSmall:
                case ItemType.MissileBig:
                    pickedUp = true;
                    int slot = item.ItemType == ItemType.UASmall || item.ItemType == ItemType.UABig ? 0 : 1;
                    _timeSincePickup = 0;
                    int amount;
                    if (item.ItemType == ItemType.UABig || item.ItemType == ItemType.MissileBig)
                    {
                        amount = _scene.Multiplayer ? 100 : 250;
                        PlaySfx(SfxId.AMMO_POWER_UP2);
                    }
                    else
                    {
                        amount = _scene.Multiplayer ? 50 : 100;
                        PlaySfx(SfxId.AMMO_POWER_UP1);
                    }
                    _ammo[slot] += amount;
                    if (_ammo[slot] > _ammoMax[slot])
                    {
                        _ammo[slot] = _ammoMax[slot];
                    }
                    // todo: update story save
                    break;
                case ItemType.VoltDriver:
                case ItemType.Battlehammer:
                case ItemType.Imperialist:
                case ItemType.Judicator:
                case ItemType.Magmaul:
                case ItemType.ShockCoil:
                case ItemType.OmegaCannon:
                case ItemType.AffinityWeapon:
                    pickedUp = true;
                    PickUpWeapon(item.ItemType);
                    break;
                case ItemType.DoubleDamage:
                    pickedUp = true;
                    _timeSincePickup = 0;
                    _doubleDmgTimer = 900 * 2; // todo: FPS stuff
                    if (IsMainPlayer)
                    {
                        _soundSource.PlayFreeSfx(SfxId.DOUBLE_DAMAGE_POWER_UP);
                        UpdateDoubleDamageSfx(index: 0, play: true);
                        UpdateDoubleDamageSpeed(1);
                    }
                    break;
                case ItemType.Cloak:
                    pickedUp = true;
                    _timeSincePickup = 0;
                    _cloakTimer = 900 * 2; // todo: FPS stuff
                    Flags2 |= PlayerFlags2.Cloaking;
                    if (IsMainPlayer)
                    {
                        _soundSource.PlayFreeSfx(SfxId.CLOAK_POWER_UP);
                        UpdateCloakSfx(index: 0, play: true);
                    }
                    break;
                case ItemType.Deathalt:
                    pickedUp = true;
                    _timeSincePickup = 0;
                    _deathaltTimer = 900 * 2; // todo: FPS stuff
                    if (!IsAltForm && !IsMorphing)
                    {
                        TrySwitchForms(force: true);
                    }
                    if (IsMainPlayer)
                    {
                        _soundSource.PlayFreeSfx(SfxId.DOUBLE_DAMAGE_POWER_UP);
                    }
                    break;
                case ItemType.EnergyTank:
                    if (!IsBot)
                    {
                        pickedUp = true;
                        _timeSincePickup = 0;
                        _healthMax += Values.EnergyTank;
                        _healthRecovery = _healthMax - _health;
                        GameState.StorySave.HealthMax = _healthMax;
                        if (IsMainPlayer)
                        {
                            // ENERGY TANK FOUND the POWER SUIT can now store 100 more UNITS of energy.
                            ShowDialog(DialogType.Event, messageId: 4, param1: (int)EventType.EnergyTank);
                        }
                    }
                    break;
                case ItemType.MissileExpansion:
                    if (!IsBot)
                    {
                        pickedUp = true;
                        _timeSincePickup = 0;
                        _ammoMax[1] += 100;
                        _ammoRecovery[1] = _ammoMax[1] - _ammo[1];
                        GameState.StorySave.AmmoMax[1] = _ammoMax[1];
                        if (IsMainPlayer)
                        {
                            // MISSILE EXPANSION FOUND your MISSILE capacity is increased by 10 UNITS.
                            ShowDialog(DialogType.Event, messageId: 3, param1: (int)EventType.MissileTank);
                        }
                    }
                    break;
                case ItemType.UAExpansion:
                    if (!IsBot)
                    {
                        pickedUp = true;
                        _timeSincePickup = 0;
                        _ammoMax[0] += 300;
                        _ammoRecovery[0] = _ammoMax[0] - _ammo[0];
                        GameState.StorySave.AmmoMax[0] = _ammoMax[0];
                        if (IsMainPlayer)
                        {
                            // UA EXPANSION FOUND your UNIVERSAL AMMO capacity is increased by 30 UNITS.
                            ShowDialog(DialogType.Event, messageId: 46, param1: (int)EventType.UATank);
                        }
                    }
                    break;
                case ItemType.ArtifactKey:
                    pickedUp = true;
                    if (IsMainPlayer)
                    {
                        _soundSource.PlayFreeSfx(SfxId.KEY_PICKUP);
                    }
                    break;
                default:
                    pickedUp = true;
                    break;
                }
                if (pickedUp)
                {
                    item.OnPickedUp();
                }
            }
        }

        private void PickUpWeapon(ItemType itemType)
        {
            BeamType weapon;
            if (itemType == ItemType.AffinityWeapon)
            {
                if (Hunter == Hunter.Samus || Hunter == Hunter.Guardian) // game doesn't check for Guardian
                {
                    _ammo[1] = Math.Min(_ammo[1] + 50, _ammoMax[1]);
                    if (IsMainPlayer && Sfx.TimedSfxMute == 0)
                    {
                        _soundSource.PlayFreeSfx(SfxId.AMMO_POWER_UP1);
                    }
                    return;
                }
                weapon = Weapons.GetAffinityBeam(Hunter);
            }
            else
            {
                weapon = itemType switch
                {
                    ItemType.VoltDriver => BeamType.VoltDriver,
                    ItemType.Battlehammer => BeamType.Battlehammer,
                    ItemType.Imperialist => BeamType.Imperialist,
                    ItemType.Judicator => BeamType.Judicator,
                    ItemType.Magmaul => BeamType.Magmaul,
                    ItemType.ShockCoil => BeamType.ShockCoil,
                    ItemType.OmegaCannon => BeamType.OmegaCannon,
                    _ => BeamType.None
                };
            }
            if (weapon == BeamType.None)
            {
                return;
            }
            if (!_scene.Multiplayer && (GameState.StorySave.Weapons & (1 << (int)weapon)) == 0)
            {
                GameState.StorySave.Weapons |= (ushort)(1 << (int)weapon);
                int weaponId = (int)weapon;
                string value1 = Metadata.WeaponNamesUpper[weaponId];
                string value2 = "";
                int messageId = Metadata.WeaponMessageIds[weaponId];
                if (messageId != 0)
                {
                    value2 = Strings.GetHudMessage(messageId);
                }
                // &tab0 FOUND you've obtained the &tab0. &tab1
                ShowDialog(DialogType.Event, messageId: 5, param1: weaponId, value1: value1, value2: value2);
            }
            WeaponInfo info = Weapons.Current[(int)weapon];
            if (_ammo[info.AmmoType] < 60)
            {
                _ammo[info.AmmoType] = Math.Min(_ammo[info.AmmoType] + 60, 60);
            }
            if (!_availableWeapons[weapon])
            {
                _availableWeapons[weapon] = true;
                _availableCharges[weapon] = true;
                BeamType slot2Weapon = _weaponSlots[2];
                int slot2Index = (int)slot2Weapon;
                BeamType affinityWeapon = Weapons.GetAffinityBeam(Hunter);
                if (slot2Weapon == BeamType.None || weapon == BeamType.OmegaCannon
                    || (info.Priority > Weapons.Current[slot2Index].Priority || weapon == affinityWeapon)
                    && (!Flags2.TestFlag(PlayerFlags2.Shooting) || CurrentWeapon != slot2Weapon))
                {
                    // todo: update HUD
                    if ((info.Priority > EquipInfo.Weapon.Priority || weapon == BeamType.OmegaCannon
                        || weapon == affinityWeapon) && !Flags2.TestFlag(PlayerFlags2.Shooting))
                    {
                        if (!TryEquipWeapon(weapon))
                        {
                            UpdateAffinityWeaponSlot(weapon, slot: 2);
                        }
                    }
                    else if (!Flags2.TestFlag(PlayerFlags2.Shooting) || CurrentWeapon != slot2Weapon)
                    {
                        UpdateAffinityWeaponSlot(weapon, slot: 2);
                    }
                }
                if (IsMainPlayer && Sfx.TimedSfxMute == 0)
                {
                    _soundSource.PlayFreeSfx(SfxId.WEAPON_POWER_UP);
                }
            }
            else if (IsMainPlayer && Sfx.TimedSfxMute == 0)
            {
                _soundSource.PlayFreeSfx(SfxId.AMMO_POWER_UP1);
            }
        }

        private static readonly int[] _healthPickupAmounts = new int[3] { 30, 60, 100 };

        public void GainHealth(uint health)
        {
            GainHealth((int)health);
        }

        public void GainHealth(int health)
        {
            if (_health > 0)
            {
                if (Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    if (_health <= _halfturret.Health)
                    {
                        _health += health - health / 2;
                        _halfturret.Health += health / 2;
                    }
                    else
                    {
                        _health += health / 2;
                        _halfturret.Health += health - health / 2;
                    }
                    if (_halfturret.Health > 100)
                    {
                        _halfturret.Health = 100;
                    }
                }
                else
                {
                    _health += health;
                }
                if (_health > _healthMax)
                {
                    _health = _healthMax;
                }
            }
        }

        private bool TrySwitchForms(bool force = false)
        {
            if (!force && (IsMorphing || IsUnmorphing || _frozenTimer > 0 || _field6D0 || _deathaltTimer > 0
                    || Flags2.TestFlag(PlayerFlags2.NoFormSwitch)
                    || !IsAltForm && Flags2.TestFlag(PlayerFlags2.BipedStuck)
                    || IsAltForm && MorphCamera != null))
            {
                if (IsMainPlayer && (CameraSequence.Current == null || !CameraSequence.Current.BlockInput))
                {
                    _soundSource.PlayFreeSfx(SfxId.BEAM_SWITCH_FAIL);
                }
                return false;
            }
            if (Hunter == Hunter.Guardian) // todo: playable Guardian
            {
                return false;
            }

            void AfterSwitch()
            {
                Flags1 &= ~PlayerFlags1.Bit30;
                UpdateZoom(false);
                EquipInfo.ChargeLevel = 0;
                EquipInfo.SmokeLevel = 0;
                if (_burnTimer > 0)
                {
                    CreateBurnEffect();
                }
            }

            if (!IsAltForm)
            {
                EnterAltForm();
                AfterSwitch();
                return true;
            }
            if (!Flags1.TestFlag(PlayerFlags1.NoUnmorph))
            {
                ExitAltForm();
                AfterSwitch();
                return true;
            }
            if (IsMainPlayer)
            {
                _soundSource.PlayFreeSfx(SfxId.BEAM_SWITCH_FAIL);
            }
            return false;
        }

        private void UpdateAimVecs()
        {
            Vector3 facing = _facingVector;
            Vector3 up = _upVector;
            _gunDrawPos = Fixed.ToFloat(Values.FieldB8) * facing
                + CameraInfo.Position
                + Fixed.ToFloat(Values.FieldB0) * _gunVec2
                + Fixed.ToFloat(Values.FieldB4) * up;
            float cos = MathF.Cos(MathHelper.DegreesToRadians(_gunViewBob));
            _gunDrawPos.Y += Fixed.ToFloat(20) * cos;
            _aimVec = _aimPosition - _gunDrawPos;
            float dot = Vector3.Dot(_aimVec, facing);
            Vector3 vec = facing * dot;
            _aimVec = (_aimVec + (vec - _aimVec) / 2).Normalized();
            _muzzlePos = _gunDrawPos + _aimVec * Fixed.ToFloat(Values.MuzzleOffset);
        }

        private void InitAltTransform()
        {
            _field4E8 = _gunVec2;
            Vector3 up = _upVector;
            _modelTransform.Row0.X = _gunVec2.X; // right?
            _modelTransform.Row0.Y = 0;
            _modelTransform.Row0.Z = _gunVec2.Z;
            _modelTransform.Row1.X = up.X;
            _modelTransform.Row1.Y = up.Y;
            _modelTransform.Row1.Z = up.Z;
            _modelTransform.Row2.X = _field70; // facing?
            _modelTransform.Row2.Y = 0;
            _modelTransform.Row2.Z = _field74;
            _modelTransform.Row2.Xyz = Vector3.Cross(_modelTransform.Row0.Xyz, _modelTransform.Row1.Xyz);
            _modelTransform.Row1.Xyz = Vector3.Cross(_modelTransform.Row2.Xyz, _modelTransform.Row0.Xyz);
            _modelTransform.Row0.Xyz = Vector3.Normalize(_modelTransform.Row0.Xyz);
            _modelTransform.Row1.Xyz = Vector3.Normalize(_modelTransform.Row1.Xyz);
            _modelTransform.Row2.Xyz = Vector3.Normalize(_modelTransform.Row2.Xyz);
        }

        private void UpdateAltTransform()
        {
            if (Hunter == Hunter.Noxus)
            {
                _altWobble += (5 - _altWobble) / 32 / 2; // todo: FPS stuff
                _altWobble = Math.Clamp(_altWobble, Fixed.ToFloat(Values.AltMinWobble), Fixed.ToFloat(Values.AltMaxWobble));
                _altTiltX -= _altTiltX / 8 / 2; // todo: FPS stuff
                _altTiltZ -= _altTiltZ / 8 / 2; // todo: FPS stuff
                _altTiltX += -(_altTiltX + Fixed.ToFloat(25) * (Speed.X - PrevSpeed.X)) / 32 / 2; // todo: FPS stuff
                _altTiltZ += -(_altTiltZ + Fixed.ToFloat(25) * (Speed.Z - PrevSpeed.Z)) / 32 / 2; // todo: FPS stuff
                float minSpinAccel = Fixed.ToFloat(Values.AltMinSpinAccel);
                float maxSpinAccel = Fixed.ToFloat(Values.AltMaxSpinAccel);
                _altSpinSpeed += (minSpinAccel
                    + (_altAttackTime * (maxSpinAccel - minSpinAccel) / (Values.AltAttackStartup * 2))
                    - _altSpinSpeed) / 32 / 2; // todo: FPS stuff
                _altSpinSpeed = Math.Clamp(_altSpinSpeed, Fixed.ToFloat(Values.AltMinSpinSpeed), Fixed.ToFloat(Values.AltMaxSpinSpeed));
                _altSpinRot += _altSpinSpeed / 2; // todo: FPS stuff
                while (_altSpinRot > 360)
                {
                    _altSpinRot -= 360;
                }
                var rotX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_altWobble));
                var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_altSpinRot));
                Matrix4 transform = rotX * rotY;
                float mag = MathF.Sqrt(_altTiltX * _altTiltX + _altTiltZ * _altTiltZ);
                if (mag != 0)
                {
                    var axis = new Vector3(_altTiltZ / mag, 0, -_altTiltX / mag);
                    float angle = mag * Fixed.ToFloat(Values.AltTiltAngleMax);
                    angle = MathF.Min(angle, Fixed.ToFloat(Values.AltTiltAngleCap));
                    var rotAxis = Matrix4.CreateFromAxisAngle(axis, MathHelper.DegreesToRadians(angle));
                    transform *= rotAxis;
                }
                _modelTransform = transform;
            }
            else if (Hunter == Hunter.Kanden)
            {
                UpdateStinglarvaSegments();
            }
            else if (Hunter == Hunter.Samus || Hunter == Hunter.Spire)
            {
                Vector3 axis = Vector3.Zero;
                float altRadius = Fixed.ToFloat(Values.AltColRadius);
                if (Hunter == Hunter.Spire || Flags1.TestFlag(PlayerFlags1.CollidingEntity))
                {
                    // todo: FPS stuff
                    axis.X = altRadius * (Speed.Z / 2);
                    axis.Z = -altRadius * (Speed.X / 2);
                }
                else
                {
                    axis.X = altRadius * (Position.Z - PrevPosition.Z);
                    axis.Z = -altRadius * (Position.X - PrevPosition.X);
                }
                float mag = axis.Length;
                if (mag > 0)
                {
                    axis /= mag;
                    float angle = mag / (altRadius * altRadius);
                    var rotMtx = Matrix4.CreateFromAxisAngle(axis, angle);
                    Matrix4 transform = _modelTransform * rotMtx;
                    if (Hunter == Hunter.Samus)
                    {
                        if (Vector3.Dot(transform.Row0.Xyz, axis) < 0)
                        {
                            axis *= -1;
                        }
                        axis = Vector3.Cross(transform.Row0.Xyz, axis);
                        float mbAngle = axis.Length;
                        if (mbAngle > 0)
                        {
                            if (mbAngle > 0.125f)
                            {
                                float div = Math.Min(angle / Fixed.ToFloat(3216), 1);
                                mbAngle *= div / 8;
                            }
                            rotMtx = Matrix4.CreateFromAxisAngle(axis, mbAngle);
                            transform *= rotMtx;
                        }
                    }
                    transform.Row2.Xyz = Vector3.Cross(transform.Row0.Xyz, transform.Row1.Xyz);
                    transform.Row1.Xyz = Vector3.Cross(transform.Row2.Xyz, transform.Row0.Xyz);
                    transform.Row0.Xyz = transform.Row0.Xyz.Normalized();
                    transform.Row1.Xyz = transform.Row1.Xyz.Normalized();
                    transform.Row2.Xyz = transform.Row2.Xyz.Normalized();
                    if (Hunter == Hunter.Spire)
                    {
                        for (int i = 0; i < _spireAltVecs.Length; i++)
                        {
                            _spireAltVecs[i] = Matrix.Vec3MultMtx3(Metadata.SpireAltVectors[i], transform);
                        }
                    }
                    _modelTransform = transform;
                }
            }
            else
            {
                _modelTransform = GetTransformMatrix(new Vector3(_field80, 0, _field84), Vector3.UnitY);
            }
        }

        private void UpdateStinglarvaSegments()
        {
            const int cycle = 13 * 2; // todo: FPS stuff
            float angle = 359f * (_scene.FrameCount % cycle) / (cycle - 1);
            float factor = 0.3f * MathF.Sin(MathHelper.DegreesToRadians(angle)) * _hSpeedMag;
            _kandenSegPos[0] = Position.AddX(_field78 * factor).AddZ(_field7C * factor);
            Vector3 dir;
            if (Speed.LengthSquared > 0.02f)
            {
                dir = new Vector3(Speed.X + _field80 / 4, Speed.Y, Speed.Z + _field84 / 4);
            }
            else
            {
                dir = new Vector3(_field80, 0, _field84);
            }
            dir = dir.Normalized();
            if (Vector3.Dot(dir, _kandenSegMtx[0].Row2.Xyz) < Fixed.ToFloat(-5))
            {
                dir.X += Fixed.ToFloat(5);
            }
            dir = _kandenSegMtx[0].Row2.Xyz + 0.3f * (dir - _kandenSegMtx[0].Row2.Xyz);
            dir = dir.Normalized();
            if (dir.X != 0 || dir.Z != 0)
            {
                _kandenSegMtx[0] = GetTransformMatrix(dir, Vector3.UnitY);
            }
            else
            {
                var facing = new Vector3(-_field70, 0, -_field74);
                var up = new Vector3(dir.X, dir.Y, 0);
                _kandenSegMtx[0] = GetTransformMatrix(facing, up);
            }
            _kandenSegMtx[0].Row3.Xyz = _kandenSegPos[0];
            Debug.Assert(_kandenSegPos.Length == _kandenSegMtx.Length);
            for (int i = 1; i < _kandenSegPos.Length; i++)
            {
                angle += 85;
                while (angle >= 360)
                {
                    angle -= 360;
                }
                factor = 0.12f * MathF.Sin(MathHelper.DegreesToRadians(angle)) * _hSpeedMag;
                Vector3 segPos = _kandenSegPos[i];
                segPos = segPos.AddX(_field78 * factor).AddZ(_field7C * factor);
                _kandenSegPos[i] = segPos;
                dir = (_kandenSegPos[i - 1] - segPos).Normalized();
                Matrix4 prevMtx = _kandenSegMtx[i - 1];
                float dot = Vector3.Dot(dir, prevMtx.Row2.Xyz);
                if (dot < Fixed.ToFloat(2896))
                {
                    var axis = Vector3.Cross(dir, prevMtx.Row2.Xyz);
                    float mag = axis.Length;
                    axis /= mag;
                    float atan = MathF.Atan2(mag, dot);
                    atan -= MathHelper.DegreesToRadians(45);
                    var rotMtx = Matrix4.CreateFromAxisAngle(axis, atan);
                    dir = Matrix.Vec3MultMtx3(dir, rotMtx);
                }
                _kandenSegMtx[i] = GetTransformMatrix(dir, _kandenSegMtx[0].Row1.Xyz);
                float dist = KandenAltNodeDistances[i - 1];
                dir *= dist;
                _kandenSegPos[i] = _kandenSegPos[i - 1] - dir;
                _kandenSegMtx[i].Row3.Xyz = _kandenSegPos[i];
            }
        }

        private void EnterAltForm()
        {
            _altRollFbX = _field70;
            _altRollFbZ = _field74;
            _altRollLrX = _gunVec2.X;
            _altRollLrZ = _gunVec2.Z;
            Flags1 |= PlayerFlags1.Morphing;
            var camFacing = new Vector3(_field70, 0, _field74);
            SwitchCamera(Values.AltFormStrafe != 0 ? CameraType.Third2 : CameraType.Third1, camFacing);
            InitAltTransform();
            _modelTransform.Row3.Xyz = Vector3.Zero;
            if (Hunter == Hunter.Spire)
            {
                for (int i = 0; i < _spireAltVecs.Length; i++)
                {
                    _spireAltVecs[i] = Vector3.Zero;
                }
                _altModel.SetAnimation((int)SpireAltAnim.Attack, AnimFlags.Paused);
            }
            else if (Hunter == Hunter.Noxus)
            {
                _altSpinSpeed = Fixed.ToFloat(Values.AltMinSpinAccel);
                _altTiltX = 0;
                _altTiltZ = 0;
                _altSpinRot = 0;
                _altWobble = 0;
                // animation frames updated later based on attack timer
                _altModel.SetAnimation((int)NoxusAltAnim.Extend, AnimFlags.Paused);
            }
            else if (Hunter == Hunter.Weavel)
            {
                _altModel.SetAnimation((int)WeavelAltAnim.Idle);
                Flags2 |= PlayerFlags2.Halfturret;
                _halfturret.NodeRef = NodeRef;
                _scene.AddEntity(_halfturret);
            }
            else if (Hunter == Hunter.Samus)
            {
                _furlEffect = _scene.SpawnEffectGetEntry(30, _gunVec2, _facingVector, Position); // samusFurl
            }
            else if (Hunter == Hunter.Kanden)
            {
                _altModel.SetAnimation((int)KandenAltAnim.Idle, AnimFlags.Paused);
            }
            else if (Hunter == Hunter.Trace)
            {
                _altModel.SetAnimation((int)TraceAltAnim.Idle);
            }
            else if (Hunter == Hunter.Sylux)
            {
                _altModel.SetAnimation((int)SyluxAltAnim.Idle);
            }
            if (EquipInfo.ChargeLevel > 0)
            {
                StopBeamChargeSfx(CurrentWeapon);
                SetGunAnimation(GunAnimation.Idle, AnimFlags.NoLoop);
            }
            EquipInfo.ChargeLevel = 0;
            SetBipedAnimation(PlayerAnimation.Morph, AnimFlags.NoLoop);
            PlayHunterSfx(HunterSfx.Morph);
        }

        private void ExitAltForm()
        {
            if (Flags2.TestFlag(PlayerFlags2.Halfturret))
            {
                Flags2 &= ~PlayerFlags2.Halfturret;
                if (_halfturret.Health > 0)
                {
                    GainHealth(_halfturret.Health);
                }
                _halfturret.Die();
            }
            Flags1 &= ~PlayerFlags1.Morphing;
            Flags1 |= PlayerFlags1.Unmorphing;
            SwitchCamera(CameraType.First, _facingVector);
            // the game stops the boost charge SFX here, but that SFX is empty
            _boostCharge = 0;
            SetBipedAnimation(PlayerAnimation.Unmorph, AnimFlags.NoLoop);
            if (Flags2.TestFlag(PlayerFlags2.AltAttack))
            {
                EndAltAttack();
            }
            UpdateZoom(false);
            EquipInfo.ChargeLevel = 0;
            EquipInfo.SmokeLevel = 0;
            PlayHunterSfx(HunterSfx.Unmorph);
            if (IsAltForm)
            {
                UpdateForm(altForm: false);
            }
        }

        private void UpdateForm(bool altForm)
        {
            if (altForm)
            {
                Flags1 |= PlayerFlags1.AltForm;
            }
            else
            {
                Flags1 &= ~PlayerFlags1.AltForm;
            }
            // todo?: update HUD if main player
            UpdateScanIds();
            if (altForm)
            {
                CollisionVolume altVolume = PlayerVolumes[(int)Hunter, 2];
                Position += _volumeUnxf.SpherePosition - altVolume.SpherePosition;
                _volumeUnxf = altVolume;
                InitAltTransform();
                _field80 = _field70;
                _field84 = _field74;
                if (Hunter == Hunter.Kanden)
                {
                    _kandenSegPos[0] = Position;
                    var facing = new Vector3(_field70, 0, _field74);
                    _kandenSegMtx[0] = GetTransformMatrix(facing, Vector3.UnitY, Position);
                    Debug.Assert(_kandenSegPos.Length == _kandenSegMtx.Length);
                    for (int i = 1; i < _kandenSegPos.Length; i++)
                    {
                        float dist = -KandenAltNodeDistances[i - 1];
                        _kandenSegPos[i] = _kandenSegPos[i - 1] + facing * dist;
                        Matrix4 matrix = _kandenSegMtx[0];
                        matrix.Row3.Xyz = _kandenSegPos[i];
                        _kandenSegMtx[i] = matrix;
                    }
                }
                else if (Hunter == Hunter.Spire)
                {
                    _scene.SpawnEffect(37, Vector3.UnitX, Vector3.UnitY, Position); // spireAltSlam
                    CameraInfo.SetShake(0.3f);
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Player || entity == this)
                        {
                            continue;
                        }
                        var other = (PlayerEntity)entity;
                        if (other.Flags1.TestFlag(PlayerFlags1.Standing) && Vector3.DistanceSquared(Position, other.Position) < 16)
                        {
                            other.CameraInfo.SetShake(0.3f);
                            if (other.Speed.Y < 0.15f)
                            {
                                other.Speed = other.Speed.WithY(0.15f);
                            }
                        }
                    }
                }
            }
            else
            {
                _gunVec1 = _facingVector;
                CollisionVolume bipedVolume = PlayerVolumes[(int)Hunter, 0];
                Position += _volumeUnxf.SpherePosition - bipedVolume.SpherePosition;
                _volumeUnxf = bipedVolume;
            }
            StopAltFormSfx();
        }

        private void CreateBurnEffect()
        {
            if (_burnEffect != null)
            {
                _scene.UnlinkEffectEntry(_burnEffect);
                _burnEffect = null;
            }
            if (!IsUnmorphing)
            {
                Vector3 up;
                Vector3 facing;
                Vector3 position;
                int effectId;
                if (IsAltForm || IsMorphing || !IsMainPlayer)
                {
                    position = _volume.SpherePosition;
                    up = Vector3.UnitY;
                    facing = new Vector3(_field70, 0, _field74);
                    effectId = IsAltForm || IsMorphing ? 187 : 189; // flamingAltForm or flamingHunter
                }
                else
                {
                    position = _muzzlePos;
                    up = _gunVec1;
                    facing = _upVector;
                    effectId = 188; // flamingGun
                }
                _burnEffect = _scene.SpawnEffectGetEntry(effectId, facing, up, position);
                _burnEffect?.SetElementExtension(true);
            }
        }

        private void CreateIceBreakEffectGun()
        {
            int effectId = 231; // iceShatter
            Vector3 playerUp = _upVector;
            Vector3 up = _facingVector;
            Vector3 facing;
            if (up.Z <= -0.9f || up.Z >= 0.9f)
            {
                facing = Vector3.Cross(Vector3.UnitX, up).Normalized();
            }
            else
            {
                facing = Vector3.Cross(Vector3.UnitZ, up).Normalized();
            }
            Vector3 position = CameraInfo.Position + up / 2;
            Vector3 spawnPos = position;
            _scene.SpawnEffect(effectId, facing, up, spawnPos);
            spawnPos = position + _gunVec2 * 0.4f;
            _scene.SpawnEffect(effectId, facing, up, spawnPos);
            spawnPos = position - _gunVec2 * 0.4f;
            _scene.SpawnEffect(effectId, facing, up, spawnPos);
            spawnPos = position + playerUp * 0.4f;
            _scene.SpawnEffect(effectId, facing, up, spawnPos);
            spawnPos = position - playerUp * 0.4f;
            _scene.SpawnEffect(effectId, facing, up, spawnPos);
        }

        private void CreateIceBreakEffectBiped(Model model)
        {
            Debug.Assert(model.Nodes.Count > 1);
            int effectId = 231; // iceShatter
            for (int i = 1; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes[i];
                Vector3 pos = _bipedIceTransforms[i].Row3.Xyz;
                Vector3 up;
                if (node.ChildIndex <= 0)
                {
                    up = (pos - Position).Normalized();
                }
                else
                {
                    up = (_bipedIceTransforms[node.ChildIndex].Row3.Xyz - pos).Normalized();
                }
                Vector3 facing;
                if (up.Z <= -0.9f || up.Z >= 0.9f)
                {
                    facing = Vector3.Cross(Vector3.UnitX, up).Normalized();
                }
                else
                {
                    facing = Vector3.Cross(Vector3.UnitZ, up).Normalized();
                }
                _scene.SpawnEffect(effectId, facing, up, pos);
            }
        }

        private void CreateIceBreakEffectAlt()
        {

            int effectId = 231; // iceShatter
            _scene.SpawnEffect(effectId, Vector3.UnitX, Vector3.UnitY, _volume.SpherePosition);
            _scene.SpawnEffect(effectId, Vector3.UnitY, Vector3.UnitX, _volume.SpherePosition);
            _scene.SpawnEffect(effectId, Vector3.UnitY, -Vector3.UnitX, _volume.SpherePosition);
            _scene.SpawnEffect(effectId, Vector3.UnitY, Vector3.UnitX, _volume.SpherePosition);
            _scene.SpawnEffect(effectId, Vector3.UnitY, -Vector3.UnitX, _volume.SpherePosition);
        }

        private PlayerSpawnEntity? GetRespawnPoint()
        {
            Debug.Assert(_scene.Room != null);
            PlayerSpawnEntity? chosenSpawn = null;
            int limit = 0;
            var valid = new List<PlayerSpawnEntity>();
            PlayerSpawnEntity? bestAvailable = null;
            float bestDistance = 0;
            // the game iterates 25 entities starting with the first spawn point; we iterate 25 spawn points
            // --> shouldn't matter since spawn points are meant to be together in the entity list
            for (int i = 0; i < _scene.Entities.Count && limit < 25; i++)
            {
                EntityBase spawn = _scene.Entities[i];
                if (spawn.Type != EntityType.PlayerSpawn)
                {
                    continue;
                }
                var candidate = (PlayerSpawnEntity)spawn;
                if (!candidate.IsActive || candidate.Cooldown != 0 || _scene.FrameCount == 0 && candidate.Availability)
                {
                    limit++;
                    continue;
                }
                if (_scene.GameMode == GameMode.Capture && candidate.Data.TeamIndex != -1
                    && candidate.Data.TeamIndex != TeamIndex)
                {
                    limit++;
                    continue;
                }
                float minDistSqr = 100;
                for (int j = 0; j < _scene.Entities.Count; j++)
                {
                    EntityBase entity = _scene.Entities[j];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (player.Health > 0)
                    {
                        Vector3 between = candidate.Position - player.Position;
                        float distSqr = Vector3.Dot(between, between);
                        if (distSqr < minDistSqr)
                        {
                            minDistSqr = distSqr;
                        }
                    }
                }
                if (minDistSqr >= 100)
                {
                    valid.Add(candidate);
                }
                else if (minDistSqr > bestDistance)
                {
                    bestDistance = minDistSqr;
                    bestAvailable = candidate;
                }
                limit++;
            }
            if (valid.Count > 0)
            {
                int index = (int)(_scene.FrameCount % (ulong)valid.Count);
                chosenSpawn = valid[index];
            }
            else
            {
                chosenSpawn = bestAvailable;
            }
            if (chosenSpawn != null)
            {
                chosenSpawn.Cooldown = 2 * 2; // todo: FPS stuff
            }
            return chosenSpawn;
        }

        private int GetTimeUntilRespawn()
        {
            // todo: FPS stuff
            int count = 0;
            if (_scene.GameMode != GameMode.Survival && _scene.GameMode != GameMode.SurvivalTeams)
            {
                if (PlayerCount > 3)
                {
                    count = 900 * 2 - _timeSinceDead;
                }
                else if (PlayerCount > 2)
                {
                    count = 600 * 2 - _timeSinceDead;
                }
                else
                {
                    count = 300 * 2 - _timeSinceDead;
                }
            }
            else if (!LoadFlags.TestFlag(LoadFlags.Spawned))
            {
                count = 210 * 2 - _timeSinceDead;
            }
            return count;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Damage)
            {
                TakeDamage((int)info.Param1, DamageFlags.IgnoreInvuln, direction: null, source: null);
            }
            else if (info.Message == Message.Death)
            {
                TakeDamage((int)info.Param1, DamageFlags.Death, direction: null, source: null);
            }
            else if (info.Message == Message.Gravity)
            {
                float gravity = Fixed.ToFloat((int)info.Param1);
                if (!Flags1.TestFlag(PlayerFlags1.Standing) && !Flags2.TestFlag(PlayerFlags2.AltAttack)
                    && gravity != 0 && _jumpPadControlLock == 0)
                {
                    Flags2 |= PlayerFlags2.GravityOverride;
                    _gravity = gravity;
                }
            }
            else if (info.Message == Message.SetCamSeqAi)
            {
                if (IsBot)
                {
                    AiData.Flags2 |= AiFlags2.Bit14;
                }
            }
            else if (info.Message == Message.Impact)
            {
                if (info.Param1 is EntityBase target && target != this
                    && (target.Type == EntityType.EnemyInstance || target.Type == EntityType.Halfturret || target.Type == EntityType.Player))
                {
                    _lastTarget = target;
                    _timeSinceHitTarget = 0;
                    if (info.Sender.Type == EntityType.BeamProjectile)
                    {
                        var beam = (BeamProjectileEntity)info.Sender;
                        if (beam.Beam == BeamType.ShockCoil)
                        {
                            if (target == _shockCoilTarget)
                            {
                                _shockCoilTimer++;
                            }
                            else
                            {
                                _shockCoilTimer = 0;
                                _shockCoilTarget = target;
                            }
                        }
                    }
                }
            }
            else if (info.Message == Message.PreventFormSwitch)
            {
                Flags2 |= PlayerFlags2.NoFormSwitch;
            }
            else if (info.Message == Message.DripMoatPlatform)
            {
                if ((int)info.Param1 == 0)
                {
                    Flags2 &= ~PlayerFlags2.BipedLock;
                }
                else
                {
                    Flags2 |= PlayerFlags2.BipedLock;
                }
            }
        }

        public override void Destroy()
        {
            _soundSource.StopAllSfx();
            if (_furlEffect != null)
            {
                _scene.UnlinkEffectEntry(_furlEffect);
                _furlEffect = null;
            }
            if (_boostEffect != null)
            {
                _scene.UnlinkEffectEntry(_boostEffect);
                _boostEffect = null;
            }
            if (_burnEffect != null)
            {
                _scene.UnlinkEffectEntry(_burnEffect);
                _burnEffect = null;
            }
            if (_chargeEffect != null)
            {
                _scene.UnlinkEffectEntry(_chargeEffect);
                _chargeEffect = null;
            }
            if (_muzzleEffect != null)
            {
                _scene.UnlinkEffectEntry(_muzzleEffect);
                _muzzleEffect = null;
            }
            if (_doubleDmgEffect != null)
            {
                _scene.UnlinkEffectEntry(_doubleDmgEffect);
                _doubleDmgEffect = null;
            }
            if (_deathaltEffect != null)
            {
                _scene.UnlinkEffectEntry(_deathaltEffect);
                _deathaltEffect = null;
            }
            base.Destroy();
        }
    }
}
