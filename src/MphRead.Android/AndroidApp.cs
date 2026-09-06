using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using MphRead.Mods;
using MphRead.Mods.Launcher;
using MphRead.Mods.Launcher.Gui;

namespace MphRead.Droid
{
    /// <summary>
    /// The Avalonia application on Android.
    ///
    /// A phone has one view rather than a desktop full of windows, so this is a
    /// single view lifetime -- and the view it shows is <see cref="HomeView"/>,
    /// the desktop front screen itself. Not a copy of it, not a phone-shaped
    /// rewrite of it: the same file, which folds to one column below a width and
    /// opens its settings and map grid as overlays where there is no second
    /// window to open. A change to the launcher is a change to both platforms,
    /// which is the whole reason this is Avalonia.
    ///
    /// What is left here is the front half of the loop the desktop's
    /// <c>GuiLauncher</c> runs: read the settings, ask the screen, start what it
    /// asked for. The difference is that a match is a view swap in
    /// <see cref="MainActivity"/> rather than a window on this thread.
    /// </summary>
    public class AndroidApp : Application
    {
        /// <summary>The front screen, for the activity to drive after a match.</summary>
        internal static HomeView? Home { get; private set; }

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            base.Initialize();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            {
                single.MainView = Home = BuildHome();
            }
            base.OnFrameworkInitializationCompleted();
        }

        private static HomeView BuildHome()
        {
            LauncherPrefs.Load();
            // Keys, mouse feel, pad bindings and the touch layout. The
            // desktop reads these from ModEntry.TryHandleHeadless, which the
            // head never runs -- so nothing here ever loaded them, and every
            // control the player changed was back to its default the next
            // time the app opened. After LauncherPrefs.Load, since
            // controls.txt sits in the directory that has just been named.
            Mods.InputSettings.Load();
            // After the load, because the head has just pointed the
            // preferences at the app's own data directory -- the package's
            // directory is read-only, and that is also where the log has to
            // go. Same switch, same file, same corner of the same screen as
            // the desktop's.
            Mods.DebugLog.Attach();
            MenuSettings settings = GameState.LoadSettings();
            GameSettings.Apply(settings);
            IReadOnlyList<string> rooms = Array.Empty<string>();
            if (GameFiles.Ready)
            {
                // The room list is read out of the extracted files, and the
                // launcher runs before upstream's own setup check does.
                GameFiles.ApplyPaths();
                rooms = ThumbnailGenerator.MultiplayerRooms();
            }
            var home = new HomeView(settings, rooms);
            home.Done += (_, plan) =>
            {
                if (plan.Kind == LaunchKind.None)
                {
                    MainActivity.Instance?.Finish();
                    return;
                }
                MainActivity.Instance?.StartMatch(plan);
            };
            return home;
        }
    }
}
