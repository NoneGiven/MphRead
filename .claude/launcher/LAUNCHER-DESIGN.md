# Launcher — design and UI

This document describes the UI components, the logo handling, and the shape of
the windows.

One toolkit

The launcher is Avalonia everywhere. It used to be two: a WinForms front screen
for Windows and an Avalonia one for everything else, over shared logic. That
split cost a second implementation of every screen, and the two halves were not
equal -- the settings window, the map grid and the pause menu existed only in
WinForms, so a Linux player was told to go and use the console menu instead.
Everything is now in `Mods/Launcher/Gui/`:

| File | What |
|---|---|
| `GuiLauncher.cs` | setup, the launcher-then-match loop, and `Pump` |
| `HomeWindow.cs` | the front screen and its cards |
| `SettingsWindow.cs` | the rail of sections and every setting |
| `MapPickerWindow.cs` | every map at once, as pictures |
| `PauseMenuWindow.cs` | what Escape shows during a match |
| `SplashView.cs`, `MenuEntry.cs`, `Rows.cs`, `SliderRow.cs`, `KeyRow.cs`, `ProgressRow.cs`, `UpdateBadge.cs`, `TrackedText.cs`, `GuiTheme.cs` | the painted controls and the palette |

Painting and controls

- The launcher draws its own controls for a consistent dark theme; only the text
  boxes and scroll bars are stock, under Fluent dark.
- `GuiTheme` holds the palette and the display face. Inter is embedded in the
  build rather than looked up on the system: there is no font list every
  platform has, and a launcher that renders in whatever fontconfig happens to
  pick looks different on every distribution.

Windows have frames

The WinForms screen was borderless and dragged by its picture. Every window here
is an ordinary decorated one: an undecorated window that a given window manager
will not let you move is a trap, and there are many window managers.

Logo and assets

One source image, chroma-keyed and cropped into four files under
`src/MphRead/Assets/`. All four allow-listed in `tools/asset-guard-allow.txt`
(PNG and JPEG are otherwise banned extensions).

| File | What | Used by |
|---|---|---|
| `fruity-prime-logo.png` | the wordmark, cherry and text together | the game-files card, the Android screen and the README |
| `fruity-prime-mark.png` | the cherry alone | the window icon |
| `fruity-prime.ico`, `fruity-prime-server.ico` | ICO frames for Windows | `ApplicationIcon` |

Notes on ICOs: 256×256 is the ICO format's ceiling; the source crop carries
detail up to ~460 px so 256 is a downsample.

Threading, and why there is only one thread

The toolkit is set up once per process **on the game's own thread**, and both
the launcher and the pause menu are windows on it:

- Avalonia allows one application per process, so a launcher that stood one up
  per visit worked exactly once and fell back to the text screen on the way back
  from the first match.
- macOS accepts windows only on the main thread, which rules out the private UI
  thread the WinForms launcher used.
- The pause menu needs the toolkit *during* a match, on the thread the render
  loop runs on.

A visit to the launcher is `Dispatcher.UIThread.PushFrame`, ended by the
window's `Closed` event. `GuiLauncher.Pump` is the other half: a nested frame
that runs until a background-priority job it posted comes back, which processes
everything pending and returns. `PauseMenu.Poll` calls it once a frame while the
menu is up -- which is why the match keeps drawing behind it -- and `Ask` calls
it once after the launcher closes, because on X11 the window's destroy request
would otherwise sit unflushed in the connection's buffer for the whole match and
leave a launcher painted over the game.

Menu entries

- **No descriptions under the titles, anywhere.** An entry called "Join" did
  not need a line saying it joins, and in the pause menu the second saying is
  what made a seven-line menu tall enough to be cut off. The only subtitles
  left are the ones reporting something the player could not otherwise know --
  missing game files on "Host", a demo that would not open, the map-preview
  progress -- and those are set when they happen, so `MenuEntry` takes its
  height from the subtitle (42 bare, 54 with one) in `OnPropertyChanged`
  rather than deciding it once in the constructor.

Pause menu

- `Escape` in a match opens it on every platform now (`Mods/PauseMenu.cs` +
  `Gui/PauseMenuWindow.cs`): Resume, Fullscreen/Windowed, Settings, Spectate or
  Rejoin, Record demo, Leave match, Quit.
- **It scales itself down rather than being cut off.** The panel's natural
  height is worked out from the entries put in it (each states its own
  `Height`), and `PauseMenuView.FitToHost` puts a `ScaleTransform` on a
  `LayoutTransformControl` around it, down to half size, when the window is
  shorter than that. The scroller under it is the last resort, not the plan:
  what a scrollbar produces here is a panel with its top and bottom cut off.
  A display at 150% is what made this ordinary -- the panel needs ~500
  device-independent pixels, which is 750 real ones, and the game window's
  floor was 600. That floor is now 1024x720 and the default window 1280x768.
- **Spectating starts on the free camera** (`Mods/SpectatorMode.cs`): "Spectate"
  puts you on the map with no HUD, a left click moves into the players and
  cycles through them, and Space toggles between the two -- `ToggleView`, not
  the camera directly, because the camera on its own would put you back behind
  your own hidden, frozen body with your own HUD on. **The HUD follows the
  camera, not the spectating**: on the free camera there is none -- it is
  CameraMode.Roam, and the scene only draws a HUD for a player's own camera --
  and following somebody shows theirs, which is what watching a recording back
  has always done and what makes watching a live match worth anything. It used
  to be hidden in both (`DrawHudObjects`/`DrawHudModels` returned early on
  `IsSpectating`), which left a spectator watching a hunter with no sign of
  what they were playing with.
  **The scoreboard is the exception on the free camera**: it is the match's
  and not a player's, so holding the show-score button draws it (and the
  filter that dims the scene) over the map. It cannot come from the usual
  place -- every keybind's state is filled in by the input pass spectating
  steps out of -- so `PlayerEntity.ProcessInput` reads that one bind off the
  keyboard snapshot against `InputSettings.Current` and leaves it in
  `SpectatorMode.ShowScoreboard`, which `Scene.ScoreboardOverFreeCamera` and
  `PlayerHud.ShowScoreboard` read.
  A spectator is also **drawn not at all** (`PlayerDraw.Draw` returns before
  `DrawShadow`, which is cast from the volume and so survived hiding the model)
  and is **not a target** (`PlayerAi`'s opponent and teammate searches ask
  `ModInPlay`, not `Health > 0`). The camera is
  the scene's, and the menu runs on the game's thread but has no scene to hand,
  so `Start`/`Rejoin` leave a `bool?` in `SpectatorMode` that
  `Scene.OnRenderFrame` acts on -- the same shape as this menu's own window
  work. Demo playback is the exception: it calls `Start(watchSomeone: true)`
  and goes straight to a player, having no view of its own to have just left.
- It talks to the game through volatile flags. GLFW window calls -- closing it,
  changing its border -- belong to the thread that created the window, so the
  menu asks and `PauseMenu.Poll` does it on the game's own thread.
- **It is the size of the game window and laid straight over it**, so it reads
  as the game's own pause screen rather than as a dialog the game opened. It was
  a 340x392 box centred on the game before, which is the shape of a settings
  prompt and not of pressing Escape in a game. `PauseMenuWindow.CoverGameWindow`
  takes the rectangle from `PauseMenu.WindowX/Y/Width/Height`.
- **It follows the game window, every frame.** Sampling that rectangle once at
  open time is not enough: drag the game and the menu stays where it was, which
  is the floating popup all over again. `PauseMenu.TakeWindowRect` re-reads the
  GLFW client rect from `Poll` -- already called once a frame while the menu is
  up -- and `PauseMenuWindow.FollowGameWindow` re-lays both the menu and the
  in-game settings when it changes. It remains a borderless window *over* the
  game rather than something drawn *inside* it, because Avalonia cannot render
  into the GL context; following is what makes that difference invisible.
- The rectangle is in client **pixels** (what GLFW reports, and what Avalonia's
  `Position` is in) while `Width`/`Height` are device-independent, so the
  display scaling has to come back out of them. Take it from
  `Screens.ScreenFromPoint(...).Scaling`, **not** `RenderScaling`: the latter
  is 1 until the window has been given a screen, so the constructor's call --
  the one that stops the window appearing mid-desktop for a frame -- would be
  wrong on any display not at 100%.
- **Nothing in it can be clipped.** The panel has a `MaxWidth` rather than a
  `Width` and sits in a `ScrollViewer`: the host is now the game window and the
  game window is whatever size it has been dragged to. Seven entries need about
  470 px of height, and below that the fixed-size version drew "Leave match"
  and "Quit" off the bottom -- a player who cannot get out of the match.
  `RenderWindow.MinimumSize` is 800x600 as well, so that case needs a window
  smaller than the game allows; `-uishot` renders a `pausemenu-small` at
  560x320 to keep the scroll path checked anyway.
- The entries are a 420-wide panel centred in it -- the same shape the Android
  overlay already used -- because a column of entries stretched across a 3840
  window is a menu you have to hunt across.
- The fill is a scrim (`GuiTheme.ScrimBrush`, `Ink` at alpha 196) with
  `TransparencyLevelHint` asking for `Transparent` and falling back to `None`.
  The match is still running behind it and that is the point; a compositor that
  will not give a window an alpha channel renders it opaque, which loses the
  view and nothing else. The panel carries a 1px `EdgeBrush` border, because
  panel and scrim are otherwise two shades of the same dark.
- **The settings window does the same when it is opened from here**
  (`SettingsWindow`, `view.InGame`): same rectangle, no decorations. A fixed
  980x660 dialog centred on its owner was two different rectangles in two
  different places for one screen -- and on a game window smaller than that, a
  dialog hanging off the edges of the thing it belongs to. From the front screen
  it is still an ordinary 980x660 dialog.
- Both are topmost so they clear a borderless-fullscreen game, and the menu
  steps out of the topmost band while the settings are up so the two are not
  left arguing about which is in front.
- **The game window itself only floats while it has the focus.**
  `WindowMode.SyncTopmost` reads `window.IsFocused` along with the fullscreen
  and pause-menu flags, once a frame. An always-on-top borderless window
  cannot be alt-tabbed away from in any way a person recognises -- the switch
  happens, the other window gets the keyboard, and the game stays drawn over
  it -- which was reported as a window that refuses to let go. Floating is
  only ever wanted for the one thing it was added for, covering the taskbar
  while the game is the window being used, and that is exactly the focused
  case.
- Its title is "<name> - paused", not the product name: the game window carries
  that, and two windows with one title is what an alt-tab list cannot tell apart.

Server browser (`Gui/ServerRow.cs`)

- **`FormattedText` wraps; `Trimming` alone does not stop it.** A
  `MaxTextWidth` with a breakable string breaks at the space rather than
  ellipsizing, so "MP3 PROVING GROUND" became two lines in a 30-pixel row and
  drew over the server beneath it. `MaxTextHeight = size * 1.6` is what forces
  one line and lets the trimming apply. A `PushClip` per cell goes with it,
  because trimming cannot help a single unbreakable word wider than its column
  -- which "PLAYERS" is at 51 pixels in a 43-pixel heading, and it simply
  overflowed into "PING".
- **Columns are pixels from the right, not fractions of the width.**
  `ServerRow.Columns` fixes ping (34), players (52) and mode (66) and gives
  what is left to the two columns that hold prose. Fractions of the launcher's
  400-pixel panel put the map column at 89 pixels, which is not a room name.
- The browse card widens the panel to 600 while it is up
  (`HomeView.PanelWidth`), because that card is a five-column table and the
  others are not. The picture beside it is decoration; the list is the thing
  being read.
- `-uishot` renders a `serverbrowser` screen at both 600 and 400 with sample
  rows, which is how the wrap and the overlap were seen and how the fix was
  checked. Neither needs a directory or a server to be up.

Implementation pitfalls

- `ScrollViewer.Padding` is not taken off the width its content is measured
  with. Every wrapped note in the settings window ran off the right edge of the
  window by exactly that much; the inset is the page's `Margin` instead.
- A `DockPanel` fills with its *last* child, so docking the Save/Cancel footer
  first put it first in the tab order -- the first Tab in the settings window
  was one press away from closing it. It is a two-row `Grid` now.
- Each window focuses its own first control when it opens. Without that a
  keyboard user tabs blindly into whatever the tree happens to offer first.
