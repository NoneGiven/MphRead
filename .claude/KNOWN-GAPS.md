# Known gaps — claims not yet verified

What's below is unproven or partially proven, not broken. Say so rather than
claiming coverage that isn't there.

- **With the server as the authority, nobody gets the snapshot-based form
  correction any more.** `NetPlayerBridge` reconciles a puppet's alt form
  against `IntentButtons.AltFormState` only on the authority -- deliberately,
  because a client doing it as well would take corrections from the owner's
  intent and the authority's snapshot at once. When the authority was a
  player, that player's own view of everybody was corrected; now the authority
  is not a player, so every client converges by replaying presses alone.
  Measured against the Pi with 150 ms injected on two of three clients, this
  shows up as one client reporting a remote player in the wrong form for 78
  consecutive frames. It is **not new** -- the relay control does not report
  it only because the client that would have is the authority and is exempt
  from the check -- but it is now everybody's. Whether clients can safely
  reconcile form from the snapshot once the authority is not a player is an
  open question, and one to settle with `run-remote-lag.sh` rather than by
  reasoning. See `.claude/multiplayer/NETWORK-SERVERAUTH.md`.
- **The scoreboard crash reported in bot matches is not reproduced here, and
  is therefore not fixed.** Reported from a phone, 2026-09-06: *"in bot matches
  the game still sometimes crashes when trying to view the scoreboard."* The
  scoreboard is now drawn on every `-maptest` run (`ModForceScoreboard`, two
  windows per run, a `MAPFAIL` if it never drew) and it was swept over all
  twelve game modes, 2 to 8 players, bots on, pro HUD on and off, and forced
  into `MatchState.Ending` — no crash anywhere, on the desktop. Two real
  defects **were** found on that path and fixed, and either could plausibly be
  it, but neither is confirmed as the cause: `GameState.Reset` cleared every
  per-slot array except `Nicknames` and `Stars`, so an offline match drew the
  previous *networked* match's roster; and `DrawText2D` indexed the font's
  width and offset tables with `ch - MinCharacter` unchecked in all four
  alignment branches, which is a crash for any character the loaded font does
  not cover — on a screen that draws eight names at once. What is needed to
  close this is a debug log from the phone it happens on: the render thread
  already catches and prints the whole exception (`GameView.Run`), so the
  stack is one switch away.
- **The raw gamepad fallback has never been held against a real unmapped
  pad.** `GamepadLayout`'s two shapes are written from the layouts SDL's own
  database uses for them, and the mapping-file path
  (`gamecontrollerdb.txt`, `SDL_GAMECONTROLLERCONFIG`) is exercised only by
  code inspection: this box has no `/dev/uinput` to fake a third pad with, and
  the virtual-pad recipe in `GAMEPAD.md` needs root. What is proven is that a
  mapped pad still takes the mapped path, since that code is unchanged. When a
  player reports buttons in the wrong places, `-gamepad` prints the mapping
  line to correct rather than a shrug.
- **Disruption over the wire is implemented and unmeasured.** `FlagBurning`
  was measured crossing (255 frames on the victim's own machine against the
  authority's 299, Kanden vs Spire, 70 s); `FlagDisrupted` is the same
  mechanism, the same shape and the same call site, and the scripted tour
  simply never landed a charged Volt Driver -- 21 hits in that run and not one
  of them disrupted anybody. The feature check counts it now, so the next run
  that manages one will say so.
- **Changing hunter between lives is proven offline and not in a match.** The
  swap itself was measured in a `-maptest`: requested while alive at frame
  241, still Samus through 500 frames of damage, dead at 781, back at 961 as
  Sylux on 99 energy in suit 2. What that run cannot show is the other half --
  the re-Identify, the roster, and every other client's `NetSlotManager.Sync`
  picking up the new hunter -- which needs two real clients and somebody
  opening the pause menu.
- **The enemy portrait's size was fixed by measurement, not by a picture of
  the fault.** `DrawHudObject`'s mode 0 was measured stretching a 32-unit
  sprite to 20.7, 27.7 and 36.3 units tall at 4:3, 16:9 and 21:9 (the weapon
  icon, three captures), which is what puts the opponent portrait over the
  name below it; the portrait itself is drawn for two seconds after a hit and
  the sampler never caught one.
- **The Windows half of the server's self-update is unrun.** The rename-aside
  path is written to Windows' own documented behaviour -- a running image
  cannot be deleted but can be renamed, since the mapping follows the file --
  and there is no Windows machine here to watch it happen. What *is* measured
  on this box: the Unix path is unchanged (delete then rename, as before), and
  the startup sweep really does delete a `.fp-old` and a `.incoming` left in an
  installation. The first Windows server to take a release is the test.
- **The one launcher has never run on Windows or macOS.** Same code on all
  three desktops now, but the only machine that's shown it is this WSL box
  (front screen, settings, map grid, pause menu — driven and screenshotted
  over X11). Windows changes two things this can't check: it's a GUI binary
  with no console, and GLFW/Avalonia share a message queue instead of two X
  connections.
- **Nobody has played a match from the launcher window.** It starts one and
  the launcher window goes away when it does (checked), but this box can't
  show a GLFW window at all (`Scene.OnRenderFrame` never produces a frame
  under its GL), so "Escape opens the pause menu over a running match" is
  proven on the menu's side (flags, windows, the pump) and unproven on the
  game's.
- **macOS is cross-compiled and unrun.** See `.claude/launcher/LAUNCHER-OVERVIEW.md`.
- **The Android match runs on an emulator; how it *looks* there proves
  nothing.** With the game files copied onto the device, an emulator (API 30,
  x86_64, software CPU and SwiftShader) has been driven front screen → offline
  match → first person with the HUD, from a cold start in portrait. So
  `Mods/Render/GlEs.cs` — immediate mode, display lists, the current colour,
  the alpha test — does load a room and draw it. But SwiftShader puts vertical
  streaks through every surface in that build, with cel shading on and off
  alike, so every picture from it is good for "it ran" and for nothing else.
  Rendering is judged on the desktop. `.claude/android/ANDROID-PORT.md` lists
  what to watch on a first run, in order.
- **The portrait freeze is reproduced and fixed; the fix is proven by
  measurement, not by playing.** A room load stretched to 12 s with a window
  resize injected into it held the UI thread for 16,921 ms under
  `GLSurfaceView` -- three times Android's ANR threshold, which is the white
  box over the black loading screen -- and for at most 1,092 ms, none of it
  during the load, once `GameView` owned its own EGL context and thread. What
  has *not* been shown is the same fix on a real phone under a real load, and
  the match has only been driven on this emulator afterwards: it starts from
  portrait, survives home-and-back, backs out and starts again.
- **The cel shading has only been judged on the desktop.** Flat colours in
  place of textures and the depth-kink ink pass were shot across five rooms
  and a live two-client match at 1600x900, and cel *off* is pixel-identical to
  before the change.

  On the emulator the mode is **unusable, and the reason is measured**: the
  ink pass reads a flat surface's kink at 0.004-0.009 under llvmpipe and at
  235-256 under SwiftShader, against a threshold of 1.1. Thirty thousand times
  the noise, on a depth field whose large-scale structure is correct -- the
  same per-pixel imprecision that streaks SwiftShader's colour, on its depth.
  Nothing in the shader survives that, and a threshold that did would draw no
  outline at all -- which is now what happens, on its own:
  `Renderer.CalibrateInk` measures a flat surface at **1998** there against
  **0.0159** here and lifts the ink floor to match, so the mode degrades to no
  outline rather than to a black screen. **What that floor does on a phone
  whose depth is merely mediocre rather than useless is untested**, and that is
  the case that matters. (An earlier claim here that the ES path had been seen
  drawing the mode was wrong: those runs had cel shading *off* -- see the
  settings-directory note in `android/ANDROID-PORT.md`.)
- **A phone is still a different machine** — the emulator is x86_64 with
  SwiftShader, a phone is arm64 with a real driver. That is the ABI and the GL
  implementation both differing from what is tested here.
- **The update check has never seen a release of this repository.** Tested
  against upstream NoneGiven/MphRead instead, which has releases: the check,
  version comparison, "update available" line and page URL were all
  exercised that way. Not covered: an asset name actually matching this
  project's — the "no matching asset" path got tested, the matching one only
  by unit test.
- **No browser has actually been opened.** `OpenPage` was only exercised
  where it correctly declined (headless, no `DISPLAY`). `xdg-open` on a real
  desktop and `UseShellExecute` on Windows are untried.
- **The rename leaves an unrun migration on the Pi.** `deploy-server.sh`
  rewrites an `ExecStart` still naming `MphRead` and deletes the old binary,
  but that code path hasn't run against the real box yet. Check
  `systemctl cat mphread-server` after the first deploy following the rename.
- **The ARM64 server package has never been started by CI** — cross-compiled
  on an x64 runner, so `check-dedicated-server.sh` can't run it there.
  `linux-x64-server` (same build config, a processor the runner actually has)
  is the nearest CI gets; the Pi via `deploy-server.sh` is the real test.
- **The Windows dedicated server is started in CI, but only there.** Checked
  on every push via the `windows-server` job, but nobody has run it on a real
  Windows machine behind a real firewall for a long session, unlike the Linux
  server on the Pi.
- **The Pi's ceiling was found as "nobody else can join", not as a broken
  match.** Twenty hosted games and 160 players held with 98.8% delivery and
  no UDP errors; 24 and 32 games admitted no more than 160 either. What is
  *not* known is whether a real match at that point was still playable --
  every player in that ramp was synthetic, so it measures the relay and not
  the game. Nor is the true traffic ceiling known: above four games this box
  could not offer a full 60 Hz per client (`sendto` costs 3.9 ms through its
  WSL NAT), so the higher steps held the total traffic constant and only
  raised the match count.
- **An old client against the new server is untested.** The Pi has run the
  2026-09-01 server since that date, and both refusals were then proved on the
  wire: a full server answers a ninth Hello with `Refused` reason 1 in 11 ms,
  and a protocol-3 Hello with reason 2. What has not been tried is a client
  built *before* `RefusedPacket` meeting that server -- by design it drops an
  unknown packet type and falls back to the eight-second timeout it always
  had, but nobody has run it.
- **The rotation crash was found on the public server's own rotation and
  fixed there; no other pair of maps has been tried.** The mechanism -- a
  pooled player's NodeRef into the room just unloaded -- does not depend on
  which rooms they are, but only MP1 SANCTORUS -> MP3 PROVING GROUND has been
  run, three clients at a time.
- **Late joiners and bursty features skew the tour's numbers**, not the
  replication. Clients start ~3 s apart; a client that joins a bursty phase
  (bombing, unmorphing) late reports a fraction of what the subject did, and
  time-normalisation can't fix a burst it wasn't there for. Judge against
  clients that were present, not the raw tally.
- **Alt-attack presses read ~60% on every observer.** Not loss (loss would
  differ per observer) — two presses inside one intent window arrive as one,
  since the edge history is ORed into a single mask per packet. The bombs
  those presses would have laid still land 79-99%.
- Kanden and Spire show lower fidelity than other hunters on `unmorph` and
  projectile lifetime. Not explained.
- The scoreboard rows tighten to fit past four players, down to 19 px; beyond
  eight would need a second column.
- The First Hunt "biodefense chamber" rooms are listed as multiplayer but
  carry no player spawn points — survival rooms, kept out of the launcher's
  map list and out of any Battle rotation rather than "fixed".
- `zoom` and `double damage` are usually `untested`, since nothing in the
  tour reliably picks either up. `double damage` is probably fine — item
  pickup is simulated from replicated positions and three clients in a 90 s
  match agreed exactly (`12`, `12`, `12`) — but "probably fine" isn't
  "measured".
