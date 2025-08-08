using System.Drawing;
using AnythingToGif.Extensions;

namespace AnythingToGif.ColorDistanceMetrics;

public readonly struct WeightedYCbCr(int wy, int wcb, int wcr, int wa, int divisor = 1) : IColorDistanceMetric {
  public static readonly WeightedYCbCr Instance = new(2, 1, 1, 1, 5);
  
  public int Calculate(Color self, Color other) {
    var (y1, cb1, cr1, a1) = self.YCbCr();
    var (y2, cb2, cr2, a2) = other.YCbCr();
    var dy = y1 - y2;
    var dcb = cb1 - cb2;
    var dcr = cr1 - cr2;
    var da = a1 - a2;

    return (wy * dy * dy + wcb * dcb * dcb + wcr * dcr * dcr + wa * da * da) / divisor;
  }
}
