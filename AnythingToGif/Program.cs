using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gif;

class Program {
  static void Main() {
    foreach (var file in new DirectoryInfo("Examples").EnumerateFiles().Where(i=>i.Extension.IsAnyOf(".jpg",".png",".bmp",".tif")))
      ProcessFile(file);

    return;

    static void ProcessFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        TotalFrameDuration = TimeSpan.FromMilliseconds(10000),
        MinimumSubImageDuration = TimeSpan.FromMilliseconds(10),
        FirstSubImageInitsBackground = true,
        Quantizer = new WuQuantizer(),
        UseBackFilling = false,
        ColorOrdering = ColorOrderingMode.MostUsedFirst
      };

      using var image = Image.FromFile(inputFile.FullName);
      using var bitmap = new Bitmap(image);
      converter.Convert(bitmap, outputFile);
    }
  }
}

public interface IDitherer {
  // TODO:
}

public readonly record struct NoDitherer : IDitherer {
  // TODO:
  public static IDitherer Instance { get; } = new NoDitherer();
}

public readonly record struct BayesDitherer : IDitherer {
  public readonly byte Size { get; }

  // TODO:
}

public readonly record struct MatrixBasedDitherer : IDitherer {
  public byte Divisor { get; }
  // TODO:
  /*
    Floyd-Steinberg
         X   7
     3   5   1
     (1/16)

    Jarvis, Judice, and Ninke
             X   7   5
     3   5   7   5   3
     1   3   5   3   1
           (1/48)

    Stucki
             X   8   4
     2   4   8   4   2
     1   2   4   2   1
           (1/42)

    Atkinson
        X   1   1
    1   1   1
        1
      (1/8)

    Burkes
             X   8   4
     2   4   8   4   2
           (1/32)

    Sierra
            X   5   3
     2   4  5   4   2
         2  3   2
          (1/32)

    Two-Row Sierra
            X   4   3
    1   2   3   2   1
          (1/16)

    Sierra Lite
        X   2
        1   1
        (1/4)
   */
}


// TODO: implement
/*
  Median-cut (MC)
  Octree (OC)
  Variance-based method (WAN)
  Binary splitting (BS)
  Greedy orthogonal bi-partitioning method (WU)
  Neuquant (NQ)
  Adaptive distributing units (ADU)
  Variance-cut (VC)
  WU combined with Ant-tree for color quantization (ATCQ or WUATCQ)
  BS combined with iterative ATCQ (BSITATCQ)
 */

public enum ColorOrderingMode {
  Random = -1,
  MostUsedFirst = 0,
  FromCenter = 1,
  LeastUsedFirst = 2,
  HighLuminanceFirst=3,
  LowLuminanceFirst=4,
}

public class SingleImageHiColorGifConverter {
  
  public TimeSpan? TotalFrameDuration { get; set; }
  public TimeSpan MinimumSubImageDuration { get; set; }
  public IQuantizer? Quantizer { get; set; }
  public IDitherer Ditherer { get; set; } = NoDitherer.Instance;
  public ColorOrderingMode ColorOrdering { get; set; } = ColorOrderingMode.MostUsedFirst;
  public byte MaximumColorsPerSubImage { get; set; } = 255;
  public bool FirstSubImageInitsBackground { get; set; }
  public bool UseBackFilling { get; set; }

  public void Convert(Bitmap image, FileInfo outputFile) {
    var histogram = this._CreateHistogram(image).ToFrozenDictionary();
    var subImages = this._CreateSubImages(image, histogram).ToArray();
    AdjustLastFrameDurationIfNeeded();
    this._WriteGif(outputFile, (Dimensions)image.Size, subImages);
    return;

    void AdjustLastFrameDurationIfNeeded() {
      var totalFrameDuration = this.TotalFrameDuration;
      if (!totalFrameDuration.HasValue)
        return;
      
      var totalFrameTime = subImages.Sum(i => i.Duration);
      if (totalFrameTime >= totalFrameDuration.Value)
        return;

      subImages[^1] = subImages[^1] with { Duration = totalFrameDuration.Value - totalFrameTime + subImages[^1].Duration };
    }
  }

  private Color[] _SortHistogram(IDictionary<Color, ICollection<Point>> histogram) {
    var result = new Color[histogram.Count];
    var index = 0;
    foreach (var color in
             (this.ColorOrdering switch {
               ColorOrderingMode.MostUsedFirst => histogram.OrderByDescending(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
               ColorOrderingMode.LeastUsedFirst => histogram.OrderBy(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
               ColorOrderingMode.HighLuminanceFirst => histogram.Select(kvp => kvp.Key).OrderByDescending(c => c.GetLuminance()),
               ColorOrderingMode.LowLuminanceFirst => histogram.Select(kvp => kvp.Key).OrderBy(c => c.GetLuminance()),
               _ => histogram.Select(kvp => kvp.Key).Randomize()
             })) {
      result[index++] = color;
    }

    return result;
  }

  private IEnumerable<Frame> _CreateSubImages(Bitmap image, IDictionary<Color, ICollection<Point>> histogram) {
    var frameDuration = this.MinimumSubImageDuration;
    var availableTime = this.TotalFrameDuration;
    var totalColorCount = histogram.Count;
    var maximumColorsPerSubImage = this.MaximumColorsPerSubImage;
    var neededFrames = totalColorCount / maximumColorsPerSubImage;
    var availableFrames = availableTime == null ? neededFrames : Math.Min(neededFrames, (int)(availableTime.Value / frameDuration));
    if (availableFrames < 1)
      availableFrames = 1;

    var dimensions = image.Size;

    var totalFrameTime = TimeSpan.Zero;
    if (this.FirstSubImageInitsBackground) {
      yield return new(Offset.None,_CreateBackgroundImage(image, maximumColorsPerSubImage, this.Quantizer, this.Ditherer, histogram), frameDuration,FrameDisposalMethod.DoNotDispose);
      totalFrameTime += frameDuration;
      if(--availableFrames<=0)
        yield break;
    }

    var usedColors = _SortHistogram(histogram);
      
    // create segments
    var colorSegments = new List<(Color color, ICollection<Point> pixelPositions)>(totalColorCount);
    colorSegments.AddRange(usedColors.Select(color => (color, histogram[color])));

    // create subimages in parallel
    foreach (var frame in ParallelEnumerable.Range(0, availableFrames).AsOrdered().Select(CreateSubImage)) { 
      yield return new(Offset.None,frame, frameDuration, FrameDisposalMethod.DoNotDispose,0);
      totalFrameTime += frameDuration;
    }

    yield break;

    unsafe Bitmap CreateSubImage(int index) {
      var startIndex = index * maximumColorsPerSubImage;
      var colorSegment = colorSegments.GetRange(startIndex, Math.Min(maximumColorsPerSubImage, totalColorCount - startIndex));
      var otherSegments = this.UseBackFilling && (startIndex + maximumColorsPerSubImage < totalColorCount) ? colorSegments[(startIndex + maximumColorsPerSubImage)..] : null;

      var result = new Bitmap(dimensions.Width,dimensions.Height, PixelFormat.Format8bppIndexed);

      var palette = result.Palette;
      var paletteEntries = palette.Entries;
      paletteEntries[0] = Color.Transparent;
      for (var i = 0; i < colorSegment.Count; ++i)
        paletteEntries[i + 1] = colorSegment[i].color;

      result.Palette = palette;

      BitmapData? bitmapData = null;
      try {

        bitmapData = result.LockBits(new(Point.Empty, result.Size), ImageLockMode.WriteOnly, result.PixelFormat);
        var pixels = (byte*)bitmapData.Scan0;
        var stride = bitmapData.Stride;

        Parallel.For(0, colorSegment.Count, i => {
          var positions = colorSegment[i].pixelPositions;
          var paletteIndex = (byte)(i + 1);
          foreach (var point in positions)
            pixels[point.Y * stride + point.X] = paletteIndex;
        });

        if (otherSegments != null)
          Parallel.ForEach(otherSegments, t => {
            var (color, positions) = t;
            var closestColorIndex = (byte)SingleImageHiColorGifConverter._FindClosestColorIndex(color, paletteEntries);
            foreach (var point in positions)
              pixels[point.Y * stride + point.X] = closestColorIndex;
          });

      } finally {
        if(bitmapData!=null)
          result.UnlockBits(bitmapData);
      }
      
      return result;
    }

  }

  private static int _FindClosestColorIndex(Color color, ReadOnlySpan<Color> palette) {
    int closestIndex = 0;
    var closestDistance = double.MaxValue;

    var r1 = color.R;
    var g1 = color.G;
    var b1 = color.B;
    for (var i = 1; i < palette.Length; ++i) {
      var paletteColor = palette[i];
      var r2 = paletteColor.R;
      var g2 = paletteColor.G;
      var b2 = paletteColor.B;

      var rMean = (r1 + r2) >> 1;
      var r = r1 - r2;
      var g = g1 - g2;
      var b = b1 - b2;
      r *= r;
      g *= g;
      b *= b;
          
      var distance = (((512 + rMean) * r) >> 8) + 4 * g + (((767 - rMean) * b) >> 8);
      if (distance>=closestDistance)
        continue;

      if (distance <= 1)
        return i;

      closestDistance = distance;
      closestIndex = i;
    }

    return closestIndex;
  }

  private unsafe Bitmap _CreateBackgroundImage(Bitmap image, byte maxColors, IQuantizer? quantizer, IDitherer ditherer, IDictionary<Color, ICollection<Point>> histogram) {

    var reducedColors = (quantizer?.ReduceColorsTo(maxColors, histogram.Select(kvp=>(kvp.Key,kvp.Value.Count))) ?? this._SortHistogram(histogram).Take(maxColors)).ToArray();

    var width = image.Width;
    var height = image.Height;

    var result = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

    var palette = result.Palette;
    var paletteEntries = palette.Entries;
    paletteEntries[0] = Color.Transparent;
    for (var i = 0; i < reducedColors.Length; ++i)
      paletteEntries[i] = reducedColors[i];

    result.Palette = palette;

    BitmapData? bitmapData = null;
    try {

      bitmapData = result.LockBits(new(Point.Empty, result.Size), ImageLockMode.WriteOnly, result.PixelFormat);
      var pixels = (byte*)bitmapData.Scan0;
      var stride = bitmapData.Stride;

      using var locker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

      for (var y = 0; y < height; ++y)
      for (var x = 0; x < width; ++x) {
        var color = locker[x, y];
        var replacementColor = (byte)_FindClosestColorIndex(color, reducedColors);
        pixels[y * stride + x] = replacementColor;
      }

    } finally {

      if (bitmapData != null)
        result.UnlockBits(bitmapData);

    }

    // TODO: draw all pixels from the source image using the given IDitherer

    return result;
  }

  private IDictionary<Color, ICollection<Point>> _CreateHistogram(Bitmap image) {
    var result = new Dictionary<Color, ICollection<Point>>();

    using var worker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var width = image.Width;
    var height = image.Height;

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x)
      result.GetOrAdd(worker[x, y], _ => []).Add(new(x, y));


    return result;
  }

  private void _WriteGif(FileInfo file, Dimensions dimensions, IEnumerable<Frame> frames) => Writer.ToFile(file, dimensions, frames, LoopCount.NotSet);

}
