# The server as the simulation authority

`-simulate` on a dedicated server. Code: `Mods/Network/ServerSim.cs`,
`Mods/Headless.cs`, `Mods/Input/SyntheticInput.cs`, and the `NetRole.Server`
branches in `NetSession.cs` / `DedicatedServer.cs`.

## What moved, and what did not

The authority was never a property of being a player. It is the property of
being the machine every other player's intent is pointed at, and until now
that machine was whichever client joined first. The server can now be that
machine.

**What this buys, in order of how much it matters.**

- **Nobody is at zero latency any more.** The client authority resolved its
  own shots against its own present and everybody else's against a rewind
  (`NETWORK-UNLAGGED.md`). It was the one player in the match who could not be
  wrong about where anyone was, and it paid nothing for the privilege. Now
  every player, the ex-slot-0 included, is compensated by exactly their own
  round trip and nobody is compensated by zero.
- **The match stops depending on a player's machine.** No handover when the
  authority leaves, no stand-down when their line blips (`AuthorityStandDowns`
  exists because that happened against the Pi), no half second of nobody
  simulating while the server picks a successor.
- **A client can no longer publish a world.** `HandleSnapshot` refuses every
  peer while simulating. Before, the authority *was* a client, so "the
  authority's snapshot is trusted" and "one particular player's machine is
  trusted" were the same sentence.

**What it does not buy, and must not be sold as.**

- **A player still does not see their own hit register any sooner.** Damage is
  felt when the snapshot carrying it arrives, which is a round trip after the
  trigger, wherever the authority sits. Moving it to the server *equalises*
  that wait; it does not shorten it. Shortening it is client-side hit
  prediction and is not implemented -- see "What is still owed".
- **The server is not authoritative over movement.** `IntentPacket.Position`
  is still where its sender says they are, exactly as it was when a client
  held this role. Deriving position from buttons is the thing the intent
  stream was built to stop doing (two simulations of one player drift apart on
  the first lost packet), and undoing that needs prediction first.

So: this refactor is about *fairness and resilience*, not about latency. A
report of "my shots go through people" is answered by unlagged, which was
already there; a report of "slot 0 always wins the trades" is answered by this.

## How it works

The engine's simulation turns out to need no GL context at all.
`Scene.OnSimulationFrame` and everything under it -- input, the entity step,
collision, beams, damage -- contains no GL call anywhere, because the frame
split (`render/FRAME-PACING.md`) had already put every one of them in
`Scene.OnDrawFrame`. `grep -l "GL\." src/MphRead/Entities/ src/MphRead/GameState.cs
src/MphRead/Scene.cs src/MphRead/SceneSetup.cs src/MphRead/HUD/` finds
**nothing**. So the server runs the real engine rather than a model of it,
and the old objection -- "a reimplementation on the server would be a second
answer free to disagree with the first" -- disappears: it is the same answer,
compiled from the same file.

| Piece | What it does |
|---|---|
| `Mods.Headless.Active` | one switch, in the shape `ThumbnailMode` established. Turns off only work whose output is a picture, a sound or an answer to a person |
| `SyntheticInput` | a `KeyboardState` and a `MouseState` built by reflection, because `Scene`'s constructor demands a pair and OpenTK only makes them from a window. Nobody ever presses them |
| `NetRole.Server` | authority, `LocalSlot = -1`, no socket of its own |
| `NetSession.StartServerAuthority(sink, matchEnded)` | takes the role; the finished snapshot is handed to the relay in this same process rather than sent as a datagram |
| `ServerSim.Advance(now)` | the same fixed-step accumulator the game window runs. A server's loop is woken by packets, at no fixed rate, which is exactly what an accumulator is for |
| `DedicatedServer.Simulate` | `-simulate`. Off by default |

**The wire does not move.** `NetConfig.ProtocolVersion` is unchanged. A client
is told it is the authority by receiving `PacketType.Authority` and in no other
way, so a simulating server simply never sends it -- and a client built before
any of this joins one and behaves correctly without knowing anything changed.
`HandleIntent` feeds `NetSession.AcceptSlotIntent` one hop earlier than a
client authority got the same bytes, through the same call, so the ordering
rule that guards a rejoining player's restarted frame counter is the one that
has already been debugged.

**The simulation follows the server the way a client does.** The roster and the
match state are applied to it through `NetSession.ApplyRoster` /
`ApplyMatchState` -- the very packets it is about to broadcast. A rotation is
therefore an ordinary `NetRoomChange` transition, carrying the intro-camera and
settling fixes that path already has, rather than a second implementation free
to disagree with every client at once.

## Traps

- **`NetSlotManager.Sync` and `NetPlayerSetup.ApplyOnce` both returned early on
  `LocalSlot < 0`.** That guard is a client's "not admitted yet". A server is
  never admitted, because none of the slots is its own, so it held for the
  whole match: no slot was ever activated, the room was simulated empty, and
  the snapshot went out with a header and no players in it. This is the first
  thing to check if a simulating server publishes 13-byte snapshots.
- **`NetHooks.LocalSlot` must be -1, not 0.** It fell through to 0 for anything
  that was not demo playback, which would make slot 0 -- a real player, on
  somebody else's machine -- the one slot on the server exempt from every "this
  belongs to somebody else" test in the engine: its intent dropped, its
  keyboard read from a keyboard nobody is holding, its shots fired from the
  origin.
- **The camera mode is forced to `Roam`, and that is load-bearing.**
  `IsMainPlayer` is `this == Main && camera mode is Player`, and
  `PlayerEntity.Main` on a server is slot 0 because something has to be. In
  `Player` mode that would make one slot in eight "the main player" and send it
  down 35 branches in `PlayerProcess` alone that no other slot takes --
  including one that skips `UpdateNodeRefVolume` entirely. `Roam` makes
  `IsMainPlayer` false everywhere, so all eight slots are treated identically,
  which is what a machine playing none of them should do.
- **The multiplayer intro camera sequence must not run.** It is a camera flying
  round the room for the person about to play in it. On a server it flew for
  nobody and applied `BlockFormSwitch` and blocked input to slot 0 alone --
  and asserted (`current.PartIndex != -1`) inside `RoomEntity.UpdateNodeRef`,
  which in a Debug build kills the server outright on the first join. Every
  client still runs its own.
- **`SendMatchEnd` had nobody to send to.** A match won on points is decided by
  the simulation, and on a server that is this process; without the
  `NetRole.Server` branch the sim reached the point goal, told no one, and the
  match ran until the clock did -- which on a rotation entry with no time limit
  is for ever.
- **A simulating server may not sleep 20 ms when idle.** It owes a step every
  16.7 ms whether or not anybody is connected.
- **The two simulation timers that live in the draw pass never ran, and one of
  them is what changes room.** `UpdateFade` and the effect advance are called
  from `GetDrawItems`/`UpdateUniforms` -- correctly, because a fade and a
  particle are drawn -- and both were already careful to consume *steps owed*
  rather than pictures drawn (`_pendingFadeSteps`, `_pendingEffectSteps`).
  Nobody had to think about a caller that draws no pictures at all. The counts
  simply climbed.

  A rotation is `SetFade(..., AfterFade.LoadRoom)`, and the load happens in
  `EndFade`, which only `UpdateFade` reaches -- so **the server went on
  simulating the first map of the session for ever** while every client
  rotated correctly. It did not look broken from any client: a live player's
  position is their own report, not the server's, so the players still saw
  each other, the scoreboards still agreed, and `compare-reports.py` said
  0 mismatches. What was wrong was invisible and total -- every shot resolved
  against the collision of a room nobody was standing in. `ModStepDrawPassTimers`
  runs both from the end of the step when headless.

  **The check that catches this is the server's own log**, not a client's:
  `sim: <room>` must name the room the *scene* is in (read off `Scene.RoomId`
  every time, never remembered) and it must change when the rotation line
  above it does.
- **`UnloadModel` deletes GL objects, and a room transition calls it.** Guarded
  like the rest -- but only its GL half: `Read.RemoveModel` underneath is what
  makes a rotating server's memory reach a plateau instead of holding every map
  it has ever played. Unguarded, the first rotation threw inside `EndFade`,
  left the fade half-ended, and every subsequent step threw in the same place
  -- 1800 failures a minute, which is what `StepFailures` in the periodic log
  is for.

## Cost, measured

`-simcheck "ROOM" [-players N] [-seconds N]` runs the headless simulation on
its own, with nobody connected and synthetic intents, and reports what a room
costs in memory and in milliseconds a step. It is not a network test --
`run-check.sh` is that -- it measures the part that used to run on a player's
PC and now has to run on a server.

Every saving below is work whose only output was a picture:

| Cut | Where | Peak RSS |
|---|---|---|
| (a full client, `-maptest`, 8 players, for comparison) | | **337 MB** |
| GL context, shaders, display lists, texture upload | `InitTextures`, `GenerateLists`, `OnLoad` | 183 MB |
| the SFX bank (every sample in the game, decoded) | `Sfx.Load` takes the stub `ThumbnailMode` uses | *included above* |
| texture **pixel data**, never decoded because nothing binds it | `Read.cs`, empty `TextureData` per texture | 118 MB |
| render instructions -- the geometry of the picture. The simulation collides against the room's *collision* file and never against its render meshes | `Read.cs`, empty list per display list | **110 MB** |

MP3 PROVING GROUND, 8 players, x86-64, Debug: **110 MB peak, 0.31 ms mean
step, 2% of the 16.7 ms budget**, load 0.5 s. The .NET runtime alone is 50 MB
of that, so the room and eight players cost about 60 MB.

MP1 SANCTORUS, 8 players, on the Pi 3B (ARM64, Release, self-contained single
file): **131 MB peak, 8.04 ms mean step -- 48% of the budget**, load 11.9 s,
20 s of match simulated in 9.7 s of wall clock. It fits, with the Pi showing
225 MB still available afterwards, and it is the machine that decides the
ceiling: the same room costs a fifteenth of the budget on a desktop.

That 48% is the **worst** case and reads worse than the machine is. It is
eight players all firing on the same schedule, which is what the synthetic
intents do; a real three-player match on the same Pi ran at **0.90 ms a step**
after ten minutes, and held **exactly 1800 steps per 30 seconds** the whole
way. The `-simcheck` mean is also dragged up by its first step (1252 ms once,
540 ms another time), which is the JIT compiling the entity path rather than
the simulation -- five steps in twelve hundred overran at all. Publishing the
ARM64 server with ReadyToRun would take most of that and a good part of the
11.9 s load with it, and has not been tried.

**A room load blocks the relay**, because the sim is stepped from the server's
own loop. Measured on the Pi: about **4 seconds** for a reload with the file
cache warm (the 11.9 s figure is a cold process, JIT included), against the
30 s `NetConfig.TimeoutSeconds` that would start dropping peers. It happens
twice in a session's life -- once when the first player joins an empty server,
because `_matchId++` there is a deliberate clean restart, and once per
rotation -- and both are moments every client is loading the same room anyway,
so most of it overlaps with a wait they are already having. It is fine as it
stands; moving the sim to a thread of its own is the answer if it ever is not,
at the price of putting a thread boundary between the sim and the peer table.

The counts are preserved where they are dropped -- one empty `TextureData` per
`Texture`, one empty instruction list per display list -- because `Recolor`
asserts one entry per texture and `mesh.DlistId` indexes the lists by
position.

## Measured against the relay, on the Pi

Three scripted clients, 150 s, MP1 SANCTORUS on `net.livetek.fr`, with 150 ms
injected on two of the three (`run-remote-lag.sh`). The same scenario twice,
the only difference being where the simulation lives.

| | relay (a client is the authority) | server authority |
|---|---|---|
| cross-client mismatches | **1** | **0** |
| scoreboards | agree within 0 events | agree within 0 events |
| remote position snaps | 0 | 0 |
| per-client verdict | 3 PASS | 2 PASS, 1 FAIL -- see below |

A separate run, two clients over three one-minute matches on the Pi: **two
rotations followed, 0 mismatches**, the server's own log naming each new room
as it reached it, and 1800 steps per 30 seconds held straight through both
room changes.

The relay run's mismatch is `CHARLIE shooting: they did 2578, ALPHA saw 557` --
the authority seeing a fifth of a laggy player's trigger frames. It is gone
under server authority.

**The one FAIL is the interesting result and it is not a regression.** ALPHA
reported *"their form stayed wrong for 78 frames in a row -- authority wanted
biped, puppet alt/Morph/ended"*. In the relay run ALPHA does not report it
because ALPHA **is** the authority, and `NetFeatureCheck` skips this check on
the authority: it is the one machine with nothing to compare against.
`NetPlayerBridge` reconciles a puppet's form against `IntentButtons.AltFormState`
**only on the authority**, deliberately -- a client doing it too would take
form corrections from the owner's intent and the authority's snapshot at once,
and the two disagree for as long as the authority takes to converge.

So the refactor did not create this. It removed slot 0's exemption from it:
what ALPHA now reports is what the other seven players have always seen. What
it does raise is a fair follow-up -- with the authority no longer a player,
the "two sources" objection is weaker, and clients reconciling form from the
snapshot may now be safe. That is a change to make with a measurement, not
because it sounds right.

## What is still owed

- **The game files.** A simulating server needs them, and "a dedicated server
  needs no game files" has been true of every build so far. `ServerSim.Available`
  is the whole of the concession: without them the server says so at startup
  and relays exactly as before. Shipping them is not an option
  (`tools/check-no-game-assets.sh`), so today this is a server whose operator
  has a dump on the box.

  **What a simulating server actually needs is 52 MB**, not the 103 MB of a
  full extraction, found by pruning until it stopped loading and then checking
  four maps: `_archives`, `levels`, `models`, `stage`, `effects`,
  `cameraEditor`, `stringTables`, `aiPersonalityData`, and from `data/` only
  `sound/*.DAT` -- the music *metadata*, which `Music.Init` reads from the
  `Scene` constructor before anything can decide the process is headless. What
  is not needed: `archives/` (the packed copies of `_archives`), every sound
  sample, every movie, and the whole front end. Cutting `Music.Init` too would
  drop `data/` entirely and save another 4.5 MB, but `Music`'s statics are
  then null for gameplay code that calls into them, so it was left alone.

  The interesting alternative is for the *client* to hand the server the files
  it reads, in RAM, at match start -- `Read.cs` is nearly a single choke point,
  and a room's collision file is about 50 KB.
- **Client-side hit prediction.** See above: this refactor equalises the wait
  for damage feedback, it does not shorten it.
- **Movement authority.** Position is still the client's claim.
- **The Windows server.** `-simulate` is a Linux-tested flag.
