using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>A small upper-case heading over a group of rows.</summary>
    internal sealed class Caption : Control
    {
        private readonly string _text;

        public Caption(string text)
        {
            _text = text;
            Height = 26;
        }

        /// <summary>
        /// Its own width when nothing constrains it, for the same reason
        /// <see cref="MenuEntry.MeasureOverride"/> has one: in a row, a
        /// control that measures to nothing is drawn on top of its neighbours.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            Size size = base.MeasureOverride(availableSize);
            return new Size(Math.Min(Label().Width + 8, availableSize.Width), size.Height);
        }

        private FormattedText Label()
        {
            return new FormattedText(_text.ToUpperInvariant(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, GuiTheme.Face(bold: true), 11,
                GuiTheme.TextDimBrush);
        }

        public override void Render(DrawingContext context)
        {
            FormattedText text = Label();
            context.DrawText(text, new Point(0, Bounds.Height - text.Height - 4));
            double y = Bounds.Height - 2;
            context.DrawLine(new Pen(GuiTheme.EdgeBrush, 1),
                new Point(0, y), new Point(Bounds.Width, y));
        }
    }

    /// <summary>
    /// One setting with a fixed set of answers: a label, the current answer,
    /// and an arrow on each side.
    ///
    /// Cycling rather than a drop-down because every list here is short and a
    /// combo box is the one control WinForms would not draw dark -- which is
    /// the reason the original exists. Keeping the same shape here is not
    /// obligation but consistency: the two screens are one product.
    /// </summary>
    internal sealed class ChoiceRow : Control
    {
        private readonly string _label;
        private IReadOnlyList<string> _options;
        private int _index;
        private bool _leftHot;
        private bool _rightHot;

        public event EventHandler? Changed;

        public int Index
        {
            get => _index;
            set
            {
                int clamped = _options.Count == 0 ? 0 : Math.Clamp(value, 0, _options.Count - 1);
                if (clamped != _index)
                {
                    _index = clamped;
                    InvalidateVisual();
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string Value => _options.Count == 0 ? "" : _options[_index];

        public ChoiceRow(string label, IReadOnlyList<string> options, int index = 0)
        {
            _label = label;
            _options = options;
            _index = options.Count == 0 ? 0 : Math.Clamp(index, 0, options.Count - 1);
            Height = 34;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        /// <summary>Replace the options in place, e.g. after a room list changes.</summary>
        public void SetItems(IReadOnlyList<string> options, int index = 0)
        {
            _options = options;
            _index = options.Count == 0 ? 0 : Math.Clamp(index, 0, options.Count - 1);
            InvalidateVisual();
        }

        /// <summary>
        /// Width of the arrow buttons, and of the column the value is drawn
        /// in between them.
        /// </summary>
        private const double ArrowWidth = 28;
        private const double ValueColumn = 180;

        /// <summary>
        /// Drawn in a square at the right-hand end of the row, past the
        /// forward arrow, when the answer is a thing better shown than named
        /// -- a crosshair, at the size and shape the row has just picked.
        /// Everything else in the row shifts left to make room for it.
        /// </summary>
        public Action<DrawingContext, Rect>? Preview
        {
            get => _preview;
            set
            {
                _preview = value;
                // Room for the picture. A crosshair at its largest is 36
                // points across, and a row of the ordinary height cannot show
                // that without shrinking it -- which would defeat a preview
                // whose job is partly to answer "how big is Big".
                Height = value == null ? 34 : 48;
                InvalidateVisual();
            }
        }

        private Action<DrawingContext, Rect>? _preview;

        private const double PreviewWidth = 52;

        private double PreviewRoom => _preview == null ? 0 : PreviewWidth;

        /// <summary>
        /// Both arrows sit still.
        ///
        /// The back arrow used to be placed from the width of the *value* --
        /// it slid left to make room for a long room name and back again for a
        /// short one. Which meant it moved as you stepped: the button jumped
        /// out from under the pointer between one map and the next, so a
        /// second click landed on the row instead of the arrow and stepped
        /// forward again. Picking a map by clicking became a thing you had to
        /// re-aim for every time.
        ///
        /// So the value gets a column of its own, of a fixed width, and is
        /// trimmed to it. The arrows never move, and the row is still one
        /// height on every card.
        /// </summary>
        private Rect LeftArrow
        {
            get
            {
                double x = Bounds.Width - PreviewRoom - ArrowWidth - ValueColumn - ArrowWidth;
                // Never over the label, on a card too narrow for the column.
                return new Rect(Math.Max(110, x), 0, ArrowWidth, Bounds.Height);
            }
        }

        private Rect RightArrow =>
            new(Bounds.Width - PreviewRoom - ArrowWidth, 0, ArrowWidth, Bounds.Height);

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            Point p = e.GetPosition(this);
            bool left = LeftArrow.Contains(p);
            bool right = RightArrow.Contains(p);
            if (left != _leftHot || right != _rightHot)
            {
                _leftHot = left;
                _rightHot = right;
                InvalidateVisual();
            }
            base.OnPointerMoved(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            _leftHot = _rightHot = false;
            InvalidateVisual();
            base.OnPointerExited(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            Focus();
            Point p = e.GetPosition(this);
            // Anywhere that is not the back arrow steps forward, so the row can
            // be poked at without aiming.
            if (LeftArrow.Contains(p))
            {
                Step(-1);
            }
            else
            {
                Step(1);
            }
            base.OnPointerPressed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                Step(-1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right || e.Key == Key.Enter || e.Key == Key.Space)
            {
                Step(1);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        private void Step(int direction)
        {
            if (_options.Count == 0)
            {
                return;
            }
            // Wrapping, because the lists are short and running off the end of
            // one is more annoying than useful.
            _index = (_index + direction + _options.Count) % _options.Count;
            InvalidateVisual();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public override void Render(DrawingContext context)
        {
            // See MenuEntry.Render: hit testing follows the drawing.
            context.FillRectangle(Brushes.Transparent,
                new Rect(0, 0, Bounds.Width, Bounds.Height));
            if (IsFocused)
            {
                context.FillRectangle(GuiTheme.PanelLightBrush,
                    new Rect(0, 0, Bounds.Width, Bounds.Height), 4);
            }
            var label = new FormattedText(_label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, GuiTheme.Face(false), 13, GuiTheme.TextDimBrush);
            context.DrawText(label, new Point(4, (Bounds.Height - label.Height) / 2));

            // The value lives in the fixed column between the arrows, and is
            // trimmed to it rather than pushing them apart.
            var value = new FormattedText(Value, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, GuiTheme.Face(true), 13, GuiTheme.TextBrush);
            Rect left = LeftArrow;
            double room = RightArrow.X - left.Right - 8;
            if (value.Width > room)
            {
                value.MaxTextWidth = Math.Max(20, room);
                value.Trimming = TextTrimming.CharacterEllipsis;
            }
            double centre = (left.Right + RightArrow.X) / 2;
            context.DrawText(value, new Point(centre - value.Width / 2,
                (Bounds.Height - value.Height) / 2));

            Arrow(context, left, pointsLeft: true, _leftHot);
            Arrow(context, RightArrow, pointsLeft: false, _rightHot);
            if (_preview != null)
            {
                const double inset = 3;
                _preview(context, new Rect(Bounds.Width - PreviewWidth + inset, inset,
                    PreviewWidth - inset * 2, Bounds.Height - inset * 2));
            }
        }

        private static void Arrow(DrawingContext context, Rect area, bool pointsLeft, bool hot)
        {
            double cx = area.X + area.Width / 2;
            double cy = area.Y + area.Height / 2;
            const double w = 4.5;
            const double h = 6;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext sink = geometry.Open())
            {
                if (pointsLeft)
                {
                    sink.BeginFigure(new Point(cx + w, cy - h), true);
                    sink.LineTo(new Point(cx - w, cy));
                    sink.LineTo(new Point(cx + w, cy + h));
                }
                else
                {
                    sink.BeginFigure(new Point(cx - w, cy - h), true);
                    sink.LineTo(new Point(cx + w, cy));
                    sink.LineTo(new Point(cx - w, cy + h));
                }
                sink.EndFigure(true);
            }
            context.DrawGeometry(hot ? GuiTheme.AccentBrush : GuiTheme.TextDimBrush,
                null, geometry);
        }
    }

    /// <summary>One setting that is on or off.</summary>
    internal sealed class ToggleRow : Control
    {
        private readonly string _label;
        private bool _on;

        public event EventHandler? Changed;

        public bool On
        {
            get => _on;
            set
            {
                if (_on != value)
                {
                    _on = value;
                    InvalidateVisual();
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ToggleRow(string label, bool on)
        {
            _label = label;
            _on = on;
            Height = 34;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            Focus();
            On = !On;
            base.OnPointerPressed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space
                || e.Key == Key.Left || e.Key == Key.Right)
            {
                On = !On;
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        public override void Render(DrawingContext context)
        {
            // See MenuEntry.Render: hit testing follows the drawing.
            context.FillRectangle(Brushes.Transparent,
                new Rect(0, 0, Bounds.Width, Bounds.Height));
            if (IsFocused)
            {
                context.FillRectangle(GuiTheme.PanelLightBrush,
                    new Rect(0, 0, Bounds.Width, Bounds.Height), 4);
            }
            var label = new FormattedText(_label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, GuiTheme.Face(false), 13, GuiTheme.TextDimBrush);
            context.DrawText(label, new Point(4, (Bounds.Height - label.Height) / 2));

            const double w = 40;
            const double h = 20;
            var track = new Rect(Bounds.Width - w - 4, (Bounds.Height - h) / 2, w, h);
            context.DrawRectangle(
                new SolidColorBrush(_on ? GuiTheme.Accent : GuiTheme.Edge), null,
                new RoundedRect(track, h / 2));
            double knob = _on ? track.Right - h / 2 : track.X + h / 2;
            context.DrawEllipse(new SolidColorBrush(_on ? GuiTheme.Ink : GuiTheme.TextDim),
                null, new Point(knob, track.Y + h / 2), h / 2 - 3, h / 2 - 3);
        }
    }

    /// <summary>A label and something to type in.</summary>
    internal sealed class FieldRow : Panel
    {
        public TextBox Box { get; }

        public string Value
        {
            get => Box.Text ?? "";
            set => Box.Text = value;
        }

        public FieldRow(string label, string value, double boxWidth = 150)
        {
            Height = 36;
            var caption = new TextBlock
            {
                Text = label,
                FontFamily = GuiTheme.Display,
                FontSize = 13,
                Foreground = GuiTheme.TextDimBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(4, 0, 0, 0)
            };
            // Colours are left to the Fluent dark theme rather than set here:
            // the text box's template paints from its own theme resources and
            // ignores a Background put on the control, so setting one would
            // look like it was doing something while the theme decided.
            Box = new TextBox
            {
                Text = value,
                Width = boxWidth,
                FontFamily = GuiTheme.Display,
                FontSize = 13,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Children.Add(caption);
            Children.Add(Box);
        }
    }

    /// <summary>A line of explanation, wrapped, under a group of rows.</summary>
    internal sealed class Note : TextBlock
    {
        public Note(string text, Color? color = null)
        {
            Text = text;
            FontFamily = GuiTheme.Display;
            FontSize = 12;
            Foreground = new SolidColorBrush(color ?? GuiTheme.TextDim);
            TextWrapping = TextWrapping.Wrap;
            Margin = new Thickness(4, 4, 4, 4);
        }
    }
}
