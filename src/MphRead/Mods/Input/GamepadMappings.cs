using System;
using System.IO;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// Extra controller mappings, for the pads GLFW's own database has never
    /// heard of.
    ///
    /// GLFW carries a snapshot of SDL's controller database, frozen at
    /// whichever version of GLFW the OpenTK redist happens to ship. Everything
    /// released since then -- and every pad that was always too obscure to be
    /// in it -- answers <c>glfwJoystickIsGamepad</c> with false, and a pad that
    /// answers false is one <see cref="GamepadDesktop"/> used to ignore
    /// outright. That is the whole of "my controller does nothing": the device
    /// is plugged in, the operating system sees it, and the game never asks it
    /// anything.
    ///
    /// Two answers, and this file is the first of them. A mapping line is
    /// eleven words of text that says which axis and which button is which,
    /// and SDL's format for it is the one every project shares: a player who
    /// already has a line for their pad -- from Steam, from another game, from
    /// the community database -- can hand it over here rather than wait for a
    /// build. The second answer is <see cref="GamepadDesktop"/>'s raw fallback,
    /// which plays an unmapped pad on a guess; this one plays it correctly.
    ///
    /// Three sources, in the order they are read, later ones winning:
    ///
    /// <list type="bullet">
    /// <item><c>gamecontrollerdb.txt</c> beside the executable -- what a
    /// release would ship if one were ever bundled, and where a player who
    /// unzipped the game will naturally drop a file.</item>
    /// <item><c>gamecontrollerdb.txt</c> in the settings directory, beside
    /// <c>controls.txt</c> -- the one that survives reinstalling, and on
    /// Android the only one the app can read at all.</item>
    /// <item><c>SDL_GAMECONTROLLERCONFIG</c>, SDL's own environment variable,
    /// because somebody who has already made their pad work in another game
    /// most likely did it there.</item>
    /// </list>
    ///
    /// Nothing here fails loudly: a missing file is the normal case, and a
    /// malformed line is GLFW's to reject. What it does do is say what it
    /// found, because a mapping file that is being read and a mapping file
    /// that is being ignored look identical from a match.
    /// </summary>
    internal static class GamepadMappings
    {
        public const string FileName = "gamecontrollerdb.txt";

        private static bool _loaded;

        /// <summary>What was read, for <c>-gamepad</c> to print.</summary>
        public static string Summary { get; private set; } = "no extra mappings loaded";

        /// <summary>
        /// Hand GLFW whatever mappings this machine has, once.
        ///
        /// Called from the first poll rather than from startup: GLFW has to be
        /// initialised before it will take a mapping, and which of the window,
        /// the probe or a settings row got there first is not something this
        /// file should have to know.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded || OperatingSystem.IsAndroid())
            {
                return;
            }
            _loaded = true;
            int files = 0;
            int lines = 0;
            foreach (string path in Paths())
            {
                string? text = TryRead(path);
                if (text == null)
                {
                    continue;
                }
                if (Apply(text))
                {
                    files++;
                    lines += Count(text);
                    Console.WriteLine($"[input] gamepad mappings: {Count(text)} from {path}");
                }
            }
            string? config = Environment.GetEnvironmentVariable("SDL_GAMECONTROLLERCONFIG");
            if (!String.IsNullOrWhiteSpace(config) && Apply(config))
            {
                files++;
                lines += Count(config);
                Console.WriteLine($"[input] gamepad mappings: {Count(config)} from "
                    + "SDL_GAMECONTROLLERCONFIG");
            }
            Summary = files == 0
                ? "no extra mappings loaded"
                : $"{lines} extra mapping(s) from {files} source(s)";
        }

        /// <summary>
        /// Where a mapping file may sit. The settings directory is second so
        /// that it wins: it is the copy a player edited, and the one beside
        /// the executable is whatever the download came with.
        /// </summary>
        private static string[] Paths()
        {
            string beside = Path.Combine(AppContext.BaseDirectory, FileName);
            string settings = Path.Combine(Launcher.LauncherPrefs.Directory, FileName);
            return beside == settings
                ? new string[] { beside }
                : new string[] { beside, settings };
        }

        private static string? TryRead(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // A file that cannot be read is the same as no file. Nothing a
                // player does about their controller should be able to stop
                // the game starting.
                return null;
            }
        }

        private static bool Apply(string text)
        {
            try
            {
                // The whole file at once: glfwUpdateGamepadMappings takes a
                // string of newline-separated lines and skips comments itself,
                // so there is nothing to parse here. It returns false only if
                // it could not parse *any* of it.
                return GLFW.UpdateGamepadMappings(text);
            }
            catch (Exception ex) when (ex is DllNotFoundException
                || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
                return false;
            }
        }

        /// <summary>Mapping lines in a file: not blank, not a comment.</summary>
        private static int Count(string text)
        {
            int count = 0;
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// A mapping line for a pad nothing knows, built from the layout
        /// <see cref="GamepadDesktop"/> is guessing at.
        ///
        /// For <c>-gamepad</c> to print, so that somebody whose pad works only
        /// approximately has something to correct and paste into
        /// <c>gamecontrollerdb.txt</c> -- and something to send here. The GUID
        /// is the part nobody can look up for themselves; the rest is the
        /// guess written out in SDL's own words, which is a far better thing
        /// to hand a person than "unsupported".
        /// </summary>
        public static string Suggest(int slot)
        {
            string guid = GLFW.GetJoystickGUID(slot) ?? "00000000000000000000000000000000";
            string name = (GLFW.GetJoystickName(slot) ?? "gamepad").Replace(',', ' ');
            GamepadLayout layout = GamepadLayout.For(slot);
            var text = new System.Text.StringBuilder();
            text.Append(guid).Append(',').Append(name).Append(',');
            text.Append($"a:b{layout.ButtonA},b:b{layout.ButtonB},");
            text.Append($"x:b{layout.ButtonX},y:b{layout.ButtonY},");
            text.Append($"leftshoulder:b{layout.ButtonLeftBumper},");
            text.Append($"rightshoulder:b{layout.ButtonRightBumper},");
            text.Append($"back:b{layout.ButtonBack},start:b{layout.ButtonStart},");
            text.Append($"leftstick:b{layout.ButtonLeftThumb},");
            text.Append($"rightstick:b{layout.ButtonRightThumb},");
            text.Append($"leftx:a{layout.AxisLeftX},lefty:a{layout.AxisLeftY},");
            text.Append($"rightx:a{layout.AxisRightX},righty:a{layout.AxisRightY},");
            text.Append(layout.AxisLeftTrigger >= 0
                ? $"lefttrigger:a{layout.AxisLeftTrigger},"
                : $"lefttrigger:b{layout.ButtonLeftTrigger},");
            text.Append(layout.AxisRightTrigger >= 0
                ? $"righttrigger:a{layout.AxisRightTrigger},"
                : $"righttrigger:b{layout.ButtonRightTrigger},");
            text.Append("dpup:h0.1,dpright:h0.2,dpdown:h0.4,dpleft:h0.8,");
            text.Append("platform:").Append(Platform()).Append(',');
            return text.ToString();
        }

        /// <summary>
        /// The word SDL puts in a mapping's <c>platform:</c> field. A line
        /// carrying the wrong one is skipped in silence, which is a confusing
        /// thing to hand somebody as a fix.
        /// </summary>
        public static string Platform()
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }
            if (OperatingSystem.IsMacOS())
            {
                return "Mac OS X";
            }
            if (OperatingSystem.IsAndroid())
            {
                return "Android";
            }
            return "Linux";
        }
    }
}
