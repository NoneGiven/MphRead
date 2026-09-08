using Avalonia.Controls;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// A frame around <see cref="SettingsView"/>, and nothing else.
    ///
    /// Every setting, every row and every value lives in the view, which is
    /// what the Android head shows as a full-screen overlay instead. This is
    /// the size, the title and the two flags that only mean something over a
    /// match -- and over a match the size is the match's, so the settings fill
    /// the game window rather than floating in front of it.
    /// </summary>
    internal sealed class SettingsWindow : Window
    {
        private readonly SettingsView _view;

        /// <summary>True when the user pressed save rather than closing.</summary>
        public bool Saved => _view.Saved;

        public SettingsWindow(MenuSettings settings, bool inGame = false)
            : this(new SettingsView(settings, inGame))
        {
        }

        public SettingsWindow(SettingsView view)
        {
            _view = view;
            _view.Closed += (_, _) => Close();
            // Placing the pen zone happens on the game, not in here, so the
            // settings get out of the way. The view has already put itself
            // into placement mode; this is only the window closing.
            _view.StylusPlacementRequested += (_, _) => Close();

            Title = view.WindowTitle;
            Icon = GuiTheme.AppIcon.Value;
            Background = GuiTheme.InkBrush;
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            if (view.InGame)
            {
                // Over the match, exactly covering it, like the pause menu it
                // was opened from. A fixed 980x660 dialog centred on its owner
                // was two different rectangles in two different places for one
                // screen -- and on a game window smaller than that, a dialog
                // hanging off the edges of the thing it belongs to.
                CanResize = false;
                SystemDecorations = SystemDecorations.None;
                PauseMenuWindow.CoverGameWindow(this);
                // A game window that may be borderless fullscreen is what
                // topmost is for; the taskbar has the game in it already.
                Topmost = true;
                ShowInTaskbar = false;
            }
            else
            {
                // From the front screen it is an ordinary dialog and behaves
                // like one.
                Width = 980;
                Height = 660;
                MinWidth = 720;
                MinHeight = 460;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            Content = view;
        }

        protected override void OnOpened(System.EventArgs e)
        {
            base.OnOpened(e);
            if (_view.InGame)
            {
                // Again, now that the window has a screen and RenderScaling
                // is the display's rather than 1. See CoverGameWindow.
                PauseMenuWindow.CoverGameWindow(this);
            }
        }
    }
}
