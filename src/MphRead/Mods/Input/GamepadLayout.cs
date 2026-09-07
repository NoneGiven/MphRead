using System;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// Which axis and which button is which, on a pad nobody has a mapping
    /// for.
    ///
    /// The second answer to an unmapped pad, after <see cref="GamepadMappings"/>
    /// -- and the one that needs nothing of the player. GLFW's raw joystick API
    /// hands over a bag of numbered axes and numbered buttons with no idea what
    /// any of them are for, which is exactly why <c>glfwJoystickIsGamepad</c>
    /// exists and why an unmapped pad was ignored. But the bag is not
    /// arbitrary: the two shapes below cover nearly every pad that reaches a PC,
    /// and picking between them by counting axes gets an unknown pad playing
    /// the game instead of doing nothing at all.
    ///
    /// <b>Six or more axes</b> -- an Xbox-shaped pad with analogue triggers,
    /// the layout Linux's own driver reports and the one SDL's database gives
    /// nearly every such device: sticks on 0/1 and 3/4, triggers on 2 and 5,
    /// buttons A B X Y LB RB Back Start Guide L3 R3 in that order.
    ///
    /// <b>Four or five axes</b> -- the flat "USB gamepad" shape, where the
    /// shoulders and triggers are all four plain buttons: sticks on 0/1 and
    /// 2/3, then four face buttons, two shoulders, two triggers, select, start,
    /// and the two stick clicks.
    ///
    /// Both are guesses, and a guess is allowed here because of what it is
    /// weighed against. Every action but the two sticks is rebindable in
    /// Settings -> Controls, so a player whose face buttons come out shuffled
    /// can put them right in a minute; a player whose pad is ignored has
    /// nothing to put right. <c>-gamepad</c> prints what each button reached
    /// and offers the mapping line that would make the guess unnecessary.
    ///
    /// The d-pad is not guessed at: GLFW reports hats separately from buttons
    /// and every pad that has one reports it the same way.
    /// </summary>
    internal readonly struct GamepadLayout
    {
        public readonly int AxisLeftX;
        public readonly int AxisLeftY;
        public readonly int AxisRightX;
        public readonly int AxisRightY;
        /// <summary>Trigger axis, or -1 where the triggers are buttons.</summary>
        public readonly int AxisLeftTrigger;
        public readonly int AxisRightTrigger;
        public readonly int ButtonA;
        public readonly int ButtonB;
        public readonly int ButtonX;
        public readonly int ButtonY;
        public readonly int ButtonLeftBumper;
        public readonly int ButtonRightBumper;
        /// <summary>Trigger button, or -1 where the triggers are axes.</summary>
        public readonly int ButtonLeftTrigger;
        public readonly int ButtonRightTrigger;
        public readonly int ButtonBack;
        public readonly int ButtonStart;
        public readonly int ButtonLeftThumb;
        public readonly int ButtonRightThumb;

        private GamepadLayout(int axisLeftX, int axisLeftY, int axisRightX, int axisRightY,
            int axisLeftTrigger, int axisRightTrigger, int buttonA, int buttonB, int buttonX,
            int buttonY, int buttonLeftBumper, int buttonRightBumper, int buttonLeftTrigger,
            int buttonRightTrigger, int buttonBack, int buttonStart, int buttonLeftThumb,
            int buttonRightThumb)
        {
            AxisLeftX = axisLeftX;
            AxisLeftY = axisLeftY;
            AxisRightX = axisRightX;
            AxisRightY = axisRightY;
            AxisLeftTrigger = axisLeftTrigger;
            AxisRightTrigger = axisRightTrigger;
            ButtonA = buttonA;
            ButtonB = buttonB;
            ButtonX = buttonX;
            ButtonY = buttonY;
            ButtonLeftBumper = buttonLeftBumper;
            ButtonRightBumper = buttonRightBumper;
            ButtonLeftTrigger = buttonLeftTrigger;
            ButtonRightTrigger = buttonRightTrigger;
            ButtonBack = buttonBack;
            ButtonStart = buttonStart;
            ButtonLeftThumb = buttonLeftThumb;
            ButtonRightThumb = buttonRightThumb;
        }

        /// <summary>The Xbox-shaped pad: analogue triggers on their own axes.</summary>
        private static readonly GamepadLayout Triggers = new GamepadLayout(
            axisLeftX: 0, axisLeftY: 1, axisRightX: 3, axisRightY: 4,
            axisLeftTrigger: 2, axisRightTrigger: 5,
            buttonA: 0, buttonB: 1, buttonX: 2, buttonY: 3,
            buttonLeftBumper: 4, buttonRightBumper: 5,
            buttonLeftTrigger: -1, buttonRightTrigger: -1,
            buttonBack: 6, buttonStart: 7, buttonLeftThumb: 9, buttonRightThumb: 10);

        /// <summary>The flat pad: four shoulder buttons and no analogue triggers.</summary>
        private static readonly GamepadLayout Buttons = new GamepadLayout(
            axisLeftX: 0, axisLeftY: 1, axisRightX: 2, axisRightY: 3,
            axisLeftTrigger: -1, axisRightTrigger: -1,
            buttonA: 0, buttonB: 1, buttonX: 2, buttonY: 3,
            buttonLeftBumper: 4, buttonRightBumper: 5,
            buttonLeftTrigger: 6, buttonRightTrigger: 7,
            buttonBack: 8, buttonStart: 9, buttonLeftThumb: 10, buttonRightThumb: 11);

        /// <summary>
        /// Which of the two shapes this joystick is, by counting its axes.
        ///
        /// Six is the number that separates them, and it is not a coincidence:
        /// an analogue trigger costs an axis each, so a pad with two of them
        /// has at least six and a pad without has four.
        /// </summary>
        public static GamepadLayout For(int slot)
        {
            float[]? axes = null;
            try
            {
                axes = GLFW.GetJoystickAxes(slot).ToArray();
            }
            catch (Exception ex) when (ex is DllNotFoundException
                || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
            }
            return axes != null && axes.Length >= 6 ? Triggers : Buttons;
        }
    }
}
