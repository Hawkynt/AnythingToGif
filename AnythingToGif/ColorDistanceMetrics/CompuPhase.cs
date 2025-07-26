using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

internal readonly struct CompuPhase : IColorDistanceMetric {
  public static readonly CompuPhase Instance = new();

  // https://www.compuphase.com/cmetric.htm
  public int Calculate(Color self, Color other) {
    var r1 = self.R;
    var g1 = self.G;
    var b1 = self.B;
    var a1 = self.A;
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

}
