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
                    // tood: update field684
                    if (Controls.MoveUp.IsDown)
                    {
                        MoveForwardBack(PlayerAnimation.WalkForward, sign: 1);
                    }
                    else if (Controls.MoveDown.IsDown)
                    {
                        MoveForwardBack(PlayerAnimation.WalkBackward, sign: -1);
                    }
                    // todo: update HUD y shift
                    // tood: update field684
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
                            Speed = Speed.WithY(0.35f / 2); // todo: FPS stuff
                        }
                        else
                        {
                            Speed = Speed.WithY(Fixed.ToFloat(Values.JumpSpeed) / 2); // todo: FPS stuff
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
                // skhere
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

        private void ProcessAlt()
        {
            // sktodo
        }

        private void ProcessMovement()
        {
            // sktodo
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
                shoot: new ButtonControl(MouseButton.Button1),
                zoom: new ButtonControl(MouseButton.Button2),
                jump: new ButtonControl(Keys.Space),
                morph: new ButtonControl(Keys.C),
                boost: new ButtonControl(Keys.Space),
                altAttack: new ButtonControl(Keys.Q)
            );
        }
    }
}
