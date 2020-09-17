# MphRead
This project is a model viewer, scene renderer, and general parser for file formats used in the Nintendo DS game Metroid Prime Hunters. The renderer is implemented using OpenGL via the [OpenTK](https://github.com/opentk/opentk) library. Documentation of various game features can be found in the [wiki](https://github.com/NoneGiven/MphRead/wiki).

## Features
- Renders individual models or complete game scenes with room entities
- Exports models to COLLADA, textures to PNG, and sound effects to WAV
- Generates Python scripts to import additional model metadata into Blender
- Contains game metadata and parses many more file types

## Planned
- Parse and render collision data
- Implement particle effects
- Music and SFX playback
- Export model animations

## Usage

Ensure there is a `paths.txt` file in the same location as the `MphRead` binary.
- The first line of the file must be the path to the directory which contains your MPH files.
  - This directory must follow the structure of the MPH file system, with the root being equivalent to the `root` directory of the ROM.
  - For the viewer to work, there must also be an `_archives` folder in the root which contains one folder for each archive, with the extracted files for that archive inside. This is not necessary for the exporter.
- The second line of the file should be the path where you want your exported files to be placed.

```
MphRead usage:

    -room <room_name -or- room_id>
    -model <model_name> [recolor_index]

The layer mask and recolor index are optional.
At most one room may be specified, while any number of models may be specified.
To load First Hunt models, include -fh in the argument list.
Available room options: -mode, -players, -boss, -node, -entity

- or -

    -extract <archive_path>

If the target archive is LZ10-compressed, it will be decompressed.

- or -

    -export <target_name>

The export target may be a model or room name.
```

See these wiki pages for model and room names:
* [Models](https://github.com/NoneGiven/MphRead/wiki/Models)
* [Rooms](https://github.com/NoneGiven/MphRead/wiki/Rooms)

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
