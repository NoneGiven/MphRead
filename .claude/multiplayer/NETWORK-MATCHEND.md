# Multiplayer — match end, rotation and the double-counted kill

A match that somebody won used to end the session for that client alone:
`GameState.ProcessFrame` ran the winner's camera, then the scoreboard, then
faded to black -- correct offline, and on a server it meant every client
dropped back to its own launcher while the server's rotation, which had never
heard anybody won, kept counting down a map nobody was still playing.

| Piece | What |
|---|---|
| `NetMatchEnd` | on the authority, sends `PacketType.MatchEnd` when `GameState.MatchState` leaves `InProgress`, repeating until the server's own state answers `FlagEnding`. Any client that sees `FlagEnding` while it still thinks the match is running sets `MatchTime = 0`, so results play out normally rather than cutting away |
| `DedicatedServer` intermission | both endings (clock and score) now enter the same 14-second intermission before `AdvanceMap` -- the client's own sequence (3 s winner's camera, `GameState.MatchEndingSeconds` = 10 s of results) plus a second, so the fade belongs to the rotation instead of cutting the results short. It was 9 (5 s of results) until the hunter picker moved onto that screen (`Mods/EndScreen.cs`): a choice somebody has to make in five seconds is a choice they make by accident. **The two numbers are one number in two places** -- a server that rotated early would take the question away mid-answer |
| `CameraSequence.Intro` reloaded per rotation | it is a **static**, loaded in one place -- `SceneSetup.LoadNewRoom`, which a rotation does not go through. So after the first map of a session, every results screen flew the sequence belonging to the map the session *started* on, whose keyframes hand the camera a `NodeRef` resolved against a level no longer in memory. Out of range that is an `ArgumentOutOfRangeException` in `RoomEntity.DrawRoomParts` killing every client at once; in range it is a black room. `NetRoomChange.ReloadIntroCamSeq` does what SceneSetup does, with the same room-id arithmetic, and calls `Initialize()` so every keyframe is re-resolved |
| `RoomEntity.ModCanPlace` | the backstop: a `NodeRef` is three indices into one particular room's arrays and nothing about it says which room, so before anything is indexed with it the indices must address something this room has and `NodeRef.RoomName` must be this room or one of its connectors. Refusing leaves `_partVisInfoHead` null, which `GetDrawInfo` already reads as "draw every part" -- the correct picture, merely uncalled. It fires nowhere in the rotation harness now that the cause above is fixed, which is the point of a backstop |
| no culling once the match is over | the end-of-match camera is the one camera in the game that is not out of somebody's eyes: an authored orbit of the winner, free to sit outside every room part in the level. That is a portal walk reaching nothing and a results screen that is black with stray polygons in the corners -- reported on MP2 HARVESTER, and *not* a stale reference. `UpdateRoomParts` returns early whenever `MatchState != InProgress` in a multiplayer match. It costs nothing worth having: nobody is playing |
| `MatchStatePacket.MatchId` | counts matches from the server's start. The room key alone can't answer "is this a new match" -- a one-map rotation (what **Host a game** builds) plays the same room repeatedly, and a client watching only the name sat on its results screen for the rest of the session |
| `MatchStatePacket.PointGoal` | the score that wins, from the rotation file; belongs to the server for the same reason the clock does |

Two things this needed underneath:

- **`NetMatchSync` must not adopt the clock during results.** `MatchTime` is
  the countdown the results sequence itself runs on; putting the old map's
  remaining time back on top of it every frame meant the sequence never
  finished.
- **The authority must stop reporting once the server has heard.** A client
  lingers in results for a second or two after the server has rotated; during
  that window the authority was reporting the end of the *new* match the
  instant rotation landed -- one whole map skipped per rotation, visible as
  two `match over` lines in the server log in the same second.

## The double-counted kill (2026-08-23)

Reported as "6-2 on the scoreboard and the match was already over for the
second client" -- which then had to be hunted down as a motionless zombie and
killed to reach 7, and crashed at the rotation that followed.

**A replayed kill was counted twice.** `NetDamage.Replay` runs the authority's
hit through `PlayerEntity.TakeDamage` so the victim feels it (indicator,
flinch, banner) -- but `TakeDamage` also *awards* the kill (`Points`/`Kills`
for the attacker, `Deaths` for the victim). The authority had already done all
three and the snapshot being applied already carried the result, so on every
machine except the authority the kill landed on the scoreboard twice, for one
frame -- enough, because `EndIfPointGoalReached` reads `TeamPoints`, and
`UpdateState` rebuilds `TeamPoints` from `Points` at the end of the same
frame, after the replay and before the next snapshot corrects it. On the sixth
kill of a seven-point match the client saw seven, ended its match, and sat on
`6-2` for the rest of the round while its player stood frozen in everyone
else's game (`UpdateScene` doesn't run outside `MatchState.InProgress`).
Reproduced exactly: one client at `matchState=GameOver score=1/2p` while the
authority was still at `score=1/1p`, 23 seconds apart.

Fix is **not** "assign the authority's scores after the replay instead of
before" -- that looks equivalent and isn't, since the replay runs on the
victim's slot and moves the *attacker's* row, so it only works if the attacker
happens to apply after the victim. `NetDamage` saves and restores the three
score arrays around the replay instead, so ordering stops mattering.

**Then the rotation exposed the same bug from a different angle.** A client
resets its scores on loading the next room, but the two machines don't change
room on the same frame -- so a client that finished loading first was still
receiving the *finished* match's snapshots, carrying the winning scores, and
applied them to a fresh match at zero. `ApplyState` now ignores peer scores
while `NetRoomChange.Settling`, for the same reason it already ignores peer
positions there: a match starts at zero on every machine, so there's nothing
to learn from the authority during that second.

**The rule that makes a third variant of this survivable:** only the machine
that keeps score may decide the score has been reached
(`NetMatchEnd.MayEndOnScore`, checked by `EndIfPointGoalReached` and
`ModeStateDefender`). Every other client's `Points` comes from the authority's
snapshot, so a client reaching the goal "first" is disagreeing with the only
copy of the scoreboard that counts, and nothing returns `MatchState` to
`InProgress` until the next rotation.

`NetMatchEnd.RecoverIfStranded` is the backstop under all of it: a client
showing results for twelve seconds while the server says the match is running
(not ending) is put back into play. Twelve is longer than the whole results
sequence and the server's own intermission, so a legitimate ending is never
cut short, and the next bug of this shape costs a hiccup instead of a player.

**The crash after rotation is unexplained** -- no log, no stack from that
client. What's known: it spent two minutes in `MatchState.Ending` while
`NetHooks.AfterSimulation` kept applying snapshots (spawns, replayed deaths,
effects, sounds) into a scene whose `UpdateScene` never ran to process or
retire them. That's the state the fixes above prevent, so it may go with them
-- not reproduced since, and not claimed fixed.

The netlog couldn't show any of this and now can: `STATE` lines carry
`matchState=` and `goal=`, and each slot carries
`score=<points>/<teampoints>p <kills>k<deaths>d` -- which is what made the
double count visible as `1/2p` in a single line.
