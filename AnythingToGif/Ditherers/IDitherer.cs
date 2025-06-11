using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace AnythingToGif.Ditherers;

public interface IDitherer {
  void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null);
}
