
# Gemini Project Overview: AnythingToGif

## Project Summary

AnythingToGif is a .NET-based command-line utility for converting a wide range of visual media formats into high-quality, true-color GIFs. It supports both still images and video files, offering advanced features like various color quantization (including Variance-Cut, BSITATCQ, Variance-Based, Binary Splitting, Binary Splitting with Ant-tree, and WU combined with Ant-tree) and dithering algorithms to achieve superior color fidelity. The tool can create GIFs with more than 256 colors by using multiple frames with local color palettes.

**Note**: It is crucial to keep the `README.md` file updated with any changes to the project's features, especially command-line options and supported modes, to ensure users have accurate and current information. You can easily do this by executing the application with the "--help" switch and copying the output.

## Project Structure

The solution is comprised of two main C# projects:

1.  **`AnythingToGif`**: This is the main executable project containing the command-line interface logic.
    *   `Program.cs`: The main entry point of the application, responsible for parsing command-line arguments and orchestrating the conversion process.
    *   `CLI/Options.cs`: Defines the command-line options available to the user.
    *   `Quantizers/`: Contains implementations of different color quantization algorithms (e.g., Octree, Median Cut, Variance-Cut, BSITATCQ, Variance-Based, Binary Splitting, Binary Splitting with Ant-tree, and WU combined with Ant-tree).
    *   `Ditherers/`: Contains implementations of different dithering algorithms (Floyd-Steinberg, A-dithering, Riemersma, Bayer, Matrix-based, etc.).
    *   `ColorDistanceMetrics/`: Contains implementations of different color distance algorithms.
    *   `Extensions/`: Provides extension methods for bitmap and color manipulation.

2.  **`GifFileFormat`**: This is a class library responsible for the low-level creation and writing of GIF files.
    *   `Writer.cs`: Handles the encoding and writing of GIF data to a file, including headers, frames, and color tables.
    *   This project is designed to be self-contained and could potentially be used in other applications that need to generate GIF files.

The solution is well-structured, with a clear separation between the user-facing command-line tool and the underlying GIF generation logic.

## How to Build and Run

The project is a standard .NET solution and can be built using the .NET SDK.

1.  **Restore Dependencies**:
    ```shell
    dotnet restore AnythingToGif.sln
    ```

2.  **Build the Solution**:
    ```shell
    dotnet build AnythingToGif.sln --configuration Release
    ```

3.  **Run the Application**:
    After a successful build, the executable will be located in the `AnythingToGif/bin/Release` directory. You can run it from the command line.

    **Syntax:**
    ```shell
    AnythingToGif.exe [<input>] [<options>] | <input> <output> [<options>]
    ```

    **Example:**
    ```shell
    # Convert a single image
    AnythingToGif.exe "X:\path\to\your\image.png" "X:\path\to\your\output.gif" -q Octree -d FloydSteinberg

    # Convert all files in a directory
    AnythingToGif.exe "X:\path\to\your\directory" "X:\path\to\your\output_directory"
    ```

## Key Command-Line Options

The tool offers a rich set of command-line options to control the GIF conversion process.

For a full list of options, run the application with the `--help` flag.

## Quantizer Architecture & Implementation Details

### Base Architecture
The quantizer system in AnythingToGif follows a well-structured inheritance pattern:

- **`QuantizerBase`**: Abstract base class providing common functionality
  - Handles edge cases (empty input, zero colors, single color scenarios)
  - Implements final palette generation with intelligent fallback colors
  - Provides consistent interface across all quantization algorithms
  - Contains `_GenerateFinalPalette()` method that fills insufficient palettes with Black, White, Transparent, then primary colors in varying shades

- **Quantizer Implementations**: Each inherits from `QuantizerBase` and overrides `_ReduceColorsTo()`
  - Focus purely on the core quantization algorithm
  - Edge cases are handled automatically by the base class
  - Return actual quantized colors (may be fewer than requested)

### Available Quantization Algorithms

#### Core Quantizers
1. **`OctreeQuantizer`**: Tree-based color reduction using octree data structure
   - Efficient for large color sets
   - Handles color merging when palette exceeds limits
   - Good general-purpose algorithm

2. **`MedianCutQuantizer`**: Classic median cut algorithm
   - Subdivides color space based on largest dimension
   - Well-suited for general-purpose color reduction
   - Predictable, stable results

3. **`WuQuantizer`**: Wu's color quantization using moment-based approach
   - Uses 32×32×32 color space subdivision
   - Highly efficient for large color histograms
   - Mathematical approach for optimal color selection

4. **`AduQuantizer`**: Competitive learning algorithm with adaptive learning rates
   - Requires distance function and iteration count parameters
   - Iterative refinement approach
   - Good for complex color distributions

5. **`BinarySplittingQuantizer`**: Uses PCA/eigenvalue analysis
   - Requires MathNet.Numerics library
   - Optimal color space splitting based on variance
   - Excellent for images with distinct color regions

6. **`VarianceBasedQuantizer`** & **`VarianceCutQuantizer`**: Variance-driven algorithms
   - Split color cubes based on variance calculations
   - Maintain color fidelity in high-variance regions
   - Good for preserving important color transitions

#### Wrapper Quantizers
1. **`PcaQuantizerWrapper`**: Applies Principal Component Analysis before quantization
   - Uses `PcaHelper` class for color space transformation
   - Can enhance any base quantizer
   - **Note**: Fixed divide-by-zero issue with single colors

2. **`AntRefinementWrapper`**: Iterative refinement using ant-colony-like approach
   - Enhances results from any base quantizer
   - Requires base quantizer, iteration count, and distance metric
   - Multiple refinement passes improve quality

### Color Distance Metrics
Available in `ColorDistanceMetrics/` directory:
- **Euclidean**: Simple RGB distance calculation
- **Manhattan**: Manhattan distance in RGB space
- **CIE94/CIEDE2000**: Perceptually uniform color differences
- **Weighted YUV/YCbCr**: Luminance-weighted distance calculations

### Testing Infrastructure
- **Comprehensive NUnit Test Suite**: Located in `AnythingToGif.Tests/`
- **Reflection-Based Discovery**: Tests automatically discover all quantizer implementations
- **Property-Based Testing**: Ensures consistent behavior across all quantizers
- **Edge Case Coverage**: Tests handle empty input, zero colors, single colors, extreme weights
- **Color Comparison**: Uses `ToArgb()` method to avoid static vs constructed color issues

### Important Implementation Notes
1. **Type Safety**: Use `uint` for color counts, not `int`, to avoid LINQ casting issues
2. **Color Comparison**: Always use `Color.ToArgb()` for comparisons to handle `Color.Red` vs `Color.FromArgb(255,0,0)` differences
3. **Edge Case Handling**: Base class automatically handles all common edge cases
4. **Palette Completion**: When quantizers return fewer colors than requested, base class fills with intelligent fallback colors
5. **Thread Safety**: Individual quantizer instances are not thread-safe; create separate instances for concurrent usage

### Dithering Algorithms

The project implements various dithering techniques to improve visual quality when reducing colors:

#### Matrix-Based Dithering (`MatrixBasedDitherer`)
- **Floyd-Steinberg**: Classic error diffusion algorithm with 7:3:5:1 distribution
- **Jarvis-Judice-Ninke**: More complex error diffusion with larger kernel (48 divisor)
- **Stucki**: Similar to JJN but with different coefficient distribution (42 divisor)
- **Atkinson**: Apple's dithering algorithm with lighter error distribution (8 divisor)
- **Burkes**: Two-row error diffusion with 32 divisor
- **Sierra**: Family of algorithms (Sierra, TwoRowSierra, SierraLite) with varying complexity
- **Custom Variants**: FalseFloydSteinberg, Simple, Pigeon, StevensonArce, ShiauFan, Fan93

#### Riemersma Dithering (`RiemersmaDitherer`)
Advanced space-filling curve based dithering algorithm:
- **Algorithm**: Uses Hilbert curve or linear traversal to process pixels in spatially coherent order
- **Error History**: Maintains exponentially decaying error buffer for improved error distribution
- **Variants Available**:
  - `Default`: 16-entry history buffer with Hilbert curve traversal
  - `Small`: 8-entry history buffer for faster processing
  - `Large`: 32-entry history buffer for highest quality
  - `Linear`: 16-entry history buffer with linear pixel traversal
- **Performance**: Iterative Hilbert curve implementation prevents stack overflow on large images
- **Quality**: Produces more natural-looking dithering patterns compared to matrix-based methods

#### A-Dithering (`ADitherer`)
Competitive learning approach to dithering with adaptive error distribution.

#### Noise-Based Dithering (`NoiseDitherer`)
Statistical noise patterns for professional-quality dithering:
- **White Noise**: Uniform random distribution across all frequencies
  - Completely uncorrelated, pure randomness
  - Good for breaking up visible patterns in uniform areas
- **Blue Noise**: High-frequency emphasis with optimal spatial distribution
  - Uses void-and-cluster algorithm for even point distribution
  - Excellent for avoiding clustering artifacts
  - Preferred for high-quality image reproduction
- **Brown Noise**: Low-frequency emphasis with Brownian motion characteristics  
  - Produces smoother, more natural gradients
  - Correlated noise with organic appearance
- **Pink Noise**: Balanced 1/f noise distribution
  - Natural frequency balance between white and brown noise
  - Mimics many natural phenomena
- **Intensity Control**: Light (30%), Normal (50%), Strong (70%) variants for each type
- **Deterministic**: Coordinate-based seeding ensures reproducible results
- **Performance**: Blue noise pre-computes texture patterns; others generate in real-time

### High-Color GIF Innovation
The core innovation of AnythingToGif lies in its ability to create GIFs with more than the traditional 256-color limitation:
1. **Multiple Frames with Local Palettes**: Each frame can have its own 256-color palette
2. **Frame Disposal Methods**: Preserve existing pixels while adding new colors
3. **Color Ordering Strategies**: Determine optimal sequence for introducing colors across frames
4. **TrueColor Representation**: Achieve 24-bit color depth through frame layering
