using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AnythingToGif.Ditherers;
using AnythingToGif.Extensions;
using AnythingToGif.Quantizers;
using Hawkynt.GifFileFormat;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class EdgeCaseAndRobustnessTests {
  private readonly string _testOutputDirectory = Path.Combine(Path.GetTempPath(), "AnythingToGifRobustnessTests");

  [SetUp]
  public void Setup() {
    if (Directory.Exists(this._testOutputDirectory))
      Directory.Delete(this._testOutputDirectory, true);
    Directory.CreateDirectory(this._testOutputDirectory);
  }

  [TearDown]
  public void Cleanup() {
    if (Directory.Exists(this._testOutputDirectory))
      Directory.Delete(this._testOutputDirectory, true);
  }

  [Test]
  public void SingleImageConverter_HandlesBoundaryDurations() {
    using var testImage = new Bitmap(10, 10);
    var converter = new SingleImageHiColorGifConverter {
      Quantizer = new OctreeQuantizer(),
      Ditherer = NoDitherer.Instance
    };

    // Test with very small duration
    converter.TotalFrameDuration = TimeSpan.FromMilliseconds(1);
    converter.MinimumSubImageDuration = TimeSpan.FromMilliseconds(1);
    
    Assert.DoesNotThrow(() => {
      var frames = converter.Convert(testImage).ToArray();
      Assert.That(frames.Length, Is.GreaterThan(0));
      foreach (var frame in frames) frame.Image.Dispose();
    });

    // Test with very large duration
    converter.TotalFrameDuration = TimeSpan.FromHours(1);
    converter.MinimumSubImageDuration = TimeSpan.FromSeconds(1);
    
    Assert.DoesNotThrow(() => {
      var frames = converter.Convert(testImage).ToArray();
      Assert.That(frames.Length, Is.GreaterThan(0));
      foreach (var frame in frames) frame.Image.Dispose();
    });
  }

  [Test]
  public void SingleImageConverter_HandlesBoundaryColorCounts() {
    using var testImage = new Bitmap(5, 5);
    var converter = new SingleImageHiColorGifConverter {
      Quantizer = new OctreeQuantizer(),
      Ditherer = NoDitherer.Instance
    };

    // Test with minimum color count
    converter.MaximumColorsPerSubImage = 1;
    Assert.DoesNotThrow(() => {
      var frames = converter.Convert(testImage).ToArray();
      Assert.That(frames.Length, Is.GreaterThan(0));
      foreach (var frame in frames) frame.Image.Dispose();
    });

    // Test with maximum color count
    converter.MaximumColorsPerSubImage = 255;
    Assert.DoesNotThrow(() => {
      var frames = converter.Convert(testImage).ToArray();
      Assert.That(frames.Length, Is.GreaterThan(0));
      foreach (var frame in frames) frame.Image.Dispose();
    });
  }

  [Test]
  public void SingleImageConverter_HandlesMonochromeImages() {
    // Create completely black image
    using var blackImage = new Bitmap(20, 20);
    using var graphics = Graphics.FromImage(blackImage);
    graphics.Clear(Color.Black);

    var converter = new SingleImageHiColorGifConverter {
      Quantizer = new OctreeQuantizer(),
      Ditherer = NoDitherer.Instance,
      MaximumColorsPerSubImage = 32
    };

    var frames = converter.Convert(blackImage).ToArray();
    Assert.That(frames, Is.Not.Null);
    Assert.That(frames.Length, Is.GreaterThan(0));
    
    foreach (var frame in frames) frame.Image.Dispose();
  }

  [Test]
  public void SingleImageConverter_HandlesComplexImages() {
    // Create image with many colors and patterns
    using var complexImage = new Bitmap(50, 50);
    var random = new Random(42);
    
    for (int x = 0; x < 50; x++) {
      for (int y = 0; y < 50; y++) {
        var color = Color.FromArgb(
          random.Next(256), 
          random.Next(256), 
          random.Next(256), 
          random.Next(256));
        complexImage.SetPixel(x, y, color);
      }
    }

    var converter = new SingleImageHiColorGifConverter {
      Quantizer = new OctreeQuantizer(),
      Ditherer = NoDitherer.Instance,
      MaximumColorsPerSubImage = 64
    };

    var frames = converter.Convert(complexImage).ToArray();
    Assert.That(frames, Is.Not.Null);
    Assert.That(frames.Length, Is.GreaterThan(0));
    
    foreach (var frame in frames) frame.Image.Dispose();
  }

  [Test]
  public void GifWriter_HandlesExtremelySmallImages() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "tiny.gif"));
    var dimensions = new Dimensions(1, 1);
    
    using var bitmap = new Bitmap(1, 1);
    bitmap.SetPixel(0, 0, Color.Red);
    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void GifWriter_HandlesLargeImages() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "large.gif"));
    var dimensions = new Dimensions(500, 500);
    
    using var bitmap = new Bitmap(500, 500);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Blue);
    
    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void GifWriter_HandlesManyFrames() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "many_frames.gif"));
    var dimensions = new Dimensions(10, 10);
    var frames = new List<Frame>();

    // Create 100 frames
    for (int i = 0; i < 100; i++) {
      var bitmap = new Bitmap(10, 10);
      using var graphics = Graphics.FromImage(bitmap);
      var hue = (i * 360.0f) / 100.0f;
      var color = ColorFromHSV(hue, 1.0f, 1.0f);
      graphics.Clear(color);
      frames.Add(new Frame(bitmap, TimeSpan.FromMilliseconds(50)));
    }

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
    
    foreach (var frame in frames) frame.Image.Dispose();
  }

  [Test]
  public void GifWriter_HandlesVeryShortFrameDurations() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "short_durations.gif"));
    var dimensions = new Dimensions(20, 20);
    var frames = new List<Frame>();

    var colors = new[] { Color.Red, Color.Green, Color.Blue };
    foreach (var color in colors) {
      var bitmap = new Bitmap(20, 20);
      using var graphics = Graphics.FromImage(bitmap);
      graphics.Clear(color);
      frames.Add(new Frame(bitmap, TimeSpan.FromMilliseconds(1))); // Very short duration
    }

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
    
    foreach (var frame in frames) frame.Image.Dispose();
  }

  [Test]
  public void BitmapExtensions_CreateHistogram_HandlesEdgeCases() {
    // Test with single pixel
    using var singlePixel = new Bitmap(1, 1);
    singlePixel.SetPixel(0, 0, Color.Red);
    
    Assert.DoesNotThrow(() => {
      var histogram = singlePixel.CreateHistogram();
      Assert.That(histogram, Is.Not.Null);
      Assert.That(histogram.Count, Is.EqualTo(1));
      Assert.That(histogram.First().Key.ToArgb(),Is.EqualTo(Color.Red.ToArgb()));
    });

    // Test with transparent pixels
    using var transparentImage = new Bitmap(5, 5, PixelFormat.Format32bppArgb);
    for (int x = 0; x < 5; x++) {
      for (int y = 0; y < 5; y++) {
        transparentImage.SetPixel(x, y, Color.Transparent);
      }
    }
    
    Assert.DoesNotThrow(() => {
      var histogram = transparentImage.CreateHistogram();
      Assert.That(histogram, Is.Not.Null);
    });
  }

  [Test]
  public void AllColorOrderings_WorkWithDifferentImageTypes() {
    var orderingModes = Enum.GetValues<ColorOrderingMode>();
    
    // Test with gradient image
    using var gradientImage = new Bitmap(30, 30);
    for (int x = 0; x < 30; x++) {
      for (int y = 0; y < 30; y++) {
        var red = (x * 255) / 29;
        var blue = (y * 255) / 29;
        gradientImage.SetPixel(x, y, Color.FromArgb(red, 0, blue));
      }
    }

    foreach (var ordering in orderingModes) {
      var converter = new SingleImageHiColorGifConverter {
        Quantizer = new OctreeQuantizer(),
        Ditherer = NoDitherer.Instance,
        ColorOrdering = ordering,
        MaximumColorsPerSubImage = 16
      };

      Assert.DoesNotThrow(() => {
        var frames = converter.Convert(gradientImage).ToArray();
        Assert.That(frames.Length, Is.GreaterThan(0), $"Color ordering {ordering} failed");
        foreach (var frame in frames) frame.Image.Dispose();
      }, $"Color ordering {ordering} should not throw");
    }
  }

  [Test]
  public void MemoryStressTest_HandlesManyConversions() {
    var initialMemory = GC.GetTotalMemory(true);
    
    for (int i = 0; i < 50; i++) {
      using var testImage = new Bitmap(25, 25);
      using var graphics = Graphics.FromImage(testImage);
      graphics.Clear(Color.FromArgb(i * 5, i * 3, i * 2));
      
      var converter = new SingleImageHiColorGifConverter {
        Quantizer = new OctreeQuantizer(),
        Ditherer = NoDitherer.Instance,
        MaximumColorsPerSubImage = 32
      };

      var frames = converter.Convert(testImage).ToArray();
      Assert.That(frames.Length, Is.GreaterThan(0));
      
      // Dispose frames immediately
      foreach (var frame in frames) frame.Image.Dispose();
      
      // Force garbage collection periodically
      if (i % 10 == 9) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
      }
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    var finalMemory = GC.GetTotalMemory(true);
    
    // Memory should not have grown excessively (allowing for some overhead)
    var memoryGrowth = finalMemory - initialMemory;
    Assert.That(memoryGrowth, Is.LessThan(50 * 1024 * 1024), // Less than 50MB growth
      $"Memory usage grew too much: {memoryGrowth} bytes");
  }

  [Test]
  public void FileSystem_HandlesSpecialCharactersInFilenames() {
    var specialFilename = "test with spaces & symbols #@$.gif";
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, specialFilename));
    var dimensions = new Dimensions(10, 10);
    
    using var bitmap = new Bitmap(10, 10);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Magenta);
    
    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));

    Assert.DoesNotThrow(() => {
      Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite);
    });

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ComponentInteraction_AllCombinations_Work() {
    var quantizers = new IQuantizer[] { 
      new OctreeQuantizer(), 
      new MedianCutQuantizer(), 
      new WuQuantizer() 
    };
    
    var ditherers = new IDitherer[] { 
      NoDitherer.Instance, 
      OrderedDitherer.Bayer2x2, 
      OrderedDitherer.Bayer4x4 
    };
    
    var colorOrderings = new[] { 
      ColorOrderingMode.MostUsedFirst, 
      ColorOrderingMode.FromCenter 
    };

    using var testImage = new Bitmap(20, 20);
    using var graphics = Graphics.FromImage(testImage);
    graphics.Clear(Color.Cyan);

    foreach (var quantizer in quantizers) {
      foreach (var ditherer in ditherers) {
        foreach (var ordering in colorOrderings) {
          var converter = new SingleImageHiColorGifConverter {
            Quantizer = quantizer,
            Ditherer = ditherer,
            ColorOrdering = ordering,
            MaximumColorsPerSubImage = 16
          };

          Assert.DoesNotThrow(() => {
            var frames = converter.Convert(testImage).ToArray();
            Assert.That(frames.Length, Is.GreaterThan(0), 
              $"Combination failed: {quantizer.GetType().Name} + {ditherer.GetType().Name} + {ordering}");
            foreach (var frame in frames) frame.Image.Dispose();
          }, $"Should not throw with {quantizer.GetType().Name} + {ditherer.GetType().Name} + {ordering}");
        }
      }
    }
  }

  private static Color ColorFromHSV(float hue, float saturation, float value) {
    int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
    double f = hue / 60 - Math.Floor(hue / 60);

    value = value * 255;
    int v = Convert.ToInt32(value);
    int p = Convert.ToInt32(value * (1 - saturation));
    int q = Convert.ToInt32(value * (1 - f * saturation));
    int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

    return hi switch {
      0 => Color.FromArgb(v, t, p),
      1 => Color.FromArgb(q, v, p),
      2 => Color.FromArgb(p, v, t),
      3 => Color.FromArgb(p, q, v),
      4 => Color.FromArgb(t, p, v),
      _ => Color.FromArgb(v, p, q)
    };
  }
}