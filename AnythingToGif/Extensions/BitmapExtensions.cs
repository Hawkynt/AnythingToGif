using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace AnythingToGif.Extensions;

internal static partial class BitmapExtensions {

  public static unsafe IDictionary<Color, ICollection<Point>> CreateHistogram(this Bitmap image) {
    ArgumentNullException.ThrowIfNull(image);

    using var worker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var width = image.Width;
    var height = image.Height;
    var bitmapData = worker.BitmapData;
    var start = (byte*)bitmapData.Scan0;
    var stride = bitmapData.Stride;

    // Thread-local dictionaries to avoid contention
    var threadHistograms = new ConcurrentBag<Dictionary<int, List<Point>>>();
    Parallel.ForEach(Partitioner.Create(0, height, Math.Max(1, height / Environment.ProcessorCount)), range => {
      var localHistogram = new Dictionary<int, List<Point>>();

      for (var y = range.Item1; y < range.Item2; ++y) {
        var linePointer = (int*)(start + stride * y);
        for (var x = 0; x < width; ++x) {
          var color = *linePointer++;

          localHistogram.GetOrAdd(color, () => new ()).Add(new(x,y));
        }
      }

      threadHistograms.Add(localHistogram);
    });

    // merge histograms
    var result = new Dictionary<Color, ICollection<Point>>();
    foreach (var threadHistogram in threadHistograms) {
      foreach (var kvp in threadHistogram) {
        var color = Color.FromArgb(kvp.Key);
        if (result.TryGetValue(color, out var indices))
          indices.AddRange(kvp.Value);
        else
          result.Add(color,kvp.Value);
      }
    }

    return result;
  }

}