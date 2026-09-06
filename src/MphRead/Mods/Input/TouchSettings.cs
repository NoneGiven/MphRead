using System;
using System.Collections.Generic;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// One button on the phone's screen. The same list the Android head
    /// draws, named here because the settings screen is shared code and the
    /// head's own type is not visible to it.
    /// </summary>
    public enum TouchControl
    {
        Shoot,
        Jump,
        Morph,
        ScanVisor,
        Scan,
        Missile,
        WeaponMenu,
        Zoom,
        Pause,
        Scoreboard,
        Chat
    }

    /// <summary>
    /// Which of the on-screen buttons a player wants on the glass.
    ///
    /// It exists because the buttons are in the way of the thing they sit on
    /// top of: aiming is a drag anywhere on the right of the screen, and a
    /// drag that starts inside a circle presses the circle instead. A thumb
    /// that keeps clipping ZOOM on the way to a shot is not a layout that can
    /// be tuned out of the problem -- the screen is small and the buttons have
    /// to be reachable -- so the answer is to let the player take away the
    /// ones they never use. Every one of them is on by default, which is the
    /// layout that shipped.
    ///
    /// Nothing here can make the game unplayable by accident: movement is the
    /// stick, which appears wherever a thumb lands on the left; aiming is a
    /// drag; jump is a double tap on the aiming side; and boost in the ball is
    /// a flick. None of the four is a button, so a screen with every button
    /// turned off is still a screen you can play on.
    ///
    /// Kept with the rest of the controls in controls.txt rather than in
    /// settings.json, because that is the file the settings screen's Controls
    /// page already writes and this is a controls question.
    /// </summary>
    public static class TouchSettings
    {
        /// <summary>
        /// The master switch: off, no button is drawn and none takes a touch.
        /// The individual choices are remembered underneath it, so turning it
        /// back on restores the layout the player had rather than all of it.
        /// </summary>
        public static bool ButtonsVisible { get; set; } = true;

        private static readonly HashSet<TouchControl> _hidden = new HashSet<TouchControl>();

        /// <summary>The order the settings screen lists them in, with labels.</summary>
        public static readonly (TouchControl Control, string Label)[] Order =
        {
            (TouchControl.Shoot, "FIRE"),
            (TouchControl.Jump, "JUMP"),
            (TouchControl.Morph, "MORPH"),
            (TouchControl.ScanVisor, "VISOR"),
            (TouchControl.Scan, "SCAN"),
            (TouchControl.Missile, "MISSILE"),
            (TouchControl.WeaponMenu, "WEAPON wheel"),
            (TouchControl.Zoom, "ZOOM"),
            (TouchControl.Pause, "MENU"),
            (TouchControl.Scoreboard, "SCORE"),
            (TouchControl.Chat, "CHAT")
        };

        public static bool IsEnabled(TouchControl control)
        {
            return !_hidden.Contains(control);
        }

        public static void SetEnabled(TouchControl control, bool enabled)
        {
            if (enabled)
            {
                _hidden.Remove(control);
            }
            else
            {
                _hidden.Add(control);
            }
        }

        /// <summary>
        /// Whether this button belongs on the screen at all right now. The
        /// head still decides whether it makes sense in the situation -- SCAN
        /// only in the visor, CHAT only in a networked match -- and this is
        /// the player's answer on top of that.
        /// </summary>
        public static bool Shown(TouchControl control)
        {
            return ButtonsVisible && IsEnabled(control);
        }

        public static void Reset()
        {
            ButtonsVisible = true;
            _hidden.Clear();
        }

        public static string SettingKey(TouchControl control)
        {
            return $"touch_{control.ToString().ToLowerInvariant()}";
        }

        public const string ButtonsSettingKey = "touch_buttons";

        /// <summary>
        /// Read one <c>key=value</c> line from controls.txt. Answers whether
        /// it was one of ours, so the caller can go on to the next test.
        /// </summary>
        public static bool ReadSetting(string key, string value)
        {
            if (key == ButtonsSettingKey)
            {
                if (Boolean.TryParse(value, out bool visible))
                {
                    ButtonsVisible = visible;
                }
                return true;
            }
            foreach ((TouchControl control, _) in Order)
            {
                if (key == SettingKey(control))
                {
                    if (Boolean.TryParse(value, out bool enabled))
                    {
                        SetEnabled(control, enabled);
                    }
                    return true;
                }
            }
            return false;
        }

        public static void WriteSettings(List<string> lines)
        {
            lines.Add($"{ButtonsSettingKey}={ButtonsVisible.ToString().ToLowerInvariant()}");
            foreach ((TouchControl control, _) in Order)
            {
                lines.Add($"{SettingKey(control)}={IsEnabled(control).ToString().ToLowerInvariant()}");
            }
        }
    }
}
