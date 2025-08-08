using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

/// <summary>
/// Standard Euclidean color distance metric struct for high-performance calculations.
/// </summary>
public readonly struct Euclidean : IColorDistanceMetric {
  public static readonly Euclidean Instance = new();

  public int Calculate(Color self, Color other) {
    var dr = self.R - other.R;
    var dg = self.G - other.G;
    var db = self.B - other.B;
    var da = self.A - other.A;
    return dr * dr + dg * dg + db * db + da * da;
  }
}