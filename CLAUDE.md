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
| `~/MphRead-dev` | the source. Upstream is NoneGiven/MphRead; everything added lives under `src/MphRead/Mods/` so pulling upstream stays a fast-forward |
| `src/MphRead.Android/` | the Android head: the same sources, an APK, a front screen and a match, over GL ES and touch controls |
| `src/MphRead/Mods/Network/` | the whole multiplayer feature |
| `src/MphRead/Mods/Launcher/` | the launcher: `Gui/` is every window (Avalonia, all platforms), `Portable/` is the logic and the text screen |
| `~/mph-net-test/` | the test rig: a copy of the build in `bin/`, extracted game files, `run-check.sh`, `compare-reports.py` |
| `C:\Users\livetek\Desktop\MPH\MphRead-develop\` | the Windows deliverable |
| `net.livetek.fr:27888` | the dedicated server on the user's Pi (systemd unit `mphread-server`) |

## Environment recipe (WSL)

Three things will waste an hour each if you do not know them:

```bash
export PATH="$HOME/.dotnet:$PATH"          # dotnet is not on PATH
export MESA_GL_VERSION_OVERRIDE=4.5COMPAT  # else Mesa hands out a Core profile
export ALSOFT_DRIVERS=null PULSE_SERVER=   # else ALSA retries stall frames
```

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
| `MphRead -server -port N -players 8` | dedicated relay server; needs no game files. `-servername "NAME"` is what a browser shows; it announces itself to `net.livetek.fr` unless `-nomaster` is passed, and `-master HOST -masterport N` points it elsewhere |
| `MphReadServer.exe -server ...` | the same server on Windows, as its own console binary. `MphRead.exe` can also do it, but it is a GUI binary: a shell will not wait for it and its exit code never reaches `%ERRORLEVEL%`. Run with no arguments it prints what it is for |
| `MphRead -masterserver [-port N] [-public HOST] [-hostports A-B]` | the server directory the launcher's browser asks, and the machine that runs matches for players who cannot open a port. Same binary, no game files, keeps nothing on disk. `-public` is the address to publish for servers registering from this same machine, whose heartbeats arrive over the loopback |
| `MphRead -hostgame "ROOM" [-mode M] [-master HOST]` | ask the directory to run a match and join it. No port forwarding anywhere; the only way to host from a machine with no launcher |
| `MphRead -servers [-master HOST] [-masterport N]` | print the server list the launcher's browser would show, with each server's map, players and round trip |
| `MphRead -connect HOST -port N -name X -hunter H` | join from the command line, no launcher |
| `MphRead -netcheck HOST -port N -name X -hunter H -seconds N [-shots DIR] [-size WxH]` | a real client driven by a script, which reports what it saw. Exit code 0 = pass. `-spectate [SEC]` makes it stop playing and watch, `-rejoin SEC` puts it back in -- the one player state the tour cannot reach on its own |
| `MphRead -netlag MS[:JITTER]` / `-netloss PCT` | play, or run any check, over a line this client makes up: `-netlag 200` adds 200 ms to the round trip (half each way), `-netlag 200:40` gives it jitter, `-netloss 5` eats one datagram in twenty. Works against the real server, on any platform, with no proxy and no `sudo` -- and unlike `hard/run-latency.sh`'s netem it can be given to **one** client while the others stay fast, which is the case a player with a bad line actually is. Every report says so when it is on |
| `MphRead -debuglog` | write the file the launcher's corner switch writes, for one run. `.claude/DEBUG-LOGS.md` |
| `~/mph-net-test/probe-chat.py [HOST] [PORT]` | what the server does with chat, asked the way no real client can: a spoofed sender, and a flood. `.claude/multiplayer/NETWORK-CHAT.md` |
| `~/mph-net-test/run-remote.sh HOST PORT SECONDS hunter...` | the same check against a server that is not on this machine -- which is the one that matters, since eight clients on one box measure the box |
| `~/mph-net-test/run-demo.sh SEC [authority\|client]` | record a demo from a scripted client and print what landed in the file. The authority is the case that matters: it is whichever client joined first, so it is normally whoever set the match up, and the server sends it no snapshots at all |
| `~/mph-net-test/run-rejoin.sh SEC LEAVE REJOIN [host] [port]` | the rejoin scenario, with a control: A hosts and leaves, the authority moves, then one client takes the vacated slot and another takes a fresh one. Prints what each took. `.claude/multiplayer/NETWORK-DIAGNOSTICS.md` |
| `~/mph-net-test/hard/run-all.sh` / `run-all2.sh` | the hard-case batch against the Pi: a ninth player, a line that goes away, 100-300 ms, packet loss, everybody spectating, everybody recording, a match boundary, an authority leaving, and a ramp to twenty-odd matches at once. `.claude/testing/TEST-HARD-CASES.md` |
| `~/mph-net-test/run-lag.sh MS SECONDS hunter...` | the same check against a loopback server behind `udp-lag.py`, which holds every datagram for `MS` before passing it on. A latency bug reproduced at a number you chose, rather than at whatever the internet is doing -- and the Pi answers in 7-17 ms, so it is the *worse* instrument for one |
| `MphRead -maptest "ROOM" -players 8 -seconds 22` | load one room with a full house, drive every player, and report what the map holds and whether it survived |
| `MphRead -maptest "ROOM" -players 8 -bots` | the same, but AI bots instead of the scripted tour -- a different code path, the only one that finds what only `PlayerAi` touches |
| `MphRead -maptest "ROOM" -renderprobe` | stand on every spawn point in the room in turn, read the frame, walk forward five seconds, read the worst. Catches a room that draws nothing -- the failure no other check can see, because everything else about it passes. `-shots DIR` writes the PNGs, `-allnodes` draws without room-part culling (which separates "the geometry is missing" from "the cull lost it"), `-hudshots` uses a real visible window and reads *its* buffer, which is the only capture that includes the HUD, and `-size WxH` sets that window's shape -- the HUD is laid out in a 4:3 space and stretched, so how it looks is partly a question about the window. Under WSL a HUD capture needs the X11 backend: `WAYLAND_DISPLAY=` `DISPLAY=:0`, or every window read comes back black |
| `MphRead -maptest "TEST ARENA" -players 8` | the harness's own room (`maps/arena/`): forty units square, eight spawns on a ring looking inward, nothing far from anything. Where damage, hit registration and the affliction states are actually measurable -- a real map's corridors mean most of the tour's shots land on a wall |
| `MphRead -rooms` | list every multiplayer room, one per line, for a shell loop. **27** is the whole cartridge and the right answer with no custom map source present; anything more is a custom map |
| `MphRead -q3convert FILE.pk3 -map LEVEL -name ROOM [-noclip]` | a Quake 3 .pk3 to a custom map in one command: textures baked from the level's own art, scale and extents picked from its geometry, spawns from its entities. Places no weapons or powerups -- where those go decides how the map plays. `.claude/mapgen/MAP-PIPELINE.md` |
| `MphRead -mapgen ["NAME"]` | generate the room binaries for the custom maps in `maps/` (recursively: a map may sit in a folder of its own with its level and textures beside it, or be a single `.fpmap` bundle), from the player's own textures. `-mapmaterials "ROOM"` prints what textures a room can lend. A map is a JSON file; the `.bin` it produces is never committed. `.claude/mapgen/MAP-PIPELINE.md` |
| `MphRead -mapbundle ["NAME"] [-mapdir DIR]` | cook a map into the one file it ships and is handed out as: recipe, level and baked textures in a `.fpmap`, with the level trimmed to the lumps the importer reads (376 KB for de_dust2, against 2.8 MB for the folder). What the workflow runs before it publishes -- the bundle is not committed, and the `.pk3` it is cooked from never reaches a package. `-mapdir` is resolved against the directory the command was typed in |
| `MphRead -gamepad [-seconds N]` | what a connected pad is doing, with no match in the way: its name, its axes, and which game action each button reaches. The only thing that tells "not connected" from "connected but GLFW has no mapping for it" from "the dead zone is eating it" apart. `.claude/GAMEPAD.md` |
| `MphRead -cel on\|off [-celbands N] [-celedge N]` / `-fog on\|off` / `-prohud on\|off` | render options for every path that never opens a launcher, which is every screenshot command. `.claude/render/CEL-SHADING.md` |
| `MphRead -fpscap N\|display` / `-interpolation on\|off` | how fast the picture is drawn, and whether the frames between simulation steps are blended. The simulation is pinned at 60 Hz on every setting, so neither touches what the game does. `.claude/render/FRAME-PACING.md` |
| `MphRead -frametimingcheck` | the fixed-step accumulator on its own, against frame times chosen rather than measured: does the game still run at 60.000 Hz when the screen runs at 144, at 165, at a jitter, or at 40. Needs no game files and no display |
| `MphRead -maptest "ROOM" -drawrate N` | draw each simulation step N times, which is what a 144 Hz screen does to a 60 Hz game. Asserts that drawing did not advance the world and that interpolation actually engaged. How the decoupled loop is checked from a box with no monitor |
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
| Settings | display, audio, controls, match rules, and profile (name, hunter, server addresses, updates, game files, credits). Also reachable from the pause menu during a match. **Pro mode HUD** is the whole HUD question in one switch -- no helmet, plain fixed crosshair, weapon list at 170%, fixed weapon, and its own energy, ammo and score readouts in place of the game's; off is the game as the DS drew it. The six settings it answers for have no rows at all, and the rows that remain have no explanations under them. Cheats, bugfixes, the leftover feature flags and the HUD-readout opacity likewise have **no UI** and no longer load from `settings.json` -- they sit at their code defaults |
| Game files | where the .nds goes. Shown first, and everything else greyed out, when there is nothing set up yet |
| Debugging logs | one line in the bottom right corner, under the version, on the front card only. Off; switched on it writes `logs/FruityPrime-<when>.log` beside the executable (the app's data directory on Android) with everything the program prints plus the machine, the driver, every model read and the stack of anything that kills it. What "it crashes when the map loads" is answered with. `.claude/DEBUG-LOGS.md` |

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
- **Interpolation is what makes the extra frames worth having.** Without it a
  144 Hz picture of a 60 Hz world is 60 positions a second shown twice, which
  is judder. `EntityBase` keeps its last two transforms and the blended one is
  *swapped into `_transform` and `_position` for the length of the
  `GetDrawInfo` call*, which is why none of the ten `GetModelTransform`
  overrides had to change. `CameraInfo` blends position and target and rebuilds
  the matrix with `LookAt`, never the matrices themselves.
- **A blend is declined whenever the two states are not two points on one
  path**: a jump over 24 units in a step, or a step more than four times the
  last one plus a unit. That is a teleport, a respawn, or a puppet being put
  where its owner says it is -- all of which must be drawn where they landed.
- **The game's speed no longer depends on the machine.** One call for both
  meant a box managing 40 fps played in slow motion; the accumulator pays what
  it owes, measured at 60.000 Hz with a 40 Hz draw rate.
- **Look for timers living in the draw pass.** Three were found by audit and
  all three are handled by counting steps owed rather than by moving code:
  `UpdateFade` (whose delay ends cutscenes and changes rooms -- it would have
  expired 2.4x early at 144 Hz), `ProcessEffects`, and the pause map's own
  animations. Frame advance is forced back to one step per picture, because the
  request to advance is consumed *after* the frame is drawn.
- **The on-screen FPS counter reports the picture**, not the simulation. The
  simulation rate is invisible to a player, which is why it goes to the debug
  log -- every five seconds, or immediately on a dropped step or a stall.

Default is **Display (VSync)**, the only tear-free setting; an explicit number
turns VSync off, since asking for 120 on a 144 Hz screen with VSync on gets 72.
OpenTK 4.9 no longer separates its update and render ticks, so `UpdateFrequency`
on the window is the frame rate and `RenderFrequency` is deprecated.

Full account, the interpolation rules, what is *not* interpolated, and how both
halves are tested without a 144 Hz monitor: `.claude/render/FRAME-PACING.md`.

## Updating

`Mods/Update/`. The program checks GitHub for a newer release on its own, says
so, and **installs nothing** — "Update now" opens the release page; download
and unpacking are the player's. It checks by itself because
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
| Server and directory | at startup, before binding | nothing — logs one line, keeps running (a server has no one at the keyboard to decide, and replacing its binary mid-match drops whoever is playing) |

`launcher.txt` carries `auto_update`, on by default; `-noupdate` turns it off
anywhere. A local build without the release workflow's version stamp reports
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

**`NetConfig.ProtocolVersion` is 4.** Any protocol change means server **and**
every client must be the same build — a mismatched client is refused outright
at Hello with a line in the server log, which is the intended outcome and not
a layout issue: the wire format doesn't move, an old client would read every
byte correctly and then simulate a different game (frozen in place, shooting
from its ankles) with nothing in the protocol to notice. Deploy the server
before handing out a client built against a new protocol. Publish commands and
the deploy script's env vars: `.claude/build-deploy/DEPLOY-SERVERS.md`.

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
  state instead of the cause, and the countdown still runs locally.
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

**Chat is T**, three lines top left in green on nothing, gone ten seconds
after they arrive -- which is why the frame counter now sits in the right-hand
corner. It draws with a font of its own (`Mods/Chat/ChatFont.cs`, pixel art in
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
rules are in `MECHANICS.md` at the repository root, regenerated with
`MphRead -mechanics`.
