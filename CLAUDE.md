# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AnythingToGif is a C#/.NET 8.0 Windows console application that converts images and videos to high-color GIF format. The project implements advanced GIF creation techniques by using multiple frames with local color palettes to achieve TrueColor (24-bit) representation, bypassing the traditional 256-color limitation.

## Architecture

The solution consists of two main projects:

- **AnythingToGif** (main console application)
  - Uses CommandLineParser for CLI argument handling
  - Integrates FFmpeg.AutoGen for video processing
  - Implements various quantization algorithms (Octree, Median-Cut, Wu, etc.)
  - Contains multiple dithering techniques (Floyd-Steinberg, Jarvis-Judice-Ninke, etc.)
  - Supports various color distance metrics (Euclidean, CIE94, CIEDE2000, etc.)

- **GifFileFormat** (custom GIF writer library)
  - Low-level GIF file format implementation
  - Handles local color palettes, transparency, frame disposal methods
  - Enables fine-grained control over GIF bytes for high-color output

## Key Technical Components

### Color Processing Pipeline
1. **Quantizers** (`Quantizers/`): Reduce color palette (Octree, MedianCut, Wu, etc.)
2. **Ditherers** (`Ditherers/`): Apply dithering patterns to improve visual quality
3. **Color Distance Metrics** (`ColorDistanceMetrics/`): Calculate color similarity using various algorithms
4. **Color Ordering** (`ColorOrderingMode.cs`): Determine sequence for introducing new colors across frames

### Core Classes
- `Program.cs`: Main entry point with file processing logic
- `SingleImageHiColorGifConverter.cs`: Converts single images to high-color GIFs
- `VideoFrameExtractor.cs`: Extracts frames from video files using FFmpeg
- `Extensions/`: Helper methods for Bitmap, Color, and FileInfo operations

## Development Commands

### Build
```bash
dotnet build AnythingToGif.sln
```

### Run
```bash
dotnet run --project AnythingToGif -- [arguments]
```

Example with arguments:
```bash
dotnet run --project AnythingToGif -- --metric Euclidean --usePca Examples Examples
```

### Clean
```bash
dotnet clean AnythingToGif.sln
```

### Restore Dependencies
```bash
dotnet restore AnythingToGif.sln
```

## Key Configuration Settings

- **Target Framework**: .NET 8.0 Windows
- **Unsafe Blocks**: Enabled (required for performance-critical bitmap operations)
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Disabled (explicit using statements required)

## Dependencies

- **CommandLineParser**: CLI argument parsing
- **FFmpeg.AutoGen**: Video frame extraction
- **FrameworkExtensions**: Custom utility libraries
- **MathNet.Numerics**: Mathematical computations for quantization
- **System.Drawing.Common**: Bitmap and image processing

## FFmpeg Integration

The application includes FFmpeg DLLs in the `ffmpeg/` directory and sets the FFmpeg root path programmatically. Video processing requires these native libraries to be present in the output directory.

## High-Color GIF Technique

The core innovation uses multiple GIF frames with local color palettes:
1. First frame: 256 most common colors (quantized and dithered)
2. Subsequent frames: 255 new colors each (256th reserved for transparency)
3. Frame disposal method preserves existing pixels while adding new colors
4. Color ordering strategies determine which colors to introduce in each frame

## Testing

No formal test framework is configured. Test using the example files in `Examples/`:
- `PlanetDemo.jpg`: Sample image for testing
- `StressTest.png`: Complex image for algorithm validation