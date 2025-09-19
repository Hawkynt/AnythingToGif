using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

/// <summary>
/// REVOLUTIONARY: Adaptive matrix scaling with edge-aware diffusion blocking!
/// Dynamically stretches error diffusion matrices based on local content.
/// 🧠 THE FUTURE OF ERROR DIFFUSION! 🚀
/// </summary>
public readonly struct AdaptiveMatrixDitherer(AdaptiveConfig? config = null) : IDitherer {
  
  private readonly AdaptiveConfig _config = config ?? AdaptiveConfig.Default;

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var stride = target.Stride;
    var targetData = (byte*)target.Scan0;
    
    using var analyzableBitmap = CreateAnalyzableBitmap(source);
    var contentMap = ContentAnalyzer.AnalyzeImage(analyzableBitmap);
    var edgeMap = this.CreateEdgeMap(analyzableBitmap);
    var gradientMap = this.CreateGradientDirectionMap(analyzableBitmap);
    this.ApplyAdaptiveErrorDiffusion(source, targetData, stride, palette, contentMap, edgeMap, gradientMap, colorDistanceMetric);
  }

  private static Bitmap CreateAnalyzableBitmap(BitmapExtensions.IBitmapLocker source) {
    var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
    using var locker = bitmap.Lock();
    for (var y = 0; y < source.Height; ++y)
    for (var x = 0; x < source.Width; ++x)
      locker[x, y] = source[x, y];
    
    return bitmap;
  }

  private unsafe bool[,] CreateEdgeMap(Bitmap image) {
    var width = image.Width;
    var height = image.Height;
    var edgeMap = new bool[width, height];

    using var locker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    var bitmapData = locker.BitmapData;
    var ptr = (byte*)bitmapData.Scan0;
    var stride = bitmapData.Stride;

    // Enhanced edge detection with multiple scales
    for (var y = 1; y < height - 1; ++y)
    for (var x = 1; x < width - 1; ++x) {
      var edgeStrength = this.CalculateMultiScaleEdgeStrength(ptr, stride, x, y, width, height);
      edgeMap[x, y] = edgeStrength > this._config.EdgeThreshold;
    }

    return edgeMap;
  }

  private unsafe double[,] CreateGradientDirectionMap(Bitmap image) {
    var width = image.Width;
    var height = image.Height;
    var gradientMap = new double[width, height];

    using var locker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    var bitmapData = locker.BitmapData;

    var ptr = (byte*)bitmapData.Scan0;
    var stride = bitmapData.Stride;
    
    for (var y = 1; y < height - 1; ++y)
    for (var x = 1; x < width - 1; ++x)
      gradientMap[x, y] = this.CalculateGradientDirection(ptr, stride, x, y, width, height);
    
    return gradientMap;
  }

  private unsafe double CalculateMultiScaleEdgeStrength(byte* imageData, int stride, int x, int y, int width, int height) {
    var totalEdgeStrength = 0.0;
    
    // Multiple scales for better edge detection
    var scales = new[] { 1, 2, 3 };
    foreach (var scale in scales) {
      // Proper bounds check to prevent out-of-bounds access
      if (x < scale || y < scale || x >= width - scale || y >= height - scale)
        continue; // Skip this scale if it would go out of bounds

      var edgeStrength = this.CalculateEdgeStrengthAtScale(imageData, stride, x, y, scale, width, height);
      totalEdgeStrength += edgeStrength / (scale * scale); // Weight by scale
    }
    
    return totalEdgeStrength;
  }

  private unsafe double CalculateEdgeStrengthAtScale(byte* imageData, int stride, int x, int y, int scale, int width, int height) {
    // Sobel operators at different scales
    var gx = 0.0;
    var gy = 0.0;
    
    for (var dy = -scale; dy <= scale; ++dy)
    for (var dx = -scale; dx <= scale; ++dx) {
      var newX = x + dx;
      var newY = y + dy;
      
      // Bounds check to prevent out-of-bounds access
      if (newX < 0 || newX >= width || newY < 0 || newY >= height)
        continue; // Skip out-of-bounds pixels
      
      var pixel = this.GetGrayscaleAt(imageData, stride, newX, newY);
      
      // Sobel weights (simplified for different scales)
      var sobelX = dx == 0 ? 0 : (dx > 0 ? 1 : -1) * (Math.Abs(dy) == scale ? 1 : 2);
      var sobelY = dy == 0 ? 0 : (dy > 0 ? 1 : -1) * (Math.Abs(dx) == scale ? 1 : 2);
      
      gx += pixel * sobelX;
      gy += pixel * sobelY;
    }
    
    return Math.Sqrt(gx * gx + gy * gy);
  }

  private unsafe double CalculateGradientDirection(byte* imageData, int stride, int x, int y, int width, int height) {
    // Calculate gradient direction using Sobel
    var gx = 0.0;
    var gy = 0.0;
    
    for (var dy = -1; dy <= 1; ++dy)
    for (var dx = -1; dx <= 1; ++dx) {
      var newX = x + dx;
      var newY = y + dy;
      
      // Bounds check to prevent out-of-bounds access
      if (newX < 0 || newX >= width || newY < 0 || newY >= height)
        continue; // Skip out-of-bounds pixels
      
      var pixel = this.GetGrayscaleAt(imageData, stride, newX, newY);
      
      // Sobel operators
      var sobelX = dx * (Math.Abs(dy) == 1 ? 1 : 2);
      var sobelY = dy * (Math.Abs(dx) == 1 ? 1 : 2);
      
      gx += pixel * sobelX;
      gy += pixel * sobelY;
    }
  
    return Math.Atan2(gy, gx); // Return angle in radians
  }

  private unsafe int GetGrayscaleAt(byte* imageData, int stride, int x, int y) {
    var pixelPtr = imageData + y * stride + x * 3;
    return (pixelPtr[0] + pixelPtr[1] + pixelPtr[2]) / 3;
  }

  private unsafe void ApplyAdaptiveErrorDiffusion(
    BitmapExtensions.IBitmapLocker source,
    byte* targetData,
    int stride,
    IReadOnlyList<Color> palette,
    DitheringStrategy[,] contentMap,
    bool[,] edgeMap,
    double[,] gradientMap,
    Func<Color, Color, int>? colorDistanceMetric) {

    var width = source.Width;
    var height = source.Height;
    var errors = new RgbError[width, height];
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var strategyStats = new Dictionary<string, int>();

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var oldColor = source[x, y];

      // Apply accumulated error
      var rgbError = errors[x, y];
      var correctedColor = Color.FromArgb(
        Clamp(rgbError.red + oldColor.R),
        Clamp(rgbError.green + oldColor.G),
        Clamp(rgbError.blue + oldColor.B)
      );

      var closestColorIndex = (byte)wrapper.FindClosestColorIndex(correctedColor);
      var newColor = palette[closestColorIndex];
      targetData[y * stride + x] = closestColorIndex;

      // Calculate quantization error
      var quantError = new RgbError {
        red = (short)(correctedColor.R - newColor.R),
        green = (short)(correctedColor.G - newColor.G),
        blue = (short)(correctedColor.B - newColor.B)
      };

      // THE MAGIC: Adaptive matrix scaling!
      var strategy = contentMap[x, y];
      var isNearEdge = x < width - 1 && y < height - 1 && edgeMap[x, y];
      var gradientDirection = x < width - 1 && y < height - 1 ? gradientMap[x, y] : 0;

      var adaptiveMatrix = this.CreateAdaptiveMatrix(strategy, isNearEdge, gradientDirection);

      // Apply error diffusion with adaptive matrix and edge blocking
      this.DistributeAdaptiveError(errors, quantError, x, y, width, height, adaptiveMatrix, edgeMap);

      // Track statistics
      var statsKey = $"{strategy}_{(isNearEdge ? "Edge" : "Normal")}";
      strategyStats[statsKey] = strategyStats.GetValueOrDefault(statsKey, 0) + 1;
    }

    PrintAdaptiveStatistics(strategyStats, width * height);
  }

  private AdaptiveMatrix CreateAdaptiveMatrix(DitheringStrategy strategy, bool isNearEdge, double gradientDirection) {
    var baseMatrix = this._config.BaseMatrix;
    
    return strategy switch {
      DitheringStrategy.SmoothGradient => this.CreateGradientAdaptedMatrix(baseMatrix, gradientDirection, isNearEdge),
      DitheringStrategy.StructurePreserving => this.CreateEdgePreservingMatrix(baseMatrix, isNearEdge),
      DitheringStrategy.DetailEnhancing => this.CreateDetailEnhancingMatrix(baseMatrix, gradientDirection),
      DitheringStrategy.ExtremeLuminance => this.CreateLuminanceAdaptedMatrix(baseMatrix, isNearEdge),
      _ => this.CreateBalancedMatrix(baseMatrix, isNearEdge)
    };
  }

  private AdaptiveMatrix CreateGradientAdaptedMatrix(ErrorDiffusionMatrix baseMatrix, double gradientDirection, bool isNearEdge) {
    // For gradients: stretch ALONG gradient direction, compress ACROSS it
    var stretchFactor = isNearEdge ? this._config.EdgeStretchFactor : this._config.GradientStretchFactor;
    
    var matrix = new AdaptiveMatrix {
      ScaleX = Math.Abs(Math.Cos(gradientDirection)) * stretchFactor + 1.0,
      ScaleY = Math.Abs(Math.Sin(gradientDirection)) * stretchFactor + 1.0,
      Rotation = gradientDirection,
      Coefficients = baseMatrix.Coefficients,
      BlockAcrossEdges = isNearEdge
    };
    
    return matrix;
  }

  private AdaptiveMatrix CreateEdgePreservingMatrix(ErrorDiffusionMatrix baseMatrix, bool isNearEdge) =>
    // For edges: COMPRESS matrix, BLOCK diffusion across edges
    new() {
      ScaleX = this._config.EdgeCompressionFactor,
      ScaleY = this._config.EdgeCompressionFactor,
      Rotation = 0,
      Coefficients = this.ReduceCoefficients(baseMatrix.Coefficients, 0.7f),
      BlockAcrossEdges = true // CRITICAL: Don't blur edges!
    };

  private AdaptiveMatrix CreateDetailEnhancingMatrix(ErrorDiffusionMatrix baseMatrix, double gradientDirection) =>
    // For textures: moderate stretch, boost coefficients for more aggressive diffusion
    new() {
      ScaleX = this._config.DetailStretchFactor,
      ScaleY = this._config.DetailStretchFactor,
      Rotation = 0,
      Coefficients = this.BoostCoefficients(baseMatrix.Coefficients, 1.3f),
      BlockAcrossEdges = false
    };

  private AdaptiveMatrix CreateLuminanceAdaptedMatrix(ErrorDiffusionMatrix baseMatrix, bool isNearEdge) =>
    // For extreme luminance: careful, conservative approach
    new() {
      ScaleX = isNearEdge ? 0.8 : 1.2,
      ScaleY = isNearEdge ? 0.8 : 1.2,
      Rotation = 0,
      Coefficients = baseMatrix.Coefficients,
      BlockAcrossEdges = isNearEdge
    };

  private AdaptiveMatrix CreateBalancedMatrix(ErrorDiffusionMatrix baseMatrix, bool isNearEdge) =>
    new() {
      ScaleX = isNearEdge ? 0.9 : 1.1,
      ScaleY = isNearEdge ? 0.9 : 1.1,
      Rotation = 0,
      Coefficients = baseMatrix.Coefficients,
      BlockAcrossEdges = isNearEdge
    };

  private float[] ReduceCoefficients(float[] coefficients, float factor) {
    var result = new float[coefficients.Length];
    for (var i = 0; i < coefficients.Length; ++i)
      result[i] = coefficients[i] * factor;

    return result;
  }

  private float[] BoostCoefficients(float[] coefficients, float factor) {
    var result = new float[coefficients.Length];
    for (var i = 0; i < coefficients.Length; ++i)
      result[i] = coefficients[i] * factor;

    return result;
  }

  private void DistributeAdaptiveError(RgbError[,] errors, RgbError error, int x, int y, int width, int height, AdaptiveMatrix matrix, bool[,] edgeMap) {
    // Apply the adaptive matrix with edge-aware blocking!
    
    var positions = this.CalculateAdaptivePositions(matrix);
    foreach (var (dx, dy, coefficient) in positions) {
      var newX = x + dx;
      var newY = y + dy;
      
      // Bounds check
      if (newX < 0 || newX >= width || newY < 0 || newY >= height) continue;
      
      // EDGE-AWARE BLOCKING: Don't diffuse across edges!
      if (matrix.BlockAcrossEdges && this.WouldCrossEdge(x, y, newX, newY, edgeMap))
        continue; // BLOCKED! Keep edges sharp!
      
      // Apply error with adaptive coefficient
      var scaledError = new RgbError {
        red = (short)(error.red * coefficient),
        green = (short)(error.green * coefficient),
        blue = (short)(error.blue * coefficient)
      };
      
      errors[newX, newY].Add(scaledError);
    }
  }

  private List<(int dx, int dy, float coefficient)> CalculateAdaptivePositions(AdaptiveMatrix matrix) {
    // Transform the base matrix positions using scaling and rotation
    var positions = new List<(int, int, float)>();
    
    // For now, simplified - just apply scaling
    // TODO: Add rotation support for gradient-aligned diffusion
    
    var basePositions = this.GetBaseMatrixPositions();
    
    foreach (var (dx, dy, coeff) in basePositions) {
      var scaledDx = (int)Math.Round(dx * matrix.ScaleX);
      var scaledDy = (int)Math.Round(dy * matrix.ScaleY);
      if (scaledDx != 0 || scaledDy != 0)
        positions.Add((scaledDx, scaledDy, coeff * matrix.Coefficients[0]));
    }
    
    return positions;
  }

  private List<(int dx, int dy, float coefficient)> GetBaseMatrixPositions() {
    // Floyd-Steinberg base pattern
    return new List<(int, int, float)> {
      (1, 0, 7.0f/16.0f),  // Right
      (-1, 1, 3.0f/16.0f), // Below-left
      (0, 1, 5.0f/16.0f),  // Below
      (1, 1, 1.0f/16.0f)   // Below-right
    };
  }

  private bool WouldCrossEdge(int x1, int y1, int x2, int y2, bool[,] edgeMap) {
    // Check if diffusion would cross an edge
    // Use simple line check - could be more sophisticated
    
    var dx = Math.Abs(x2 - x1);
    var dy = Math.Abs(y2 - y1);
    
    if (dx <= 1 && dy <= 1)
      // Adjacent pixels - check if either is an edge
      return edgeMap[x1, y1] || edgeMap[x2, y2];
    
    // For longer distances, check intermediate points
    var steps = Math.Max(dx, dy);
    for (var i = 1; i < steps; ++i) {
      var checkX = x1 + (x2 - x1) * i / steps;
      var checkY = y1 + (y2 - y1) * i / steps;
      
      if (checkX >= 0 && checkX < edgeMap.GetLength(0) && 
          checkY >= 0 && checkY < edgeMap.GetLength(1) &&
          edgeMap[checkX, checkY])
        return true; // Found edge in path
    }
    
    return false;
  }

  private static int Clamp(int value) {
    return Math.Max(0, Math.Min(255, value));
  }

  private static void PrintAdaptiveStatistics(Dictionary<string, int> stats, int totalPixels) {
    Console.WriteLine("📊 ADAPTIVE MATRIX STATISTICS:");
    
    foreach (var (strategy, count) in stats) {
      var percentage = (double)count / totalPixels * 100;
      Console.WriteLine($"   {strategy}: {percentage:F1}% ({count:N0} pixels)");
    }
  }

  // Static factory methods
  public static AdaptiveMatrixDitherer Default => new(AdaptiveConfig.Default);
  public static AdaptiveMatrixDitherer Aggressive => new(AdaptiveConfig.Aggressive);
  public static AdaptiveMatrixDitherer Conservative => new(AdaptiveConfig.Conservative);
}

// Supporting data structures for the SCIENCE! 🧬

public struct AdaptiveMatrix {
  public double ScaleX;
  public double ScaleY;
  public double Rotation;
  public float[] Coefficients;
  public bool BlockAcrossEdges;
}

public struct ErrorDiffusionMatrix {
  public float[] Coefficients;
}

public struct RgbError {
  public short red;
  public short green;
  public short blue;
  
  public void Add(RgbError other) {
    this.red += other.red;
    this.green += other.green;
    this.blue += other.blue;
  }
}

public class AdaptiveConfig {
  public ErrorDiffusionMatrix BaseMatrix { get; set; }
  public double EdgeThreshold { get; set; } = 25.0;
  public double GradientStretchFactor { get; set; } = 2.0;
  public double EdgeStretchFactor { get; set; } = 0.5;
  public double EdgeCompressionFactor { get; set; } = 0.7;
  public double DetailStretchFactor { get; set; } = 1.5;
  
  public static AdaptiveConfig Default => new() {
    BaseMatrix = new ErrorDiffusionMatrix { Coefficients = [1.0f] }
  };
  
  public static AdaptiveConfig Aggressive => new() {
    BaseMatrix = new ErrorDiffusionMatrix { Coefficients = [1.0f] },
    GradientStretchFactor = 3.0,
    EdgeCompressionFactor = 0.5,
    DetailStretchFactor = 2.0
  };
  
  public static AdaptiveConfig Conservative => new() {
    BaseMatrix = new ErrorDiffusionMatrix { Coefficients = [1.0f] },
    GradientStretchFactor = 1.5,
    EdgeCompressionFactor = 0.8,
    DetailStretchFactor = 1.2
  };
}