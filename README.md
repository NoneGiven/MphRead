# MphRead
This project is a reader and viewer for file formats used in the Nintendo DS game Metroid Prime Hunters. It is a console application written in C# which includes a model viewer using OpenGL via the [OpenTK](https://github.com/opentk/opentk) library.

The renderer is a work in progress. A much more impressive C implementation with more features can be found here:
- **[mph-viewer by hackyourlife](https://github.com/McKay42/mph-model-viewer)**

## Features
- Parses model file metadata to CLR types
- Reads image data and OpenGL display list instructions
- Exports images to PNG and models to COLLADA
- Renders models

## Planned
- Parse animation and collision files
- Support rendering the full range of effects found in MPH models
- Full map rendering with entities

## Usage

- Create a `paths.txt` file in `src/MphRead`. The first line should contain a full path to the directory which contains your MPH files. This directory must follow the structure of the MPH file system, with the root being equivalent to of the `root` directory of the ROM.
- Run `MphRead.exe` with the model name and (optional) recolor index as arguments, e.g. `MphRead.exe Trace_lod1 3`.

## Building

If you do not want to build from source, simply download and run the latest [release](https://github.com/NoneGiven/MphRead/releases).

### With Visual Studio

With a recent version of [Visual Studio 2019](https://visualstudio.microsoft.com/vs/) installed, you should be able to open the solution and build immediately.

### Without Visual Studio

- Install the [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1).
- Run `dotnet build` in the `src/MphRead` directory.

## Acknowledgements

A significant portion of this project's code was  based on the file format information or source code from several other projects.

- **dsgraph** - The original MPH model viewer, on which all other projects are built.
- **[Chemical's model format](https://gitlab.com/ch-mcl/metroid-prime-hunters-file-document/-/blob/master/Model/BinModel.md)** - Significant documentation of the model format.
- **[McKay42'2 model viewer](https://github.com/McKay42/mph-model-viewer)** - Rendering information and COLLADA export method.
- **[hackyourlife's model viewer](https://github.com/hackyourlife/mph-viewer/issues)** - Advanced model format and rendering information.
