using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnythingToGif;
using Gif;

class Program {
  static void Main() {
    foreach (var file in new DirectoryInfo("Examples").EnumerateFiles().Where(i=>i.Extension.IsAnyOf(".jpg",".png",".bmp",".tif")))
      ProcessFile(file);

    return;

    static void ProcessFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        TotalFrameDuration = TimeSpan.FromSeconds(0),
        MinimumSubImageDuration = TimeSpan.FromMilliseconds(10),
        FirstSubImageInitsBackground = true,
        Quantizer = new OctreeQuantizer(),
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
  void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, ReadOnlySpan<Color> palette);
}

public readonly record struct NoDitherer : IDitherer {
  
  public static IDitherer Instance { get; } = new NoDitherer();

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, ReadOnlySpan<Color> palette) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var color = source[x, y];
      var replacementColor = (byte)FindClosestColorIndex(color, palette);
      data[y * stride + x] = replacementColor;
    }

    return;

    static int FindClosestColorIndex(Color color, ReadOnlySpan<Color> palette) {
      var closestIndex = 0;
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
        if (distance >= closestDistance)
          continue;

        if (distance <= 1)
          return i;

        closestDistance = distance;
        closestIndex = i;
      }

      return closestIndex;
    }

  }

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
    var histogram = _CreateHistogram(image).ToFrozenDictionary();
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

  private static Color[] _SortHistogram(IDictionary<Color, ICollection<Point>> histogram, ColorOrderingMode mode) {
    var result = new Color[histogram.Count];
    var index = 0;
    foreach (var color in
             (mode switch {
               ColorOrderingMode.MostUsedFirst => histogram.OrderByDescending(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
               ColorOrderingMode.LeastUsedFirst => histogram.OrderBy(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
               ColorOrderingMode.HighLuminanceFirst => histogram.Keys.OrderByDescending(c => c.GetLuminance()),
               ColorOrderingMode.LowLuminanceFirst => histogram.Keys.OrderBy(c => c.GetLuminance()),
               ColorOrderingMode.Random => histogram.Keys.Randomize(),
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
      yield return new(Offset.None, _CreateBackgroundImage(image, maximumColorsPerSubImage, this.Quantizer, this.Ditherer ?? NoDitherer.Instance, histogram, this.ColorOrdering), frameDuration, FrameDisposalMethod.DoNotDispose);
      totalFrameTime += frameDuration;
      if (--availableFrames <= 0)
        yield break;
    }

    var usedColors = _SortHistogram(histogram, this.ColorOrdering);

    // create segments
    var colorSegments = new List<(Color color, ICollection<Point> pixelPositions)>(totalColorCount);
    colorSegments.AddRange(usedColors.Select(color => (color, histogram[color])));

    // create subimages in parallel
    foreach (var frame in ParallelEnumerable.Range(0, availableFrames).AsOrdered().Select(CreateSubImage)) {
      yield return new(Offset.None, frame, frameDuration, FrameDisposalMethod.DoNotDispose, 0);
      totalFrameTime += frameDuration;
    }

    yield break;

    // TODO: if firstsubimageinitsbackground is false and usebackfilling is false, the last image always has to act like usebackfilling is true, otherwise there would be missing pixels on screen
    unsafe Bitmap CreateSubImage(int index) {
      var startIndex = index * maximumColorsPerSubImage;
      var colorSegment = colorSegments.GetRange(startIndex, Math.Min(maximumColorsPerSubImage, totalColorCount - startIndex));
      var otherSegments = this.UseBackFilling && (startIndex + maximumColorsPerSubImage < totalColorCount) ? colorSegments[(startIndex + maximumColorsPerSubImage)..] : null;

      var result = new Bitmap(dimensions.Width, dimensions.Height, PixelFormat.Format8bppIndexed);

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
            var closestColorIndex = (byte)FindClosestColorIndex(color, paletteEntries);
            foreach (var point in positions)
              pixels[point.Y * stride + point.X] = closestColorIndex;
          });

      } finally {
        if (bitmapData != null)
          result.UnlockBits(bitmapData);
      }

      return result;

      static int FindClosestColorIndex(Color color, ReadOnlySpan<Color> palette) {
        var closestIndex = 0;
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
          if (distance >= closestDistance)
            continue;

          if (distance <= 1)
            return i;

          closestDistance = distance;
          closestIndex = i;
        }

        return closestIndex;
      }

    }

  }

  private static Bitmap _CreateBackgroundImage(Bitmap image, byte maxColors, IQuantizer? quantizer, IDitherer ditherer, IDictionary<Color, ICollection<Point>> histogram, ColorOrderingMode mode) {

    var reducedColors = (quantizer?.ReduceColorsTo(maxColors, histogram.Select(kvp => (kvp.Key, (uint)kvp.Value.Count))) ?? _SortHistogram(histogram, mode).Take(maxColors)).ToArray();

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
      using var locker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      ditherer.Dither(locker, bitmapData, reducedColors);

    } finally {

      if (bitmapData != null)
        result.UnlockBits(bitmapData);

    }

    return result;
  }

  private static IDictionary<Color, ICollection<Point>> _CreateHistogram(Bitmap image) {
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
