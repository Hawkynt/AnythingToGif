using System;
using System.Drawing;
using AnythingToGif.Extensions;

namespace AnythingToGif.ColorDistanceMetrics;

/// <summary>
/// CIEDE2000 color distance metric struct for high-performance calculations.
/// </summary>
public readonly struct CieDe2000 : IColorDistanceMetric {
  public static readonly CieDe2000 Instance = new();

  public int Calculate(Color self, Color other) {
    var (L1, a1, b1, _) = self.Lab();
    var (L2, a2, b2, _) = other.Lab();
    
    var deltaE = CalculateDeltaE2000(L1, a1, b1, L2, a2, b2);
    return (int)(deltaE * deltaE * 100);
  }

  private static readonly double Pow25To7 = Math.Pow(25, 7);
  private static readonly double DegToRad = Math.PI / 180.0;
  private static readonly double RadToDeg = 180.0 / Math.PI;
  private static readonly double PiDiv360 = Math.PI / 360.0;

  private static double CalculateDeltaE2000(double L1, double a1, double b1, double L2, double a2, double b2) {
    var C1 = Math.Sqrt(a1 * a1 + b1 * b1);
    var C2 = Math.Sqrt(a2 * a2 + b2 * b2);
    var Cab = (C1 + C2) * 0.5;
    
    var CabPow7 = Math.Pow(Cab, 7);
    var G = 0.5 * (1 - Math.Sqrt(CabPow7 / (CabPow7 + Pow25To7)));
    
    var a1Prime = a1 * (1 + G);
    var a2Prime = a2 * (1 + G);
    
    var C1Prime = Math.Sqrt(a1Prime * a1Prime + b1 * b1);
    var C2Prime = Math.Sqrt(a2Prime * a2Prime + b2 * b2);
    
    var h1Prime = Math.Atan2(b1, a1Prime) * RadToDeg;
    if (h1Prime < 0) h1Prime += 360;
    
    var h2Prime = Math.Atan2(b2, a2Prime) * RadToDeg;
    if (h2Prime < 0) h2Prime += 360;
    
    var deltaLPrime = L2 - L1;
    var deltaCPrime = C2Prime - C1Prime;
    
    var deltahPrime = 0.0;
    var c1c2Product = C1Prime * C2Prime;
    if (c1c2Product != 0) {
      var hDiff = h2Prime - h1Prime;
      var absHDiff = Math.Abs(hDiff);
      
      if (absHDiff <= 180) {
        deltahPrime = hDiff;
      } else if (hDiff > 180) {
        deltahPrime = hDiff - 360;
      } else {
        deltahPrime = hDiff + 360;
      }
    }
    
    var deltaHPrime = 2 * Math.Sqrt(c1c2Product) * Math.Sin(deltahPrime * PiDiv360);
    
    var LBarPrime = (L1 + L2) * 0.5;
    var CBarPrime = (C1Prime + C2Prime) * 0.5;
    
    var hBarPrime = 0.0;
    if (c1c2Product != 0) {
      var hSum = h1Prime + h2Prime;
      var absH1H2Diff = Math.Abs(h1Prime - h2Prime);
      
      if (absH1H2Diff <= 180) {
        hBarPrime = hSum * 0.5;
      } else if (hSum < 360) {
        hBarPrime = (hSum + 360) * 0.5;
      } else {
        hBarPrime = (hSum - 360) * 0.5;
      }
    }
    
    var hBarPrimeRad = hBarPrime * DegToRad;
    var T = 1 - 0.17 * Math.Cos((hBarPrime - 30) * DegToRad) +
            0.24 * Math.Cos(2 * hBarPrimeRad) +
            0.32 * Math.Cos((3 * hBarPrime + 6) * DegToRad) -
            0.20 * Math.Cos((4 * hBarPrime - 63) * DegToRad);
    
    var hBarMinus275Div25 = (hBarPrime - 275) / 25;
    var deltaTheta = 30 * Math.Exp(-(hBarMinus275Div25 * hBarMinus275Div25));
    
    var CBarPrimePow7 = Math.Pow(CBarPrime, 7);
    var RC = 2 * Math.Sqrt(CBarPrimePow7 / (CBarPrimePow7 + Pow25To7));
    
    var LBarMinus50 = LBarPrime - 50;
    var SL = 1 + (0.015 * LBarMinus50 * LBarMinus50) / Math.Sqrt(20 + LBarMinus50 * LBarMinus50);
    var SC = 1 + 0.045 * CBarPrime;
    var SH = 1 + 0.015 * CBarPrime * T;
    
    var RT = -Math.Sin(2 * deltaTheta * DegToRad) * RC;
    
    var deltaLTerm = deltaLPrime / SL;
    var deltaCTerm = deltaCPrime / SC;
    var deltaHTerm = deltaHPrime / SH;
    
    var deltaE = Math.Sqrt(
      deltaLTerm * deltaLTerm +
      deltaCTerm * deltaCTerm +
      deltaHTerm * deltaHTerm +
      RT * deltaCTerm * deltaHTerm
    );
    
    return deltaE;
  }
}