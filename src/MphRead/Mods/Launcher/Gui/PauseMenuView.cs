using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MphRead.Mods.Network;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// What Escape shows during a match: resume, the settings, and the two ways
    /// out.
    ///
    /// A view rather than a window, because there is a platform with no windows
    /// on it to be. <see cref="PauseMenuWindow"/> wraps this on the desktop,
    /// where a small window over a still-running match is the right shape;
    /// Android shows the same object through the launcher's full-screen
    /// overlay, which is what <see cref="HomeView"/> already does with the
    /// settings. One menu either way, so an entry added here turns up on both
    /// rather than on whichever was remembered.
    ///
    /// It decides nothing itself. Every entry raises an event and the host acts
    /// on it: leaving a match is closing a window on one platform and swapping
    /// two views on the other, and neither of those belongs in a menu.
    /// </summary>
    internal sealed class PauseMenuView : UserControl
    {
        public event EventHandler? Resumed;
        public event EventHandler? SettingsRequested;
        public event EventHandler? LeaveRequested;
        public event EventHandler? QuitRequested;
        public event EventHandler? FullscreenRequested;
        public event EventHandler? SpectateRequested;
        public event EventHandler? RejoinRequested;
        public event EventHandler? RecordToggleRequested;

        private readonly MenuEntry _resume;

        /// <param name="offerWindowMode">
        /// Show the fullscreen/windowed entry. False on a phone, which has one
        /// window, it is already the whole screen, and there is no F11.
        /// </param>
        public PauseMenuView(bool offerWindowMode)
        {
            // The host is the size of the game, on every platform: a phone's
            // overlay is the screen and the desktop's window now covers the
            // one the match is being played in. So the entries are always a
            // panel of a stated width in the middle, never a column stretched
            // across whatever the match happens to be running at -- 1024 or
            // 3840 -- which is a menu you have to hunt across.
            const double panelWidth = 420;
            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(new Caption("Paused") { Height = 34 });
            // Titles only. Every entry here said what it did twice -- "Quit",
            // "Close FruityPrime" -- and the second saying is what made a
            // seven-line menu tall enough to be cut off by the window it is
            // drawn over.
            _resume = Add(stack, "Resume",
                () => Resumed?.Invoke(this, EventArgs.Empty));
            if (offerWindowMode)
            {
                var windowEntry = new MenuEntry(WindowLabel(), titleSize: 17);
                windowEntry.Click += (_, _) =>
                {
                    FullscreenRequested?.Invoke(this, EventArgs.Empty);
                    // The game thread does it on the next frame; reflect it
                    // here straight away so the label is not a lie for 16
                    // milliseconds.
                    windowEntry.Title = WindowMode.IsFullscreen ? "Windowed" : "Fullscreen";
                };
                stack.Children.Add(windowEntry);
            }
            Add(stack, "Settings",
                () => SettingsRequested?.Invoke(this, EventArgs.Empty));
            if (!DemoPlayback.IsActive)
            {
                if (SpectatorMode.IsSpectating)
                {
                    Add(stack, "Rejoin match",
                        () => RejoinRequested?.Invoke(this, EventArgs.Empty));
                }
                else if (SpectatorMode.CanSpectate)
                {
                    Add(stack, "Spectate",
                        () => SpectateRequested?.Invoke(this, EventArgs.Empty));
                }
                if (NetSession.Active)
                {
                    Add(stack, DemoRecorder.IsRecording ? "Stop recording" : "Record demo",
                        () => RecordToggleRequested?.Invoke(this, EventArgs.Empty));
                }
            }
            Add(stack, "Leave match",
                () => LeaveRequested?.Invoke(this, EventArgs.Empty));
            Add(stack, "Quit",
                () => QuitRequested?.Invoke(this, EventArgs.Empty));

            var panel = new Border
            {
                Background = GuiTheme.PanelBrush,
                // An edge, because what is behind this is a scrim of nearly
                // the same colour over a running match: without one the panel
                // and the dimmed game are two shades of the same dark and the
                // menu has no shape.
                BorderBrush = GuiTheme.EdgeBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22, 18, 22, 18),
                Child = stack
            };
            panel.MaxWidth = panelWidth;
            panel.CornerRadius = new CornerRadius(6);
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;
            // What the panel needs, worked out from what was just put in it
            // rather than measured later: every entry states its own height,
            // so this is a fact about the menu and not a guess about layout.
            double needed = PanelPadding;
            foreach (Control child in stack.Children)
            {
                // Every entry here states its height; anything that did not
                // would measure as NaN and take the whole sum with it.
                needed += (Double.IsNaN(child.Height) ? 0 : child.Height) + stack.Spacing;
            }
            _neededHeight = needed;
            // Shrunk to fit, then scrolled if even that is not enough.
            //
            // A maximum width rather than a fixed one, and a scroller under
            // it, because the host is the game window and the game window is
            // whatever size the player dragged it to -- but a scrollbar is a
            // poor answer for a pause menu: what it produces is a panel with
            // its top and bottom cut off, which is what "the menu is always
            // bitten" was. Scaling is the better one at this size, because
            // there is nothing here to reflow: seven entries in a column stay
            // seven entries in a column, just smaller. It only ever shrinks --
            // a menu that grew to fill a 4K window would be a menu in
            // 40-point type.
            _scaler = new LayoutTransformControl
            {
                Child = panel,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var scroller = new ScrollViewer
            {
                Content = _scaler,
                Padding = new Thickness(12),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            scroller.SizeChanged += (_, e) => FitToHost(e.NewSize.Height);
            Content = scroller;
        }

        /// <summary>The panel's own top and bottom padding, plus the scroller's.</summary>
        private const double PanelPadding = 18 + 18 + 12 + 12;

        /// <summary>How tall the panel wants to be, at full size.</summary>
        private readonly double _neededHeight;
        private readonly LayoutTransformControl _scaler;

        /// <summary>
        /// Fit the panel to the height it has been given, down to half size.
        ///
        /// Below that the scroller takes over: text that small is not a menu
        /// either, and a window that short is not one anybody is playing in.
        /// </summary>
        private void FitToHost(double height)
        {
            if (height <= 0 || _neededHeight <= 0)
            {
                return;
            }
            double scale = Math.Clamp(height / _neededHeight, 0.5, 1);
            var current = _scaler.LayoutTransform as ScaleTransform;
            if (current != null && Math.Abs(current.ScaleY - scale) < 0.001)
            {
                return;
            }
            _scaler.LayoutTransform = scale >= 1 ? null : new ScaleTransform(scale, scale);
        }

        /// <summary>
        /// Somebody who just asked for this is looking at a short list and
        /// expects the top entry to be the one already chosen.
        /// </summary>
        public void FocusResume()
        {
            Dispatcher.UIThread.Post(() => _resume.Focus(), DispatcherPriority.Background);
        }

        private static string WindowLabel()
        {
            return WindowMode.IsFullscreen ? "Windowed" : "Fullscreen";
        }

        private static MenuEntry Add(StackPanel stack, string text, Action action)
        {
            var entry = new MenuEntry(text, titleSize: 17);
            entry.Click += (_, _) => action();
            stack.Children.Add(entry);
            return entry;
        }
    }
}
