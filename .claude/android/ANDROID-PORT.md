# Android — the renderer and the touch controls

The head is `src/MphRead.Android/`. It compiles the same sources as the desktop
project with `ANDROID` defined, and it now builds a match, not just a screen.
Two things had to be answered to get there: the engine draws through OpenTK's
desktop GL, and it reads a keyboard and a mouse. Neither exists on a phone.


## The renderer: one alias

The engine calls `GL.Something` from about 250 places in files that are
upstream's. Rewriting those was not an option -- everything this project adds
lives under `Mods/` so a pull from NoneGiven/MphRead stays a fast-forward. So
the Android head points the *name* somewhere else, in one line of its csproj:

```xml
<Using Include="MphRead.Mods.Render.GlEs" Alias="GL" />
```

A global using alias is resolved before the `using OpenTK.Graphics.OpenGL;` at
the top of those files, in every namespace of the compilation (checked: it wins
in `MphRead` and in `MphRead.Formats` alike). Not one call site changed, and the
desktop build never sees it. The enum types the call sites pass are still the
desktop ones -- they are the same GL constants -- and `GlEs` casts them across
to `OpenTK.Graphics.ES30`.

`Mods/Render/GlEs.cs` then answers the four things ES 3.0 does not have:

| Missing | Answer |
|---|---|
| Immediate mode | `Begin`/`Vertex3`/`End` accumulate into a vertex buffer. Quads, quad strips, triangle strips and fans become indexed triangles; `LineLoop` becomes lines |
| Display lists | `NewList`/`EndList` bake that buffer into a VBO, an IBO and a VAO. `CallList` is one `glDrawElements` -- the same trade the display list was making |
| The current colour | A vertex with no colour of its own takes the colour current at *execution* time, which is the `GL.Color3(item.Diffuse)` `DoMaterial` issues per render item. Vertices carry a flag; the ones without their own colour read the `imm_color` uniform |
| `glAlphaFunc` | The engine asks for exactly two comparisons, Equal 1.0 and Less 1.0. The fragment shader discards on them |

And one thing that is bookkeeping rather than emulation: the engine binds
texture names it made up itself (`_textureCount++`), which a compatibility
profile allows and ES does not. `GlEs` keeps a map from those to real names and
creates one on first bind, with a single high-water counter so `GenTexture`
keeps handing out the next number in the engine's own sequence.

**What is lost:** `glPolygonMode`, so the wireframe and collision-volume debug
views draw solid. Nothing a player sees uses it.

## The shaders

`Mods/Render/EsShaders.cs` holds the six shaders of `Shaders.cs` written for
`#version 300 es`. The desktop ones are GLSL 1.20 reading `gl_Vertex`,
`gl_Color`, `gl_Normal` and `gl_MultiTexCoord0` out of the fixed-function
pipeline -- which is *why* the desktop build needs a compatibility profile and
`MESA_GL_VERSION_OVERRIDE=4.5COMPAT`. The ES ones declare those four as
attributes at fixed locations 0-4 and are otherwise the same program, expression
for expression, plus `imm_color`/`a_color_set` and `alpha_test`.

`GlEs.ShaderSource` substitutes them by reference as the engine compiles. They
do not follow a change to `Shaders.cs` on their own, and a silent divergence
would be a rendering bug with no message anywhere, so each is checked against
the SHA-256 of the source it was written from and a mismatch throws with the
name of the shader that moved. Recompute with the extractor in the commit
message or by hand: normalise CRLF to LF first.

Verified with `glslang` (16.5.0) when they were written, and since then by
SwiftShader on the emulator, which compiled and linked all four programs --
main, RTT, the shift program and the cel shading ink pass, the last three
sharing the RTT vertex shader -- while drawing a room. That is a check that
they compile, not a check that the picture is right; see the note on
SwiftShader's own artefacts in `.claude/KNOWN-GAPS.md`.

## The loop

`GameView` is a **`SurfaceView` with an EGL context and a thread of its own**.
Everything the engine does with GL -- a room's textures, its baked geometry,
the shaders, the drawing -- happens on that thread, so the scene is *built*
there rather than by whoever asked for the match. Loading blocks that thread
for seconds, which is the right thread to block: the UI thread stays free and
the loading notice on top keeps drawing.

### Why not `GLSurfaceView`, which it used to be

**Because `GLSurfaceView` answers a window event by making the UI thread wait
for the GL thread.** `surfaceChanged` hands the new size over and then blocks
until that thread has been all the way round its loop; `onPause` and
`onResume` do the same with a handshake of their own. So any window event that
landed while a room was loading froze the UI thread for the length of the
load, and Android put its own "isn't responding" dialog over the black loading
screen -- a white box over a black one, with nothing to press, which is what
starting a match from portrait did.

Measured here, with a room load stretched to 12 s and a window resize injected
into it (`adb shell wm size`), against a heartbeat posted to the UI thread
every 200 ms:

| | worst gap on the UI thread |
|---|---|
| `GLSurfaceView` | **16,921 ms** |
| our own EGL and thread | 1,092 ms, and nothing during the load |

Android's input-dispatch ANR threshold is 5,000 ms.

Waiting for the window to hold still before creating the view --
`MainActivity.WaitForSteadyWindow` -- made that *rarer* and could never make it
impossible: a phone resizes its own window whenever it likes, for the system
bars, for insets, for a call. Now `surfaceCreated`, `surfaceChanged` and
`surfaceDestroyed` write a field and return, and the loop picks the change up
when it is next between frames.

Two things come free from owning the context:

- **It survives the surface going away and coming back**, so a match is not
  lost to the home button. `GLSurfaceView` only kept it as a favour, through
  `PreserveEGLContextOnPause`; here nothing destroys it until the match ends.
- **Pausing is a flag**, not a handshake, so `OnPause` cannot block either.

`surfaceDestroyed` is the one callback that *should* wait -- Android wants the
surface unused by the time it returns -- and it does, for up to two seconds.
Not longer: the render thread cannot answer from inside a room load, and
hanging the UI thread is the thing this class exists to stop. A swap against a
surface the framework has taken back returns false rather than crashing, which
is handled by letting go and waiting for the next one.

**The window should still have stopped moving before the match starts.** That
is no longer what keeps the app from freezing, but a match that starts
mid-rotation starts at the wrong size and re-lays-out under the player. So
`MainActivity.WaitForSteadyWindow` does not create the `GameView` until the
content view has held the same size for 250 ms and is landscape (or three
seconds have passed and the display clearly will not turn). Waiting for the
*configuration change* is not enough: it arrives before the window has been
laid out at its new size. The orientation is then pinned with
`ScreenOrientation.Locked` for the length of the load and released back to
`SensorLandscape` once the match is on screen.

**That wait is also the thing that can eat a start, and four ways it could
were fixed.** The front screen is hidden the moment `StartMatch` is called and
the `GameView` does not exist yet, so anything that delays the start is a
black screen with nothing on it -- which is what "it will not start a map"
looks like from the player's side. So:

- The three-second deadline is **no longer conditional on the window having
  settled**. It used to be `steady && waited >= RotateMs`, and a window that
  never held still left the loop posting itself for ever. Waiting is a nicety;
  starting is the job.
- The loading notice goes up in `StartMatch`, not in `BeginMatch`, so there is
  never a black screen with nothing on it. `ShowNotice` is shared and
  `BringToFront`s it once the touch overlay is in.
- **Back cancels a pending start** (`CancelPending`) and puts the front screen
  back, rather than doing nothing because `InMatch` is still false.
- **A second `StartMatch` while one is pending is refused.** It used to
  overwrite `_orientationBefore` with the landscape it had just asked for, so
  `EndMatch` would restore *that* and leave the front screen stuck sideways
  for the rest of the session -- and pressing START twice is what a player
  does when the first press seems to do nothing.

There is also a hard 8 s give-up for the one case that cannot be started from
at all -- a content view that never reports a size, which would build a scene
for a zero-pixel window -- and it says so on screen instead of starting
something the player can neither see nor leave. Every decision logs one
`[android]` line with the sizes involved.

The order is the desktop's: `GameState.ApplyPause()`, `Scene.OnUpdateFrame()`,
`Scene.OnRenderFrame()`, `Scene.AfterRenderFrame()`, then `eglSwapBuffers`.

The pacing is not. `RenderWindow` asks OpenTK for 60 updates a second; here the
buffer swaps at the display's rate, which on a modern phone is 90 or 120, and
**the update is the frame**: the render item lists are built during the update
and cleared after the draw, so a render with no update in front of it draws
nothing. Rendering more often than the game ticks is therefore not an option --
the thread waits for the next 60 Hz tick instead.

## Frame rate

The match loop is decoupled the way the desktop's is: the simulation runs at
exactly 60 Hz on `FrameTiming`'s accumulator and the picture runs at the FPS
limit, up to 240. `GameView.RenderLoop` used to sleep to a hard `1.0 / 60.0`
around one `OnUpdateFrame`, which is why a 120 Hz phone drew 60.

Three things are specific to this head:

- **Input goes inside the step loop**, not beside it. `ApplyInput` works out
  this step's rising edges from the touch state, so running it once per
  *picture* would turn one tap on FIRE into two presses at 120 Hz.
- **In display mode the loop does not sleep.** `eglSwapBuffers` blocks until
  the panel is ready; sleeping as well is double pacing and would halve the
  rate. `MinFrameSeconds` is only a floor, so a driver that does not block (an
  emulator, a surface with no vsync) spins at 500 Hz rather than flat out.
- **`Surface.SetFrameRate`** (API 30+, guarded and caught) tells SurfaceFlinger
  what the surface intends, because a 120 Hz phone often sits at 60 until
  something asks. Below API 30 the FPS limit still caps the loop; it just
  cannot raise the panel. Best-effort throughout -- a device that refuses is
  not a reason to end a match.

The setting is the launcher's own **FPS limit** row, shared with the desktop,
so it needs nothing of its own here.

**None of this has run on a device**, for the same reason nothing else in this
head has: the emulator available here has no extracted game files and cannot
load a match. It builds, and the code under it is the code the desktop
measurements were taken on. Treat it as untested.

## The controls

No hook in the engine's input path, and none needed. `ProcessAllInput` reads a
`KeyboardState` and a `MouseState` once a frame and turns them into the
`Keybind` flags the player code reads. `AndroidInput` hands the scene a keyboard
and a mouse of its own and presses the keys the player has bound. OpenTK does
not let anyone else build those two types, so the constructors and setters are
reached by reflection, once, into delegates.

Three things come free from doing it that way:

- **Rebinding works.** A thumb on FIRE presses whatever `Controls.Shoot` says,
  key or mouse button.
- **Sensitivity and inverted aim work**, because aiming moves a pointer and the
  engine reads the same delta it always did.
- **The weapon wheel works**, because it was a touchscreen mechanic on the DS:
  `UpdateWeaponSelect` reads the pointer's absolute position. While WEAPON is
  held the pointer *is* the finger; the rest of the time it accumulates drag.
- **The wheel is picked with the same thumb that opened it**: press WEAPON,
  drag into the weapon, let go. This needs one exemption from a general rule
  --- a thumb that slides off a button releases it, which here closed the
  wheel before anything could be chosen --- so `_wheelPointer` skips that
  check exactly as `_fireAimPointer` already did for FIRE's aim drag, and
  `WeaponWheelPosition` hands the wheel that finger's position. Before this,
  picking took a second finger, and the finger people lifted to do it was the
  one on the stick: choosing a weapon meant standing still. The two-finger
  way still works and still wins when both are down, because a second finger
  put down while the wheel is open is an explicit choice.

Layout (`TouchControls`, drawn by `TouchOverlayView` with a Canvas rather than
in GL -- it is a dozen circles that change when touched):

| Control | Bind |
|---|---|
| Floating stick, left half | `MoveUp/Down/Left/Right` **and** `RollUp/Down/Left/Right`, eight-way. Both sets, because walking reads one and the morph ball the other |
| FIRE | `Shoot` |
| JUMP | `Jump` **and** `Boost` -- one button on the DS, and the same key by default: jumping on foot is boosting in the ball |
| MORPH | `Morph` |
| ALT | `AltAttack` |
| WEAPON | `WeaponMenu`, held, with the pointer following the finger -- including that same finger once it drags off the button |
| ZOOM | `Zoom` |
| MENU | `Pause` |
| Anywhere else on the right | aim |

Drag is converted to density-independent pixels before it becomes pointer
movement, so a swipe turns the same amount on any phone; `GameView.AimScale` is
the one number to change if it feels wrong, and the player's own mouse
sensitivity scales it after that.

### The controls step aside for a pad, and come back at a touch

There is no setting for it, and there was one — "Use a connected gamepad",
now gone. Being *connected* was never the question a player was asking: a pad
paired with the phone for something else is still paired while they play with
their thumbs. What `TouchControls.NotePadActivity` watches is the pad actually
being **used** — a button down, or a stick past the dead zone, which is what
`GamepadInput.InUse` answers — and the answer is reversed by the next finger
on the glass.

**Hiding the layout hides the layout, and nothing else.** The screen keeps
working the whole time the pad is in use: both are live at once, and that is
the point — a stick in one hand and a thumb on FIRE is a normal way to hold a
phone, and the weapon wheel cannot be reached from a pad at all (it reads an
absolute pointer position; see GAMEPAD.md), so a player who wants it has to be
able to touch the screen without first paying a press to wake the controls up.

Two things that are easy to get wrong here and are not:

- **The view stays; only the drawing stops.** `TouchOverlayView` is still the
  only thing receiving touches, so a touch while the controls are invisible is
  an ordinary one: it does what it landed on *and* brings the layout back.
  Making the view `Gone` would hand every touch to whatever is underneath and
  there would be nothing left to bring them back with.
- **Nothing is released when the layout goes away.** It used to be —
  everything held was let go, because a thumb resting on FIRE as the player
  picked the pad up would otherwise be held for ever, *the finger's release
  landing on a control that had stopped listening*. Nothing stops listening
  any more, so the release arrives and does its job; cancelling would instead
  cut off a player deliberately holding a button with one hand and a stick
  with the other.

Both of those were the right answer to "the player has put the pad down and
gone back to the glass", and the wrong one to "the player is using both",
which is the commoner case. Changed 2026-09-04 on that report.

**The one exception is a dialog box.** `PlayerDialog.CheckButtonPressed` reads
`Input.ClickX/Y` — the OK button is a *position*, because on the DS it was a
touch screen — and `GamepadInput` deliberately drives no pointer. So
`GameView.ApplyInput` sets `TouchControls.ForceVisible` from
`GameState.DialogPause` every frame, pad or no pad. Without it the story stops
at the first scan, with nothing on screen to press. A flag the loop keeps
setting rather than a one-shot "show yourself": a thumb still resting on the
stick would otherwise put the controls away again on the very next motion
event, and the box would flicker for as long as the pad was held.

Pad events reach the state through `MainActivity.DispatchKeyEvent` and
`DispatchGenericMotionEvent` rather than through `GameView` — a pad has to
reach the settings screen too, where no `GameView` exists, so that a button
can be rebound. `GameView`'s own handlers are kept as a fallback and never see
a pad event in this app.

### The player chooses which buttons exist

Settings → Controls → **On-screen buttons**: a master switch and one toggle per
button, applied in `ApplyLayoutLocked` as an `AND` over whatever the situation
had already decided. The state lives in core, in
`Mods/Input/TouchSettings.cs`, because the settings screen is shared code and
`TouchAction` is this head's own type; `TouchControls.SettingOf` maps between
them. It is saved in `controls.txt` with the rest of the controls.

It exists because of a report, and the report is the design rationale: *"the
on-screen touch commands interfere with the aiming and it's very easy to
accidentally hit those."* They do, and no layout fixes it -- aiming is a drag
anywhere on the right of the screen, the buttons have to be reachable, and a
drag that starts inside a circle presses the circle. A button turned off is
drawn nowhere **and takes no touch** (the hit test in `Down` already skipped
invisible buttons), so the glass it occupied becomes aim.

Turning every one of them off cannot strand a player: movement is the stick,
which appears wherever a thumb lands on the left; aiming is a drag; jump is a
double tap on the aiming side; boost in the ball is a flick. None of the four is
a button.

`MainActivity.ClosePauseMenu` calls `TouchControls.ReloadSettings`, since the
settings are reachable from the pause menu mid-match.

### Controls no longer reset when the app closes

Two faults, both of them "the desktop does this somewhere this head never runs":

- **Nothing called `InputSettings.Load`.** It is called from
  `ModEntry.TryHandleHeadless`, which is the desktop's every-invocation entry
  point and is not on this head's path at all. `AndroidApp.BuildHome` calls it
  now, after `LauncherPrefs.Load` names the directory.
- **`controls.txt` was written to `AppContext.BaseDirectory`**, which an Android
  package does not own. Every write was refused, and `InputSettings.Save`
  swallows the exception deliberately (it runs from the pause menu, on the
  thread the menu is on). It follows `LauncherPrefs.Directory` now — the same
  property the head already points at the app's data directory for
  `launcher.txt` and `paths.txt`.

The symptom was "control settings still reset to default when quitting the
game", and the shape to remember is that **a swallowed write error and a load
that never runs look identical from the outside**.

### Spectating

The spectator's screen is the same dozen circles doing different jobs, and
`TouchControls.ApplyLayoutLocked` is the one place that decides which are on
and what they say — visibility used to be set by each of the three callers
reaching into the button list for the ones it knew about, which only works
while no two of them care about the same button, and spectating cares about
nine.

| Button | While spectating |
|---|---|
| FIRE → **NEXT** | next player. The desktop's left click |
| VISOR → **VIEW** | the map camera, or the player being watched. The desktop's Space |
| JUMP → **UP**, MORPH → **DOWN** | the free camera only, and only shown on it |
| Left stick | drives the free camera |
| Aim drag | turns the free camera |
| SCORE | the scoreboard, the one control a spectator keeps |
| everything else | gone |

**The free camera is not driven through binds**, and that is the part worth
knowing. It is the room viewer's Roam camera (`Scene.SetFreeCamera`), which
`Scene.OnKeyHeld` moves by reading W/A/S/D, Space and V **off the keyboard
directly** and `Scene.OnMouseMove` turns. So `GameView.Spectate` presses those
letters through `AndroidInput.ApplyKey` and hands the aim drag to
`OnMouseMove` — the same call the desktop's mouse makes, so the player's own
sensitivity and inversion apply. The letters are a copy of that method's table
and the one place this can go out of step with it.

Before this, the spectator branch pressed nothing and threw the aim delta
away: the free camera opened and could not be moved, turned or left.

`SpectatorMode.Rejoin` never puts a score *up*. A score below zero is a
penalty — a suicide costs a point — and clearing it by spectating for a second
would make the pause menu the cheapest way out of one, so only a positive
score goes back to zero. That is shared, not Android's.

## Playing online

`AndroidMatch.Build` is the head's half of `MatchStart.Launch`, and it was
missing the networked half of it: it loaded `plan.RoomKey` and filled the slots
with local players whatever the plan said. An online plan carries an **empty
room key on purpose** — the front screen joins before it knows what is running —
so joining a server failed with *"No room with this name is known"*, which is
what an empty string is. It now defers to `NetLaunch.ServerRoom()` for the map
*and the mode*, builds the slots with `NetLaunch.BuildPlayers`, and loads with
`NetLaunch.RoomPlayerCount` so every client lays out the same world.

## Game files

The package's own directory is read-only, and the extracted game files are
hundreds of megabytes the player has to copy onto the device themselves. So both
`LauncherPrefs.Directory` and `GameFiles.Root` (added for this) point at
`GetExternalFilesDir(null)` -- the directory reachable over USB under
`Android/data/fr.livetek.fruityprime/files` without the app asking for a storage
permission -- and that is made the working directory, because upstream's `Paths`
reads `paths.txt` relative to it.

Extract on a desktop, then copy `paths.txt` and the extracted folders there. The
front screen says so when they are missing and has a "Check for game files"
entry, since they arrive while it is already up.

## Sound

Both halves play, and neither of them the way the desktop does it.

- **Music** goes through SoundFlow, which ships `libminiaudio.so` for Android;
  it is in the APK.
- **SFX** used to be silent, and the reason is worth keeping: they go through
  OpenAL, OpenTK ships the bindings only, and `Silk.NET.OpenAL.Soft.Native` —
  where the desktop gets its native — has runtimes for Windows, Linux and macOS
  and none for a phone. `Sfx.Load` caught the missing library, installed the
  silent stub, and the game played its music with nothing else. That is exactly
  the split a Windows machine with no `oalinst.exe` shows, and it was fixed
  there the same way it has now been fixed here: give the name something to
  resolve to.

  **The renderer's trick, again.** Two using aliases in the csproj point `AL`
  and `ALC` at `Mods/Sound/AlEs.cs`, for every file in the compilation, and not
  one of the fifty-odd call sites in `Sound/Sfx.cs` and `Formats/Movie.cs`
  changed. Behind the name is `Mods/Sound/SfxMixer.cs`, which does what OpenAL
  was doing — buffers, voices, per-source gain and pitch, SOFT loop points,
  buffer queues, the linear-clamped distance model and stereo placement from
  the listener's orientation — and hands one block of stereo at a time to
  miniaudio, on the **music's own playback device** (`MusicPlayer.Engine` and
  `MusicPlayer.PlaybackDevice` were added for this). A second device would be a
  second audio callback, a second buffer of latency and one of the two ducked.

  Not emulated, because the engine never asks: HRTF, doppler, velocity, effects.

## Map previews

Rendered on the device from the player's own extracted files, and nothing about
them is shown. Three things used to be wrong and all three are the same
mistake — doing it on a `GLSurfaceView`:

- `OffscreenGl.cs` creates an EGL **pbuffer** context on an ordinary thread, so
  there is no view, nothing on screen, and no forced landscape. The scene never
  draws to the pbuffer anyway: `Scene.ReadSceneTarget` reads the renderer's own
  framebuffer object, and EGL simply will not make a context current without a
  surface.
- **Several at once, in processes of their own.** A scene is not a local thing —
  the entity lists, the player roster and the game state are static, so two
  scenes in one process would be one world with two cameras. The desktop
  answers that with ten worker processes and so does this: `PreviewService.cs`
  declares six services with `android:process`, each handed a share of the room
  list round-robin, each dropping a marker file when it is done. The launcher
  watches the cache directory rather than talking to them, which needs no IPC
  and survives a worker being killed for memory. Whatever they could not
  produce is rendered in-process afterwards, so a device that refuses to start
  services still gets its pictures.
- **640x360, not 1600x900.** Every pixel is paid for four times over — fill
  rate, a `glReadPixels` stall, a managed pixel loop in `AndroidPng`, and a PNG
  encode — and the launcher shows them in a 248-point band.

### ⚠️ The settings file follows `ChooseRoot`, and it is not always the SD card

`GameState`'s save folder is the relative path `Savedata`, resolved against
the working directory, which `CustomizeAppBuilder` sets to whatever
`ChooseRoot` picked. Both candidate roots can end up holding a `paths.txt`
from earlier runs, and the one that wins is then the only one the game reads.
An emulator here chose the **internal** directory
(`/data/user/0/fr.livetek.fruityprime/files`) while a hand-written
`settings.json` sat unread in the external one, and three rounds of "the ES
renderer draws cel shading fine" were measured with cel shading off.

`[android] N bundled map files -> <path>` names the root it chose, in the
first seconds of every launch. Read it before trusting anything pushed with
`adb push`, and push to that path.

### ⚠️ `ThumbnailMode` is process-wide

`Mods/ThumbnailMode.cs` suppresses the HUD and mutes the sound. The desktop
never had to leave it: every capture is a worker process that exits when its
picture is written. Android renders previews in the app's own process, so
entering and never leaving meant **the next match had no HUD and no sound** —
which is exactly how it was reported. `ThumbnailMode.Exit()` exists for that,
`PreviewRun` calls it in a `finally`, and `MainActivity.StartMatch` refuses
while an in-process run is going.


## Custom maps

They reach the phone as **`.fpmap` bundles** and could not reach it any other
way: `AndroidAsset` was globbed as `maps\*.json` and `AssetManager.List` does
not recurse, so a map that keeps its level in a folder of its own -- which is
how every converted map is worked on -- matched neither side and the phone
listed the 27 cartridge rooms. A bundle is one file at the top of `maps/`, and
`AndroidMaps.Install` unpacks it into the external directory beside the
extracted game files, where a player can also drop one of their own over USB.
The room binaries are still built on the device, from that player's own
textures; `AndroidMaps.EnsureBuilt` is what runs the builder before a match.

## Demos

Watching a recorded match works here too, and needed three things the desktop
never had to think about:

- **The system file picker cannot reach this app's own recordings at all.**
  Since Android 11 `Android/data` and `Android/obb` are excluded from the
  Storage Access Framework: the picker cannot be pointed at the folder
  `DemoRecorder` writes to, and a player cannot navigate to it either. None of
  that stops *this* app reading the folder -- it owns it and needs no
  permission for it, which is the whole of the confusion. So the Demos entry
  lists the folder itself (`DemoLibrary` and `DemoPickerView`) and the system
  picker is one entry inside that list, for a demo that came from somewhere
  else. `SuggestedStartLocation` is still set for the desktop, where it is
  honoured; on Android it is ignored, and it used to be handed a *relative*
  path, which no storage provider anywhere could resolve.
- **The file picker cannot filter by pattern.** Android filters by MIME type
  and a `.fpdemo` has none, so `FileTypeFilter` is skipped here exactly as it
  is for the `.nds` picker -- setting one produces a picker in which every file
  is refused.
- **There is no path behind what it hands back.** A `content://` document has
  no local path, and `DemoPlayback.Join` takes one, so `HomeView.ChooseDemo`
  copies the document into `GameFiles.Root` and plays it from there. Kept
  rather than deleted afterwards, unlike the cartridge copy: the reader holds
  the file for the whole session, and the next demo overwrites it.

### Where a recording lands

`DemoRecorder.Start` writes to `Paths.Combine(Paths.Export, "_demos", name)`,
and `Export` is **empty** in every `paths.txt` a desktop extraction produces.
An empty first element makes that a *relative* path, resolved against the
working directory -- which `CustomizeAppBuilder` set to whatever `ChooseRoot`
picked. So on a phone a recording is:

```
<root>/_demos/<room>_<yyyy-MM-dd_HH-mm-ss>.fpdemo
```

with `<root>` normally the external files directory, reachable over USB at
`Android/data/fr.livetek.fruityprime/files/_demos/`. The
`[android] N bundled map files -> <path>` line names the root that was
actually chosen; on a device that fell back to internal storage the folder is
under `/data/user/0/...` and only `adb` can reach it.

That path is worth having: no file manager on a modern Android can open the
folder, so copying a recording off the device means USB/MTP or `adb`. It is
written on screen in exactly one place -- the Demos list, when there is
nothing in it yet. The "saved to..." line at the end of a recording is still a
`Console.WriteLine`, which is logcat here and invisible to a player.

`DemoLibrary.List` reads the room and the moment back out of the file *name*
rather than opening anything: the name already carries both, and reading a
header out of every file to learn what the name says would turn a directory
listing into a disk full of seeks. The timestamp is a fixed nineteen
characters at the end, which is what makes it safe to split -- a room name can
contain underscores of its own, since `SanitizeFileName` puts one in for every
character a file name cannot hold.

`AndroidMatch.BuildDemo` is the rest -- the half of `MatchStart.LaunchDemo`
that is not a window: join, read the room out of the recording's own
MatchState, build the players with `localSlot: -1`, load the room.
`MainActivity.EndMatch` calls `DemoPlayback.Stop`, which `NetSession.Stop`
does not do for it: a demo feeds the session from a file rather than a socket.

## Building

```bash
export PATH="$HOME/.dotnet:$PATH"
export JAVA_HOME=$HOME/jdk17            # a JDK 17; the workload does not bring one
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1   # or install libicu
dotnet workload install android
dotnet build src/MphRead.Android/MphRead.Android.csproj -c Debug \
  -p:AndroidSdkDirectory=$HOME/android-sdk
```

The SDK needs `platforms;android-35` and `build-tools;35.0.0` to match the
`net9.0-android35.0` target; `sdkmanager --sdk_root=$HOME/android-sdk` installs
them. `EnableAvaloniaXamlCompilation=false` is deliberate and explained in the
csproj.

The APK lands in `bin/Debug/net9.0-android35.0/fr.livetek.fruityprime-Signed.apk`
(~20 MB; a Release publish is ~45 MB, being every ABI with the trimmer run).
`adb install -r` it.

Nobody has to do any of that to get one, though: `build.yml` has an `android`
job on every push, and its **FruityPrime-android** artifact holds the release
APK and an INSTALL.txt. `release.yml` puts `FruityPrime-<tag>-android.apk` in a
tagged release. Both are signed with the SDK's debug key -- enough to install,
not enough for a store.

The untrimmed Debug APK is no longer built or collected: it existed to tell a
crash apart from the trimmer having removed something, which is worth building
when that question is being asked and not worth handing out beside the real one
every push. Build it by hand when you need it -- and with
`-p:EmbedAssembliesIntoApk=true`, or it holds no managed code.

**The trimmer is why `Properties/TrimmerRoots.xml` exists.** A Release publish
really does run ILLink over this app, and every use of `KeyboardState` and
`MouseState` is through a `MethodInfo`, so without the descriptor the touch
controls work in Debug and throw on the first frame of a release APK. Checked,
not assumed: the members survive in
`obj/Release/.../android-arm64/linked/OpenTK.Windowing.GraphicsLibraryFramework.dll`.

## Three traps that cost a release each

All three were found by running the APK, and none is visible from a build log.

**The activity's theme must be an AppCompat descendant.** Avalonia's
`AvaloniaMainActivity` is an AndroidX `AppCompatActivity`, and AppCompat throws
`IllegalStateException: You need to use a Theme.AppCompat theme (or descendant)
with this activity` out of `onCreate` under anything else. The head shipped with
`@android:style/Theme.Material.NoActionBar`, a framework theme that looks right
and can never work: the app died before a line of this project's code ran, every
time, on every device. `Resources/values/styles.xml` holds the theme now.

**A Debug APK built by `dotnet build` contains no managed code.** Debug defaults
`EmbedAssembliesIntoApk` to false and expects `dotnet build -t:Run` to push the
assemblies over adb afterwards. Install that APK by hand and it aborts at
startup with *"No assemblies found in .../.__override__/<abi>. Assuming this is
part of Fast Deployment."* The build succeeds, the APK installs, and it is
hollow. `-p:EmbedAssembliesIntoApk=true` is what makes a debug APK someone can
be handed; it is ~110 MB rather than 20.

**The synchronous `HttpClient.Send` does not exist here.** .NET for Android
defaults `UseNativeHttpHandler` to true, which puts
`Xamarin.Android.Net.AndroidMessageHandler` behind every `HttpClient` -- and
that handler implements `SendAsync` and nothing else, so the synchronous call
falls through to `HttpMessageHandler.Send`, whose whole body is a throw. The
update check used it. On a phone it therefore never reached GitHub at all: it
caught the `NotSupportedException`, wrote "could not reach GitHub" into
`UpdateCheck.LastReason`, and the front screen -- which only shows the corner
when a release was *found* -- said nothing, so a phone sat on v0.3.0 with
v0.3.1 published and no way to tell that apart from being up to date. The
desktop was fine throughout, because `SocketsHttpHandler` implements the
synchronous path. `Mods/Update/SyncHttp.cs` is the one-line answer, and the
shape to watch for is any synchronous `HttpClient` or `HttpContent` call in the
shared sources: it compiles on this head and throws on the device.

## Text input

Nothing on this head read a key event until chat needed one. `GameView` is
focusable now and handles `OnKeyDown`, which covers a keyboard plugged in,
paired over Bluetooth, or the emulator's -- and, through
`OnCreateInputConnection`, the soft keyboard the CHAT button asks for.

Three things that are not obvious, all in `GameView`:

- **`InputTypes.Null`** on the `EditorInfo` is what makes an IME send plain
  key events instead of `commitText`. Without it the view has to keep an
  editable buffer in step with `ChatBox`'s, which is two copies of one string.
- **`NoFullscreen | NoExtractUi`**, or the IME covers the match with its own
  full-screen text box in landscape.
- **A key event carries its own character** here, where GLFW raises a key
  callback and then a character callback for one press. So the desktop's
  "swallow the opening character" step must be turned *off* on this path
  (`ChatBox.Open(swallowOpeningChar: false)`), or the first real letter is
  eaten instead.

Focus and touch are separate: the touch overlay sits above `GameView` and takes
every touch, and never takes focus, so the view keeps the keys.

Full account of what chat is and what the server does with it:
`.claude/multiplayer/NETWORK-CHAT.md`.

## Testing it here

An emulator runs this without a device, and without KVM -- which WSL does not
grant unless the user is in the `kvm` group:

```bash
sdkmanager --sdk_root=$HOME/android-sdk "emulator" "system-images;android-30;default;x86_64"
avdmanager create avd -n fruity -k "system-images;android-30;default;x86_64" -d pixel_4
$ANDROID_HOME/emulator/emulator -avd fruity -no-window -no-audio -no-boot-anim \
  -gpu swiftshader_indirect -accel off -memory 4096 -cores 4
```

`-accel off` is software CPU emulation: it boots in about five minutes and the
system UI shows "isn't responding" dialogs of its own, which are the emulator
and not this app.

Four things that cost time here and are not obvious:

- **`adb push` into the app's external directory is refused** on API 30 --
  scoped storage, `Permission denied`, even for a directory the app itself
  writes. `adb root` first (the `default` system image allows it; a
  `google_apis` one does not), and it works.
- **`adb push --sync`** for the 100 MB of extracted files. A push that dies
  half way otherwise starts again from nothing.
- **`pm list packages` finding the package proves nothing** -- an install from
  a previous session leaves it there, so a wait loop on that returns
  immediately and the thing under test is last week's APK. Wait on `adb
  install` actually exiting, and check `dumpsys package ... lastUpdateTime`.
- **A 112 MB debug APK takes several minutes to install** on a software CPU,
  and `cmd: Can't find service: package` means the system is still coming up
  rather than that anything is wrong. `sys.boot_completed` is never set on
  this image; `init.svc.bootanim` going `stopped` is the signal, and the
  package service is up a while after that. `adb install -r`, `adb shell am start -n
fr.livetek.fruityprime/crc64e2a07749a868b9fd.MainActivity`, and `adb shell
screencap` are enough to see the front screen. With KVM it would be seconds
rather than minutes.

SwiftShader implements GL ES 3.0, so it is a real check that the shaders
compile, link and run. It is **not** a check of any picture, and there is now a
number for how far off it is: the cel outline pass measures a flat surface's
depth kink at 0.004-0.009 under llvmpipe on the desktop and at 235-256 here,
against a threshold of 1.1. The large-scale depth field is right -- the room's
shape is plainly visible in a `fract(d * 64.0)` probe -- and the per-pixel
values are not, which is the same defect that streaks its colour. Anything
that reads neighbouring pixels and compares them will be nonsense on this
renderer.

## What has run and what has not

A room has now been loaded on the emulator: front screen, offline match, the
touch controls and the HUD, from a cold start in portrait -- repeatedly, and
including a second match started straight after backing out of the first
(which is two rotations in quick succession) and a double tap on START. The
portrait launch could **not** be made to fail here, on API 30 at 1080x2280,
with auto-rotate both on and off; the hardening above comes from reading the
wait rather than from reproducing a failure in it, and a device still failing
should be asked for its `[android]` lines. What the emulator
cannot answer is what any of it *looks* like -- SwiftShader draws this scene
with vertical streaks through every surface, with cel shading on and off alike,
so the pictures are only good for "it ran". The desktop build is where the
rendering is judged.

See `.claude/KNOWN-GAPS.md`. The first run on a new device should still be
watched for, in this order:

1. `[gles] shader ... failed to compile` in logcat. The compile status is only
   read under a debugger upstream, so `GlEs.CompileShader` logs it.
2. `Scene.FramebufferStatus` -- the offscreen target is an unsized `GL_RGB`
   colour texture with a `DEPTH24_STENCIL8` renderbuffer. RGB8 is
   colour-renderable in ES 3.0, but a driver that disagrees makes the whole
   picture black while every other signal looks fine.
3. Geometry that is inside out or missing: that is the strip/quad/fan winding in
   `GlEs.EmitIndices`, which is the one piece of this with no test behind it.
4. Untextured or wrongly-coloured meshes: that is the `imm_color`/`a_color_set`
   path, i.e. a mesh whose display list never set a colour of its own.
