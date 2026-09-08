using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MphRead.Entities;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods
{
    /// <summary>
    /// Keys and mouse feel, kept where a settings screen can edit them and a
    /// player can keep them.
    ///
    /// Upstream builds a fresh <see cref="PlayerControls"/> per player from
    /// <see cref="PlayerControls.GetDefault"/> and never reads a file, so
    /// rebinding anything lasted exactly as long as the process. This holds
    /// one canonical set, applies it to every set upstream creates, and writes
    /// it to controls.txt beside the executable -- deliberately its own file
    /// rather than a corner of MenuSettings, for the same reason launcher.txt
    /// is.
    ///
    /// Mouse sensitivity was a literal in the aim path with an "itodo" beside
    /// it. One multiplier lives here instead, where 1.0 is exactly the feel
    /// that literal gave.
    /// </summary>
    public static class InputSettings
    {
        /// <summary>
        /// controls.txt, beside the executable -- except where the program
        /// does not own that folder.
        ///
        /// It follows <see cref="Launcher.LauncherPrefs.Directory"/> rather
        /// than <c>AppContext.BaseDirectory</c> because an Android package's
        /// own directory is read-only: every write went to a path the app is
        /// not allowed to create, the exception was swallowed (as it has to
        /// be, see <see cref="Save"/>), and every rebind, every sensitivity
        /// and every pad binding was lost the moment the game was closed.
        /// The head points that one property at the app's data directory
        /// before anything reads, so this lands where the rest of a player's
        /// settings already do.
        /// </summary>
        private static string Path
            => System.IO.Path.Combine(Launcher.LauncherPrefs.Directory, "controls.txt");

        /// <summary>Multiplier on mouse movement. 1.0 is the original feel.</summary>
        public static float MouseSensitivity { get; set; } = 1;

        public static bool InvertMouseY { get; set; }
        public static bool InvertMouseX { get; set; }

        /// <summary>
        /// Whether the wheel cycles every weapon or only the affinity slots.
        ///
        /// On by default, which upstream's constant was not, and the
        /// difference is not a nicety: the cycling code runs only when this is
        /// set *or* the equipped weapon is neither the Power Beam nor the
        /// Missile, so with it off the wheel was dead in the hand every player
        /// spawns with -- "the scroll wheel does not change weapons", exactly.
        /// </summary>
        public static bool ScrollAllWeapons { get; set; } = true;

        /// <summary>
        /// The key that opens the chat prompt. T, which is where every
        /// shooter since Quake has put it.
        ///
        /// Not a <see cref="Keybind"/> on <see cref="PlayerControls"/> like
        /// everything else here, and deliberately so: that class is upstream's
        /// and its every member is read by <c>ProcessAllInput</c> as something
        /// the *player* does in the world. Chat is the opposite -- it takes
        /// the keyboard away from the player -- so it is handled by the window
        /// before the game sees the key at all, and a binding the game never
        /// reads has no business in the game's binding set.
        /// </summary>
        public static Keys ChatKey { get; set; } = Keys.T;

        /// <summary>
        /// Saves the last few seconds of play (see
        /// <see cref="Network.DemoClip"/>). Unknown means unbound, which also
        /// switches the rolling buffer off: nothing can ask for a clip, so
        /// there is nothing worth keeping.
        ///
        /// Rebinding it throws away whatever was held. Binding the button is
        /// the moment somebody starts meaning to use it, and handing them the
        /// seconds before that is handing them a clip of a decision they had
        /// not made yet.
        /// </summary>
        public static Keys ClipKey
        {
            get => _clipKey;
            set
            {
                if (_clipKey != value)
                {
                    Network.DemoClip.Purge();
                }
                _clipKey = value;
            }
        }

        private static Keys _clipKey = Keys.F10;

        /// <summary>
        /// How far a stick must move before it counts, 0 to 0.9.
        ///
        /// Applied radially rather than per axis -- see
        /// <see cref="Input.GamepadInput"/> for why that is not the same
        /// thing. 0.2 clears the resting drift of a worn stick without
        /// swallowing a deliberate nudge.
        /// </summary>
        public static float GamepadDeadZone
        {
            get => _gamepadDeadZone;
            set => _gamepadDeadZone = Math.Clamp(value, 0, 0.9f);
        }

        private static float _gamepadDeadZone = 0.2f;

        /// <summary>Multiplier on the right stick's turn rate. 1.0 is 210 degrees a second.</summary>
        public static float GamepadLookSensitivity
        {
            get => _gamepadLook;
            set => _gamepadLook = Math.Clamp(value, 0.1f, 5f);
        }

        private static float _gamepadLook = 1f;

        /// <summary>
        /// Invert the right stick's vertical aim. Its own setting rather than
        /// sharing the mouse's, because a great many people invert one and not
        /// the other, and there is no third thing they would rather set.
        /// </summary>
        public static bool GamepadInvertY { get; set; }

        private static bool _creating;
        private static PlayerControls? _current;

        /// <summary>
        /// The bindings every player is created with. The settings screen
        /// edits this set; <see cref="Apply"/> copies it onto each set upstream
        /// creates, and <see cref="ApplyToPlayers"/> onto the ones that already
        /// exist.
        /// </summary>
        public static PlayerControls Current
        {
            get
            {
                if (_current == null)
                {
                    // GetDefault calls Apply, which asks for Current: build the
                    // canonical set without letting that come back around.
                    _creating = true;
                    _current = PlayerControls.GetDefault();
                    _creating = false;
                }
                return _current;
            }
        }

        /// <summary>Every rebindable control, in the order a screen should list them.</summary>
        public static IReadOnlyList<PropertyInfo> Bindings => _bindings ??= FindBindings();

        private static PropertyInfo[]? _bindings;

        /// <summary>
        /// The ones worth putting first. Everything else -- the nine weapon
        /// slots, the roll and aim keys -- follows in declaration order, so a
        /// control added upstream shows up without being listed here.
        /// </summary>
        private static readonly string[] _order =
        {
            nameof(PlayerControls.MoveUp), nameof(PlayerControls.MoveDown),
            nameof(PlayerControls.MoveLeft), nameof(PlayerControls.MoveRight),
            nameof(PlayerControls.Jump), nameof(PlayerControls.Boost),
            nameof(PlayerControls.Shoot), nameof(PlayerControls.Zoom),
            nameof(PlayerControls.Morph), nameof(PlayerControls.AltAttack),
            nameof(PlayerControls.NextWeapon), nameof(PlayerControls.PrevWeapon),
            nameof(PlayerControls.WeaponMenu), nameof(PlayerControls.ScanVisor),
            nameof(PlayerControls.Pause), nameof(PlayerControls.HudOverlay)
        };

        private static PropertyInfo[] FindBindings()
        {
            PropertyInfo[] all = typeof(PlayerControls)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(Keybind))
                .ToArray();
            return all
                .OrderBy(p =>
                {
                    int index = Array.IndexOf(_order, p.Name);
                    return index < 0 ? _order.Length : index;
                })
                .ToArray();
        }

        public static Keybind Bind(PropertyInfo property)
        {
            return (Keybind)property.GetValue(Current)!;
        }

        /// <summary>"Left Shift", "Mouse left", "Scroll up", "1".</summary>
        public static string Describe(Keybind bind)
        {
            switch (bind.Type)
            {
                case ButtonType.Mouse:
                    // OpenTK's enum names these Button1..Button8 and aliases
                    // the first three; ToString picks the number, which is not
                    // what anybody calls them.
                    return bind.MouseButton switch
                    {
                        MouseButton.Left => "Mouse left",
                        MouseButton.Right => "Mouse right",
                        MouseButton.Middle => "Mouse middle",
                        _ => $"Mouse {(int)bind.MouseButton + 1}"
                    };
                case ButtonType.ScrollUp:
                    return "Scroll up";
                case ButtonType.ScrollDown:
                    return "Scroll down";
                default:
                    return bind.Key == Keys.Unknown ? "unbound" : KeyName(bind.Key);
            }
        }

        /// <summary>"D1" -> "1", "LeftShift" -> "Left shift", "KeyPad4" -> "Key pad 4".</summary>
        public static string KeyName(Keys key)
        {
            string name = key.ToString();
            if (name.Length == 2 && name[0] == 'D' && Char.IsDigit(name[1]))
            {
                return name[1].ToString();
            }
            var builder = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && Char.IsUpper(name[i]) && !Char.IsUpper(name[i - 1]))
                {
                    builder.Append(' ');
                    builder.Append(Char.ToLowerInvariant(name[i]));
                }
                else
                {
                    builder.Append(name[i]);
                }
            }
            return builder.ToString();
        }

        /// <summary>"Humanise" a control's name for a screen: "AltAttack" -> "Alt attack".</summary>
        public static string ActionName(PropertyInfo property)
        {
            string name = property.Name == nameof(PlayerControls.Pause)
                ? "Scoreboard"
                : property.Name == nameof(PlayerControls.RolltLeft) ? "Roll left" : property.Name;
            var builder = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && Char.IsUpper(name[i]) && !Char.IsUpper(name[i - 1]))
                {
                    builder.Append(' ');
                    builder.Append(Char.ToLowerInvariant(name[i]));
                }
                else
                {
                    builder.Append(i == 0 ? Char.ToUpperInvariant(name[i]) : name[i]);
                }
            }
            return builder.ToString();
        }

        /// <summary>Point a control at a key, a mouse button or the wheel.</summary>
        public static void Rebind(PropertyInfo property, ButtonType type, Keys key,
            MouseButton button)
        {
            Keybind bind = Bind(property);
            bind.Type = type;
            bind.Key = type == ButtonType.Key ? key : Keys.Unknown;
            bind.MouseButton = button;
        }

        /// <summary>
        /// Copy the canonical bindings onto a set upstream just created. Called
        /// from GetDefault, so it covers every player in every match.
        /// </summary>
        public static void Apply(PlayerControls controls)
        {
            if (_creating || _current == null)
            {
                return;
            }
            foreach (PropertyInfo property in Bindings)
            {
                var source = (Keybind)property.GetValue(_current)!;
                var target = (Keybind)property.GetValue(controls)!;
                target.Type = source.Type;
                target.Key = source.Key;
                target.MouseButton = source.MouseButton;
            }
            controls.ScrollAllWeapons = ScrollAllWeapons;
        }

        /// <summary>
        /// Push the current bindings onto players that already exist.
        ///
        /// <see cref="Apply"/> copies values into each player's own Keybind
        /// objects, so editing this set afterwards reaches nobody -- and a
        /// rebind made from the pause menu is by definition made in the middle
        /// of a match, where waiting for the next one is not an answer.
        /// </summary>
        public static void ApplyToPlayers()
        {
            try
            {
                for (int i = 0; i < PlayerEntity.Players.Count; i++)
                {
                    Apply(PlayerEntity.Players[i].Controls);
                }
            }
            catch (Exception)
            {
                // The pause menu's settings window runs on its own thread, so
                // this can land in the middle of a room load rebuilding the
                // player list. Losing the push is nothing -- the bindings are
                // saved, and PlayerControls.GetDefault applies them to every
                // set the load is creating anyway.
            }
        }

        public static void Load()
        {
            if (!File.Exists(Path))
            {
                return;
            }
            try
            {
                foreach (string raw in File.ReadAllLines(Path))
                {
                    string line = raw.Trim();
                    int split = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || split <= 0)
                    {
                        continue;
                    }
                    string key = line[..split].Trim();
                    string value = line[(split + 1)..].Trim();
                    if (key == "sensitivity")
                    {
                        if (Single.TryParse(value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float parsed))
                        {
                            MouseSensitivity = Math.Clamp(parsed, 0.05f, 10f);
                        }
                        continue;
                    }
                    if (key == "invert_y" && Boolean.TryParse(value, out bool invertY))
                    {
                        InvertMouseY = invertY;
                        continue;
                    }
                    if (key == "invert_x" && Boolean.TryParse(value, out bool invertX))
                    {
                        InvertMouseX = invertX;
                        continue;
                    }
                    if (key == "pointer_jump_guard" && Boolean.TryParse(value, out bool guardJumps))
                    {
                        Input.PointerInput.GuardJumps = guardJumps;
                    }
                    if (key == "stylus_zone" && Boolean.TryParse(value, out bool stylusZone))
                    {
                        // Desktop only, and enforced here rather than only in
                        // the settings screen: nothing on a phone updates the
                        // zone, so a file carried over from a PC must not
                        // switch on an overlay that cannot be aimed at,
                        // pressed, or -- since the rows are not built there --
                        // turned back off.
                        Input.StylusZone.Enabled = stylusZone && !OperatingSystem.IsAndroid();
                    }
                    // Three numbers for one rectangle: the height follows the
                    // DS's shape and is not stored, so a hand-edited file
                    // cannot produce a zone the layout does not fit.
                    if (key == "stylus_zone_opacity" && Single.TryParse(value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float zoneOpacity))
                    {
                        Input.StylusZone.Opacity = Math.Clamp(zoneOpacity, 0.02f, 1f);
                    }
                    if (key == "stylus_zone_rect")
                    {
                        string[] parts = value.Split(',');
                        if (parts.Length == 3
                            && Single.TryParse(parts[0], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out float zoneLeft)
                            && Single.TryParse(parts[1], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out float zoneTop)
                            && Single.TryParse(parts[2], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out float zoneWidth))
                        {
                            Input.StylusZone.SetRect(zoneLeft, zoneTop, zoneWidth);
                        }
                    }
                    if (key == "scroll_all_weapons" && Boolean.TryParse(value, out bool scrollAll))
                    {
                        ScrollAllWeapons = scrollAll;
                        continue;
                    }
                    if (key == "gamepad")
                    {
                        // Written by every build up to the one that removed
                        // the toggle. Skipped rather than refused so an old
                        // controls.txt still loads the rest of itself.
                        continue;
                    }
                    if (Input.PadBindings.TryLoad(key, value))
                    {
                        continue;
                    }
                    if (key == "clip_key")
                    {
                        // Assigned to the field, not the property: the setter
                        // purges the buffer on a change, which is right for a
                        // player rebinding it and wrong for loading the file
                        // they saved it in.
                        _clipKey = value.Equals("none", StringComparison.OrdinalIgnoreCase)
                            ? Keys.Unknown
                            : Enum.TryParse(value, out Keys parsedClip) ? parsedClip : _clipKey;
                        continue;
                    }
                    if (key == "clip_seconds"
                        && Int32.TryParse(value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int clipSeconds))
                    {
                        Network.DemoClip.Seconds = clipSeconds;
                        continue;
                    }
                    if (Input.TouchSettings.ReadSetting(key, value))
                    {
                        continue;
                    }
                    if (key == "gamepad_deadzone"
                        && Single.TryParse(value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float deadZone))
                    {
                        GamepadDeadZone = deadZone;
                        continue;
                    }
                    if (key == "gamepad_look"
                        && Single.TryParse(value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float look))
                    {
                        GamepadLookSensitivity = look;
                        continue;
                    }
                    if (key == "gamepad_invert_y" && Boolean.TryParse(value, out bool padInvert))
                    {
                        GamepadInvertY = padInvert;
                        continue;
                    }
                    if (key == "chat_key")
                    {
                        // "none" rather than a missing line, so a player who
                        // wants the key back for something else can say so and
                        // still have the file rewritten with everything in it.
                        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                        {
                            ChatKey = Keys.Unknown;
                        }
                        else if (Enum.TryParse(value, out Keys chatKey))
                        {
                            ChatKey = chatKey;
                        }
                        continue;
                    }
                    PropertyInfo? property = Bindings.FirstOrDefault(p => p.Name == key);
                    if (property != null)
                    {
                        ParseBind(property, value);
                    }
                }
            }
            catch (Exception)
            {
                // Bindings are a convenience; an unreadable file must not stop
                // the game from starting. Every exception, not only IOException:
                // a folder the user cannot read raises
                // UnauthorizedAccessException, which is not one -- and an
                // install under Program Files is exactly where that happens.
            }
        }

        private static void ParseBind(PropertyInfo property, string value)
        {
            string[] parts = value.Split(':', 2);
            string type = parts[0].Trim();
            string name = parts.Length > 1 ? parts[1].Trim() : "";
            if (type == "ScrollUp")
            {
                Rebind(property, ButtonType.ScrollUp, Keys.Unknown, MouseButton.Left);
            }
            else if (type == "ScrollDown")
            {
                Rebind(property, ButtonType.ScrollDown, Keys.Unknown, MouseButton.Left);
            }
            else if (type == "Mouse" && Enum.TryParse(name, out MouseButton button))
            {
                Rebind(property, ButtonType.Mouse, Keys.Unknown, button);
            }
            else if (type == "Key" && Enum.TryParse(name, out Keys key))
            {
                Rebind(property, ButtonType.Key, key, MouseButton.Left);
            }
        }

        public static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    $"# {Branding.Name} controls. Delete a line to go back to the default.",
                    $"sensitivity={MouseSensitivity.ToString("0.###", CultureInfo.InvariantCulture)}",
                    $"invert_y={InvertMouseY.ToString().ToLowerInvariant()}",
                    $"invert_x={InvertMouseX.ToString().ToLowerInvariant()}",
                    $"scroll_all_weapons={ScrollAllWeapons.ToString().ToLowerInvariant()}",
                    $"pointer_jump_guard={Input.PointerInput.GuardJumps.ToString().ToLowerInvariant()}",
                    $"stylus_zone={Input.StylusZone.Enabled.ToString().ToLowerInvariant()}",
                    "stylus_zone_opacity="
                        + Input.StylusZone.Opacity.ToString("0.###", CultureInfo.InvariantCulture),
                    "stylus_zone_rect="
                        + Input.StylusZone.Left.ToString("0.####", CultureInfo.InvariantCulture) + ","
                        + Input.StylusZone.Top.ToString("0.####", CultureInfo.InvariantCulture) + ","
                        + Input.StylusZone.Width.ToString("0.####", CultureInfo.InvariantCulture),
                    $"chat_key={(ChatKey == Keys.Unknown ? "none" : ChatKey.ToString())}",
                    $"clip_key={(ClipKey == Keys.Unknown ? "none" : ClipKey.ToString())}",
                    $"clip_seconds={Network.DemoClip.Seconds.ToString(CultureInfo.InvariantCulture)}",
                    "gamepad_deadzone=" + GamepadDeadZone.ToString(CultureInfo.InvariantCulture),
                    "gamepad_look=" + GamepadLookSensitivity.ToString(CultureInfo.InvariantCulture),
                    $"gamepad_invert_y={GamepadInvertY.ToString().ToLowerInvariant()}"
                };
                foreach (Input.PadAction action in Input.PadBindings.Actions)
                {
                    lines.Add($"{Input.PadBindings.SettingKey(action)}="
                        + Input.PadBindings.Get(action));
                }
                Input.TouchSettings.WriteSettings(lines);
                foreach (PropertyInfo property in Bindings)
                {
                    Keybind bind = Bind(property);
                    string value = bind.Type switch
                    {
                        ButtonType.Mouse => $"Mouse:{bind.MouseButton}",
                        ButtonType.ScrollUp => "ScrollUp",
                        ButtonType.ScrollDown => "ScrollDown",
                        _ => $"Key:{bind.Key}"
                    };
                    lines.Add($"{property.Name}={value}");
                }
                File.WriteAllLines(Path, lines);
            }
            catch (Exception)
            {
                // Same reason as Load: the folder beside the executable is not
                // guaranteed to be writable, and losing a rebind is a far
                // smaller thing than taking down the window that made it --
                // which, from the pause menu, is the thread the menu runs on.
            }
        }

        /// <summary>Put everything back the way it shipped.</summary>
        public static void Reset()
        {
            _creating = true;
            _current = PlayerControls.GetDefault();
            _creating = false;
            MouseSensitivity = 1;
            InvertMouseY = false;
            InvertMouseX = false;
            ScrollAllWeapons = true;
            ChatKey = Keys.T;
            ClipKey = Keys.F10;
            Network.DemoClip.Seconds = 10;
            Input.PadBindings.Reset();
            Input.TouchSettings.Reset();
            GamepadDeadZone = 0.2f;
            GamepadLookSensitivity = 1f;
            GamepadInvertY = false;
        }
    }
}
