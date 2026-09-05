# Testing — metrics and interpreting results

This file describes how to read netcheck and maptest results and common traps.

Key lines and their meaning

| Line | Reads as |
|---|---|
| `damage pipeline (resolved here / replayed here)` | the two ends of the damage path. The authority shows `N/0`, everyone else `0/N`. They must match — a shortfall means hits were resolved and never reached the victim |
| `remote position snaps` | visible teleports (bad). Smoothed catch-up is invisible; a snap isn't. Healthy is 0-3 per client per 100 s, worst ones are respawns. A run with rotations in it is not comparable to one without — clients don't finish loading at the same instant |
| `late=N` in the packets line | snapshots that arrived after a newer one and were refused — not loss, these are reordered, and applying them would run health/score/damage backwards |
| `scoreboards agree (within N event)` | N > 1 means the clients are keeping different scores. Clients stop a few seconds apart, so one event of difference is timing |
| `form disagreed ... longest run` | one morph animation's worth is normal. A long run means a puppet is stuck in the wrong form |
| `FAIL: no beam can hurt these players` | `BeamEffectiveness` is all-zero — the player is literally invulnerable (see spawning in `MECHANICS.md`) |
| `untested` | the feature was never performed, so nothing is being claimed — not a pass, and not a fail either; it's a question about the harness |
| `dropped=N` in the packets line | this client couldn't keep up with what it was sent. Non-zero here reads on *other* clients' reports as "they never saw me turn" |
| `pings: slot 0 12 ms ...` | what the server measured per slot, which is what the scoreboard draws |
| `restreams=N` in the packets line | times this client re-based its snapshot ordering on a new source. One per authority handover is right; a stream of them means two machines are publishing |
| `server silence: N re-announce(s), longest gap X s, M authority stand-down(s)` | what a dropped connection looked like from inside. A stand-down is this client giving the simulation back after being re-admitted |
| `scoreboard mid-run:` | the board sampled two thirds of the way in, while everyone was still connected. **This is the one to compare between clients** -- the final board cannot be compared, because a departing player's score is cleared on everybody else's copy and clients stop seconds apart |
| `spectating: N frame(s)` vs `slot K was spectating on M of my frame(s)` | what a spectator meant to do, against what reached everyone else. The two must be close; they were 6001 against 189 before `PlayerEntity.Spawn`'s flag wipe was fixed |
| `alt-attack` | rising edges on the alt-attack button, both sides — separates "the press never arrived" from "it arrived and laid no bomb" |
| `node lookups unresolved: N` | frames on which a player's room node could not be worked out from its position at all, even after the body and half a unit either side were tried. Non-zero means that map has places the portal volumes do not cover, and the players there are drawn without culling rather than hidden — see the AD2 ALINOS PERCH section in NETWORK-DIAGNOSTICS |
| `SIMULATED LINE: ...` at the top of a report | this client was run with `-netlag`/`-netloss`. **Every number under it describes a line the client made up**, and none of it may be quoted as a measurement of the real thing |
| `chat: sent=N received=M` | M counts the server's own join/leave notices as well as what players typed, and clients start three seconds apart, so it is not predictable from the client count. Zero on every client is the number that means something |
| `jumppads 7 (7/7 launched)` | seven in the room, seven tried, seven launched the player standing on them |
| `afflicted freeze 1 burn 2 disrupt 0` | how many players were frozen/burned/disrupted at least once during the run |
| `(probe freeze ok burn nohit disrupt FAIL)` | the affliction probe: `ok` inflicted it, `nohit` the charged shot missed, `FAIL` hit with no state, `n/a` that hunter wasn't in the match |

Common traps

- Comparing raw totals without normalising for join times or rotations.
- Assuming a hit is a hit: afflictions require a charged shot; some weapons require holding `FullCharge * 2` frames.
- Standing at an entity's position expecting its trigger to notice — volumes are relative to entities.
- Writing a button twice in one frame: edges must be computed once, at the end of the frame, from the state at its start — computing them inside each write lets a later clear wipe the edge an earlier press produced (a charged weapon fires on release, so nothing charged ever fired).
- A tolerance of 1.0 silently turns a check into decoration — if a check stops failing, make sure it can still fail.
- Judging a Sylux against a Weavel's abilities: only three hunters lay bombs, only Weavel leaves a halfturret.
- Testing a rotation with only one map in it misses nothing — testing it with only *more than one* map misses the "is this a new match" class of bug, which needs the single-map case too. Test both `maprotation-test.txt` (three maps) and `maprotation-one.txt` (one), each with a two-point goal so a match ends inside a run.
- A run with match restarts in it is not a clean read of the tour: `NetTestScript`'s 15 phases key to the *server's* clock, and a restart resets it, so clients that finish loading a fraction of a second apart land in different phases. Expect `alt form`/`their form stayed wrong for N frames` failures a restart-free run doesn't produce — 181 frames in particular is the correction machinery's full budget working as designed (90 frames grace, a transition attempt, 90 more, then forced), and the check fails at 60, below that budget.
- Reading one client's column of a cross-check as a defect before checking the others: low for *every* observer is systematic (a rate, a sampling difference); low for one is that client's own story, usually a late join.
- Comparing final scoreboards in any run where somebody leaves. A leaver's
  score is cleared on every other client (`NetSlotManager.Deactivate`, and
  deliberately -- somebody who has gone is not on the board), and every run
  staggers its exits, so the last client to print is the only row still
  standing. One churn run read "scoreboards disagree by up to 11" for exactly
  this reason and nothing else. Compare `scoreboard mid-run:`.
- Reading a cross-check where both sides derive the number from the same
  place. `spectating` originally counted the entity flag on both the owner's
  machine and the observers', so when a respawn wiped that flag at the source
  both sides counted the same zero and agreed perfectly about a feature that
  was reaching nobody. A cross-check is only worth having if the two sides can
  disagree: the owner's side now counts what it *decided* (`SpectatorMode`).
- Reading `damage pipeline` as healthy just because both ends are non-zero: `25/0` against `0/258` is a byte counter that ran backwards and nearly wrapped forward, not noise on a working pipeline — the three-digit number is the tell.

## Frame pacing verification (2026-09-05)

The decoupled frame loop (`.claude/render/FRAME-PACING.md`): the simulation
pinned at 60 Hz under a picture drawn at the display's rate.

| Check | Result |
|---|---|
| `-frametimingcheck` | all 9 cases pass. 60.000 Hz of simulation under 60 / 144 / 165 / 240 Hz displays and under jitter; **60.000 Hz under a 40 Hz display**, where the old single-rate loop played in slow motion; a 2 s stall returns 1 step, not 120; alpha visits 10/10 tenths at 144 Hz |
| `-maptest -drawrate 1 / 3 / 4`, TEST ARENA + MP1 SANCTORUS + AD2 ALINOS PERCH | MAPTEST line **identical** at every draw rate; `draws advancing the game: 0`; blend ratio 0.666 at N=3 and 0.749 at N=4, the predicted `(N-1)/N` |
| Real `RenderWindow`, `-fpscap display` under Mesa | sim **60.05 Hz** while the picture ran at **180 Hz** (Mesa ignores VSync here, which makes it the better test), measured over 22 s from the debug log. 0 dropped, 0 stalls |
| Real `RenderWindow`, `-fpscap 120` / `-fpscap 144` | draw 119.9 / 143.8 Hz, sim 59.93 Hz, histogram a clean alternation of 0 and 1 step per frame |
| Performance against the unmodified build | 1832 identical frames in **32.9 s vs 33.1 s** -- marginally faster, not slower |
| `run-check.sh 130`, 3 clients, x3 | **0 mismatches every run**, matching the 2026-08-23 baseline |
| `run-check.sh 150`, 6 clients, x4 | 5, 5, 3, 0 mismatches -- inside the unmodified build's own range, see below |

### The 6-client mismatch count on this box measures the box

Worth writing down because it cost an hour and will cost it again. Running
`run-check.sh 150` with six clients on this WSL machine, on the **unmodified**
build, over seven runs:

```
0, 1, 0, 2, 3, 6, 9 mismatches      clients at 39.6-44.6 fps
```

Every one of them on `zoom` or `unmorph`, plus one `damage-taken`. Those are
frame counts of an observed state, scoped to a **server-clock phase**, and
`NetFeatureCheck` says so itself where `alt-form-total` is defined: the
phase-scoped count "depends on both clients agreeing where a phase boundary
falls, and a difference there looks exactly like a replication failure."

Six clients plus a server do not fit on this box: they run the tour at ~41 fps,
so each drifts against the server clock, and the later-starting clients
(CHARLIE, DELTA, FOXTROT) spend fewer frames inside each phase and count less.
The mismatch count is a measurement of that starvation.

**So do not read a 6-client mismatch count here as a pass or a fail without a
same-day run of the build you are comparing against.** Three clients fit and
give 0 reliably; that is the local instrument. Eight clients against the Pi
(`run-remote.sh`) is the real one.

## Last verified status (2026-09-01, the hard-case batch)

Full detail, including the eight faults it found, in
`.claude/testing/TEST-HARD-CASES.md`.

| Check | Result |
|---|---|
| 8 real clients, 220 s, against the Pi | 0 mismatches, 0 position snaps, 2 ms pings, `dropped=0` |
| Map rotation with real clients | **was killing every client**; fixed (`NodeRef.None` on rebuild), 0 of 3 crashed afterwards and both rotations were followed |
| A player's line cut for 1, 3, 8 and 40 s | heals by itself under the 30 s timeout; at 40 s the peer is dropped, the authority moves, and the same slot comes back within a second of the line returning |
| Latency 100 / 200 / 300 / 500 ms (kernel `netem`) | scoreboards identical at every level; snaps 0/0/18/190 |
| Loss 5 / 15 / 30 %, both legs | no divergence; hit registration is what degrades |
| Every player spectating | replicates properly now; nobody can be hit; match and rotation carry on |
| Six clients recording demos | 13 KiB/s each, every file replays |
| Ninth player at a full server | `Refused` reason 1 in 11 ms (server deployed 2026-09-01); a protocol-3 Hello gets reason 2 |
| Pi 3B ceiling | 20 concurrent hosted matches / 160 players; past that new players cannot join while those inside keep 98.7 % delivery |
| Server journal across 3.5 h of hostile testing | zero exceptions or errors |

## Previous verified status (2026-08-23)

| Check | Result |
|---|---|
| 3 clients, 130 s, `run-check.sh` | PASS on all three, 0 mismatches, scoreboards agree within 0 events |
| Damage pipeline on that run | 69/56/49 resolved on the authority, 69/56/48-49 replayed on both observers (shortfall = one hit still in flight at cutoff) |
| Match won on points, 3-map rotation, 4 clients | every rotation announced once, all four peers carried across. **Superseded 2026-08-31: with real clients on the Pi, a rotation to a DIFFERENT room killed every client outright** -- `ArgumentOutOfRangeException` in `RoomEntity.DrawRoomParts`, a pooled player's `NodeRef` into the old room indexing the new room's part arrays. Reproduced 3/3 on the build before this batch's changes and 0/3 after `NetRoomChange.RebuildPlayers` hands each slot `NodeRef.None`. A rotation to the SAME room (a one-map rotation) never hit it, which is why five consecutive matches on one map passed while the first change of map did not |
| Match won on points, one-map rotation | correctly reloads the same room — the case that used to strand everybody |
| Authority leaving mid-session | promoted to the next peer, session continued |
| `tools/check-dedicated-server.sh` | server and directory both start from a published build, register, and answer |

Last full map pass (2026-08-20):

| Check | Result |
|---|---|
| Every multiplayer map, 8 players / 8 AI bots, 22-24 s each | 33/33, zero crashes, both modes |
| Players placed | 8/8 on 27 maps; 0/8 on the six First Hunt "biodefense chamber" rooms (no spawn points — the only `MAPFAIL`s) |
| `moved N/M (furthest X units)` | how far each player actually got, summed per frame and ignoring respawn jumps. The metric that separates a bot that cannot navigate from one that is losing: a bot standing still still counts as spawned, and never firing or dying reads the same either way. DUST2 with generated node data gets 7/8 and 785 units over 150 s; MP3 PROVING GROUND gets 8/8 and 369 over 60 |
| Jump pads / teleporters, stood on in turn | every pad on all 24 maps that have one; both of AD1 TRANSFER LOCK's teleporters moved the player |
| Afflictions, probed per map | freeze on 14 maps, disrupt on 13, burn on 6 — a sample, not a verdict: ~2 charged shots per state per map, and the probe's first (uncharged) press of each cycle can land a hit that registers as "hit, no affliction," which understates burn's column most |
| 6 clients against the Pi, 110 s | 3 mismatches, all late-joiner/stop-skew artifacts; pings 6-16 ms, `dropped=0` |
| Pi 3B under 6 clients | server process 11-16% of one core, system 65-75% idle |
| Invulnerable players (`BeamEffectiveness` all-zero) | none |
