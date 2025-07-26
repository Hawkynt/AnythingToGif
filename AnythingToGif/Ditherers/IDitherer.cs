using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using AnythingToGif.ColorDistanceMetrics;

namespace AnythingToGif.Ditherers;

public interface IDitherer {
  void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null);
}

public interface IDitherer<in TMetric> where TMetric : struct, IColorDistanceMetric {
  void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, TMetric metric);
}
