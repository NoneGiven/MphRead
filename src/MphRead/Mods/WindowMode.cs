using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods
{
    public enum WindowStartMode
    {
        Windowed,
        BorderlessFullscreen
    }

    /// <summary>
    /// Windowed or borderless fullscreen, and the two ways in and out of it
    /// that people expect: F11 and Alt+Enter. Escape belongs to the pause
    /// menu now, which is where the same switch also sits as an entry.
    ///
    /// Borderless rather than exclusive fullscreen: it alt-tabs instantly,
    /// keeps the desktop resolution, and does not black the screen while the
    /// display mode changes -- which matters most for the one thing this
    /// build is for, a match somebody is trying to join while talking to
    /// their friends on something else.
    ///
    /// The window's own state is the truth. Nothing here caches "am I
    /// fullscreen" beyond what it needs to put the window back where it was.
    /// </summary>
    public static class WindowMode
    {
        /// <summary>
        /// How the next window should open. Set by the launcher from its
        /// saved preference, or by -fullscreen on the command line.
        /// </summary>
        public static WindowStartMode Startup { get; set; } = WindowStartMode.Windowed;

        public static bool IsFullscreen { get; private set; }

        private static WindowBorder _savedBorder = WindowBorder.Resizable;
        private static Vector2i _savedLocation;
        private static Vector2i _savedSize;
        private static bool _saved;

        /// <summary>Called once, after the window is first shown.</summary>
        public static void ApplyStartup(NativeWindow window)
        {
            if (Startup == WindowStartMode.BorderlessFullscreen && !IsFullscreen)
            {
                Enter(window);
            }
        }

        /// <summary>
        /// The keys that change the mode: F11 and Alt+Enter. Returns true when
        /// the key was one of them, so the caller can stop.
        ///
        /// Escape is not one of them any more: it opens the pause menu, which
        /// is where leaving fullscreen now lives along with everything else
        /// somebody presses Escape looking for.
        /// </summary>
        public static bool HandleKey(NativeWindow window, KeyboardKeyEventArgs e)
        {
            if (e.Key == Keys.F11 || (e.Key == Keys.Enter && e.Alt))
            {
                Toggle(window);
                return true;
            }
            return false;
        }

        public static void Toggle(NativeWindow window)
        {
            if (IsFullscreen)
            {
                Leave(window);
            }
            else
            {
                Enter(window);
            }
        }

        public static void Enter(NativeWindow window)
        {
            if (IsFullscreen)
            {
                return;
            }
            if (!_saved)
            {
                _savedBorder = window.WindowBorder;
                _savedLocation = window.Location;
                _savedSize = window.ClientSize;
                _saved = true;
            }
            MonitorInfo monitor = Monitors.GetMonitorFromWindow(window);
            // State first: leaving any Maximized/Minimized state before the
            // border changes, so the window manager isn't asked to strip
            // decorations off a window it still considers snapped.
            window.WindowState = WindowState.Normal;
            window.WindowBorder = WindowBorder.Hidden;
            // Some window managers only apply a border change once they've
            // processed an event since it was requested -- setting the
            // geometry in the same tick can compute it against the window's
            // pre-change (decorated) size, which is what needed a second F11
            // press to actually take effect. Pumping events here flushes that
            // pending change before Location/ClientSize are set below.
            GLFW.PollEvents();
            window.Location = monitor.ClientArea.Min;
            // One pixel short of the monitor, not an exact match: a
            // borderless window that covers a display exactly is what
            // Windows' fullscreen optimizations key off to promote it into
            // an exclusive-like mode, which then refuses to show *any* other
            // window above it -- Topmost included, which is the only thing
            // that puts the pause menu over this window at all (see
            // PauseMenuWindow). One pixel is not visible and keeps that from
            // triggering.
            window.ClientSize = new Vector2i(monitor.ClientArea.Size.X, monitor.ClientArea.Size.Y - 1);
            IsFullscreen = true;
            // And above the taskbar, which is the other half of covering the
            // screen. A borderless window is an ordinary window as far as the
            // desktop is concerned: it sits in the normal z-band, and the
            // taskbar (and the dock, and a panel) is always-on-top, so it
            // stayed drawn over the game -- pressing F11 filled the screen and
            // left the taskbar sitting on it, which is how it was reported.
            //
            // Exclusive fullscreen is what usually takes the screen away from
            // the shell, and it is the thing this deliberately is not; asking
            // for always-on-top instead gets the same picture while keeping
            // every reason borderless was chosen (instant alt-tab, no display
            // mode change, no black flash).
            SetTopmost(window, true);
        }

        public static void Leave(NativeWindow window)
        {
            if (!IsFullscreen)
            {
                return;
            }
            window.WindowState = WindowState.Normal;
            window.WindowBorder = _saved ? _savedBorder : WindowBorder.Resizable;
            GLFW.PollEvents();
            if (_saved)
            {
                window.ClientSize = _savedSize;
                window.Location = _savedLocation;
            }
            IsFullscreen = false;
            SetTopmost(window, false);
        }

        /// <summary>
        /// Whether the game window sits above the shell's own always-on-top
        /// windows. True for the length of borderless fullscreen, and dropped
        /// while the pause menu is up.
        /// </summary>
        public static bool IsTopmost => _topmost;

        private static bool _topmost;

        /// <summary>
        /// Ask the window manager to float this window, or stop.
        ///
        /// Cached, because <see cref="SyncTopmost"/> is called once a frame
        /// and this is a round trip to the window manager, not a field.
        ///
        /// Best-effort by nature: X11 window managers are free to ignore it
        /// and Wayland has no concept of it at all, so a failure here is a
        /// taskbar that is still visible, not a broken window. It is never
        /// worth an exception reaching the render loop.
        /// </summary>
        public static void SetTopmost(NativeWindow window, bool topmost)
        {
            if (_topmost == topmost)
            {
                return;
            }
            _topmost = topmost;
            try
            {
                unsafe
                {
                    GLFW.SetWindowAttrib(window.WindowPtr, WindowAttribute.Floating, topmost);
                }
            }
            catch (Exception)
            {
                // See above: not worth a match.
            }
        }

        /// <summary>
        /// Keep the floating state right, once a frame.
        ///
        /// The pause menu is a separate always-on-top Avalonia window, and two
        /// windows in the same always-on-top band are ordered by whichever the
        /// desktop last raised -- which is not something to rely on for the one
        /// window the player needs to be able to press. So the game window
        /// stands down for as long as the menu is up and takes the band back
        /// when it closes.
        ///
        /// **And it stands down the moment the window is not the focused
        /// one.** An always-on-top borderless window cannot be alt-tabbed away
        /// from in any way a person would recognise: the switch happens, the
        /// other window is given the keyboard, and the game stays drawn over
        /// the top of it -- which reads as a window that refuses to let go,
        /// and is how it was reported. Floating is only ever wanted for the
        /// one thing it was added for (covering the taskbar while the game is
        /// the window being used), and that is exactly the case where this
        /// window has the focus. Alt-tab away and it drops out of the band on
        /// the next frame; alt-tab back and it takes it again.
        /// </summary>
        public static void SyncTopmost(NativeWindow window)
        {
            SetTopmost(window, IsFullscreen && !PauseMenu.Open && window.IsFocused);
        }

        /// <summary>"borderless"/"fullscreen"/"windowed" from a settings file or a flag.</summary>
        public static WindowStartMode Parse(string? value, WindowStartMode fallback)
        {
            if (value == null)
            {
                return fallback;
            }
            string text = value.Trim().ToLowerInvariant();
            if (text is "borderless" or "fullscreen" or "borderless fullscreen" or "1" or "true")
            {
                return WindowStartMode.BorderlessFullscreen;
            }
            if (text is "windowed" or "window" or "0" or "false")
            {
                return WindowStartMode.Windowed;
            }
            return fallback;
        }
    }
}
