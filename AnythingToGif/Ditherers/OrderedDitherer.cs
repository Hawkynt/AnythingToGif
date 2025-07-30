using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly struct OrderedDitherer(byte[,] matrix) : IDitherer {

  /// <summary>
  /// Creates a Bayer dithering matrix of the specified size.
  /// Size must be a power of 2 (2, 4, 8, 16, 32, etc.).
  /// </summary>
  /// <param name="size">The size of the matrix (must be power of 2)</param>
  /// <returns>An OrderedDitherer with the generated Bayer matrix</returns>
  /// <exception cref="ArgumentException">Thrown when size is not a power of 2</exception>
  public static IDitherer CreateBayer(int size) {
    if (size < 2 || (size & (size - 1)) != 0)
      throw new ArgumentException("Size must be a power of 2 and at least 2", nameof(size));
    
    return new OrderedDitherer(GenerateBayerMatrix(size));
  }

  /// <summary>
  /// Generates a Bayer dithering matrix of the specified size using recursive construction.
  /// </summary>
  /// <param name="size">The size of the matrix (must be power of 2)</param>
  /// <returns>A 2D byte array containing the Bayer matrix</returns>
  private static byte[,] GenerateBayerMatrix(int size) {
    if (size == 2) {
      // Base case: 2x2 Bayer matrix
      return new byte[,] {
        { 0, 2 },
        { 3, 1 }
      };
    }
    
    var halfSize = size / 2;
    var smallMatrix = GenerateBayerMatrix(halfSize);
    var result = new byte[size, size];
    
    // Apply the recursive Bayer matrix construction formula:
    // B(2n) = [ 4*B(n) + 0*U(n)   4*B(n) + 2*U(n) ]
    //         [ 4*B(n) + 3*U(n)   4*B(n) + 1*U(n) ]
    for (var y = 0; y < halfSize; ++y) {
      for (var x = 0; x < halfSize; ++x) {
        var baseValue = (byte)(4 * smallMatrix[y, x]);
        
        // Top-left: 4*B(n) + 0
        result[y, x] = baseValue;
        
        // Top-right: 4*B(n) + 2  
        result[y, x + halfSize] = (byte)(baseValue + 2);
        
        // Bottom-left: 4*B(n) + 3
        result[y + halfSize, x] = (byte)(baseValue + 3);
        
        // Bottom-right: 4*B(n) + 1
        result[y + halfSize, x + halfSize] = (byte)(baseValue + 1);
      }
    }
    
    return result;
  }

  // Bayer 2x2 dither matrix
  public static IDitherer Bayer2x2 { get; } = CreateBayer(2);

  // Bayer 4x4 dither matrix  
  public static IDitherer Bayer4x4 { get; } = CreateBayer(4);

  // Bayer 8x8 dither matrix
  public static IDitherer Bayer8x8 { get; } = CreateBayer(8);

  // Halftone 8x8 matrix - creates clustered-dot pattern reminiscent of traditional photographic halftoning
  // Based on: https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/
  public static IDitherer Halftone8x8 { get; } = new OrderedDitherer(new byte[,] {
    { 24, 10, 12, 26, 35, 47, 49, 37 },
    { 8, 0, 2, 14, 45, 59, 61, 51 },
    { 22, 6, 4, 16, 43, 57, 63, 53 },
    { 30, 20, 18, 28, 33, 41, 55, 39 },
    { 34, 46, 48, 36, 25, 11, 13, 27 },
    { 44, 58, 60, 50, 9, 1, 3, 15 },
    { 42, 56, 62, 52, 23, 7, 5, 17 },
    { 32, 40, 54, 38, 31, 21, 19, 29 }
  });

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var matrixSize = matrix.GetLength(0);
    var maxThreshold = (float)matrixSize * matrixSize - 1;

    for (var y = 0; y < height; ++y) {
      var offset = y * stride;
      var matrixY = y % matrixSize;

      for (var x = 0; x < width; ++offset, ++x) {
        var originalColor = source[x, y];

        var threshold = matrix[matrixY, x % matrixSize];
        var normalizedThreshold = threshold / maxThreshold - 0.5f;
        var thresholdInt = (int)(normalizedThreshold * 255);

        var r = Math.Max(0, Math.Min(255, originalColor.R + thresholdInt));
        var g = Math.Max(0, Math.Min(255, originalColor.G + thresholdInt));
        var b = Math.Max(0, Math.Min(255, originalColor.B + thresholdInt));

        var ditheredColor = Color.FromArgb(r, g, b);
        var closestColorIndex = (byte)wrapper.FindClosestColorIndex(ditheredColor);
        data[offset] = closestColorIndex;
      }
    }
  }
}