using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntityNew
    {
        public override bool Process(Scene scene)
        {
            if (scene.Multiplayer && !LoadFlags.TestFlag(LoadFlags.Connected) && LoadFlags.TestFlag(LoadFlags.WasConnected))
            {
                LoadFlags |= LoadFlags.Disconnected;
                LoadFlags &= ~LoadFlags.Active;
            }
            // sktodo: need to set this flag somewhere
            if (!LoadFlags.TestFlag(LoadFlags.Active))
            {
                if (scene.Multiplayer)
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
            // todo: copy player input unless there's a camseq
            if (Flags1.TestFlag(PlayerFlags1.AltForm))
            {
                Flags1 |= PlayerFlags1.AltFormPrevious;
            }
            PrevPosition = Position;
            // todo: update camera info prev pos
            PrevSpeed = Speed;
            Flags1 &= ~PlayerFlags1.MovingBiped;
            Flags1 &= ~PlayerFlags1.ShotCharged;
            Flags1 &= ~PlayerFlags1.ShotMissile;
            Flags1 &= ~PlayerFlags1.ShotUncharged;
            _field4AC = 0;
            if (_respawnTimer > 0)
            {
                _respawnTimer--;
                // todo: if in survival mode and out of lives, draw HUD message and prevent _respawnTimer from reaching 0
            }
            if (_health == 0)
            {
                if (_respawnTimer == 0) // todo: and this slot was not spawned by an enemy spawner
                {
                    // todo: if we loaded into this room through a door or teleporter, respawn in the right place
                    // else...
                    int time = GetTimeUntilRespawn();
                    if (IsMainPlayer && scene.Multiplayer) // todo: and some global is set
                    {
                        // todo: set HUD model and draw messages
                    }
                    if (!scene.Multiplayer || time <= 0 || IsBot) // todo: or input, or something with wi-fi, or forced
                    {
                        // todo?: something with wifi
                        // else...
                        PlayerSpawnEntity? respawn = GetRespawnPoint();
                        if (respawn != null)
                        {
                            Spawn(respawn.Position, respawn.FacingVector, respawn.UpVector, respawn: true);
                        }
                    }
                }
            }
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
            // todo: update positional audio and SFX
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
                    // todo: draw string if player radar setting is on
                    // else...
                    int revealTime = (_scene.PlayerCount > 2 ? 600 : 300) * 2; // todo: FPS stuff
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
                        // todo: draw HUD string
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
                    _bombAmount = 3;
                }
            }
            else if (Hunter == Hunter.Sylux)
            {
                if (_bombCooldown > 0)
                {
                    _bombAmount = 0;
                }
                else
                {
                    _bombAmount = (byte)(3 - _syluxBombCount);
                }
            }
            else
            {
                _bombAmount = 1;
            }
            if (_bombCooldown > 0)
            {
                _bombCooldown--;
                if (Hunter == Hunter.Kanden && _bombCooldown == 10 * 2)
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
                    Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY, _volume.SpherePosition);
                    _deathaltEffect = _scene.SpawnEffectGetEntry(effectId, transform);
                    _deathaltEffect.SetElementExtension(true);
                }
                else
                {
                    Matrix4 transform = GetTransformMatrix(Vector3.UnitY, Vector3.UnitX);
                    _deathaltEffect.Transform(_volume.SpherePosition, transform);
                }
            }
            else if (_deathaltEffect != null)
            {
                scene.UnlinkEffectEntry(_deathaltEffect);
                _deathaltEffect = null;
            }
            if (Flags2.TestFlag(PlayerFlags2.Cloaking))
            {
                Debug.Assert(_cloakTimer != 0);
                _cloakTimer--;
                if (_cloakTimer > 0)
                {
                    _targetAlpha = 3 / 31f;
                    // todo: update SFX
                }
                else
                {
                    Flags2 &= ~PlayerFlags2.Cloaking;
                    _targetAlpha = 1;
                    // todo: stop and play SFX
                }
            }
            else
            {
                _targetAlpha = 1;
                if ((Hunter == Hunter.Trace || IsPrimeHunter)
                    && _hspeedMag < 0.05f && Speed.Y < 0.05f && Speed.Y > -0.05f) // todo: or prime hunter
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
            else if (IsMorphing || IsUnmorphing)
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
                    WeaponInfo slotInfo = Weapons.Current[(int)slotWeap];
                    if (slotInfo.Priority > priority && ammo >= slotInfo.AmmoCost)
                    {
                        priority = slotInfo.Priority;
                        slot = i;
                    }
                }
                // todo: update HUD
                TryEquipWeapon(_weaponSlots[slot]);
            }
            ProcessInput();
            if (Flags1.TestFlag(PlayerFlags1.Boosting) && _hspeedMag <= Fixed.ToFloat(Values.AltMinHSpeed))
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
                        // todo: update some SFX-related global
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
                            // todo: stop SFX
                            _ammo[i] += _ammoRecovery[i];
                            _ammoRecovery[i] = 0;
                        }
                        else
                        {
                            _ammo[i] += 3;
                            _ammoRecovery[i] -= 3;
                            // todo: update some SFX-related global
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
                // todo: play landing SFX
            }
            UpdateGunAnimation();
            _gunModel.UpdateAnimFrames();
            // todo: update weapon SFX
            PickUpItems();
            // skhere
            return true;
        }

        private void ProcessInput()
        {
            // sktodo: process input
        }

        private void PickUpItems()
        {
            // todo: also return if the following are all true - cur camseq, block input flag set, IsMainPlayer
            if (_health == 0 || (IsBot && !_scene.Multiplayer) || IgnoreItemPickups)
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
                        && between.Y >= Fixed.ToFloat(Values.MinPickupHeight) && between.Y <= Fixed.ToFloat(Values.MaxPickupHeight))
                    {
                        inRange = true;
                    }
                }
                if (!inRange)
                {
                    continue;
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
                        // todo: play SFX
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
                    }
                    else
                    {
                        amount = _scene.Multiplayer ? 50 : 100;
                    }
                    _ammo[slot] += amount;
                    // todo: play SFX
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
                    // todo: play SFX
                    break;
                case ItemType.DoubleDamage:
                    pickedUp = true;
                    _timeSincePickup = 0;
                    _doubleDmgTimer = 900 * 2; // todo: FPS stuff
                    // todo: play SFX
                    break;
                case ItemType.Cloak:
                    pickedUp = true;
                    _timeSincePickup = 0;
                    _cloakTimer = 900 * 2; // todo: FPS stuff
                    Flags2 |= PlayerFlags2.Cloaking;
                    // todo: play SFX
                    break;
                case ItemType.Deathalt:
                    pickedUp = true;
                    _timeSincePickup = 0;
                    _deathaltTimer = 900 * 2; // todo: FPS stuff
                    if (!IsAltForm && !IsMorphing)
                    {
                        SwitchForms(force: true);
                    }
                    // todo: play SFX
                    break;
                case ItemType.EnergyTank:
                    if (!IsBot)
                    {
                        pickedUp = true;
                        _timeSincePickup = 0;
                        _healthMax += Values.EnergyTank;
                        _healthRecovery = _healthMax - _health;
                        // todo: update story save, show dialog
                    }
                    break;
                case ItemType.MissileExpansion:
                    if (!IsBot)
                    {
                        pickedUp = true;
                        _timeSincePickup = 0;
                        _ammoMax[1] += 100;
                        _ammoRecovery[1] = _ammoMax[1] - _ammo[1];
                        // todo: update story save, show dialog
                    }
                    break;
                case ItemType.UAExpansion:
                    if (!IsBot)
                    {
                        pickedUp = true;
                        _timeSincePickup = 0;
                        _ammoMax[0] += 300;
                        _ammoRecovery[0] = _ammoMax[0] - _ammo[0];
                        // todo: update story save, show dialog
                    }
                    break;
                case ItemType.ArtifactKey:
                    pickedUp = true;
                    // todo: play SFX
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
                    return;
                }
                weapon = Weapons.GetAffinityBeam(Hunter);
            }
            else
            {
                weapon = itemType switch
                {
                    ItemType.VoltDriver => BeamType.VoltDriver,
                    ItemType.Battlehammer => BeamType.Battlehamer,
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
            // todo: update story save
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
            }
        }

        private static readonly int[] _healthPickupAmounts = new int[3] { 30, 60, 100 };

        private void GainHealth(int health)
        {
            if (_health > 0)
            {
                if (Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    // todo: split health with halfturret
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

        private void SwitchForms(bool force = false)
        {
            // sktodo
        }

        private PlayerSpawnEntity? GetRespawnPoint()
        {
            PlayerSpawnEntity? chosenSpawn = null;
            int limit = 0;
            var valid = new List<PlayerSpawnEntity>();
            PlayerSpawnEntity? bestAvailable = null;
            float bestDistance = 0;
            // the game iterates 25 entities starting with the first spawn point; we iterate 25 spawn points
            // --> shouldn't matter since spawn points are meant to be together in the entity list
            for (int i = 0; i < _scene.Entities.Count && limit < 25; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.PlayerSpawn)
                {
                    continue;
                }
                var candidate = (PlayerSpawnEntity)entity;
                if (candidate.IsActive && candidate.Cooldown == 0 && (_scene.FrameCount > 0 || !candidate.Availability))
                {
                    // todo: if CTF mode, check team index
                    float minDistSqr = 100;
                    for (int j = 0; j < _scene.Entities.Count; j++)
                    {
                        EntityBase player = _scene.Entities[i];
                        if (player.Type != EntityType.Player)
                        {
                            continue;
                        }
                        Vector3 between = candidate.Position - player.Position;
                        float distSqr = Vector3.Dot(between, between);
                        if (distSqr < minDistSqr)
                        {
                            minDistSqr = distSqr;
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
            if (_scene.GameMode == GameMode.Survival || _scene.GameMode == GameMode.SurvivalTeams)
            {
                if (_scene.PlayerCount > 3)
                {
                    count = 900 * 2 - _timeSinceDead;
                }
                else if (_scene.PlayerCount > 2)
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

        protected override LightInfo GetLightInfo(Scene scene)
        {
            return new LightInfo(_light1Vector, _light1Color, _light2Vector, _light2Color);
        }

        public override void GetDrawInfo(Scene scene)
        {
            // sktodo
        }
    }
}
