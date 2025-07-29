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

### Test Suite
A comprehensive NUnit test suite is located in `AnythingToGif.Tests/`:

**Run Tests:**
```bash
dotnet test AnythingToGif.Tests/
```

**Test Coverage:**
- **GIF Writer Tests** (`GifWriterTests.cs`): Tests core GIF file creation functionality
- **Simple Integration Tests** (`SimpleIntegrationTests.cs`): End-to-end testing of image conversion
- Automatic discovery of all public quantizers using reflection
- Testing of various color ordering modes and conversion scenarios
- Frame disposal methods, loop counts, and GIF format features

**InternalsVisibleTo:** The main project exposes internal types to the test assembly for comprehensive testing.

### Example Files
Test using the example files in `Examples/`:
- `PlanetDemo.jpg`: Sample image for testing
- `StressTest.png`: Complex image for algorithm validation

## Quantizer Architecture & Implementation Guidelines

### Base Architecture
- **QuantizerBase**: Abstract base class that handles common functionality
  - Edge case handling (empty input, zero colors, single colors)
  - Final palette generation with fallback colors
  - Consistent interface for all quantizers
- **Quantizer Implementations**: Override `_ReduceColorsTo` method for core algorithm
  - Focus purely on the quantization algorithm
  - Let base class handle edge cases and palette completion

### Common Implementation Patterns
1. **Edge Case Handling**: The base class automatically handles:
   - Empty input collections
   - Zero colors requested
   - Single color inputs
   - Insufficient colors from quantizer (fills with fallback colors)

2. **Color Comparison**: Always use `Color.ToArgb()` for comparisons to avoid issues between `Color.Red` static properties vs constructed colors like `Color.FromArgb(255, 0, 0)`

3. **Type Safety**: Use `uint` for color counts, not `int`, to avoid casting issues in LINQ operations

### Quantizer-Specific Notes

#### **OctreeQuantizer**
- Tree-based color reduction using octree data structure
- Handles color merging when too many colors are present
- Returns actual quantized colors (may be fewer than requested)

#### **AduQuantizer** 
- Competitive learning algorithm with adaptive learning rates
- Requires distance function and iteration count parameters
- Can handle complex color distributions with iterative refinement

#### **BinarySplittingQuantizer**
- Uses PCA/eigenvalue analysis for optimal color space splitting
- Requires MathNet.Numerics for mathematical operations
- Good for images with distinct color regions

#### **MedianCutQuantizer**
- Classic median cut algorithm for color cube subdivision
- Splits color space based on largest dimension
- Works well for general-purpose color reduction

#### **WuQuantizer**
- Wu's color quantization algorithm using moment-based approach
- Uses 32x32x32 color space subdivision
- Efficient for large color histograms

#### **VarianceBasedQuantizer & VarianceCutQuantizer**
- Split color cubes based on variance calculations
- Good for maintaining color fidelity in high-variance regions

### Wrapper Quantizers

#### **PcaQuantizerWrapper**
- Applies Principal Component Analysis before quantization
- Uses `PcaHelper` class for color space transformation
- **Important**: PcaHelper had divide-by-zero issues with single colors (fixed)

#### **AntRefinementWrapper**
- Iterative refinement of quantizer results using ant-colony-like approach
- Requires base quantizer, iteration count, and distance metric
- Improves quality through multiple refinement passes

### Color Distance Metrics
Available metrics in `ColorDistanceMetrics/`:
- **Euclidean**: Simple RGB distance
- **Manhattan**: Manhattan distance in RGB space  
- **CIE94/CIEDE2000**: Perceptually uniform color differences
- **Weighted YUV/YCbCr**: Luminance-weighted distances

### Testing Considerations
- Tests use reflection to discover all quantizer implementations automatically
- Property-based testing ensures all quantizers handle edge cases consistently
- Tests verify exact color counts, no exceptions on edge cases, and proper fallback behavior
- **Critical**: Use `ToArgb()` in tests for color comparisons to avoid static vs constructed color issues