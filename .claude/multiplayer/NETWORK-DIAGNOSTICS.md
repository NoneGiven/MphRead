# Multiplayer — diagnostics, and the damage bug (resolved 2026-08-23)

The long-running "one remote slot receives zero damage for the whole match" bug
was two faults, both from commit `bbb13b8`, and **neither was latency**. Full
account in `CLAUDE.md`, "The damage bug, and what it actually was".

1. **Every client except the authority was frozen.** `ApplyState` took position
   and speed from the snapshot for the local player. The authority's copy of
   that player is the player's own report from a round trip earlier, so writing
   it back pinned the character forever and every frame of local movement was
   discarded before it could be published. Three clients, seventy seconds: the
   authority moved 95 units, the other two moved 0 and 2. It reproduced on
   loopback too.
2. **Remote players fired from their ankles.** `ModSetAim` assigned a remote
   player's `CameraInfo.Position` from its bare `Position`, missing the
   `AimYOffset` (0.9 units) that `UpdateCameraFirst` adds. Every shot starts
   from that field, so on the authority a remote beam travelled parallel to the
   one its owner aimed and 0.9 units below it. The authority's own player was
   unaffected -- which is why the failure looked directional and looked like lag.

The measurement that separated them from latency, at 11 ms of ping:

```text
authority   player overlaps by shooter: [0->1] 2 [1->0] 1
slot 1      player overlaps by shooter: [1->2] 69
slot 2      player overlaps by shooter: [2->0] 58
```

After the fix, at 130 ms of induced latency, all three agree within two hits.

Against the Pi on MP3 PROVING GROUND, three clients, 100 s, after all four
fixes: **PASS on all three**, 0 mismatches, 0 visible teleports, damage
`[0] 21 [1] 19 [2] 37` resolved on the authority and replayed identically by
both observers, and `player overlaps by shooter` agreeing within 4 on every
pair. Pick the map deliberately -- MP1 SANCTORUS, which the Pi's rotation opens
on, is big enough that three scripted players never meet, and a run there shows
zero overlaps for *everybody* and deaths by kill plane.

## Two more the first fix uncovered

Taking the local player's position from the snapshot had been hiding
disagreements, not only freezing people.

3. **Every respawn placed the two machines differently.** `GetRespawnPoint`
   picks among points free of living players *on the machine running it* and
   rotates with the frame counter, and the local player respawns early by
   holding fire -- so it chooses well before the authority does, and neither
   ever yields. 55 in 100 s, up to 175 units apart. Local spawning is kept for
   responsiveness; only the placement is handed over, on the frame the
   authority's snapshot first reports the player spawned.
4. **A derived velocity became a launch.** `Speed` was worked out from two
   reported positions; across a respawn that is the whole level over two
   frames, so ~150 units per frame -- published in the snapshot, applied by
   every client, and handed back to the owner at its next respawn. The
   authority was measured holding a player at Y=163 climbing 35 units a frame,
   with 975 corrections logged in 100 s. Steps beyond `SnapDistance` now yield
   zero, the result is clamped to `MaxReportedSpeed` (5 u/frame against boost's
   0.6), and a freshly placed player gets no speed at all.

Three clients, 100 s at 130 ms, after both: **0 visible teleports** (was 715,
worst 265 units), 3/0/0 corrections (was 975/55/0), 0 mismatches, damage
`31/3/20` resolved and replayed identically, and Weavel's halfturret exercised
for the first time (601 frames, observers 600 and 617).

## The randomised sweep

`~/mph-net-test/run-batch.sh <runs> [seed]` draws map, roster, player count,
match length, point goal, latency (0-250 ms each way) and packet loss (0-3%) at
random, keeps every run's logs under `batch-<seed>/NN/`, and prints only the runs
that reported something. Short matches and low point goals are deliberate: a
match that ends inside a run is the case every "is this a new match" bug hides
in, and the fixed scenarios were all long enough to avoid it.

Four more came out of it, three of them only at a rotation:

5. **`NetRoomChange.Settling` had no callers** -- the guard went with the loop it
   was attached to. 26 corrections in 4 s at one restart, the local player
   alternating between two rooms' coordinates.
6. **The damage sequence was reset on each side separately at a room change.**
   It is a sequence number, not a tally; resetting it on machines that change
   room on different frames means either a resync that swallows the next real
   hits or **up to 32 hits replayed into a player who has just spawned**. Four
   `damage sequence jumped` events per client per rotation; one run had the
   authority resolve 115 hits and three of five clients replay none.
7. **The divergence backstop compared against "now"** instead of against the
   instant the authority was looking at. A player falling out of the level covers
   30 units in half a round trip at 250 ms, so it was hauled back up out of its
   own fall and could never die -- 77 in one run, peers seeing 64-unit jumps. It
   now indexes this player's own recorded history by the ping and requires a
   second of disagreement. After: zero corrections, zero teleports.
8. **Every jump pad was being called a desync.** `Mods.WorldEvents` already
   reports pads and teleporters; the check now asks and grants the same grace it
   gives a respawn.

## Intents go every frame now

`NetConfig.IntentSendInterval` is **1**, so a player publishes position and aim
at 60 Hz rather than 30.

This is not a smoothing question, it is what a remote player *is*: a puppet is
pinned to the position its owner reported (`NetPlayerBridge.RestoreReportedPosition`
runs after the engine's own movement step), so whatever the local simulation
does between intents is thrown away. At 2, everybody but yourself moved in 30 Hz
steps on a 60 Hz screen, and a recorded demo -- where every player is a puppet,
including the recorder's own -- stepped from end to end. That is what "the demo
looks like 30 fps" was. Measured in the file: a two-player recording carried
**53.8 SlotIntent/s before and 104.7 after** (~27/s per player, then ~52/s),
and grew from 3.0 to 4.2 KiB/s on disk.

It was 2 because the server relays N*(N-1) intents a frame and at six players
that was losing enough of them to leave gaps. What made that true was fault 12
below: a send queue that dropped the *newest* packets when it filled, which has
since been fixed.

Four eight-client 120 s loopback runs, two each way:

| | mismatches | scoreboards |
|---|---|---|
| 30 Hz | 15 (11 unmorph, 4 alt-attack) | agree |
| 30 Hz | 5 (3 unmorph, 1 alt-attack) | disagree by 2 |
| 60 Hz | 9 (6 unmorph, 2 teleports) | disagree by 5 |
| 60 Hz | 14 (13 unmorph) | disagree by 4 |

**What that shows is that it is not worse, and nothing finer than that.** The
run-to-run spread (5 to 15) swamps the difference between the cadences, and the
scoreboard drifts in three runs of four at both -- so it is this harness on this
machine, not the change. Eight clients on one box measure the box; `run-remote.sh`
against the Pi is the instrument for anything sharper. The cost is about 100
bytes on the wire per player per frame, so ~42 KB/s into each client of an
eight-player match.

No protocol bump: the layout did not move and nothing reads the cadence, so a
build sending at 30 and one sending at 60 understand each other exactly as
before -- the older one is simply seen in coarser steps. `ProtocolVersion` is
for a build that would read the bytes correctly and then play a different game,
which this is not.

## What is left at 250 ms and is not a bug

`alt-attack` at ~40% (one-frame presses collapsing into one intent window),
`movement` at ~50% (a 30 Hz reconstruction of a 60 Hz path with a fifth of the
updates reordered and refused), `zoom` lagging (a toggle with a half-second round
trip), `form stayed wrong for 181 frames` (the correction machinery's full budget
-- and the check fails at 60, *below* that budget, so it cannot pass whenever the
machinery has acted), and `never took a single hit` on a large map (check
`player overlaps by shooter`: empty for *everybody* means the room, not the
network).

## The sweep has to reach the Pi

**A randomised run against a loopback server is a regression check, not a
real-world one.** Loopback has none of the reordering, jitter or server-side CPU
load the bugs in this file were found under. `run-batch.sh` is the fast local
check; `run-batch-pi.sh` is the one whose result counts -- it puts `udp-lag.py`
*in front of the Pi*, so the real path is underneath and the chosen delay is on
top, which is the only way to ask a server that answers in 7-17 ms what happens
at 250. It sets the map, match length and point goal in the server's own
rotation over SSH per run and restores it on the way out.

## Reproducing latency

Three instruments, in order of how much they are worth:

```bash
# Inside the client. Per-process, no sudo, no extra hop, works against the Pi.
FruityPrime -netcheck net.livetek.fr -port 27888 ... -netlag 200:20 -netloss 2
./hard/run-netlag.sh 150 0 0 150 150 300   # one match, five different lines

# In the kernel, on this box's interface. Everything this machine sends.
./hard/run-latency.sh 150 5 0 100 200 300

# A relay in front of a loopback server. The weakest of the three.
./run-lag.sh 60 90 Samus Kanden Trace
```

`-netlag MS[:JITTER]` (`Mods/Network/NetLag.cs`) holds half the round trip on
the way out and half on the way in, inside `NetTransport`, with the queue
drained from the head only while the head is due -- so jitter delays a
datagram and never overtakes the one in front of it. `-netloss PCT` throws
datagrams away each way. Every report that prints a measurement says
**SIMULATED LINE** at the top when either is set: a run with these on is a
reproduction and must never be quoted as a measurement of the real thing.

What each is for: **netem** answers "what does 200 ms do to a match", and does
it in the kernel where it costs nothing. **-netlag** answers the question netem
cannot, which is the one the reports are actually about -- a match where the
*players* have different connections, and one of them is the authority. And it
is the only one of the three that works on Android, on Windows, or in
somebody's hand.

`udp-lag.py` is the old relay; `run-lag.sh` puts a loopback server behind it.
It is kept for the loopback case and should not be used for anything else: at
five clients that Python forwarder *was* the experiment -- asked for 100 ms it
produced round trips of 1767-1859 ms and lost 40% of the intents.

## A freeze that existed on one machine only

Reported 2026-09-04, from the person hosting: *"I see players being frozen who
keep moving -- either they are frozen and cannot move, or they are not."*

The freeze is applied inside `PlayerEntity.TakeDamage`, in the branch that
reads `beam.Afflictions` -- from the **beam entity** that landed the hit. A
beam entity only ever exists on the machine that resolved the shot, and
`NetDamage.Replay` calls `TakeDamage` with no beam at all (it cannot: the
projectile was never spawned here). So the affliction was applied on the
authority and *nowhere else*:

- the victim, on their own machine, was never frozen. They walked around
  normally and kept reporting positions;
- the authority, which pins a puppet wherever its owner's last intent said it
  was (`ApplyReportedPosition`), therefore drew a player encased in ice
  sliding across the room -- because `_frozenGfxTimer` was set there and the
  position was not its own to decide;
- every other client saw neither: no ice, and a player who stopped moving only
  because the authority's snapshots had stopped moving them.

`PlayerState.FlagFrozen` (bit 5, additive, no protocol bump) carries the
*state* rather than the cause. `NetPlayerBridge.ApplyState` applies it to
puppets and to this machine's own player alike -- being frozen is part of the
match, like health and the score. `PlayerEntity.ModSetFrozen` starts the
engine's own timer with the engine's own numbers and its re-freeze rule, tops
it up while the flag is still set so it cannot expire a round trip early, and
hands the *ending* back to the ordinary tick by leaving one frame on it -- so
the break sound and the ice-shatter effect are the game's, not an imitation.

The shape to remember: **an affliction is not state until it is sent.**

## The other two afflictions, reported the same way (2026-09-07)

The paragraph that used to end the section above said burn (`_burnTimer`) and
disrupt (`_disruptedTimer`) have exactly the same structure, that neither had
been reported, and that two spare bits were left in the flags byte. Both were
then reported, in the same batch of player feedback:

- *"The Volt Driver's charged shot is missing the screen-distortion effect."*
- *"Hunters taking burn damage do not display the burning visual effect."*

Neither was missing anything. The distortion is a shader, a shift table and a
four-state machine in `PlayerHud`/`Renderer`, and it works perfectly -- for
whoever happens to be the authority. The flames are `CreateBurnEffect`, and
likewise. What was missing is that nothing set either of them off anywhere
else, for the reason above: the affliction is read off the beam, and the
replay has no beam.

`PlayerState.FlagDisrupted` (bit 6) and `FlagBurning` (bit 7) carry them, with
`ModSetDisrupted` and `ModSetBurning` shaped exactly like `ModSetFrozen` --
the engine's own numbers, topped up while the flag holds, ending handed back
to the ordinary tick so the recovery animation and the effect's own teardown
are the game's. Two details worth keeping:

- **Disruption is applied on the victim's own machine, and that is the point.**
  The timer is what the aim path reads, so a disrupted player's aim is
  disrupted where their aim is actually computed rather than on a puppet
  reading a relayed direction; and the HUD state is the main player's alone,
  which is the whole of what was reported missing.
- **Burning is cosmetic on arrival, deliberately.** The burn's own tick calls
  `TakeDamage` every eight frames and `NetDamage.Suppress` drops that
  everywhere but on the machine resolving the match -- so the fire burns on
  every screen while the damage still travels as damage, counted once.
  `_burnedBy` is left alone for the same reason: attribution belongs to the
  kill the authority resolves.

The flags byte is now full. The next flag needs somewhere to live.

Measured on the two-client instrument, Kanden against Spire, 70 s: Spire set
itself on fire with its own Magmaul and counted **255 burning frames on its own
machine** against the authority's 299 -- the difference being the round trip at
each end. Before the change its own count was necessarily zero. Disruption is
the same mechanism and was not exercised: the tour landed 21 hits and no
charged Volt Driver among them.

## The shadow that trailed a step behind (2026-09-07)

Reported as *"a character's shadow can sometimes be rendered behind the
character instead of directly beneath them"*.

The blob shadow is cast from `_volume.SpherePosition` -- the collision volume,
not the position. The engine recomputes that volume once a frame, at the end of
the movement step, and a **puppet is moved after that**: `RestoreReportedPosition`
runs post-movement, by design, so that what is drawn and what is shot at agree
with the owner's own report rather than with this machine's guess. `Move` set
the position, the previous position and the node ref -- and not the volume.

So for every remote player, all frame, the volume described where the local
simulation had guessed they were while the model was drawn where they actually
are. For anybody moving, that is a shadow on the ground they have just left.
The burn effect is positioned from the same field and lagged with it, and so
did the hitbox, which is the half of this nobody would have reported.

`ModRefreshVolume`, called from `Move`.

## A frozen player who kept sliding, from the host's side

The state flag above stops the *victim* moving on their own machine, but the
authority pins puppets to their owners' reported positions -- and for one round
trip after a freeze the victim is still walking about and still reporting where
they have got to. Every one of those reports was applied on top of a player the
authority was holding still: at 250 ms, fifteen frames of ice sliding across
the room, which is exactly what it looks like.

A frozen player cannot move, so any position that arrives while the timer runs
describes a moment before the ice. `ApplyReportedPosition` and
`RestoreReportedPosition` ignore them (`FrozenInPlace`). The local simulation
still runs -- a frozen player falls -- and whatever the two copies disagree
about by the time it thaws is what `Diverged` is for.

## A player stuck in alt form on everybody else's screen (2026-09-07)

Reported as *"a player can sometimes appear to other players as being in
Alt-Form even though they are actually in Normal Form"*.

The form was replicated by replaying the morph **press** through the engine and
by nothing else. That works until one of those presses does not take -- a
packet lost at the wrong moment, or a press that arrives while the puppet is
somewhere it cannot unmorph. The authority's copy is then in the wrong form for
the rest of the life, and since `FlagAltForm` in every snapshot is read off
that copy, every other client faithfully agrees with it. The one machine that
knows better is the owner's, and nothing was asking: `IntentButtons.AltFormState`
has always been in the packet, and was used only to convert the reported
position between forms.

`ApplyIntent` now feeds it to `ApplyForm` -- the same grace period, the same
attempt-then-force -- but **only on the authority**. A client that also acted on
it would be taking form corrections from two sources at once, its owner's
intent and the authority's snapshot, and the two disagree for exactly as long
as it takes the authority to converge.

## Players invisible at the top of AD2 ALINOS PERCH

The other half of the node-ref story in `ModRefreshNodeRef`. That method was
written for MP4 HIGHGROUND EXPANDED, where the lookup *succeeded* and the walk
had gone stale; this is the case where `GetNodeRefByPosition` returns nothing
at all, because the position is inside no room part's portal volumes -- which
is what the top of that map is. The fallback then kept whatever node the
puppet already had, `PlayerDraw` culls on `IsMainPlayer || IsVisible(NodeRef)`,
and the result was a player who could be shot and could not be seen, with
their shadow still moving about underneath them. Confirmed by several players
at once.

Now: probe the body and half a unit either side before giving up, and if it
still cannot be placed, set `ModNodeUnresolved` and draw the player anyway.
Drawing one that should have been culled costs a model the depth buffer mostly
swallows; hiding one that should have been drawn costs a match.
`node lookups unresolved: N` in every check's report is how often it happens.
It is **not** rare and it is not only that map: zero on MP2 HARVESTER over
90 s with three clients, but 12-231 per client on MP1 SANCTORUS over 150 s
with five -- about 2.5% of frames, every one of which was a puppet the
renderer would have been free to hide. Measured 2026-09-04 against the Pi.

## The gun that lowered itself, and the shots that were never spawned

The largest of this round, and it was found by pulling on the pad bug. The
engine lowers an idle player's gun after `GunIdleTime` (`UpdateGunAnimation`),
and raises it again only on a frame where `_timeSinceInput` is exactly zero.
That counter is reset from **one** place: `Input.HasInput`, written by the pass
in `ProcessAllInput` that turns a keyboard into binds. Three kinds of player
never go through that pass:

| Driven by | Goes idle? | What it cost |
|---|---|---|
| keyboard and mouse | no | -- |
| touch controls | no -- they press keys into a `KeyboardState` of their own | -- |
| a **gamepad** | yes | the gun sank off the bottom of the screen and never came back: `CanShoot` and `TryEquipWeapon` both refuse while `GunAnimation.UpDown` is playing |
| a **remote player** (`ApplyIntent`) | yes | every puppet on every machine, including the authority's |
| a **scripted client** (the tour) | yes | the harness measured a fraction of the shots the tour asks for |

The middle row is the one that reached players. A puppet's beams are spawned
by its own simulation on each machine, and the machine that matters is the
authority, because `NetDamage.Suppress` throws away damage resolved anywhere
else. So a player who held still and fired -- which is what a sniper does --
had their shots fail to spawn on the one machine whose shots count. Their
Imperialist went through somebody and did nothing, and then a later shot, on a
frame where a weapon switch or a respawn happened to have reset the counter,
killed them. That is "the shot seems to miss and the death arrives a second
later", and it is not latency.

`PlayerEntity.ModNoteInput` is the fix, called from `GamepadInput.Apply`, from
`NetPlayerBridge.ApplyIntent` (masked to the *pressed* bits, so a puppet goes
idle exactly when its owner does) and from `NetTestScript.Finish`.

The measurement, on TEST ARENA with three clients, before and after: shots
spawned per slot went from 28-63 to **118-142**, hits taken from 2-20 to
**49-72**, and the affliction probe went from `freeze FAIL burn nohit disrupt
FAIL` to `freeze ok burn ok`. None of those numbers were a network problem.

## Why a hit can land a second after the shot

Not a bug, and worth being able to say so quickly. On a dedicated server the
authority is **one of the clients**, so a shot fired by anybody else takes
four hops to become a death anybody can see: shooter to server, server to
authority, authority's snapshot back to the server, server to everyone. At the
100-200 ms this was reported at, that is 0.4-0.8 s before the victim's body
reacts, and the shooter meanwhile sees their own beam pass through a puppet
that keeps walking -- because `NetDamage.Suppress` throws away damage resolved
on any machine that is not the authority, which is what stops one kill being
counted twice.

Three things are *not* the cause and have been checked: snapshots go out every
frame (`AfterSimulation` -> `BroadcastSnapshot`, no cadence), intents go every
frame (`IntentSendInterval` is 1), and the relay adds no queueing of its own.
The fix, if it is ever worth one, is lag compensation on the authority --
rewinding the victim by the shooter's latency before testing the overlap --
and that changes what a hit *is*, so it is not something to do quietly.

## Traps

- `run-check.sh` copied `MphRead.dll`, which the rename to `FruityPrime` deleted,
  with `2>/dev/null`. Every run silently tested a stale binary. Fixed in both
  runners.
- The Pi was running **protocol 4** while the notes claimed protocol 3. A
  mismatched Hello is dropped silently, so the client reports "the server did
  not answer in time" -- which reads as a dead server. Probe it with a
  `StatusQuery` (packet type 14); the protocol byte is at payload offset 96.
- `NetFeatureCheck` compared puppets against `NetSession.RemoteStates` on the
  authority, which never receives a snapshot. It read clean only while everybody
  was frozen. Skipped there now.
- `NetTestScript.FindTarget`'s deterministic ring was `% 3`, so every run with
  more than three players aimed the extra slots back at slots 1 and 2. It now
  rings over the whole roster.
- **`untested` is a question about the harness, not a pass.** The zoom phase
  pressed the Imperialist's own weapon key, which only selects a weapon already
  picked up -- so on a fresh roster it did nothing, the zoom button was never
  held, and the phase reported `untested` for months. Instrumented, no intent in
  a whole run carried `IntentButtons.Zoom`. The tour now *issues* a zoom-capable
  weapon (`ModArmZoomWeapon`), presses towards the wanted state rather than on a
  timer (`UpdateZoom` is an edge toggle, so a cadence zooms in and back out),
  and presses zoom off on the way out (nothing else clears it, and the
  Imperialist does half damage unzoomed). Zoom replication was never broken:
  `mine 124 / theirs 123`, PASS both ways.

## Flying bodies, invincible players, hits never counted

Three separate faults, reported together as "weird things happen with two
Trace clients" -- each one a shape worth recognising again.

9. **A hit launched the victim across the level.** `TakeDamage` adds its
   `direction` argument straight onto `Speed`, so whatever is in it is a
   velocity in units per frame. Most beams carry no direction of their own
   (`DamageDirType` is 0), so `NetDamage.Note` filled one in as
   `victim.Position - attacker.Position` -- the *distance between the two
   players*, not a unit vector, so a hit from ten units away launched the
   victim at ten units a frame through the wall. It looked asymmetric ("A
   shoots B and B flies, B shoots A and it's fine") because A was the
   authority, applying its own damage directly with the real vector, while
   everyone else replays. Fix: relay the impulse verbatim, zero included, and
   let the receiver's own fallback (indicator only, never `Speed`) handle
   zero.
10. **A player took no damage at all, until the shooter switched weapons.**
    `BeamProjectileEntity` refuses to spawn a beam whose ammo cost exceeds the
    shooter's ammo. Every machine simulates every player's shots and spends
    ammo, but pickups are collected locally and not replicated -- so the
    puppet on the authority's machine ran dry within a round, the authority
    created no projectile, and the target took nothing while the shooter's own
    screen showed a hit. Switching weapons "fixed" it only until the other
    pool ran out too. **Look for this shape whenever a remote player can do
    something on their own machine and not anyone else's: the puppet runs the
    same code with different resources, and only the owner's copy of those is
    authoritative.** Fix: the intent now carries the owner's ammo, the way it
    already carried position and weapon; `ModSetAmmo` writes it onto the
    puppet.
11. **Snapshots were the one stream nobody ordered.** Both intent streams
    refuse an older frame; `HandleSnapshot` did not, and it's the stream
    carrying health, score and the damage counter. An overtaken datagram put a
    player back where they'd been, undid a kill, and ran the (byte-sized)
    damage counter backwards -- `Replay` read the difference as ~250 new hits:
    25 resolved on the authority against 258 "replayed" on a client. Ordering
    the stream took three lines. `NetDamage.Replay` also now refuses more than
    32 hits in one snapshot, so one bad packet can't flinch, shove or kill
    from a corrupted count.

## "Observers only see half your turn"

Listed for a long time as packet loss under load. It was two things, neither
quite that:

12. **The transport dropped the wrong packets.** `NetTransport` queued 256
    received packets and, when full, dropped the *arriving* one -- the newest
    input, in a protocol whose packets say "this is where I'm aiming *now*".
    Eight clients on one machine produce ~2000 packets/s between them, so one
    130 ms frame overflows it. Queue is 2048 now, socket buffers 1 MB, and an
    overflow drops the oldest.
13. **The check compared two different sample rates.** A player published
    position/aim every `IntentSendInterval` frames, which was then 30 Hz, but
    `NetFeatureCheck` measured the *local* player's path every frame (60 Hz)
    and compared lengths -- looked clean with one opponent (aim moves slowly),
    fell apart with seven (aim slews several times a second, the 30 Hz
    reconstruction cuts every corner). Fixed by sampling the local player only
    at the instants it actually publishes.

Eight clients, 150 s, one machine: 33 mismatches before both fixes, 10 after,
none of them `facing`.

## The Shock Coil did double damage against players

Not a network fault. It's the one beam that stays alive and re-tests
collision every frame, and every beam hit carries `DamageFlags.NoDmgInvuln`
unconditionally, so the per-hit invulnerability window never applied and
nothing else limited how often it landed. At 30 fps that was fine; this
engine runs at 60, so it landed roughly twice as often -- ~600 dmg/s, a full
hunter in a sixth of a second. The identical compensation already existed in
the same file for the same weapon against *enemies* (with a `// todo: FPS
stuff` beside it); only the player path had been missed. Now does half what
it did before -- **not independently measured**, since the scripted tour
barely fires the beam at a player; rests on reading the code beside its
already-corrected twin.

## Idle server cost

The Pi's run loop slept 1 ms between passes whether or not anyone was
connected -- 5-7% of one core burnt for nothing. Now sleeps 20 ms while empty.
Six real clients on the Pi 3B: 5-22% of one core (typically 11-16%), system
65-75% idle, 0 packets dropped.

## The scoreboard's ping column

Tab shows scores as always; in a networked match there's now a third column,
from the server's per-second roster measurement, smoothed so one late
datagram isn't a worse connection. Green under 80 ms, amber under 160, red
above, `--` before the first measurement. A player on the same machine as the
server reads ~20 ms, not 0 — the round trip includes the client's own frame.
The two stock columns shift left in a networked session (`ModScoreColumn1/2`)
to make room.

## Diagnostics available

`NetDamage`: `Fired`, `AimDrift`/`WorstDrift`, `PlayerChecks`,
`PlayerOverlaps`, `PlayerAccepted`, `PlayerOverlapsByShooter`.
`MPHREAD_NETLOG_INTERVAL` sets the netlog cadence.

`player overlaps by shooter` is the one that mattered: comparing a shooter's own
count against the authority's is what separates "the shot missed" from "the shot
was resolved somewhere else".

## Still open

No lag compensation, and one missing field is why it cannot have any: nothing a
client sends says which snapshot it was looking at. `IntentPacket.Frame` and
`SnapshotHeader.Frame` are counters in unrelated clocks, each starting at zero
when its own process joins -- so the abandoned frame-stamped rewind was indexing
the authority's history with the shooter's frame number. Echoing
`NetSession.LastSnapshotFrame` back in the intent fixes that for four bytes, a
protocol bump and a Pi redeploy. Not needed for correctness.

`double damage` is still usually `untested` -- nothing in the tour picks one up.
Item pickup is simulated on every machine from replicated positions and the
clients agree exactly on how many items were taken, so it is probably fine, but
probably is not measured.


## A vacated slot kept its occupant's score

Reported as: kill somebody, leave the match, reconnect to the same server, and
the kill is still there against your name.

It is the same shape as every other rejoin fault -- per-slot state describing
whoever *used* to be in the slot -- and the score was the one piece nobody had
cleared. `NetSlotManager` already forgot the simulation's state, the wire's and
the damage sequence's when a slot changed hands (three `ForgetSlot` calls);
`NetScoreboard.ForgetSlot` is the fourth, and it runs both when a slot is
vacated and when it is taken, because a slot can be refilled before this
machine has run a frame with it empty.

It has to happen on every machine and not only on the one that left: the
scoreboard belongs to the authority, which publishes Points, Kills and Deaths
per slot in each snapshot for everyone else to adopt. Team totals are not
touched -- `GameState.UpdateStandings` recomputes them from the per-slot
figures every update, so clearing the slot clears the team, and clearing a team
total directly would be wrong in a team mode.

Measured with four scripted clients on a 15-minute match, A leaving at 50 s and
D taking its slot:

| | A's slot when it left | slot 0 at the end |
|---|---|---|
| before | 0k/7d/-7p | 0k/**11d**/-11p (A's 7 plus D's 4) |
| after | 0k/6d/-6p | D's own 0k/4d/-4p, and 0k/0d/0p once D also left |

The rig's default `maprotation.txt` runs one-minute matches, and a rotation
resets every score (`NetRoomChange.ResetScores`) -- which hides this fault
completely. Any measurement of a scoreboard needs a match longer than the run.

## Rejoining: per-slot state, and what `run-rejoin.sh` measures

The report: A creates the game, B joins, A quits, B stays, A rejoins the
same match. B sees A but cannot hit them; A can hit B.

`~/mph-net-test/run-rejoin.sh [seconds] [leave_at] [rejoin_at] [host] [port]`
builds exactly that topology and adds a control:

- A joins first, so it takes slot 0 and becomes the authority.
- B joins (slot 1).
- A leaves. The server promotes B (`DedicatedServer.Remove`), so the
  authority moves mid-match -- which is half of what makes this scenario
  different from any other.
- Then **two** clients join at the same moment: C, which takes slot 0, the
  one A vacated, and D, which takes slot 2, never used. Both then play the
  same tour against the same authority for the same time.

A is two processes on purpose: the suspicion is state that is only cleared
when a session starts or stops, so a rejoin simulated inside one process
would clear the very thing that needs to survive.

**Not reproduced.** Loopback and against the Pi, the rejoined client in the
reused slot registers damage in both directions, and if anything more than
the control:

| | slot | took a hit |
|---|---|---|
| C (reused slot) loopback | 0 | 23 |
| D (fresh slot) loopback | 2 | 6 |
| C (reused slot) on the Pi | 0 | 13 |
| D (fresh slot) on the Pi | 2 | 5 |

A later run of the same script, after the fix below, gave C 1 hit and D 0 --
which is not a regression, it is the variance. Two to four scripted clients
duelling for a minute engage each other by luck: across runs the same slot
has come out anywhere between 0 and 23 hits. **The hit counts here answer
"did damage flow at all in this topology", and nothing finer.** The
regression gate is `run-check.sh`, which cross-checks what each client did
against what the others saw and reports mismatches; that is what to run
after touching any of this.

So slot reuse plus an authority handover does not, on its own, break
damage. Whatever the report is about needs something this does not have --
a human's engagement pattern rather than the tour's, the launcher's own
host path rather than a dedicated server, or a particular moment (a
rotation, an intermission) to rejoin in.

**One trap to avoid when running this.** An earlier run of this script was
read as a reproduction: the rejoining client reported `took a hit 0` and
`authority=True` when the server said the authority was the other client.
Both were artefacts. The `authority=True` was real but legitimate -- the
other client had left seconds before the report was printed, so the
authority had moved back. And `took a hit 0` was a two-player match with
little engagement, not a client that could not be hurt. **A single run of
two scripted clients is not evidence of a damage fault**; that is what the
control client is for.

### What was fixed anyway

Per-slot state was cleared only when the session started or stopped, or the
room changed -- never when a slot changed hands. So a slot's next occupant
inherited the previous one's reported position and frame number, spawn
barrier, divergence and staleness counters, and damage sequence.

`StaleSinceSpawn` names this hazard in its own comment -- "a peer that
reconnects restarts its counter at zero, and a slot that changes hands
inherits the barrier of whoever held it... a player nobody can hit and who
slides without ever taking a step" -- and bounds it with a 120-frame give-up,
so it costs two seconds rather than a session. Two seconds of a player who
cannot be hit is still the reported symptom.

`NetPlayerBridge.ForgetSlot` and `NetDamage.ForgetSlot`, called from both
`NetSlotManager.Activate` and `Deactivate`, clear it. A slot changing hands
means the old occupant's history is meaningless by definition, so there is
nothing to weigh up. **This is hardening, not a confirmed fix for the
report**: the report was not reproduced, so nothing here is known to be the
cause of it.

### Reproduced: the intent stream's ordering guard

The report *was* reproducible. What the harness had been missing was not the
topology -- it built that correctly -- but **how long A played before it
left**, and that turns out to be the whole size of the fault.

`NetSession.HandleSlotIntent` refused any relayed intent whose frame was not
newer than the last one accepted for that slot. Sound, for reordering. But a
client's frame counter starts at zero in `NetSession.StartClient`, so a
player rejoining a match they had been in for five minutes comes back
numbering from 1 while the authority still holds `_lastSlotIntentFrame` =
18000 -- and **every intent they send is refused for the next five minutes**.
The barrier is exactly as tall as their previous session.

That is the asymmetry in the report. The rejoiner's own arrays are fresh, so
they see everyone normally and their shots at other people are resolved from
intents the authority does accept. The authority holds the stale counter, so
the rejoiner's aim and trigger never arrive there, and the rejoiner's puppet
is pinned to wherever the previous occupant was standing -- which is why
nobody can hit them and they cannot hit anyone, on the one machine that
decides either. It clears itself when the counter climbs past the old value,
which from a player's seat looks like "it started working again after a
while", and dying and respawning is the thing they happened to be doing
while they waited.

`run-rejoin.sh 130 60 68` -- A plays for a minute before leaving, so the
barrier outlasts the rest of the run:

| | slot | intents refused *by the authority* | took a hit |
|---|---|---|---|
| before, C (reused slot) | 0 | **1547** | **0** |
| before, D (fresh slot) | 2 | 0 | 9 |
| after, C (reused slot) | 0 | **0** | 2 |
| after, D (fresh slot) | 2 | 0 | 8 |

The hit counts still carry the variance the section above warns about. **The
number that is not noise is the refusal count**, which is now reported by
`-netcheck` as `intents late=` beside the snapshot equivalent, and which went
from 1547 to 0 while the authority's accepted intents went 3591 -> 5452.

The fix is the one the snapshot stream already had: a reset gap.
`IntentResetGap` = 600 frames in `NetSession` and in `DedicatedServer` --
below it a packet is a reordered straggler and is dropped, above it the
sender's counter has restarted and the newcomer is who to believe. Plus
`NetSession.ForgetSlot`, called beside the other two from
`NetSlotManager.Activate`/`Deactivate`, which clears the frame counter, the
last intent and the last snapshot for a slot that has changed hands -- a
vacated slot was keeping its old occupant's intent flagged *valid*, so the
authority went on placing, aiming and firing a player who had left.

## Ping: 20 ms to a server 1 ms away

The scoreboard's ping is a round trip the **server** measures (clients never
exchange packets, so nobody else can measure it for everybody), and it was
measuring far more than the network:

- the client's Pong was sent from `NetSession.Update`, i.e. **on the next
  rendered frame** -- half a frame on average, a whole one at worst, more
  when the frame ran long;
- `NetTransport.ReceiveLoop` polled `Available` and `Thread.Sleep(1)` between
  passes, on **both** machines, and `Sleep(1)` is not a millisecond on
  Windows, where the scheduler tick is 15.6 ms unless something in the
  process has raised the timer resolution;
- the server's own loop sleeps a millisecond between passes.

So ICMP 1 ms, scoreboard 20 ms, and none of the difference was the wire.

Two changes. `ReceiveLoop` now **blocks** on `Receive` with a 500 ms timeout
(there only so shutdown is prompt) instead of polling -- which takes the
arrival delay off the front of *every* packet the session receives, not just
pings. And a client's transport answers Ping on its own thread
(`AnswerPingsImmediately`, opt-in, set in `StartClient`): a Pong echoes an id
and needs no game state, so there is nothing for it to wait for a frame for.

Measured on loopback, where the true round trip is nil: **8-11 ms before,
1 ms after.**
