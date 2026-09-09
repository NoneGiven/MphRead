# Instant hit registration

Code: `Mods/Network/NetHitPrediction.cs`. The other half of
[lag compensation](NETWORK-UNLAGGED.md), and it only works because that
exists.

## The fault

`NETWORK-UNLAGGED.md` fixed *where* a client's shot is resolved. It did
nothing about *when* the answer comes back, and said so:

> What it does **not** buy is a shorter wait for your own hit to register --
> that is a round trip wherever the authority sits.

So a client fires, the intent goes upstream, the authority resolves it, the
snapshot comes back, and only then does the victim flinch. At the Pi's 15 ms
nobody notices. At 150 ms a player empties a clip into somebody who reacts a
fifth of a second after each shot -- and the shots were landing the whole
time. The complaint is not "my shots miss" any more, it is "nothing happens
when I shoot".

**The authority never had this.** Its own gun resolves in the frame it is
fired. That asymmetry is the whole of what is removed here: everybody who is
not the authority resolves their own shots locally, immediately, and the
authority's answer arrives afterwards to confirm or overrule it.

With a **simulating server** (`-simulate`) nobody is the authority, so this is
the only thing that gives anyone an instant hit at all.

## Why it is sound here

Because the rewind is already there. The authority puts every other player
back to the snapshot frame the shooter had applied -- which is exactly the
world the shooter's own machine is holding at the moment it fires. The local
resolution and the authority's rewound one are therefore the same test against
the same positions. Prediction is not a guess about what the authority will
say; it is the same calculation, run earlier, on the machine that already has
the inputs.

Without `NetUnlagged` underneath it this would mispredict as often as shots
used to miss, and it would be worse than useless: a hit shown and then taken
away is more confusing than a hit shown late.

## Two rules

1. **A prediction never scores and never ends a match.** The death path awards
   the kill, and on a predicting machine that award is transient: the
   scoreboard is assigned from the snapshot for every slot on every
   `ApplyState`, so the authority's `Points`, `Kills` and `Deaths` overwrite it
   within a snapshot. What could not be undone is the *match ending* on a
   score that turned out not to have been reached, and that is already refused
   -- `EndIfPointGoalReached` returns immediately unless
   `NetMatchEnd.MayEndOnScore`, which is false on any machine that is not
   keeping the score. This is why there is still no
   `SaveScores`/`RestoreScores` here as there is in `NetDamage.Replay`: the
   replay runs *after* the authority has already counted the kill, so its
   award would be a second one; a prediction's is a first one that is
   corrected.
2. **A prediction is only your own shot on somebody else.** Incoming damage is
   never predicted. Whether *you* were hit is a question about a shot fired on
   another machine and aimed at a copy of you that machine is holding; this
   one has no better answer to it than the authority's, it has a worse one.
   The single exception is the health your own Shock Coil drains out of
   somebody -- the arithmetic of a hit this machine has already resolved, not
   a guess about anybody else's input. See *The drain* below.

There used to be a third, and it was the first: **a prediction never kills**,
with the damage clamped at the last moment to leave the victim standing on one
point of health. `-nodeathprediction` puts it back, and `LethalHeld` still
counts what it holds. It went because it was visible: the prediction stopped
exactly one point short of the thing it was predicting, and a player emptying a
clip watched the bar stick at 1 and the body stay up until the authority
answered. Everything the rule was protecting turned out to be either corrected
by the snapshot (the score) or already refused (the match end).

## What is held

Predicting a hit and then letting the next snapshot assign the authority's
health straight over it is a prediction that lasts one frame. The flinch is
instant, the mark lands -- and the bar springs back up, because
`ApplyState`'s `player.Health = state.Health` is describing a world one round
trip old. At the Pi's 15 ms nobody sees it; at Japan's 270 ms it is most of
what "it is not registering" is actually describing.

So three things are held against the snapshot until the authority catches up:

| Held | How | Where |
|---|---|---|
| the victim's health | the authority's number less the damage of every prediction still outstanding for that slot | `HealthFor`, called from `ApplyState` |
| a victim predicted dead | the snapshot is not allowed to spawn them | `HeldDead`, checked in the `!wasInPlay` branch |
| this machine's own drained health | the authority's number plus every drain credit still outstanding | `LocalHealthFor` |

`HealthFor` never returns zero on its own account: assigning zero health is
not a death -- it skips the whole death path -- so a hold that ran the bar to
the bottom would produce a player who is neither alive nor dead. A predicted
kill goes through `TakeDamage` like every other hit, and `HeldDead` is what
keeps it down.

**The hold window is not the pending window.** `PendingFrames` is 120 -- two
seconds, deliberately generous, because a confirmation that arrives late is
still a confirmation and counting it as a miss would flatter nothing.
`HoldFrames` is one measured round trip (`NetSession.SlotPing` for the local
slot) plus twelve frames, clamped to 15-90. A mispredicted hit is a *wrong
health bar*, and a wrong health bar has to right itself in about the time the
authority takes to answer rather than in the time it takes to be certain it
never will. At Japan's 270 ms that is 28 frames; with no ping measured yet it
is the 15-frame floor.

## Your own splash, on you

A rocket jump is not damage that arrives late -- it is a jump that does not
happen. The push comes out of `TakeDamage` (`Speed += direction * 0.4f`), so
suppressing the hit suppressed the jump with it, and at Japan's 270 ms the
player left the ground a fifth of a second after the missile went off. Same for
a bomb jump, and for every weapon carrying `WeaponFlags.SelfDamageUncharged` --
the Missile, the Magmaul, the Battlehammer and their charged forms.

There is **no shooter-side recoil in this engine**: nothing pushes you for
firing, and the only `Recoil` in the tree is a platform's. The push a player
means by "recoil" is this one -- their own splash, on themselves.

`Predicts` used to refuse any hit whose victim was the local player. That
refusal is rule 2 and it is already made by the line under it: damage from
somebody else has an owner who is not this slot. What the extra clause actually
excluded was the one hit that is **entirely** local -- source, target and input
all on this machine, nothing to guess about anybody, and no rewind to bet on.
It is arithmetic, not a prediction, and it is the hit whose feedback matters
most on the frame it happens.

What had to move with it:

| | |
|---|---|
| `Predicts` | drops the victim clause; the owner clause is the rule |
| `NoteHit` | no longer returns early on `attacker == victim`, and **always clamps a self-inflicted lethal hit**, whatever `DeathEnabled` says |
| the mark | not raised for a self-hit: the X answers "did that land on somebody" |
| `LocalHealthFor` | subtracts the outstanding self-debit as well as adding the drain credit -- nothing calls `HealthFor` for the local slot, so without this the health came off for one frame and the next snapshot handed it back |
| `NetDamage.Replay` | `mine` no longer excludes the local slot, or the authority's copy of a hit this machine has already applied would take the health twice |
| `SelfPredicted` / `SelfConfirmed` | counted apart from `Predicted`/`Confirmed`: the percentage is a claim about shots aimed at other people over a wire, and a hit resolved on the machine that fired it would only flatter it |
| `NoteRespawn` | now called for the local slot too, so a debit from the last life cannot come off the health of the new one |

**A self-inflicted prediction never kills, on purpose, and this is not
`DeathEnabled`.** The knockback is applied regardless of what the damage number
ends up being, so the clamp costs the jump nothing -- the push lands either
way. What it avoids is the local death path run on a guess about the machine's
own player: the death camera, `_deathCountdown`, `PausePrevented` and the
respawn are far more to take back than a puppet lying down, and none of it is
what "the jump has to be instant" is asking for. Rocket-jumping at 1 HP
therefore still dies a round trip late, which is the one case that is not
instant and the one where nobody is waiting on the answer.

## The drain

The Shock Coil -- `WeaponFlags.LifeDrainUncharged`, Sylux's affinity weapon --
gives whatever it deals to whoever fired it, in `BeamProjectileEntity`'s
player-collision branch. On a client that heal ran locally and was assigned
straight back off again by the next snapshot, so the one weapon in the game
whose whole point is the health it buys was the one weapon whose payoff
arrived a round trip late while its damage arrived instantly.

`NoteDrain` records what the shooter *actually gained* -- `Health` before and
after `GainHealth`, not the damage that was offered, because the halfturret
splits a heal in two and a full tank takes none of it -- and `LocalHealthFor`
adds the outstanding credit on top of the authority's number.

**A credit, not an absolute.** Holding "the health I think I have" would hide a
rocket that lands while the beam is running; adding a credit to what the
authority says means every point of damage taken still shows the moment it is
reported, and a stale credit is worth a point or two for a fraction of a
second. The credit expires on `HoldFrames` like everything else here, which is
about when the authority's own number starts including it.

The Shock Coil resolves a hit **every other frame** for as long as the trigger
is held, which is why `PendingCapacity` is 24 rather than the 8 it started at:
at 300 ms of round trip nine hits are outstanding before the first answer
arrives, and an overflow no longer merely loses a statistic -- it drops that
hit's damage out of the health the victim is being held at.

## The mechanism

| Piece | Where |
|---|---|
| `NetDamage.Suppress(victim, source)` | the top of `TakeDamage`. Asks `NetHitPrediction.Predicts` before throwing the hit away, so a "no" costs exactly what it always cost |
| `NetHitPrediction.NoteHit(victim, attacker, ref damage)` | beside the existing `NetDamage.Note`, which is the last point at which the damage is final and the death has not been decided. Clamps, records the pending prediction, and raises the mark |
| `NetHitPrediction.Confirm(slot, landed)` | inside `NetDamage.Replay`, **before** its "already down here" return: a hit the authority credits this machine's player with, on a victim it already predicted, is consumed and **not** shown a second time. `landed` is the snapshot's own count of hits since the last one, so two of this machine's hits inside one snapshot window retire two predictions rather than one -- capped at what is outstanding, so a burst that included somebody else's hits cannot retire more than this machine predicted |
| `NetHitPrediction.HealthFor` / `HeldDead` / `LocalHealthFor` | `NetPlayerBridge.ApplyState`, on the three lines that used to assign the authority's health and spawn a puppet unconditionally |
| `NetHitPrediction.NoteDrain(healer, gained)` | `BeamProjectileEntity`'s life-drain branch, beside the `GainHealth` it is reporting |
| `NetHitPrediction.ForgetSlot` / `ForgetPending` | `NetSlotManager.Activate`/`Deactivate` and `NetRoomChange`: a prediction describes a hit on a particular player in a particular room, and a lethal one kept across either would hold the slot's next occupant dead on this screen |
| `NetHitPrediction.Tick()` | `Renderer.OnSimulationFrame`, next to `NetHooks.AfterSimulation` -- outside the network hooks because the mark is drawn offline too |

Nothing is rolled back, because nothing durable is ever written. Health is
assigned from the snapshot on the very next `ApplyState` -- the same line that
has always corrected it -- and the afflictions are flags on the wire that the
same snapshot re-asserts or clears. A prediction the authority never confirms
simply ages out.

### What counts as a shot

`OwnerOf` resolves a damage source to the player who aimed it:

- a **beam** whose owner is a player,
- a **beam** whose owner is a halfturret (Weavel's turret shoots for him),
- a **bomb**, which belongs to whoever laid it,
- a **player**, which is how an alt form's attack is delivered -- Weavel's
  scythe, Spire's spin, Sylux's trail. These are the hits that feel worst
  late, because they land at arm's length: the whole attack is over before the
  answer to it comes back. Missing this case was worth 11 predictions of 11 in
  one run, and until it was added a Weavel client predicted **nothing at all**
  while the authority credited it with hits.

### The mark

Four bars in an X around the crosshair, `Renderer.DrawHitMarker`, twelve
frames with a fade over the last six. Same flat-fill trick as
`DrawCustomCrosshair` -- the RTT shader's `fade_color` path, no asset, no
sprite -- and sized off the crosshair's own scale, so a player who asked for a
big crosshair gets a mark to match. Drawn over whichever reticle is in use,
because "did that land" is not a question about which crosshair somebody
picked.

It is drawn on **every** machine and in every match, offline included: on the
authority and in an offline match the hit has actually happened, and there is
no reason the confirmation should depend on which machine is running the
match. Not in the story mode, which is the DS's game and has no such mark.

## Measuring it

Every `-netcheck` report carries a line:

```
hit prediction: 26 predicted, 24 confirmed (92.3%), 2 denied, 0 unpredicted,
                0 kills left to the authority
```

- **confirmed** -- the authority agreed. This is the number.
- **denied** -- predicted here, never confirmed. An upper bound on
  mispredictions rather than a count of them: a snapshot names only the *last*
  attacker, so a hit of yours that landed in the same snapshot window as
  somebody else's is invisible to `Confirm` and times out looking like a miss.
- **unpredicted** -- the opposite error, and the one that costs nothing: the
  authority credited a hit this machine did not resolve locally, so it is
  shown when it arrives, which is what every hit used to do. Weavel's
  halfturret produces these on purpose -- it picks its own targets on every
  machine, so its shots are not the same shots.
- **kills predicted / undone** -- lethal predictions made here, and the ones
  the authority never confirmed, counted as they expire. `undone` is the
  number to watch: a wrongly killed player is the most visible thing this file
  can get wrong. Under `-nodeathprediction` the line reads **kills left to the
  authority** instead, which is `LethalHeld`.
- **health drained ahead** -- points of Shock Coil drain credited before the
  authority reported them. Absent when the run never fired one.

`-nohitprediction` is the control, `-nodeathprediction` the control for the
lethal half alone, and `-nohitmarker` turns off only the mark. All three are on
by default, as `-nounlagged` is off by default.

### Verified 2026-09-08/09 (WSL, loopback)

| Check | Result |
|---|---|
| `run-check.sh 120 Samus Weavel Sylux`, no lag | **97.6%** confirmed (42 predicted), and **87.0%** on a second run (23 predicted) |
| `run-unlagged.sh 90 150 on`, 150 ms on two clients | **92.3% / 88.9%** on one run and **86.0%** (114 predicted, the largest sample taken) on another; the rewind itself unmoved at 163 ms against 150 injected, 0 history misses |
| `run-serverauth.sh 100 Samus Weavel Sylux`, simulating server | **0 mismatches**, scoreboards agree, and all three clients predicting: 100%, 100%, 88.9% |
| 5% packet loss and 120:30 ms of jittery lag on every client | **89.0%** (118 predicted) and 100% (16 predicted); damage pipeline exact (118/9/29 resolved, 118/9/29 replayed); 0 history misses at a 311 ms mean rewind |
| damage pipeline | matched exactly on the runs where it was clean before (24/26/9 resolved, 24/26/9 replayed) |
| the same scenario with `-nohitprediction` | the pre-existing `shooting` mismatch reproduces identically, so it is not this |

**Take the range, not the best run: 86-98% confirmed, and the small samples
not at all.** The scripted tour does not fire the same shots twice -- one pair
of runs differed by a factor of five in predictions made -- so a run with six
predictions in it says nothing, and the honest summary is that between one in
seven and one in forty predictions is not confirmed, with the share rising
with latency. The 114-prediction run at 150 ms is the one to quote.

Before the alt-form case was added, the same 150 ms instrument read 70% and
81%; the difference is entirely Weavel's scythe being predicted rather than
waited for.

Two mismatches the harness reports on these runs -- `shooting` against the
authority, and `damage-taken` between clients -- are both in the rig's own
history from before any of this, `damage-taken` with the identical numbers
(17 against 7). So is the authority's `Resolved` count reading lower than the
clients' `Replayed`, which reproduces exactly with `-nohitprediction`. None of
the three is this feature; all three are worth someone's time on their own.

### Verified 2026-09-09 against Japan (`13.78.14.98:27890`, simulating, 267-274 ms)

The held prediction, predicted death and the drain credit were written and
measured against a real line rather than a loopback with lag injected into it.
Two scripted clients, 70 s, `MP3 PROVING GROUND`:

| Client | Result |
|---|---|
| Sylux (Shock Coil) | **28 predicted, 28 confirmed (100%), 0 denied**, 103 unpredicted, **23 health drained ahead** of the authority |
| Samus | **3 predicted, 3 confirmed (100%)**, 0 denied, 14 unpredicted |
| three clients, 45 s, over a rotation | **1 kill predicted, 0 undone**; Sylux 3/3 and 3 health drained ahead |
| two clients, 60 s, with self-damage predicted | Samus **3 self-hits predicted, 2 confirmed** (its own missile splash); Sylux 8/8 and 7 health drained ahead; both `PASS` |

**0 denied at 270 ms, against the 86-98% the loopback instrument used to
read.** That is the batched `Confirm` rather than the hold: retiring one
prediction per snapshot left the rest to time out looking like misses, and
`landed` is how many actually landed. Do not read the drop in `denied` as a
change in how often a prediction is *right*.

`unpredicted` is high on Sylux and that is the Shock Coil, not a fault. It is a
`Continuous` weapon whose damage is divided by 32 and dithered off
`scene.FrameCount`, so the frame parity that produces a damaging hit is not the
same frame parity on two machines: the authority landed 131 hits where this
client resolved 28. Nothing is lost by it -- an unpredicted hit is shown when
it arrives, which is what every hit used to do.

The one `RESULT: FAIL` on these runs is `their form stayed wrong for 69 frames
in a row`, which is the open question in `.claude/KNOWN-GAPS.md` about form
reconciliation with a simulating server, measured there at 78 frames. Not this.

**The box is the instrument's limit, not the server.** Three clients on this
WSL machine run the tour at about 9 fps, so snapshots arrive faster than they
are consumed (16-20k received against 2700-4200 frames) and every number above
is a small sample. Two clients is the honest maximum here.

## Traps

- **`HealthFor` must never return zero.** A health assignment is not a death:
  it skips `TakeDamage` and everything the death path does, leaving a player
  who is neither alive nor dead and who no snapshot will respawn (the
  authority thinks they are up). The hold floors at 1 and the dying is left to
  `TakeDamage`, which is where a predicted kill goes anyway.
- **`Confirm` has to run before `Replay`'s "already down here" return.** That
  return exists because a victim already dead on this machine has nothing to
  replay -- but with kills predicted, a victim this machine killed *is* dead
  here, so the authority's confirmation of that very kill would hit the return
  and never retire the prediction. It would count as denied, and the corpse
  would be held down for the whole hold window rather than until the answer
  arrived.
- **A lethal snapshot still replays even when the hit was predicted.**
  Reaching that line with `state.Health == 0` means this machine's prediction
  did not kill them -- it was clamped, or the killing blow was somebody
  else's -- so returning early would leave a player alive here and dead
  everywhere else. A kill this machine did predict never reaches the line; it
  is the "already down" return.
- **The kill streak is the one part of the death path the snapshot does not
  correct.** `Points`, `Kills` and `Deaths` are assigned from every snapshot;
  `GameState.KillStreak` is not on the wire, so a mispredicted kill leaves this
  machine's own streak one too high and can ring the "5 in a row" line early.
  It is local, cosmetic and rare -- `DeathsUndone` is the number that bounds
  it -- but it is the one thing here that does not put itself right.
- **`NetDamage.Note` must stay silent on a predicting machine.** A prediction
  is not a resolution. Letting it through would put a damage sequence and a
  `Resolved` count on a machine that decides nothing -- and the whole damage
  pipeline measurement is the comparison between the one machine that resolves
  and the ones that replay. `NetHitPrediction.Predicting` is the guard, and it
  reproduces exactly what `Suppress` returning true used to guarantee.
- **Consume the confirmation, do not skip the kill.** `Confirm` is called for
  every hit the authority credits this machine with, including the lethal one,
  so the pending entry is retired either way -- but the replay is only skipped
  when the hit is *not* lethal. Returning early on a lethal confirmation would
  leave a player alive on the shooter's screen and dead on everyone else's.
- **The clamp has to be at `Note`, not at `Suppress`.** `TakeDamage` applies
  the beam's effectiveness multiplier, the damage level and the halfturret
  split *after* the suppression check, so a clamp at the top of the function
  is a clamp on a number that is not the damage yet.
- **A frozen puppet stops taking its owner's positions.** A mispredicted
  freeze therefore stalls a puppet locally until the next snapshot's
  `ModSetFrozen(false)` thaws it -- which is one frame, and is why the freeze
  is left to predict along with everything else rather than special-cased.
- **`Tick` lives in the simulation step.** Both the pending ages and the
  mark's countdown are measured in frames; a picture with no step behind it
  must not advance either. See `render/FRAME-PACING.md`.
- **Loss costs nothing.** A confirmation is not a packet of its own -- it is
  the damage sequence in whatever snapshot next arrives -- so a lost snapshot
  delays a confirmation rather than destroying it, and `landed` picks up both
  hits when the next one lands. Only a sequence jump big enough to trip
  `MaxCatchUp` skips a confirmation outright, and that path shows nothing
  either way, so there is no double feedback to be had. Measured above.
- **No protocol change.** Nothing new is sent, nothing existing moved, and
  `NetConfig.ProtocolVersion` stays at 6. A predicting client and a server
  built before this interoperate; the client simply predicts against whatever
  the server tells it.
