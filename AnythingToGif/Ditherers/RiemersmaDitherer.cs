using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly struct RiemersmaDitherer : IDitherer {
  
  private readonly int _historySize;
  private readonly bool _useHilbertCurve;
  
  private RiemersmaDitherer(int historySize, bool useHilbertCurve) {
    this._historySize = historySize;
    this._useHilbertCurve = useHilbertCurve;
  }

  public static IDitherer Default { get; } = new RiemersmaDitherer(16, true);
  public static IDitherer Small { get; } = new RiemersmaDitherer(8, true);
  public static IDitherer Large { get; } = new RiemersmaDitherer(32, true);
  public static IDitherer Linear { get; } = new RiemersmaDitherer(16, false);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    // Handle edge case: empty palette
    if (palette.Count == 0) {
      // Fill with zeros (no colors available)
      var totalBytes = height * stride;
      for (var i = 0; i < totalBytes; ++i)
        data[i] = 0;
      return;
    }

    // Error history buffer - stores RGB errors
    var errorHistory = new (double r, double g, double b)[this._historySize];
    var historyIndex = 0;

    // Generate pixel traversal order
    var traversalOrder = this._useHilbertCurve 
      ? GenerateHilbertCurveOrder(width, height)
      : GenerateLinearOrder(width, height);

    foreach (var (x, y) in traversalOrder) {
      var originalColor = source[x, y];
      
      // Get accumulated error from history with exponential decay
      var totalErrorR = 0.0;
      var totalErrorG = 0.0;
      var totalErrorB = 0.0;
      
      // Apply errors with exponential decay (most recent errors have highest weight)
      for (var i = 0; i < this._historySize; ++i) {
        var index = (historyIndex - i - 1 + this._historySize) % this._historySize;
        // Exponential decay: newer errors (i=0) have weight ~1.0, older errors decay exponentially
        var weight = Math.Exp(-i * 0.1); // Decay factor of 0.1 per step
        totalErrorR += errorHistory[index].r * weight;
        totalErrorG += errorHistory[index].g * weight;
        totalErrorB += errorHistory[index].b * weight;
      }
      
      // Apply damping factor to prevent error accumulation from becoming too extreme
      var dampingFactor = 0.5;
      totalErrorR *= dampingFactor;
      totalErrorG *= dampingFactor;
      totalErrorB *= dampingFactor;

      // Apply error to original color
      var adjustedR = Math.Max(0, Math.Min(255, originalColor.R + totalErrorR));
      var adjustedG = Math.Max(0, Math.Min(255, originalColor.G + totalErrorG));
      var adjustedB = Math.Max(0, Math.Min(255, originalColor.B + totalErrorB));
      
      var adjustedColor = Color.FromArgb(
        (int)Math.Round(adjustedR),
        (int)Math.Round(adjustedG),
        (int)Math.Round(adjustedB)
      );

      // Find closest palette color
      var closestColorIndex = wrapper.FindClosestColorIndex(adjustedColor);
      var closestColor = palette[closestColorIndex];

      // Calculate quantization error based on original pixel, not adjusted pixel
      var errorR = originalColor.R - closestColor.R;
      var errorG = originalColor.G - closestColor.G;
      var errorB = originalColor.B - closestColor.B;

      // Store error in history buffer
      errorHistory[historyIndex] = (errorR, errorG, errorB);
      historyIndex = (historyIndex + 1) % this._historySize;

      // Write result to target bitmap
      var offset = y * stride + x;
      data[offset] = (byte)closestColorIndex;
    }
  }

  private static List<(int x, int y)> GenerateHilbertCurveOrder(int width, int height) {
    // Pre-allocate list with expected capacity for better performance
    var result = new List<(int, int)>(width * height);
    
    // Handle edge cases
    if (width <= 0 || height <= 0) return result;
    
    // Find the smallest power of 2 that contains both width and height
    var n = 1;
    while (n < Math.Max(width, height)) {
      n *= 2;
    }

    // Generate Hilbert curve iteratively to avoid stack overflow
    var totalPoints = n * n;
    for (var i = 0; i < totalPoints; ++i) {
      var (x, y) = HilbertIndexToXY(i, n);
      if (x < width && y < height) {
        result.Add((x, y));
      }
    }

    return result;
  }

  private static (int x, int y) HilbertIndexToXY(int index, int n) {
    int x = 0, y = 0;
    int t = index;
    int s = 1;

    while (s < n) {
      int rx = 1 & (t / 2);
      int ry = 1 & (t ^ rx);
      (x, y) = Rot(s, x, y, rx, ry);
      x += s * rx;
      y += s * ry;
      t /= 4;
      s *= 2;
    }

    return (x, y);
  }

  private static (int x, int y) Rot(int n, int x, int y, int rx, int ry) {
    if (ry == 0) {
      if (rx == 1) {
        x = n - 1 - x;
        y = n - 1 - y;
      }
      // Swap x and y
      (x, y) = (y, x);
    }
    return (x, y);
  }

  private static List<(int x, int y)> GenerateLinearOrder(int width, int height) {
    var result = new List<(int, int)>(width * height);
    
    for (var y = 0; y < height; ++y) {
      if ((y & 1) == 0) {
        // Even rows: left to right
        for (var x = 0; x < width; ++x) {
          result.Add((x, y));
        }
      } else {
        // Odd rows: right to left (serpentine pattern)
        for (var x = width - 1; x >= 0; --x) {
          result.Add((x, y));
        }
      }
    }
    
    return result;
  }
}