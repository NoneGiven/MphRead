using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// What the pad is actually doing, asked without a match in the way.
    ///
    /// "My controller does nothing" has four causes that look identical from
    /// inside a game -- the pad is not connected, GLFW has no mapping for it
    /// (so its buttons are in the wrong places or absent), the dead zone is
    /// eating the sticks, or the mapping to game actions is wrong. This
    /// separates them: it prints the pad's name, the axes as they move, and
    /// which game action each button reaches. No window, no room, no game
    /// files.
    /// </summary>
    internal static class GamepadProbe
    {
        public static int Run(double seconds)
        {
            if (!GLFW.Init())
            {
                Console.WriteLine("[gamepad] GLFW would not start; no pads can be read here.");
                return 1;
            }
            try
            {
                return Watch(seconds);
            }
            finally
            {
                GLFW.Terminate();
            }
        }

        private static int Watch(double seconds)
        {
            Console.WriteLine($"[gamepad] watching for {seconds:0} s. "
                + $"dead zone {InputSettings.GamepadDeadZone:0.00}, "
                + $"look {InputSettings.GamepadLookSensitivity:0.00}, "
                + $"invert y {(InputSettings.GamepadInvertY ? "on" : "off")}");
            Console.WriteLine("[gamepad] buttons: " + String.Join(", ",
                PadBindings.Actions.Select(a =>
                    $"{PadBindings.Name(a)} {PadBindings.Describe(PadBindings.Get(a))}")));
            ReportPresence();
            var clock = Stopwatch.StartNew();
            string last = "";
            bool everConnected = false;
            bool everMoved = false;
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                // GLFW only refreshes joystick state inside an event poll, and
                // with no window there is nothing else to pump it.
                GLFW.PollEvents();
                GamepadDesktop.Poll();
                GamepadInput.BeginFrame();
                GamepadState state = GamepadInput.State;
                everConnected |= state.Connected;
                string line = Describe(state);
                if (line != last)
                {
                    last = line;
                    Console.WriteLine($"  {clock.Elapsed.TotalSeconds,5:0.0}s {line}");
                    everMoved |= state.Connected
                        && (state.Buttons != GamepadButtons.None
                            || MathF.Abs(state.LeftX) > 0.5f || MathF.Abs(state.LeftY) > 0.5f
                            || MathF.Abs(state.RightX) > 0.5f || MathF.Abs(state.RightY) > 0.5f);
                }
                Thread.Sleep(16);
            }
            Console.WriteLine();
            if (!everConnected)
            {
                Console.WriteLine("[gamepad] FAIL: no pad was seen -- nothing is plugged in or "
                    + "paired that reports itself as a joystick at all. A pad GLFW has no "
                    + "mapping for would still have been listed above and read raw.");
                return 1;
            }
            if (!everMoved)
            {
                Console.WriteLine("[gamepad] a pad is connected, but nothing was pressed or "
                    + "moved far enough to read.");
                return 1;
            }
            Console.WriteLine("[gamepad] PASS: a pad is connected and its input arrives.");
            return 0;
        }

        /// <summary>
        /// Every joystick slot GLFW can see, and whether it has a mapping.
        ///
        /// The distinction is the whole point: a pad that is *present* but not
        /// *mapped* is the case where the game ignores it entirely, and it
        /// looks exactly like nothing being plugged in unless somebody says so.
        /// </summary>
        private static void ReportPresence()
        {
            // Before the listing, because a mapping file that has just been
            // read may be the reason a pad below says "mapped".
            GamepadMappings.EnsureLoaded();
            Console.WriteLine($"[gamepad] {GamepadMappings.Summary}");
            int found = 0;
            for (int i = 0; i < 16; i++)
            {
                if (!GLFW.JoystickPresent(i))
                {
                    continue;
                }
                found++;
                string name = GLFW.GetJoystickName(i) ?? "?";
                if (GLFW.JoystickIsGamepad(i))
                {
                    Console.WriteLine($"  slot {i}: {name} -- mapped, usable");
                    continue;
                }
                // Not the dead end it used to be: the game reads such a pad
                // raw, on a guess at what its numbers mean. Which means the
                // useful thing to print is not "unsupported" but the guess
                // itself, and the one line that would replace it.
                Console.WriteLine($"  slot {i}: {name} -- no mapping for this device; "
                    + "read raw, on a guessed layout");
                Console.WriteLine($"    axes {GLFW.GetJoystickAxes(i).Length}, "
                    + $"buttons {GLFW.GetJoystickButtons(i).Length}, "
                    + $"hats {GLFW.GetJoystickHats(i).Length}");
                Console.WriteLine("    if any button below is in the wrong place, correct this "
                    + $"line and put it in {GamepadMappings.FileName}, beside the game or "
                    + "with your settings:");
                Console.WriteLine($"    {GamepadMappings.Suggest(i)}");
            }
            if (found == 0)
            {
                Console.WriteLine("  no joystick in any slot");
            }
        }

        private static string Describe(GamepadState state)
        {
            if (!state.Connected)
            {
                return "no pad";
            }
            var text = new StringBuilder();
            text.Append($"{state.Name}  L({state.LeftX,5:0.00},{state.LeftY,5:0.00})");
            text.Append($" R({state.RightX,5:0.00},{state.RightY,5:0.00})");
            text.Append($" LT{state.LeftTrigger:0.00} RT{state.RightTrigger:0.00}");
            text.Append($"  aim({GamepadInput.AimDeltaX,6:0.00},{GamepadInput.AimDeltaY,6:0.00})");
            if (state.Buttons != GamepadButtons.None)
            {
                text.Append($"  {state.Buttons}");
                text.Append($"  -> {Actions(state.Buttons)}");
            }
            return text.ToString();
        }

        /// <summary>
        /// What each pressed button does in a match. Mirrors
        /// <see cref="GamepadInput.Apply"/> -- the one thing this probe cannot
        /// do is read it out of that method, since the mapping is a run of
        /// calls rather than a table, so the two are kept side by side.
        /// </summary>
        private static string Actions(GamepadButtons buttons)
        {
            var text = new StringBuilder();
            Name(text, buttons, GamepadButtons.RightTrigger, "shoot/alt-attack");
            Name(text, buttons, GamepadButtons.LeftTrigger, "zoom");
            Name(text, buttons, GamepadButtons.A, "jump/boost");
            Name(text, buttons, GamepadButtons.B, "morph");
            Name(text, buttons, GamepadButtons.X, "scan");
            Name(text, buttons, GamepadButtons.Y, "scan visor");
            Name(text, buttons, GamepadButtons.Back, "scoreboard");
            Name(text, buttons, GamepadButtons.RightBumper | GamepadButtons.DpadRight,
                "next weapon");
            Name(text, buttons, GamepadButtons.LeftBumper | GamepadButtons.DpadLeft,
                "previous weapon");
            Name(text, buttons, GamepadButtons.DpadUp, "missile");
            Name(text, buttons, GamepadButtons.DpadDown, "power beam");
            Name(text, buttons, GamepadButtons.Start, "pause menu");
            return text.Length == 0 ? "(nothing bound)" : text.ToString();
        }

        private static void Name(StringBuilder text, GamepadButtons buttons,
            GamepadButtons match, string action)
        {
            if ((buttons & match) != 0)
            {
                text.Append(text.Length > 0 ? ", " : "");
                text.Append(action);
            }
        }
    }
}
