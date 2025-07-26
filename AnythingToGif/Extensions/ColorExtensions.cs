using System;
using System.Drawing;

namespace AnythingToGif.Extensions;

internal static class ColorExtensions {

  private static class LowRed {
    public const int RedWeight = 2;
    public const int GreenWeight = 4;
    public const int BlueWeight = 3;
    public const int AlphaWeight = 1;
  }

  private static class HighRed {
    public const int RedWeight = 3;
    public const int GreenWeight = 4;
    public const int BlueWeight = 2;
    public const int AlphaWeight = 1;
  }

  private static class BT709 {
    public const int RedWeight = 2126;
    public const int GreenWeight = 7152;
    public const int BlueWeight = 722;
    public const int AlphaWeight = 10000;
    public const int Divisor = 10000;
  }

  // https://github.com/igor-bezkrovny/image-quantization/issues/4#issuecomment-235155320
  private static class Nommyde {
    public const int RedWeight = 4984;
    public const int GreenWeight = 8625;
    public const int BlueWeight = 2979;
    public const int AlphaWeight = 10000;
    public const int Divisor = 10000;
  }

  public static int EuclideanDistance(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return r * r + g * g + b * b + a * a;
  }

  public static int EuclideanBT709Distance(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return (BT709.RedWeight * r * r + BT709.GreenWeight * g * g + BT709.BlueWeight * b * b + BT709.AlphaWeight * a * a) / BT709.Divisor;
  }

  public static int EuclideanNommydeDistance(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return (Nommyde.RedWeight * r * r + Nommyde.GreenWeight * g * g + Nommyde.BlueWeight * b * b + Nommyde.AlphaWeight * a * a) / Nommyde.Divisor;
  }

  public static int WeightedEuclideanDistanceHighRed(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return HighRed.RedWeight * r * r + HighRed.GreenWeight * g * g + HighRed.BlueWeight * b * b + HighRed.AlphaWeight * a * a;
  }

  public static int WeightedEuclideanDistanceLowRed(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return LowRed.RedWeight * r * r + LowRed.GreenWeight * g * g + LowRed.BlueWeight * b * b + LowRed.AlphaWeight * a * a;
  }

  // https://www.compuphase.com/cmetric.htm
  public static int CompuPhaseDistance(this Color @this, Color other) {
    var r1 = @this.R;
    var g1 = @this.G;
    var b1 = @this.B;
    var a1 = @this.A;
    var r2 = other.R;
    var g2 = other.G;
    var b2 = other.B;
    var a2 = other.A;

    var rMean = r1 + r2;
    var r = r1 - r2;
    var g = g1 - g2;
    var b = b1 - b2;
    var a = a1 - a2;
    rMean >>= 1;
    r *= r;
    g *= g;
    b *= b;
    a *= a;
    var rb = 512 + rMean;
    var bb = 767 - rMean;
    g <<= 2;
    rb *= r;
    bb *= b;
    rb >>= 8;
    bb >>= 8;

    return rb + g + bb + a;
  }

  public static int ManhattanDistance(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return r.Abs() + g.Abs() + b.Abs() + a.Abs();
  }

  public static int ManhattanBT709Distance(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return (BT709.RedWeight * r.Abs() + BT709.GreenWeight * g.Abs() + BT709.BlueWeight * b.Abs() + BT709.AlphaWeight * a.Abs()) / BT709.Divisor;
  }

  public static int ManhattanNommydeDistance(this Color @this, Color other) {
    var r = @this.R - other.R;
    var g = @this.G - other.G;
    var b = @this.B - other.B;
    var a = @this.A - other.A;
    return (Nommyde.RedWeight * r.Abs() + Nommyde.GreenWeight * g.Abs() + Nommyde.BlueWeight * b.Abs() + Nommyde.AlphaWeight * a.Abs()) / Nommyde.Divisor;
  }

  /// <summary>
  /// PNGQuant color distance calculation - considers how colors appear when blended on black vs white backgrounds.
  /// Particularly effective for semi-transparent colors. Uses integer math for performance.
  /// Based on: https://github.com/pornel/pngquant/blob/cc39b47799a7ff2ef17b529f9415ff6e6b213b8f/lib/pam.h#L148
  /// </summary>
  public static int PNGQuantDistance(this Color @this, Color other) => PNGQuantDistance(@this, other, Color.White);

  /// <summary>
  /// PNGQuant color distance with configurable white point for channel weighting.
  /// White point values: higher = less important, lower = more important.
  /// Default Color.White gives equal weighting. Color.FromArgb(255,255,128,255) makes green 2x more important.
  /// </summary>
  public static int PNGQuantDistance(this Color @this, Color other, Color whitePoint) {
    // Calculate white point weights with 16-bit precision: (255 << 16) / whitePoint
    var wpR = whitePoint.R > 0 ? (255 << 16) / whitePoint.R : 0;
    var wpG = whitePoint.G > 0 ? (255 << 16) / whitePoint.G : 0;
    var wpB = whitePoint.B > 0 ? (255 << 16) / whitePoint.B : 0;
    var wpA = whitePoint.A > 0 ? (255 << 16) / whitePoint.A : 0;

    var r1 = @this.R;
    var g1 = @this.G;
    var b1 = @this.B;
    var a1 = @this.A;

    var r2 = other.R;
    var g2 = other.G;
    var b2 = other.B;
    var a2 = other.A;

    // Alpha difference scaled by white point (keep high precision)
    var alphas = (a2 - a1) * wpA;

    var rDiff = ColorDifferenceCh((r1 * wpR) >> 16, (r2 * wpR) >> 16, alphas);
    var gDiff = ColorDifferenceCh((g1 * wpG) >> 16, (g2 * wpG) >> 16, alphas);
    var bDiff = ColorDifferenceCh((b1 * wpB) >> 16, (b2 * wpB) >> 16, alphas);

    return rDiff + gDiff + bDiff;

    // Calculate color difference for each channel
    int ColorDifferenceCh(int x, int y, int alphaDiff) {
      // Maximum of channel blended on white, and blended on black
      var black = x - y;
      var white = black + (alphaDiff >> 16); // Scale alpha back down for blending
      return black * black + white * white;
    }
  }

  public static int FindClosestColorIndex(this Color[] @this, Color color, Func<Color, Color, int> metric) {
    var closestIndex = -1;
    var closestDistance = int.MaxValue;
    for (var i = 0; i < @this.Length; ++i) {
      var distance = metric(color, @this[i]);
      if (distance >= closestDistance)
        continue;

      if (distance <= 1)
        return i;

      closestDistance = distance;
      closestIndex = i;
    }

    return closestIndex;
  }

  public static int FindClosestColorIndex(this Color[] @this, Color color) {
    var i1 = color.ToArgb();
    var a1 = (byte)(i1 >> 24);
    var r1 = (byte)(i1 >> 16);
    var g1 = (byte)(i1 >> 8);
    var b1 = (byte)i1;
    
    var closestIndex = -1;
    var closestDistance = int.MaxValue;
    for (var i = 0; i < @this.Length; ++i) {
      var i2 = @this[i].ToArgb();
      var a2 = (byte)(i2 >> 24);
      var r2 = (byte)(i2 >> 16);
      var g2 = (byte)(i2 >> 8);
      var b2 = (byte)i2;

      var rMean = r1 + r2;
      var r = r1 - r2;
      var g = g1 - g2;
      var b = b1 - b2;
      var a = a1 - a2;
      rMean >>= 1;
      r *= r;
      g *= g;
      b *= b;
      a *= a;
      var rb = 512 + rMean;
      var bb = 767 - rMean;
      g <<= 2;
      rb *= r;
      bb *= b;
      rb >>= 8;
      bb >>= 8;

      var distance = rb + g + bb + a;
      if (distance >= closestDistance)
        continue;

      if (distance <= 1)
        return i;

      closestDistance = distance;
      closestIndex = i;
    }

    return closestIndex;
  }

}
