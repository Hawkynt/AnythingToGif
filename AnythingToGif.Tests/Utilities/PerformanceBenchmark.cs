using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using AnythingToGif.Ditherers;

namespace AnythingToGif.Tests.Utilities;

/// <summary>
///   Performance benchmarking utilities for dithering algorithms.
///   Provides standardized timing and memory usage measurements.
/// </summary>
public static class PerformanceBenchmark {
  /// <summary>
  ///   Results from a performance benchmark run.
  /// </summary>
  public class BenchmarkResult {
    public string AlgorithmName { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsed { get; set; } // bytes
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public int PaletteSize { get; set; }
    public double PixelsPerSecond => this.ImageWidth * this.ImageHeight / this.ExecutionTime.TotalSeconds;
    public double MemoryPerPixel => (double)this.MemoryUsed / (this.ImageWidth * this.ImageHeight);
  }

  /// <summary>
  ///   Benchmarks a single dithering algorithm with the given parameters.
  /// </summary>
  /// <param name="ditherer">The dithering algorithm to test</param>
  /// <param name="algorithmName">Name for reporting</param>
  /// <param name="sourceImage">Input image</param>
  /// <param name="palette">Color palette</param>
  /// <param name="warmupRuns">Number of warmup iterations</param>
  /// <param name="measurementRuns">Number of measurement iterations</param>
  /// <returns>Benchmark results</returns>
  public static BenchmarkResult BenchmarkDitherer(
    IDitherer ditherer,
    string algorithmName,
    Bitmap sourceImage,
    Color[] palette,
    int warmupRuns = 3,
    int measurementRuns = 5) {
    // Warmup runs to stabilize performance
    for (var i = 0; i < warmupRuns; ++i) {
      _ = RunDithering(ditherer, sourceImage, palette);
      GC.Collect();
      GC.WaitForPendingFinalizers();
    }

    // Measurement runs
    var times = new List<TimeSpan>();
    var memoryUsages = new List<long>();

    for (var i = 0; i < measurementRuns; ++i) {
      var initialMemory = GC.GetTotalMemory(true);
      var stopwatch = Stopwatch.StartNew();

      _ = RunDithering(ditherer, sourceImage, palette);

      stopwatch.Stop();
      var finalMemory = GC.GetTotalMemory(false);

      times.Add(stopwatch.Elapsed);
      memoryUsages.Add(Math.Max(0, finalMemory - initialMemory));

      GC.Collect();
      GC.WaitForPendingFinalizers();
    }

    // Calculate median values for more stable results
    var medianTime = times.OrderBy(t => t.Ticks).Skip(times.Count / 2).First();
    var medianMemory = memoryUsages.OrderBy(m => m).Skip(memoryUsages.Count / 2).First();

    return new BenchmarkResult {
      AlgorithmName = algorithmName,
      ExecutionTime = medianTime,
      MemoryUsed = medianMemory,
      ImageWidth = sourceImage.Width,
      ImageHeight = sourceImage.Height,
      PaletteSize = palette.Length
    };
  }

  /// <summary>
  ///   Benchmarks multiple dithering algorithms with the same parameters.
  /// </summary>
  /// <param name="ditherers">Dictionary of algorithm name to ditherer</param>
  /// <param name="sourceImage">Input image</param>
  /// <param name="palette">Color palette</param>
  /// <param name="warmupRuns">Number of warmup iterations per algorithm</param>
  /// <param name="measurementRuns">Number of measurement iterations per algorithm</param>
  /// <returns>List of benchmark results sorted by execution time</returns>
  public static List<BenchmarkResult> BenchmarkMultiple(
    Dictionary<string, IDitherer> ditherers,
    Bitmap sourceImage,
    Color[] palette,
    int warmupRuns = 3,
    int measurementRuns = 5) {
    var results = new List<BenchmarkResult>();

    foreach (var (name, ditherer) in ditherers) {
      var result = BenchmarkDitherer(ditherer, name, sourceImage, palette, warmupRuns, measurementRuns);
      results.Add(result);
    }

    return results.OrderBy(r => r.ExecutionTime).ToList();
  }

  /// <summary>
  ///   Creates a standardized test image for benchmarking.
  /// </summary>
  /// <param name="width">Image width</param>
  /// <param name="height">Image height</param>
  /// <param name="pattern">Pattern type to generate</param>
  /// <returns>Test image bitmap</returns>
  public static Bitmap CreateTestImage(int width, int height, TestImagePattern pattern = TestImagePattern.Gradient) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

    switch (pattern) {
      case TestImagePattern.Gradient:
        CreateGradientPattern(bitmap);
        break;
      case TestImagePattern.Noise:
        CreateNoisePattern(bitmap);
        break;
      case TestImagePattern.Geometric:
        CreateGeometricPattern(bitmap);
        break;
      case TestImagePattern.Photo:
        CreatePhotoLikePattern(bitmap);
        break;
    }

    return bitmap;
  }

  /// <summary>
  ///   Creates a standard color palette for testing.
  /// </summary>
  /// <param name="size">Number of colors in palette</param>
  /// <param name="type">Type of palette to generate</param>
  /// <returns>Color palette array</returns>
  public static Color[] CreateTestPalette(int size, PaletteType type = PaletteType.Uniform) {
    var palette = new Color[size];

    switch (type) {
      case PaletteType.Uniform:
        CreateUniformPalette(palette);
        break;
      case PaletteType.Weighted:
        CreateWeightedPalette(palette);
        break;
      case PaletteType.Grayscale:
        CreateGrayscalePalette(palette);
        break;
    }

    return palette;
  }

  /// <summary>
  ///   Formats benchmark results as a readable report.
  /// </summary>
  /// <param name="results">Benchmark results to format</param>
  /// <returns>Formatted report string</returns>
  public static string FormatResults(List<BenchmarkResult> results) {
    if (results.Count == 0) return "No benchmark results available.";

    var report = new StringBuilder();
    report.AppendLine("Performance Benchmark Results");
    report.AppendLine("============================");
    report.AppendLine();

    var imageInfo = results.First();
    report.AppendLine($"Image: {imageInfo.ImageWidth}x{imageInfo.ImageHeight} pixels");
    report.AppendLine($"Palette: {imageInfo.PaletteSize} colors");
    report.AppendLine();

    report.AppendFormat("{0,-25} {1,10} {2,12} {3,10} {4,12}",
      "Algorithm", "Time (ms)", "Pixels/sec", "Memory(KB)", "Mem/Pixel");
    report.AppendLine();
    report.AppendLine(new string('-', 75));

    foreach (var result in results) {
      report.AppendFormat("{0,-25} {1,10:F2} {2,12:F0} {3,10:F1} {4,12:F3}",
        result.AlgorithmName,
        result.ExecutionTime.TotalMilliseconds,
        result.PixelsPerSecond,
        result.MemoryUsed / 1024.0,
        result.MemoryPerPixel);
      report.AppendLine();
    }

    return report.ToString();
  }

  public enum TestImagePattern {
    Gradient,
    Noise,
    Geometric,
    Photo
  }

  public enum PaletteType {
    Uniform,
    Weighted,
    Grayscale
  }

  private static Bitmap RunDithering(IDitherer ditherer, Bitmap sourceImage, Color[] palette) {
    var targetBitmap = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, sourceImage.Width, sourceImage.Height),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = sourceImage.Lock();
      ditherer.Dither(locker, targetData, palette);
    } finally {
      targetBitmap.UnlockBits(targetData);
    }

    return targetBitmap;
  }

  private static void CreateGradientPattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var r = x * 255 / bitmap.Width;
      var g = y * 255 / bitmap.Height;
      var b = (x + y) * 255 / (bitmap.Width + bitmap.Height);
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private static void CreateNoisePattern(Bitmap bitmap) {
    var random = new Random(42); // Fixed seed for reproducibility
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var r = random.Next(256);
      var g = random.Next(256);
      var b = random.Next(256);
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private static void CreateGeometricPattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var pattern = x / 32 % 2 == y / 32 % 2;
      var intensity = pattern ? 255 : 0;
      var r = intensity;
      var g = x * 255 / bitmap.Width;
      var b = y * 255 / bitmap.Height;
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private static void CreatePhotoLikePattern(Bitmap bitmap) {
    // Simulate photo-like content with varied frequency components
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var nx = (double)x / bitmap.Width;
      var ny = (double)y / bitmap.Height;

      var r = (int)(127 + 64 * Math.Sin(nx * Math.PI * 4) * Math.Cos(ny * Math.PI * 3));
      var g = (int)(127 + 64 * Math.Sin(nx * Math.PI * 6) * Math.Sin(ny * Math.PI * 2));
      var b = (int)(127 + 64 * Math.Cos(nx * Math.PI * 5) * Math.Cos(ny * Math.PI * 4));

      r = Math.Max(0, Math.Min(255, r));
      g = Math.Max(0, Math.Min(255, g));
      b = Math.Max(0, Math.Min(255, b));

      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private static void CreateUniformPalette(Color[] palette) {
    for (var i = 0; i < palette.Length; ++i) {
      var intensity = i * 255 / (palette.Length - 1);
      palette[i] = Color.FromArgb(intensity, intensity, intensity);
    }
  }

  private static void CreateWeightedPalette(Color[] palette) {
    for (var i = 0; i < palette.Length; ++i) {
      var t = (double)i / (palette.Length - 1);
      var r = (int)(255 * Math.Pow(t, 0.8));
      var g = (int)(255 * Math.Pow(t, 1.0));
      var b = (int)(255 * Math.Pow(t, 1.2));
      palette[i] = Color.FromArgb(r, g, b);
    }
  }

  private static void CreateGrayscalePalette(Color[] palette) {
    CreateUniformPalette(palette); // Same as uniform for grayscale
  }
}
