using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MphRead.Entities;
using MphRead.Mods;
using GlfwKeys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using GlfwMouse = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// One rebindable control: what it does on the left, what it is bound to on
    /// the right, click and press to change it.
    ///
    /// The awkward part is the same one the WinForms row had: this window is a
    /// toolkit's and the game is GLFW's, and their key enumerations agree only
    /// about printable ASCII -- Escape is 27 in one and 256 in the other. The
    /// map below covers everything a person is likely to bind; anything
    /// unmapped is refused rather than bound to whatever key happens to share
    /// its number.
    /// </summary>
    internal sealed class KeyRow : Control
    {
        private readonly PropertyInfo? _property;
        private readonly double _labelWidth;

        // The second mode: a plain Keys setting rather than one of
        // PlayerControls' Keybinds.
        //
        // The chat key is the only one, and it is not a Keybind because it
        // cannot be. Bindings are found by reflecting over PlayerControls,
        // which is upstream's type, and everything this project adds lives
        // under Mods/ so a pull stays a fast-forward -- so chat, which this
        // project added, has nowhere in that list to be. It is a keyboard-only
        // row: there is no sense in opening the chat line with the wheel.
        private readonly string? _label;
        private readonly Func<GlfwKeys>? _get;
        private readonly Action<GlfwKeys>? _set;
        private bool _listening;
        private bool _hot;

        public event EventHandler? Rebound;

        public KeyRow(PropertyInfo property, double labelWidth = 160)
        {
            _property = property;
            _labelWidth = labelWidth;
            Height = 32;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        public KeyRow(string label, Func<GlfwKeys> get, Action<GlfwKeys> set, double labelWidth = 160)
        {
            _label = label;
            _get = get;
            _set = set;
            _labelWidth = labelWidth;
            Height = 32;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        private Rect Box => new(_labelWidth, 2,
            Math.Max(60, Bounds.Width - _labelWidth - 4), Bounds.Height - 4);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            Focus();
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
            if (!_listening)
            {
                if (Box.Contains(e.GetPosition(this)))
                {
                    _listening = true;
                    InvalidateVisual();
                }
                e.Handled = true;
                base.OnPointerPressed(e);
                return;
            }
            // Already listening: this press is the new binding.
            GlfwMouse? button = properties.PointerUpdateKind switch
            {
                PointerUpdateKind.LeftButtonPressed => GlfwMouse.Left,
                PointerUpdateKind.RightButtonPressed => GlfwMouse.Right,
                PointerUpdateKind.MiddleButtonPressed => GlfwMouse.Middle,
                PointerUpdateKind.XButton1Pressed => GlfwMouse.Button4,
                PointerUpdateKind.XButton2Pressed => GlfwMouse.Button5,
                _ => null
            };
            if (button != null && _property != null)
            {
                InputSettings.Rebind(_property, ButtonType.Mouse, GlfwKeys.Unknown, button.Value);
                Done();
            }
            e.Handled = true;
            base.OnPointerPressed(e);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (_listening && e.Delta.Y != 0 && _property != null)
            {
                InputSettings.Rebind(_property,
                    e.Delta.Y > 0 ? ButtonType.ScrollUp : ButtonType.ScrollDown,
                    GlfwKeys.Unknown, GlfwMouse.Left);
                Done();
                e.Handled = true;
            }
            base.OnPointerWheelChanged(e);
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            _hot = true;
            InvalidateVisual();
            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            _hot = false;
            InvalidateVisual();
            base.OnPointerExited(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_listening)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    _listening = true;
                    InvalidateVisual();
                    e.Handled = true;
                }
                base.OnKeyDown(e);
                return;
            }
            // While listening every key belongs to this row, including the ones
            // the window would otherwise spend on moving the focus or closing.
            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                Done();
                return;
            }
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                Assign(GlfwKeys.Unknown);
                Done();
                return;
            }
            GlfwKeys? key = Translate(e.Key);
            if (key != null)
            {
                Assign(key.Value);
                Done();
            }
        }

        private void Assign(GlfwKeys key)
        {
            if (_set != null)
            {
                _set(key);
                return;
            }
            InputSettings.Rebind(_property!, ButtonType.Key, key, GlfwMouse.Left);
        }

        private void Done()
        {
            _listening = false;
            InvalidateVisual();
            Rebound?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e)
        {
            _listening = false;
            InvalidateVisual();
            base.OnLostFocus(e);
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            InvalidateVisual();
            base.OnGotFocus(e);
        }

        /// <summary>The toolkit's key to the one the game's input layer speaks.</summary>
        private static GlfwKeys? Translate(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
            {
                return GlfwKeys.A + (key - Key.A);
            }
            if (key >= Key.D0 && key <= Key.D9)
            {
                return GlfwKeys.D0 + (key - Key.D0);
            }
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return GlfwKeys.KeyPad0 + (key - Key.NumPad0);
            }
            if (key >= Key.F1 && key <= Key.F12)
            {
                return GlfwKeys.F1 + (key - Key.F1);
            }
            return key switch
            {
                Key.Space => GlfwKeys.Space,
                Key.Tab => GlfwKeys.Tab,
                Key.Enter => GlfwKeys.Enter,
                Key.LeftShift => GlfwKeys.LeftShift,
                Key.RightShift => GlfwKeys.RightShift,
                Key.LeftCtrl => GlfwKeys.LeftControl,
                Key.RightCtrl => GlfwKeys.RightControl,
                Key.LeftAlt => GlfwKeys.LeftAlt,
                Key.RightAlt => GlfwKeys.RightAlt,
                Key.Left => GlfwKeys.Left,
                Key.Right => GlfwKeys.Right,
                Key.Up => GlfwKeys.Up,
                Key.Down => GlfwKeys.Down,
                Key.Insert => GlfwKeys.Insert,
                Key.Home => GlfwKeys.Home,
                Key.End => GlfwKeys.End,
                Key.PageUp => GlfwKeys.PageUp,
                Key.PageDown => GlfwKeys.PageDown,
                Key.CapsLock => GlfwKeys.CapsLock,
                Key.OemMinus => GlfwKeys.Minus,
                Key.OemPlus => GlfwKeys.Equal,
                Key.OemOpenBrackets => GlfwKeys.LeftBracket,
                Key.OemCloseBrackets => GlfwKeys.RightBracket,
                Key.OemSemicolon => GlfwKeys.Semicolon,
                Key.OemQuotes => GlfwKeys.Apostrophe,
                Key.OemComma => GlfwKeys.Comma,
                Key.OemPeriod => GlfwKeys.Period,
                Key.OemQuestion => GlfwKeys.Slash,
                Key.OemBackslash or Key.OemPipe => GlfwKeys.Backslash,
                Key.OemTilde => GlfwKeys.GraveAccent,
                Key.Add => GlfwKeys.KeyPadAdd,
                Key.Subtract => GlfwKeys.KeyPadSubtract,
                Key.Multiply => GlfwKeys.KeyPadMultiply,
                Key.Divide => GlfwKeys.KeyPadDivide,
                _ => null
            };
        }

        public override void Render(DrawingContext context)
        {
            // See MenuEntry.Render: hit testing follows the drawing.
            context.FillRectangle(Brushes.Transparent,
                new Rect(0, 0, Bounds.Width, Bounds.Height));
            FormattedText label = TrackedText.Make(
                _label ?? InputSettings.ActionName(_property!), 12,
                bold: true, GuiTheme.TextBrush);
            context.DrawText(label, new Point(4, (Bounds.Height - label.Height) / 2));

            Rect box = Box;
            context.DrawRectangle(GuiTheme.PanelLightBrush,
                new Pen(new SolidColorBrush(_listening ? GuiTheme.Warm
                    : IsFocused || _hot ? GuiTheme.Accent : GuiTheme.Edge), 1),
                new RoundedRect(box, 4));

            string text = _listening
                ? (_get != null ? "press a key" : "press a key, a mouse button or the wheel")
                : _get != null
                    ? (_get() == GlfwKeys.Unknown ? "none" : InputSettings.KeyName(_get()))
                    : InputSettings.Describe(InputSettings.Bind(_property!));
            FormattedText value = TrackedText.Make(text, 12, bold: true,
                new SolidColorBrush(_listening ? GuiTheme.Warm : GuiTheme.Text));
            // Never wider than the box: a binding nobody has heard of should
            // not push its own frame off the row.
            value.MaxTextWidth = Math.Max(20, box.Width - 12);
            value.MaxTextHeight = box.Height;
            value.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(value, new Point(box.X + (box.Width - value.Width) / 2,
                box.Y + (box.Height - value.Height) / 2));
        }
    }
}
