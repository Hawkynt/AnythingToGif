using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using AnythingToGif;
using AnythingToGif.Ditherers;
using AnythingToGif.Extensions;
using AnythingToGif.Quantizers;
using Hawkynt.GifFileFormat;

public class SingleImageHiColorGifConverter {

  public TimeSpan? TotalFrameDuration { get; set; }
  public TimeSpan MinimumSubImageDuration { get; set; } = TimeSpan.FromMilliseconds(10);
  public TimeSpan SubImageDurationTimeSlice { get; set; } = TimeSpan.FromMilliseconds(10);
  public IQuantizer? Quantizer { get; set; }
  public IDitherer Ditherer { get; set; } = NoDitherer.Instance;
  public ColorOrderingMode ColorOrdering { get; set; } = ColorOrderingMode.MostUsedFirst;
  public byte MaximumColorsPerSubImage { get; set; } = 255;
  public bool FirstSubImageInitsBackground { get; set; }
  public bool UseBackFilling { get; set; }

  /// <summary>
  /// The metric to use for calculating the distance between colors.
  /// </summary>
  /// <remarks>
  ///   <see langword="null"/> means automatically, which uses the one implemented in <see cref="AnythingToGif.Extensions.ColorExtensions.FindClosestColorIndex"/>.
  /// </remarks>
  public Func<Color, Color, int>? ColorDistanceMetric { get; set; } = null;

  public IEnumerable<Frame> Convert(Bitmap image) {
    var histogram = image.CreateHistogram().ToFrozenDictionary();
    var subImages = this._CreateSubImages(image, histogram).ToArray();
    AdjustLastFrameDurationIfNeeded();
    return subImages;

    void AdjustLastFrameDurationIfNeeded() {
      var totalFrameDuration = this.TotalFrameDuration;
      if (!totalFrameDuration.HasValue)
        return;

      var totalFrameTime = subImages.Sum(i => i.Duration);
      if (totalFrameTime >= totalFrameDuration.Value)
        return;

      var duration = totalFrameDuration.Value - totalFrameTime + subImages[^1].Duration;
      var sliceTime = this.SubImageDurationTimeSlice;
      if (sliceTime.Ticks != 0)
        duration = TimeSpan.FromTicks(duration.Ticks / sliceTime.Ticks * sliceTime.Ticks);

      subImages[^1] = subImages[^1] with { Duration = duration };
    }
  }

  private static Color[] _SortHistogram(IDictionary<Color, ICollection<Point>> histogram, Size imageSize, ColorOrderingMode mode) {
    var result = new Color[histogram.Count];
    var index = 0;

    var orderedColors = mode switch {
      ColorOrderingMode.MostUsedFirst => histogram.OrderByDescending(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
      ColorOrderingMode.LeastUsedFirst => histogram.OrderBy(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
      ColorOrderingMode.HighLuminanceFirst => histogram.Keys.OrderByDescending(c => c.GetLuminance()),
      ColorOrderingMode.LowLuminanceFirst => histogram.Keys.OrderBy(c => c.GetLuminance()),
      ColorOrderingMode.FromCenter =>
        histogram.OrderBy(kvp => kvp.Value.Min(p => {
          var dx = p.X - (imageSize.Width - 1) / 2.0;
          var dy = p.Y - (imageSize.Height - 1) / 2.0;
          return dx * dx + dy * dy;
        })).Select(kvp => kvp.Key),
      ColorOrderingMode.Random => histogram.Keys.Shuffled(),
      _ => histogram.Select(kvp => kvp.Key).Shuffled()
    };

    foreach (var color in orderedColors)
      result[index++] = color;

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
      yield return new(Offset.None, SingleImageHiColorGifConverter._CreateBackgroundImage(image, maximumColorsPerSubImage, this.Quantizer, this.Ditherer ?? NoDitherer.Instance, histogram, this.ColorOrdering, this.ColorDistanceMetric), frameDuration, FrameDisposalMethod.DoNotDispose);
      totalFrameTime += frameDuration;
      if (--availableFrames <= 0)
        yield break;
    }

    var usedColors = SingleImageHiColorGifConverter._SortHistogram(histogram, dimensions, this.ColorOrdering);

    // create segments
    var colorSegments = new List<(Color color, ICollection<Point> pixelPositions)>(totalColorCount);
    colorSegments.AddRange(usedColors.Select(color => (color, histogram[color])));

    // create subimages in parallel
    foreach (var frame in ParallelEnumerable.Range(0, availableFrames).AsOrdered().Select(CreateSubImage)) {
      yield return new(Offset.None, frame, frameDuration, FrameDisposalMethod.DoNotDispose, 0);
      totalFrameTime += frameDuration;
    }

    yield break;

    unsafe Bitmap CreateSubImage(int index) {
      var startIndex = index * maximumColorsPerSubImage;
      var colorSegment = colorSegments.GetRange(startIndex, Math.Min(maximumColorsPerSubImage, totalColorCount - startIndex));
      var isLastFrame = index == availableFrames - 1;
      var applyBackFilling = this.UseBackFilling || (isLastFrame && !this.FirstSubImageInitsBackground);
      var otherSegments = applyBackFilling && (startIndex + maximumColorsPerSubImage < totalColorCount)
        ? colorSegments[(startIndex + maximumColorsPerSubImage)..]
        : null;

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
            pixels[point.Y.FusedMultiplyAdd(stride, point.X)] = paletteIndex;
        });

        if (otherSegments != null) {
          var wrapper = new PaletteWrapper(paletteEntries, this.ColorDistanceMetric);
          Parallel.ForEach(otherSegments, tuple => {
            var (color, positions) = tuple;
            var closestColorIndex = (byte)wrapper.FindClosestColorIndex(color);
            foreach (var point in positions)
              pixels[point.Y.FusedMultiplyAdd(stride, point.X)] = closestColorIndex;
          });
        }

      } finally {
        if (bitmapData != null)
          result.UnlockBits(bitmapData);
      }

      return result;
    }

  }

  private static Bitmap _CreateBackgroundImage(Bitmap image, byte maxColors, IQuantizer? quantizer, IDitherer ditherer, IDictionary<Color, ICollection<Point>> histogram, ColorOrderingMode mode, Func<Color, Color, int>? colorDistanceMetric) {
    var colors = 
      maxColors >= histogram.Count
      ? histogram.Keys
      : quantizer?.ReduceColorsTo(maxColors, histogram.Select(kvp => (kvp.Key, (uint)kvp.Value.Count))) 
        ?? SingleImageHiColorGifConverter._SortHistogram(histogram, image.Size, mode).Take(maxColors)
      ;

    var reducedColors = colors.ToArray();

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
      ditherer.Dither(locker, bitmapData, reducedColors, colorDistanceMetric);

    } finally {

      if (bitmapData != null)
        result.UnlockBits(bitmapData);

    }

    return result;
  }

}
