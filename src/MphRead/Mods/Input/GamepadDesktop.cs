using System;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// The desktop's pad, read from GLFW.
    ///
    /// <c>glfwGetGamepadState</c> where it can be: GLFW carries SDL's
    /// controller database, so a DualShock, a Switch Pro pad, an eight-bit-do
    /// and an Xbox pad all arrive already remapped onto one layout, over USB
    /// or over Bluetooth alike -- the operating system has already decided
    /// which of those it is by the time a pad reaches here, and Bluetooth is
    /// not a different kind of device to it.
    ///
    /// And the raw joystick API where it cannot. That database is a snapshot,
    /// frozen at whichever GLFW the OpenTK redist ships, so "GLFW has never
    /// heard of this pad" is not a rare case -- it is every pad released since
    /// that snapshot and every one too obscure to have been in it. Such a pad
    /// used to be skipped in silence, which is indistinguishable from nothing
    /// being plugged in and is most of what "my controller does nothing"
    /// means. Three things now happen before that conclusion is reached:
    /// <see cref="GamepadMappings"/> offers GLFW whatever mapping files this
    /// machine has, the scan below falls back to reading the pad raw through
    /// <see cref="GamepadLayout"/>'s guess, and <c>-gamepad</c> prints the
    /// mapping line that would replace the guess.
    ///
    /// Polled rather than evented, because that is the only shape GLFW offers
    /// for pads and because the game is a frame loop anyway. Sixteen slots is
    /// GLFW's own maximum; the first one that answers wins, since nothing here
    /// has a second player to give the second pad to -- and a mapped pad wins
    /// over an unmapped one wherever both are plugged in, since the mapped one
    /// is the one whose buttons are known rather than assumed.
    /// </summary>
    internal static class GamepadDesktop
    {
        private static int _slot = -1;
        private static int _rescanCountdown;

        /// <summary>
        /// Whether the pad in <see cref="_slot"/> is being read raw. Kept so
        /// the per-frame read goes back to the same API that found it, and so
        /// a rescan can still prefer a mapped pad that appears later.
        /// </summary>
        private static bool _rawSlot;

        /// <summary>
        /// Frames between hunts for a pad when none is connected. Once a
        /// second: <c>glfwGetGamepadState</c> on sixteen empty slots is cheap
        /// but not free, and a pad switched on mid-match should be usable
        /// without restarting anything.
        /// </summary>
        private const int RescanFrames = 60;

        // GLFW's own gamepad indices. Written out rather than taken from an
        // enum because OpenTK 4.9 binds `glfwGetGamepadState` without binding
        // the two enums that name its slots -- `GamepadState` is a pair of
        // fixed arrays and nothing else. These are GLFW_GAMEPAD_BUTTON_* and
        // GLFW_GAMEPAD_AXIS_*, which are part of its stable API.
        private const int ButtonA = 0;
        private const int ButtonB = 1;
        private const int ButtonX = 2;
        private const int ButtonY = 3;
        private const int ButtonLeftBumper = 4;
        private const int ButtonRightBumper = 5;
        private const int ButtonBack = 6;
        private const int ButtonStart = 7;
        private const int ButtonLeftThumb = 9;
        private const int ButtonRightThumb = 10;
        private const int ButtonDpadUp = 11;
        private const int ButtonDpadRight = 12;
        private const int ButtonDpadDown = 13;
        private const int ButtonDpadLeft = 14;

        private const int AxisLeftX = 0;
        private const int AxisLeftY = 1;
        private const int AxisRightX = 2;
        private const int AxisRightY = 3;
        private const int AxisLeftTrigger = 4;
        private const int AxisRightTrigger = 5;

        /// <summary>
        /// Whether GLFW has been stood up from here. Only ever set true; the
        /// game's own window may own the library by then and terminating it
        /// would take the window with it.
        /// </summary>
        private static bool _initialised;

        /// <summary>
        /// Poll for a settings screen rather than for a frame.
        ///
        /// Two things the game loop does for free and a menu does not. GLFW
        /// only refreshes joystick state inside an event poll, and a launcher
        /// shown before any match has no window pumping one; and the library
        /// may not have been initialised at all yet, since OpenTK does that
        /// when it creates the game's window. Both are cheap to ask for again
        /// and neither is an error when it has already happened.
        ///
        /// Called only while a <c>PadRow</c> is listening for a button, so
        /// nothing here runs for a settings screen nobody is rebinding on.
        /// </summary>
        public static void PollForMenu()
        {
            if (OperatingSystem.IsAndroid())
            {
                // Evented there: the activity puts pad presses into the state
                // whether or not a match is running. See GamepadBridge.
                return;
            }
            try
            {
                if (!_initialised)
                {
                    GLFW.Init();
                    _initialised = true;
                }
                GLFW.PollEvents();
            }
            catch (Exception ex) when (ex is DllNotFoundException
                || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
                _slot = -2;
                return;
            }
            Poll();
        }

        public static void Poll()
        {
            if (OperatingSystem.IsAndroid())
            {
                // The Android head has no GLFW at all -- its window is an
                // Android one and its pad arrives as key and motion events.
                // See GamepadBridge there.
                return;
            }
            try
            {
                PollUnsafe();
            }
            catch (Exception ex) when (ex is DllNotFoundException
                || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
                // No GLFW in this process: the dedicated server, the map
                // audit, anything headless. Not an error -- there is no window
                // and there is nobody holding a pad.
                GamepadInput.State = default;
                _slot = -2;
            }
        }

        private static void PollUnsafe()
        {
            if (_slot == -2)
            {
                return;
            }
            GamepadMappings.EnsureLoaded();
            if (_slot >= 0 && (_rawSlot ? TryReadRaw(_slot) : TryRead(_slot)))
            {
                return;
            }
            _slot = -1;
            GamepadInput.State = default;
            if (_rescanCountdown-- > 0)
            {
                return;
            }
            _rescanCountdown = RescanFrames;
            // Mapped pads first, all sixteen slots of them, before any
            // unmapped one is considered: a pad SDL's database knows has its
            // buttons where it says they are, and one read raw has them where
            // GamepadLayout guesses. Somebody with both plugged in should be
            // playing on the one that is right.
            for (int i = 0; i < 16; i++)
            {
                if (TryRead(i))
                {
                    _slot = i;
                    _rawSlot = false;
                    Console.WriteLine($"[input] gamepad: {GamepadInput.State.Name}");
                    return;
                }
            }
            for (int i = 0; i < 16; i++)
            {
                if (TryReadRaw(i))
                {
                    _slot = i;
                    _rawSlot = true;
                    Console.WriteLine($"[input] gamepad: {GamepadInput.State.Name}"
                        + " -- no mapping for this device, reading it raw."
                        + " Run -gamepad to check the buttons, and rebind in"
                        + " Settings, Controls if any are in the wrong place.");
                    return;
                }
            }
        }

        private static unsafe bool TryRead(int slot)
        {
            if (!GLFW.JoystickIsGamepad(slot)
                || !GLFW.GetGamepadState(slot, out OpenTK.Windowing.GraphicsLibraryFramework
                    .GamepadState raw))
            {
                return false;
            }
            var state = new Mods.Input.GamepadState
            {
                Connected = true,
                Name = GLFW.GetGamepadName(slot) ?? "gamepad",
                LeftX = raw.Axes[AxisLeftX],
                // Negated: GLFW reports a stick pushed forward as -1 and every
                // caller above wants forward to be positive. See GamepadState.
                LeftY = -raw.Axes[AxisLeftY],
                RightX = raw.Axes[AxisRightX],
                RightY = -raw.Axes[AxisRightY],
                // Triggers rest at -1 and go to 1, unlike the sticks, so they
                // are moved onto 0..1 here rather than at each use.
                LeftTrigger = (raw.Axes[AxisLeftTrigger] + 1) / 2,
                RightTrigger = (raw.Axes[AxisRightTrigger] + 1) / 2
            };
            GamepadButtons buttons = GamepadButtons.None;
            Add(ref buttons, raw.Buttons, ButtonA, GamepadButtons.A);
            Add(ref buttons, raw.Buttons, ButtonB, GamepadButtons.B);
            Add(ref buttons, raw.Buttons, ButtonX, GamepadButtons.X);
            Add(ref buttons, raw.Buttons, ButtonY, GamepadButtons.Y);
            Add(ref buttons, raw.Buttons, ButtonLeftBumper, GamepadButtons.LeftBumper);
            Add(ref buttons, raw.Buttons, ButtonRightBumper, GamepadButtons.RightBumper);
            Add(ref buttons, raw.Buttons, ButtonBack, GamepadButtons.Back);
            Add(ref buttons, raw.Buttons, ButtonStart, GamepadButtons.Start);
            Add(ref buttons, raw.Buttons, ButtonLeftThumb, GamepadButtons.LeftThumb);
            Add(ref buttons, raw.Buttons, ButtonRightThumb, GamepadButtons.RightThumb);
            Add(ref buttons, raw.Buttons, ButtonDpadUp, GamepadButtons.DpadUp);
            Add(ref buttons, raw.Buttons, ButtonDpadRight, GamepadButtons.DpadRight);
            Add(ref buttons, raw.Buttons, ButtonDpadDown, GamepadButtons.DpadDown);
            Add(ref buttons, raw.Buttons, ButtonDpadLeft, GamepadButtons.DpadLeft);
            if (state.LeftTrigger > TriggerPress)
            {
                buttons |= GamepadButtons.LeftTrigger;
            }
            if (state.RightTrigger > TriggerPress)
            {
                buttons |= GamepadButtons.RightTrigger;
            }
            state.Buttons = buttons;
            GamepadInput.State = state;
            return true;
        }

        /// <summary>
        /// A pad GLFW has no mapping for, read through the raw joystick API
        /// and <see cref="GamepadLayout"/>'s guess at what its numbers mean.
        ///
        /// Only reached once every slot has been offered to
        /// <see cref="TryRead"/>, so a pad that is properly mapped never comes
        /// through here. What arrives above this file is the same
        /// <see cref="GamepadState"/> either way: nothing downstream knows or
        /// cares which of the two read it, which is what keeps the guess in
        /// one place.
        ///
        /// The name says so, because the settings screen shows it and "why is
        /// my B button jumping" deserves an answer in the one place somebody
        /// will look.
        /// </summary>
        private static bool TryReadRaw(int slot)
        {
            if (!GLFW.JoystickPresent(slot) || GLFW.JoystickIsGamepad(slot))
            {
                return false;
            }
            float[] axes = GLFW.GetJoystickAxes(slot).ToArray();
            JoystickInputAction[] buttons = GLFW.GetJoystickButtons(slot).ToArray();
            // A device with no axes and no buttons is not something anybody is
            // playing with -- and GLFW counts things that are not pads at all
            // as joysticks, from steering wheels to the accelerometer in a
            // laptop lid.
            if (axes.Length < 2 || buttons.Length < 4)
            {
                return false;
            }
            if (_floorSlot != slot)
            {
                _floorSlot = slot;
                _leftFloor = 0;
                _rightFloor = 0;
            }
            GamepadLayout layout = GamepadLayout.For(slot);
            var state = new Mods.Input.GamepadState
            {
                Connected = true,
                Name = (GLFW.GetJoystickName(slot) ?? "gamepad") + " (unmapped)",
                LeftX = Axis(axes, layout.AxisLeftX),
                LeftY = -Axis(axes, layout.AxisLeftY),
                RightX = Axis(axes, layout.AxisRightX),
                RightY = -Axis(axes, layout.AxisRightY),
                LeftTrigger = Trigger(axes, layout.AxisLeftTrigger, ref _leftFloor),
                RightTrigger = Trigger(axes, layout.AxisRightTrigger, ref _rightFloor)
            };
            GamepadButtons flags = GamepadButtons.None;
            AddRaw(ref flags, buttons, layout.ButtonA, GamepadButtons.A);
            AddRaw(ref flags, buttons, layout.ButtonB, GamepadButtons.B);
            AddRaw(ref flags, buttons, layout.ButtonX, GamepadButtons.X);
            AddRaw(ref flags, buttons, layout.ButtonY, GamepadButtons.Y);
            AddRaw(ref flags, buttons, layout.ButtonLeftBumper, GamepadButtons.LeftBumper);
            AddRaw(ref flags, buttons, layout.ButtonRightBumper, GamepadButtons.RightBumper);
            AddRaw(ref flags, buttons, layout.ButtonBack, GamepadButtons.Back);
            AddRaw(ref flags, buttons, layout.ButtonStart, GamepadButtons.Start);
            AddRaw(ref flags, buttons, layout.ButtonLeftThumb, GamepadButtons.LeftThumb);
            AddRaw(ref flags, buttons, layout.ButtonRightThumb, GamepadButtons.RightThumb);
            // On a pad whose triggers are plain buttons they are still the
            // trigger flags, not two more face buttons: FIRE is on the right
            // trigger by default and should be wherever the player's finger
            // already is.
            AddRaw(ref flags, buttons, layout.ButtonLeftTrigger, GamepadButtons.LeftTrigger);
            AddRaw(ref flags, buttons, layout.ButtonRightTrigger, GamepadButtons.RightTrigger);
            // The d-pad is the one part of an unmapped pad that is not a
            // guess: GLFW reports hats separately, in one shape, on every
            // device that has one.
            JoystickHats[] hats = GLFW.GetJoystickHats(slot).ToArray();
            if (hats.Length > 0)
            {
                JoystickHats hat = hats[0];
                AddHat(ref flags, hat, JoystickHats.Up, GamepadButtons.DpadUp);
                AddHat(ref flags, hat, JoystickHats.Right, GamepadButtons.DpadRight);
                AddHat(ref flags, hat, JoystickHats.Down, GamepadButtons.DpadDown);
                AddHat(ref flags, hat, JoystickHats.Left, GamepadButtons.DpadLeft);
            }
            if (state.LeftTrigger > TriggerPress)
            {
                flags |= GamepadButtons.LeftTrigger;
            }
            if (state.RightTrigger > TriggerPress)
            {
                flags |= GamepadButtons.RightTrigger;
            }
            state.Buttons = flags;
            GamepadInput.State = state;
            return true;
        }

        /// <summary>
        /// The lowest each trigger axis has been seen at, and which slot they
        /// were measured on.
        ///
        /// An analogue trigger rests at -1 and pulls to 1 on most pads, and
        /// rests at 0 on some. Both conventions exist, nothing in the raw API
        /// says which one a device follows, and reading a 0-resting trigger as
        /// though it rested at -1 leaves it reporting itself half pulled while
        /// nobody is touching it. Watching where it actually sits costs two
        /// floats and covers both.
        /// </summary>
        private static int _floorSlot = -1;
        private static float _leftFloor;
        private static float _rightFloor;

        private static float Axis(float[] axes, int index)
        {
            return index >= 0 && index < axes.Length ? axes[index] : 0;
        }

        private static float Trigger(float[] axes, int index, ref float floor)
        {
            if (index < 0 || index >= axes.Length)
            {
                return 0;
            }
            float value = axes[index];
            if (value < floor)
            {
                floor = value;
            }
            float span = 1 - floor;
            return span <= 0 ? 0 : Math.Clamp((value - floor) / span, 0, 1);
        }

        private static void AddRaw(ref GamepadButtons into, JoystickInputAction[] buttons,
            int index, GamepadButtons flag)
        {
            if (index >= 0 && index < buttons.Length
                && buttons[index] == JoystickInputAction.Press)
            {
                into |= flag;
            }
        }

        private static void AddHat(ref GamepadButtons into, JoystickHats hat,
            JoystickHats match, GamepadButtons flag)
        {
            // Flags, not values: a hat pushed diagonally reports Up and Right
            // at once as RightUp, and both directions should reach the game.
            if ((hat & match) != 0)
            {
                into |= flag;
            }
        }

        /// <summary>
        /// Matches <c>GamepadInput</c>'s own threshold: the Android head sets
        /// the same two flags from its own trigger axes, so the number has to
        /// be the same on both or the same pull fires on one platform and not
        /// the other.
        /// </summary>
        private const float TriggerPress = 0.65f;

        private static unsafe void Add(ref GamepadButtons into,
            byte* buttons, int index, GamepadButtons flag)
        {
            if (buttons[index] == (byte)JoystickInputAction.Press)
            {
                into |= flag;
            }
        }
    }
}
