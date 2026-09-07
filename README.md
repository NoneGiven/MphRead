<img src="src/MphRead/Assets/fruity-prime-intro.png" alt="Fruity Prime" width="100%">

**Metroid Prime Hunters on PC and Android.** Online matches for up to 8 players, widescreen, 60 FPS,
and a launcher that does the setting up for you.

A fork of [NoneGiven/MphRead](https://github.com/NoneGiven/MphRead).

> You bring your own Metroid Prime Hunters cartridge dump. No Nintendo game data ships here or is
> downloaded.

**[Download](https://github.com/liveteklol/Fruity-Prime/releases)** · [Support the project ☕](https://ko-fi.com/livetek)

## Features

- **Ultra Widescreen support**
- **High resolution**
- **Windows / Linux / Android port**
- **240+ FPS support**
- **Online without lags !** (no WFC support)
- **Up to 8 players**
- **Dedicated servers**
- **Demo recording**
- **Custom maps**
- **Bots**: 0 to 7 offline
- **All 12 modes**: Battle, Survival, Capture, Bounty, Defender, Nodes, Prime Hunter, and teams
- **Keyboard & mouse**, and **gamepads**
- **Pick your suit colour**, and never share one with the other Samus in the match
- **Change hunter between lives**, from the pause menu
- **Cel shading**
- **Modern HUD**
- **Story mode**
- **Auto Update check**
- **All roms are compatible**

## Support

If you enjoy it: **[ko-fi.com/livetek](https://ko-fi.com/livetek)** ☕

<img width="500" height="300" alt="Fruity Prime" src="https://github.com/user-attachments/assets/ec6a2871-2b67-4de0-8b1a-ac6740c8d388" />

## Getting started

1. **[Download](https://github.com/liveteklol/Fruity-Prime/releases)** the package for your system
   and unzip it.
2. Run it:
   - **Windows** — double-click `FruityPrime.exe`
   - **Linux** — `./FruityPrime -launcher`
   - **macOS** — `xattr -dr com.apple.quarantine .` once, then the same as Linux
3. Click **Game files** and pick your `.nds`. It unpacks itself, once, with a progress bar.
4. Play.

`Escape` opens the menu, `F11` is fullscreen. Your name, hunter, controls and HUD are in
**Settings**, in the launcher or from that menu.

## Playing with other people

| | |
|---|---|
| **Join** | **Join → Find a server**, pick one from the list |
| **Host** | **Host → Where: Online** — a public machine runs the match and you join it, so there is nothing to open on your router |
| **On your own** | **Host → Where: Local**, with up to 7 bots |

Everybody in a match needs the same version; the launcher checks for a new one and says so.

Want a machine of your own that is always up? [`SERVER.md`](SERVER.md).

## Custom maps

A map is one file: `something.fpmap`. Put it in the `maps` folder beside the game and it is in the
map list next time you open the launcher, picture and all. **de_dust2** comes with it.

## Not done yet

- **Adventure co-op.** The launcher's toggle is a placeholder; the story is one player.

## Building

```bash
dotnet publish src/MphRead/MphRead.csproj -c Release \
  -r win-x64|linux-x64|osx-x64|osx-arm64 --self-contained true -p:PublishSingleFile=true
```

Needs [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) or later.
`-p:MphReadServer=true` builds the dedicated server; Android is
`dotnet build src/MphRead.Android/MphRead.Android.csproj` with the `android` workload. Every command
line option, and the test harness, are in [`CLAUDE.md`](CLAUDE.md).

## Credits

Fruity Prime is Livetek's fork of [MphRead](https://github.com/NoneGiven/MphRead) by **NoneGiven** —
the model viewer, the renderer, the format parsers and the recreation of the game itself are theirs.
That work is in turn built on **dsgraph**, [Chemical](https://gitlab.com/ch-mcl/metroid-prime-hunters-file-document),
[McKay42](https://github.com/McKay42), [Barubary](https://github.com/Barubary/dsdecmp),
[loveemu](https://github.com/loveemu/loveemu-lab), **Gericom**,
[CharlesVanEeckhout](https://github.com/CharlesVanEeckhout/actimagine),
[CyberBotX](https://github.com/CyberBotX/NCSF) and
[hackyourlife](https://github.com/hackyourlife/mph-viewer), with
[OpenTK](https://github.com/opentk/opentk), [OpenAL Soft](https://github.com/kcat/openal-soft) and
[SoundFlow](https://github.com/LSXPrime/SoundFlow) underneath. `FruityPrime -credits` prints the
list with what each one is for, and the Settings screen shows it too.

Metroid Prime Hunters is Nintendo's. No game data is included with this program: it comes from your
own cartridge dump.
