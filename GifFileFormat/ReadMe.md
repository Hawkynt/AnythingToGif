# Hawkynt.GifFileFormat

[![Build](https://github.com/Hawkynt/AnythingToGif/actions/workflows/Build.yml/badge.svg)](https://github.com/Hawkynt/AnythingToGif/actions/workflows/Build.yml)
[![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/AnythingToGif?branch=master)](https://github.com/Hawkynt/AnythingToGif/commits/master/GifFileFormat)
[![NuGet](https://img.shields.io/nuget/v/Hawkynt.GifFileFormat)](https://www.nuget.org/packages/Hawkynt.GifFileFormat/)
![License](https://img.shields.io/github/license/Hawkynt/AnythingToGif)

## Overview

**GifFileFormat** is a C# project aimed at providing a robust way to work with GIF files. This project includes various classes and methods to handle different aspects of GIFs, such as dimensions, frames, color resolution, and more.

## Project Structure

- **ColorResolution**: Handles the color resolution settings for GIFs.
- **Dimensions**: Manages the width and height of the GIF.
- **Frame**: Represents individual frames within a GIF.
- **FrameDisposalMethod**: Handles how frames are disposed of when a new frame is drawn.
- **LoopCount**: Manages the looping behavior of GIFs.
- **Offset**: Handles the offset values for GIF frames.
- **Writer**: Provides methods to write or save GIF files.

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) installed on your machine.

### Building the Project

To build the project, navigate to the project directory and run:

```bash
dotnet build
```

### Running Tests

If the project includes unit tests (typically in a `Tests` or similar directory), you can run them using:

```bash
dotnet test
```

### Using the Library

To use the **GifFileFormat** classes, simply include them in your project and start working with GIF files. Here’s a basic example:

```csharp
using Hawkynt.GifFileFormat;

public class Program {
  static void Main(string[] args) {
    // Example usage of the Frame class
    Frame gifFrame = new Frame();
    // Add your code to manipulate the gifFrame here
  }
}
```

## Contributing

Contributions are welcome! Please feel free to submit issues, fork the repository, and send pull requests.

## License

This project is licensed under the MIT License. See the `LICENSE` file for more details.

## Contact

For any questions or issues, please open an issue on the GitHub repository or contact the maintainers.
