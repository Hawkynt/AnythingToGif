using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly struct ADitherer : IDitherer {
  
  public enum Pattern {
    XorY149,
    XorY149WithChannel,
    XYArithmetic,
    XYArithmeticWithChannel,
    Uniform
  }

  private readonly Pattern _pattern;
  
  private ADitherer(Pattern pattern) {
    this._pattern = pattern;
  }

  public static IDitherer XorY149 { get; } = new ADitherer(Pattern.XorY149);
  public static IDitherer XorY149WithChannel { get; } = new ADitherer(Pattern.XorY149WithChannel);
  public static IDitherer XYArithmetic { get; } = new ADitherer(Pattern.XYArithmetic);
  public static IDitherer XYArithmeticWithChannel { get; } = new ADitherer(Pattern.XYArithmeticWithChannel);
  public static IDitherer Uniform { get; } = new ADitherer(Pattern.Uniform);

  // Delegate types for the different mask calculation patterns
  private delegate double MaskCalculator(int x, int y149_or_y237_or_y236);
  private delegate void ChannelMaskCalculator(int x, int y149_or_y237_or_y236, out double rMask, out double gMask, out double bMask);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    const double levels = 256.0;
    const double invLevels = 1.0 / 256.0;
    const double inv255 = 1.0 / 255.0;

    // Assign the appropriate delegate based on pattern using switch expression
    var (singleMaskCalc, channelMaskCalc, yMultiplier, useChannels) = this._pattern switch {
      Pattern.XorY149 => (
        (MaskCalculator)((x, y149) => ((x ^ y149) * 1234 & 511) * (1.0 / 511.0)),
        (ChannelMaskCalculator?)null,
        149, 
        false
      ),
      Pattern.XorY149WithChannel => (
        (MaskCalculator?)null,
        (ChannelMaskCalculator)((int x, int y149, out double r, out double g, out double b) => {
          const double inv511 = 1.0 / 511.0;
          r = (((x + 0 * 17) ^ y149) * 1234 & 511) * inv511;
          g = (((x + 1 * 17) ^ y149) * 1234 & 511) * inv511;
          b = (((x + 2 * 17) ^ y149) * 1234 & 511) * inv511;
        }),
        149,
        true
      ),
      Pattern.XYArithmetic => (
        (MaskCalculator)((x, y237) => ((x + y237) * 119 & 255) * (1.0 / 255.0)),
        (ChannelMaskCalculator?)null,
        237,
        false
      ),
      Pattern.XYArithmeticWithChannel => (
        (MaskCalculator?)null,
        (ChannelMaskCalculator)((int x, int y236, out double r, out double g, out double b) => {
          const double inv255Pattern = 1.0 / 255.0;
          r = (((x + 0 * 67) + y236) * 119 & 255) * inv255Pattern;
          g = (((x + 1 * 67) + y236) * 119 & 255) * inv255Pattern;
          b = (((x + 2 * 67) + y236) * 119 & 255) * inv255Pattern;
        }),
        236,
        true
      ),
      Pattern.Uniform => (
        (MaskCalculator)((x, y) => 0.5),
        (ChannelMaskCalculator?)null,
        0,
        false
      ),
      _ => throw new ArgumentOutOfRangeException()
    };

    // Hot loop with pre-assigned delegates
    for (var y = 0; y < height; ++y) {
      var offset = y * stride;
      var yMultiplied = y * yMultiplier;
      
      for (var x = 0; x < width; ++offset, ++x) {
        var originalColor = source[x, y];

        double r, g, b;
        
        if (useChannels) {
          channelMaskCalc!(x, yMultiplied, out var rMask, out var gMask, out var bMask);
          r = (int)Math.Floor(levels * (originalColor.R * inv255) + rMask) * invLevels * 255.0;
          g = (int)Math.Floor(levels * (originalColor.G * inv255) + gMask) * invLevels * 255.0;
          b = (int)Math.Floor(levels * (originalColor.B * inv255) + bMask) * invLevels * 255.0;
        } else {
          var mask = singleMaskCalc!(x, yMultiplied);
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