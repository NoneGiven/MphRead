# Fruity Prime — tools, design, and the mechanics catalogue

**The project is Fruity Prime. The code is still `namespace MphRead`, and stays
that way.** Upstream is NoneGiven/MphRead and every pull from it is a
fast-forward only while the 221 files that declare that namespace and the 271
that import it are untouched; renaming it would put a conflict in all of them
for a string only a developer ever reads. The rename is the product, the
binaries, the window title and the release artifacts. `Mods/Branding.cs` is
where the name lives — nothing else should spell it out.

| Build | Binary |
|---|---|
| Windows game | `FruityPrime.exe` |
| Windows server | `FruityPrimeServer.exe` |
| Linux game, Linux and ARM64 server | `FruityPrime` |

This file exists so a fresh session can pick the work up without rediscovering
the environment or the failure modes. Everything below has been used; nothing
is aspirational. It stays short on purpose: depth for a given area lives in
`.claude/` (indexed in `.claude/CLAUDE-INDEX.md`) and is loaded only when that
area is the one being touched.

## Where things are

| Path | What |
|---|---|
| `~/GIT/Fruity-Prime` | the source. Upstream is NoneGiven/MphRead; everything added lives under `src/MphRead/Mods/` so pulling upstream stays a fast-forward. (It was `~/MphRead-dev` before the rename, and that path is gone) |
| `src/MphRead.Android/` | the Android head: the same sources, an APK, a front screen and a match, over GL ES and touch controls |
| `src/MphRead/Mods/Network/` | the whole multiplayer feature |
| `src/MphRead/Mods/Launcher/` | the launcher: `Gui/` is every window (Avalonia, all platforms), `Portable/` is the logic and the text screen |
| `~/mph-test/` | the extracted game files and `paths.txt`. **`paths.txt` has to sit next to the DLL** you are running, so copy it into `src/MphRead/bin/Release/net9.0/` and run `dotnet FruityPrime.dll` from there |
| `~/mph-net-test/` | the test rig -- `bin/`, `run-check.sh`, `compare-reports.py`, `hard/`, and every `run-*.sh` named in the table below. **It does not exist on this box** and every command that names it has to be rebuilt before it can be run; the game files in `~/mph-test/` are what survived. A two-client run against a real server needs nothing more than two `-netcheck` processes and the game files |
| `C:\Users\livetek\Desktop\MPH\MphRead-develop\` | the Windows deliverable |
| `net.livetek.fr:27888` | the dedicated server on the user's Pi (systemd unit `mphread-server`) |

## Environment recipe (WSL)

Three things will waste an hour each if you do not know them:

```bash
export PATH="$HOME/.dotnet:$PATH"          # dotnet is not on PATH
export DOTNET_ROOT="$HOME/.dotnet"         # else the apphost cannot find a runtime
export MESA_GL_VERSION_OVERRIDE=4.5COMPAT  # else Mesa hands out a Core profile
export ALSOFT_DRIVERS=null PULSE_SERVER=   # else ALSA retries stall frames
```

- **`DOTNET_ROOT` is what the built `./FruityPrime` needs, and `PATH` is not.**
  The apphost looks for `libhostfxr.so` under `DOTNET_ROOT` or a system install,
  neither of which exists here, so running the binary directly dies with *"You
  must install .NET to run this application"* while `dotnet FruityPrime.dll`
  from the same directory works. Either export it or run through `dotnet`.

- If `~/.dotnet` is empty, the SDK is not installed at all:
  `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0`
  puts it there.
- The Avalonia launcher needs `libICE` and `libSM`, which the game itself does
  not and a minimal WSL install does not have. Without them it falls back to the
  text launcher rather than failing, so a window that never appears is this and
  not a bug in the screen. `sudo apt install libice6 libsm6`.
- `dotnet` aborting on startup with *"Couldn't find a valid ICU package"* is a
  missing `libicu`, not a broken SDK. `sudo apt install libicu-dev` is the fix;
  `export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` gets a build out of a box
  with no root, at the cost of culture-aware string handling — fine for
  building and for the server checks, not something to leave set while
  testing anything that formats text for a player.
- `dotnet publish -o DIR` does **not** copy `libSkiaSharp.so` or
  `libHarfBuzzSharp.so` next to the binary, and without them every Avalonia
  screen dies with *"The type initializer for 'SkiaSharp.SKImageInfo' threw an
  exception"* -- which `-uishot` reports as "no Avalonia backend on this
  machine", so it reads as a missing display rather than a missing file. Copy
  them out of `~/.nuget/packages/{skiasharp,harfbuzzsharp}.nativeassets.linux/*/runtimes/linux-x64/native/`.
- **`MESA_GL_VERSION_OVERRIDE=4.5COMPAT` is not optional.** Without it Mesa gives
  a Core profile despite the Compatability request, every `GL.Begin` fails
  silently with `InvalidOperation`, and every frame renders black. Nothing in
  any log says so.
- A window created with `StartVisible = false` has no usable back buffer under
  Mesa. Screenshots must read the scene's offscreen target
  (`Scene.ReadSceneTarget`, used by `Mods/ScreenCapture.cs`), which carries the
  world but not the HUD.
- `paths.txt` must sit **next to the DLL**, not in the working directory:
  `ConsoleSetup.Run` does `Directory.SetCurrentDirectory(BaseDirectory)`.
- Audio failures used to kill the process from a static constructor. That is now
  non-fatal, but under WSL the audio device is flaky enough that the test rig
  disables it outright.
- Do not `cp` over a DLL while a process is using it: .NET memory-maps it and
  the process dies with an opaque crash. Stop the server first, or write to a
  new name and `mv`.

## Commands

| Command | Use |
|---|---|
| `MphRead -server ... -noshadowfreeze` | run the room with the Judicator's ice wave as a cone rather than as a column of infinite height. A rule, broadcast to every client in the match state, because the machine resolving a shot decides who it hit |
| `MphRead -server -port N -players 8` | dedicated relay server; needs no game files. `-servername "NAME"` is what a browser shows; it announces itself to `net.livetek.fr` unless `-nomaster` is passed, and `-master HOST -masterport N` points it elsewhere |
| `MphRead -server ... -simulate` | the same server, simulating the match itself instead of pointing the authority at the first client to connect. It is then the only machine that resolves a shot, so nobody plays at zero latency and the match survives any player leaving. Needs game files on that machine -- without them it says so and relays as before. `.claude/multiplayer/NETWORK-SERVERAUTH.md` |
| `MphRead -simcheck "ROOM" [-players N] [-seconds N]` | what a room costs a server: peak memory, milliseconds a simulation step, and whether every slot spawned. Runs the headless engine with nobody connected. The measurement that decides whether a given box can be the authority for a given map |
| `MphReadServer.exe -server ...` | the same server on Windows, as its own console binary. `MphRead.exe` can also do it, but it is a GUI binary: a shell will not wait for it and its exit code never reaches `%ERRORLEVEL%`. Run with no arguments it prints what it is for |
| `MphRead -masterserver [-port N] [-public HOST] [-hostports A-B]` | the server directory the launcher's browser asks, and the machine that runs matches for players who cannot open a port. Same binary, no game files, keeps nothing on disk. `-public` is the address to publish for servers registering from this same machine, whose heartbeats arrive over the loopback |
| `MphRead -hostgame "ROOM" [-mode M] [-master HOST]` | ask the directory to run a match and join it. No port forwarding anywhere; the only way to host from a machine with no launcher |
| `MphRead -servers [-master HOST] [-masterport N]` | print the server list the launcher's browser would show, with each server's map, players and round trip |
| `MphRead -connect HOST -port N -name X -hunter H` | join from the command line, no launcher |
| `MphRead -netcheck HOST -port N -name X -hunter H -seconds N [-shots DIR] [-size WxH]` | a real client driven by a script, which reports what it saw. Exit code 0 = pass. `-spectate [SEC]` makes it stop playing and watch, `-rejoin SEC` puts it back in -- the one player state the tour cannot reach on its own |
| `MphRead -netlag MS[:JITTER]` / `-netloss PCT` | play, or run any check, over a line this client makes up: `-netlag 200` adds 200 ms to the round trip (half each way), `-netlag 200:40` gives it jitter, `-netloss 5` eats one datagram in twenty. Works against the real server, on any platform, with no proxy and no `sudo` -- and unlike `hard/run-latency.sh`'s netem it can be given to **one** client while the others stay fast, which is the case a player with a bad line actually is. Every report says so when it is on |
| `MphRead -nounlagged` | resolve shots against the present, the way every build before lag compensation did. The control for measuring it; on by default. `.claude/multiplayer/NETWORK-UNLAGGED.md` |
| `MphRead -nohitprediction` / `-nohitmarker` / `-deathprediction` | wait for the authority before a hit lands, the way every build before instant hit registration did; drop the mark over the crosshair that says one has; and let a prediction kill **somebody else**, which it does not by default -- a predicted hit is clamped to leave the victim standing on one point of health and the dying waits for the authority. Hit prediction and the mark are on by default, predicted kills on other players are **off** (`-nodeathprediction` is still accepted and is what the default already does), and a **self**-kill is predicted whatever any of them say. `.claude/multiplayer/NETWORK-PREDICTION.md` |
| `MphRead -debuglog` | write the file the launcher's corner switch writes, for one run. `.claude/DEBUG-LOGS.md` |
| `~/mph-net-test/probe-chat.py [HOST] [PORT]` | what the server does with chat, asked the way no real client can: a spoofed sender, and a flood. `.claude/multiplayer/NETWORK-CHAT.md` |
| `~/mph-net-test/run-remote.sh HOST PORT SECONDS hunter...` | the same check against a server that is not on this machine -- which is the one that matters, since eight clients on one box measure the box |
| `~/mph-net-test/run-demo.sh SEC [authority\|client]` | record a demo from a scripted client and print what landed in the file. The authority is the case that matters: it is whichever client joined first, so it is normally whoever set the match up, and the server sends it no snapshots at all |
| `~/mph-net-test/run-rejoin.sh SEC LEAVE REJOIN [host] [port]` | the rejoin scenario, with a control: A hosts and leaves, the authority moves, then one client takes the vacated slot and another takes a fresh one. Prints what each took. `.claude/multiplayer/NETWORK-DIAGNOSTICS.md` |
| `~/mph-net-test/hard/run-all.sh` / `run-all2.sh` | the hard-case batch against the Pi: a ninth player, a line that goes away, 100-300 ms, packet loss, everybody spectating, everybody recording, a match boundary, an authority leaving, and a ramp to twenty-odd matches at once. `.claude/testing/TEST-HARD-CASES.md` |
| `~/mph-net-test/run-lag.sh MS SECONDS hunter...` | the same check against a loopback server behind `udp-lag.py`, which holds every datagram for `MS` before passing it on. A latency bug reproduced at a number you chose, rather than at whatever the internet is doing -- and the Pi answers in 7-17 ms, so it is the *worse* instrument for one |
| `MphRead -maptest "ROOM" -players 8 -seconds 22` | load one room with a full house, drive every player, and report what the map holds and whether it survived |
| `MphRead -maptest "ROOM" -players 8 -bots` | the same, but AI bots instead of the scripted tour -- a different code path, the only one that finds what only `PlayerAi` touches |
| `MphRead -maptest "ROOM" -hunter H -hudshots` | put that hunter in slot 0, whose eyes and whose HUD every capture is taken through. Each of the eight lays its readouts out differently, so a HUD picture with no hunter named is a picture of Samus's and of nobody else's |
| `MphRead -maptest "ROOM" -renderprobe` | stand on every spawn point in the room in turn, read the frame, walk forward five seconds, read the worst. Catches a room that draws nothing -- the failure no other check can see, because everything else about it passes. `-shots DIR` writes the PNGs, `-allnodes` draws without room-part culling (which separates "the geometry is missing" from "the cull lost it"), `-hudshots` uses a real visible window and reads *its* buffer, which is the only capture that includes the HUD, and `-size WxH` sets that window's shape -- the HUD is laid out in a 4:3 space and stretched, so how it looks is partly a question about the window. Under WSL a HUD capture needs the X11 backend: `WAYLAND_DISPLAY=` `DISPLAY=:0`, or every window read comes back black |
| `MphRead -maptest "TEST ARENA" -players 8` | the harness's own room (`maps/arena/`): forty units square, eight spawns on a ring looking inward, nothing far from anything. Where damage, hit registration and the affliction states are actually measurable -- a real map's corridors mean most of the tour's shots land on a wall |
| `MphRead -rooms` | list every multiplayer room, one per line, for a shell loop. **27** is the whole cartridge and the right answer with no custom map source present; anything more is a custom map |
| `MphRead -q3convert FILE.pk3 -map LEVEL -name ROOM [-noclip]` | a Quake 3 .pk3 to a custom map in one command: textures baked from the level's own art, scale and extents picked from its geometry, spawns from its entities. Places no weapons or powerups -- where those go decides how the map plays. `.claude/mapgen/MAP-PIPELINE.md` |
| `MphRead -mapgen ["NAME"]` | generate the room binaries for the custom maps in `maps/` (recursively: a map may sit in a folder of its own with its level and textures beside it, or be a single `.fpmap` bundle), from the player's own textures. `-mapmaterials "ROOM"` prints what textures a room can lend. A map is a JSON file; the `.bin` it produces is never committed. `.claude/mapgen/MAP-PIPELINE.md` |
| `MphRead -mapbundle ["NAME"] [-mapdir DIR]` | cook a map into the one file it ships and is handed out as: recipe, level and baked textures in a `.fpmap`, with the level trimmed to the lumps the importer reads (376 KB for de_dust2, against 2.8 MB for the folder). What the workflow runs before it publishes -- the bundle is not committed, and the `.pk3` it is cooked from never reaches a package. `-mapdir` is resolved against the directory the command was typed in |
| `MphRead -gamepad [-seconds N]` | what a connected pad is doing, with no match in the way: its name, its axes, and which game action each button reaches. The only thing that tells "not connected" from "connected but GLFW has no mapping for it" from "the dead zone is eating it" apart. `.claude/GAMEPAD.md` |
| `MphRead -cel on\|off [-celbands N] [-celedge N]` / `-fog on\|off` / `-prohud on\|off` | render options for every path that never opens a launcher, which is every screenshot command. `.claude/render/CEL-SHADING.md` |
| `MphRead -fpscap N\|display` | how fast the picture is drawn. The simulation is pinned at 60 Hz on every setting, so this does not touch what the game does. `.claude/render/FRAME-PACING.md` |
| `MphRead -crosshair STYLE` / `-crosshairsize Small\|Medium\|Big` | which crosshair the pro HUD draws, for the screenshot commands that open no launcher. Styles are Cross, Dot, CrossDot, Circle, Brackets |
| `MphRead -weaponstyle static\|dynamic` | where the gun and the crosshair sit -- Quake's welded pair or the DS game's drifting one, which is the settings screen's Weapon row. For the same paths, and a sharper reason: the two answers differ mainly in what the middle of the picture is doing, so a screenshot is how the difference is checked at all. `quake` and `metroid` are accepted as the same two answers |
| `MphRead -frametimingcheck` | the fixed-step accumulator on its own, against frame times chosen rather than measured: does the game still run at 60.000 Hz when the screen runs at 144, at 165, at a jitter, or at 40. Needs no game files and no display |
| `MphRead -maptest "ROOM" -drawrate N` | draw each simulation step N times, which is what a 144 Hz screen does to a 60 Hz game. Asserts that drawing did not advance the world. How the decoupled loop is checked from a box with no monitor |
| `MphRead -uishot DIR` | pictures of the launcher's own screens -- home, settings, the map picker, the pause menu -- rendered without anyone looking at a display. The one part of the program that could not otherwise be checked from a headless box |
| `MphRead -demoinfo FILE [-replay]` | what a recorded match contains -- records, frames, a packet-type histogram, and how well it compressed. `-replay` then runs the file through the real player with no room or window and reports how the packets landed per frame, which is the measurement "the replay stutters" is about. Needs no game files. `.claude/multiplayer/NETWORK-DEMOS.md` |
| `MphRead -netcheck ... -recorddemo` | the harness client, recording a demo as it plays |
| `MphRead -mechanics` | print the catalogue in `MECHANICS.md`, generated from the game's own tables |
| `MphRead` (no arguments, Windows or macOS) | the front screen. The Windows build is a GUI binary, so double-clicking it opens the launcher with no terminal behind it |
| `MphRead -menu` | the console menu, for people who typed something |
| `MphRead -launcher [-console]` | the front screen explicitly; `-console` also gives it a terminal. The same Avalonia screen on Windows, Linux and macOS, or the text one when there is no display. A bare `MphRead` on Linux still opens upstream's `-menu` prompts, unchanged |
| `FruityPrime -launcher -text` | the text front screen on a machine that has a display. What an SSH session gets anyway |
| `FruityPrime -update` | check GitHub for a newer release and open its page. Installs nothing; the one command that answers "am I on the latest build" |
| `FruityPrime -noupdate` | do none of that, on any command that would have |
| `FruityPrime -server ... -noautoupdate` | keep a dedicated server on the build it was started with. It updates itself otherwise -- see Updating |
| `FruityPrime -credits` | who this is built on and who forked it, from `Mods/Credits.cs` -- which also holds the ko-fi address the settings' Credits page offers |
| `MphRead -fullscreen` / `-windowed` / `-nohelmet` | display choices for the paths that never open a launcher |

## The launcher

**One launcher, in Avalonia, on Windows, Linux, macOS and Android** — one
thread, one toolkit setup per process
(`GuiLauncher.EnsureSetup`), each visit a nested dispatcher loop. `-launcher`
opens a front screen, not a settings dialog: a map picture on the left, the
things you can do on the right.

| Entry | What it does |
|---|---|
| Host | the story from a save slot, or a match: map, mode, hunter, and a `Where` row -- **Local** is an offline match with 0-7 bots and their skill, **Online** asks the directory to run it. The listen-host path (`NetHostSession`, the dedicated server in this process over the loopback) still exists and is still what `LaunchKind.Host` can do, but the card no longer offers it: the port, "let the directory run it" and "list it" rows are built and forced rather than shown, because every one of them is a question about the player's router. Running a server yourself is the dedicated server's job |
| Join | name, hunter, `host` or `host:port`, and a live line saying what that server is running. **Find a server** opens the browser |
| Demos | pick a `.fpdemo` and replay it -- on Android too, where the picker cannot filter by pattern and hands back a `content://` document that has to be copied in first |
| Settings | display, audio, controls, match rules, and profile (name, hunter, server addresses, updates, game files, credits). Also reachable from the pause menu during a match. **Pro mode HUD** is the whole HUD question in one switch -- no helmet, plain fixed crosshair, weapon list at 170%, fixed weapon, and its own energy, ammo and score readouts in place of the game's; off is the game as the DS drew it. Two rows appear under it while it is on and nowhere else, because they are questions only it can answer: **Crosshair size** (Small / Medium / Big) and **Crosshair type** (Cross, Dot, Cross + dot, Circle, Brackets), the type row carrying a live picture of the answer at the chosen size. The six settings pro mode answers for have no rows at all, and the rows that remain have no explanations under them. Cheats, bugfixes, the leftover feature flags and the HUD-readout opacity likewise have **no UI** and no longer load from `settings.json` -- they sit at their code defaults |
| Game files | where the .nds goes. Shown first, and everything else greyed out, when there is nothing set up yet |
| Debugging logs | one line in the bottom right corner, under the version, on the front card only. Off; switched on it writes `logs/FruityPrime-<when>.log` beside the executable (the app's data directory on Android) with everything the program prints plus the machine, the driver, every model read and the stack of anything that kills it. What "it crashes when the map loads" is answered with. **Share logs** sits to its left, only when logs exist, and zips them into the phone's share sheet -- the app's own directory being one no file manager will browse. `.claude/DEBUG-LOGS.md` |

Gotchas worth keeping in view without opening another file:

- **Cheats are all off in a networked match**, not just the obviously leaky
  ones: `NetLaunch.DisableCheatsForMatch` walks every `public static bool` on
  `Cheats` by reflection, so the list can't drift.
- **There is no console window at all** on the Windows build (`WinExe`).
  `Mods.ConsoleWindow.Prepare` attaches to a parent console when a command was
  typed, allocates one when double-clicked, and does neither for the launcher.
- **A bare invocation opens the launcher on Windows and macOS**; on Linux it
  still opens upstream's console menu, since that's a screen people there
  already use — `-launcher` asks for the window there too.
- Offline matches can hold eight players; `PlayerEntity.MaxPlayers` defaults
  to four (a DS match's cap), so the launcher raises it before creating
  players or asking for seven opponents silently produces three.
- **The weapon icons are supersampled, and they are the only thing in the
  program that is.** `Mods/Render/SmoothHudIcon.cs`: the art is one texel per
  DS pixel and lands in a box two or three times that size (more in pro mode,
  which draws the list at 170%), so nearest magnification gave every icon a
  staircase with two-pixel steps in some rows and three in others -- reported
  as "the icons are blurry", which it is not; it is a wobbly edge. These
  frame is replicated **hard-edged at 8x**, no interpolation, and
  `HudObjectInstance.Smooth` asks `DrawHudObject` for linear filtering on that
  one texture: the GPU is then *minifying* sharp squares, which is those same
  squares with a hairline of anti-aliasing. Everything else stays nearest,
  because everything else is meant to look like the DS. **Two traps.** The
  factor has to stay above the largest magnification in play (about seven, at
  4K with the list at 170%) or the GPU magnifies instead and the icon goes
  soft -- 4x did exactly that at 1440p and got the same word back. And
  resampling the mask (bilinear, then a smoothstep) rounds every corner into a
  blob: sharp is the goal, the anti-aliasing only stops the edge wobbling.
  `DrawWeaponList` reuses the instances rather than rebuilding them, because
  the HUD is set up again on every rotation and nine 256x256 textures a map
  is a leak.
- **Changing hunter is asked on the results screen, not in the pause menu.**
  The two rows that used to sit there ("Respawn as", "Suit colour") are gone:
  a pause menu is opened instead of playing, so the one screen where the
  change is free was the one screen that never offered it. `Mods/EndScreen.cs`
  puts it in the top right corner of the ten-second results screen instead --
  the hunter's own portrait with an arrow either side, four suit swatches
  whose colours are read out of that hunter's model (`Mods/HunterSuits.cs`,
  nothing is written down), and the next map's name off
  `MatchStatePacket.NextRoomKey`, which had been on the wire since the
  rotation was written and never read. **The hunter is the real model, not a
  sprite** (`Mods/Render/HunterPreview.cs`): it is an `EntityBase` that is
  *never inserted into the scene* -- so it takes no slot, runs no Process,
  holds no NodeRef and cannot outlive a room change -- whose items are
  collected last and drawn in a pass of their own
  (`Mods/Render/PreviewPass.cs`) into a scissored corner with its own camera,
  its own fixed lighting and its own cleared depth buffer, so nothing in the
  level can occlude, light or cull it. It stands still, facing the camera
  (these models are authored facing -Z, hence the half turn) in the `Idle`
  animation, because with no animation set the skeleton draws as authored,
  which is a T-pose. The panel is drawn in four boxes with a hole where the
  model lands, since the model reaches the frame before the HUD does. The
  sprite portrait is still there as the fallback for a model that will not
  load. Arrow keys or the d-pad. The answer is
  still `RespawnChoice`'s and is still cashed in at the next spawn.
  `GameState.MatchEndingSeconds` and `DedicatedServer.EndSequenceSeconds`
  are one number in two places and have to move together. The cursor is
  released for the length of the results screen (`Renderer.OnRenderFrame`
  reads `EndScreen.Available`) because the picker is something you click:
  arrows for the hunter, the swatches directly for the suit. The hit boxes are
  published by the draw (`EndScreen.NoteLayout`) rather than worked out twice,
  so they cannot drift from the picture. **The panel's height is derived from
  the stack inside it** (`EndRow`/`EndStackHeight`), not stated: it was a
  constant 92 that the content did not fit, so READY hung out of the bottom
  edge and the "NEXT: ROOM" line -- placed by measuring *up* from that same
  edge -- was drawn straight through the middle of it, which at the 1.45x the
  picker used to be drawn at on a phone was the whole button. The suit caption
  and the colour's name are one line now ("SUIT: ORANGE") rather than two on
  either side of the swatches, and `EndScale` is 1.3 on Android.
- `PacketType.StatusQuery` answers "what map, what mode, how many players"
  without claiming a slot, which is what lets the browser poll idly. A server
  built before it falls back to a slot-taking Hello/Bye probe — redeploy the
  server to get the cheap path. Full account, plus the directory and hosting
  design: `.claude/multiplayer/NETWORK-BROWSER.md`.

Deep dive (UI components, settings window, first-run/extraction, macOS/Android):
`.claude/launcher/LAUNCHER-OVERVIEW.md`, `LAUNCHER-DESIGN.md`,
`LAUNCHER-SETTINGS.md`, `LAUNCHER-FIRSTRUN.md`.

## Android

The head builds a playable APK. The engine's `GL` is redirected to OpenGL ES 3.0
by **one using alias** in the Android csproj, pointing the name at
`Mods/Render/GlEs.cs`, which emulates the four things ES does not have —
immediate mode, display lists, the current colour and the alpha test — so not
one call site in upstream's renderer changed. Input is the same trick from the
other end: `AndroidInput` hands the scene a keyboard and a mouse of its own and
presses whatever the player has bound, which is why rebinding, aim sensitivity
and the DS weapon wheel all work without touching `ProcessAllInput`.

**Which on-screen buttons are drawn is the player's.** Settings → Controls →
On-screen buttons is a master switch and one toggle per button, and it exists
because the buttons sit on top of the thing they get in the way of: aiming is a
drag anywhere on the right of the screen, and a drag that starts inside a
circle presses the circle. A button turned off is drawn nowhere and takes no
touch, so the glass it was on becomes aim. Nothing there can strand a player:
movement is the stick, aiming is a drag, jump is a double tap and boost is a
flick, and not one of the four is a button. Kept in `controls.txt` with the
rest of the controls (`Mods/Input/TouchSettings.cs`).

**Controls used to reset every time the app closed**, and it was two faults at
once: `InputSettings.Load` is called from `ModEntry.TryHandleHeadless`, which
this head never runs, and `controls.txt` was written beside the executable --
a directory an Android package does not own, so every save was refused and the
exception swallowed. It follows `LauncherPrefs.Directory` now, and
`AndroidApp.BuildHome` loads it.

**The front screen runs; the match has never been loaded.** An emulator (API
30, x86_64, software CPU and GL) shows the screen and the game-files card; what
that box cannot do is load a room, having no extracted game files, so the
renderer, the touch controls and demo playback (which the head can now do --
see the port notes) are still unmeasured on a device. Two traps that killed the
app before any of this project's code ran — an activity theme that was not an
AppCompat descendant, and a Debug APK that carries no managed code unless
`EmbedAssembliesIntoApk=true` — are written up with the rest in
`.claude/android/ANDROID-PORT.md`, along with the build recipe, the game-files
directory, and how to run an emulator here.

## Gamepads

**A pad and the touchscreen are both live at once on Android.** Using the pad
puts the on-screen layout away and does nothing else: the surface underneath
keeps working, so a touch does what it landed on *and* brings the layout back.
A stick in one hand and a thumb on FIRE is a normal way to hold a phone, and
the weapon wheel cannot be reached from a pad at all.

A pad plays the game on the desktop and on Android, over USB or Bluetooth,
in an Xbox-shaped layout: sticks move and aim, right trigger shoots, A jumps,
B morphs, the bumpers and d-pad change weapon, Back is the scoreboard and
Start is the pause menu. There is **no weapon wheel on a pad** -- it reads an
absolute pointer position, which a stick does not have.

It reaches the game the way the touch controls do, from the other end: after
`ProcessAllInput` has run, the pad's contribution is **ored** onto the same
keybinds the keyboard just filled in, so a pad and a keyboard work at once and
no upstream call site changed. Aim is the exception, since a stick is analogue
-- it goes in at `ApplyModAim`, in the same units and at the same point in the
frame as the mouse's.

`FruityPrime -gamepad` prints what a pad is doing with no match in the way,
and distinguishes "not connected" from "connected but unmapped". Layout, feel
(radial dead zone, squared look curve, 3.5 degrees a frame at full stick), the
four settings, and how to test one with a virtual pad on `uinput`:
`.claude/GAMEPAD.md`.

## Frame rate

**The simulation runs at exactly 60 Hz. The picture runs at the display's
rate.** They used to be the same call, which is why 60 was the whole frame
rate; `Scene.OnUpdateFrame` is now `OnSimulationFrame` plus `OnDrawFrame`, and
`RenderWindow` runs the first on a fixed-step accumulator
(`Mods/Render/FrameTiming.cs`) and the second every time it draws.

The simulation cannot be moved off 60 and that is not a limitation to design
around, it is the reason the split exists: every timer in the engine is counted
in frames -- `grep -rc "todo: FPS stuff"` finds **806** -- and an intent is sent
per frame, a demo is a count of frames, and `NetConfig.ProtocolVersion` would
have to move. Nothing about the wire, the demo format or the DS behaviour
changes here, because nothing about the simulation does.

- **`Scene.OnUpdateFrame()` is kept and still does one step and one picture.**
  Every harness client calls it -- `NetCheckClient`, `MapAudit`, `WeaponDps`,
  `ThumbnailCapture` -- and is therefore untouched by any of this. So is
  Android, which drives the same call.
- **Every picture is of the newest simulated state, and nothing is blended.**
  There was an interpolation pass -- entity transforms and the camera blended
  between their last two simulated states -- and it is **gone**, deliberately
  and completely. It bought smoother motion between steps and cost visible
  wrongness on everything that is pooled and reused: a beam projectile or an
  impact effect taken off the free list starts its new life holding the last
  one's transform, and a blend against that draws the shot somewhere between
  where it used to be and where it is. That is the "artifacts de tirs" and the
  wall impacts landing nowhere. Do not put it back without an answer for entity
  reuse.
- **The game's speed no longer depends on the machine.** One call for both
  meant a box managing 40 fps played in slow motion; the accumulator pays what
  it owes, measured at 60.000 Hz with a 40 Hz draw rate.
- **Look for timers living in the draw pass.** Three were found by audit and
  all three are handled by counting steps owed rather than by moving code:
  `UpdateFade` (whose delay ends cutscenes and changes rooms -- it would have
  expired 2.4x early at 144 Hz), `ProcessEffects`, and the pause map's own
  animations. Frame advance is forced back to one step per picture, because the
  request to advance is consumed *after* the frame is drawn.
- **When a frame is split in two, look at every counter both halves read.**
  Effect elements spawn particles on every *other* step (the DS ran effects at
  30 Hz) and record the parity they were created with, so their **first**
  advance is a spawning one -- which is where a burst lives. Upstream
  incremented `_frameCount` *after* `GetDrawItems`, so a spawn and the advance
  that followed it in the same frame saw the same number; the split moved the
  increment into the step, before the draw, and every element's first advance
  started failing its own check. No flash on a charging Missile, no explosion
  on a wall, while the smoke and debris of those same effects carried on --
  and every number the harness measured was unmoved. `_effectFrame` is a clock
  the effect system owns, read by both halves. 776 effect particles a run
  became 1254.
- **Draw state must be cleared in the draw pass, not in the step.** The
  single-particle table -- the fuzzball at the head of a shot, the scan-visor
  markers, the death sparks -- is filled during the entity draws and emptied
  once a frame, and the emptying stayed behind in the simulation step. A
  picture with no step behind it, which is most of them at 144 Hz, therefore
  drew the previous frame's particles a second time at the positions they had
  then, and kept doing so until the 200-entry table filled and started dropping
  the new ones. `_singleParticleCount = 0` now sits in `OnDrawFrame`.
- **The on-screen FPS counter reports the picture**, not the simulation. The
  simulation rate is invisible to a player, which is why it goes to the debug
  log -- every five seconds, or immediately on a dropped step or a stall.

Default is **Display (VSync)**, the only tear-free setting; an explicit number
turns VSync off, since asking for 120 on a 144 Hz screen with VSync on gets 72.
OpenTK 4.9 no longer separates its update and render ticks, so `UpdateFrequency`
on the window is the frame rate and `RenderFrequency` is deprecated.

Android runs the same split in `GameView.RenderLoop`, with input inside the
step loop and no sleep in display mode (`eglSwapBuffers` is the pacing there).
It builds but **has never run on a device**, like the rest of that head.

Full account, what the draw pass may and may not touch, Android, and how it is
all tested without a 144 Hz monitor: `.claude/render/FRAME-PACING.md`.

## Updating

`Mods/Update/`. The program checks GitHub for a newer release on its own, says
so, and **installs nothing where a person could decide** — "Update now" opens
the release page; download and unpacking are the player's. **A dedicated
server is the exception and installs on its own**, because every part of that
reasoning inverts when there is nobody at the keyboard. It checks by itself because
`NetConfig.ProtocolVersion` makes a server refuse a client on a different
build outright at Hello, so a copy one release behind can't join anything, and
that's worth automating; it does not install because that means downloading
and executing a file with no signing behind it, so the guarantee would only
ever be "TLS, and GitHub was not compromised" — not doing it is better than
doing it carefully.

| | When it checks | What "update now" does |
|---|---|---|
| Launcher window | in the background once the window is up | opens the release page; badge shows the address if there's no browser |
| Text launcher | at startup, waiting up to 2 s | prints the address, opens a browser if there is one |
| `-update` | when asked | prints the address and opens it |
| Server and directory | at startup before binding, then every 10 min | **installs it**, and restarts — but only once nobody is connected (a server) or no hosted match is running (the directory), so a busy one keeps playing and swaps when the last person leaves. `-noautoupdate` opts out |

A server updating itself is the one place the "no unsigned installs" rule is
traded away, and it is traded for a bigger one: `NetConfig.ProtocolVersion`
makes a server refuse every client on a different build at Hello, so a stale
server is a server **nobody in the world can join**, indistinguishable from
one that is switched off. A bad binary is a failure an operator can undo; that
one is a failure nobody can even see.

The swap is not the launcher's. `DesktopUpdate` starts a second process that
waits for this one to exit and then copies over the installation, which is
exactly wrong under systemd: the copier is a child, so it lives in the unit's
control group, and the moment the main process exits systemd kills the group
and restarts the unit — killing the copier mid-copy and bringing the old build
back, for ever, with no error anywhere. `Mods/Update/ServerUpdate.cs` needs no
second process: a running program on Linux holds its files by inode, so the
new build is written over the installation by the server itself, one atomic
rename at a time, and then it exits. Under a supervisor (`INVOCATION_ID`)
exiting *is* the restart; with none, it starts its successor itself — **with
the command line it was given**, since a dedicated server restarted bare opens
a launcher on a machine with nobody at it.

Windows will not delete a running image, and that used to end the swap
half-done: a Windows server downloaded every release and applied none of them,
which stopped being cosmetic the moment a protocol bump made a stale server one
nobody can join. It *will* rename a running image, so the old build is moved
aside to `.fp-old` and deleted by the next start, which is the first moment
nothing is running out of it. Both platforms now take the same path with one
step different, and a `-server` start sweeps whatever a previous update left
behind whether or not updating is still switched on.

`launcher.txt` carries `auto_update`, on by default; `-noupdate` turns it off
anywhere, including the server's. A local build without the release workflow's version stamp reports
itself `a local build` and stands down, since there's no way to tell it apart
from a release either ahead or behind.

## The test method

The failure that matters here is invisible from one side: two clients can be
perfectly connected — right slots, agreed clock — while each holds a scene
containing only itself. `-netcheck` runs the **real client** (real
`Scene.OnUpdateFrame`, real `PlayerEntity` simulation, real net hooks, hidden
window) driven by `NetTestScript`'s fixed 15-phase tour, keyed to the
**server's** clock so every client is in the same phase at once. Every client
records what it *did* and what it *saw*; `compare-reports.py` cross-checks
that what one claims to have done shows up as what every other client says it
saw.

```bash
cd ~/mph-net-test
./run-check.sh 150 Samus Weavel Sylux Trace Samus Noxus   # seconds, then hunters
```

Read the output in this order: per-feature `MISMATCH` lines, then
`scoreboards agree`, then `damage pipeline`, then `remote position snaps`.

Map sweeps (`-maptest`, `-maptest -bots`), the world/affliction probes, how to
read every metric the harness prints, and the traps that have already cost
time: `.claude/testing/TEST-HARNESS.md` and `.claude/testing/TEST-METRICS.md`
(the latter also carries the last verified pass/fail status).

## Building and releasing

`.github/workflows/build.yml` publishes `win-x64`, `linux-x64`,
`linux-x64-server`, `linux-arm64`, `osx-x64` and `osx-arm64` on every push and
PR; `release.yml` builds those six plus the Windows server (seven packages) and
the APK on a pushed `v*` tag, and leaves them on a **draft** release for a
person to read and publish -- it is never published by the workflow itself,
and it is deliberately not flagged a prerelease, since GitHub's
`releases/latest` (what the in-app update check asks) skips those:
Releases here are numbered from **v0.1.0** and are their own line, not
upstream's: `Program.Version` (0.35.1.0) is upstream's data-format number and
is what `paths.txt` is checked against, while the tag is what
`BuildVersion.Current` reads. The two never meet, which is why the release
numbering could start over without invalidating anybody's extracted files.

```bash
git tag v0.2.0 && git push origin v0.2.0
```

Or run `release` from the Actions tab with the tag box empty and a **bump**
picked (`patch`/`minor`/`major`): the workflow reads the newest `v*` tag,
works out the next one, creates it on the commit the run was dispatched from
and builds that. Nothing to clone, nothing to type. That is the only
auto-tagging there is -- a push to master tags nothing, because every push
would then be a release, and the draft still waits for a person either way.
It lives inside `release.yml` rather than in a workflow of its own because a
tag pushed with the default `GITHUB_TOKEN` does not trigger another workflow.

The release notes are a standing block (beta, bring your own cartridge, which
package is which) with GitHub's own generated changelog appended under a rule
-- every commit and merged PR since the previous tag. If the generator fails
the block still goes out, with a warning in the log.

`tools/check-no-game-assets.sh` (no Nintendo asset ever published) and
`tools/check-dedicated-server.sh` (the server actually starts) both run in CI
and are worth running locally before pushing:

```bash
tools/check-no-game-assets.sh                    # the repository
tools/check-no-game-assets.sh publish/win-x64    # a build
```

Tagging (including the bump path and its traps), PE-header subsystem
split, why only the Windows server is renamed, and the CI runner layout: `.claude/build-deploy/BUILD-WORKFLOW.md`.

## Deployment

```bash
# server and directory (rebuilds ARM64, installs both units, restarts them)
MPH_SERVER_HOST=net.livetek.fr MPH_SERVER_USER=livetek \
  MPH_SERVER_PASS="$(read -rsp 'pi password: ' p; echo "$p")" ./deploy-server.sh
# MPH_DEPLOY_MASTER=0 to leave the directory alone
```

The exe is often locked by a running game: write `MphRead.new.exe`, then `mv`.

**`NetConfig.ProtocolVersion` is 6.** Any protocol change means server **and**
every client must be the same build — a mismatched client is refused outright
at Hello with a line in the server log, which is the intended outcome and not
a layout issue: the wire format doesn't move, an old client would read every
byte correctly and then simulate a different game (frozen in place, shooting
from its ankles) with nothing in the protocol to notice. Deploy the server
before handing out a client built against a new protocol. Publish commands and
the deploy script's env vars: `.claude/build-deploy/DEPLOY-SERVERS.md`.

`-simulate` is the one server option that needs game files on the server box.
It changes nothing on the wire, so it can be turned on and off between
restarts without touching a single client.

## Multiplayer: bugs found and fixed

A "damage is broken" report chased as latency for a fortnight turned out to be
eleven separate faults — frozen remote puppets,
shots fired from ankle height, respawn placement races, a derived-velocity
launch bug, a stale settling guard, a per-machine damage-sequence reset, a
divergence backstop comparing against the wrong instant, jump pads misread as
desyncs, a damage-direction vector abused as a launch velocity, unreplicated
ammo making a puppet briefly untouchable, and an unordered snapshot stream —
plus a double-counted kill that could end a match early for one client and not
another, and a transport queue that dropped the newest packets under load
instead of the oldest. None of it was actually latency; all of it reproduced
at single-digit-millisecond pings on loopback or the Pi.

A round from real matches on 2026-09-07: **every client died on the map
rotation, and MP2 HARVESTER's results screen came up black.** One cause.
`CameraSequence.Intro` is a static loaded only by `SceneSetup.LoadNewRoom`,
and **a rotation does not go through it** -- it is a room *transition* -- so
after the first map of a session the results screen flew the sequence
belonging to the map the session started on, and every `NodeRef` in its
keyframes named a level no longer in memory. Out of range that is an
`ArgumentOutOfRangeException` in `RoomEntity.DrawRoomParts` that kills every
client in the match on the same frame; in range it is a room drawn from a
part the camera is not in. Three changes:
`NetRoomChange.ReloadIntroCamSeq` gives each map its own sequence and
`Initialize`s it; `RoomEntity.ModCanPlace` refuses to cull against a ref this
room cannot place (indices in range **and** `NodeRef.RoomName` this room or
one of its connectors); and nothing culls at all while `MatchState` is not
`InProgress`, because the end-of-match camera is an authored orbit that is
free to sit outside every room part in the level -- which is the black
results screen and is not a stale ref at all. Reproduced and confirmed fixed
with `run-rotate.sh`: six rotations over four maps, zero crashes, and the
backstop never fires now that the cause is gone.

A second round, from reports out of real matches on 2026-09-04: a freeze that
existed on one machine only, players who went invisible at the top of one map,
a gun that fell off the bottom of the screen on a pad and could not be raised
or fired again, a weapon cycle that stopped dead on an empty weapon, and no
camera at all in alt form on a pad. Only the first is a network fault; the
rest are input and rendering, and three of the five were invisible to every
check here because nothing in the harness holds a controller.

Shapes worth keeping without opening anything else:

- **An affliction is not state until it is sent.** Freeze is applied inside
  `TakeDamage` from the *beam entity*, and `NetDamage.Replay` has no beam to
  give -- so a player frozen on the authority was frozen nowhere else: they
  walked around normally on their own screen while the machine running the
  simulation, which pins a puppet wherever its owner last said it was, drew a
  block of ice sliding across the room. `PlayerState.FlagFrozen` carries the
  state instead of the cause, and the countdown still runs locally. The same
  was then reported of the other two: the Volt Driver's screen distortion and
  the Magmaul's flames reached nobody but the authority either
  (`FlagDisrupted`, `FlagBurning`, and the flags byte is now full). A frozen
  puppet also stopped taking its owner's reported positions, since those
  describe a moment before the ice.
- **A puppet is moved after the movement step, and only the position moved.**
  `Move` set the position, the previous position and the node ref -- not the
  collision volume, which the engine recomputes inside the step this
  correction comes *after*. The blob shadow and the burn effect are drawn from
  that volume, and shots are tested against it, so for every remote player all
  three described where this machine had guessed they were while the model was
  drawn where they are. "The shadow is behind the character" was the visible
  third of it.
- **A press can be lost; a state cannot.** The alt form was replicated by
  replaying the morph press and nothing else, so one press that did not take
  left the authority's copy in the wrong form for the rest of the life -- and
  every other client agreed with it, since `FlagAltForm` is read off that
  copy. `IntentButtons.AltFormState` had been in the packet all along, used
  only to convert reported positions between forms; the authority now
  reconciles against it, and only the authority does.
- **A lookup that fails is not the same as a lookup that is stale.**
  `GetNodeRefByPosition` returns nothing for a position no room part contains
  -- the top of AD2 ALINOS PERCH, among others -- and the fallback kept the
  node the puppet already had, which the viewer often cannot see. That is a
  player who can shoot you from somewhere you cannot see them, with their
  shadow still moving about underneath. Now: probe the body and half a unit
  either side first, and if it still cannot be placed, say so and draw it
  anyway.
- **Nothing that answers "has this person touched anything lately" knew about
  the pad -- or about a remote player.** `Input.HasInput` is written in the
  pass that turns a keyboard into binds, and three kinds of player never go
  through it: a pad (ored on afterwards), a puppet (driven from relayed
  intents), and a scripted client. All three therefore looked idle, and the
  engine lowers an idle player's gun -- which `CanShoot` and `TryEquipWeapon`
  both refuse to work through. On a pad that meant a gun that fell off the
  screen and a player who could not fire again; on a **puppet** it meant a
  player who held still and fired had their shots fail to spawn *on the
  authority*, which is the only machine whose shots count. Shots spawned per
  slot went from 28-63 to 118-142 once it was fixed. `ModNoteInput`.

- **A stale input is not harmless just because it's only a position.** The
  intent stream has no notion of "this predates what just happened," so
  anything the authority does to a player of its own accord (a spawn, a
  teleport) can be undone by the next packet that predates it.
- **Look for this shape whenever a remote player can do something on their
  own machine and not on anyone else's:** the puppet is running the same code
  with different *resources* (ammo, in this case), and only the owner's copy
  of those is authoritative.
- **`untested` is a question about the harness, not a pass or a fail.** The
  zoom-replication check read `untested` for months because the tour never
  actually pressed the zoom button, not because zoom was broken.
- **A frame counter that restarts is not an out-of-order packet.** Every
  ordering guard on the wire compared frame numbers and nothing else, so a
  client rejoining a match it had been in for five minutes -- counter back to
  1, the authority still holding 18000 -- had every intent it sent refused for
  the next five minutes. Reproduced, and the shape to look for is any guard
  that says "older than what I have" without also asking "older by how much".
- **A ping is a measurement of the code path, not only of the wire.** The
  number on the scoreboard read 20 ms to a server 1 ms away by ICMP because
  the reply waited for the next rendered frame and for two poll-sleeps on the
  way. Now 1 ms on loopback, where it was 8-11.
- **A randomised run against a loopback server is a regression check, not a
  real-world one, and must not be reported as one** — it has none of the
  reordering, jitter or CPU load the bugs above were found under.

**The server can be the simulation authority** -- `-simulate`. The authority
was never a property of being a player: it is the property of being the
machine every other player's intent is pointed at, and until now that was
whichever client joined first. The engine's simulation needs no GL context at
all (the frame split had already put every GL call in `OnDrawFrame`), so the
server runs the *real* engine rather than a model of it -- which is what
answers the old objection that a reimplementation would be a second answer
free to disagree with the first. What it buys is fairness and resilience:
nobody is at zero latency any more, no handover when the authority leaves, and
`HandleSnapshot` refuses every client's world outright. What it does **not**
buy is a shorter wait for your own hit to register -- that is a round trip
wherever the authority sits, and shortening it is client-side prediction,
which is not implemented. The wire does not move: a client is told it is the
authority by receiving `PacketType.Authority` and in no other way, so a
simulating server simply never sends it. Measured at **110 MB and 0.31 ms a
step** for an 8-player room, against 337 MB for a full client, by dropping
work whose only output was a picture. `.claude/multiplayer/NETWORK-SERVERAUTH.md`.

**Shots are resolved against the world the shooter was looking at**, not the
one that exists by the time their trigger arrives -- backwards reconciliation,
ported from Q-Zandronum's `unlagged.cpp`. The error it removes is one-sided and
exactly a client's round trip: the authority spawns a remote player's beam
against the puppets it holds *now*, while that player aimed at the puppets a
snapshot showed them a round trip ago. `IntentPacket.AckFrame` is the whole
input -- the snapshot frame the shooter was looking at -- and the authority
rewinds everyone else to it, spawns, and then walks the shot forward to the
present one frame at a time, re-reconciling at each step. That second half is
Q-Zandronum's own and is the half this game needed: almost nothing here is
hitscan, so without it a laggy player's Missile merely leaves the muzzle late.
The authority itself is rewound by zero, because it already aims and resolves
against the same puppets. Measured at 156 ms of rewind against 150 ms injected,
with 0 mismatches on the 3-client instrument. `-nounlagged` is the control.
`.claude/multiplayer/NETWORK-UNLAGGED.md`.

**A client's own hits land the frame it fires them**, rather than a round trip
later -- which is the half the rewind deliberately did not buy, and the only
thing that gives anybody an instant hit when a *server* is simulating the
match and nobody is the authority. It is sound only because the rewind is
there: the authority puts everyone back to the snapshot frame the shooter had
applied, which is the world the shooter's own machine is holding when it
fires, so the local resolution and the authority's are the same test on the
same positions -- run earlier, on the machine that already has the inputs.
Two rules keep a prediction from becoming a lie: it **never scores and never
ends a match** -- the scoreboard is assigned from the snapshot for every slot
and `EndIfPointGoalReached` is already refused on a machine that is not
keeping the score -- and it is **only your own shot**, on somebody else or on
yourself, since incoming damage is a question about a shot fired on another
machine and this one has a worse answer to it than the authority does. **Your
own splash on yourself is predicted** -- a rocket jump is not damage that
arrives late, it is a jump that does not happen, and the push comes out of
`TakeDamage` with the damage. Source, target and input are all on this machine,
so it is arithmetic rather than a bet on a rewind; it is counted apart from the
rest for that reason, and **it is the one prediction that is still allowed to
kill** -- a rocket jump at low health, a recoil, a crusher, and above all a
fall into the void, which is the one death a player has already watched happen.

**The prediction is held rather than assigned over.** A victim's health is the
authority's number less what this machine has landed on them and not yet had
confirmed, a victim predicted dead stays down instead of being stood back up
by a snapshot that has not heard about it yet, and the health this machine's
own Shock Coil drains is credited on top of the authority's until it catches
up. The hold lasts one measured round trip and a margin -- not the two seconds
a prediction is kept for the statistics -- because a mispredicted hit is a
wrong health bar and a wrong health bar has to right itself in the time the
answer takes. **Killing somebody else is not predicted**, and that is a change
made on the strength of a real line rather than a loopback: against Japan a
client could kill the same opponent twice for one kill on the scoreboard,
because the authority disagreed and the next snapshot stood the body back up.
The damage is clamped to leave the victim on one point of health, so the flinch
and the mark are still instant and only the body falling is owed a round trip;
`-deathprediction` puts it back for measuring. **A self-kill is predicted**,
whatever that switch says. Nothing is rolled back because nothing durable is
written -- health, the score and a wrongly killed puppet's spawn all come off
the next snapshot on the lines that always carried them.
Confirmation is a white X around the crosshair, drawn on every machine and in
every match, offline included -- `.claude/multiplayer/NETWORK-PREDICTION.md`.
Measured at **86-98% of predictions confirmed** across runs -- 86% on the
largest sample, with 150 ms injected -- and at zero mismatches with a
simulating server, where all three clients predict. Quote the range: the
scripted tour does not fire the same shots twice. `-nohitprediction` is the
control, `-deathprediction` turns the lethal half back on for measuring, and
`-nohitmarker` turns off just the mark. No protocol change.

**Chat is T**, three lines bottom left in green on nothing, gone ten seconds
after they arrive -- the frame counter sits in the right-hand corner, which is
where it went when the log was still in the top-left one. Bottom left because
that is where every game that took Quake's shape puts it and where players
look; the block is anchored at y 168, above Pro mode's energy panel, and steps
right past the weapon column when the modern HUD is drawing one. It draws with a font of its own (`Mods/Chat/ChatFont.cs`, pixel art in
the file, no asset): the game's has one alphabet, so every line typed came out
shouted and half as wide again as it needed to be. `PacketType.Chat` is additive and needs no protocol bump, so an older
server drops it silently and chat simply does nothing there until it is
redeployed. **Never in the story** -- `ChatBox.Available` is
`!GameState.SinglePlayer` -- and on Android it is a CHAT button that asks for
the soft keyboard, or any keyboard that happens to be attached. The server writes the slot and the name onto every line it relays
rather than trusting the sender's, and rate limits at the relay. Packet
numbers 24 and 25 are left free for a voice channel.
`.claude/multiplayer/NETWORK-CHAT.md`.

Recording and watching a match back -- the file format, the two things a demo
has to synthesize because they were never received, and why the player counts
frames rather than milliseconds: `.claude/multiplayer/NETWORK-DEMOS.md`.

Full postmortem, measurements, before/after tables, and the traps that cost
the most time: `.claude/multiplayer/NETWORK-DIAGNOSTICS.md`. The
double-counted-kill bug and match-end/rotation handling specifically:
`.claude/multiplayer/NETWORK-MATCHEND.md`. Current verified pass/fail status:
`.claude/testing/TEST-METRICS.md`.

## Known gaps

Claims that are unproven or only partly proven — not bugs, but not to be
re-claimed as solid either: `.claude/KNOWN-GAPS.md`.

## Mechanics catalogue

Weapons, damage multipliers, hunters, movement, states/afflictions, spawning,
match modes, world interactions, pickups, bots, and the multiplayer protocol
rules are in `MECHANICS.md` at the repository root.

`MphRead -mechanics` **prints** the catalogue to stdout; it writes no file.
Do not regenerate the committed one by redirecting it over the top --
`MECHANICS.md` carries detail the generator cannot produce (the affliction
table's charge rules, among others), so a wholesale `> MECHANICS.md` silently
deletes it. Change `MechanicsDump.cs` for anything derived from the game's
tables, and edit the file directly for anything that is not.
