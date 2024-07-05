using System;
using System.Collections.Generic;

namespace AnythingToGif;

using System.Drawing;
using System.Drawing.Imaging;

internal static partial class BitmapExtensions {
  public static IDictionary<Color, ICollection<Point>> CreateHistogram(this Bitmap image) {
    ArgumentNullException.ThrowIfNull(image);

    var result = new Dictionary<Color, ICollection<Point>>();

    using var worker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var width = image.Width;
    var height = image.Height;

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x)
      result.GetOrAdd(worker[x, y], _ => []).Add(new(x, y));
    
    return result;
  }
}