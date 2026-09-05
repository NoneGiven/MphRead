# Frame pacing: 60 Hz of game, as many pictures as the screen wants

The simulation runs at exactly 60 Hz. The picture runs at the display's rate.
Nothing else changed, and that separation is the whole feature.

## Why the simulation cannot simply be sped up

Every timer in this engine is counted in frames. The DS ran its logic at 30 Hz;
upstream reached 60 by doubling each interval and halving each increment, by
hand, at every site:

```
_cooldownTimer = (ushort)(_data.CooldownTime * 2);          // JumpPadEntity
_timeSinceJumpPad > 7 * 2                                    // PlayerInput
_viewTiltAngleH += Values.ViewTiltIncrement * sign / 2;      // PlayerInput
_frozenTimer = _timeSinceFrozen > 60 * 2                     // PlayerEntityNetAim
```

`grep -rc "todo: FPS stuff" --include=*.cs src/` counts **806** of them. Running
the simulation at 120 would mean redoing all 806 as `* 4` / `/ 4`, and would
also move things that are not in this repository's gift:

- an intent is sent **per simulation frame**, so the wire rate would change
- `NetConfig.ProtocolVersion` would have to move, orphaning every older client
- a demo is a **count of frames** (`DemoFile`), so old recordings would replay
  at the wrong speed
- the DS behaviour this engine reproduces is defined at its own tick rate

So the simulation is not asked to move. Only the drawing is.

## The split

`Scene.OnUpdateFrame()` used to be one call doing both, and `RenderWindow` ran
it at `UpdateFrequency = 60`. It is now three methods:

| Method | Rate | Contains |
|---|---|---|
| `Scene.OnSimulationFrame()` | exactly 60 Hz | input, `NetSession.Update`, `NetHooks`, `UpdateScene`, sound, the clock, `_frameCount` |
| `Scene.OnDrawFrame()` | the display's rate | GL setup, render-item rebuild, camera, projection, `GetDrawItems` |
| `Scene.OnUpdateFrame()` | — | both, in that order |

`OnUpdateFrame` is kept **because the harness calls it**. `NetCheckClient`,
`MapAudit`, `WeaponDps` and `ThumbnailCapture` each drive one call per frame of
their own loop, so their timing is untouched by any of this: same steps, same
order, same packets on the same frame numbers.

`RenderWindow.OnRenderFrame` is where the two rates meet, on a fixed-step
accumulator in `Mods/Render/FrameTiming.cs`:

```
steps = FrameTiming.Advance(args.Time);   // 0, 1 or 2+ per drawn frame
for (i = 0; i < steps; i++) Scene.OnSimulationFrame();
Scene.OnDrawFrame();
```

### A side effect worth having

The game's speed no longer depends on whether the machine can keep up. Update
and render used to be one call, so a box managing 40 fps played the game in
slow motion. The accumulator pays what it owes: `-frametimingcheck` measures
60.000 Hz of simulation at a 40 Hz draw rate.

## Interpolation

A 144 Hz picture of a 60 Hz simulation is not smoother than the 60 Hz one --
it is the same 60 distinct positions a second, some shown twice. That reads as
judder. `FrameTiming.Alpha` says how far past the last simulated step the frame
falls, and two things blend across it.

**Entities.** `EntityBase` keeps the last two transforms. During the draw pass
the interpolated one is *swapped into `_transform` and `_position` for the
length of the `GetDrawInfo` call* and put back afterwards. That swap is why not
one of the ten `GetModelTransform` overrides had to change, and why attached
effects and shadows move with the model.

**The camera**, which is the half that is actually felt, since it is the whole
screen. `CameraInfo` keeps position, target, up and fov, and `ModGetDrawView`
rebuilds the matrix with `Matrix4.LookAt` from the blended pair. Blending two
LookAt matrices instead would blend the basis vectors, which is not a rotation
and drifts on fast turns -- and a fast turn is what a mouse does.

### When a blend is declined

Interpolation is wrong whenever the two states are not two points on one path.
`ModBeginInterpolatedDraw` returns false, and the entity is drawn where it
actually is, for:

- fewer than two captured steps (it just spawned)
- `alpha >= 1` -- which is every frame when the picture runs at 60
- the transform moved after the step was captured
- **a jump over 24 units in one step** (1440 units a second): a teleport, a
  respawn, or a puppet being put where its owner says it is
- **a step whose distance is more than four times the last one, plus a unit**:
  a teleport too short to trip the ceiling. The constant keeps anything
  starting from a standstill -- a jump, a jump pad, a shot leaving the barrel
  -- out of it

The matrix blend is component-wise rather than a decomposition and a slerp. The
blend of two rotation matrices is not itself one; it is a rotation scaled by
cos(half the angle between them), and half of one 60 Hz step is a fraction of a
degree here. A pickup spinning a brisk six degrees a step comes out 0.14% small.

## Timers that live in the draw pass

Two were found by audit and both are handled by counting steps owed rather than
by moving the code:

- **`UpdateFade`** decrements `_fadeDelay` and calls `EndFade`, which ends
  cutscenes and changes rooms. Left as one per drawn frame it would expire two
  and a half times early on a 144 Hz screen. It now consumes
  `_pendingFadeSteps`.
- **`ProcessEffects`** advances particles, and is called from inside
  `GetDrawItems` where its ordering against the entity pass matters. It now
  runs `_pendingEffectSteps` times, in the same place.
- **The pause map's animations** (`GetPauseMapRenderItems`) advance from
  `Scene.FrameTime` in the draw call; the branch hands them the steps actually
  taken.

`grep -rn "\.FrameTime"` over the rest of the tree finds 46 uses and none of
them in a draw-path method.

**Frame advance** (the debug single-step) is forced back to one step per
picture: the request to advance is consumed *after* the frame is drawn
(`AfterRenderFrame`), so a picture drawn with no step behind it would eat it
before the simulation ever saw it.

## Settings

| Where | What |
|---|---|
| Launcher → Settings → Performance | **FPS limit**, a slider directly under Render scale over the stops Display (VSync) / 30 / 60 / 75 / 90 / 100 / 120 / 144 / 165 / 180 / 200 / 240 / Unlimited, and **Motion interpolation** under it |
| `settings.json` | `FrameRateCap` (`display` or a number), `Interpolation` |
| `-fpscap N` / `-fpscap display` | for the paths that never open a launcher |
| `-interpolation on\|off`, `-nointerpolation` | same |

**Display (VSync) is the default**, and is the only tear-free setting: an
explicit number turns VSync off, because asking for 120 on a 144 Hz screen with
VSync on gets you 72. OpenTK 4.9 no longer separates its update and render
ticks -- `RenderFrequency` is deprecated and the two callbacks fire together --
so `UpdateFrequency` on the window is the *frame* rate, and 0 means "as fast as
it will go".

The on-screen FPS counter reports the **picture**, since `CountFrame` runs in
`Scene.OnRenderFrame`. The simulation rate is not visible to a player at all,
which is why it goes to the debug log.

## How it is tested

Neither half needs a 144 Hz monitor.

**`FruityPrime -frametimingcheck`** runs the accumulator alone against frame
times chosen rather than measured. The half that can silently be wrong is
arithmetic: a game running at 60.4 Hz loses a second every two and a half
minutes, is invisible in a screenshot, and is fatal to a match clock.

**`FruityPrime -maptest "ROOM" -players 8 -drawrate N`** draws each simulation
step N times, which is what a 144 Hz screen does to a 60 Hz game. It steps
alpha 1/N, 2/N .. 1 across the draws rather than taking a wall clock, so a run
is reproducible and visits the whole range. It asserts:

- the simulation's frame counter did not move during a draw (`draws advancing
  the game: 0` -- any other number is a `MAPFAIL`)
- interpolation actually engaged, because "the setting is on" is not something
  a run can check by looking at it: every blend can decline, and a bug that
  made them all decline would leave the old picture with nothing saying so

The expected blend ratio is `(N-1)/N`, since alpha reaches exactly 1 on the
last draw of each step and 1 declines. Measured on TEST ARENA, 8 players,
12 seconds:

| Draw rate | MAPTEST line | Entity draws | Blended | Ratio |
|---|---|---|---|---|
| 1 | reference | 43119 | 0 | — |
| 3 | **identical** | 129357 | 86138 | 0.6659 (2/3) |
| 4 | **identical** | 172476 | 129207 | 0.7491 (3/4) |

"Identical" is the whole point and is meant literally: same 1688 frames, same
`spawned 8/8`, same `moved 7/8 (furthest 51 units)`, same `deaths 5`, same
`freeze ok burn ok disrupt ok`, same lit percentages to a decimal. Drawing the
world three or four times as often changed nothing about what the world did.

### The forced-alpha lifetime trap

`ForcedAlpha` is set before the first simulation step and **left set** for the
whole run. `CaptureDrawState` runs at the *end* of a simulation step and skips
its work when nothing is going to blend against what it would remember -- so
clearing the override between frames makes every capture skip, and nothing ever
interpolates, while the setting still reads "on". That is exactly the failure
the `interpolation never engaged` assertion exists to catch, and it caught it.

## What is not interpolated

- **`PlatformEntity`**, which overrides `GetModelTransform` to build its matrix
  from its own `_curRotation` quaternion rather than from `_transform`. Moving
  platforms therefore still step at 60 Hz. They move slowly and it is not
  obvious; fixing it means giving that class its own capture.
- **Animations** (model, texture, material) and **particles**, which are frame
  indexed and advance once per simulation step.
- **The HUD**, whose state is updated once per step and drawn every frame.
- **The fade**, whose percentage derives from `_globalElapsedTime`.

None of these are wrong, they are simply at 60. The camera and the entity
transforms are where the eye actually looks.
