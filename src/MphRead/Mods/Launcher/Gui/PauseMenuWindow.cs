using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MphRead.Mods;
using MphRead.Mods.Network;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// What Escape shows during a match: resume, the window mode, the settings,
    /// and the two ways out.
    ///
    /// Sized to the game window and laid straight over it, so it reads as the
    /// game's own pause screen rather than as a dialog the game happens to
    /// have opened. It was a 340x392 box in the middle of the desktop before,
    /// which is what a settings prompt looks like and not what pressing
    /// Escape in a game does.
    ///
    /// The match is still running behind it, which is the point -- a networked
    /// match cannot be paused, and watching it carry on is more honest than
    /// pretending it stopped -- so the fill is a scrim rather than a wall. A
    /// compositor that will not give a window an alpha channel renders it
    /// opaque, which loses the view of the match and nothing else.
    ///
    /// Unlike the WinForms menu this replaces, it does not own a thread. The
    /// game's own thread is the one Avalonia was set up on, so the menu is a
    /// window on that thread and <see cref="PauseMenu.Poll"/> gives the toolkit
    /// a slice of every frame. That is what makes it work on every platform:
    /// macOS will not accept windows off the main thread at all, and a second
    /// UI thread is not something Avalonia offers anywhere.
    /// </summary>
    internal sealed class PauseMenuWindow : Window
    {
        private static PauseMenuWindow? _open;
        /// <summary>
        /// The settings dialog, while it is up over a match. Tracked so it can
        /// be moved with the game window too: it covers the same rectangle,
        /// and one of the two following the game while the other stays put
        /// would be worse than neither doing it.
        /// </summary>
        private static Window? _openSettings;
        private readonly PauseMenuView _view;
        private bool _settingsOpen;

        /// <summary>
        /// Lay the in-game windows back over the game window, because it has
        /// moved or been resized. Called from <see cref="PauseMenu.Poll"/>,
        /// on the game's own thread, which is the toolkit's thread too.
        /// </summary>
        internal static void FollowGameWindow()
        {
            if (_open != null)
            {
                CoverGameWindow(_open);
            }
            if (_openSettings != null)
            {
                CoverGameWindow(_openSettings);
            }
        }

        /// <summary>Open the menu, or bring the one already up to the front.</summary>
        public static bool Open()
        {
            if (_open != null)
            {
                _open.Activate();
                return true;
            }
            var window = new PauseMenuWindow();
            _open = window;
            window.Show();
            window.Activate();
            return true;
        }

        /// <summary>Close it from the game's own code path.</summary>
        public static void CloseIfOpen()
        {
            PauseMenuWindow? window = _open;
            if (window == null)
            {
                return;
            }
            try
            {
                window.Close();
            }
            catch (Exception)
            {
                // A menu that will not close must not take the match with it.
            }
        }

        public static bool IsOpen => _open != null;

        private PauseMenuWindow()
        {
            // Not just the product name: the game window carries that, and two
            // windows with one title is what an alt-tab list cannot tell apart.
            Title = $"{Mods.Branding.Name} - paused";
            Icon = GuiTheme.AppIcon.Value;
            CanResize = false;
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = _scrimLevels;
            Background = GuiTheme.ScrimBrush;
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            // The game may be borderless fullscreen, and a menu that
            // disappears behind the window it belongs to is not a menu.
            Topmost = true;
            ShowInTaskbar = false;
            CoverGameWindow(this);

            // The entries themselves live in PauseMenuView, which Android
            // shows through an overlay because it has no windows to put this
            // in. What is left here is the window and what the entries mean on
            // a desktop.
            _view = new PauseMenuView(offerWindowMode: true);
            _view.Resumed += (_, _) => Close();
            _view.FullscreenRequested += (_, _) =>
            {
                PauseMenu.RequestFullscreenToggle();
                Close();
            };
            _view.SettingsRequested += (_, _) => OpenSettings();
            _view.VoteMapRequested += (_, _) => OpenMapVote();
            _view.SpectateRequested += (_, _) => { SpectatorMode.Start(); Close(); };
            _view.RejoinRequested += (_, _) => { SpectatorMode.Rejoin(); Close(); };
            _view.RecordToggleRequested += (_, _) =>
            {
                if (DemoRecorder.IsRecording)
                {
                    Console.WriteLine($"[demo] recording saved to {DemoRecorder.CurrentPath}");
                    DemoRecorder.Stop();
                }
                else
                {
                    DemoRecorder.Start();
                }
                Close();
            };
            _view.LeaveRequested += (_, _) => { PauseMenu.RequestLeave(); Close(); };
            _view.QuitRequested += (_, _) => { PauseMenu.RequestQuit(); Close(); };
            Content = _view;
        }

        /// <summary>
        /// Transparency worth asking for, best first. Avalonia walks the list
        /// and takes the first the platform will give; None is last, and is
        /// the honest fallback rather than a failure.
        /// </summary>
        private static readonly IReadOnlyList<WindowTransparencyLevel> _scrimLevels =
            new[] { WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None };

        /// <summary>
        /// Put a window exactly over the game's client area, or over the
        /// screen when the game has not said where it is yet.
        ///
        /// Shared with the settings, which is opened from here and has to land
        /// on the same rectangle: two in-game screens of different sizes in
        /// different places is the "popup" shape all over again.
        ///
        /// Called twice, once in the constructor and again from OnOpened.
        /// RenderScaling is 1 until the window has been given a screen, so on
        /// a display running at anything but 100% the first call gets the
        /// conversion wrong and only the second one can be right -- but the
        /// first is still worth making, because it is what stops the window
        /// appearing in the middle of the desktop for a frame before moving.
        /// </summary>
        internal static void CoverGameWindow(Window window)
        {
            if (PauseMenu.WindowWidth <= 0 || PauseMenu.WindowHeight <= 0)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            var origin = new PixelPoint(PauseMenu.WindowX, PauseMenu.WindowY);
            if (window.Position != origin)
            {
                window.Position = origin;
            }
            // Client pixels, which is what GLFW reports and what Avalonia's
            // Position is in. Width and Height are device-independent, so the
            // display's scaling has to come back out of them or the menu
            // overhangs the game by that factor.
            //
            // Not RenderScaling: it is 1 until the window has been given a
            // screen, so the first call -- the one in the constructor, which
            // is what stops the window appearing in the middle of the desktop
            // for a frame -- would get it wrong on any display not running at
            // 100%. The screen under the game window knows before anybody has
            // been shown anything.
            double scale = ScalingAt(window, origin);
            double width = PauseMenu.WindowWidth / scale;
            double height = PauseMenu.WindowHeight / scale;
            // Only when it actually changed. This runs on every frame of a
            // drag, and assigning a size is a layout pass whether or not the
            // number moved; a move is not a resize.
            if (Math.Abs(window.Width - width) > 0.5
                || Math.Abs(window.Height - height) > 0.5)
            {
                window.Width = width;
                window.Height = height;
            }
        }

        private static double ScalingAt(Window window, PixelPoint point)
        {
            try
            {
                Avalonia.Platform.Screen? screen = window.Screens?.ScreenFromPoint(point);
                if (screen != null && screen.Scaling > 0)
                {
                    return screen.Scaling;
                }
            }
            catch (Exception)
            {
                // A backend with no screen information is not a reason to
                // refuse to draw the menu.
            }
            return window.RenderScaling > 0 ? window.RenderScaling : 1;
        }

        private static string WindowLabel()
        {
            return WindowMode.IsFullscreen ? "Windowed" : "Fullscreen";
        }

        /// <summary>
        /// Open the settings over the menu, not under it.
        ///
        /// Both windows are topmost, because the game behind them may be
        /// borderless fullscreen and a menu that disappears behind the window it
        /// belongs to is not a menu. This one steps out of the topmost band
        /// while the dialog is up so that the two are not left arguing about
        /// which of them is in front.
        /// </summary>
        private async void OpenSettings()
        {
            if (_settingsOpen)
            {
                return;
            }
            _settingsOpen = true;
            bool wasTopmost = Topmost;
            Topmost = false;
            try
            {
                MenuSettings settings = GameState.LoadSettings();
                var window = new SettingsWindow(settings, inGame: true);
                _openSettings = window;
                await window.ShowDialog(this);
            }
            catch (Exception ex)
            {
                // Anything the settings could not do -- an unreadable settings
                // file, a preview cache it cannot count -- would otherwise reach
                // the toolkit as an unhandled exception, over a match still
                // being played.
                Console.WriteLine($"[pause] the settings could not be opened: {ex.Message}");
            }
            finally
            {
                _openSettings = null;
                _settingsOpen = false;
                Topmost = wasTopmost;
                Activate();
            }
        }

        private bool _voteOpen;

        /// <summary>
        /// Pick a map and put it to the room.
        ///
        /// The same grid the front screen chooses a map from -- pictures, not
        /// a list of names, because "which map is that" is the question a
        /// name cannot answer and the whole reason the previews exist. What
        /// happens after the click is the server's: this sends the proposal
        /// and closes, and the answer arrives as a line in the chat and a
        /// prompt on everybody's screen, this player's included.
        /// </summary>
        private async void OpenMapVote()
        {
            if (_voteOpen)
            {
                return;
            }
            string why = MapVote.WhyNotProposing();
            if (why.Length > 0)
            {
                // Said in the game's own chat rather than in a dialog here:
                // it is one sentence, the player is about to go back to the
                // match, and a modal box for it is a second thing to dismiss.
                Chat.ChatBox.System(why);
                Close();
                return;
            }
            _voteOpen = true;
            bool wasTopmost = Topmost;
            Topmost = false;
            try
            {
                var rooms = ThumbnailGenerator.MultiplayerRooms();
                if (rooms.Count == 0)
                {
                    Chat.ChatBox.System("no maps to vote for");
                    return;
                }
                string current = NetSession.ServerMatch?.RoomKey ?? rooms[0];
                var view = new MapPickerView(rooms, current);
                var window = new MapPickerWindow(view);
                view.Closed += (_, _) => window.Close();
                CoverGameWindow(window);
                await window.ShowDialog(this);
                if (view.RoomKey != null)
                {
                    MapVote.Propose(view.RoomKey);
                    Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[pause] the map vote could not be opened: {ex.Message}");
            }
            finally
            {
                _voteOpen = false;
                Topmost = wasTopmost;
                Activate();
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            CoverGameWindow(this);
            _view.FocusResume();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (ReferenceEquals(_open, this))
            {
                _open = null;
            }
            // Tell the game the cursor is its own again. Called straight out
            // rather than posted: this already runs on the game's own thread --
            // the toolkit is pumped from inside the render loop -- and what it
            // sets is a flag the next frame acts on.
            PauseMenu.MarkClosed();
            base.OnClosed(e);
        }
    }
}
