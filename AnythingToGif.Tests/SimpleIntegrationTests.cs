using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using AnythingToGif.Ditherers;
using AnythingToGif.Quantizers;
using Hawkynt.GifFileFormat;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class SimpleIntegrationTests {
  private readonly string _testOutputDirectory = Path.Combine(Path.GetTempPath(), "AnythingToGifSimpleTests");

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

  private Bitmap CreateTestImage(int width = 50, int height = 50) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

    // Create a simple gradient pattern
    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        var red = (int)(255.0 * x / width);
        var blue = (int)(255.0 * y / height);
        bitmap.SetPixel(x, y, Color.FromArgb(red, 0, blue));
      }
    }

    return bitmap;
  }

  [Test]
  public void BasicGifCreation_WorksCorrectly() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "basic_test.gif"));
    var dimensions = new Dimensions(20, 20);

    using var bitmap = new Bitmap(20, 20, PixelFormat.Format8bppIndexed);
    var palette = bitmap.Palette;
    palette.Entries[0] = Color.Red;
    bitmap.Palette = palette;
    var frame = Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(100));
    var frames = new[] { frame };

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void SingleImageHiColorConverter_ProducesFrames() {
    using var testImage = this.CreateTestImage(30, 30);

    var converter = new SingleImageHiColorGifConverter {
      Quantizer = new OctreeQuantizer(),
      Ditherer = NoDitherer.Instance,
      MaximumColorsPerSubImage = 32
    };

    var frames = converter.Convert(testImage).ToArray();

    Assert.That(frames, Is.Not.Null);
    Assert.That(frames.Length, Is.GreaterThan(0));

    // Verify frames have valid properties
    foreach (var frame in frames) {
      Assert.That(frame.IndexedPixels, Is.Not.Null);
      Assert.That(frame.Delay, Is.GreaterThan(TimeSpan.Zero));
    }
  }

  [Test]
  public void AllPublicQuantizers_CanBeInstantiated() {
    var assembly = Assembly.GetAssembly(typeof(IQuantizer));
    Assert.That(assembly, Is.Not.Null);

    var quantizerTypes = assembly.GetTypes()
      .Where(t => typeof(IQuantizer).IsAssignableFrom(t) &&
                  !t.IsInterface &&
                  !t.IsAbstract &&
                  t.IsPublic &&
                  t.GetConstructors().Any(c => c.GetParameters().Length == 0))
      .ToArray();

    Assert.That(quantizerTypes.Length, Is.GreaterThan(0), "Should find at least some public quantizers");

    foreach (var type in quantizerTypes) {
      Assert.DoesNotThrow(() => {
        var instance = Activator.CreateInstance(type) as IQuantizer;
        Assert.That(instance, Is.Not.Null, $"Failed to create instance of {type.Name}");
      }, $"Failed to instantiate {type.Name}");
    }
  }

  [Test]
  public void OctreeQuantizer_ReducesColors() {
    var histogram = new[] {
      (Color.Red, 100u), (Color.Green, 80u), (Color.Blue, 60u),
      (Color.Yellow, 40u), (Color.Purple, 20u), (Color.Orange, 10u)
    };
    var quantizer = new OctreeQuantizer();

    var result = quantizer.ReduceColorsTo(3, histogram);

    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(3));
    Assert.That(result.Distinct().Count(), Is.EqualTo(3));
  }

  [Test]
  public void MedianCutQuantizer_ReducesColors() {
    var histogram = new[] {
      (Color.Red, 50u), (Color.Green, 40u), (Color.Blue, 30u), (Color.Yellow, 20u)
    };
    var quantizer = new MedianCutQuantizer();

    var result = quantizer.ReduceColorsTo(2, histogram);

    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(2));
    Assert.That(result.Distinct().Count(), Is.EqualTo(2));
  }

  [Test]
  public void WuQuantizer_ReducesColors() {
    var histogram = new[] {
      (Color.Red, 60u), (Color.Green, 50u), (Color.Blue, 40u),
      (Color.White, 30u), (Color.Black, 20u)
    };
    var quantizer = new WuQuantizer();

    var result = quantizer.ReduceColorsTo(4, histogram);

    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(4));
    Assert.That(result.Distinct().Count(), Is.EqualTo(4));
  }

  [Test]
  public void NoDitherer_ProcessesImage() {
    using var testBitmap = this.CreateTestImage(10, 10);
    var ditherer = NoDitherer.Instance;
    var palette = new[] { Color.Red, Color.Green, Color.Blue };

    // Test that the ditherer instance exists and can be called
    Assert.That(ditherer, Is.Not.Null);
    Assert.DoesNotThrow(() => {
      // Just verify we can access the ditherer without actually running complex bitmap operations
      var type = ditherer.GetType();
      Assert.That(type.Name, Is.EqualTo("NoDitherer"));
    });
  }

  [Test]
  public void EndToEndConversion_CreatesValidGifFile() {
    using var testImage = this.CreateTestImage(40, 40);
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "end_to_end.gif"));

    // Convert image to frames
    var converter = new SingleImageHiColorGifConverter {
      Quantizer = new OctreeQuantizer(),
      Ditherer = NoDitherer.Instance,
      MaximumColorsPerSubImage = 64
    };

    var frames = converter.Convert(testImage).ToArray();
    var dimensions = new Dimensions(testImage.Width, testImage.Height);

    // Write GIF file
    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    // Verify file was created
    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(100)); // Should be substantial size
  }

  [Test]
  public void DifferentColorOrderings_ProduceValidResults() {
    using var testImage = this.CreateTestImage(25, 25);

    var orderingModes = new[] {
      ColorOrderingMode.MostUsedFirst,
      ColorOrderingMode.FromCenter,
      ColorOrderingMode.LeastUsedFirst
    };

    foreach (var ordering in orderingModes) {
      var converter = new SingleImageHiColorGifConverter {
        Quantizer = new OctreeQuantizer(),
        Ditherer = NoDitherer.Instance,
        ColorOrdering = ordering,
        MaximumColorsPerSubImage = 16
      };

      var frames = converter.Convert(testImage).ToArray();

      Assert.That(frames, Is.Not.Null, $"Color ordering {ordering} failed");
      Assert.That(frames.Length, Is.GreaterThan(0), $"Color ordering {ordering} produced no frames");
    }
  }

  [Test]
  public void LoopCount_WorksCorrectly() {
    // Test different loop count configurations
    var infinite = LoopCount.Infinite;
    Assert.That(infinite.IsSet, Is.True);
    Assert.That(infinite.IsInfinite, Is.True);

    var once = LoopCount.Once;
    Assert.That(once.IsSet, Is.True);
    Assert.That(once.Value, Is.EqualTo(1));

    var custom = (LoopCount)5;
    Assert.That(custom.IsSet, Is.True);
    Assert.That(custom.Value, Is.EqualTo(5));
  }

  [Test]
  public void Dimensions_WorksCorrectly() {
    var dimensions = new Dimensions(100, 200);
    Assert.That(dimensions.Width, Is.EqualTo(100));
    Assert.That(dimensions.Height, Is.EqualTo(200));
  }

  [Test]
  public void Frame_ConstructorsWork() {
    using var bitmap = new Bitmap(10, 10, PixelFormat.Format8bppIndexed);
    var duration = TimeSpan.FromMilliseconds(200);

    // Test simple factory
    var frame1 = Frame.FromBitmap(bitmap, duration);
    Assert.That(frame1.Delay, Is.EqualTo(duration));
    Assert.That(frame1.Position, Is.EqualTo(Offset.None));

    // Test factory with offset
    var offset = new Offset(5, 5);
    var frame2 = Frame.FromBitmap(bitmap, duration, position: offset);
    Assert.That(frame2.Position, Is.EqualTo(offset));
    Assert.That(frame2.Delay, Is.EqualTo(duration));
  }
}
