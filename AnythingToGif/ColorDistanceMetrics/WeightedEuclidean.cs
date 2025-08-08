using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

public readonly struct WeightedEuclidean(int wr,int wg,int wb,int wa, int divisor = 1) : IColorDistanceMetric {
  public static readonly WeightedEuclidean RGBOnly = new(1,1,1,0);
  public static readonly WeightedEuclidean BT709 = new(Weights.BT709.Red, Weights.BT709.Green, Weights.BT709.Blue, Weights.BT709.Alpha, Weights.BT709.Divisor);
  public static readonly WeightedEuclidean Nommyde = new(Weights.Nommyde.Red, Weights.Nommyde.Green, Weights.Nommyde.Blue, Weights.Nommyde.Alpha, Weights.Nommyde.Divisor);
  public static readonly WeightedEuclidean LowRed = new(Weights.LowRed.Red, Weights.LowRed.Green, Weights.LowRed.Blue, Weights.LowRed.Alpha);
  public static readonly WeightedEuclidean HighRed = new(Weights.HighRed.Red, Weights.HighRed.Green, Weights.HighRed.Blue, Weights.HighRed.Alpha);

  public int Calculate(Color self, Color other) {
    var dr = self.R - other.R;
    var dg = self.G - other.G;
    var db = self.B - other.B;
    var da = self.A - other.A;
    return (wr * dr * dr + wg * dg * dg + wb * db * db + wa * da * da) / divisor;
  }
}
