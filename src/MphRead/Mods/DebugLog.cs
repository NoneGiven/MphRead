using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MphRead.Mods.Launcher;
using MphRead.Mods.Update;

namespace MphRead.Mods
{
    /// <summary>
    /// Everything the program can say about itself, in a file, when the player
    /// asks for it.
    ///
    /// This exists for one kind of report: "it crashes when the map loads" --
    /// on a machine nobody here can plug in, sent by somebody who has no
    /// console window to copy anything out of (the Windows build is a GUI
    /// binary and deliberately opens none). Without a file there is nothing to
    /// ask for except a description.
    ///
    /// Two halves, and the first is most of the value for none of the work:
    /// <see cref="Console.Out"/> is *teed* into the file, so every line the
    /// program already prints -- the net session's, the launcher's, the
    /// renderer's one-line summary of what the options came out as -- is
    /// captured without a single call site being added. The second half is the
    /// handful of places that say something a log needs and a terminal does
    /// not: the machine, the build, the room being loaded and how far it got,
    /// what the driver calls itself, and the stack of anything that killed the
    /// process.
    ///
    /// Off by default and never on by accident. It costs a file handle, a lock
    /// per line and a directory that grows, which is not something to hand
    /// somebody who did not ask -- so it is one switch in the corner of the
    /// launcher, and it stays where they left it.
    /// </summary>
    public static class DebugLog
    {
        private static StreamWriter? _writer;
        private static readonly object _lock = new();
        private static bool _hooked;
        private static bool _forced;
        private static TextWriter? _consoleWas;

        /// <summary>Whether lines are going anywhere.</summary>
        public static bool Active => _writer != null;

        /// <summary>Where the file ended up, for the launcher to show.</summary>
        public static string? Path { get; private set; }

        /// <summary>How many logs are kept before the oldest is deleted.</summary>
        private const int KeepFiles = 8;

        /// <summary>
        /// Turn it on for this run whatever the setting says. The
        /// <c>-debuglog</c> flag, which is how somebody who cannot reach the
        /// launcher -- because the launcher is what is crashing -- still gets
        /// a file.
        /// </summary>
        public static void Force() => _forced = true;

        /// <summary>
        /// Start logging if it has been asked for. Safe to call as often as
        /// anybody likes: the second call does nothing.
        /// </summary>
        public static void Attach()
        {
            if (_writer != null || (!_forced && !LauncherPrefs.DebugLogs))
            {
                return;
            }
            try
            {
                string directory = System.IO.Path.Combine(LauncherPrefs.Directory, "logs");
                System.IO.Directory.CreateDirectory(directory);
                Prune(directory);
                string name = $"{Branding.Name.Replace(" ", "")}-"
                    + $"{DateTime.Now:yyyyMMdd-HHmmss}.log";
                Path = System.IO.Path.Combine(directory, name);
                // Shared, so the file can be read while the game is still
                // running -- which is the only way to read the tail of one
                // that is about to crash.
                var stream = new FileStream(Path, FileMode.Create, FileAccess.Write,
                    FileShare.ReadWrite);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                // A log that cannot be opened must not be the reason a session
                // does not start.
                _writer = null;
                Console.WriteLine($"[debug] could not open a log: {ex.Message}");
                return;
            }
            Hook();
            WriteHeader();
        }

        /// <summary>
        /// Stop, and put the console back the way it was. Called when the
        /// player turns the switch off.
        /// </summary>
        public static void Detach()
        {
            lock (_lock)
            {
                if (_consoleWas != null)
                {
                    Console.SetOut(_consoleWas);
                    _consoleWas = null;
                }
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
                _hooked = false;
            }
        }

        private static void Prune(string directory)
        {
            try
            {
                var files = new List<FileInfo>(new DirectoryInfo(directory).GetFiles("*.log"));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = KeepFiles - 1; i < files.Count; i++)
                {
                    files[i].Delete();
                }
            }
            catch (Exception)
            {
                // A directory that cannot be tidied is still a directory that
                // can be written to.
            }
        }

        private static void Hook()
        {
            if (_hooked)
            {
                return;
            }
            _hooked = true;
            // The half that costs nothing: everything already printed is
            // written to the file as well, in the order it was printed.
            _consoleWas = Console.Out;
            Console.SetOut(new TeeWriter(_consoleWas));
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Line("crash", "the process is going down with an exception "
                    + $"(terminating={e.IsTerminating})");
                Exception("crash", e.ExceptionObject as Exception);
                lock (_lock)
                {
                    _writer?.Flush();
                }
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Exception("task", e.Exception);
                e.SetObserved();
            };
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                Line("exit", "process exiting");
                lock (_lock)
                {
                    _writer?.Flush();
                }
            };
        }

        private static void WriteHeader()
        {
            Line("build", $"{Branding.Name} {BuildVersion.Display}, "
                + $"data format {Program.Version}");
            Line("build", $"protocol {Network.NetConfig.ProtocolVersion}, "
                + $"log started {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Line("system", $"{Environment.OSVersion} {RuntimeArchitecture()}, "
                + $".NET {Environment.Version}, {Environment.ProcessorCount} cpu(s)");
            Line("system", $"64-bit process={Environment.Is64BitProcess}, "
                + $"culture={CultureInfo.CurrentCulture.Name}");
            Line("paths", $"base={AppContext.BaseDirectory}");
            Line("paths", $"prefs={LauncherPrefs.Directory}");
            Line("paths", $"log={Path}");
            try
            {
                Line("paths", $"game files ready={GameFiles.Ready}");
            }
            catch (Exception ex)
            {
                Line("paths", $"game files could not be checked: {ex.Message}");
            }
            Line("args", String.Join(' ', Environment.GetCommandLineArgs()));
            WriteDisplay();
            Line("render", $"cel={RenderOptions.OnOff(RenderOptions.CelShading)} "
                + $"fog={RenderOptions.OnOff(RenderOptions.Fog)} "
                + $"window={LauncherPrefs.WindowMode}");
            if (Network.NetLag.Active)
            {
                Line("net", $"simulated line: {Network.NetLag.Describe()}");
            }
        }

        /// <summary>
        /// What kind of desktop this is, on the platforms where that is a
        /// question.
        ///
        /// Every graphical failure reported from Linux so far has come down to
        /// which display server is running and which client libraries the
        /// binary could find: the launcher falls back to text when Avalonia
        /// cannot bind them, and the game window is created by GLFW, which
        /// binds a different set and dies in native code rather than throwing
        /// when they are wrong. Neither says so, and both are answered by
        /// these four values -- which nobody thinks to include in a report and
        /// which cost nothing to record.
        /// </summary>
        private static void WriteDisplay()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                return;
            }
            static string Value(string name)
            {
                string? value = Environment.GetEnvironmentVariable(name);
                return String.IsNullOrEmpty(value) ? "(unset)" : value;
            }
            Line("display", $"session={Value("XDG_SESSION_TYPE")} "
                + $"desktop={Value("XDG_CURRENT_DESKTOP")}");
            Line("display", $"DISPLAY={Value("DISPLAY")} "
                + $"WAYLAND_DISPLAY={Value("WAYLAND_DISPLAY")}");
        }

        private static string RuntimeArchitecture()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
                + "/" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
        }

        /// <summary>One line, with a category in front of it. Cheap when off.</summary>
        public static void Line(string category, string message)
        {
            if (_writer == null)
            {
                return;
            }
            lock (_lock)
            {
                _writer?.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}");
            }
        }

        /// <summary>An exception and everything under it, indented.</summary>
        public static void Exception(string category, Exception? ex)
        {
            if (_writer == null || ex == null)
            {
                return;
            }
            Line(category, $"{ex.GetType().FullName}: {ex.Message}");
            lock (_lock)
            {
                _writer?.WriteLine(ex.StackTrace);
            }
            if (ex.InnerException != null)
            {
                Line(category, "caused by:");
                Exception(category, ex.InnerException);
            }
        }

        /// <summary>
        /// A step of something that can fail half way, with how long it took.
        /// Used around room loading, which is where the report that this was
        /// written for says the crash happens.
        /// </summary>
        public static IDisposable? Step(string category, string what)
        {
            return _writer == null ? null : new Timed(category, what);
        }

        private sealed class Timed : IDisposable
        {
            private readonly string _category;
            private readonly string _what;
            private readonly System.Diagnostics.Stopwatch _clock
                = System.Diagnostics.Stopwatch.StartNew();

            public Timed(string category, string what)
            {
                _category = category;
                _what = what;
                Line(category, $"{what}: started");
            }

            public void Dispose()
            {
                Line(_category, $"{_what}: done in {_clock.ElapsedMilliseconds} ms");
            }
        }

        /// <summary>
        /// The console, and the file, in that order.
        ///
        /// Writing to the console first means a line reaches the terminal
        /// whether or not the file is still there to take it, and the lock is
        /// the same one every other writer takes -- the log is written from the
        /// game thread, the net thread and the launcher's dispatcher, and
        /// interleaved half-lines would be worse than no log at all.
        /// </summary>
        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _console;

            public TeeWriter(TextWriter console)
            {
                _console = console;
            }

            public override Encoding Encoding => _console.Encoding;

            public override void Write(char value)
            {
                _console.Write(value);
                lock (_lock)
                {
                    _writer?.Write(value);
                }
            }

            public override void Write(string? value)
            {
                _console.Write(value);
                lock (_lock)
                {
                    _writer?.Write(value);
                }
            }

            public override void WriteLine(string? value)
            {
                _console.WriteLine(value);
                lock (_lock)
                {
                    _writer?.WriteLine(value);
                }
            }

            public override void Flush()
            {
                _console.Flush();
                lock (_lock)
                {
                    _writer?.Flush();
                }
            }
        }
    }
}
