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

## Three rules

1. **A prediction never kills.** Damage is clamped at the last moment before
   `TakeDamage` decides, so the victim is left standing on one point of
   health. The death path awards the kill, raises the banner, starts the
   respawn timer and feeds `EndIfPointGoalReached` -- a kill that turned out
   not to have happened would have to be unpicked from all of it. The killing
   shot still *feels* instant, because the mark and the flinch are shown; only
   the dying waits for the authority, exactly as it did before. `LethalHeld`
   counts them.
2. **A prediction never scores.** It cannot: nothing but the death path awards
   a point, and the death path is the one thing rule 1 keeps it out of. This
   is why there is no `SaveScores`/`RestoreScores` here as there is in
   `NetDamage.Replay`.
3. **A prediction is only your own shot on somebody else.** Incoming damage is
   never predicted. Whether *you* were hit is a question about a shot fired on
   another machine and aimed at a copy of you that machine is holding; this
   one has no better answer to it than the authority's, it has a worse one.

## The mechanism

| Piece | Where |
|---|---|
| `NetDamage.Suppress(victim, source)` | the top of `TakeDamage`. Asks `NetHitPrediction.Predicts` before throwing the hit away, so a "no" costs exactly what it always cost |
| `NetHitPrediction.NoteHit(victim, attacker, ref damage)` | beside the existing `NetDamage.Note`, which is the last point at which the damage is final and the death has not been decided. Clamps, records the pending prediction, and raises the mark |
| `NetHitPrediction.Confirm(slot)` | inside `NetDamage.Replay`: a hit the authority credits this machine's player with, on a victim it already predicted, is consumed and **not** shown a second time |
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
- **kills left to the authority** -- rule 1, counted.

`-nohitprediction` is the control, and `-nohitmarker` turns off only the mark.
Both are on by default, as `-nounlagged` is off by default.

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

## Traps

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
