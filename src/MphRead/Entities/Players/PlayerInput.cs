using System;
using MphRead.Formats;
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
                    // todo: actual pause menu should required pressed
                    if (_scene.Multiplayer && !Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen) && Controls.Pause.IsDown)
                    {
                        _showScoreboard = true;
                    }
                    else
                    {
                        _showScoreboard = false;
                    }
                }
                if (_frozenTimer > 0)
                {
                    _frozenTimer--;
                    _timeSinceFrozen = 0;
                    if (_frozenTimer == 0)
                    {
                        _soundSource.PlaySfx(SfxId.SHOTGUN_BREAK_FREEZE);
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
                            CreateIceBreakEffectBiped(_bipedModelLods[lod].Model);
                        }
                    }
                }
                if (_frozenGfxTimer > 0)
                {
                    _frozenGfxTimer--;
                    if (IsMainPlayer && _frozenGfxTimer == 0)
                    {
                        _drawIceLayer = false;
                    }
                }
                if (_timeSinceFrozen != UInt16.MaxValue)
                {
                    _timeSinceFrozen++;
                }
            }
            else
            {
                _showScoreboard = _scene.Multiplayer && Controls.Pause.IsDown;
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

        private static readonly BeamType[] _weaponOrder = new BeamType[9]
        {
            /* 0 */ BeamType.PowerBeam,
            /* 1 */ BeamType.Missile,
            /* 2 */ BeamType.VoltDriver,
            /* 3 */ BeamType.Battlehammer,
            /* 4 */ BeamType.Imperialist,
            /* 5 */ BeamType.Judicator,
            /* 6 */ BeamType.Magmaul,
            /* 7 */ BeamType.ShockCoil,
            /* 8 */ BeamType.OmegaCannon
        };

        private void ProcessTouchInput()
        {
            // the game explicitly checks for Samus, and doesn't check if the weapon menu is open
            if (!_scene.Multiplayer && Controls.ScanVisor.IsPressed && !Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen)
                && !IsAltForm && !IsMorphing)
            {
                if (ScanVisor)
                {
                    SwitchVisors(reset: false);
                }
                else
                {
                    // todo?: play SFX for the "hold scan" feature
                    SwitchVisors(reset: false);
                    UpdateZoom(zoom: false);
                }
            }
            if ((_scene.Multiplayer || _weaponSlots[2] != BeamType.OmegaCannon) && Controls.WeaponMenu.IsDown)
            {
                Flags1 |= PlayerFlags1.NoAimInput;
                Flags1 |= PlayerFlags1.WeaponMenuOpen;
                _showScoreboard = false;
            }
            bool selected = false;
            if (!Controls.WeaponMenu.IsDown)
            {
                EndWeaponMenu(ref selected);
            }
            if (!selected)
            {
                if (Controls.PowerBeam.IsPressed)
                {
                    if (CurrentWeapon != BeamType.PowerBeam)
                    {
                        TryEquipWeapon(BeamType.PowerBeam, debug: true);
                    }
                }
                else if (Controls.Missile.IsPressed)
                {
                    if (CurrentWeapon != BeamType.Missile)
                    {
                        TryEquipWeapon(BeamType.Missile, debug: true);
                    }
                }
                else if (Controls.VoltDriver.IsPressed)
                {
                    if (CurrentWeapon != BeamType.VoltDriver)
                    {
                        TryEquipWeapon(BeamType.VoltDriver, debug: true);
                    }
                }
                else if (Controls.Battlehammer.IsPressed)
                {
                    if (CurrentWeapon != BeamType.Battlehammer)
                    {
                        TryEquipWeapon(BeamType.Battlehammer, debug: true);
                    }
                }
                else if (Controls.Imperialist.IsPressed)
                {
                    if (CurrentWeapon != BeamType.Imperialist)
                    {
                        TryEquipWeapon(BeamType.Imperialist, debug: true);
                    }
                }
                else if (Controls.Judicator.IsPressed)
                {
                    if (CurrentWeapon != BeamType.Judicator)
                    {
                        TryEquipWeapon(BeamType.Judicator, debug: true);
                    }
                }
                else if (Controls.Magmaul.IsPressed)
                {
                    if (CurrentWeapon != BeamType.Magmaul)
                    {
                        TryEquipWeapon(BeamType.Magmaul, debug: true);
                    }
                }
                else if (Controls.ShockCoil.IsPressed)
                {
                    if (CurrentWeapon != BeamType.ShockCoil)
                    {
                        TryEquipWeapon(BeamType.ShockCoil, debug: true);
                    }
                }
                else if (Controls.OmegaCannon.IsPressed)
                {
                    if (CurrentWeapon != BeamType.OmegaCannon)
                    {
                        TryEquipWeapon(BeamType.OmegaCannon, debug: true);
                    }
                }
                else if (Controls.AffinitySlot.IsPressed)
                {
                    BeamType weapon = _weaponSlots[2];
                    if (weapon != BeamType.None && CurrentWeapon != weapon)
                    {
                        TryEquipWeapon(weapon);
                    }
                }
                else if (Controls.ScrollAllWeapons || CurrentWeapon != BeamType.PowerBeam && CurrentWeapon != BeamType.Missile)
                {
                    int currentIndex = -1;
                    for (int i = 0; i < _weaponOrder.Length; i++)
                    {
                        if (_weaponOrder[i] == CurrentWeapon)
                        {
                            currentIndex = i;
                        }
                    }
                    int nextIndex = currentIndex;
                    BeamType nextBeam = CurrentWeapon;
                    if (Controls.NextWeapon.IsPressed)
                    {
                        do
                        {
                            nextIndex++;
                            if (Controls.ScrollAllWeapons && nextIndex > 8)
                            {
                                nextIndex = 0;
                            }
                            else if (!Controls.ScrollAllWeapons && nextIndex > 7)
                            {
                                nextIndex = 2;
                            }
                            nextBeam = _weaponOrder[nextIndex];
                        }
                        while (nextIndex != currentIndex && !_availableWeapons[nextBeam]);
                    }
                    else if (Controls.PrevWeapon.IsPressed)
                    {
                        do
                        {
                            nextIndex--;
                            if (Controls.ScrollAllWeapons && nextIndex < 0)
                            {
                                nextIndex = 8;
                            }
                            else if (!Controls.ScrollAllWeapons && nextIndex < 2)
                            {
                                nextIndex = 7;
                            }
                            nextBeam = _weaponOrder[nextIndex];
                        }
                        while (nextIndex != currentIndex && !_availableWeapons[nextBeam]);
                    }
                    if (nextBeam != CurrentWeapon)
                    {
                        TryEquipWeapon(nextBeam);
                    }
                }
            }
        }

        private void EndWeaponMenu(ref bool selected)
        {
            if (WeaponSelection != BeamType.None)
            {
                if (WeaponSelection != CurrentWeapon)
                {
                    TryEquipWeapon(WeaponSelection);
                    selected = true;
                }
                else if (IsMainPlayer && Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
                {
                    _soundSource.PlayFreeSfx(SfxId.BEAM_SWITCH_FAIL);
                }
                WeaponSelection = CurrentWeapon;
            }
            Flags1 &= ~PlayerFlags1.NoAimInput;
            Flags1 &= ~PlayerFlags1.WeaponMenuOpen;
        }

        private void UpdateAimFacing()
        {
            float dot = Vector3.Dot(_gunVec1, _facingVector);
            if (dot < Fixed.ToFloat(3956))
            {
                Vector3 temp1 = (_facingVector - _gunVec1 * dot).Normalized();
                _facingVector = Fixed.ToFloat(3956) * _gunVec1 + Fixed.ToFloat(1060) * temp1;
            }
            Vector3 temp2 = _gunVec1 - _facingVector;
            _facingVector += temp2 * 0.1f;
            _facingVector = _facingVector.Normalized();
        }

        private void UpdateAimY(float amount)
        {
            if (Controls.InvertAimY)
            {
                amount *= -1;
            }
            float sensitivity = 1; // itodo: this
            if (EquipInfo.Zoomed)
            {
                float fovFactor = CameraInfo.Fov - Fixed.ToFloat(Values.NormalFov) * 2;
                sensitivity /= -Fixed.ToFloat(Values.Field70) * fovFactor;
            }
            amount *= sensitivity;
            // unimpl-controls: these calculations are different when exact aim is not set
            float prevAim = _aimY;
            _aimY += amount;
            if (IsAltForm)
            {
                _aimY = Math.Clamp(_aimY, -25, 5);
            }
            else
            {
                _aimY = Math.Clamp(_aimY, -85, 85);
            }
            float diff = MathHelper.DegreesToRadians(_aimY - prevAim);
            Matrix4 transform = GetTransformMatrix(_gunVec1, Vector3.UnitY);
            Vector3 vector;
            if (diff <= 0)
            {
                vector = new Vector3(0, MathF.Sin(diff), MathF.Cos(diff));
            }
            else
            {
                vector = new Vector3(0, -MathF.Sin(-diff), MathF.Cos(-diff));
            }
            _gunVec1 = Matrix.Vec3MultMtx3(vector, transform).Normalized();
            _aimPosition = CameraInfo.Position + _gunVec1 * Fixed.ToFloat(Values.AimDistance);
            UpdateAimFacing();
        }

        private void UpdateAimX(float amount)
        {
            if (Controls.InvertAimX)
            {
                amount *= -1;
            }
            float sensitivity = 1; // itodo: this
            if (EquipInfo.Zoomed)
            {
                float fovFactor = CameraInfo.Fov - Fixed.ToFloat(Values.NormalFov) * 2;
                sensitivity /= -Fixed.ToFloat(Values.Field70) * fovFactor;
            }
            amount *= sensitivity;
            // unimpl-controls: these calculations are different when exact aim is not set
            float sin;
            float cos;
            float angle = MathHelper.DegreesToRadians(amount);
            if (amount <= 0)
            {
                sin = MathF.Sin(angle);
                cos = MathF.Cos(angle);
            }
            else
            {
                sin = -MathF.Sin(-angle);
                cos = MathF.Cos(-angle);
            }
            float x = _gunVec1.X;
            float z = _gunVec1.Z;
            _gunVec1.X = x * cos + z * sin;
            _gunVec1.Z = x * -sin + z * cos;
            _gunVec1 = _gunVec1.Normalized();
            _aimPosition = CameraInfo.Position + _gunVec1 * Fixed.ToFloat(Values.AimDistance);
            if (EquipInfo.Zoomed)
            {
                _facingVector = _gunVec1;
            }
            else
            {
                UpdateAimFacing();
            }
        }

        private readonly float[] _pastAimX = new float[8];
        private readonly float[] _pastAimY = new float[8];

        private void UpdateHudShiftY(float amount)
        {
            if (IsMainPlayer)
            {
                float sum = 0;
                for (int i = 6; i >= 0; i--)
                {
                    float past = _pastAimY[i];
                    sum += past;
                    _pastAimY[i + 1] = past;
                }
                _pastAimY[0] = amount;
                if (Features.HudSway)
                {
                    float average = (sum + amount) / 8;
                    _hudShiftY = Math.Clamp(-MathF.Round(average), -8, 8);
                }
                else
                {
                    _hudShiftY = 0;
                }
                _objShiftY = -_hudShiftY / 2;
            }
        }

        private void UpdateHudShiftX(float amount)
        {
            if (IsMainPlayer)
            {
                float sum = 0;
                for (int i = 6; i >= 0; i--)
                {
                    float past = _pastAimX[i];
                    sum += past;
                    _pastAimX[i + 1] = past;
                }
                _pastAimX[0] = amount;
                if (Features.HudSway)
                {
                    float average = (sum + amount) / 8;
                    _hudShiftX = Math.Clamp(MathF.Round(average), -8, 8);
                }
                else
                {
                    _hudShiftX = 0;
                }
                _objShiftX = _hudShiftX / 2;
            }
        }

        private void ProcessBiped()
        {
            if (IsMainPlayer && !_scene.Multiplayer && CameraSequence.Current != null)
            {
                _timeIdle = 0;
            }
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
            if (_frozenTimer == 0 && _health > 0 && !_field6D0)
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
                if (Controls.MouseAim && !Flags1.TestFlag(PlayerFlags1.NoAimInput))
                {
                    float aimY = -Input.MouseDeltaY / 4f; // itodo: x and y sensitivity
                    float aimX = -Input.MouseDeltaX / 4f;
                    if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true
                        || _scene.FrameAdvance || _scene.FrameAdvanceLastFrame) // skdebug
                    {
                        aimX = aimY = 0;
                    }
                    UpdateHudShiftY(aimY);
                    UpdateHudShiftX(aimX);
                    UpdateAimY(aimY);
                    UpdateAimX(aimX);
                    if (Flags1.TestFlag(PlayerFlags1.Grounded))
                    {
                        // sktodo: threshold values
                        if (aimX > 3)
                        {
                            _timeIdle = 0;
                            anim1 = PlayerAnimation.Turn;
                            animFlags1 = AnimFlags.Reverse;
                            if (Biped2Anim == PlayerAnimation.Turn)
                            {
                                _bipedModel2.AnimInfo.Flags[0] &= ~AnimFlags.NoLoop;
                                _bipedModel2.AnimInfo.Flags[0] |= AnimFlags.Reverse;
                            }
                            if (Biped1Anim == PlayerAnimation.Turn)
                            {
                                _bipedModel1.AnimInfo.Flags[0] &= ~AnimFlags.NoLoop;
                                _bipedModel1.AnimInfo.Flags[0] |= AnimFlags.Reverse;
                            }
                        }
                        else if (aimX < -3)
                        {
                            _timeIdle = 0;
                            anim1 = PlayerAnimation.Turn;
                            if (Biped2Anim == PlayerAnimation.Turn)
                            {
                                _bipedModel2.AnimInfo.Flags[0] &= ~AnimFlags.NoLoop;
                                _bipedModel2.AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                            }
                            if (Biped1Anim == PlayerAnimation.Turn)
                            {
                                _bipedModel1.AnimInfo.Flags[0] &= ~AnimFlags.NoLoop;
                                _bipedModel1.AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                            }
                        }
                    }
                }
                if (Controls.KeyboardAim)
                {
                    // itodo: button aim
                }
                bool jumping = false;
                if (!Flags2.TestAny(PlayerFlags2.BipedLock | PlayerFlags2.BipedStuck))
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
                            _field684 += Fixed.ToFloat(Values.Field114) * sign / 2; // todo: FPS stuff
                            _field684 = Math.Clamp(_field684, -180, 180);
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
                            _field688 += Fixed.ToFloat(Values.Field114) * sign / 2; // todo: FPS stuff
                            _field688 = Math.Clamp(_field688, -180, 180);
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
                    if (_field684 < Fixed.ToFloat(500) && _field684 > Fixed.ToFloat(-500))
                    {
                        _field684 = 0;
                    }
                    else
                    {
                        _field684 *= 0.9f; // sktodo: FPS stuff
                    }
                    if (Controls.MoveUp.IsDown)
                    {
                        MoveForwardBack(PlayerAnimation.WalkForward, sign: 1);
                    }
                    else if (Controls.MoveDown.IsDown)
                    {
                        MoveForwardBack(PlayerAnimation.WalkBackward, sign: -1);
                    }
                    if (_field688 < Fixed.ToFloat(500) && _field688 > Fixed.ToFloat(-500))
                    {
                        _field688 = 0;
                    }
                    else
                    {
                        _field688 *= 0.9f; // sktodo: FPS stuff
                    }
                    if (Cheats.UnlimitedJumps)
                    {
                        Flags1 &= ~PlayerFlags1.UsedJump;
                    }
                    // unimpl-controls: in the up/down code path, the game processes aim reset if that flag is off
                    // unimpl-controls: the aim input disable flag is also checked by the game
                    if (_jumpPadControlLockMin == 0 && Controls.Jump.IsPressed && !Flags1.TestFlag(PlayerFlags1.UsedJump))
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
                        PlayHunterSfx(HunterSfx.Jump);
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
            if (_frozenTimer == 0 && _health > 0 && !_field6D0)
            {
                bool scanInput = false;
                if (ScanVisor)
                {
                    scanInput = true;
                    if (!_scanning && Controls.Scan.IsPressed || _scanning && Controls.Scan.IsDown)
                    {
                        UpdateScanning(scanning: true);
                    }
                    else
                    {
                        UpdateScanning(scanning: false);
                        if (Controls.Scan != Controls.Shoot
                            && (Controls.Shoot.IsPressed || Controls.Morph.IsPressed))
                        {
                            SwitchVisors(reset: false);
                            scanInput = false;
                        }
                    }
                    if (EquipInfo.ChargeLevel > 0)
                    {
                        EquipInfo.ChargeLevel = 0;
                        StopBeamChargeSfx(CurrentWeapon);
                    }
                }
                if (!scanInput && !IsUnmorphing)
                {
                    if (!Controls.Shoot.IsDown)
                    {
                        Flags2 &= ~PlayerFlags2.Shooting;
                    }
                    else if (Controls.Shoot.IsPressed || !Flags2.TestFlag(PlayerFlags2.NoShotsFired))
                    {
                        Flags2 |= PlayerFlags2.Shooting;
                        Flags2 &= ~PlayerFlags2.NoShotsFired;
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
                                // the game doesn't need this condition, but we do because "the next frame will
                                // overwrite it" type stuff isn't guaranteed to get in ahead of the audio system
                                if (CurrentWeapon != BeamType.PowerBeam
                                    || EquipInfo.ChargeLevel >= EquipInfo.Weapon.MinCharge * 2) // todo: FPS stuff
                                {
                                    PlayBeamChargeSfx(CurrentWeapon);
                                }
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
                            StopBeamChargeSfx(CurrentWeapon);
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
                            float zoomFov = Fixed.ToFloat(EquipInfo.Weapon.ZoomFov);
                            Vector3 facing = _facingVector;

                            void CheckZoomTargets(EntityType type)
                            {
                                for (int i = 0; i < _scene.Entities.Count; i++)
                                {
                                    EntityBase entity = _scene.Entities[i];
                                    if (entity.Type != type || entity == this || !entity.GetTargetable())
                                    {
                                        continue;
                                    }
                                    if (entity.Type == EntityType.Object
                                        && !((ObjectEntity)entity).Data.EffectFlags.TestFlag(ObjEffFlags.WeaponZoom))
                                    {
                                        continue;
                                    }
                                    entity.GetPosition(out Vector3 position);
                                    Vector3 between = position - Position;
                                    float dot = Vector3.Dot(between, facing);
                                    if (dot > 1 && dot / between.Length >= Fixed.ToFloat(4074))
                                    {
                                        float angle = MathHelper.RadiansToDegrees(MathF.Atan2(3, dot));
                                        if (angle < zoomFov)
                                        {
                                            zoomFov = angle;
                                        }
                                    }
                                }
                            }

                            CheckZoomTargets(EntityType.Player);
                            CheckZoomTargets(EntityType.EnemyInstance);
                            CheckZoomTargets(EntityType.Object);
                            zoomFov *= 2;
                            float currentFov = CameraInfo.Fov;
                            if (zoomFov > currentFov)
                            {
                                currentFov += 2 * 2;
                                if (currentFov > zoomFov)
                                {
                                    currentFov = zoomFov;
                                }
                            }
                            else if (zoomFov < currentFov)
                            {
                                currentFov -= 2 * 2;
                                if (currentFov < zoomFov)
                                {
                                    currentFov = zoomFov;
                                }
                            }
                            CameraInfo.Fov = currentFov;
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
                    // the game doesn't require pressed here, but presumably the control scheme would have the pressed flag
                    // todo: use the ability flag for the morph touch button too, even though the game doesn't
                    if (!Flags2.TestFlag(PlayerFlags2.BipedStuck) && _abilities.TestFlag(AbilityFlags.AltForm)
                        && Controls.Morph.IsPressed || IsMainPlayer && CameraSequence.Current?.ForceAlt == true)
                    {
                        if (TrySwitchForms() && IsMainPlayer && IsMorphing)
                        {
                            // the game only does this when using the touch screen button, but this is equivalent,
                            // and we want to call this beause it updates the reticle expansion
                            HudOnMorphStart(teleported: false);
                        }
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
                    Vector3 diff = _gunVec1 - _facingVector;
                    _facingVector += diff * 0.3f / 2; // todo: FPS stuff
                    _facingVector = _facingVector.Normalized();
                }
                if (anim1 == PlayerAnimation.None)
                {
                    if (Flags1.TestFlag(PlayerFlags1.Grounded))
                    {
                        if (Biped1Anim == PlayerAnimation.Idle)
                        {
                            if (++_timeIdle > 300 * 2 && _timeSinceInput > 300 * 2) // todo: FPS stuff
                            {
                                SetBiped1Animation(PlayerAnimation.Flourish, AnimFlags.NoLoop);
                            }
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
                int pbAuto = Math.Min(_powerBeamAutofire / 2, 90); // todo: FPS stuff
                pbAuto = (int)(pbAuto * 15 / 90f);
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
                PlayBeamEmptySfx(EquipInfo.Weapon.Beam);
                return false;
            }
            // todo: update license stats
            _timeSinceShot = 0;
            if (IsMainPlayer)
            {
                HudOnFiredShot();
            }
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
                if (_muzzleEffect != null && !IsMainPlayer)
                {
                    _muzzleEffect.SetDrawEnabled(false);
                }
            }
            bool charged;
            if (EquipInfo.Weapon.Flags.TestFlag(WeaponFlags.PartialCharge))
            {
                charged = Flags1.TestFlag(PlayerFlags1.ShotCharged);
            }
            else
            {
                charged = EquipInfo.ChargeLevel >= EquipInfo.Weapon.FullCharge * 2; // todo: FPS stuff
            }
            bool continuous = EquipInfo.Weapon.Flags.TestFlag(WeaponFlags.Continuous);
            bool homing = result.TestFlag(BeamResultFlags.Homing);
            float amountA = 0x3FFF * _shockCoilTimer / (30f * 2); // todo: FPS stuff
            PlayBeamShotSfx(EquipInfo.Weapon.Beam, charged, continuous, homing, amountA);
            if (EquipInfo.Weapon.Beam == BeamType.Imperialist && EquipInfo.GetAmmo() >= EquipInfo.Weapon.AmmoCost)
            {
                _soundSource.PlaySfx(SfxId.SNIPER_RELOAD);
            }
            EquipInfo.Weapon = curWeapon;
            UnequipOmegaCannon(); // todo?: set the flag if wifi
            return true;
        }

        private void ProcessAlt()
        {
            Vector3 speedDelta = Vector3.Zero;
            int animId = -1;
            AnimFlags animFlags = AnimFlags.None;
            Flags1 |= PlayerFlags1.UsedJump;
            if (_frozenTimer == 0 && _health > 0)
            {
                // todo?: if touch movement for alt form was a thing, this would need extra conditions
                if ((!Controls.MoveRight.IsDown && !Controls.MoveLeft.IsDown && !Controls.MoveUp.IsDown && !Controls.MoveDown.IsDown)
                    || Controls.MoveRight.IsPressed || Controls.MoveLeft.IsPressed || Controls.MoveUp.IsPressed || Controls.MoveDown.IsPressed)
                {
                    Flags1 &= ~PlayerFlags1.AltDirOverride;
                }
                if (_timeSinceMorphCamera > 10 * 2 && !Flags1.TestFlag(PlayerFlags1.AltDirOverride) // todo: FPS stuff
                    && (MathF.Abs(CameraInfo.Field48) >= 1 / 4096f || MathF.Abs(CameraInfo.Field4C) >= 1 / 4096f))
                {
                    _altRollFbX = CameraInfo.Field48;
                    _altRollFbZ = CameraInfo.Field4C;
                    _altRollLrX = CameraInfo.Field50;
                    _altRollLrZ = CameraInfo.Field54;
                }
                // todo?: field35C targeting(?) stuff
                if (Values.AltFormStrafe != 0)
                {
                    // Trace, Sylux, Weavel
                    if (Controls.MouseAim && !Flags1.TestFlag(PlayerFlags1.NoAimInput))
                    {
                        float aimY = -Input.MouseDeltaY / 4f; // itodo: x and y sensitivity
                        float aimX = -Input.MouseDeltaX / 4f;
                        if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true
                            || _scene.FrameAdvance || _scene.FrameAdvanceLastFrame) // skdebug
                        {
                            aimX = aimY = 0;
                        }
                        UpdateHudShiftY(aimY);
                        UpdateHudShiftX(aimX);
                        UpdateAimY(aimY);
                        UpdateAimX(aimX);
                        if ((Hunter == Hunter.Trace || Hunter == Hunter.Weavel) && Flags1.TestFlag(PlayerFlags1.Grounded))
                        {
                            // sktodo: threshold values
                            if (aimX > 3)
                            {
                                _timeIdle = 0;
                                animId = (int)WeavelAltAnim.Turn; // or TraceAltAnim.MoveBackward
                                animFlags = AnimFlags.Reverse;
                                if (_altModel.AnimInfo.Index[0] == animId)
                                {
                                    _altModel.AnimInfo.Flags[0] &= ~AnimFlags.NoLoop;
                                    _altModel.AnimInfo.Flags[0] |= AnimFlags.Reverse;
                                }
                            }
                            else if (aimX < -3)
                            {
                                _timeIdle = 0;
                                animId = (int)WeavelAltAnim.Turn; // or TraceAltAnim.MoveBackward
                                if (_altModel.AnimInfo.Index[0] == animId)
                                {
                                    _altModel.AnimInfo.Flags[0] &= ~AnimFlags.NoLoop;
                                    _altModel.AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                                }
                            }
                        }
                    }
                    if (Controls.KeyboardAim)
                    {
                        // itodo: button aim
                    }
                    if (!Flags2.TestFlag(PlayerFlags2.BipedLock) && (Hunter != Hunter.Trace || !Flags2.TestFlag(PlayerFlags2.AltAttack)))
                    {
                        // unimpl-controls: the game also tests for either the free strafe flag, or the strafe button held
                        // and later, for up/down, it tests for either flag, or the look button not held

                        void MoveRightLeft(int walkAnim, int sign)
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
                            speedDelta.X -= _field78 * traction * sign;
                            speedDelta.Z -= _field7C * traction * sign;
                            if (!EquipInfo.Zoomed)
                            {
                                // todo: update field684 (using sign)
                            }
                            if (Hunter == Hunter.Trace || Hunter == Hunter.Weavel)
                            {
                                animId = walkAnim;
                                animFlags = AnimFlags.None;
                            }
                        }

                        void MoveForwardBack(int walkAnim, int sign)
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
                            if (!EquipInfo.Zoomed)
                            {
                                // todo: update field688 (using sign)
                            }
                            if (Hunter == Hunter.Trace || Hunter == Hunter.Weavel)
                            {
                                animId = walkAnim;
                                animFlags = AnimFlags.None;
                            }
                        }

                        if (Controls.MoveRight.IsDown)
                        {
                            MoveRightLeft(walkAnim: 4, sign: 1); // TraceAltAnim.MoveRight or WeavelAltAnim.MoveRight
                        }
                        else if (Controls.MoveLeft.IsDown)
                        {
                            MoveRightLeft(walkAnim: 2, sign: -1); // TraceAltAnim.MoveLeft or WeavelAltAnim.MoveLeft
                        }
                        // todo: update field684
                        if (Controls.MoveUp.IsDown)
                        {
                            MoveForwardBack(walkAnim: 3, sign: 1); // TraceAltAnim.MoveForward or WeavelAltAnim.MoveForward
                        }
                        else if (Controls.MoveDown.IsDown)
                        {
                            MoveForwardBack(walkAnim: 5, sign: -1); // TraceAltAnim.MoveBackward or WeavelAltAnim.MoveBackward
                        }
                        // todo: update field684
                        // unimpl-controls: in the up/down code path, the game processes aim reset if that flag is off
                    }
                }
                else
                {
                    // Samus, Kanden, Spire, Noxus
                    // todo: touch roll
                    float traction = Fixed.ToFloat(Values.RollAltTraction);
                    if (_jumpPadControlLockMin > 0)
                    {
                        traction *= Fixed.ToFloat(Values.JumpPadSlideFactor);
                    }
                    if (Controls.MoveUp.IsDown)
                    {
                        speedDelta.X += _altRollFbX * traction;
                        speedDelta.Z += _altRollFbZ * traction;
                    }
                    else if (Controls.MoveDown.IsDown)
                    {
                        speedDelta.X -= _altRollFbX * traction;
                        speedDelta.Z -= _altRollFbZ * traction;
                    }
                    if (Controls.MoveLeft.IsDown)
                    {
                        speedDelta.X += _altRollLrX * traction;
                        speedDelta.Z += _altRollLrZ * traction;
                    }
                    else if (Controls.MoveRight.IsDown)
                    {
                        speedDelta.X -= _altRollLrX * traction;
                        speedDelta.Z -= _altRollLrZ * traction;
                    }
                }
                if (!IsMorphing)
                {
                    if (_abilities.TestFlag(AbilityFlags.Bombs) && Controls.AltAttack.IsPressed
                        && _bombAmmo > 0 && _bombCooldown == 0 && _field35C == null)
                    {
                        SpawnBomb();
                    }
                    if (_abilities.TestFlag(AbilityFlags.NoxusAltAttack))
                    {
                        if (Controls.AltAttack.IsDown)
                        {
                            if (Controls.AltAttack.IsPressed)
                            {
                                _altAttackTime = 1;
                                _altModel.SetAnimation((int)NoxusAltAnim.Extend, AnimFlags.NoLoop);
                            }
                            else if (_altAttackTime > 0)
                            {
                                _altAttackTime++;
                                if (_altAttackTime == 7 * 2) // todo: FPS stuff
                                {
                                    _soundSource.PlaySfx(SfxId.NOX_TOP_ATTACK1);
                                }
                                else
                                {
                                    int startupTime = Values.AltAttackStartup * 2; // todo: FPS stuff
                                    if (_altAttackTime == startupTime / 2)
                                    {
                                        _soundSource.PlaySfx(SfxId.NOX_TOP_ATTACK2, loop: true);
                                    }
                                    else if (_altAttackTime >= startupTime)
                                    {
                                        _altAttackTime = (ushort)startupTime;
                                        Flags2 |= PlayerFlags2.AltAttack;
                                    }
                                }
                            }
                            _altModel.AnimInfo.Frame[0] = (_altAttackTime / 2 * _altModel.AnimInfo.FrameCount[0] - 1)
                                / Values.AltAttackStartup; // todo: FPS stuff ^
                        }
                        else
                        {
                            EndAltAttack();
                        }
                    }
                    if (_abilities.TestFlag(AbilityFlags.SpireAltAttack))
                    {
                        if (Flags2.TestFlag(PlayerFlags2.AltAttack))
                        {
                            if (_altModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                            {
                                EndAltAttack();
                            }
                        }
                        else if (Controls.AltAttack.IsPressed)
                        {
                            Flags2 |= PlayerFlags2.AltAttack;
                            _altModel.SetAnimation((int)SpireAltAnim.Attack, AnimFlags.NoLoop);
                            _soundSource.PlaySfx(SfxId.SPIRE_ALT_ATTACK);
                            _spireRockPosR = Position;
                            _spireRockPosL = Position;
                            _spireAltUp = _fieldC0;
                            var cross = Vector3.Cross(_facingVector, _spireAltUp);
                            _spireAltFacing = Vector3.Cross(_spireAltUp, cross).Normalized();
                        }
                    }
                    if (_abilities.TestFlag(AbilityFlags.TraceAltAttack))
                    {
                        if (Flags2.TestFlag(PlayerFlags2.AltAttack) || _altAttackCooldown > 0)
                        {
                            if (Flags1.TestFlag(PlayerFlags1.Standing))
                            {
                                EndAltAttack();
                            }
                        }
                        else if (Controls.AltAttack.IsPressed)
                        {
                            Flags2 |= PlayerFlags2.AltAttack;
                            float attackHSpeed = Fixed.ToFloat(Values.LungeHSpeed);
                            float attackVSpeed = Fixed.ToFloat(Values.LungeVSpeed);
                            float accelX = _field70 * attackHSpeed;
                            float accelZ = _field74 * attackHSpeed;
                            if (_field70 * Speed.X + _field74 * Speed.Z < attackHSpeed)
                            {
                                Speed = Speed.WithX(accelX).WithZ(accelZ);
                            }
                            _accelerationTimer = 6 * 2; // todo: FPS stuff
                            Acceleration = new Vector3(accelX, 0, accelZ);
                            if (Speed.Y < attackVSpeed)
                            {
                                float newYSpeed = Speed.Y + attackVSpeed;
                                if (newYSpeed > attackVSpeed)
                                {
                                    newYSpeed = attackVSpeed;
                                }
                                Speed = Speed.WithY(newYSpeed);
                            }
                            animId = (int)TraceAltAnim.Attack;
                            animFlags = AnimFlags.NoLoop;
                            _soundSource.PlaySfx(SfxId.TRACE_ALT_ATTACK);
                        }
                    }
                    if (_abilities.TestFlag(AbilityFlags.WeavelAltAttack))
                    {
                        if (Flags2.TestFlag(PlayerFlags2.AltAttack) || _altAttackCooldown > 0)
                        {
                            if (Flags1.TestFlag(PlayerFlags1.Standing))
                            {
                                EndAltAttack();
                            }
                        }
                        else if (Controls.AltAttack.IsPressed)
                        {
                            Flags2 |= PlayerFlags2.AltAttack;
                            // todo: if bot with encounter state, use alternate values
                            float attackHSpeed = Fixed.ToFloat(Values.LungeHSpeed);
                            float attackVSpeed = Fixed.ToFloat(Values.LungeVSpeed);
                            if (_field70 * Speed.X + _field74 * Speed.Z < attackHSpeed)
                            {
                                Speed = Speed.WithX(_field70 * attackHSpeed).WithZ(_field74 * attackHSpeed);
                            }
                            if (Speed.Y < attackVSpeed)
                            {
                                float newYSpeed = Speed.Y + attackVSpeed;
                                if (newYSpeed > attackVSpeed)
                                {
                                    newYSpeed = attackVSpeed;
                                }
                                Speed = Speed.WithY(newYSpeed);
                            }
                            animId = (int)WeavelAltAnim.Attack;
                            animFlags = AnimFlags.NoLoop;
                            _soundSource.PlaySfx(SfxId.WEAVEL_ALT_ATTACK);
                        }
                    }
                    if (_abilities.TestFlag(AbilityFlags.Boost) && AttachedEnemy == null)
                    {
                        // todo: touch boost
                        // else...
                        if (Controls.Boost.IsDown)
                        {
                            // the game plays the boost charge SFX here, but that SFX is empty
                            if (_boostCharge < Values.BoostChargeMax * 2) // todo: FPS stuff
                            {
                                _boostCharge++;
                            }
                        }
                        else
                        {
                            if (_boostCharge > Values.BoostChargeMin * 2) // todo: FPS stuff
                            {
                                if (_boostCharge > 0)
                                {
                                    int sfx = Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.Boost];
                                    _soundSource.PlaySfx(sfx);
                                }
                                float boostHCap = Fixed.ToFloat(Values.BoostSpeedCap) * _boostCharge
                                    / (Values.BoostChargeMax * 2); // todo: FPS stuff
                                if (_hSpeedCap < boostHCap)
                                {
                                    _hSpeedCap = boostHCap;
                                }
                                float factor = Fixed.ToFloat(Values.BoostSpeedMin)
                                    + _boostCharge * (Fixed.ToFloat(Values.BoostSpeedMax) - Fixed.ToFloat(Values.BoostSpeedMin))
                                    / (Values.BoostChargeMax * 2); // todo: FPS stuff
                                speedDelta = speedDelta.AddX(_field70 * factor).AddZ(_field74 * factor);
                                _altAttackCooldown = (ushort)(Values.AltAttackCooldown * 2); // todo: FPS stuff
                                Flags1 |= PlayerFlags1.Boosting;
                                _boostDamage = (ushort)(Values.AltAttackDamage * _boostCharge / (Values.BoostChargeMax * 2)); // todo: FPS stuff
                                if (IsMainPlayer)
                                {
                                    _boostInst.SetAnimation(start: 0, target: 10, frames: 11, afterAnim: 0);
                                }
                                if (_boostEffect != null)
                                {
                                    _scene.UnlinkEffectEntry(_boostEffect);
                                    _boostEffect = null;
                                }
                                _boostEffect = _scene.SpawnEffectGetEntry(136, _gunVec2, _facingVector, Position); // samusDash
                                if (_boostEffect != null)
                                {
                                    _boostEffect.SetElementExtension(true);
                                }
                            }
                            _boostCharge = 0;
                        }
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
                if (_field35C != null)
                {
                    Speed = Speed.WithX(0).WithZ(0);
                }
                else if (AttachedEnemy != null)
                {
                    Speed = Speed.WithX(Speed.X / 2).WithZ(Speed.Z / 2);
                }
                // the game doesn't require pressed here, but presumably the control scheme would have the pressed flag
                // the game also doesn't check the ability flag here
                if (_abilities.TestFlag(AbilityFlags.AltForm) && Controls.Morph.IsPressed
                    || IsMainPlayer && CameraSequence.Current?.ForceBiped == true)
                {
                    TrySwitchForms();
                }
                if (Hunter == Hunter.Trace || Hunter == Hunter.Weavel)
                {
                    AnimationInfo info = _altModel.AnimInfo;
                    if (animId != -1)
                    {
                        if ((info.Index[0] != 1 || info.Flags[0].TestFlag(AnimFlags.Ended)) && animId != info.Index[0])
                        {
                            _altModel.SetAnimation(animId, animFlags);
                        }
                    }
                    else if (info.Index[0] != 0 && (!info.Flags[0].TestFlag(AnimFlags.NoLoop) || info.Flags[0].TestFlag(AnimFlags.Ended)))
                    {
                        _altModel.SetAnimation(0);
                    }
                }
            }
            ProcessMovement();
            UpdateCamera();
        }

        private void SpawnBomb()
        {
            // todo?: wi-fi condition and alternate function for spawning Lockjaw bombs
            Matrix4 transform = Matrix4.Identity;
            if (Hunter == Hunter.Kanden)
            {
                Matrix4 segMtx = _kandenSegMtx[4];
                transform = GetTransformMatrix(segMtx.Row2.Xyz, segMtx.Row1.Xyz, _kandenSegPos[4]);
            }
            else
            {
                if (Hunter == Hunter.Sylux && SyluxBombCount >= 3)
                {
                    SyluxBombs[2]!.Countdown = 0;
                    SyluxBombs[1]!.Countdown = 0;
                    SyluxBombs[0]!.Countdown = 0;
                    return;
                }
                transform = GetTransformMatrix(Vector3.UnitZ, Vector3.UnitY, Position.AddY(Fixed.ToFloat(-1000)));
            }
            var bomb = BombEntity.Spawn(this, transform, _scene);
            if (bomb != null)
            {
                if (Hunter == Hunter.Sylux)
                {
                    SyluxBombs[SyluxBombCount] = bomb;
                    bomb.BombIndex = SyluxBombCount++;
                    // todo?: wifi stuff
                }
                bomb.NodeRef = NodeRef;
                bomb.Radius = Fixed.ToFloat(Values.BombRadius);
                bomb.SelfRadius = Fixed.ToFloat(Values.BombSelfRadius);
                // todo: if bot and encounter state, set damage values
                // else...
                bomb.Damage = (ushort)Values.BombDamage;
                bomb.EnemyDamage = (ushort)Values.BombEnemyDamage;
                if (_doubleDmgTimer > 0)
                {
                    bomb.Damage *= 2;
                    bomb.EnemyDamage *= 2;
                }
                if (_bombAmmo >= 2)
                {
                    _bombRefillTimer = (ushort)(Values.BombRefillTime * 2); // todo: FPS stuff
                }
                _bombAmmo--;
                _bombCooldown = (ushort)(Values.BombCooldown * 2); // todo: FPS stuff
                if (Hunter == Hunter.Kanden)
                {
                    _altModel.SetAnimation((int)KandenAltAnim.TailOut, AnimFlags.NoLoop);
                }
                else if (Hunter == Hunter.Sylux && SyluxBombCount == 3)
                {
                    // todo: FPS stuff
                    _bombOveruse += 27 * 2;
                    if (_bombOveruse >= 100 * 2)
                    {
                        _bombCooldown = 150 * 2;
                    }
                }
                bomb.PlaySpawnSfx();
            }
        }

        private void EndAltAttack()
        {
            if (Hunter == Hunter.Samus)
            {
                Flags1 &= ~PlayerFlags1.Boosting;
            }
            else if (Hunter == Hunter.Trace || Hunter == Hunter.Weavel)
            {
                if (Flags2.TestFlag(PlayerFlags2.AltAttack))
                {
                    // todo: if bot and encounter state, set a 10f cooldown
                    _altAttackCooldown = (ushort)(Values.AltAttackCooldown * 2); // todo: FPS stuff
                }
            }
            else if (Hunter == Hunter.Noxus)
            {
                if (_altAttackTime > 0)
                {
                    _soundSource.StopSfx(SfxId.NOX_TOP_ATTACK1);
                    _soundSource.StopSfx(SfxId.NOX_TOP_ATTACK2);
                    if (_altAttackTime >= Values.AltAttackStartup / 2 * 2) // todo: FPS stuff
                    {
                        _soundSource.PlaySfx(SfxId.NOX_TOP_ATTACK3);
                    }
                    _altModel.SetAnimation((int)NoxusAltAnim.Extend, AnimFlags.Paused);
                    _altAttackTime = 0;
                }
            }
            Flags2 &= ~PlayerFlags2.AltAttack;
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
                _hSpeedMag = 0;
            }
            else
            {
                hSpeed /= hSpeedMag;
                if (Values.AltFormStrafe == 0)
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
                        _facingVector = new Vector3(_field70, 0, _field74);
                        _gunVec1 = _facingVector;
                        float add = _gunVec1.X * Fixed.ToFloat(Values.AimDistance);
                        _aimPosition = Position.AddX(add).AddZ(add);
                    }
                }
                if (IsAltForm)
                {
                    float altMin = Fixed.ToFloat(Values.AltMinHSpeed); // todo: FPS stuff?
                    if (_hSpeedCap <= altMin)
                    {
                        _hSpeedCap = altMin;
                    }
                    else if (hSpeedMag >= _hSpeedCap)
                    {
                        _hSpeedCap -= Fixed.ToFloat(Values.AltHSpeedCapIncrement) / 2; // todo: FPS stuff
                    }
                    else
                    {
                        _hSpeedCap = hSpeedMag;
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
                _hSpeedMag = hSpeedMag;
            }
            // todo: check how much of this overwrites stuff done above
            float hMag = MathF.Sqrt(_facingVector.X * _facingVector.X + _facingVector.Z * _facingVector.Z);
            _field70 = _facingVector.X / hMag;
            _field74 = _facingVector.Z / hMag;
            _gunVec2 = new Vector3(_field74, 0, -_field70);
            _field78 = _gunVec2.X;
            _field7C = _gunVec2.Z;
            _upVector = Vector3.Cross(_facingVector, _gunVec2).Normalized();
            if (Values.AltFormStrafe != 0)
            {
                _field80 = _field70;
                _field84 = _field74;
            }
            _aimPosition = _gunVec1 * Fixed.ToFloat(Values.AimDistance);
            _aimPosition += CameraInfo.Position;
            // unimpl-controls: this calculation is different when exact aim is not set
            hMag = MathF.Sqrt(_gunVec1.X * _gunVec1.X + _gunVec1.Z * _gunVec1.Z);
            _aimY = MathHelper.RadiansToDegrees(MathF.Atan2(_gunVec1.Y, hMag));
            if (_aimY > 75 || _aimY < -75)
            {
                UpdateAimY(0);
            }
            if (Flags1.TestFlag(PlayerFlags1.UsedJumpPad))
            {
                // basically exclude the jump pad speed for the rest of the speed calc, then restore it below
                float prevX = Speed.X;
                Speed = Speed.AddX(-_jumpPadAccel.X);
                if (prevX <= 0 && Speed.X > 0 || prevX > 0 && Speed.X < 0)
                {
                    _jumpPadAccel.X += Speed.X / 2; // todo: FPS stuff
                    Speed = Speed.WithX(0);
                }
                float prevZ = Speed.Z;
                Speed = Speed.AddZ(-_jumpPadAccel.Z);
                if (prevZ <= 0 && Speed.Z > 0 || prevZ > 0 && Speed.Z < 0)
                {
                    _jumpPadAccel.Z += Speed.Z / 2; // todo: FPS stuff
                    Speed = Speed.WithZ(0);
                }
            }
            float slideSfxAmount = 0;
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
                    slideSfxAmount = 0xFFFF * _hSpeedMag / Fixed.ToFloat(Values.WalkSpeedCap);
                }
            }
            UpdateSlidingSfx(slideSfxAmount);
            Vector3 speedMul = Speed.WithX(Speed.X * speedFactor).WithZ(Speed.Z * speedFactor);
            Speed += (speedMul - Speed) / 2; // todo: FPS stuff
            if (Flags1.TestFlag(PlayerFlags1.UsedJumpPad))
            {
                Speed = Speed.AddX(_jumpPadAccel.X);
                Speed = Speed.AddZ(_jumpPadAccel.Z);
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
                            if (Flags1.TestFlag(PlayerFlags1.Standing) && _slipperiness == 0 && Values.AltFormStrafe != 0)
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
                    _kandenSegPos[i] = _kandenSegPos[i].AddY(-0.1f / 2); // todo: FPS stuff
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
            Terrain prevTerrain = _standTerrain;
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
            if (_standTerrain != prevTerrain)
            {
                StopTerrainSfx(prevTerrain);
            }
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
                        CameraInfo.SetShake(Fixed.ToFloat(204));
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
                _timeBeforeLanding = _timeSinceGrounded;
                _timeSinceGrounded = 0;
                Flags1 |= PlayerFlags1.Grounded;
            }
            else if (_timeSinceGrounded < 90 * 2) // todo: FPS stuff
            {
                _timeSinceGrounded++;
                if (_timeSinceGrounded >= 8 * 2) // todo: FPS stuff
                {
                    Flags1 &= ~PlayerFlags1.Grounded;
                    _walkSfxTimer = 0;
                    _walkSfxIndex = 0;
                }
            }
            bool burning = false;
            if (_health > 0 && (_burnTimer > 0 || Hunter != Hunter.Spire
                && Flags1.TestFlag(PlayerFlags1.OnLava) && Flags1.TestFlag(PlayerFlags1.Grounded)))
            {
                burning = true;
            }
            UpdateBurningSfx(burning);
            if ((!IsAltForm || Hunter == Hunter.Weavel) && Flags1.TestFlag(PlayerFlags1.Grounded))
            {
                UpdateWalkingSfx();
            }
        }

        public static void ProcessInput(KeyboardState keyboardState, MouseState mouseState)
        {
            KeyboardState keyboardSnap = keyboardState.GetSnapshot();
            MouseState mouseSnap = mouseState.GetSnapshot();
            for (int i = 0; i < 1; i++) // skdebug
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
                        Keybind control = player.Controls.All[j];
                        if (control.Type == ButtonType.Key)
                        {
                            if (control.Key != Keys.Unknown)
                            {
                                bool prevDown = prevKeyboardSnap?.IsKeyDown(control.Key) ?? false;
                                control.IsDown = keyboardSnap.IsKeyDown(control.Key);
                                control.IsPressed = control.IsDown && !prevDown;
                                control.IsReleased = !control.IsDown && prevDown;
                            }
                        }
                        else if (control.Type == ButtonType.Mouse)
                        {
                            if (GameState.DialogPause)
                            {
                                continue;
                            }
                            if (control.MouseButton == MouseButton.Left && player._ignoreClick)
                            {
                                control.NeedsRepress = true;
                            }
                            bool down = mouseSnap.IsButtonDown(control.MouseButton);
                            bool prevDown = prevMouseSnap?.IsButtonDown(control.MouseButton) ?? false;
                            if (control.NeedsRepress && !player._ignoreClick)
                            {
                                if (!down || !prevDown)
                                {
                                    control.NeedsRepress = false;
                                }
                            }
                            if (!control.NeedsRepress)
                            {
                                control.IsDown = down;
                                control.IsPressed = control.IsDown && !prevDown;
                                control.IsReleased = !control.IsDown && prevDown;
                            }
                        }
                        else
                        {
                            // todo?: deal with overflow or whatever
                            float curScrollY = mouseSnap.Scroll.Y;
                            float prevScrollY = prevMouseSnap?.Scroll.Y ?? 0;
                            control.IsDown = control.Type == ButtonType.ScrollUp && curScrollY > prevScrollY
                                || control.Type == ButtonType.ScrollDown && curScrollY < prevScrollY;
                            control.IsPressed = control.IsDown;
                            control.IsReleased = false;
                        }
                    }
                }
                player._ignoreClick = false;
                if (mouseSnap.IsButtonDown(MouseButton.Left) && prevMouseSnap?.IsButtonDown(MouseButton.Left) != true)
                {
                    player.Input.ClickX = mouseSnap.X;
                    player.Input.ClickY = mouseSnap.Y;
                }
                else
                {
                    player.Input.ClickX = -1;
                    player.Input.ClickY = -1;
                }
            }
        }

        public PlayerControls Controls { get; } = PlayerControls.GetDefault();
        private PlayerInput Input { get; } = new PlayerInput();

        private class PlayerInput
        {
            public KeyboardState? PrevKeyboardState { get; set; }
            public KeyboardState? KeyboardState { get; set; }
            public MouseState? PrevMouseState { get; set; }
            public MouseState? MouseState { get; set; }

            public float MouseDeltaX => (MouseState?.X - PrevMouseState?.X) ?? 0;
            public float MouseDeltaY => (MouseState?.Y - PrevMouseState?.Y) ?? 0;
            public float ClickX { get; set; } = -1;
            public float ClickY { get; set; } = -1;
        }
    }

    public enum ButtonType
    {
        Key,
        Mouse,
        ScrollUp,
        ScrollDown
    }

    public class Keybind
    {
        public ButtonType Type { get; set; }
        public Keys Key { get; set; }
        public MouseButton MouseButton { get; set; }

        public bool IsPressed { get; set; }
        public bool IsDown { get; set; }
        public bool IsReleased { get; set; }
        public bool NeedsRepress { get; set; }

        public Keybind(Keys key)
        {
            Type = ButtonType.Key;
            Key = key;
        }

        public Keybind(MouseButton mouseButton)
        {
            Type = ButtonType.Mouse;
            MouseButton = mouseButton;
        }

        public Keybind(ButtonType scrollType)
        {
            if (scrollType != ButtonType.ScrollUp && scrollType != ButtonType.ScrollDown)
            {
                throw new ProgramException("Unexpected control type.");
            }
            Type = scrollType;
        }

        public static bool operator ==(Keybind lhs, Keybind rhs)
        {
            return lhs.Type == rhs.Type && lhs.Key == rhs.Key && lhs.MouseButton == rhs.MouseButton;
        }

        public static bool operator !=(Keybind lhs, Keybind rhs)
        {
            return lhs.Type != rhs.Type || lhs.Key != rhs.Key || lhs.MouseButton != rhs.MouseButton;
        }

        public override bool Equals(object? obj)
        {
            return obj is Keybind other && Type == other.Type && Key == other.Key && MouseButton == other.MouseButton;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Key, MouseButton);
        }
    }

    public class PlayerControls
    {
        public bool MouseAim { get; set; }
        public bool KeyboardAim { get; set; }
        public Keybind MoveLeft { get; }
        public Keybind MoveRight { get; }
        public Keybind MoveUp { get; }
        public Keybind MoveDown { get; }
        public Keybind AimLeft { get; }
        public Keybind AimRight { get; }
        public Keybind AimUp { get; }
        public Keybind AimDown { get; }
        public Keybind Shoot { get; }
        public Keybind Zoom { get; }
        public Keybind Jump { get; }
        public Keybind Morph { get; }
        public Keybind Boost { get; }
        public Keybind AltAttack { get; }
        public Keybind ScanVisor { get; }
        public Keybind Scan { get; }
        public Keybind NextWeapon { get; }
        public Keybind PrevWeapon { get; }
        public Keybind WeaponMenu { get; }
        public Keybind PowerBeam { get; }
        public Keybind Missile { get; }
        public Keybind VoltDriver { get; }
        public Keybind Battlehammer { get; }
        public Keybind Imperialist { get; }
        public Keybind Judicator { get; }
        public Keybind Magmaul { get; }
        public Keybind ShockCoil { get; }
        public Keybind OmegaCannon { get; }
        public Keybind AffinitySlot { get; }
        public Keybind Pause { get; }

        public bool InvertAimY { get; }
        public bool InvertAimX { get; }
        public bool ScrollAllWeapons { get; }

        public Keybind[] All { get; }

        public PlayerControls(Keybind moveLeft, Keybind moveRight, Keybind moveUp, Keybind moveDown, Keybind aimLeft, Keybind aimRight,
            Keybind aimUp, Keybind aimDown, Keybind shoot, Keybind zoom, Keybind jump, Keybind morph, Keybind boost, Keybind altAttack,
            Keybind scanVisor, Keybind scan, Keybind nextWeapon, Keybind prevWeapon, Keybind weaponMenu, Keybind powerBeam, Keybind missile,
            Keybind voltDriver, Keybind battlehammer, Keybind imperialist, Keybind judicator, Keybind magmaul, Keybind shockCoil,
            Keybind omegaCannon, Keybind affinitySlot, Keybind pause)
        {
            MouseAim = true;
            KeyboardAim = true;
            ScrollAllWeapons = false;
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
            ScanVisor = scanVisor;
            Scan = scan;
            NextWeapon = nextWeapon;
            PrevWeapon = prevWeapon;
            WeaponMenu = weaponMenu;
            PowerBeam = powerBeam;
            Missile = missile;
            VoltDriver = voltDriver;
            Battlehammer = battlehammer;
            Imperialist = imperialist;
            Judicator = judicator;
            Magmaul = magmaul;
            ShockCoil = shockCoil;
            OmegaCannon = omegaCannon;
            AffinitySlot = affinitySlot;
            Pause = pause;
            All = new[]
            {
                moveLeft, moveRight, moveUp, moveDown, aimLeft, aimRight, aimUp, aimDown, shoot, zoom, jump, morph, boost,
                altAttack, scanVisor, scan, nextWeapon, prevWeapon, weaponMenu, powerBeam, missile, voltDriver, battlehammer,
                imperialist, judicator, magmaul, shockCoil, omegaCannon, affinitySlot, pause
            };
        }

        public void ClearAll()
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].IsDown = false;
                All[i].IsPressed = false;
                All[i].IsReleased = false;
            }
        }

        public void ClearPressed()
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].IsPressed = false;
            }
        }

        public static PlayerControls GetDefault()
        {
            return new PlayerControls(
                moveLeft: new Keybind(Keys.A),
                moveRight: new Keybind(Keys.D),
                moveUp: new Keybind(Keys.W),
                moveDown: new Keybind(Keys.S),
                aimLeft: new Keybind(Keys.Left),
                aimRight: new Keybind(Keys.Right),
                aimUp: new Keybind(Keys.Up),
                aimDown: new Keybind(Keys.Down),
                shoot: new Keybind(MouseButton.Left),
                zoom: new Keybind(MouseButton.Right),
                jump: new Keybind(Keys.Space),
                morph: new Keybind(Keys.C),
                boost: new Keybind(Keys.Space),
                altAttack: new Keybind(Keys.Q),
                scanVisor: new Keybind(Keys.E),
                scan: new Keybind(Keys.Q),
                nextWeapon: new Keybind(ButtonType.ScrollDown),
                prevWeapon: new Keybind(ButtonType.ScrollUp),
                weaponMenu: new Keybind(MouseButton.Middle),
                powerBeam: new Keybind(Keys.D1),
                missile: new Keybind(Keys.D2),
                voltDriver: new Keybind(Keys.D3),
                battlehammer: new Keybind(Keys.D4),
                imperialist: new Keybind(Keys.D5),
                judicator: new Keybind(Keys.D6),
                magmaul: new Keybind(Keys.D7),
                shockCoil: new Keybind(Keys.D8),
                omegaCannon: new Keybind(Keys.D9),
                affinitySlot: new Keybind(Keys.Unknown),
                pause: new Keybind(Keys.Tab)
            );
        }
    }
}
