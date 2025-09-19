using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

/// <summary>
///   Adaptive dithering system that automatically selects the best dithering algorithm
///   based on image characteristics and quality metrics.
///   This system analyzes the input image to determine properties like:
///   - Color complexity and distribution
///   - Edge density and detail level
///   - Gradient smoothness
///   - Noise characteristics
///   Based on these characteristics, it selects the most appropriate dithering
///   algorithm to achieve optimal visual quality.
/// </summary>
public readonly struct AdaptiveDitherer(AdaptiveDitherer.AdaptiveStrategy strategy = AdaptiveDitherer.AdaptiveStrategy.QualityOptimized) : IDitherer {
  /// <summary>
  ///   Strategy for adaptive algorithm selection.
  /// </summary>
  public enum AdaptiveStrategy {
    /// <summary>Optimize for visual quality regardless of performance</summary>
    QualityOptimized,

    /// <summary>Balance quality and performance</summary>
    Balanced,

    /// <summary>Optimize for performance while maintaining acceptable quality</summary>
    PerformanceOptimized,

    /// <summary>Use machine learning-like selection based on multiple criteria</summary>
    SmartSelection
  }

  /// <summary>
  ///   Image characteristics analyzed for adaptive selection.
  /// </summary>
  public readonly struct ImageCharacteristics {
    public double ColorComplexity { get; init; } // 0-1, higher = more colors
    public double EdgeDensity { get; init; } // 0-1, higher = more edges
    public double GradientSmoothness { get; init; } // 0-1, higher = smoother gradients
    public double NoiseLevel { get; init; } // 0-1, higher = more noise
    public double DetailLevel { get; init; } // 0-1, higher = more fine detail
    public int ImageSize { get; init; } // Total pixels
    public int PaletteSize { get; init; } // Number of colors in palette
  }

  /// <summary>
  ///   Creates an adaptive ditherer optimized for visual quality.
  /// </summary>
  public static IDitherer QualityOptimized { get; } = new AdaptiveDitherer(AdaptiveStrategy.QualityOptimized);

  /// <summary>
  ///   Creates an adaptive ditherer that balances quality and performance.
  /// </summary>
  public static IDitherer Balanced { get; } = new AdaptiveDitherer(AdaptiveStrategy.Balanced);

  /// <summary>
  ///   Creates an adaptive ditherer optimized for performance.
  /// </summary>
  public static IDitherer PerformanceOptimized { get; } = new AdaptiveDitherer(AdaptiveStrategy.PerformanceOptimized);

  /// <summary>
  ///   Creates an adaptive ditherer with smart algorithm selection.
  /// </summary>
  public static IDitherer SmartSelection { get; } = new AdaptiveDitherer(AdaptiveStrategy.SmartSelection);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    // Handle edge case: empty palette
    if (palette.Count == 0) {
      // Fill with zeros (no colors available)
      var data = (byte*)target.Scan0;
      var totalBytes = target.Height * target.Stride;
      for (var i = 0; i < totalBytes; ++i)
        data[i] = 0;
      return;
    }

    // Analyze image characteristics
    var characteristics = AnalyzeImage(source, palette);

    // Select best dithering algorithm based on characteristics and strategy
    var selectedDitherer = SelectOptimalDitherer(characteristics);

    // Apply the selected dithering algorithm
    selectedDitherer.Dither(source, target, palette, colorDistanceMetric);
  }

  private static ImageCharacteristics AnalyzeImage(BitmapExtensions.IBitmapLocker source, IReadOnlyList<Color> palette) {
    var width = source.Width;
    var height = source.Height;
    var totalPixels = width * height;

    // Sample pixels for analysis (use every 4th pixel for performance)
    var sampleSize = Math.Min(10000, totalPixels / 4);
    var sampleStep = Math.Max(1, totalPixels / sampleSize);

    var colorSet = new HashSet<int>();
    var edgeCount = 0;
    var gradientVariance = 0.0;
    var noiseSum = 0.0;
    var detailSum = 0.0;
    var sampleCount = 0;

    for (var y = 0; y < height; y += Math.Max(1, (int)Math.Sqrt(sampleStep)))
    for (var x = 0; x < width; x += Math.Max(1, (int)Math.Sqrt(sampleStep))) {
      var pixel = source[x, y];
      colorSet.Add(pixel.ToArgb());

      // Edge detection (simple Sobel-like operator)
      if (x > 0 && y > 0 && x < width - 1 && y < height - 1) {
        var edgeStrength = CalculateEdgeStrength(source, x, y);
        if (edgeStrength > 30) edgeCount++; // Threshold for edge detection
        detailSum += edgeStrength;
      }

      // Gradient analysis (look at local variance)
      if (x > 1 && y > 1 && x < width - 2 && y < height - 2) {
        var localVariance = CalculateLocalVariance(source, x, y);
        gradientVariance += localVariance;
      }

      // Noise detection (high-frequency variation)
      if (x > 0 && y > 0) {
        var prevPixel = source[x - 1, y - 1];
        var colorDistance = Math.Abs(pixel.R - prevPixel.R) +
                            Math.Abs(pixel.G - prevPixel.G) +
                            Math.Abs(pixel.B - prevPixel.B);
        noiseSum += colorDistance;
      }

      sampleCount++;
    }

    // Normalize metrics
    var colorComplexity = Math.Min(1.0, colorSet.Count / (double)Math.Min(1000, totalPixels / 10));
    var edgeDensity = edgeCount / (double)sampleCount;
    var gradientSmoothness = 1.0 - Math.Min(1.0, gradientVariance / (sampleCount * 10000.0));
    var noiseLevel = Math.Min(1.0, noiseSum / (sampleCount * 255.0));
    var detailLevel = Math.Min(1.0, detailSum / (sampleCount * 100.0));

    return new ImageCharacteristics {
      ColorComplexity = colorComplexity,
      EdgeDensity = edgeDensity,
      GradientSmoothness = gradientSmoothness,
      NoiseLevel = noiseLevel,
      DetailLevel = detailLevel,
      ImageSize = totalPixels,
      PaletteSize = palette.Count
    };
  }

  private static double CalculateEdgeStrength(BitmapExtensions.IBitmapLocker source, int x, int y) {
    // Simple edge detection using difference with neighbors
    var center = source[x, y];
    var right = source[x + 1, y];
    var down = source[x, y + 1];
    var diag = source[x + 1, y + 1];

    var horizontalGrad = Math.Abs(center.R - right.R) + Math.Abs(center.G - right.G) + Math.Abs(center.B - right.B);
    var verticalGrad = Math.Abs(center.R - down.R) + Math.Abs(center.G - down.G) + Math.Abs(center.B - down.B);
    var diagonalGrad = Math.Abs(center.R - diag.R) + Math.Abs(center.G - diag.G) + Math.Abs(center.B - diag.B);

    return Math.Sqrt(horizontalGrad * horizontalGrad + verticalGrad * verticalGrad + diagonalGrad * diagonalGrad);
  }

  private static double CalculateLocalVariance(BitmapExtensions.IBitmapLocker source, int x, int y) {
    // Calculate variance in 3x3 neighborhood
    var values = new double[9];
    var index = 0;

    for (var dy = -1; dy <= 1; ++dy)
    for (var dx = -1; dx <= 1; ++dx) {
      var pixel = source[x + dx, y + dy];
      values[index++] = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B; // Luminance
    }

    var mean = values.Average();
    var variance = values.Select(v => (v - mean) * (v - mean)).Average();
    return variance;
  }

  private IDitherer SelectOptimalDitherer(ImageCharacteristics characteristics) {
    return strategy switch {
      AdaptiveStrategy.QualityOptimized => SelectForQuality(characteristics),
      AdaptiveStrategy.Balanced => SelectForBalance(characteristics),
      AdaptiveStrategy.PerformanceOptimized => SelectForPerformance(characteristics),
      AdaptiveStrategy.SmartSelection => SelectSmart(characteristics),
      _ => MatrixBasedDitherer.FloydSteinberg
    };
  }

  private static IDitherer SelectForQuality(ImageCharacteristics characteristics) {
    // High-quality algorithm selection based on image characteristics

    // For images with smooth gradients, use high-quality algorithms
    if (characteristics is { GradientSmoothness: > 0.7, EdgeDensity: < 0.3 })
      return characteristics.PaletteSize > 64
        ? KnollDitherer.HighQuality
        : RiemersmaDitherer.Large;

    // For high detail images, use sophisticated error diffusion
    if (characteristics.DetailLevel > 0.6 || characteristics.EdgeDensity > 0.5) {
      // Choose based on complexity - more complex images get more sophisticated dithering
      return characteristics.ColorComplexity switch {
        > 0.8 => MatrixBasedDitherer.StevensonArce,  // Most sophisticated matrix ditherer
        > 0.6 => MatrixBasedDitherer.JarvisJudiceNinke,
        > 0.4 => MatrixBasedDitherer.Stucki,
        _ => MatrixBasedDitherer.Sierra
      };
    }

    // For high color complexity, use advanced algorithms
    if (characteristics.ColorComplexity > 0.8)
      return NConvexDitherer.Default;

    // For noisy images, use noise-based dithering
    if (characteristics.NoiseLevel > 0.4)
      return NoiseDitherer.BlueStrong;

    // Default to high-quality Floyd-Steinberg
    return MatrixBasedDitherer.FloydSteinberg;
  }

  private static IDitherer SelectForBalance(ImageCharacteristics characteristics) {
    // Balanced selection considering both quality and performance

    // For large images, prefer faster algorithms
    if (characteristics.ImageSize > 1000000) {
      // > 1MP - use faster matrix ditherers
      return characteristics.GradientSmoothness > 0.6
        ? OrderedDitherer.Bayer8x8
        : characteristics.DetailLevel switch {
          > 0.7 => MatrixBasedDitherer.TwoRowSierra,  // Good balance of quality/speed
          > 0.4 => MatrixBasedDitherer.Atkinson,      // Fast but decent quality
          _ => MatrixBasedDitherer.SierraLite         // Fastest matrix ditherer
        };
    }

    // For medium complexity, use standard algorithms with more variety
    if (characteristics is { ColorComplexity: > 0.5, DetailLevel: > 0.4 }) {
      return characteristics.EdgeDensity switch {
        > 0.6 => MatrixBasedDitherer.Burkes,        // Good for edges
        > 0.3 => MatrixBasedDitherer.FloydSteinberg, // Standard choice
        _ => MatrixBasedDitherer.FalseFloydSteinberg // Lighter variant
      };
    }

    // For low complexity, simpler algorithms suffice
    if (characteristics.ColorComplexity < 0.3)
      return OrderedDitherer.Bayer4x4;

    // Use adaptive selection
    return characteristics.NoiseLevel > 0.3
      ? NoiseDitherer.Blue
      : NClosestDitherer.Default;
  }

  private static IDitherer SelectForPerformance(ImageCharacteristics characteristics) {
    // Performance-optimized selection

    // Always prefer ordered dithering for speed
    if (characteristics.GradientSmoothness > 0.5)
      return OrderedDitherer.Bayer8x8;

    // Use fast error diffusion for detailed images - prioritize speed
    if (characteristics.DetailLevel > 0.6) {
      return characteristics.ImageSize switch {
        > 2000000 => MatrixBasedDitherer.Simple,        // Fastest for very large images
        > 500000 => MatrixBasedDitherer.SierraLite,     // Fast and reasonable quality
        _ => MatrixBasedDitherer.Atkinson               // Good balance for smaller images
      };
    }

    // For high noise, use simple noise dithering
    if (characteristics.NoiseLevel > 0.4)
      return NoiseDitherer.WhiteLight; // Lighter variant for speed

    // Default to fastest reasonable algorithm
    return OrderedDitherer.Bayer4x4;
  }

  private static IDitherer SelectSmart(ImageCharacteristics characteristics) {
    // Smart selection using multiple criteria and scoring

    var candidates = new Dictionary<IDitherer, double>();

    // Score matrix-based ditherers based on characteristics
    
    // Floyd-Steinberg: Good general purpose
    var fsScore = 0.7 + characteristics.DetailLevel * 0.3;
    candidates[MatrixBasedDitherer.FloydSteinberg] = fsScore;
    
    // Jarvis-Judice-Ninke: Better for high detail
    var jjnScore = characteristics.DetailLevel * 0.8 + characteristics.EdgeDensity * 0.4;
    if (characteristics.ImageSize > 1000000) jjnScore *= 0.8; // Penalize for large images
    candidates[MatrixBasedDitherer.JarvisJudiceNinke] = jjnScore;
    
    // Stucki: Good balance
    var stuckiScore = characteristics.DetailLevel * 0.6 + characteristics.ColorComplexity * 0.4;
    if (characteristics.ImageSize > 1000000) stuckiScore *= 0.9; // Less penalty than JJN
    candidates[MatrixBasedDitherer.Stucki] = stuckiScore;
    
    // Atkinson: Fast, good for high contrast
    var atkinsonScore = characteristics.EdgeDensity * 0.7 + (1.0 - characteristics.GradientSmoothness) * 0.5;
    if (characteristics.ImageSize > 1000000) atkinsonScore *= 1.1; // Bonus for large images (speed)
    candidates[MatrixBasedDitherer.Atkinson] = atkinsonScore;
    
    // Sierra variants: Good for different scenarios
    var sierraScore = characteristics.DetailLevel * 0.5 + characteristics.ColorComplexity * 0.3;
    candidates[MatrixBasedDitherer.Sierra] = sierraScore;
    candidates[MatrixBasedDitherer.TwoRowSierra] = sierraScore * 1.1; // Slightly prefer 2-row (faster)
    candidates[MatrixBasedDitherer.SierraLite] = sierraScore * 1.2;   // Even more prefer lite version
    
    // Burkes: Good for edge preservation
    var burkesScore = characteristics.EdgeDensity * 0.8 + characteristics.DetailLevel * 0.4;
    candidates[MatrixBasedDitherer.Burkes] = burkesScore;
    
    // Stevenson-Arce: High quality but slow
    var stevensonScore = characteristics.ColorComplexity * 0.6 + characteristics.DetailLevel * 0.5;
    if (characteristics.ImageSize > 500000) stevensonScore *= 0.6; // Heavy penalty for large images
    candidates[MatrixBasedDitherer.StevensonArce] = stevensonScore;
    
    // Simple variants for speed
    if (characteristics.ImageSize > 2000000) {
      var simpleScore = 0.8 + (1.0 - characteristics.ColorComplexity) * 0.3;
      candidates[MatrixBasedDitherer.Simple] = simpleScore;
      candidates[MatrixBasedDitherer.FalseFloydSteinberg] = simpleScore * 0.9;
    }

    // Ordered dithering: Fast, good for gradients
    var orderScore = characteristics.GradientSmoothness * 0.8 +
                     (1.0 - characteristics.ColorComplexity) * 0.2;
    candidates[OrderedDitherer.Bayer8x8] = orderScore;

    // Blue noise: Good for natural images
    var noiseScore = characteristics.NoiseLevel * 0.5 +
                     characteristics.ColorComplexity * 0.3 +
                     (1.0 - characteristics.EdgeDensity) * 0.2;
    candidates[NoiseDitherer.Blue] = noiseScore;

    // Knoll: High quality for complex images
    var knollScore = characteristics.ColorComplexity * 0.4 +
                     characteristics.DetailLevel * 0.3 +
                     characteristics.GradientSmoothness * 0.3;
    // Penalize for large images (performance cost)
    if (characteristics.ImageSize > 500000) knollScore *= 0.7;
    candidates[KnollDitherer.Default] = knollScore;

    // N-Closest: Good for color mixing
    var nClosestScore = characteristics.ColorComplexity * 0.6 +
                        (1.0 - characteristics.NoiseLevel) * 0.4;
    candidates[NClosestDitherer.Default] = nClosestScore;

    // Riemersma: Good for avoiding directional artifacts
    var riemersmaScore = characteristics.DetailLevel * 0.5 +
                         characteristics.EdgeDensity * 0.3 +
                         (1.0 - characteristics.NoiseLevel) * 0.2;
    candidates[RiemersmaDitherer.Default] = riemersmaScore;

    // Select the highest scoring algorithm
    return candidates.OrderByDescending(kvp => kvp.Value).First().Key;
  }
}
