# MphRead
This project is a model viewer, scene renderer, and general parser for file formats used in the Nintendo DS game Metroid Prime Hunters. The renderer is implemented using OpenGL via the [OpenTK](https://github.com/opentk/opentk) library. Documentation of various game features can be found in the [wiki](https://github.com/NoneGiven/MphRead/wiki).

## Features
- Renders individual models or complete game rooms with entities
- Processes and renders particle systems and effects
- Visualizes collision data for rooms and entities
- Plays in-engine camera sequences (cutscenes)
- Exports models to COLLADA, textures to PNG, and sound effects to WAV
- Generates Python scripts to import model animations and more into Blender

## Planned
- Music and SFX playback
- - Room editor and save editor
- Render more things, implement more gameplay logic
- And even more!

## Usage

After setup, MphRead can be launched from the executable with no arguments, and menu prompts will appear to help you set up the scene.

See the [full setup and export guide](https://github.com/NoneGiven/MphRead/wiki/Setup-&-Export-Guide) for details on setup and command line options.

## Building

If you do not want to build from source, simply download and run the latest [release](https://github.com/NoneGiven/MphRead/releases).

### With Visual Studio

With a recent version of [Visual Studio 2019](https://visualstudio.microsoft.com/vs/) installed, you should be able to open the solution and build immediately.

### Without Visual Studio

- Install the [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1).
- Run `dotnet build` in the `src/MphRead` directory.

## Acknowledgements

A significant portion of this project's code was based on the file format information or source code from several other projects.

- **dsgraph** - The original MPH model viewer, on which all other projects are built.
- **[Chemical's model format](https://gitlab.com/ch-mcl/metroid-prime-hunters-file-document/-/blob/master/Model/BinModel.md)** - Documentation of the model format.
- **[McKay42's mph-model-viewer](https://github.com/McKay42/mph-model-viewer)** - COLLADA export method.
- **[McKay42's mph-arc-extractor](https://github.com/McKay42/mph-arc-extractor)** - ARC file format information.
- **[Barubary's dsdecmp](https://github.com/Barubary/dsdecmp)** - LZ10 compression routines.
- **[loveemu's swav2wav](https://github.com/loveemu/loveemu-lab)** - SWAV conversion function.

## Special Thanks

This project is an ongoing reverse engineering effort developed parallel to **[hackyourlife's mph-viewer](https://github.com/hackyourlife/mph-viewer)**, a model viewer implementation in C. Major features such as the transparency rendering implementation are derived from its source code.
