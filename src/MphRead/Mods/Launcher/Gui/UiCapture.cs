#if MPHREAD_AVALONIA
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MphRead.Mods.Network;

namespace MphRead.Mods.Launcher.Gui
{
    /// <summary>
    /// Screenshots of the front screen, without a screen.
    ///
    /// The launcher is the one part of this program that could not be looked
    /// at from here: the game renders through GL and can be read back
    /// (ScreenCapture), but the launcher is Avalonia, and checking a change to
    /// it meant opening a window on a machine with a display and looking. On a
    /// headless box, or over SSH, or in CI, there was no way to see what a
    /// layout change had actually done -- which is how a control that moves
    /// under the pointer ships.
    ///
    /// Avalonia can measure, arrange and draw a control into a bitmap with no
    /// window involved, which is all a screenshot of a layout needs. So
    /// `-uishot DIR` builds each screen at a fixed size, renders it, and
    /// writes a PNG.
    ///
    /// What this does *not* prove: that a real window manager gives the window
    /// the size asked for, that the fonts on another machine are these ones,
    /// or that anything is clickable. It proves the layout -- which is what
    /// every report about this screen has been about.
    /// </summary>
    internal static class UiCapture
    {
        /// <summary>The window size the launcher opens at (see HomeWindow).</summary>
        private static readonly Size _windowSize = new Size(940, 560);

        public static int Run(string directory)
        {
            if (!GuiLauncher.EnsureSetup())
            {
                Console.WriteLine("[uishot] no Avalonia backend on this machine; nothing captured");
                return 1;
            }
            Directory.CreateDirectory(directory);
            // The front screen's Share button only exists where something can
            // receive a file, which today is Android alone -- so without a
            // stand-in the one corner this tool was made to check could never
            // be photographed as a phone draws it. Same reason as SampleDemos
            // below, and it is still only offered when real logs exist.
            Mods.LogShare.Current ??= new CaptureLogShare();
            int written = 0;
            // On the toolkit's own thread, and drained afterwards: the views
            // post work to the dispatcher as they are built (the front screen
            // focuses its first control that way), and a render before that
            // has run is a picture of a half-built screen.
            Dispatcher.UIThread.Invoke(() =>
            {
                var settings = new MenuSettings();
                List<string> rooms = RoomList();
                foreach ((string name, Control view, Size size) in Screens(settings, rooms))
                {
                    string path = Path.Combine(directory, $"{name}.png");
                    if (Capture(view, path, size))
                    {
                        written++;
                        Console.WriteLine($"[uishot] {path}");
                    }
                }
            });
            Console.WriteLine($"[uishot] {written} screen(s) written to {directory}");
            return written > 0 ? 0 : 1;
        }

        private static List<string> RoomList()
        {
            var rooms = new List<string>();
            try
            {
                foreach (RoomMetadata meta in Metadata.RoomMetadata.Values)
                {
                    if (meta.Multiplayer)
                    {
                        rooms.Add(meta.Name);
                    }
                }
            }
            catch (Exception)
            {
                // No game files here. The screens still lay out; the map rows
                // are simply empty, which is itself worth being able to see.
            }
            rooms.Sort(StringComparer.OrdinalIgnoreCase);
            return rooms;
        }

        private static IEnumerable<(string, Control, Size)> Screens(MenuSettings settings,
            IReadOnlyList<string> rooms)
        {
            yield return ("home", new HomeView(settings, rooms), _windowSize);
            yield return ("settings", new SettingsView(settings), _windowSize);
            var credits = new SettingsView(settings);
            credits.ShowSection("Credits");
            yield return ("settings-credits", credits, _windowSize);
            if (rooms.Count > 0)
            {
                yield return ("mappicker", new MapPickerView(rooms, rooms[0]), _windowSize);
            }
            // Both halves of it: the list a machine that has recorded
            // something gets, and the line a machine that has not gets --
            // which is the one carrying the folder's path and the only place
            // that path is ever written down.
            yield return ("demopicker", new DemoPickerView(SampleDemos(),
                Network.DemoLibrary.Directory), _windowSize);
            yield return ("demopicker-empty", new DemoPickerView(
                Array.Empty<Network.DemoRecording>(), Network.DemoLibrary.Directory),
                _windowSize);
            yield return ("pausemenu", new PauseMenuView(offerWindowMode: true), _windowSize);
            // Deliberately shorter than the menu's own content, and shorter
            // than the game window is now allowed to be. The pause menu is
            // laid over the game window, so its host is whatever size the
            // player dragged that to, and entries drawn off the bottom edge
            // are a player who cannot leave the match. This is the check that
            // the scroll view carries them.
            yield return ("pausemenu-small", new PauseMenuView(offerWindowMode: true),
                new Size(560, 320));
            yield return ("serverbrowser", ServerList(), _windowSize);
        }

        /// <summary>
        /// Somewhere for the Share button to point while it is being
        /// photographed. Nothing is built and nothing is sent: a capture has
        /// nobody to press it.
        /// </summary>
        private sealed class CaptureLogShare : Mods.ILogShare
        {
            public string StagingPath(string fileName) =>
                Path.Combine(Path.GetTempPath(), fileName);

            public bool Share(string path, string subject, out string error)
            {
                error = "there is nothing to share to on this platform";
                return false;
            }
        }

        /// <summary>
        /// Recordings that are not there, so the list can be seen on a machine
        /// that has never recorded one.
        /// </summary>
        private static IReadOnlyList<Network.DemoRecording> SampleDemos()
        {
            var now = new DateTime(2026, 9, 4, 18, 22, 7);
            return new[]
            {
                new Network.DemoRecording("MP3 PROVING GROUND_2026-09-04_18-22-07.fpdemo",
                    "MP3 PROVING GROUND", now, 1_512_320),
                new Network.DemoRecording("COMBAT HALL_2026-09-02_21-04-55.fpdemo",
                    "COMBAT HALL", now.AddDays(-2), 402_112),
                new Network.DemoRecording("sent-to-me.fpdemo", "", now.AddDays(-9), 88_400)
            };
        }

        /// <summary>
        /// The browser's table, at the width the panel gives it, with rows
        /// standing in for servers that are not up.
        ///
        /// Built here rather than reached through HomeView because the card is
        /// private to it and only fills in when a directory answers -- and the
        /// fault this is for (a map name wrapping onto the row below, headings
        /// running into each other) is a property of the columns and the
        /// width, not of any real server. Both widths are drawn: the panel's,
        /// and the 400 the rest of the cards use, so a narrow row is checked
        /// too.
        /// </summary>
        private static Control ServerList()
        {
            var stack = new StackPanel { Spacing = 18, Margin = new Thickness(12) };
            foreach (double width in new[] { 600.0, 400.0 })
            {
                var list = new StackPanel { Spacing = 2, Width = width };
                list.Children.Add(new ServerHeader());
                foreach ((string name, string room, GameMode mode, int players, int ping) in _sampleServers)
                {
                    var row = new ServerRow(name, "203.0.113.7:27888");
                    row.SetStatus(new ServerStatus
                    {
                        Online = true,
                        RoomKey = room,
                        Mode = mode,
                        Players = players,
                        MaxPlayers = 8,
                        Latency = ping
                    });
                    list.Children.Add(row);
                }
                stack.Children.Add(list);
            }
            return stack;
        }

        private static readonly (string, string, GameMode, int, int)[] _sampleServers =
        {
            ("net.livetek.fr", "MP3 PROVING GROUND", GameMode.Battle, 3, 41),
            ("A very long server name indeed", "MP7 PROCESSOR CORE", GameMode.PrimeHunter, 8, 152),
            ("lan", "MP2 HARVESTER", GameMode.Bounty, 1, 2)
        };

        /// <summary>
        /// Render one screen.
        ///
        /// Through a real <see cref="Window"/>, not by laying the control out
        /// on its own. Avalonia resolves styles through the visual tree's
        /// style host, and a control with no window above it has none: it
        /// measures, arranges and renders perfectly happily and comes out a
        /// flat rectangle of the background colour, which is exactly what the
        /// first attempt at this produced. The window is what connects the
        /// tree to the Application's styles.
        ///
        /// It is shown, because a window that has never been shown has no
        /// layout pass behind it -- but shown *off the side of the display*
        /// and without taking focus, so a capture run does not steal the
        /// pointer or flash a window per screen.
        /// </summary>
        private static bool Capture(Control view, string path, Size size)
        {
            Window? window = null;
            try
            {
                window = new Window
                {
                    Width = size.Width,
                    Height = size.Height,
                    Background = GuiTheme.PanelBrush,
                    RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark,
                    SystemDecorations = SystemDecorations.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Position = new PixelPoint(-4000, -4000),
                    Content = view
                };
                window.Show();
                // The views post work to the dispatcher as they are built --
                // the front screen focuses its first control that way, and the
                // map picker loads its pictures -- and a render before that has
                // run is a picture of a half-built screen. Several passes,
                // because one job can queue another.
                for (int i = 0; i < 8; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                }
                window.Measure(size);
                window.Arrange(new Rect(size));
                Dispatcher.UIThread.RunJobs();
                var bitmap = new RenderTargetBitmap(
                    new PixelSize((int)size.Width, (int)size.Height),
                    new Vector(96, 96));
                bitmap.Render(window);
                bitmap.Save(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[uishot] {Path.GetFileName(path)} could not be rendered: {ex.Message}");
                return false;
            }
            finally
            {
                window?.Close();
            }
        }

    }
}
#endif
