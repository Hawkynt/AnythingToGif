using System;
using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

/// <summary>
/// Standard Manhattan (L1) color distance metric struct for high-performance calculations.
/// </summary>
internal readonly struct Manhattan : IColorDistanceMetric {
  public static readonly Manhattan Instance = new();
  public int Calculate(Color self, Color other) => Math.Abs(self.R - other.R) + Math.Abs(self.G - other.G) + Math.Abs(self.B - other.B) + Math.Abs(self.A - other.A);
}