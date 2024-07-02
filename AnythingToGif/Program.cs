using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

class Program {
  static void Main() {
    foreach (var file in new DirectoryInfo("Examples").EnumerateFiles().Where(i=>i.Extension!=".gif"))
      ProcessFile(file);

    return;

    static void ProcessFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        //TotalFrameDuration = TimeSpan.FromMilliseconds(33),
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
  
  private enum DisposalMethod {
    None = 0,
    DoNotDispose = 1,
    RestoreToBackground = 2,
    RestoreToPrevious = 3
  }

  private readonly record struct SubImage(Point Offset, Bitmap Frame, TimeSpan FrameTime, DisposalMethod Disposal, byte? TransparentColor, bool UseLocalColorTable);

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
        subImages[^1] = lastSubImage with { FrameTime = totalFrameDuration.Value - totalFrameTime + lastSubImage.FrameTime }; //new(lastSubImage.Frame, totalFrameDuration.Value - totalFrameTime + lastSubImage.FrameTime);}
      }
    }

    this._WriteGif(image.Size, subImages, outputFile);
  }

  private IEnumerable<SubImage> _CreateSubImages(Bitmap image, Dictionary<Color, ICollection<Point>> histogram) {
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
      yield return new(Point.Empty,_CreateBackgroundImage(image, maximumColorsPerSubImage, this.Quantizer, this.Ditherer, histogram), frameDuration,DisposalMethod.DoNotDispose,null,true);
      totalFrameTime += frameDuration;
      if(--availableFrames<=0)
        yield break;
    }

    // sort colors somehow
    var usedColors = histogram.OrderByDescending(kvp => kvp.Value.Count).Select(kvp => kvp.Key);
    
    // create segments
    var colorSegments = new List<(Color color, ICollection<Point> pixelPositions)>(totalColorCount);
    colorSegments.AddRange(usedColors.Select(color => (color, histogram[color])));

    // create subimages in parallel
    foreach (var frame in ParallelEnumerable.Range(0, availableFrames).AsOrdered().Select(CreateSubImage)) { 
      yield return new(Point.Empty,frame, frameDuration,DisposalMethod.DoNotDispose,0,true);
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

  private void _WriteGif(Size d, IEnumerable<SubImage> s, FileInfo o) {
    o.TryDelete();
    WriteGif(d,s,o);
  
    return;

    void WriteGif(Size dimensions, IEnumerable<SubImage> subImages, FileInfo outputFile, int loopCount = 0, byte backgroundColorIndex = 0, byte colorResolution = 8, IReadOnlyList<Color>? globalColorTable = null) {
      outputFile.TryDelete();

      using (var fileStream = outputFile.Create())
      using (var writer = new BinaryWriter(fileStream)) {
        WriteHeader(writer);
        WriteLogicalScreenDescriptor(writer, dimensions, backgroundColorIndex,  colorResolution, globalColorTable);

        if (globalColorTable is { Count: > 0 })
          WriteColorTable(writer, globalColorTable);

        if (loopCount > 1)
          WriteApplicationExtension(writer, loopCount);

        var buffer = (new byte[dimensions.Width * dimensions.Height]).AsSpan();
        foreach (var subImage in subImages) {
          WriteGraphicsControlExtension(writer, subImage.FrameTime, subImage.Disposal, subImage.TransparentColor);

          var frameSize = subImage.Frame.Size;
          if (subImage.UseLocalColorTable) {
            var localColorTable = GetColorTable(subImage.Frame);
            WriteImageDescriptor(writer, frameSize, subImage.Offset,true,GetColorTableSize(localColorTable.Count).bitCountMinusOne);
            WriteColorTable(writer, localColorTable);
          } else
            WriteImageDescriptor(writer, frameSize, subImage.Offset, false,0);

          var indexedData = CopyImageToArray(subImage.Frame, buffer);
          WriteImageDataUncompressed(writer, indexedData);
        }

        WriteTrailer(writer);
      }

      Console.WriteLine("GIF created successfully!");
    }

    void WriteHeader(BinaryWriter writer) => writer.Write("GIF89a"u8);

    void WriteLogicalScreenDescriptor(BinaryWriter writer, Size dimensions, byte backgroundColorIndex, byte colorResolution, IReadOnlyList<Color>? globalColorTable) {
      writer.Write((ushort)dimensions.Width);
      writer.Write((ushort)dimensions.Height);
      var packed = (colorResolution - 1) << 4;
      if (globalColorTable is { Count: > 0 }) {
        packed |= 0x80;
        packed |= GetColorTableSize(globalColorTable.Count).bitCountMinusOne;
      }
      
      writer.Write(packed);
      writer.Write(backgroundColorIndex);
      writer.Write(0);
    }

    void WriteApplicationExtension(BinaryWriter writer, int loopCount) {
      writer.Write((byte)0x21); // Extension Introducer
      writer.Write((byte)0xFF); // Application Extension Label
      writer.Write((byte)0x0B); // Block Size
      writer.Write("NETSCAPE2.0"u8); // Application Identifier
      writer.Write((byte)0x03); // Block Size
      writer.Write((byte)0x01); // Sub-block Index
      writer.Write((ushort)(loopCount < 0 ? 0 : loopCount)); // Loop Count (0 means indefinite looping)
      writer.Write((byte)0x00); // Block Terminator
    }

    void WriteGraphicsControlExtension(BinaryWriter writer, TimeSpan frameTime, DisposalMethod disposalMethod, byte? transparentColor) {
      writer.Write((byte)0x21); // Extension Introducer
      writer.Write((byte)0xF9); // Graphic Control Label
      writer.Write((byte)0x04); // Block Size
      var packed = (byte)(((byte)disposalMethod << 2) | (transparentColor.HasValue ? 0x01 : 0x00));
      writer.Write(packed);
      writer.Write((ushort)(frameTime.TotalMilliseconds / 10)); // Delay Time
      writer.Write(transparentColor ?? 0); // Transparent Color Index
      writer.Write((byte)0x00); // Block Terminator
    }

    void WriteImageDescriptor(BinaryWriter writer, Size dimensions, Point offset, bool useLocalColorTable,byte sizeOfTableInBits) {
      writer.Write((byte)0x2C); // Image Separator
      writer.Write((ushort)offset.X); // Image Left Position
      writer.Write((ushort)offset.Y); // Image Top Position
      writer.Write((ushort)dimensions.Width); // Image Width
      writer.Write((ushort)dimensions.Height); // Image Height
      var packed = useLocalColorTable ? 0x80 : 0x00;
      packed |= sizeOfTableInBits;
      writer.Write((byte)packed); // Local Color Table Flag
    }

    void WriteColorTable(BinaryWriter writer, IReadOnlyList<Color> colorTable) {
      foreach (var color in colorTable) {
        writer.Write(color.R);
        writer.Write(color.G);
        writer.Write(color.B);
      }

      // Fill the rest of the color table to the next power of 2
      var size = GetColorTableSize(colorTable.Count);
      for (var i = colorTable.Count; i < size.numberOfEntries; ++i) {
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
      }
    }

    void WriteImageDataUncompressed(BinaryWriter writer, ReadOnlySpan<byte> buffer) {
      // The Minimum Code Size (8 bits per pixel)
      const byte minCodeSize = 8;
      writer.Write(minCodeSize);

      // Calculate the clear code and EOI code
      const ushort clearCode = 1 << minCodeSize; // 256
      const ushort eoiCode = clearCode + 1;      // 257

      var bitBuffer = 0;
      var bitCount = 0;

      var chunkBuffer = new byte[256];
      var chunkIndex = 0;


      // Write each pixel value directly as an LZW code (abusing the LZW algorithm)
      var i = 0;
      foreach (var pixel in buffer) {

        // Write the clear code to initialize the LZW decoder
        if (i++ % 100 == 0)
          WriteBits(clearCode, minCodeSize + 1);

        WriteBits(pixel, minCodeSize + 1);
      }

      // Write the end-of-information code
      WriteBits(eoiCode, minCodeSize + 1);

      // Flush any remaining bits
      if (bitCount > 0)
        chunkBuffer[chunkIndex++] = (byte)bitBuffer;

      // Write any remaining data in the chunk buffer
      if (chunkIndex > 0) {
        writer.Write((byte)chunkIndex); // Write remaining chunk size
        writer.Write(chunkBuffer, 0, chunkIndex); // Write remaining chunk data
      }

      // Block terminator
      writer.Write((byte)0x00);

      return;

      void WriteBits(int bits, int size) {
        bitBuffer |= (bits << bitCount);
        bitCount += size;
        while (bitCount >= 8) {
          chunkBuffer[chunkIndex++] = (byte)(bitBuffer & 0xFF);
          bitBuffer >>= 8;
          bitCount -= 8;

          if (chunkIndex != 255)
            continue;

          writer.Write((byte)255); // Write chunk size
          writer.Write(chunkBuffer, 0, chunkIndex); // Write chunk data
          chunkIndex = 0; // Reset chunk index
        }
      }
    }

    unsafe ReadOnlySpan<byte> CopyImageToArray(Bitmap frame, Span<byte> buffer) {
      fixed (byte* target = buffer) {
        var offset = target;
        var width = frame.Width;
        var height = frame.Height;
        
        BitmapData? bmpData = null;
        try {
          bmpData = frame.LockBits(new(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
          var rowPointer = (byte*)bmpData.Scan0;
          for (var y = 0; y < height; ++y) {
            var columnPointer = rowPointer;
            for (var x = 0; x < width; ++offset, ++columnPointer, ++x)
              *offset = *columnPointer;

            rowPointer += bmpData.Stride;
          }
        } finally {
          if (bmpData != null)
            frame.UnlockBits(bmpData);
        }

        return buffer[..(width * height)];
      }
    }
    
    void WriteTrailer(BinaryWriter writer) => writer.Write((byte)0x3B); // GIF Trailer

    List<Color> GetColorTable(Bitmap bitmap) {
      var palette = bitmap.Palette.Entries;
      return palette.ToList();
    }
    
    (byte bitCountMinusOne,int numberOfEntries) GetColorTableSize(int usedEntryCount) {
      var numberOfEntries = 2;
      byte bitCountMinusOne = 0;
      while (numberOfEntries < usedEntryCount) {
        numberOfEntries <<= 1;
        ++bitCountMinusOne;
      }

      return (bitCountMinusOne,numberOfEntries);
    }

  }

}
