using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

/// <summary>
/// PNGQuant color distance metric struct for high-performance distance calculations.
/// Considers how colors appear when blended on black vs white backgrounds.
/// Particularly effective for semi-transparent colors.
/// Based on: https://github.com/pornel/pngquant/blob/cc39b47799a7ff2ef17b529f9415ff6e6b213b8f/lib/pam.h#L148
/// </summary>
internal readonly struct PngQuantMetric(Color whitePoint) : IColorDistanceMetric {

  /// <summary>
  /// White point values: higher = less important, lower = more important.
  /// Default Color.White gives equal weighting. Color.FromArgb(255,255,128,255) makes green 2x more important.
  /// </summary>
  public static readonly PngQuantMetric Instance = new(Color.White);

  public int Calculate(Color self, Color other) {
    // Calculate white point weights with 16-bit precision: (255 << 16) / whitePoint
    var wpR = whitePoint.R > 0 ? (255 << 16) / whitePoint.R : 0;
    var wpG = whitePoint.G > 0 ? (255 << 16) / whitePoint.G : 0;
    var wpB = whitePoint.B > 0 ? (255 << 16) / whitePoint.B : 0;
    var wpA = whitePoint.A > 0 ? (255 << 16) / whitePoint.A : 0;

    var r1 = self.R;
    var g1 = self.G;
    var b1 = self.B;
    var a1 = self.A;

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

    static int ColorDifferenceCh(int x, int y, int alphaDiff) {
      // Maximum of channel blended on white, and blended on black
      var black = x - y;
      var white = black + (alphaDiff >> 16); // Scale alpha back down for blending
      return black * black + white * white;
    }
  }
  
}