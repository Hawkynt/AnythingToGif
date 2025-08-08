using System;
using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

public readonly struct WeightedManhattan(int wr, int wg, int wb, int wa, int divisor = 1) : IColorDistanceMetric {
  public static readonly WeightedManhattan RGBOnly = new(1, 1, 1, 0);
  public static readonly WeightedManhattan BT709 = new(Weights.BT709.Red, Weights.BT709.Green, Weights.BT709.Blue, Weights.BT709.Alpha, Weights.BT709.Divisor);
  public static readonly WeightedManhattan Nommyde = new(Weights.Nommyde.Red, Weights.Nommyde.Green, Weights.Nommyde.Blue, Weights.Nommyde.Alpha, Weights.Nommyde.Divisor);
  public static readonly WeightedManhattan LowRed = new(Weights.LowRed.Red, Weights.LowRed.Green, Weights.LowRed.Blue, Weights.LowRed.Alpha);
  public static readonly WeightedManhattan HighRed = new(Weights.HighRed.Red, Weights.HighRed.Green, Weights.HighRed.Blue, Weights.HighRed.Alpha);

  public int Calculate(Color self, Color other) {
    var dr = (self.R - other.R).Abs();
    var dg = (self.G - other.G).Abs();
    var db = (self.B - other.B).Abs();
    var da = (self.A - other.A).Abs();
    return (wr * dr + wg * dg + wb * db + wa * da) / divisor;
  }
}