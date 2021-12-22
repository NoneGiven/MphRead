using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private void ProcessInput()
        {
            if (_health > 0)
            {
                if (Flags1.TestFlag(PlayerFlags1.FreeLook))
                {
                    Flags1 |= PlayerFlags1.FreeLookPrevious;
                }
                else
                {
                    Flags1 &= ~PlayerFlags1.FreeLookPrevious;
                }
                Flags1 &= ~PlayerFlags1.FreeLook;
                Flags1 &= ~PlayerFlags1.Walking;
                Flags1 &= ~PlayerFlags1.Strafing;
                if (!IsBot)
                {
                    ProcessTouchInput();
                }
                if (_frozenTimer > 0)
                {
                    _frozenTimer--;
                    _timeSinceFrozen = 0;
                    if (_frozenTimer == 0)
                    {
                        // todo: play SFX
                        if (IsAltForm)
                        {
                            CreateIceBreakEffectAlt();
                        }
                        else if (IsMainPlayer)
                        {
                            CreateIceBreakEffectGun();
                        }
                        else if (Flags2.TestFlag(PlayerFlags2.DrawnThirdPerson))
                        {
                            int lod = Flags2.TestFlag(PlayerFlags2.Lod1) ? 1 : 0;
                            CreateIceBreakEffectBiped(_bipedModelLods[lod].Model, _modelTransform);
                        }
                    }
                }
                if (_frozenGfxTimer > 0)
                {
                    _frozenGfxTimer--;
                    if (IsMainPlayer && _frozenGfxTimer == 0)
                    {
                        // todo: update HUD
                    }
                }
                if (_timeSinceFrozen != UInt16.MaxValue)
                {
                    _timeSinceFrozen++;
                }
            }
            if (IsAltForm || IsMorphing)
            {
                ProcessAlt();
            }
            else
            {
                ProcessBiped();
            }
        }

        private void ProcessTouchInput()
        {
            // todo: touch input
        }

        private void ProcessBiped()
        {
            // todo: set a field if cam seq, main player, and 1P mode
            if (EquipInfo.SmokeLevel < EquipInfo.Weapon.SmokeDrain)
            {
                EquipInfo.SmokeLevel = 0;
            }
            else
            {
                EquipInfo.SmokeLevel -= EquipInfo.Weapon.SmokeDrain;
            }
            Vector3 speedDelta = Vector3.Zero;
            PlayerAnimation anim1 = PlayerAnimation.None;
            PlayerAnimation anim2 = PlayerAnimation.None;
            AnimFlags animFlags1 = AnimFlags.None;
            AnimFlags animFlags2 = AnimFlags.None;
            if (_frozenTimer == 0 && _health > 0 && _field6D0 == 0)
            {
                if (Biped1Anim == PlayerAnimation.Turn)
                {
                    if (Biped1Frame <= Biped1FrameCount / 2)
                    {
                        Biped1Flags |= AnimFlags.Reverse;
                    }
                    else
                    {
                        Biped1Flags &= ~AnimFlags.Reverse;
                    }
                    Biped1Flags |= AnimFlags.NoLoop;
                }
                if (Biped2Anim == PlayerAnimation.Turn)
                {
                    if (Biped2Frame <= Biped2FrameCount / 2)
                    {
                        Biped2Flags |= AnimFlags.Reverse;
                    }
                    else
                    {
                        Biped2Flags &= ~AnimFlags.Reverse;
                    }
                    Biped2Flags |= AnimFlags.NoLoop;
                }
                if (Controls.MouseAim)
                {
                    // itodo: aim input
                }
                else if (Controls.KeyboardAim)
                {
                    // itodo: aim input
                }
                bool jumping = false;
                if (!Flags2.TestFlag(PlayerFlags2.BipedLock | PlayerFlags2.BipedStuck))
                {
                    // unimpl-controls: the game also tests for either the free strafe flag, or the strafe button held
                    // and later, for up/down, it tests for either flag, or the look button not held

                    void MoveRightLeft(PlayerAnimation walkAnim, int sign)
                    {
                        Flags1 |= PlayerFlags1.Strafing;
                        Flags1 |= PlayerFlags1.MovingBiped;
                        if (Flags1.TestFlag(PlayerFlags1.Standing))
                        {
                            Flags1 |= PlayerFlags1.Walking;
                        }
                        else
                        {
                            Flags1 &= ~PlayerFlags1.Walking;
                        }
                        float traction = Fixed.ToFloat(Values.StrafeBipedTraction);
                        if (_jumpPadControlLockMin > 0)
                        {
                            traction *= Fixed.ToFloat(Values.JumpPadSlideFactor);
                        }
                        else if (Flags1.TestFlag(PlayerFlags1.Standing) && _slipperiness != 0)
                        {
                            traction *= Metadata.TractionFactors[_slipperiness];
                        }
                        speedDelta.X -= _field78 * traction * sign;
                        speedDelta.Z -= _field7C * traction * sign;
                        if (!Controls.MoveUp.IsDown && !Controls.MoveDown.IsDown
                            && Flags1.TestFlag(PlayerFlags1.Grounded) && _timeSinceJumpPad > 7 * 2) // todo: FPS stuff
                        {
                            anim1 = walkAnim;
                        }
                        if (!EquipInfo.Zoomed)
                        {
                            // todo: update field684 (using sign)
                        }
                    }

                    void MoveForwardBack(PlayerAnimation walkAnim, int sign)
                    {
                        Flags1 |= PlayerFlags1.MovingBiped;
                        if (Flags1.TestFlag(PlayerFlags1.Standing))
                        {
                            Flags1 |= PlayerFlags1.Walking;
                        }
                        else
                        {
                            Flags1 &= ~PlayerFlags1.Walking;
                        }
                        float traction = Fixed.ToFloat(Values.WalkBipedTraction);
                        if (_jumpPadControlLockMin > 0)
                        {
                            traction *= Fixed.ToFloat(Values.JumpPadSlideFactor);
                        }
                        else if (Flags1.TestFlag(PlayerFlags1.Standing) && _slipperiness != 0)
                        {
                            traction *= Metadata.TractionFactors[_slipperiness];
                        }
                        speedDelta.X += _field70 * traction * sign;
                        speedDelta.Z += _field74 * traction * sign;
                        if (Flags1.TestFlag(PlayerFlags1.Grounded) && _timeSinceJumpPad > 7 * 2) // todo: FPS stuff
                        {
                            anim1 = walkAnim;
                        }
                        if (!EquipInfo.Zoomed)
                        {
                            // todo: update field688 (using sign)
                        }
                    }

                    if (Controls.MoveRight.IsDown)
                    {
                        MoveRightLeft(PlayerAnimation.WalkRight, sign: 1);
                    }
                    else if (Controls.MoveLeft.IsDown)
                    {
                        MoveRightLeft(PlayerAnimation.WalkLeft, sign: -1);
                    }
                    // todo: update HUD x shift
                    // todo: update field684
                    if (Controls.MoveUp.IsDown)
                    {
                        MoveForwardBack(PlayerAnimation.WalkForward, sign: 1);
                    }
                    else if (Controls.MoveDown.IsDown)
                    {
                        MoveForwardBack(PlayerAnimation.WalkBackward, sign: -1);
                    }
                    // todo: update HUD y shift
                    // todo: update field684
                    // unimpl-controls: in the up/down code path, the game processes aim reset if that flag is off
                    if (_jumpPadControlLockMin == 0 && Controls.Jump.IsPressed
                        && !Flags1.TestAny(PlayerFlags1.NoAimInput | PlayerFlags1.UsedJump))
                    {
                        // unimpl-controls: double tap jump is hard coded as an alternate condition to the jump input
                        jumping = true;
                        if (!Flags1.TestFlag(PlayerFlags1.Standing) || !_abilities.TestFlag(AbilityFlags.SpaceJump))
                        {
                            Flags1 |= PlayerFlags1.UsedJump;
                        }
                        if (IsPrimeHunter)
                        {
                            Speed = Speed.WithY(0.35f); // todo: FPS stuff?
                        }
                        else
                        {
                            Speed = Speed.WithY(Fixed.ToFloat(Values.JumpSpeed)); // todo: FPS stuff?
                        }
                        _timeSinceGrounded = 8 * 2; // todo: FPS stuff
                        // todo: play SFX
                    }
                }
                // unimpl-controls: the game attempts to play free look SFX, but they don't exist
                // --> it does this in between L/R/U/D and jump, while we have jump in the condition above
                if (jumping || _timeSinceJumpPad == 1)
                {
                    animFlags1 = AnimFlags.NoLoop;
                    if (Controls.MoveUp.IsDown)
                    {
                        anim1 = PlayerAnimation.JumpForward;
                    }
                    else if (Controls.MoveDown.IsDown)
                    {
                        anim1 = PlayerAnimation.JumpBack;
                    }
                    else if (Controls.MoveLeft.IsDown)
                    {
                        anim1 = PlayerAnimation.JumpLeft;
                    }
                    else if (Controls.MoveRight.IsDown)
                    {
                        anim1 = PlayerAnimation.JumpRight;
                    }
                    else
                    {
                        anim1 = PlayerAnimation.JumpNeutral;
                    }
                }
            }
            ProcessMovement();
            UpdateCamera();
            UpdateAimVecs();
            if (_frozenTimer == 0 && _health > 0 && _field6D0 == 0)
            {
                // todo: scan visor
                // else...
                if (!IsUnmorphing)
                {
                    if (Controls.Shoot.IsPressed || (Controls.Shoot.IsDown && Flags2.TestFlag(PlayerFlags2.NoShotsFired)))
                    {
                        Flags2 |= PlayerFlags2.Shooting;
                        Flags2 &= ~PlayerFlags2.NoShotsFired;
                    }
                    else if (!Controls.Shoot.IsDown)
                    {
                        Flags2 &= ~PlayerFlags2.Shooting;
                    }
                    if (!_availableCharges[CurrentWeapon] || !EquipWeapon.Flags.TestFlag(WeaponFlags.CanCharge))
                    {
                        EquipInfo.ChargeLevel = 0;
                    }
                    else
                    {
                        bool releaseCharge = false;
                        if (!Flags2.TestFlag(PlayerFlags2.Shooting) || EquipInfo.GetAmmo() < EquipWeapon.ChargeCost)
                        {
                            releaseCharge = true; // charge released/insufficient
                        }
                        else
                        {
                            if (EquipInfo.ChargeLevel > 0 && GunAnimation != GunAnimation.MissileClose)
                            {
                                // todo: play SFX
                                if (Biped2Flags.TestFlag(AnimFlags.Ended) || Biped2Anim == PlayerAnimation.Charge
                                    || Biped2Anim == PlayerAnimation.Shoot && Biped2Frame > 8)
                                {
                                    anim2 = PlayerAnimation.Charge;
                                }
                            }
                            if (EquipInfo.ChargeLevel >= EquipWeapon.FullCharge * 2) // todo: FPS stuff
                            {
                                EquipInfo.SmokeLevel += EquipWeapon.SmokeChargeAmount;
                                EquipInfo.SmokeLevel = (ushort)Math.Min(EquipInfo.SmokeLevel, EquipWeapon.SmokeStart * 2); // todo: FPS stuff
                            }
                            else
                            {
                                EquipInfo.ChargeLevel++;
                                int minCharge = EquipWeapon.MinCharge * 2; // todo: FPS stuff
                                if (EquipInfo.ChargeLevel > minCharge)
                                {
                                    int fullCharge = EquipWeapon.FullCharge * 2; // todo: FPS stuff
                                    int chargeCost = EquipWeapon.ChargeCost * 2; // todo: FPS stuff
                                    int minCost = EquipWeapon.MinChargeCost * 2; // todo: FPS stuff
                                    int cost = minCost + (chargeCost - minCost) * (EquipInfo.ChargeLevel - minCharge) / (fullCharge - minCharge);
                                    if (EquipInfo.GetAmmo() < cost)
                                    {
                                        EquipInfo.ChargeLevel--;
                                    }
                                }
                            }
                            // todo?: auto release
                        }
                        if (releaseCharge)
                        {
                            if (EquipInfo.ChargeLevel >= 2 * 2) // todo: FPS stuff
                            {
                                // todo: stop SFX
                            }
                            if (EquipInfo.ChargeLevel >= EquipWeapon.MinCharge * 2) // todo: FPS stuff
                            {
                                TryFireWeapon();
                                anim2 = PlayerAnimation.ChargeShoot;
                                animFlags2 = AnimFlags.NoLoop;
                            }
                            EquipInfo.ChargeLevel = 0;
                        }
                    }
                    if (EquipWeapon.Flags.TestFlag(WeaponFlags.CanZoom))
                    {
                        if (Controls.Zoom.IsPressed)
                        {
                            UpdateZoom(!EquipInfo.Zoomed);
                        }
                        if (EquipInfo.Zoomed)
                        {
                            // todo: zoom camera FOV
                        }
                    }
                    if (Controls.Shoot.IsPressed && EquipInfo.ChargeLevel <= 1 * 2 // todo: FPS stuff
                        || EquipWeapon.Flags.TestFlag(WeaponFlags.RepeatFire) && Flags2.TestFlag(PlayerFlags2.Shooting)
                        && (!EquipWeapon.Flags.TestFlag(WeaponFlags.CanCharge) || EquipInfo.ChargeLevel < EquipWeapon.MinCharge * 2)) // todo: FPS stuff
                    {
                        if (TryFireWeapon())
                        {
                            anim2 = PlayerAnimation.Shoot;
                            animFlags2 |= AnimFlags.NoLoop;
                            if (Biped2Anim == PlayerAnimation.Shoot)
                            {
                                SetBiped2Animation(PlayerAnimation.Shoot, Biped2Flags);
                            }
                        }
                    }
                    // todo: or if main player in cam seq which forces alt
                    if (!Flags2.TestFlag(PlayerFlags2.BipedStuck) && _abilities.TestFlag(AbilityFlags.AltForm) && Controls.Morph.IsPressed)
                    {
                        // the game doesn't require pressed here, but presumably the control scheme would have the pressed flag
                        // todo: use the ability flag for the morph touch button too, even though the game doesn't
                        TrySwitchForms();
                        anim1 = PlayerAnimation.Morph;
                        anim2 = PlayerAnimation.Morph;
                    }
                }
                float magBefore = MathF.Sqrt(Speed.X * Speed.X + Speed.Z * Speed.Z);
                Speed += speedDelta; // todo: FPS stuff?
                float magAfter = MathF.Sqrt(Speed.X * Speed.X + Speed.Z * Speed.Z);
                if (magAfter > magBefore && magAfter > _hSpeedCap)
                {
                    float factor;
                    if (magBefore <= _hSpeedCap)
                    {
                        factor = _hSpeedCap / magAfter;
                    }
                    else
                    {
                        factor = magBefore / magAfter;
                    }
                    Speed = Speed.WithX(Speed.X * factor).WithZ(Speed.Z * factor);
                }
                if (EquipInfo.Zoomed)
                {
                    Vector3 facing = FacingVector;
                    Vector3 diff = _gunVec1 - facing;
                    facing += diff * 0.3f / 2; // todo: FPS stuff
                    SetTransform(facing.Normalized(), UpVector, Position);
                }
                if (anim1 == PlayerAnimation.None)
                {
                    if (Flags1.TestFlag(PlayerFlags1.Grounded))
                    {
                        if (Biped1Anim == PlayerAnimation.Idle && ++_timeIdle > 300 * 2 && _timeSinceInput > 300 * 2) // todo: FPS stuff
                        {
                            SetBiped1Animation(PlayerAnimation.Flourish, AnimFlags.NoLoop);
                        }
                        else if (!Biped1Flags.TestFlag(AnimFlags.NoLoop) || Biped1Flags.TestFlag(AnimFlags.Ended))
                        {
                            _timeIdle = 0;
                            SetBiped1Animation(PlayerAnimation.Idle, AnimFlags.None);
                        }
                    }
                }
                else if (anim1 != Biped1Anim || anim1 == PlayerAnimation.JumpForward || anim1 == PlayerAnimation.JumpBack
                    || anim1 == PlayerAnimation.JumpLeft || anim1 == PlayerAnimation.JumpRight || anim1 == PlayerAnimation.JumpNeutral)
                {
                    SetBiped1Animation(anim1, animFlags1);
                }
                if (anim2 == PlayerAnimation.None)
                {
                    if ((!Biped2Flags.TestFlag(AnimFlags.NoLoop) || Biped2Flags.TestFlag(AnimFlags.Ended)) && Biped2Anim != Biped1Anim)
                    {
                        SetBiped2Animation(Biped1Anim, Biped1Flags);
                        _bipedModel2.AnimInfo.Frame[0] = Biped1Frame;
                    }
                }
                else if (anim2 != Biped2Anim)
                {
                    SetBiped2Animation(anim2, animFlags2);
                }
            }
        }

        private bool TryFireWeapon()
        {
            if (!Flags2.TestFlag(PlayerFlags2.Cloaking))
            {
                _cloakTimer = 0;
            }
            if (AttachedEnemy != null)
            {
                return false;
            }
            bool pressed = Controls.Shoot.IsPressed;
            if (pressed || CurrentWeapon != BeamType.PowerBeam)
            {
                _autofireCooldown = (ushort)(EquipWeapon.AutofireCooldown * 2); // todo: FPS stuff
                _powerBeamAutofire = 0;
            }
            else
            {
                if (_powerBeamAutofire < UInt16.MaxValue)
                {
                    _powerBeamAutofire++;
                }
                // basically adds 0, 1, or 2 to the base autofire cooldown depending on how long the PB has repeated fire
                // --> could add more, but the min charge is reaached quickly
                int pbAuto = Math.Min((int)_powerBeamAutofire, 90 * 2); // todo: FPS stuff
                pbAuto = (int)(pbAuto * 15 * 2 / (90f * 2));
                _autofireCooldown = (ushort)((pbAuto + EquipWeapon.AutofireCooldown) * 2); // todo: FPS stuff
            }
            // todo: autofire cooldown case can be bypassed if a certain bot AI flag is set
            if (_timeSinceShot < EquipWeapon.ShotCooldown * 2 // todo: FPS stuff
                || !pressed && _timeSinceShot < _autofireCooldown
                || GunAnimation == GunAnimation.UpDown)
            {
                return false;
            }
            Vector3 shotVec = _aimPosition - _muzzlePos;
            if (_disruptedTimer > 0)
            {
                // random values between -3 and 3
                shotVec.X += Fixed.ToFloat((int)Rng.GetRandomInt2(24576) - 12288);
                shotVec.Y += Fixed.ToFloat((int)Rng.GetRandomInt2(24576) - 12288);
                shotVec.Z += Fixed.ToFloat((int)Rng.GetRandomInt2(24576) - 12288);
            }
            shotVec = shotVec.Normalized();
            WeaponInfo curWeapon = EquipInfo.Weapon;
            if (IsPrimeHunter)
            {
                // todo?: make this more solid to avoid e.g. the battlehammer ammo cost thing
                EquipInfo.Weapon = Weapons.Current[(int)CurrentWeapon + 9];
            }
            if (IsBot && !_scene.Multiplayer)
            {
                // todo: update bot 1P weapon
            }
            BeamSpawnFlags flags = BeamSpawnFlags.NoMuzzle;
            if (_doubleDmgTimer > 0)
            {
                flags |= BeamSpawnFlags.DoubleDamage;
            }
            else if (IsPrimeHunter)
            {
                flags |= BeamSpawnFlags.PrimeHunter;
            }
            BeamResultFlags result = BeamProjectileEntity.Spawn(this, EquipInfo, _muzzlePos, shotVec, flags, _scene);
            if (result == BeamResultFlags.NoSpawn)
            {
                EquipInfo.Weapon = curWeapon;
                // todo: play SFX
                return false;
            }
            // todo: update license stats
            _timeSinceShot = 0;
            // todo: update HUD
            if (CurrentWeapon == BeamType.Missile)
            {
                Flags1 |= PlayerFlags1.ShotMissile;
            }
            if (EquipInfo.ChargeLevel < EquipWeapon.MinCharge * 2) // todo: FPS stuff
            {
                Flags1 |= PlayerFlags1.ShotUncharged;
            }
            else
            {
                Flags1 |= PlayerFlags1.ShotCharged;
            }
            if (_muzzleEffect == null || !EquipWeapon.Flags.TestFlag(WeaponFlags.Continuous))
            {
                if (_muzzleEffect != null)
                {
                    _scene.UnlinkEffectEntry(_muzzleEffect);
                    _muzzleEffect = null;
                }
                int effectId = Metadata.MuzzleEffectIds[(int)CurrentWeapon];
                _muzzleEffect = _scene.SpawnEffectGetEntry(effectId, _gunVec2, _gunVec1, _muzzlePos);
                if (!IsMainPlayer)
                {
                    _muzzleEffect.SetDrawEnabled(false);
                }
            }
            // todo: play SFX
            EquipInfo.Weapon = curWeapon;
            UnequipOmegaCannon(); // todo?: set the flag if wifi
            return true;
        }

        private void ProcessAlt()
        {
            // sktodo
        }

        private void ProcessMovement()
        {
            if (_accelerationTimer > 0)
            {
                _accelerationTimer--;
                Speed += Acceleration / 2; // todo: FPS stuff
            }
            var hSpeed = new Vector3(Speed.X, 0, Speed.Z);
            float hSpeedMag = hSpeed.Length;
            if (hSpeedMag == 0)
            {
                _hspeedMag = 0;
            }
            else
            {
                hSpeed /= hSpeedMag;
                if (Values.AltGroundedNoGrav == 0)
                {
                    if (hSpeedMag > Fixed.ToFloat(Values.Field5C)) // todo: FPS stuff?
                    {
                        _field80 = hSpeed.X;
                        _field84 = hSpeed.Z;
                    }
                    if ((IsAltForm || IsMorphing) && hSpeedMag > Fixed.ToFloat(Values.Field58)) // todo: FPS stuff?
                    {
                        _field70 = hSpeed.X;
                        _field74 = hSpeed.Z;
                        var face = new Vector3(_field70, 0, _field74);
                        _gunVec1 = face;
                        float add = _gunVec1.X * Fixed.ToFloat(Values.AimDistance);
                        _aimPosition = Position.AddX(add).AddZ(add);
                        SetTransform(face, UpVector, Position);
                    }
                }
                if (IsAltForm)
                {
                    float altMin = Fixed.ToFloat(Values.AltMinHSpeed); // todo: FPS stuff?
                    if (_hSpeedCap <= altMin)
                    {
                        _hSpeedCap = altMin;
                    }
                    else if (_hspeedMag > _hSpeedCap)
                    {
                        _hSpeedCap -= Fixed.ToFloat(Values.AltHSpeedCapIncrement); // todo: FPS stuff?
                    }
                    else
                    {
                        _hSpeedCap = _hspeedMag;
                    }
                }
                else
                {
                    bool strafing = Flags1.TestFlag(PlayerFlags1.Strafing);
                    _hSpeedCap = Fixed.ToFloat(strafing ? Values.StrafeSpeedCap : Values.WalkSpeedCap); // todo: FPS stuff?
                }
                if (IsPrimeHunter && !IsAltForm)
                {
                    _hSpeedCap = 0.4f; // todo: FPS stuff?
                }
                _hspeedMag = hSpeedMag;
            }
            // todo: check how much of this overwrites stuff done above
            Vector3 facing = FacingVector;
            float hMag = MathF.Sqrt(facing.X * facing.X + facing.Z * facing.Z);
            _field70 = facing.X / hMag;
            _field74 = facing.Z / hMag;
            _gunVec2 = new Vector3(_field74, 0, -_field70);
            _field78 = _gunVec2.X;
            _field7C = _gunVec2.Z;
            Vector3 up = Vector3.Cross(facing, _gunVec2).Normalized();
            SetTransform(facing, up, Position);
            if (Values.AltGroundedNoGrav != 0)
            {
                _field80 = _field70;
                _field84 = _field74;
            }
            _aimPosition = _gunVec1 * Fixed.ToFloat(Values.AimDistance);
            _aimPosition += _scene.CameraPosition; // todo: use camera info pos
            // unimpl-controls: this calculation is different when exact aim is not set
            hMag = MathF.Sqrt(_gunVec1.X * _gunVec1.X + _gunVec1.Z * _gunVec1.Z);
            _field88 = MathHelper.DegreesToRadians(MathF.Atan2(_gunVec1.Y, hMag));
            if (_field88 > 75 || _field88 < -75)
            {
                // todo: update aim
            }
            if (Flags1.TestFlag(PlayerFlags1.UsedJumpPad))
            {
                // todo: FPS stuff
                float prevX = Speed.X;
                Speed = Speed.AddX(-_jumpPadAccel.X / 2);
                if (prevX <= 0 && Speed.X > 0 || prevX > 0 && Speed.X < 0)
                {
                    _jumpPadAccel.X += Speed.X / 2;
                    Speed = Speed.WithX(0);
                }
                float prevZ = Speed.Z;
                Speed = Speed.AddZ(-_jumpPadAccel.Z / 2);
                if (prevZ <= 0 && Speed.Z > 0 || prevZ > 0 && Speed.Z < 0)
                {
                    _jumpPadAccel.Z += Speed.Z / 2;
                    Speed = Speed.WithZ(0);
                }
            }
            float slideSfxPct = 0;
            float speedFactor;
            if (IsAltForm || IsMorphing)
            {
                if (Flags2.TestFlag(PlayerFlags2.AltAttack) && (Hunter == Hunter.Trace || Hunter == Hunter.Weavel))
                {
                    speedFactor = 0.96f;
                }
                else if (Flags1.TestFlag(PlayerFlags1.Standing))
                {
                    speedFactor = Fixed.ToFloat(Values.AltGroundSpeedFactor);
                }
                else
                {
                    speedFactor = Fixed.ToFloat(Values.AirSpeedFactor);
                }
            }
            else if (Flags1.TestFlag(PlayerFlags1.Standing))
            {
                if (Flags1.TestFlag(PlayerFlags1.Strafing))
                {
                    speedFactor = Fixed.ToFloat(Values.StrafeSpeedFactor);
                }
                else if (Flags1.TestFlag(PlayerFlags1.Walking))
                {
                    speedFactor = Fixed.ToFloat(Values.WalkSpeedFactor);
                }
                else
                {
                    speedFactor = Fixed.ToFloat(Values.StandSpeedFactor);
                }
            }
            else
            {
                speedFactor = Fixed.ToFloat(Values.AirSpeedFactor);
            }
            if (Flags1.TestFlag(PlayerFlags1.Standing) && _slipperiness != 0)
            {
                speedFactor += (1 - speedFactor) * Metadata.SlipSpeedFactors[_slipperiness];
                if (!Flags1.TestFlag(PlayerFlags1.MovingBiped))
                {
                    slideSfxPct = _hspeedMag / _hSpeedCap * (16 - 1 / 4096f);
                }
            }
            // todo: play SFX (with slideSfxPct)
            Speed = Speed.WithX(Speed.X * speedFactor).WithZ(Speed.Z * speedFactor);
            if (Flags1.TestFlag(PlayerFlags1.UsedJumpPad))
            {
                Speed = Speed.AddX(_jumpPadAccel.X / 2); // todo: FPS stuff
                Speed = Speed.AddZ(_jumpPadAccel.Z / 2); // todo: FPS stuff
            }
            if (Flags1.TestFlag(PlayerFlags1.Standing) && _timeSinceJumpPad > 5 * 2) // todo: FPS stuff
            {
                _lastJumpPad = null;
                Flags1 &= ~PlayerFlags1.UsedJumpPad;
                _jumpPadControlLock = 0;
                _jumpPadControlLockMin = 0;
            }
            if (IsAltForm)
            {
                Flags2 |= PlayerFlags2.AltFormGravity;
            }
            else if (Speed.Y <= 0.01f)
            {
                Flags2 &= ~PlayerFlags2.AltFormGravity;
            }
            if (_health > 0)
            {
                if (_jumpPadControlLock == 0 && !Flags2.TestFlag(PlayerFlags2.BipedStuck))
                {
                    if (Flags2.TestFlag(PlayerFlags2.GravityOverride))
                    {
                        Flags2 &= ~PlayerFlags2.GravityOverride;
                    }
                    else
                    {
                        if (IsAltForm || Flags2.TestFlag(PlayerFlags2.AltFormGravity))
                        {
                            if (Flags1.TestFlag(PlayerFlags1.Standing) && _slipperiness == 0 && Values.AltGroundedNoGrav != 0)
                            {
                                _gravity = 0;
                            }
                            else if (Flags1.TestFlag(PlayerFlags1.Standing))
                            {
                                _gravity = Fixed.ToFloat(Values.AltGroundGravity);
                            }
                            else
                            {
                                _gravity = Fixed.ToFloat(Values.AltAirGravity);
                            }
                        }
                        else if (Flags1.TestFlag(PlayerFlags1.Standing) && _slipperiness == 0)
                        {
                            _gravity = 0;
                        }
                        else
                        {
                            _gravity = Fixed.ToFloat(Values.BipedGravity);
                        }
                    }
                    Speed = Speed.AddY(_gravity / 2); // todo: FPS stuff
                }
                Vector3 position = Position + Speed / 2; // todo: FPS stuff
                if (AttachedEnemy?.EnemyType == EnemyType.Quadtroid)
                {
                    position.X = Position.X;
                    position.Z = Position.Z;
                }
                Position = position;
                // unimpl-controls: the game does more calculation here if exact aim is off
                // --> does so outside of the _health > 0 condition, before the player collision check (which is inside another _health > 0)
                CheckPlayerCollision();
            }
            if (Hunter == Hunter.Kanden && IsAltForm && Flags1.TestFlag(PlayerFlags1.Standing))
            {
                for (int i = 1; i < _kandenSegPos.Length; i++)
                {
                    _kandenSegPos[i] = _kandenSegPos[i].AddY(-0.1f);
                }
            }
            if (_standingEntCol != null)
            {
                Vector3 position = Matrix.Vec3MultMtx4(Position, _standingEntCol.Inverse2);
                Position = Matrix.Vec3MultMtx4(position, _standingEntCol.Transform);
            }
            if (!Flags1.TestFlag(PlayerFlags1.CollidingLateral))
            {
                _horizColTimer = 0;
            }
            else if (_horizColTimer != UInt16.MaxValue)
            {
                _horizColTimer++;
            }
            bool standingPrev = Flags1.TestFlag(PlayerFlags1.Standing);
            bool noUnmorphPev = Flags1.TestFlag(PlayerFlags1.NoUnmorph);
            Flags1 &= ~PlayerFlags1.Standing;
            Flags1 &= ~PlayerFlags1.StandingPrevious;
            Flags1 &= ~PlayerFlags1.NoUnmorph;
            Flags1 &= ~PlayerFlags1.NoUnmorphPrevious;
            Flags1 &= ~PlayerFlags1.OnLava;
            Flags1 &= ~PlayerFlags1.OnAcid;
            Flags2 &= ~PlayerFlags2.SpireClimbing;
            Flags1 &= ~PlayerFlags1.CollidingLateral;
            Flags1 &= ~PlayerFlags1.NoUnmorph;
            Flags1 &= ~PlayerFlags1.CollidingEntity;
            Flags1 &= ~PlayerFlags1.Standing;
            if (standingPrev)
            {
                Flags1 |= PlayerFlags1.StandingPrevious;
            }
            if (noUnmorphPev)
            {
                Flags1 |= PlayerFlags1.NoUnmorphPrevious;
            }
            Vector3 prevC0 = _fieldC0;
            _fieldC0 = Vector3.Zero;
            CheckCollision();
            if (_field449 > 0 && _field449 < 30 * 2) // todo: FPS stuff
            {
                _fieldC0 = prevC0;
            }
            else if (_fieldC0 != Vector3.Zero)
            {
                _fieldC0 = _fieldC0.Normalized();
            }
            else
            {
                _fieldC0 = Vector3.UnitY;
            }
            // todo: stop SFX if terrain changed
            if (Flags1.TestFlag(PlayerFlags1.Standing) && !Flags1.TestFlag(PlayerFlags1.StandingPrevious))
            {
                // landing
                _timeStanding = 0;
                if (PrevSpeed.Y >= 0)
                {
                    _field44C = 0;
                }
                else
                {
                    _field44C = -PrevSpeed.Y * 0.35f;
                    if (_field44C > Fixed.ToFloat(800))
                    {
                        _field44C = Fixed.ToFloat(800);
                    }
                    if (PrevSpeed.Y < -0.65f)
                    {
                        // todo: set camera shake
                    }
                }
            }
            else if (_timeStanding != UInt16.MaxValue)
            {
                _timeStanding++;
            }
            if (IsAltForm)
            {
                UpdateAltTransform();
            }
            if (Flags1.TestFlag(PlayerFlags1.Grounded))
            {
                Flags1 |= PlayerFlags1.GroundedPrevious;
            }
            else
            {
                Flags1 &= ~PlayerFlags1.GroundedPrevious;
            }
            if (Flags1.TestFlag(PlayerFlags1.Standing) || Flags2.TestFlag(PlayerFlags2.SpireClimbing))
            {
                _field438 = _timeSinceGrounded;
                _timeSinceGrounded = 0;
                Flags1 |= PlayerFlags1.Grounded;
            }
            else if (_timeSinceGrounded < 90 * 2) // todo: FPS stuff
            {
                _timeSinceGrounded++;
                if (_timeSinceGrounded >= 8 * 2) // todo: FPS stuff
                {
                    Flags1 &= ~PlayerFlags1.Grounded;
                    // todo: clear SFX field
                }
            }
            bool burning = false;
            if (_health > 0 && (_burnTimer > 0 || Hunter != Hunter.Spire
                && Flags1.TestFlag(PlayerFlags1.OnLava) && Flags1.TestFlag(PlayerFlags1.Grounded)))
            {
                burning = true;
            }
            // todo: update burning SFX
            if ((!IsAltForm || Hunter == Hunter.Weavel) && Flags1.TestFlag(PlayerFlags1.Grounded))
            {
                if (Flags1.TestFlag(PlayerFlags1.MovingBiped))
                {
                    // todo: play SFX
                }
                else
                {
                    // todo: stop SFX
                }
            }
            // skhere
        }

        private void UpdateCamera()
        {
            // sktodo
        }

        // sktodo: aim input (after camera)
        public static void ProcessInput(KeyboardState keyboardState, MouseState mouseState)
        {
            KeyboardState keyboardSnap = keyboardState.GetSnapshot();
            MouseState mouseSnap = mouseState.GetSnapshot();
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = Players[i];
                KeyboardState? prevKeyboardSnap = player.Input.KeyboardState;
                MouseState? prevMouseSnap = player.Input.MouseState;
                player.Input.PrevKeyboardState = prevKeyboardSnap;
                player.Input.PrevMouseState = prevMouseSnap;
                player.Input.KeyboardState = keyboardSnap;
                player.Input.MouseState = mouseSnap;
                if (player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    for (int j = 0; j < player.Controls.All.Length; j++)
                    {
                        ButtonControl control = player.Controls.All[j];
                        if (control.IsKey == true)
                        {
                            bool prevDown = prevKeyboardSnap?.IsKeyDown(control.Key) ?? false;
                            control.IsDown = keyboardSnap.IsKeyDown(control.Key);
                            control.IsPressed = control.IsDown && !prevDown;
                            control.IsReleased = !control.IsDown && prevDown;
                        }
                        else if (control.IsKey == false)
                        {
                            bool prevDown = prevMouseSnap?.IsButtonDown(control.MouseButton) ?? false;
                            control.IsDown = mouseSnap!.IsButtonDown(control.MouseButton);
                            control.IsPressed = control.IsDown && !prevDown;
                            control.IsReleased = !control.IsDown && prevDown;
                        }
                    }
                }
            }
        }

        private PlayerControls Controls { get; } = PlayerControls.GetDefault();
        private PlayerInput Input { get; } = new PlayerInput();

        private class PlayerInput
        {
            public KeyboardState? PrevKeyboardState { get; set; }
            public KeyboardState? KeyboardState { get; set; }
            public MouseState? PrevMouseState { get; set; }
            public MouseState? MouseState { get; set; }
        }
    }

    public class ButtonControl
    {
        public bool? IsKey { get; set; }
        public Keys Key { get; set; }
        public MouseButton MouseButton { get; set; }

        // todo?: double tap
        public bool IsPressed { get; set; }
        public bool IsDown { get; set; }
        public bool IsReleased { get; set; }

        public ButtonControl()
        {
        }

        public ButtonControl(Keys key)
        {
            IsKey = true;
            Key = key;
        }

        public ButtonControl(MouseButton mouseButton)
        {
            IsKey = false;
            MouseButton = mouseButton;
        }
    }

    public class PlayerControls
    {
        public bool MouseAim { get; set; }
        public bool KeyboardAim { get; set; }
        public ButtonControl MoveLeft { get; }
        public ButtonControl MoveRight { get; }
        public ButtonControl MoveUp { get; }
        public ButtonControl MoveDown { get; }
        public ButtonControl AimLeft { get; }
        public ButtonControl AimRight { get; }
        public ButtonControl AimUp { get; }
        public ButtonControl AimDown { get; }
        public ButtonControl Shoot { get; }
        public ButtonControl Zoom { get; }
        public ButtonControl Jump { get; }
        public ButtonControl Morph { get; }
        public ButtonControl Boost { get; }
        public ButtonControl AltAttack { get; }

        public ButtonControl[] All { get; }

        public PlayerControls(ButtonControl moveLeft, ButtonControl moveRight, ButtonControl moveUp, ButtonControl moveDown,
            ButtonControl aimLeft, ButtonControl aimRight, ButtonControl aimUp, ButtonControl aimDown, ButtonControl shoot,
            ButtonControl zoom, ButtonControl jump, ButtonControl morph, ButtonControl boost, ButtonControl altAttack)
        {
            MouseAim = true;
            KeyboardAim = false;
            MoveLeft = moveLeft;
            MoveRight = moveRight;
            MoveUp = moveUp;
            MoveDown = moveDown;
            AimLeft = aimLeft;
            AimRight = aimRight;
            AimUp = aimUp;
            AimDown = aimDown;
            Shoot = shoot;
            Zoom = zoom;
            Jump = jump;
            Morph = morph;
            Boost = boost;
            AltAttack = altAttack;
            All = new[]
            {
                moveLeft, moveRight, moveUp, moveDown, aimLeft, aimRight, aimUp, aimDown, shoot, zoom, jump, morph, boost, altAttack
            };
        }

        public static PlayerControls GetDefault()
        {
            return new PlayerControls(
                moveLeft: new ButtonControl(Keys.A),
                moveRight: new ButtonControl(Keys.D),
                moveUp: new ButtonControl(Keys.W),
                moveDown: new ButtonControl(Keys.S),
                aimLeft: new ButtonControl(),
                aimRight: new ButtonControl(),
                aimUp: new ButtonControl(),
                aimDown: new ButtonControl(),
                shoot: new ButtonControl(MouseButton.Button2),
                zoom: new ButtonControl(MouseButton.Button3),
                jump: new ButtonControl(Keys.Space),
                morph: new ButtonControl(Keys.C),
                boost: new ButtonControl(Keys.Space),
                altAttack: new ButtonControl(Keys.Q)
            );
        }
    }
}
