using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.Serialization;
using AnimatedGif;

class Program {
  static void Main() {
    foreach (var file in new DirectoryInfo("Examples").EnumerateFiles())
      ProcessFile(file);

    return;

    static void ProcessFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        TotalFrameDuration = TimeSpan.FromMilliseconds(33),
        MinimumSubImageDuration = TimeSpan.FromMilliseconds(1),
        Quantizer = null,
        UseBackFilling = true
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
  ByUsage = 0,
  FromCenter = 1,
}

public class SingleImageHiColorGifConverter {
  private readonly struct SubImage(Bitmap frame, TimeSpan frameTime) {
    public TimeSpan FrameTime { get; } = frameTime;
    public Bitmap Frame { get; } = frame;
  }

  public TimeSpan? TotalFrameDuration { get; set; }

  public TimeSpan MinimumSubImageDuration { get; set; }

  public IQuantizer? Quantizer { get; set; }
  public IDitherer Ditherer { get; set; } = NoDitherer.Instance;

  public byte MaximumColorsPerSubImage { get; set; } = 255;

  public bool FirstSubImageInitsBackground { get; set; }
  public bool UseBackFilling { get; set; }

  public void Convert(Bitmap image, FileInfo outputFile) {
    var histogram = this._CreateHistogram(image);
    var subImages = this._CreateSubImages(image, histogram).ToList();

    // Adjust the last sub-image frame time if TotalFrameDuration is specified
    var totalFrameDuration = this.TotalFrameDuration;
    if (totalFrameDuration.HasValue) {
      var totalFrameTime = subImages.Sum(i => i.FrameTime);
      if (totalFrameTime < totalFrameDuration.Value) {
        var lastSubImage = subImages[^1];
        subImages[^1] = new SubImage(lastSubImage.Frame, totalFrameDuration.Value - totalFrameTime + lastSubImage.FrameTime);
      }
    }

    this._WriteGif(subImages, outputFile);
  }

  private IEnumerable<SubImage> _CreateSubImages(Bitmap image, Dictionary<Color, ICollection<Point>> histogram) {
    var totalFrameTime = TimeSpan.Zero;
    var frameDuration = this.MinimumSubImageDuration;

    if (this.FirstSubImageInitsBackground) {
      yield return new SubImage(_CreateBackgroundImage(image, this.MaximumColorsPerSubImage, this.Quantizer, this.Ditherer, histogram), frameDuration);
      totalFrameTime += frameDuration;
    }

    while (histogram.Count > 0 && (this.TotalFrameDuration == null || (totalFrameTime + frameDuration) < this.TotalFrameDuration)) {
      yield return new SubImage(_CreateSubImage(image, this.MaximumColorsPerSubImage, histogram,this.UseBackFilling), frameDuration);
      totalFrameTime += frameDuration;
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

  private static Bitmap _CreateSubImage(Bitmap image, byte maxColors, Dictionary<Color, ICollection<Point>> histogram, bool useBackfill) {
    var result = new Bitmap(image.Width, image.Height, PixelFormat.Format8bppIndexed);
    var palette = result.Palette;

    palette.Entries[0] = Color.Transparent;
    var usedColors = histogram.OrderByDescending(kvp=>kvp.Value.Count).Select(kvp=>kvp.Key).Take(maxColors).ToList();
    for (var i = 0; i < usedColors.Count; ++i)
      palette.Entries[i + 1] = usedColors[i];

    result.Palette = palette;

    var bitmapData = result.LockBits(new Rectangle(Point.Empty, result.Size), ImageLockMode.WriteOnly, result.PixelFormat);
    var pixels = new byte[bitmapData.Stride * bitmapData.Height];

    foreach (var color in usedColors) {
      if (!histogram.TryGetValue(color, out var points))
        continue;

      var colorIndex = Array.IndexOf(palette.Entries, color);
      foreach (var point in points) {
        pixels[point.Y * bitmapData.Stride + point.X] = (byte)colorIndex;
      }

      histogram.Remove(color);
    }

    if (useBackfill) {
      // TODO: use all colors left and fill the pixels with the closest color from the current palette
      foreach (var kvp in histogram) {
        var closestColorIndex = FindClosestColorIndex(kvp.Key, palette);
        foreach (var point in kvp.Value) {
          pixels[point.Y * bitmapData.Stride + point.X] = (byte)closestColorIndex;
        }
      }
    }

    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
    result.UnlockBits(bitmapData);
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
        int rMean = (c1.R + c2.R) / 2;
        int r = c1.R - c2.R;
        int g = c1.G - c2.G;
        int b = c1.B - c2.B;

        return Math.Sqrt((((512 + rMean) * r * r) >> 8) + 4 * g * g + (((767 - rMean) * b * b) >> 8));
      }

    }

  }
  
  private Dictionary<Color, ICollection<Point>> _CreateHistogram(Bitmap image) {
    var result = new Dictionary<Color, ICollection<Point>>();

    using var worker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x)
      result.GetOrAdd(worker[x, y], () => []).Add(new Point(x, y));

    return result;
  }

  private void _WriteGif(IEnumerable<SubImage> subImages, FileInfo outputFile) {
    outputFile.TryDelete();

    using var gif = AnimatedGif.AnimatedGif.Create(outputFile.FullName, (int)this.MinimumSubImageDuration.TotalMilliseconds,1);
    var index = 0;
    foreach (var subImage in subImages) {
      //subImage.Frame.SaveToPng(outputFile.WithNewExtension($"{index++}.png"));
      gif.AddFrame(subImage.Frame, (int)subImage.FrameTime.TotalMilliseconds, GifQuality.Bit8);
    }
  }

}
