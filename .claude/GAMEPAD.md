# Gamepads

A pad drives the game on the desktop and on Android, over USB or Bluetooth.
Bluetooth needs nothing of its own on either platform: by the time events
reach an application the operating system has already decided which driver
produced them, and a paired pad is an input device like any other.

| Path | What |
|---|---|
| `Mods/Input/GamepadState.cs` | the neutral snapshot both platforms fill in |
| `Mods/Input/PadBindings.cs` | which button each action is on, and the player's changes to it |
| `Mods/Input/GamepadInput.cs` | dead zones, the response curve, and what every button means |
| `Mods/Input/GamepadDesktop.cs` | the desktop reader, polling GLFW |
| `Mods/Input/GamepadMappings.cs` | extra SDL mappings, for pads GLFW has never heard of |
| `Mods/Input/GamepadLayout.cs` | the guess used when there is no mapping at all |
| `Mods/Input/GamepadProbe.cs` | `-gamepad`, the diagnostic |
| `MphRead.Android/GamepadBridge.cs` | the Android reader, accumulating key and motion events |
| `Mods/Launcher/Gui/PadRow.cs` | one rebindable button in the settings screen |
| `Mods/InputSettings.cs` | the three settings and the bindings, saved to `controls.txt` |

## Pads nothing has a mapping for

GLFW carries a snapshot of SDL's controller database, frozen at whichever GLFW
the OpenTK redist ships. Everything released since then, and everything too
obscure to have been in it, answers `glfwJoystickIsGamepad` with false -- and a
pad that answers false used to be skipped in silence, which from inside a match
is indistinguishable from nothing being plugged in. That is most of what "my
controller does nothing" means, and it was reported as exactly that.

Three things now stand between a pad and that conclusion.

1. **Mapping files.** `GamepadMappings` hands GLFW every mapping it can find,
   once, before the first scan: `gamecontrollerdb.txt` beside the executable,
   the same file in the settings directory (which wins, and is the copy that
   survives reinstalling), and `SDL_GAMECONTROLLERCONFIG` -- SDL's own
   environment variable, because somebody who has already made their pad work
   in another game most likely did it there. A missing file is the normal case
   and says nothing; a file that is read says how many lines it had.
2. **The raw fallback.** Failing that, `GamepadDesktop` reads the pad through
   GLFW's raw joystick API on `GamepadLayout`'s guess: two shapes, chosen by
   counting axes. Six or more axes is the Xbox shape (sticks on 0/1 and 3/4,
   analogue triggers on 2 and 5, buttons A B X Y LB RB Back Start Guide L3 R3);
   four or five is the flat "USB gamepad" shape, where the shoulders and
   triggers are four plain buttons. The d-pad is not guessed at -- GLFW reports
   hats separately and every pad that has one reports it the same way -- and
   the trigger axes are calibrated against the lowest value seen, since some
   pads rest a trigger at -1 and some at 0.
3. **Saying so.** A pad read this way is named `<pad> (unmapped)` in the
   settings screen, the console says it is being read raw, and `-gamepad`
   prints the axis and button counts, what each button reached, and a mapping
   line built from the guess -- GUID and all -- ready to be corrected and
   dropped into `gamecontrollerdb.txt`.

A guess is allowed here because of what it is weighed against: every action but
the two sticks is rebindable in Settings -> Controls, so a player whose face
buttons come out shuffled can put them right in a minute, while a player whose
pad is ignored has nothing to put right. Mapped pads are still preferred --
all sixteen slots are offered to `glfwGetGamepadState` before any of them is
read raw.

## The layout

Xbox-shaped, because that is what both platforms hand over: GLFW remaps every
pad it recognises through SDL's controller database, and Android's own
`KEYCODE_BUTTON_*` constants are the same fifteen in the same places. A
DualShock's cross arrives as A from both.

This is the **default** layout. Every row of it below the two sticks is a
`PadAction` the player can move to another button on the Controls page; the
table is `PadBindings`, which starts as exactly this.

| Pad | Action |
|---|---|
| Left stick | move (and roll, in the ball) |
| Right stick | aim |
| A | jump / boost |
| B | morph |
| X | scan |
| Y | scan visor |
| Right trigger | shoot, and the alt form's attack |
| Left trigger | zoom |
| LB / d-pad ← | previous weapon |
| RB / d-pad → | next weapon |
| D-pad ↑ | missile |
| D-pad ↓ | power beam |
| Back | scoreboard |
| Start | pause menu |

Two actions drive two binds each, and have one row apiece rather than two:
FIRE is both the gun and the alt form's attack (the DS had one attack button
and the game's own defaults still bind both to it), and JUMP is also the
ball's boost. Offering four rows for two buttons would let somebody build a
pad on which the ball cannot boost, with nothing on screen saying why.

**No weapon wheel.** `PlayerHud`'s weapon select reads the *absolute* pointer
position, because on the DS it was a touch screen and the slot under the
stylus is the one that gets picked. A stick has no position, so driving it
would mean warping the mouse cursor about to fake one — which fights whoever
also has a hand on the mouse, and is a surprising thing for a controller to do
to a desktop. The bumpers and the d-pad reach every weapon without it.

## How it reaches the game

The same trick the Android touch controls use, from the other end: rather than
fork `ProcessAllInput`, `GamepadInput.Apply` waits until it has run and then
**ors** the pad's contribution onto the same `Keybind` flags the keyboard just
filled in. Everything downstream — firing, morphing, what goes on the wire as
an intent — reads those flags and cannot tell where they came from, so a pad
and a keyboard work at once, rebinding still applies, and not one call site in
upstream's input code changed.

Two things cannot go through a keybind:

- **Aim**, because a stick is analogue and a key is not. It goes in at
  `ApplyModAim`, which is the one hook upstream already offers inside the
  player's own input step, in the same units the mouse's arrives in (degrees
  of turn per frame) and at the same point in the frame. Aim applied a few
  lines earlier or later than the mouse's would feel different from the mouse
  for reasons nobody could name.
- **The menu button**, because the pause menu is not a thing the *player*
  does: it is a window the host platform opens. `TakeMenuPress` is consumed by
  whatever owns a window — `RenderWindow.OnRenderFrame` on the desktop, the
  render loop on Android. It is rebindable like the rest; Start is only where
  it starts.

And one thing the or could not carry, which cost two bugs before anybody
worked out they were the same one. **`Input.HasInput`** — the engine's answer
to "has this person touched anything lately" — is written in the pass that
turns the keyboard into binds, and the pad arrives *after* that pass has run.
So on a controller the player looked idle every frame of a match they were
playing:

- `UpdateGunAnimation` lowers the gun after `GunIdleTime` and raises it again
  only on a frame where `_timeSinceInput` is exactly zero. That frame never
  came. The gun sank off the bottom of the screen and stayed there — and both
  `CanShoot` and `TryEquipWeapon` refuse while `GunAnimation.UpDown` is
  playing, so the player could neither fire nor change weapon for the rest of
  the life. Reported from Android as "with Trace, sniping, the weapon falls
  away and I cannot shoot".
- the idle sway starts drifting `_facingVector` on its own after
  `SwayStartTime`, which is worst for exactly the player who is deliberately
  holding still.

`GamepadInput.Apply` now calls `PlayerEntity.ModNotePadInput` whenever the pad
is `InUse`, and `ApplyGamepadAim` does the same for the stick. The touch
controls never needed it: they press the keys the player has bound into a
`KeyboardState` of their own, so they arrive *inside* the pass that sets the
flag.

**Alt form** was the other half. `ApplyModAim` was called from `ProcessBiped`
and nowhere else, so a pad could walk, boost and attack in alt form but not
look — Trace, Sylux and Weavel aim in alt form, and only the mouse could do
it, from `ProcessAlt`'s own branch. It is now called from both, in the same
place as the mouse's, which also means a remote player's relayed aim reaches
their puppet while they are morphed instead of waiting for the next snapshot.
The ball hunters (Samus, Kanden, Spire, Noxus) are unchanged: nothing aims a
morph ball, and the mouse does not turn one either.

## Both at once, on a phone

Using the pad hides the touch layout (`TouchControls.NotePadActivity`) and
**does nothing else**: the overlay view still receives every touch, so a
finger on the glass does what it landed on and brings the layout back in the
same gesture. Nothing held is cancelled when the layout goes away either.

That is a change from how it was first built, and the reason is in the table
above: there is no weapon wheel on a pad, because the wheel reads an absolute
pointer position. Somebody playing with a pad who wants a different weapon has
to touch the screen -- and used to pay a wasted press for it every time,
because the reviving touch was swallowed. See ANDROID-PORT.md.

## Feel

- **The dead zone is radial, not per axis.** Per-axis is the common mistake
  and it is visible: the neutral area comes out a square, so a stick pushed
  diagonally starts to move before one pushed straight, and slow circles turn
  into slow octagons. Past the edge the magnitude is rescaled, so the first
  movement past the dead zone is the smallest movement rather than a jump to
  the dead zone's own size.
- **The look curve is squared, keeping the sign.** The useful half of a
  stick's travel is the first half, where a shooter wants to make small
  corrections; linear, one stick has to do both the flick and the nudge and is
  bad at the nudge.
- **3.5 degrees per frame at full deflection** — 210 a second, which is where
  console shooters have sat since they settled the question. The sensitivity
  setting runs 0.25x to 3x around it.
- **Walking is a threshold, not a curve** (half deflection), because the walk
  keys are on or off. Larger than the aim dead zone: a thumb resting on the
  stick should not walk you off a ledge.

## Settings

`controls.txt`, and the launcher's Controls page has a Gamepad section and a
Gamepad buttons section:

```
gamepad_deadzone=0.2
gamepad_look=1
gamepad_invert_y=false
pad_Shoot=RightTrigger
pad_NextWeapon=RightBumper, DpadRight
...
```

Vertical invert is its own setting rather than sharing the mouse's, because a
great many people invert one and not the other.

**There is no `gamepad=true` any more.** The toggle it saved — "Use a
connected gamepad" — could only ever matter to somebody with a pad attached
who did not want it, and what it cost was a row that reads like it might be
the reason the pad is not working, in the one screen somebody whose pad is not
working will go looking. A pad that is merely connected now changes nothing on
its own, and on Android the touch controls step aside for one by themselves.
The key is still *read* and skipped, so an older `controls.txt` still loads
the rest of itself.

### Rebinding, and why it is not a `Keybind`

`ButtonType` and `Keybind` are upstream's, in `Entities/Players/PlayerInput.cs`.
Adding a fifth `ButtonType` for a pad button would put this project's change in
an upstream file, which is the one thing the `Mods/` rule exists to avoid. It
also buys nothing: `GamepadInput.Apply` already *adds* the pad's contribution
to the binds after `ProcessAllInput` has filled them in, so the two mappings
never have to agree about anything. `PadBindings` is therefore its own table,
`PadAction` → `GamepadButtons`, saved under its own `pad_` keys.

A binding is a **flag set**, not one button, because two of the defaults have
two buttons in them. The settings row replaces the whole set with the single
button that was pressed; the reset puts the pairs back.

`PadRow` does not listen for a toolkit event the way `KeyRow` does — a pad is
already reduced to `GamepadState` by the time anything could see it, so the
row watches that state instead and cannot disagree with the game about what
was pressed. Two things had to be arranged for it to see anything at all
outside a match:

- **Android**: `MainActivity.DispatchKeyEvent` and `DispatchGenericMotionEvent`
  now feed `GamepadBridge`, so a pad press lands in the state whatever view
  has the focus. It used to be `GameView`'s alone, which does not exist while
  the launcher is up.
- **Desktop**: `GamepadDesktop.PollForMenu` initialises GLFW if nothing has
  yet (OpenTK does it when it creates the game's window, which has not
  happened on a first run) and pumps an event poll, since GLFW only refreshes
  joystick state inside one. Called only while a row is listening.

## Checking it

```
FruityPrime -gamepad [-seconds N]
```

Prints the pad's name, every axis as it moves, and **which game action each
button reaches**. It exists because "my controller does nothing" has four
causes that look identical from inside a match — the pad is not connected,
GLFW has no mapping for it, the dead zone is eating the sticks, or the mapping
is wrong — and this separates them. In particular it distinguishes a pad that
is *present* from one that is *mapped*: an unmapped pad is ignored by the game
entirely and looks exactly like nothing being plugged in.

### Testing without a pad

A virtual one, on Linux, through `uinput`. The vendor and product ids are
Microsoft's real ones, which is what makes GLFW's controller database
recognise it and hand the game a mapped gamepad rather than a nameless
joystick:

```python
from evdev import UInput, AbsInfo, ecodes as e
ui = UInput(caps, name="Microsoft X-Box 360 pad", vendor=0x045e,
            product=0x028e, version=0x110, bustype=e.BUS_USB)
ui.write(e.EV_ABS, e.ABS_Y, -32768); ui.syn()   # left stick forward
```

Needs `python3-evdev` and root for `/dev/uinput`, and the device it creates is
root-owned — `chmod a+r /dev/input/event*` afterwards or the game cannot read
it. Under WSL there is no `/dev/input` at all until the first such device is
created.

Measured this way on 2026-09-03, against a real match started from the text
launcher: every button reached the action listed above, full right-stick
deflection produced exactly the 3.5 degrees a frame, a stick at 0.12 (inside
the 0.20 dead zone) produced nothing while 0.37 produced a small correct turn,
and the player walked about 40 units across MP3 Proving Ground on the left
stick alone. Unplugging mid-run was handled by the rescan without a stall.

## Known gaps

- **Android is unmeasured.** The bridge compiles and the shared half of it is
  the half that was tested above — including the automatic touch handover in
  `TouchControls.NotePadActivity`, which no pad and no thumb have exercised
  together — but no pad has been held against the APK:
  the emulator here would not keep its package service up long enough to
  install one, and there is no phone on this machine.
- **The launcher itself is not navigable with a pad** — only a match is. The
  front screen is Avalonia and would need its own focus handling.
- **No rumble.**
- **The sticks are not rebindable**, only the buttons. Nobody has asked to
  swap move and aim, and the two are not interchangeable anyway — one is a
  threshold and the other a curve.
- **`PadRow`'s capture is unmeasured on the desktop launcher.** The GLFW
  lazy-init path has not been run against a real pad from a cold start with no
  game window; it is guarded and falls back to showing the binding it already
  has, but "press a button on the pad" doing nothing there is the first thing
  to check.
- **One pad.** The first that answers wins, since nothing here has a second
  player to give a second pad to.
- **The raw fallback is unmeasured against a real unmapped pad.** It is
  written from the two layouts SDL's own database uses for those shapes, and
  the machine this was built on has no `/dev/uinput` to fake a third with. The
  mapping-file path is the one to reach for first when a pad comes out wrong,
  and `-gamepad` prints the line to put in it.
