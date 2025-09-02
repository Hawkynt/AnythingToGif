using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

/// <summary>
/// Ostromoukhov variable-coefficient error diffusion dithering.
/// Based on Victor Ostromoukhov's algorithm that varies the dithering kernel 
/// based on the current pixel value to achieve blue-noise characteristics.
/// </summary>
public readonly record struct OstromoukhovDitherer : IDitherer {

  private readonly (int a, int b, int c)[] _coefficients;

  public static readonly OstromoukhovDitherer Instance = new();

  public OstromoukhovDitherer() {
    // Ostromoukhov's variable coefficients table (256 entries)
    // Each entry contains coefficients (a, b, c) for error distribution
    // where a + b + c = 16 for normalization
    this._coefficients = new (int, int, int)[256];
    this.InitializeCoefficients();
  }

  private void InitializeCoefficients() {
    // Ostromoukhov's optimized coefficients for blue noise dithering
    // This is a simplified version - the full table would have 256 entries
    // For now, using a representative subset with interpolation
    var baseCoefficients = new (int value, int a, int b, int c)[] {
      (0, 13, 0, 5),    (25, 13, 0, 5),   (51, 21, 0, 10),  (76, 7, 0, 4),
      (102, 8, 0, 5),   (127, 47, 3, 28), (153, 23, 3, 13), (178, 15, 3, 8),
      (204, 22, 6, 11), (229, 16, 5, 7),  (255, 9, 4, 4)
    };

    // Fill the coefficients array with interpolation
    for (var i = 0; i < 256; ++i) {
      // Find the two nearest base coefficients
      int lower = 0, upper = baseCoefficients.Length - 1;
      for (var j = 0; j < baseCoefficients.Length - 1; ++j) {
        if (i > baseCoefficients[j + 1].value)
          continue;

        lower = j;
        upper = j + 1;
        break;
      }

      var lowerCoeff = baseCoefficients[lower];
      var upperCoeff = baseCoefficients[upper];

      if (lowerCoeff.value == upperCoeff.value)
        this._coefficients[i] = (lowerCoeff.a, lowerCoeff.b, lowerCoeff.c);
      else {
        var t = (float)(i - lowerCoeff.value) / (upperCoeff.value - lowerCoeff.value);
        var a = (int)(lowerCoeff.a + t * (upperCoeff.a - lowerCoeff.a));
        var b = (int)(lowerCoeff.b + t * (upperCoeff.b - lowerCoeff.b));
        var c = (int)(lowerCoeff.c + t * (upperCoeff.c - lowerCoeff.c));
        this._coefficients[i] = (a, b, c);
      }
    }
  }

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);
    var errorR = new float[width, height];
    var errorG = new float[width, height];
    var errorB = new float[width, height];

    for (var y = 0; y < height; ++y) {
      var rightToLeft = (y & 1) == 1; // Serpentine scanning

      for (var i = 0; i < width; ++i) {
        var x = rightToLeft ? width - 1 - i : i;
        var pixel = source[x, y];

        // Add accumulated error
        var newR = Math.Clamp(pixel.R + errorR[x, y], 0, 255);
        var newG = Math.Clamp(pixel.G + errorG[x, y], 0, 255);
        var newB = Math.Clamp(pixel.B + errorB[x, y], 0, 255);

        var newColor = Color.FromArgb(pixel.A, (int)newR, (int)newG, (int)newB);
        
        // Find closest palette color
        var closestIndex = wrapper.FindClosestColorIndex(newColor);
        var closestColor = palette[closestIndex];
        data[y * stride + x] = (byte)closestIndex;

        // Calculate error
        var errR = newR - closestColor.R;
        var errG = newG - closestColor.G;
        var errB = newB - closestColor.B;

        if (errR == 0 && errG == 0 && errB == 0)
          continue;

        // Get Ostromoukhov coefficients based on luminance
        var luminance = (int)(0.299f * newR + 0.587f * newG + 0.114f * newB);
        var (a, b, c) = this._coefficients[luminance];
        float sum = a + b + c;

        if (sum == 0)
          continue;

        // Distribute error using variable coefficients
        // Error distribution pattern (serpentine-aware):
        //     X   a
        // b   c
        var dx1 = rightToLeft ? -1 : 1;
        var dx2 = rightToLeft ? 1 : -1;
        var dx3 = rightToLeft ? 0 : 0;

        // Right/Left (depending on direction)
        if (x + dx1 >= 0 && x + dx1 < width) {
          errorR[x + dx1, y] += errR * a / sum;
          errorG[x + dx1, y] += errG * a / sum;
          errorB[x + dx1, y] += errB * a / sum;
        }

        // Below left/right (depending on direction)
        if (y + 1 < height && x + dx2 >= 0 && x + dx2 < width) {
          errorR[x + dx2, y + 1] += errR * b / sum;
          errorG[x + dx2, y + 1] += errG * b / sum;
          errorB[x + dx2, y + 1] += errB * b / sum;
        }

        if (y + 1 >= height)
          continue;

        // Below
        errorR[x, y + 1] += errR * c / sum;
        errorG[x, y + 1] += errG * c / sum;
        errorB[x, y + 1] += errB * c / sum;
      }
    }
  }
}