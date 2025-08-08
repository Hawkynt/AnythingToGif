using System;
using System.Drawing;
using AnythingToGif.Extensions;

namespace AnythingToGif.ColorDistanceMetrics;

public readonly struct WeightedYuv(float wy, float wu, float wv, float wa) : IColorDistanceMetric {
  public static readonly WeightedYuv Instance = new(6, 2, 2, 10);

  private readonly float divisor = wy + wu + wv + wa;

  public int Calculate(Color self, Color other) {
    var (y1, u1, v1, a1) = self.Yuv();
    var (y2, u2, v2, a2) = other.Yuv();
    var dy = y1 - y2;
    var du = u1 - u2;
    var dv = v1 - v2;
    var da = a1 - a2;
    return (int)Math.Round( 1000 * (wy * dy * dy + wu * du * du + wv * dv * dv + wa * da * da) / this.divisor);
  }

}
