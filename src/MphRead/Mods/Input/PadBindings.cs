using System;
using System.Collections.Generic;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// What a pad button does, as a thing a player can change.
    ///
    /// Not the <c>PlayerControls</c> properties themselves, and the difference
    /// is deliberate: several of those are one button on a pad and always were
    /// on the DS. <see cref="Shoot"/> drives both <c>Shoot</c> and
    /// <c>AltAttack</c>, <see cref="Jump"/> drives both <c>Jump</c> and
    /// <c>Boost</c>, and offering four rows for two buttons would let a player
    /// build a pad on which the ball cannot boost and nothing on screen says
    /// why.
    ///
    /// The weapon wheel is missing for the reason <see cref="GamepadInput"/>
    /// gives: it reads an absolute pointer position, which a stick does not
    /// have. <see cref="NextWeapon"/> and <see cref="PrevWeapon"/> reach every
    /// weapon without it.
    /// </summary>
    public enum PadAction
    {
        /// <summary>The gun on foot and the alt form's attack in the ball.</summary>
        Shoot,
        Zoom,
        /// <summary>Jumping on foot, boosting in the ball: one button, as on the DS.</summary>
        Jump,
        Morph,
        Scan,
        ScanVisor,
        /// <summary>The DS's own pause button: map and status, scoreboard in a match.</summary>
        Scoreboard,
        NextWeapon,
        PrevWeapon,
        Missile,
        PowerBeam,
        /// <summary>
        /// The app's own menu. Not a <c>Keybind</c> like the rest -- it opens a
        /// window rather than doing something in the world -- so it is taken
        /// by whoever owns the window, through
        /// <see cref="GamepadInput.TakeMenuPress"/>.
        /// </summary>
        Menu,
        /// <summary>
        /// Open the chat line. Like <see cref="Menu"/> and unlike the rest,
        /// this opens something that then takes the keyboard, so it is taken
        /// through <see cref="GamepadInput.TakeChatPress"/> rather than held
        /// as a bind.
        /// </summary>
        Chat
    }

    /// <summary>
    /// Which pad button each action is on, and the player's changes to that.
    ///
    /// Its own table rather than an extra <c>ButtonType</c> on
    /// <see cref="Entities.Keybind"/>, because that type is upstream's and
    /// everything this project adds stays under <c>Mods/</c> so a pull from
    /// NoneGiven/MphRead is a fast-forward. It costs nothing to keep them
    /// apart: <see cref="GamepadInput.Apply"/> already *adds* the pad's
    /// contribution to the binds the keyboard just filled in, so the two
    /// mappings never have to agree about anything.
    ///
    /// A binding may be more than one button -- the defaults put the weapon
    /// cycling on a bumper *and* the d-pad -- which is why this is a flag set
    /// and not a single value. The settings screen replaces the whole set with
    /// the one button that was pressed; a player who wants both back has the
    /// reset.
    /// </summary>
    public static class PadBindings
    {
        private static readonly GamepadButtons[] _defaults =
        {
            /* Shoot      */ GamepadButtons.RightTrigger,
            /* Zoom       */ GamepadButtons.LeftTrigger,
            /* Jump       */ GamepadButtons.A,
            /* Morph      */ GamepadButtons.B,
            /* Scan       */ GamepadButtons.X,
            /* ScanVisor  */ GamepadButtons.Y,
            /* Scoreboard */ GamepadButtons.Back,
            /* NextWeapon */ GamepadButtons.RightBumper | GamepadButtons.DpadRight,
            /* PrevWeapon */ GamepadButtons.LeftBumper | GamepadButtons.DpadLeft,
            /* Missile    */ GamepadButtons.DpadUp,
            /* PowerBeam  */ GamepadButtons.DpadDown,
            /* Menu       */ GamepadButtons.Start,
            // The left stick click, which is the only button on a standard pad
            // the defaults had not already spent. Awkward to hit by accident
            // while aiming, which is what you want from the one that stops you
            // playing and starts you typing.
            /* Chat       */ GamepadButtons.LeftThumb
        };

        private static readonly GamepadButtons[] _current = (GamepadButtons[])_defaults.Clone();

        /// <summary>Every action, in the order a settings screen should list them.</summary>
        public static IReadOnlyList<PadAction> Actions { get; } = new[]
        {
            PadAction.Shoot, PadAction.Jump, PadAction.Morph, PadAction.Zoom,
            PadAction.ScanVisor, PadAction.Scan, PadAction.NextWeapon,
            PadAction.PrevWeapon, PadAction.Missile, PadAction.PowerBeam,
            PadAction.Scoreboard, PadAction.Menu, PadAction.Chat
        };

        public static GamepadButtons Get(PadAction action)
        {
            return _current[(int)action];
        }

        public static void Set(PadAction action, GamepadButtons buttons)
        {
            _current[(int)action] = buttons;
        }

        public static GamepadButtons Default(PadAction action)
        {
            return _defaults[(int)action];
        }

        /// <summary>Put every button back where it shipped.</summary>
        public static void Reset()
        {
            Array.Copy(_defaults, _current, _defaults.Length);
        }

        /// <summary>The name of the setting a row is editing.</summary>
        public static string Name(PadAction action)
        {
            return action switch
            {
                PadAction.Shoot => "Fire / alt attack",
                PadAction.Zoom => "Zoom",
                PadAction.Jump => "Jump / boost",
                PadAction.Morph => "Morph ball",
                PadAction.Scan => "Scan",
                PadAction.ScanVisor => "Scan visor",
                PadAction.Scoreboard => "Map / scoreboard",
                PadAction.NextWeapon => "Next weapon",
                PadAction.PrevWeapon => "Previous weapon",
                PadAction.Missile => "Missile",
                PadAction.PowerBeam => "Power beam",
                PadAction.Menu => "Menu",
                _ => action.ToString()
            };
        }

        /// <summary>"A", "RT", "RB or D-pad right", "unbound".</summary>
        public static string Describe(GamepadButtons buttons)
        {
            if (buttons == GamepadButtons.None)
            {
                return "unbound";
            }
            var names = new List<string>();
            foreach (GamepadButtons button in Enum.GetValues<GamepadButtons>())
            {
                if (button != GamepadButtons.None && (buttons & button) == button)
                {
                    names.Add(ButtonName(button));
                }
            }
            return String.Join(" or ", names);
        }

        /// <summary>
        /// What the button is called on the pad in the player's hands.
        ///
        /// Xbox names, because that is the layout both readers remap onto --
        /// see <see cref="GamepadState"/> -- and because naming the face
        /// buttons after a DualShock's shapes would be wrong for everyone
        /// holding anything else.
        /// </summary>
        public static string ButtonName(GamepadButtons button)
        {
            return button switch
            {
                GamepadButtons.A => "A",
                GamepadButtons.B => "B",
                GamepadButtons.X => "X",
                GamepadButtons.Y => "Y",
                GamepadButtons.LeftBumper => "LB",
                GamepadButtons.RightBumper => "RB",
                GamepadButtons.LeftTrigger => "LT",
                GamepadButtons.RightTrigger => "RT",
                GamepadButtons.Back => "Back",
                GamepadButtons.Start => "Start",
                GamepadButtons.LeftThumb => "Left stick",
                GamepadButtons.RightThumb => "Right stick",
                GamepadButtons.DpadUp => "D-pad up",
                GamepadButtons.DpadDown => "D-pad down",
                GamepadButtons.DpadLeft => "D-pad left",
                GamepadButtons.DpadRight => "D-pad right",
                _ => button.ToString()
            };
        }

        /// <summary>The key this action is written under in controls.txt.</summary>
        public static string SettingKey(PadAction action)
        {
            return "pad_" + action;
        }

        /// <summary>
        /// Read one saved line. The value is the flag set's own round trip --
        /// "RightBumper, DpadRight" -- so a binding with two buttons in it
        /// survives being written out and read back.
        /// </summary>
        public static bool TryLoad(string key, string value)
        {
            if (!key.StartsWith("pad_", StringComparison.Ordinal)
                || !Enum.TryParse(key[4..], out PadAction action)
                || !Enum.TryParse(value, out GamepadButtons buttons))
            {
                return false;
            }
            Set(action, buttons);
            return true;
        }
    }
}
