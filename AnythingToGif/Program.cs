using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using Gif;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

class Program {
  static void Main() {
    foreach (var file in new DirectoryInfo("Examples").EnumerateFiles().Where(i=>i.Extension.IsAnyOf(".jpg",".png",".bmp",".tif")))
      ProcessFile(file);

    return;

    static void ProcessFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        TotalFrameDuration = TimeSpan.FromMilliseconds(5000),
        MinimumSubImageDuration = TimeSpan.FromMilliseconds(10),
        Quantizer = null,
        UseBackFilling = true,
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

public interface IQuantizer {
  IEnumerable<Color> ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors);
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
    var histogram = this._CreateHistogram(image);
    var subImages = this._CreateSubImages(image, histogram).ToList();
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

  private IEnumerable<Frame> _CreateSubImages(Bitmap image, Dictionary<Color, ICollection<Point>> histogram) {
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

    // sort colors somehow
    var usedColors =
      this.ColorOrdering switch {
        ColorOrderingMode.MostUsedFirst => histogram.OrderByDescending(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
        ColorOrderingMode.LeastUsedFirst => histogram.OrderBy(kvp => kvp.Value.Count).Select(kvp => kvp.Key),
        _ => histogram.Select(kvp => kvp.Key).Randomize()
      };
      
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
      palette.Entries[0] = Color.Transparent;
      
      var bitmapData = result.LockBits(new(Point.Empty, result.Size), ImageLockMode.WriteOnly, result.PixelFormat);
      var pixels = (byte*)bitmapData.Scan0;
      var stride = bitmapData.Stride;
      
      byte paletteIndex = 1;
      foreach (var (color,positions) in colorSegment) {
        palette.Entries[paletteIndex] = color;
        foreach (var point in positions)
          pixels[point.Y * stride + point.X] = paletteIndex;

        ++paletteIndex;
      }

      if (otherSegments != null) {
        foreach (var (color, positions) in otherSegments) {
          var closestColorIndex = FindClosestColorIndex(color, palette);
          foreach (var point in positions)
            pixels[point.Y * stride + point.X] = (byte)closestColorIndex;
        }
      }

      result.UnlockBits(bitmapData);
      result.Palette = palette;

      return result;

      static int FindClosestColorIndex(Color color, ColorPalette palette) {
        var closestIndex = 0;
        var closestDistance = double.MaxValue;

        for (var i = 1; i < palette.Entries.Length; ++i) {
          var paletteColor = palette.Entries[i];
          var distance = ColorDistance(color, paletteColor);
          if (!(distance < closestDistance))
            continue;

          closestDistance = distance;
          closestIndex = i;
        }

        return closestIndex;

        static double ColorDistance(Color c1, Color c2) {
          var rMean = (c1.R + c2.R) / 2;
          var r = c1.R - c2.R;
          var g = c1.G - c2.G;
          var b = c1.B - c2.B;

          return Math.Sqrt((((512 + rMean) * r * r) >> 8) + 4 * g * g + (((767 - rMean) * b * b) >> 8));
        }

      }
    }

  }

  private static Bitmap _CreateBackgroundImage(Bitmap image, byte maxColors, IQuantizer? quantizer, IDitherer ditherer, Dictionary<Color, ICollection<Point>> histogram) {
    // Initialize the background image with a reduced color palette using quantizer
    var result = new Bitmap(image.Width, image.Height, PixelFormat.Format8bppIndexed);
    var palette = result.Palette;

    var usedColors = histogram.Keys.ToList();
    var reducedColors = (quantizer == null ? histogram.Keys.Take(maxColors) : quantizer.ReduceColorsTo(maxColors, usedColors)).ToList();

    palette.Entries[0] = Color.Transparent;
    for (int i = 0; i < reducedColors.Count; ++i)
      palette.Entries[i] = reducedColors[i];

    result.Palette = palette;

    using (var graphics = Graphics.FromImage(result)) {
      graphics.Clear(Color.Transparent);

      // TODO: draw all pixels from the source image using the given IDitherer
    }

    return result;
  }
  
  private Dictionary<Color, ICollection<Point>> _CreateHistogram(Bitmap image) {
    var result = new Dictionary<Color, ICollection<Point>>();

    using var worker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x)
      result.GetOrAdd(worker[x, y], () => []).Add(new(x, y));

    return result;
  }

  private void _WriteGif(FileInfo file, Dimensions dimensions, IEnumerable<Frame> frames) => Writer.ToFile(file, dimensions, frames, LoopCount.NotSet);

}
