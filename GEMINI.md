
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
