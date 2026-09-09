# Testing — the hard cases

`TEST-HARNESS.md` covers the normal run: several real clients playing the
tour and cross-checking what they saw. This file covers the runs where
something is deliberately wrong — a player disconnects, a line goes away, a
ninth player arrives at an eight-slot server, everybody spectates at once,
the Pi is asked to relay twenty matches.

Everything here runs **against the Pi** (`net.livetek.fr`), never a loopback
server. A loopback server has none of the reordering, none of the jitter and
none of the Pi's processor; a result from one must not be reported as a
result about the real thing.

## The two instruments

| Tool | What it is | What it must never be used for |
|---|---|---|
| `-netcheck` (real client) | the whole engine, driven by the tour | nothing — it is the only thing that can say what a player would see |
| `netprobe.py` / `netload.py` (Python) | the wire protocol with no engine behind it | any claim about what a player sees. It measures the **server** |

The Python side exists because several of these questions cannot be asked
with a real client: a Hello claiming protocol 3, a Snapshot from somebody
who is not the authority, a hundred and sixty players from one box. It
mirrors `NetProtocol.cs`, so **a layout change there must be mirrored in
`netproto.py`** or every probe reports a dead server.

## The rig

```
~/mph-net-test/
  netproto.py        the wire format: packets, a virtual client, a status query
  netprobe.py        the edge-case suite -- one subcommand per question
  netload.py         synthetic players, for capacity: one process per game
  udp-blackout.py    a relay that can take the line away and give it back
  udp-lag.py         a relay that holds every datagram for a fixed delay
  hard/common.sh     shared setup: build refresh, leftover kill, host_game,
                     pi_ssh/pi_watch/pi_journal (the Pi, from the Pi)
  hard/pi-sample.py  copied to the Pi: CPU per role, memory, eth0, UDP errors
  hard/run-*.sh      one scenario each
  hard/rotation-ab.sh  the same rotation with two builds, to answer "whose bug"
  hard/run-all.sh    the batch, serialised (they all share one server)
  hard/run-all2.sh   the second half: match boundaries, promotion, capacity
  hard/run-all3.sh   the third: the A/B, and everything the first two got wrong
  hard/report.sh     one screen of what a batch found
```

The Pi's own numbers need `~/.mph-pi-pass` (600) or `MPH_SERVER_PASS`; without
either, `pi_watch` is skipped and the scenario says so rather than reporting a
one-sided run as a whole one.

## The scenarios

| Script | The question |
|---|---|
| `run-capacity.sh` | eleven real clients at an eight-slot server: who gets in, and what the refused ones are told |
| `run-blackout.sh` | one player's line disappears for 1, 3, 8 and 40 s while the others keep playing |
| `run-churn.sh` | players leaving and rejoining mid-match, both by quitting and by vanishing |
| `run-authority.sh` | the authority leaving mid-match, and coming back into a match it no longer runs |
| `run-latency.sh` | 100 / 200 / 300 / 500 ms of round trip, added by `netem` in the kernel — the same line for every client |
| `run-netlag.sh` | one match, a **different** line per client (`-netlag`, inside each process): the report that says "a couple of players had 100-200 ms and stuttered" is a mixture, not a uniform delay, and which of them the server made the authority is most of the answer |
| `run-loss.sh` | 5 / 15 / 30 % packet loss, shaped on both legs, no added latency |
| `run-spectate.sh` | one spectator among players, then every player spectating at once |
| `run-demos.sh` | every client recording the same match, then replaying every file |
| `run-rotation.sh` | a match boundary crossed with real clients: multi-map and single-map |
| `run-fullhouse.sh` | eight real clients, nothing artificial — the baseline the rest is read against |
| `run-pi-limit.sh` | how many matches the Pi will relay: a ramp of hosted games with synthetic players |

`netprobe.py` subcommands: `capacity`, `protocol`, `slotclaim`, `spoof`,
`matchend`, `fuzz`, `names`, `churn`, `idle`, `statusflood`, `masterhost`.

## Against a server that simulates the match

`-simulate` (`.claude/multiplayer/NETWORK-SERVERAUTH.md`) moves the simulation
authority off the first client and onto the server. Point the whole batch at
one with `MPH_SERVER_PORT`:

```bash
MPH_SERVER_PORT=27919 ./hard/run-all.sh serverauth
```

Three things in this rig assume a *client* authority, and two of them had to
be taught the difference. **The probes detect it rather than being told**: a
client is made the authority by being sent `PacketType.Authority` and in no
other way, so a first client that was not sent one is talking to a simulating
server.

| Where | What changes |
|---|---|
| `netprobe.py spoof` | `authority-is-first` becomes `no-client-authority`. And the watcher now receives snapshots constantly and legitimately -- from the server -- so "did any arrive" stopped being the question: it is `liar-frame-not-relayed`, whether the stream ever took the liar's frame number |
| `netprobe.py matchend` | there is no client that *may* end the match, so the check becomes `client-cannot-end-it`: spamming `MatchEnd` must move neither the match id nor the ending flag. The rotation itself is still exercised, on the server's own clock, by `run-rotation.sh` |
| `hard/run-authority.sh` | its premise is gone -- nobody is promoted and nothing is handed over. The run is still worth doing as "the first player leaves and comes back", and what it should now show is *no* stand-downs and *no* handover in the server's log |

**And read the server's own log, which is new.** With no client simulating,
two measurements exist only there:

```
sim: MP1 SANCTORUS (Battle), 14311 step(s), 0.86 ms mean, ... , 0 FAILED
sim: lag compensation: 168 shots rewound, mean 7.7 frames (128 ms), ...
```

The first says whether the box is holding 60 Hz (1800 steps per 30 s, and
`FAILED` non-zero is a bug, not a slow machine). The second is the whole of
the lag-compensation measurement now: every `-netcheck` report correctly reads
*"nothing to compensate"*, because no client rewinds anything any more. And
the room named on that line must follow the rotation -- a server whose
simulation silently stayed on the first map still produces 0 mismatches,
because a live player's position is their own report.

## What the batch found against a simulating server (2026-09-08, the Pi)

`MPH_SERVER_PORT=27919 ./hard/run-all.sh serverauth`, against
`-simulate -nomaster` on the Pi. Fifty-seven minutes.

**Nothing in the server broke.** Over the whole batch, from its own log:

| | |
|---|---|
| simulation steps | **206,739** -- 60.00 Hz held for the hour, 101 dropped (0.05%), 5 stalls |
| steps that threw | **0** |
| step cost | 1.63 ms mean, 48 overruns in 206k |
| lag compensation | **24,662 shots rewound**, mean 4.4 frames (74 ms), worst 24 (the clamp, from the 300 ms clients), catch-up 48,344 steps / 3,452 hits, **0 history misses** |
| rotations followed | 4 |
| the box | 136 MB RSS, 19% of one core, 222 MB still free |

Probe results, once the rig was repaired (see below): **42 of 42**. The three
that are new or changed all passed --
`spoof/no-client-authority` (nobody is promoted),
`spoof/liar-frame-not-relayed` (the watcher took 424 snapshots and the lowest
frame in the stream was 37856; the liar sent frame 3), and
`matchend/client-cannot-end-it`.

Scenario results, against the old client-authority baseline in the table
further down:

| Scenario | Result |
|---|---|
| capacity, 11 clients at 8 slots | 8/8 checks; slots 0-7 distinct, 4 refused, the eight playing undisturbed |
| blackout, 1/3/8/40 s cuts | 7 mismatches, **all of them ALPHA's own shots** -- 345 fired into a dead line, ~145 seen. **2 position snaps** (worst 19.9 units). Scoreboards agree within 0 events. Under a client authority ALPHA *was* the authority, so its blackout forced a handover on everybody; now it is one player's problem |
| everybody spectating | **0 mismatches**, and observers saw 6000+ frames of it each |
| every client recording a demo | **0 mismatches, 6/6 PASS**, `dropped=0`, and `snapshots sent=0` on every client -- the architecture, visible in the numbers |
| latency ladder 0/100/200/300 ms | mismatches 0/0/6/6 against 0/0/1/9 before; **position snaps 0 at every level, against 0/0/18/190 before**. The visible teleports are gone |

The mismatch counts move around between runs (the tour does not fire the same
number of shots twice) and should not be read as a pair; the snap counts do
not, and 190 to 0 at 300 ms is the result worth keeping.

### What the batch found in the rig, not the server

Three faults, all of which made the batch report a healthy server as a broken
one, and all of which had been there since before this work:

- **`netproto.py` was two protocol versions behind** (`PROTOCOL = 4`, and an
  `IntentPacket` with no `AckFrame`). Every probe was refused at Hello by any
  server built since, and the batch read that as the server being down.
- **`run-all.sh` never passed `--port` to the Python probes**, only `--host`.
  A batch pointed at a second server with `MPH_SERVER_PORT` silently tested
  the first one -- which is how the first run of this batch produced a page of
  protocol failures against the production server.
- **`parse_roster` was missing the suit-colour byte**, so every entry was read
  one byte short of where it starts. It reported seven clients all in slot 0
  with unprintable names: a server bug that was not one. With it fixed,
  `names` is 4/4.

## What the first full run found (2026-08-31/09-01, against the Pi)

Eight faults, every one reproduced on the real server, and three of them
things a player would meet on an ordinary evening:

| Fault | How it showed | Fix |
|---|---|---|
| **Every client crashes when the server changes map** | 3 of 3 clients died in `RoomEntity.DrawRoomParts` at the first rotation; reproduced on the build from *before* this batch, so it is not a regression from it | pooled players keep a `NodeRef` into the room just unloaded; `NetRoomChange.RebuildPlayers` now hands each slot `NodeRef.None` (0 of 3 crashed, 2 rotations followed) |
| **A player whose line drops strobes on every other screen** | 1600-2176 position snaps per client in a run with 52 s of cuts, 0 in every clean run | the position pin is "this player says they are here", which stops being true when they stop saying it: `NetHooks` now skips it once an intent is half a second stale |
| **Spectating never left the spectator's own machine** | 6001 frames spectating, observers saw 0 | the intent had no spectating bit and only the authority's snapshot carries one: `IntentButtons.SpectatingState` |
| **A spectator's own respawn put them back in the match** | observers saw 189 frames of 6001 | `PlayerEntity.Spawn` clears `Flags2` wholesale; `NetHooks` re-asserts the flag every frame |
| **An ex-authority kept simulating after reconnecting** | it ignored every snapshot and played a private match | `PacketType.Authority` only ever promotes, so a client stands down on re-admission and waits to be told again |
| **An authority handover froze every puppet for ~2.7 s** | 159 and 169 snapshots refused in a row as "late" | the new authority's frame counter is its own: the ordering guard re-bases after 12 consecutive older snapshots |
| **A refused player could not tell "full" from "off"** | eight seconds of silence, then a three-way guess | `RefusedPacket` from the server, and a StatusQuery fallback for servers already deployed |
| The scripted tour drove a spectating player | "spectating" tested nothing | gated in `NetHooks` |

And what held up, measured rather than assumed:

| Question | Answer |
|---|---|
| Ninth player at an eight-slot server | refused, slots 0-7 distinct, the eight playing were undisturbed while three refused clients hammered Hello |
| 100 / 200 / 300 / 500 ms of round trip | scoreboards **identical at every level**; snaps 0/0/18/190, mismatches 0/0/1/9/16 -- observation gets coarser, nothing diverges |
| 5 / 15 / 30 % packet loss | no divergence at any level, scoreboards agree exactly; what degrades is hit registration |
| Connection lost for 1, 3, 8 and 40 s | under 30 s the server never notices; at 40 s it times the peer out, promotes, and re-admits it to the same slot within a second of the line returning |
| Every player spectating at once | match, clock and rotation carry on; 0 mismatches; nobody can be hit, which is the point |
| Every client recording a demo | 13 KiB/s each, `dropped=0`, all six files replay, 3.5x compression |
| Match end and rotation | announced once, to the announced map; a one-map rotation reloads correctly (five consecutive matches) |
| 313 malformed / truncated / oversized datagrams | server still answering, joinable and playing; zero exceptions in its journal all session |
| A browser polling at 233 queries/s | the player's ping went 12 ms -> 16 ms |
| Eight real clients, 220 s | 0 mismatches, 0 snaps, 2 ms pings, Pi at 25 % of one core, no UDP errors |
| The directory's hosted games | 20 (its configured range), all serving, the 21st refused with a reason |

## What the Pi will hold (2026-09-01)

A ramp of hosted games, eight synthetic players each, every one at a full
60 Hz, with the box sampled from inside (`hard/pi-sample.py`):

| Games | Players in | Delivery | An idle poller's round trip | Pi CPU (400% = all four cores) | The directory process | UDP errors |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 8/8 | 100% | 21 ms | 15.7% | 55% | 0 |
| 4 | 32/32 | 99.8% | 29 ms | 45.0% | 150% | 0 |
| 8 | 64/64 | 99.8% | 54 ms | 53.5% | 187% | 0 |
| 12 | 96/96 | 99.3% | 81 ms | 65.3% | 231% | 0 |
| 16 | 128/128 | 99.0% | 103 ms | 77.2% | 283% | 0 |
| **20** | **160/160** | 98.8% | 125 ms | 86.9% | 331% | 0 |
| 24 | 160/192 | 98.7% | 124 ms | 87.4% | 332% | 0 |
| 32 | 160/256 | 98.7% | 102 ms | 87.5% | 330% | 0 |

**Twenty matches, a hundred and sixty players.** Past that the numbers stop
moving: the directory process plateaus at ~330% of the four cores and the
joined count sticks at 160, because the limit arrives as *new players cannot
get in* rather than as anything going wrong for the players already in --
three whole games at the 24-game step never got a single player, each of
their clients having sent twelve Hellos over six seconds to no answer, while
everyone already playing kept 98.7% of their snapshots and the box logged not
one UDP error.

What it costs before that: one full-rate eight-player match is about 55% of
one core, and **concurrency costs more than throughput does** -- at a
constant 1.7 MB/s outbound, going from four games to eight pushed an idle
poller's round trip from 29 ms to 54 ms, because each game is two more
threads inside one process.

Two honest limits on the table. Above four games the load generator could not
offer a full 60 Hz (this box tops out near 2000 datagrams a second to a
remote host -- see the `sendto` trap below), so the higher rows are the Pi
holding *more matches* at a *fixed* total traffic, not more traffic. And the
players are synthetic: this measures the relay, and says nothing about
whether a person would have enjoyed the game.

## Traps this batch walked into

- **A pipeline is a subshell.** `refresh_build | tee summary.txt` set `GAME`
  in the subshell and nothing in the caller, and with `set -u` every client
  died on the spot — reported as eleven clients failing to *join*. Anything
  a script needs afterwards has to be assigned outside a pipe.
- **A leftover client from an earlier run is another player in the match**,
  and an unexplained one: two different players called SMOKEA in one report
  cost half an hour. `kill_leftovers` runs before every scenario now.
- **A probe that answers pings is not idle.** The server takes a Pong as a
  sign of life, so the "silent client" check watched a client that was
  quietly staying alive. `Client.auto_pong = False` is what makes it look
  like a line that has gone away.
- **A flood measured from the process that sends it measures that process.**
  The first status-flood run reported the player's round trip going from
  ~15 ms to 242 ms; the sender had starved its own receive loop. The number
  that decides it is the server's own measurement, off the roster it
  broadcasts once a second, with the flood in a process of its own.
- **A relay written in Python is the experiment, not the instrument.**
  `udp-lag.py` asked for 100 ms with five clients behind it and produced
  round trips of 1767-1859 ms while losing 40% of the traffic: a
  single-threaded forwarder cannot move ~7000 datagrams a second. Latency and
  loss are now shaped by the kernel (`netem` on `eth0`, and on an `ifb`
  device fed by an ingress redirect for the return leg), which is exact and
  free. `modprobe ifb numifbs=1` succeeds on this kernel and creates no
  device -- `ip link add ifb0 type ifb` is what makes one, and without it the
  whole chain silently falls back to shaping one direction.
- **`sendto` costs 3.9 ms on a socket that is also receiving**, through this
  box's WSL NAT -- against 0.019 ms to a loopback server with the same code.
  It is a blocking cost, so one thread doing eight sends a frame tops out at
  31 Hz however idle the processor is, and the first capacity ramp therefore
  offered the Pi a third of the load it claimed to. `netload.py` gives every
  virtual client its own sender thread and reaches the rate asked for; it
  prints the rate it actually achieved, and the ramp says so per step, because
  a load test that quietly under-loads reads exactly like a server with
  headroom.
- **The session's clock is frames, not seconds.** `NetSession` is driven by
  `Scene._globalElapsedTime`, which advances one frame's worth per frame, so
  every "second" it measures is a sixtieth of a frame count. Five real
  clients on one box render at about 24 fps, and a 40-second outage was
  reported as 15.9 "seconds" of silence. The netcheck report now prints the
  client's real frame rate and converts.
- **A hosted game the directory started is torn down if the same address
  asks for another one while it is still empty** (`StartHosted`: one game per
  asker, replaced only when empty). A ramp that asks for twenty games has to
  put a player in each one before asking for the next, or it ends up with
  one.
- **A game hosted with mode 0 is `GameMode.None`, not Battle.** `SingleMatch`
  substitutes Battle, but the request should say what it means.
