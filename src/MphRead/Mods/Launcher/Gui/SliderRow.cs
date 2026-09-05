using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// A labelled slider, shaped like <see cref="ChoiceRow"/> so that a column
    /// of rows lines up whatever each one is.
    ///
    /// Drawn rather than a styled Slider for the same reason every other row
    /// here is drawn: what is wanted is a track, a fill and a value on the
    /// right, and a control template that produces exactly that is more code
    /// than the drawing.
    ///
    /// The range defaults to 0-100 because every row that existed before the
    /// FPS limit was a percentage. <paramref name="min"/> and
    /// <paramref name="max"/> let one slide over something else -- the FPS
    /// limit slides over an index into its own table of stops, so that a drag
    /// lands on 144 and never on 143.
    /// </summary>
    internal sealed class SliderRow : Control
    {
        private readonly string _label;
        private readonly double _labelWidth;
        private readonly Func<int, string> _format;
        private readonly int _min;
        private readonly int _max;
        private readonly int _keyStep;
        private int _value;
        private bool _dragging;
        private bool _hot;

        public event EventHandler? ValueChanged;

        public SliderRow(string label, int value, Func<int, string>? format = null,
            double labelWidth = 120, int min = 0, int max = 100, int keyStep = 5)
        {
            _label = label;
            _labelWidth = labelWidth;
            _min = min;
            _max = Math.Max(min + 1, max);
            _keyStep = Math.Max(1, keyStep);
            _value = Math.Clamp(value, _min, _max);
            _format = format ?? (v => $"{v.ToString(CultureInfo.InvariantCulture)}%");
            Height = 34;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, _min, _max);
                if (clamped != _value)
                {
                    _value = clamped;
                    InvalidateVisual();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Room kept on the right for the value, which is drawn over the track
        /// rather than beside it.
        ///
        /// This was 64, which fits "100%" and every other percentage these
        /// rows carried before the FPS limit. That row's longest readings --
        /// "Display (VSync)" and "Unlimited" -- are wider, and at a value near
        /// the top of the range the text landed on the slider's own handle.
        /// Widened for every row rather than for that one, because the tracks
        /// ending in a column is what makes the page read as a column.
        /// </summary>
        private const double ValueGutter = 112;

        private Rect Track => new(_labelWidth, Bounds.Height / 2 - 2,
            Math.Max(40, Bounds.Width - _labelWidth - ValueGutter), 4);

        private void SetFromPointer(double x)
        {
            Rect track = Track;
            double fraction = (x - track.X) / Math.Max(1, track.Width);
            Value = _min + (int)Math.Round(Math.Clamp(fraction, 0, 1) * (_max - _min));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            Focus();
            Point p = e.GetPosition(this);
            if (p.X >= _labelWidth && IsEnabled)
            {
                _dragging = true;
                // Captured so a drag that leaves the row keeps moving the
                // value: letting go of a slider two pixels above the track is
                // not a gesture anybody means.
                e.Pointer.Capture(this);
                SetFromPointer(p.X);
            }
            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            Point p = e.GetPosition(this);
            bool hot = p.X >= _labelWidth;
            if (hot != _hot)
            {
                _hot = hot;
                InvalidateVisual();
            }
            if (_dragging)
            {
                SetFromPointer(p.X);
            }
            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            base.OnPointerReleased(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            _hot = false;
            InvalidateVisual();
            base.OnPointerExited(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!IsEnabled)
            {
                base.OnKeyDown(e);
                return;
            }
            if (e.Key == Key.Left)
            {
                Value -= _keyStep;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right)
            {
                Value += _keyStep;
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            InvalidateVisual();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e)
        {
            InvalidateVisual();
            base.OnLostFocus(e);
        }

        public override void Render(DrawingContext context)
        {
            // See MenuEntry.Render: hit testing follows the drawing.
            context.FillRectangle(Brushes.Transparent,
                new Rect(0, 0, Bounds.Width, Bounds.Height));
            var dim = new SolidColorBrush(Color.FromRgb(70, 76, 90));
            TrackedText.Draw(context, _label.ToUpperInvariant(), 11,
                IsEnabled ? GuiTheme.TextDimBrush : dim,
                4, (Bounds.Height - TrackedText.LineHeight(11)) / 2, tracking: 1);

            Rect track = Track;
            context.FillRectangle(GuiTheme.PanelLightBrush, track);
            double filled = track.Width * ((_value - _min) / (double)(_max - _min));
            IBrush accent = IsEnabled
                ? new SolidColorBrush(IsFocused || _hot
                    ? GuiTheme.Shade(GuiTheme.Accent, 0.15) : GuiTheme.Accent)
                : dim;
            context.FillRectangle(accent, new Rect(track.X, track.Y, filled, track.Height));
            context.DrawEllipse(accent, null,
                new Point(track.X + filled, track.Y + track.Height / 2), 5, 5);

            FormattedText value = TrackedText.Make(_format(_value), 12, bold: true,
                IsEnabled ? GuiTheme.TextBrush : dim);
            context.DrawText(value, new Point(Bounds.Width - 4 - value.Width,
                (Bounds.Height - value.Height) / 2));
        }
    }
}
