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

}
