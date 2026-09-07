using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using MphRead.Mods.Network;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// Entry point for the graphical launcher, on every platform.
    ///
    /// The loop is the one every game with a front screen has: one launcher,
    /// then a match, then the launcher again. "Leave match" in the pause menu
    /// comes back here; "Quit" and closing the launcher are what end the
    /// program.
    ///
    /// The toolkit is set up **once, on the thread that calls in** -- which is
    /// the game's own thread, the one the GL context will belong to -- and each
    /// visit to the launcher is a nested dispatcher loop on it rather than a
    /// fresh application. Three things make that the right shape and not an
    /// optimisation:
    ///
    /// - Avalonia allows one application per process. A second
    ///   <c>AppBuilder.Setup</c> throws, so a launcher that stood one up per
    ///   visit worked exactly once and fell back to the text screen on the way
    ///   back from the first match.
    /// - macOS will not accept windows off the main thread. AppKit is not
    ///   thread-safe and a window created anywhere else does not draw, which
    ///   rules out the private UI thread the WinForms launcher used.
    /// - The pause menu needs the toolkit *during* a match, on the thread the
    ///   render loop is running on. Nothing else can pump it.
    /// </summary>
    public static class GuiLauncher
    {
        private static bool _setUp;
        private static bool _failed;

        /// <summary>
        /// Show the launcher, or say why it could not be shown.
        ///
        /// Returns false when there is no usable display -- a machine with no X
        /// or Wayland session, an SSH login without forwarding, a container, or
        /// a system missing the client libraries Avalonia binds. That is not an
        /// error worth stopping for: the text launcher does the same job, and
        /// falling back to it is the difference between "this build has no
        /// launcher on my machine" and "this build does not start".
        /// </summary>
        public static bool TryRun()
        {
            if (!EnsureSetup())
            {
                return false;
            }
            try
            {
                Run();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[launcher] the window could not be opened: {ex.Message}");
                Console.WriteLine("[launcher] falling back to the text launcher");
                Mods.DebugLog.Exception("launcher", ex);
                return false;
            }
        }

        /// <summary>
        /// Stand the toolkit up, once per process, on this thread.
        ///
        /// Also what the pause menu calls: in a session started from a command
        /// line rather than from the launcher, nothing has set the toolkit up
        /// and the first Escape is where it is needed.
        /// </summary>
        internal static bool EnsureSetup()
        {
            if (_setUp)
            {
                return true;
            }
            if (_failed || !Probe())
            {
                return false;
            }
            try
            {
#if ANDROID
                // Android stands the toolkit up itself, from the activity, and
                // has no desktop backend to detect. Nothing here runs there:
                // this whole class is the desktop launcher loop, and the head
                // in src/MphRead.Android is the entry point instead.
                _setUp = false;
                return false;
#else
                AppBuilder.Configure<LauncherApp>()
                    .UsePlatformDetect()
                    .WithInterFont()
                    .SetupWithoutStarting();
                _setUp = true;
                return true;
#endif
            }
            catch (Exception ex)
            {
                // Remembered, because everything that asks is in a loop or a
                // frame: a toolkit that could not start on this machine must be
                // asked once, not once a frame.
                _failed = true;
                Console.WriteLine($"[launcher] the window toolkit could not start: {ex.Message}");
                // The whole stack, into the debug log, because the message
                // alone is usually a type name from inside Skia or the X11
                // backend and says nothing about which library is missing.
                Mods.DebugLog.Exception("launcher", ex);
                SayWhyOnLinux();
                return false;
            }
        }

        /// <summary>
        /// What a Linux player who has just landed in the text launcher needs
        /// to be told.
        ///
        /// Falling back is the right behaviour -- the text launcher plays the
        /// same game -- but it is also indistinguishable from "this build has
        /// no window on my machine", and the reason is nearly always the same
        /// one: the graphical launcher binds a handful of desktop libraries
        /// that the game itself does not, so a system that runs the match
        /// perfectly well can still have no launcher. Reported from NixOS,
        /// where a prebuilt Linux binary finds no library at the path it was
        /// linked against at all.
        /// </summary>
        private static void SayWhyOnLinux()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                || OperatingSystem.IsAndroid())
            {
                return;
            }
            Console.WriteLine("[launcher] the game itself is unaffected -- the text launcher "
                + "below starts the same matches.");
            Console.WriteLine("[launcher] the window needs libICE, libSM and fontconfig, which "
                + "a minimal install often lacks:");
            Console.WriteLine("[launcher]   Debian/Ubuntu: sudo apt install libice6 libsm6 "
                + "libfontconfig1");
            Console.WriteLine("[launcher]   Fedora: sudo dnf install libICE libSM fontconfig");
            Console.WriteLine("[launcher]   NixOS/Guix: run it inside an FHS environment, "
                + "e.g. steam-run ./FruityPrime -launcher");
        }

        /// <summary>
        /// Is there a display at all? Checked before Avalonia is initialised
        /// rather than by catching its failure, because the failure is a native
        /// abort in some configurations and there is nothing to catch.
        /// </summary>
        private static bool Probe()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                return true;
            }
            string? display = Environment.GetEnvironmentVariable("DISPLAY");
            string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            if (String.IsNullOrEmpty(display) && String.IsNullOrEmpty(wayland))
            {
                Console.WriteLine("[launcher] no DISPLAY or WAYLAND_DISPLAY; "
                    + "using the text launcher");
                return false;
            }
            return true;
        }

        private static void Run()
        {
            LauncherPrefs.Load();
            if (GameFiles.Ready)
            {
                // Upstream's CheckSetup does this before anything runs; the
                // launcher is dispatched before that check, so it does it here
                // -- and tolerates the files being absent, which is the whole
                // reason it goes first.
                GameFiles.ApplyPaths();
                // A map added after the install was set up has no picture and
                // no sweep coming to give it one.
                Mods.ThumbnailGenerator.EnsureCustomPreviews();
            }
            IReadOnlyList<string> rooms = Array.Empty<string>();

            while (true)
            {
                PauseMenu.Reset();
                // Read again rather than reusing the object from the last time
                // round: the pause menu's settings window loads and commits its
                // own copy, so after a match this one is stale and would write
                // the old values back over it.
                MenuSettings settings = GameState.LoadSettings();
                // LoadSettings only fills in Features; the rest of the file
                // reaches the engine through Mods.GameSettings.
                Mods.GameSettings.Apply(settings);
                LauncherPrefs.Load();
                Mods.WindowMode.Startup = LauncherPrefs.WindowMode;
                if (rooms.Count == 0 && GameFiles.Ready)
                {
                    // Needs the game files: the room list is read out of them.
                    rooms = ThumbnailGenerator.MultiplayerRooms();
                }

                // Before the screen that offers "Random" as a hunter: the
                // roll is held for one launch so the joined server and the
                // loaded player agree, and this is where a launch begins.
                Hunters.Reroll();
                LaunchPlan plan = Ask(settings, rooms);
                if (plan.Kind == LaunchKind.None)
                {
                    return;
                }
                try
                {
                    MatchStart.Launch(settings, plan);
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine($"The game could not start: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    // The Windows build is a GUI binary with no console behind
                    // it, so the two lines above reach nobody: from the
                    // player's side the game simply disappears while a map is
                    // loading. This is the one place that can still be read
                    // afterwards -- and the whole reason the switch in the
                    // corner of the front screen exists.
                    Mods.DebugLog.Line("crash", "the match could not start");
                    Mods.DebugLog.Exception("crash", ex);
                    return;
                }
                finally
                {
                    // Both own a worker thread and a bound socket; a crash in
                    // the game must not leave either behind.
                    NetSession.Stop();
                    NetHostSession.Stop();
                    // The match may have left one up -- a settings window opened
                    // from the pause menu on the frame the match ended.
                    PauseMenuWindow.CloseIfOpen();
                }
                if (PauseMenu.QuitProgram)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Show the front screen and wait for an answer.
        ///
        /// A nested dispatcher loop rather than an application lifetime: the
        /// loop ends when the window closes, the thread carries on into the
        /// match, and the next visit is another loop on the same toolkit.
        /// </summary>
        private static LaunchPlan Ask(MenuSettings settings, IReadOnlyList<string> rooms)
        {
            var window = new HomeWindow(settings, rooms);
            var frame = new DispatcherFrame();
            window.Closed += (_, _) => frame.Continue = false;
            window.Show();
            Dispatcher.UIThread.PushFrame(frame);
            // The loop ends on the Closed event, which is raised before the
            // toolkit has finished taking the window down -- and the thread is
            // about to spend the next twenty minutes inside a match, where
            // nothing pumps it. On X11 the destroy request would sit unflushed
            // in the connection's output buffer for all of that, leaving a
            // launcher painted over the game that started from it.
            Pump();
            return window.Plan;
        }

        /// <summary>
        /// Give the toolkit a slice of this frame.
        ///
        /// Called once a frame by the game while the pause menu is up. The
        /// posted job runs after everything already queued -- native input
        /// included -- and ends the loop, so this processes what is pending and
        /// returns rather than taking the thread over.
        /// </summary>
        internal static void Pump()
        {
            if (!_setUp)
            {
                return;
            }
            var frame = new DispatcherFrame(exitWhenRequested: false);
            Dispatcher.UIThread.Post(() => frame.Continue = false,
                DispatcherPriority.Background);
            Dispatcher.UIThread.PushFrame(frame);
        }
    }

    /// <summary>
    /// The Avalonia application object. Fluent is here for the handful of stock
    /// controls the screens use -- the text boxes and the scroll bars; every
    /// other control on them is drawn by this code, because a launcher whose
    /// controls are half themed reads as broken rather than as a choice.
    /// </summary>
    internal sealed class LauncherApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            base.Initialize();
        }
    }
}
