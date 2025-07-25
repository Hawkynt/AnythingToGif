using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly struct BayerDitherer : IDitherer {
  private readonly byte[,] _matrix;
  private readonly int _matrixSize;
  private readonly double _maxThreshold;

  private BayerDitherer(byte[,] matrix) {
    this._matrix = matrix;
    this._matrixSize = matrix.GetLength(0);
    this._maxThreshold = this._matrixSize * this._matrixSize - 1;
  }

  // Bayer 2x2 dither matrix
  public static IDitherer Bayer2x2 { get; } = new BayerDitherer(new byte[,] {
    { 0, 2 },
    { 3, 1 }
  });

  // Bayer 4x4 dither matrix
  public static IDitherer Bayer4x4 { get; } = new BayerDitherer(new byte[,] {
    { 0, 8, 2, 10 },
    { 12, 4, 14, 6 },
    { 3, 11, 1, 9 },
    { 15, 7, 13, 5 }
  });

  // Bayer 8x8 dither matrix
  public static IDitherer Bayer8x8 { get; } = new BayerDitherer(new byte[,] {
    { 0, 32, 8, 40, 2, 34, 10, 42 },
    { 48, 16, 56, 24, 50, 18, 58, 26 },
    { 12, 44, 4, 36, 14, 46, 6, 38 },
    { 60, 28, 52, 20, 62, 30, 54, 22 },
    { 3, 35, 11, 43, 1, 33, 9, 41 },
    { 51, 19, 59, 27, 49, 17, 57, 25 },
    { 15, 47, 7, 39, 13, 45, 5, 37 },
    { 63, 31, 55, 23, 61, 29, 53, 21 }
  });

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);
    
    var matrix = this._matrix;
    var matrixSize = this._matrixSize;
    var maxThreshold = this._maxThreshold;

    for (var y = 0; y < height; ++y) {
      var offset = y * stride;
      var matrixY = y % matrixSize;
      
      for (var x = 0; x < width; ++offset, ++x) {
        var originalColor = source[x, y];

        var threshold = matrix[matrixY, x % matrixSize];
        var normalizedThreshold = threshold / maxThreshold - 0.5;
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