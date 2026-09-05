# Testing — test harness

This document explains the netcheck, maptest and the harness scripts used in `~/mph-net-test`.

The runs where something is deliberately wrong -- a line that goes away, a
ninth player, everybody spectating, twenty matches at once -- are in
`TEST-HARD-CASES.md`, along with the two instruments they need (kernel `netem`
for latency and loss, a Python protocol client for what a real client cannot
be made to send).

Philosophy

- `-netcheck` runs the real client in a hidden window; using fake packets or stepping the network without the engine can miss failures where clients have disconnected scenes.
- `NetTestScript` replaces AI with a fixed tour (15 phases) keyed to the server's clock so all clients are in the same phase simultaneously.

How to run

```bash
cd ~/mph-net-test
./run-check.sh 150 Samus Weavel Sylux Trace Samus Noxus   # seconds, then hunters
```

What the harness records

- Every client records what it DID and what it SAW. `compare-reports.py` cross-checks them; an action claimed by one client must show up in others' observations.

Map sweeps and probes

- `-maptest "ROOM" -players 8 -seconds 22` loads a room with eight players (a
  different hunter per slot), drives them through the tour, and prints an
  inventory: spawns, jump pads, teleporters, doors, afflictions, deaths.
  `./run-maps.sh 8 22` in `~/mph-net-test` sweeps every room (~15 min for 33);
  `grep MAPCRASH` / `grep MAPFAIL` the log.
- `-maptest "ROOM" -drawrate N` draws every simulation step N times, which is
  what a 144 Hz screen does to a 60 Hz game. It is how the decoupled frame loop
  is checked from a box with no monitor, and it is deliberately *not* the
  wall-clock accumulator the game uses: it steps alpha 1/N, 2/N .. 1 across the
  draws, so a run is reproducible and visits the whole range instead of
  whatever the machine's load produces. Two assertions, both `MAPFAIL` when
  they trip: **`draws advancing the game`** must be 0 (a draw pass that writes
  back to the world would make the game behave differently on a fast monitor),
  and interpolation must have engaged at all -- "the setting is on" is not
  checkable by reading the setting, since every blend can legitimately decline.
  The MAPTEST line itself must come out **identical** to the `-drawrate 1` run;
  the expected blend ratio is `(N-1)/N`. `.claude/render/FRAME-PACING.md`.
- `-frametimingcheck` checks the accumulator alone, with no room and no window:
  60.000 Hz of simulation under 60, 144, 165, 240 Hz displays, under jitter,
  and under a 40 Hz display where the old single-rate loop played in slow
  motion.
- `-maptest -bots` runs the same rooms with **AI bots** instead of the
  scripted tour — a different code path (the tour writes `Controls` directly
  and never touches behaviour trees), and the only one that reaches bugs only
  `PlayerAi` triggers. This is how the slot-capacity work (`SlotCapacity` to
  8) got finished: raising it is not one constant, every array indexed by a
  player slot has to grow with it, and several only crashed under bots —
  `GameState.BeamKills[4,9]`, `TeleporterEntity._triggeredSlots`,
  `AreaVolumeEntity._triggeredSlots`/`_cooldownSlots`/`_prioritySlots`,
  `PlayerAi._slotHits`/`_slotDamage`/`captureList`,
  `PlayerAi._playerVisibility` (`bool[4,4]`), `PlayerAi._globalObjs`. If the
  capacity is raised again, grep for `[4]` and `[player.SlotIndex]` before
  trusting it.
- **The world probe.** Counting jump pads answers whether a map contains one,
  not whether it works. After the tour ends, the audit teleports a player
  into every jump pad's and teleporter's trigger volume in turn, holds it
  twelve frames, and watches for the event via `Mods.WorldEvents`. Aim at the
  volume's centre (`JumpPadEntity.ModVolume`), not the entity's own position —
  several pads carry their volume beside or above the model, and standing at
  the entity stands the player next to the box, not in it. A pad that stays
  silent is retried higher and in alt form (some ignore bipeds) before being
  called silent; three of MP6 HEADSHOT's eight pads were wrongly reported dead
  for a fortnight on the strength of the first mistake. A map where *every*
  pad stays silent is `MAPFAIL`; one silent pad is not (some are meant to be
  dropped onto from above).
- **`TEST ARENA` (`maps/arena/arena.json`).** A room built for this harness
  rather than for playing: forty units square with a ceiling, one low block in
  the middle, eight spawns on a ring of nine all looking inward, ammo and
  health between them. Nothing is more than twenty units from anything else,
  so the tour's duel and affliction phases actually land their shots -- which
  is the difference between measuring hit registration and measuring the
  hallways of MP1 SANCTORUS. MP2 HARVESTER produced no freezes at all across
  several runs; the arena produces two in thirty seconds. Put its folder in
  the rig's `bin/maps/` and its name in `bin/maprotation.txt` to run a
  networked check on it. It reports MAPFAIL for the odd black frame, which
  `-renderprobe` disagrees with (99.7-99.9% lit from all eight spawns): that
  is the third-person camera passing through the wall of a small closed box,
  not the room failing to draw.
- **The affliction probe.** Freeze/burn/disrupt are states the plain tour can
  never reach, because three things must be true at once: (1) the hunter must
  hold its *own* affinity weapon (`beam + 9`), which the probe issues via
  `ModArmAffinityWeapon` since in a match it's a pickup, not a given; (2) the
  shot must be **charged** — every affliction sits on the charged entry, and a
  weapon without `PartialCharge` only counts as charged at `FullCharge * 2`
  (120 frames for the Judicator), so the probe holds until the weapon itself
  says `ModChargeReady` rather than counting frames; (3) releasing has to
  reach the engine — edges must be computed once at the end of the frame, not
  inside each `Hold` call, or a later clear wipes the edge an earlier press
  produced (a charged weapon fires on release, so a broken edge means it never
  fires). The probe stands the one hunter that can inflict each state two
  units from a target, everyone else stands down, and reports `ok` / `nohit`
  (charged shot missed) / `FAIL` (hit, no state) / `n/a`. `-netdebug` prints
  what each probe actually saw.

The render probe: `-maptest ROOM -renderprobe`

The one failure every other check passes: a room that draws nothing. The
players spawn, the pads fire, the teleporters move, the scoreboards agree --
and the screen is black with the gun in it. Only the pixels say so, so the
pixels are read.

`-maptest ROOM` now samples the frame once a second and reports
`lit first/min/max/mean` as a fraction of the frame that is not the clear
colour, and fails a map that never draws or stops drawing.

`-renderprobe` is the reported repro recipe as a sweep, and is the form to use
when chasing this: it stands the player on every spawn point in the room in
turn (teleporting them there with that spawn entity's own node ref, which is
what PlayerProcess's respawn passes), lets them settle, reads the frame, then
walks them forward for five seconds reading the worst. One line per spawn
point:

```
RENDERSPAWN MP4 HIGHGROUND - EXPANDED | spawn 2 at 12.6,12.1,-16.5
  | at spawn 99.4% | worst while walking 99.4% | part 7
```

Two flags tell the two causes apart, and asking in this order is what makes
the answer quick:

- `-allnodes` draws every node the model has, ignoring the portal-graph
  room-part culling. If the picture comes back, the cull lost it; if it does
  not, the geometry is not being drawn at all. It must be set for a WHOLE
  FRAME, update included -- the draw lists are built during the update and the
  flag is read while they are, so toggling it around a second `OnRenderFrame`
  measures nothing and will happily report culling innocent when it is not.
  That cost an hour once.
- `-shots DIR` writes the PNGs, which is the only way to tell a dark map from
  a broken one by eye.

Reading the numbers

Whole-room sweep, all 27 rooms, 236 spawn points, after the Elder Passage fix:
232 above 70% lit, three between 40 and 70, one below. The one below is MP9
Cryochasm's spawn 2, which sits on a ledge over a chasm -- walking straight
forward off it is what the probe does and what the map is for. A low reading
is not automatically a fault; look at the shot.

The threshold the report uses (`_renderFloor`, 6%) is deliberately far below
what a broken room actually measures. Elder Passage's eight bad spawns read
8.7-19%, which is ABOVE it: the floor only catches "nothing at all", and the
number to compare against is the same room's good spawns, which read 99%+.
Sweep the room; do not trust one reading.

`-hudshots`

`Scene.ReadSceneTarget` -- what every screenshot, thumbnail and map preview
reads -- is the offscreen target, and the HUD is deliberately not drawn into
it (it goes to the default framebuffer after the target is unbound, which is
what makes thumbnails come out without one). So no capture could ever show the
HUD, and HUD work could only be checked by playing.

`-maptest ROOM -hudshots -shots DIR` opens a VISIBLE window at 1024x576 and
reads its buffer instead (`Scene.ReadWindowBuffer`, called between the draw
and the swap). Only valid on a visible window -- a hidden one has no usable
back buffer under Mesa, which is the whole reason the offscreen target exists.
The window is bigger because the HUD is authored for 256x192 and scaled to it:
at 320x180 a weapon icon is a few pixels and a capture of it says nothing.
