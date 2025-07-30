using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

// Non-generic wrapper for backward compatibility
public readonly struct ADitherer {

  public static IDitherer XorY149 { get; } = new Implementation<XorY149MaskCalculator>(new XorY149MaskCalculator());
  public static IDitherer XorY149WithChannel { get; } = new Implementation<XorY149WithChannelMaskCalculator>(new XorY149WithChannelMaskCalculator());
  public static IDitherer XYArithmetic { get; } = new Implementation<XYArithmeticMaskCalculator>(new XYArithmeticMaskCalculator());
  public static IDitherer XYArithmeticWithChannel { get; } = new Implementation<XYArithmeticWithChannelMaskCalculator>(new XYArithmeticWithChannelMaskCalculator());
  public static IDitherer Uniform { get; } = new Implementation<UniformMaskCalculator>(new UniformMaskCalculator());

  private readonly struct Implementation<TMaskCalculator>(TMaskCalculator maskCalculator) : IDitherer
    where TMaskCalculator : struct, IMaskCalculator {

    public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
      var width = source.Width;
      var height = source.Height;
      var stride = target.Stride;
      var data = (byte*)target.Scan0;
      var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

      const double levels = 256.0;
      const double invLevels = 1.0 / 256.0;
      const double inv255 = 1.0 / 255.0;

      var yMultiplier = maskCalculator.YMultiplier;
      var useChannels = maskCalculator.UseChannels;

      // Hot loop with struct-based mask calculation
      for (var y = 0; y < height; ++y) {
        var offset = y * stride;
        var yMultiplied = y * yMultiplier;

        for (var x = 0; x < width; ++offset, ++x) {
          var originalColor = source[x, y];

          double r, g, b;

          if (useChannels) {
            maskCalculator.CalculateChannelMasks(x, yMultiplied, out var rMask, out var gMask, out var bMask);
            r = (int)Math.Floor(levels * (originalColor.R * inv255) + rMask) * invLevels * 255.0;
            g = (int)Math.Floor(levels * (originalColor.G * inv255) + gMask) * invLevels * 255.0;
            b = (int)Math.Floor(levels * (originalColor.B * inv255) + bMask) * invLevels * 255.0;
          } else {
            var mask = maskCalculator.CalculateMask(x, yMultiplied);
            r = (int)Math.Floor(levels * (originalColor.R * inv255) + mask) * invLevels * 255.0;
            g = (int)Math.Floor(levels * (originalColor.G * inv255) + mask) * invLevels * 255.0;
            b = (int)Math.Floor(levels * (originalColor.B * inv255) + mask) * invLevels * 255.0;
          }

          var ditheredColor = Color.FromArgb(
            Math.Max(0, Math.Min(255, (int)r)),
            Math.Max(0, Math.Min(255, (int)g)),
            Math.Max(0, Math.Min(255, (int)b))
          );

          var closestColorIndex = (byte)wrapper.FindClosestColorIndex(ditheredColor);
          data[offset] = closestColorIndex;
        }
      }
    }
  }

  // Interface for mask calculators
  private interface IMaskCalculator {
    int YMultiplier { get; }
    bool UseChannels { get; }
    double CalculateMask(int x, int yMultiplied);
    void CalculateChannelMasks(int x, int yMultiplied, out double rMask, out double gMask, out double bMask);
  }

  // XorY149 mask calculator
  private readonly struct XorY149MaskCalculator : IMaskCalculator {
    public int YMultiplier => 149;
    public bool UseChannels => false;

    public double CalculateMask(int x, int yMultiplied) => ((x ^ yMultiplied) * 1234 & 511) * (1.0 / 511.0);

    public void CalculateChannelMasks(int x, int yMultiplied, out double rMask, out double gMask, out double bMask) =>
      // Not used for single channel
      rMask = gMask = bMask = this.CalculateMask(x, yMultiplied);
  }

  // XorY149WithChannel mask calculator
  private readonly struct XorY149WithChannelMaskCalculator : IMaskCalculator {
    public int YMultiplier => 149;
    public bool UseChannels => true;

    public double CalculateMask(int x, int yMultiplied) =>
      // Not used for multi-channel
      ((x ^ yMultiplied) * 1234 & 511) * (1.0 / 511.0);

    public void CalculateChannelMasks(int x, int yMultiplied, out double rMask, out double gMask, out double bMask) {
      const double inv511 = 1.0 / 511.0;
      rMask = (((x + 0 * 17) ^ yMultiplied) * 1234 & 511) * inv511;
      gMask = (((x + 1 * 17) ^ yMultiplied) * 1234 & 511) * inv511;
      bMask = (((x + 2 * 17) ^ yMultiplied) * 1234 & 511) * inv511;
    }
  }

  // XYArithmetic mask calculator
  private readonly struct XYArithmeticMaskCalculator : IMaskCalculator {
    public int YMultiplier => 237;
    public bool UseChannels => false;

    public double CalculateMask(int x, int yMultiplied) => ((x + yMultiplied) * 119 & 255) * (1.0 / 255.0);

    public void CalculateChannelMasks(int x, int yMultiplied, out double rMask, out double gMask, out double bMask) =>
      // Not used for single channel
      rMask = gMask = bMask = this.CalculateMask(x, yMultiplied);
  }

  // XYArithmeticWithChannel mask calculator
  private readonly struct XYArithmeticWithChannelMaskCalculator : IMaskCalculator {
    public int YMultiplier => 236;
    public bool UseChannels => true;

    public double CalculateMask(int x, int yMultiplied) =>
      // Not used for multi-channel
      ((x + yMultiplied) * 119 & 255) * (1.0 / 255.0);

    public void CalculateChannelMasks(int x, int yMultiplied, out double rMask, out double gMask, out double bMask) {
      const double inv255 = 1.0 / 255.0;
      rMask = (((x + 0 * 67) + yMultiplied) * 119 & 255) * inv255;
      gMask = (((x + 1 * 67) + yMultiplied) * 119 & 255) * inv255;
      bMask = (((x + 2 * 67) + yMultiplied) * 119 & 255) * inv255;
    }
  }

  // Uniform mask calculator
  private readonly struct UniformMaskCalculator : IMaskCalculator {
    public int YMultiplier => 0;
    public bool UseChannels => false;

    public double CalculateMask(int x, int yMultiplied) => 0.5;

    public void CalculateChannelMasks(int x, int yMultiplied, out double rMask, out double gMask, out double bMask) =>
      // Not used for single channel
      rMask = gMask = bMask = 0.5;

  }

}