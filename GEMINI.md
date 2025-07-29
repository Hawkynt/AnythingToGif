
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
    *   `Ditherers/`: Contains implementations of different dithering algorithms.
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

### High-Color GIF Innovation
The core innovation of AnythingToGif lies in its ability to create GIFs with more than the traditional 256-color limitation:
1. **Multiple Frames with Local Palettes**: Each frame can have its own 256-color palette
2. **Frame Disposal Methods**: Preserve existing pixels while adding new colors
3. **Color Ordering Strategies**: Determine optimal sequence for introducing colors across frames
4. **TrueColor Representation**: Achieve 24-bit color depth through frame layering
