# Debugging logs

One switch, in the bottom right corner of the launcher's front card, under the
version. Off. Switched on, the program writes everything it can say about
itself to a file.

It exists for one kind of report, and the report is the design: *"it crashes
when the map loads"*, from a machine nobody here can plug in, sent by somebody
with no console window to copy anything out of. The Windows build is a GUI
binary and deliberately opens no console (`Mods/ConsoleWindow.cs`), so
`GuiLauncher`'s own `catch` -- which prints the exception and returns -- was
printing into nothing: from the player's side the game simply disappears while
a map is loading. The log is the only thing that can be read afterwards.

## Where it lives

| Path | What |
|---|---|
| `Mods/DebugLog.cs` | the whole of it: the file, the console tee, the hooks |
| `Mods/LogShare.cs` | `LogArchive`, which zips them, and the `ILogShare` seam |
| `MphRead.Android/AndroidLogShare.cs` | the only implementation of that seam |
| `Mods/Launcher/Gui/HomeView.cs` | `BuildDebugSwitch`, the corner row |
| `Mods/Launcher/Portable/LauncherPrefs.cs` | `debug_logs` in `launcher.txt` |
| `Mods/ModEntry.cs` | `DebugLog.Attach()`, before anything else runs |
| `logs/FruityPrime-<yyyyMMdd-HHmmss>.log` | the file, beside the executable |

On Android the file goes to the app's data directory, because
`LauncherPrefs.Directory` is pointed there by the head before anything reads --
a package's own directory is read-only. The switch is the same control on the
same screen: the launcher is one Avalonia view on every platform.

## Getting the file off the machine

**Share logs**, immediately left of the switch, same size and same grey, and
**only there when logs exist**. It zips every log `DebugLog` is keeping and
hands the zip to whatever the player picks.

It is on the phone that this is not a convenience. The logs sit in the app's
own external files directory, which since Android 11 no file manager will
browse: without this there is a file somebody has been asked for and no way
for them to reach it. On a desktop the tooltip already names the path and a
file manager can open it, so `LogShare.Current` is left null there and the
button simply does not appear.

Three things worth knowing before touching it:

- **The live log is in the zip, and that is the point** -- the interesting run
  is usually the one still going. `DebugLog` opens it `FileShare.ReadWrite`
  and flushes every line, so it can be read while it is still being written,
  but only by a reader that *also* says ReadWrite. That is why `LogArchive`
  copies the bytes by hand instead of using `CreateEntryFromFile`, which asks
  for `FileShare.Read` and would be refused the open.
- **A `FileProvider` is not optional.** Handing another app a `file://` URI
  has thrown `FileUriExposedException` since Android 7. The provider is
  declared in `Properties/AndroidManifest.xml` with authority
  `${applicationId}.logs` -- the placeholder *is* substituted, verified in the
  built APK as `fr.livetek.fruityprime.logs`, which is what
  `PackageName + ".logs"` produces at runtime. `@xml/file_paths` exposes one
  directory, the cache folder the zip is built in, and not the logs directory
  itself.
- **A bad provider config crashes the app at launch**, not at the share:
  Android instantiates providers when the process starts. So a change here is
  checked by starting the app, not by pressing the button.
- **`--` inside an XML comment is not valid XML** and aapt2 rejects the
  resource with `APT2258: not well-formed`. The house comment style uses it
  freely in C#; `Resources/xml/*.xml` cannot.

## What is in it

Most of the value costs nothing: **`Console.Out` is teed into the file**, so
every line the program already prints is captured with no call site added --
`[net]` joining and slot assignment, `[render]`'s summary of what the options
came out as, the launcher's own failures, the exception `GuiLauncher` prints
where nobody can see it.

On top of that, the things a log needs and a terminal does not:

- the build, the data format, the protocol version, the machine, the .NET
  version, the architecture and the command line;
- `GL_VENDOR`, `GL_RENDERER`, `GL_VERSION` and the shading language version,
  read once in `Renderer.OnLoad` where a context is certainly current;
- **every model actually read off disk**, by name -- a map is a hundred of
  these, and the last line before a crash is the file it died on;
- the room being loaded, and how long each load took;
- the stack of anything that kills the process
  (`AppDomain.UnhandledException`), of an unobserved task, and of the
  match-start failure the launcher catches.

Eight files are kept; the oldest is deleted on the way in. The file is opened
`FileShare.ReadWrite`, so it can be read while the game is still running --
which is the only way to read the tail of one that is about to crash.

## Turning it on without the launcher

`FruityPrime -debuglog` forces it for one run, for the case where the launcher
is what will not start. `DebugLog.Attach()` is called from
`ModEntry.TryHandleHeadless`, which runs for **every** invocation -- the game,
the server, the harness -- so a command line path that never opens a launcher
still writes one.

Switching it on in the launcher starts the file immediately rather than at the
next start: being asked to restart first is where a report like this is
usually lost.

## Traps

- **It is not free.** A lock on every line the program prints, a file handle,
  and a directory that grows. That is why it is a switch rather than something
  on by default, and why the control is the smallest thing on the screen.
- **`Console.SetOut` is process-wide.** Turning the switch off puts the
  original writer back; anything that captured `Console.Out` in between keeps
  the tee. Nothing in this build does.
- **It is not the net log.** `netlog-<name>.txt` (`Mods/Network/NetLog.cs`) is
  written for every client session whether or not this is on, and holds the
  per-slot roster dumps that compare two machines' view of the same frame.
  Both are useful; they answer different questions.
