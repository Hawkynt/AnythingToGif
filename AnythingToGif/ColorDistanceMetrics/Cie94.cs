using System;
using System.Drawing;
using AnythingToGif.Extensions;

namespace AnythingToGif.ColorDistanceMetrics;

/// <summary>
/// CIE94 color distance metric struct for high-performance calculations.
/// </summary>
internal readonly struct Cie94(double kL, double k1, double k2) : IColorDistanceMetric {
  public static readonly Cie94 Textiles = new(kL: 2.0, k1: 0.048, k2: 0.014);
  public static readonly Cie94 GraphicArts = new(kL: 1.0, k1: 0.045, k2: 0.015);

  public int Calculate(Color self, Color other) {
    var (L1, a1, b1, _) = self.Lab();
    var (L2, a2, b2, _) = other.Lab();
    
    var deltaL = L1 - L2;
    var deltaA = a1 - a2;
    var deltaB = b1 - b2;
    
    var C1 = Math.Sqrt(a1 * a1 + b1 * b1);
    var C2 = Math.Sqrt(a2 * a2 + b2 * b2);
    var deltaC = C1 - C2;
    
    var deltaH2 = deltaA * deltaA + deltaB * deltaB - deltaC * deltaC;
    var deltaH = deltaH2 > 0 ? Math.Sqrt(deltaH2) : 0;
    
    var sL = 1.0;
    var sC = 1 + k1 * C1;
    var sH = 1 + k2 * C1;
    
    var deltaLTerm = deltaL / (kL * sL);
    var deltaCTerm = deltaC / sC;
    var deltaHTerm = deltaH / sH;
    
    var deltaE = Math.Sqrt(deltaLTerm * deltaLTerm + deltaCTerm * deltaCTerm + deltaHTerm * deltaHTerm);
    return (int)(deltaE * deltaE * 100);
  }
}