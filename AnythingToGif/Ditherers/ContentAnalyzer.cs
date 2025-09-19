using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AnythingToGif.Ditherers;

/// <summary>
/// Analyzes image content to classify regions for intelligent dithering strategies.
/// This is where the SCIENCE happens! 🧠
/// </summary>
public static class ContentAnalyzer {

  /// <summary>
  /// Analyzes an image and returns a strategy map for different regions.
  /// Each pixel gets classified for optimal dithering approach.
  /// </summary>
  public static DitheringStrategy[,] AnalyzeImage(Bitmap image) {
    var width = image.Width;
    var height = image.Height;
    var strategies = new DitheringStrategy[width, height];

    // Lock the image for fast pixel access
    var rect = new Rectangle(0, 0, width, height);
    var bitmapData = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    
    try {
      unsafe {
        var ptr = (byte*)bitmapData.Scan0;
        var stride = bitmapData.Stride;

        // Analyze in blocks for efficiency
        const int blockSize = 8;
        
        for (int y = 0; y < height; y += blockSize) {
          for (int x = 0; x < width; x += blockSize) {
            var strategy = AnalyzeBlock(ptr, stride, x, y, 
              Math.Min(blockSize, width - x), 
              Math.Min(blockSize, height - y));
            
            // Fill the block with the determined strategy
            FillBlock(strategies, x, y, 
              Math.Min(blockSize, width - x), 
              Math.Min(blockSize, height - y), strategy);
          }
        }

        // Post-process for smoother transitions
        ApplyRegionSmoothing(strategies, width, height);
      }
    } finally {
      image.UnlockBits(bitmapData);
    }

    return strategies;
  }

  private static unsafe DitheringStrategy AnalyzeBlock(byte* imageData, int stride, int startX, int startY, int blockWidth, int blockHeight) {
    var edgeStrength = 0.0;
    var gradientSmoothness = 0.0;
    var colorVariance = 0.0;
    var avgBrightness = 0.0;
    
    var pixelCount = blockWidth * blockHeight;
    var colorSum = new { R = 0, G = 0, B = 0 };

    // Analyze each pixel in the block
    for (int y = 0; y < blockHeight; ++y) {
      for (int x = 0; x < blockWidth; ++x) {
        var pixelPtr = imageData + (startY + y) * stride + (startX + x) * 3;
        
        var b = pixelPtr[0];
        var g = pixelPtr[1]; 
        var r = pixelPtr[2];
        
        colorSum = new { R = colorSum.R + r, G = colorSum.G + g, B = colorSum.B + b };
        avgBrightness += (r + g + b) / 3.0;

        // Edge detection (simplified Sobel)
        if (x > 0 && y > 0 && x < blockWidth - 1 && y < blockHeight - 1) {
          edgeStrength += CalculateEdgeStrength(imageData, stride, startX + x, startY + y);
        }
      }
    }

    // Calculate metrics
    avgBrightness /= pixelCount;
    edgeStrength /= pixelCount;
    
    var avgColor = new { 
      R = colorSum.R / pixelCount, 
      G = colorSum.G / pixelCount, 
      B = colorSum.B / pixelCount 
    };

    // Calculate color variance for this block
    for (int y = 0; y < blockHeight; ++y) {
      for (int x = 0; x < blockWidth; ++x) {
        var pixelPtr = imageData + (startY + y) * stride + (startX + x) * 3;
        var r = pixelPtr[2] - avgColor.R;
        var g = pixelPtr[1] - avgColor.G;
        var b = pixelPtr[0] - avgColor.B;
        colorVariance += r * r + g * g + b * b;
      }
    }
    colorVariance = Math.Sqrt(colorVariance / pixelCount);

    // Determine strategy based on analysis
    return ClassifyRegion(edgeStrength, colorVariance, avgBrightness);
  }

  private static unsafe double CalculateEdgeStrength(byte* imageData, int stride, int x, int y) {
    // Simplified Sobel edge detection
    var pixelPtr = imageData + y * stride + x * 3;
    
    // Get surrounding pixels (grayscale approximation)
    var tl = GetGrayscale(pixelPtr - stride - 3);
    var tm = GetGrayscale(pixelPtr - stride);
    var tr = GetGrayscale(pixelPtr - stride + 3);
    var ml = GetGrayscale(pixelPtr - 3);
    var mr = GetGrayscale(pixelPtr + 3);
    var bl = GetGrayscale(pixelPtr + stride - 3);
    var bm = GetGrayscale(pixelPtr + stride);
    var br = GetGrayscale(pixelPtr + stride + 3);

    // Sobel operators
    var gx = -tl + tr - 2 * ml + 2 * mr - bl + br;
    var gy = -tl - 2 * tm - tr + bl + 2 * bm + br;
    
    return Math.Sqrt(gx * gx + gy * gy);
  }

  private static unsafe int GetGrayscale(byte* pixel) {
    // Fast grayscale approximation
    return (pixel[2] + pixel[1] + pixel[0]) / 3;
  }

  private static DitheringStrategy ClassifyRegion(double edgeStrength, double colorVariance, double avgBrightness) {
    // SCIENCE: The classification logic! 🧬
    
    // High edge strength = preserve structure
    if (edgeStrength > 30) {
      return DitheringStrategy.StructurePreserving;
    }
    
    // Low variance = smooth gradient
    if (colorVariance < 20) {
      return DitheringStrategy.SmoothGradient;
    }
    
    // High variance = complex texture
    if (colorVariance > 60) {
      return DitheringStrategy.DetailEnhancing;
    }
    
    // Very dark or very bright = special handling
    if (avgBrightness < 30 || avgBrightness > 220) {
      return DitheringStrategy.ExtremeLuminance;
    }
    
    // Default case
    return DitheringStrategy.Balanced;
  }

  private static void FillBlock(DitheringStrategy[,] strategies, int startX, int startY, int blockWidth, int blockHeight, DitheringStrategy strategy) {
    for (int y = startY; y < startY + blockHeight; ++y) {
      for (int x = startX; x < startX + blockWidth; ++x) {
        strategies[x, y] = strategy;
      }
    }
  }

  private static void ApplyRegionSmoothing(DitheringStrategy[,] strategies, int width, int height) {
    // Smooth transitions between regions to avoid harsh boundaries
    var smoothed = new DitheringStrategy[width, height];
    Array.Copy(strategies, smoothed, strategies.Length);

    const int radius = 2;
    
    for (int y = radius; y < height - radius; ++y) {
      for (int x = radius; x < width - radius; ++x) {
        var counts = new int[Enum.GetValues<DitheringStrategy>().Length];
        
        // Count strategies in neighborhood
        for (int dy = -radius; dy <= radius; ++dy) {
          for (int dx = -radius; dx <= radius; ++dx) {
            var strategy = strategies[x + dx, y + dy];
            counts[(int)strategy]++;
          }
        }
        
        // Find most common strategy
        var maxCount = 0;
        var dominantStrategy = DitheringStrategy.Balanced;
        
        for (int i = 0; i < counts.Length; ++i) {
          if (counts[i] > maxCount) {
            maxCount = counts[i];
            dominantStrategy = (DitheringStrategy)i;
          }
        }
        
        // Only change if there's a clear dominant strategy
        if (maxCount > (2 * radius + 1) * (2 * radius + 1) / 2) {
          smoothed[x, y] = dominantStrategy;
        }
      }
    }
    
    // Copy back the smoothed results
    Array.Copy(smoothed, strategies, strategies.Length);
  }
}

/// <summary>
/// Different dithering strategies for different types of image content.
/// Each strategy maps to optimal ditherer combinations.
/// </summary>
public enum DitheringStrategy {
  /// <summary>
  /// For edges, text, and structural elements - preserve sharpness
  /// </summary>
  StructurePreserving,
  
  /// <summary>
  /// For smooth gradients and sky - minimize visible patterns
  /// </summary>
  SmoothGradient,
  
  /// <summary>
  /// For complex textures - enhance detail and avoid loss
  /// </summary>
  DetailEnhancing,
  
  /// <summary>
  /// For very dark or very bright regions - special handling
  /// </summary>
  ExtremeLuminance,
  
  /// <summary>
  /// Balanced approach for mixed content
  /// </summary>
  Balanced
}