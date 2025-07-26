using System;
using System.Drawing;
using AnythingToGif.ColorDistanceMetrics;

namespace AnythingToGif.Extensions;

internal static class ColorExtensions {

  /// <summary>
  /// High-performance generic version using struct-based color metrics
  /// </summary>
  public static int FindClosestColorIndex<TMetric>(this Color[] @this, Color color, TMetric metric) where TMetric : struct, IColorDistanceMetric {

    var closestIndex = -1;
    var closestDistance = int.MaxValue;

    for (var i = 0; i < @this.Length; ++i) {
      var distance = metric.Calculate(color, @this[i]);
      if (distance >= closestDistance)
        continue;

      if (distance <= 1)
        return i;

      closestDistance = distance;
      closestIndex = i;
    }

    return closestIndex;
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

  public static (float red, float green, float blue, float alpha) Normalized(this Color @this) => (@this.R / 255f, @this.G / 255f, @this.B / 255f, @this.A / 255f);

  public static (float luminance, float greenBlueChrominance, float redGreenChrominance, float alpha) Yuv(this Color @this) {
    var (r, g, b, a) = Normalized(@this);
    var y = 0.299f * r + 0.587f * g + 0.114f * b;
    var u = -0.14713f * r - 0.28886f * g + 0.436f * b;
    var v = 0.615f * r - 0.51499f * g - 0.10001f * b;
    return (y, u, v, a);
  }

  public static (byte luminance, byte greenBlueChrominance, byte redGreenChrominance, byte alpha) YCbCr(this Color @this) {
    var y = (byte)(0.299 * @this.R + 0.587 * @this.G + 0.114 * @this.B);
    var cb = (byte)(128 - 0.168736 * @this.R - 0.331264 * @this.G + 0.5 * @this.B);
    var cr = (byte)(128 + 0.5 * @this.R - 0.418688 * @this.G - 0.081312 * @this.B);
    return (y, cb, cr, @this.A);
  }

  public static (double L, double A, double B, double alpha) Lab(this Color @this) {
    var (r, g, b, alpha) = Normalized(@this);
    
    r = r <= 0.04045f ? r / 12.92f : MathF.Pow((r + 0.055f) / 1.055f, 2.4f);
    g = g <= 0.04045f ? g / 12.92f : MathF.Pow((g + 0.055f) / 1.055f, 2.4f);
    b = b <= 0.04045f ? b / 12.92f : MathF.Pow((b + 0.055f) / 1.055f, 2.4f);

    var x = (r * 0.4124f + g * 0.3576f + b * 0.1805f) / 0.95047f;
    var y = (r * 0.2126f + g * 0.7152f + b * 0.0722f) / 1.00000f;
    var z = (r * 0.0193f + g * 0.1192f + b * 0.9505f) / 1.08883f;
    
    var fx = F(x);
    var fy = F(y);
    var fz = F(z);

    var L = 116 * fy - 16;
    var A = 500 * (fx - fy);
    var B = 200 * (fy - fz);
    return (L, A, B, alpha);

    static float F(float t) => t > 0.008856f ? MathF.Pow(t, 1 / 3f) : 7.787f * t + 16 / 116f;
  }
}
