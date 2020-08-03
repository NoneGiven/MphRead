# MphRead
This project is a reader and viewer for file formats used in the Nintendo DS game Metroid Prime Hunters. It is a console application written in C# which includes a model viewer using OpenGL via the [OpenTK](https://github.com/opentk/opentk) library.

The renderer is a work in progress. A much more impressive C implementation with more features can be found here:
- **[mph-viewer by hackyourlife](https://github.com/hackyourlife/mph-viewer)**

## Features
- Parses model file metadata to CLR types
- Reads image data and OpenGL display list instructions
- Exports images to PNG and models to COLLADA
- Generates Python scripts to import model metadata to Blender
- Renders models, and allows inspecting and manipulating them

## Planned
- Parse animation and collision files
- Support rendering the full range of effects found in MPH models
- Full map rendering with entities

## Usage

Ensure there is a `paths.txt` file in the same location as the `MphRead` binary.
- The first line of the file must be the path to the directory which contains your MPH files.
  - This directory must follow the structure of the MPH file system, with the root being equivalent to the `root` directory of the ROM.
  - For the viewer to work, there must also be an `_archives` folder in the root which contains one folder for each archive, with the extracted files for that archive inside. This is not necessary for the exporter.
- The second line of the file should be the path where you want your exported files to be placed.

```
MphRead usage:

    -room <room_name>
    -model <model_name> [recolor_index]

The layer mask and recolor index are optional.
At most one room may be specified, while any number of models may be specified.

- or -

    -extract <archive_path>

If the target archive is LZ10-compressed, it will be decompressed.

- or -

    -export <target_name>

The export target may be a model or room name.
```

See [this page](https://github.com/NoneGiven/MphRead/wiki) for a list of room and model names.

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
- **[Chemical's model format](https://gitlab.com/ch-mcl/metroid-prime-hunters-file-document/-/blob/master/Model/BinModel.md)** - Significant documentation of the model format.
- **[McKay42's model viewer](https://github.com/McKay42/mph-model-viewer)** - Rendering information and COLLADA export method.
- **[McKay42's ARC extractor](https://github.com/McKay42/mph-arc-extractor)** - ARC file format information.
- **[Barubary's dsdecmp](https://github.com/Barubary/dsdecmp)** - LZ10 compression routines.
- **[hackyourlife's model viewer](https://github.com/hackyourlife/mph-viewer)** - Advanced model format and rendering information and much more.
