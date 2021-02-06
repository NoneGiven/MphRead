# MphRead
This project is a model viewer, scene renderer, and general parser for file formats used in the Nintendo DS game Metroid Prime Hunters. The renderer is implemented using OpenGL via the [OpenTK](https://github.com/opentk/opentk) library. Documentation of various game features can be found in the [wiki](https://github.com/NoneGiven/MphRead/wiki).

## Features
- Renders individual models or complete game scenes with room entities
- Processes and renders particle systems and effects
- Exports models to COLLADA, textures to PNG, and sound effects to WAV
- Generates Python scripts to import model animations and more into Blender
- Contains game metadata and parses many more file types

## Planned
- Parse and visualize collision data
- Music and SFX playback
- And more!

## Usage

Ensure there is a `paths.txt` file in the same location as the `MphRead` binary. The contents should be as follows:

```
MPH file path
FH file path
Export path
```

The file paths must follow the structure of the games' file systems, with each root being equivalent to the `root` directory of the ROM; for example, `<MPH file path>\models\Trace_lod1_Model.bin` should be a valid path. Additionally, for the viewer to work, there must also be an `_archives` folder in the root which contains one folder for each archive, with the extracted files for that archive inside. This is not necessary for the exporter.

If you don't have First Hunt files or don't need an export path, those lines can be left blank. Paths can be absolute or relative, so a blank line can also be used to point the directory where the `MphRead` binary is located.

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
* [Models (MPH)](https://github.com/NoneGiven/MphRead/wiki/Models) / [Models (FH)](https://github.com/NoneGiven/MphRead/wiki/Models-(First-Hunt))
* [Rooms (MPH)](https://github.com/NoneGiven/MphRead/wiki/Rooms) / [Rooms (FH)](https://github.com/NoneGiven/MphRead/wiki/Rooms-(First-Hunt))

See also:
* [Full setup and export guide](https://github.com/NoneGiven/MphRead/wiki/Setup-&-Export-Guide)

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
