using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Hawkynt.GifFileFormat;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class GifWriterTests {
  private readonly string _testOutputDirectory = Path.Combine(Path.GetTempPath(), "AnythingToGifTests");

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
  public void ToFile_CreatesSingleFrameGif_Successfully() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "single_frame.gif"));
    var dimensions = new Dimensions(100, 100);
    using var bitmap = new Bitmap(100, 100);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Red);

    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));
    var frames = new[] { frame };

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_CreatesMultiFrameGif_Successfully() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "multi_frame.gif"));
    var dimensions = new Dimensions(50, 50);
    var frames = new List<Frame>();

    var colors = new[] { Color.Red, Color.Green, Color.Blue };
    foreach (var color in colors) {
      var bitmap = new Bitmap(50, 50);
      using var graphics = Graphics.FromImage(bitmap);
      graphics.Clear(color);
      frames.Add(new Frame(bitmap, TimeSpan.FromMilliseconds(200)));
    }

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
    frames.ForEach(f => f.Image.Dispose());
  }

  [Test]
  public void ToFile_WithGlobalColorTable_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "global_color_table.gif"));
    var dimensions = new Dimensions(25, 25);
    using var bitmap = new Bitmap(25, 25);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Blue);

    var globalColorTable = new List<Color> { Color.Red, Color.Green, Color.Blue };
    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite, 
                  globalColorTable: globalColorTable);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithTransparency_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "transparent.gif"));
    var dimensions = new Dimensions(30, 30);
    using var bitmap = new Bitmap(30, 30);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Transparent);
    graphics.FillRectangle(Brushes.Red, 5, 5, 20, 20);

    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100), transparentColor: 0);

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithDifferentFrameDisposalMethods_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "disposal_methods.gif"));
    var dimensions = new Dimensions(40, 40);
    var frames = new List<Frame>();

    var disposalMethods = new[] { 
      FrameDisposalMethod.Unspecified, 
      FrameDisposalMethod.DoNotDispose, 
      FrameDisposalMethod.RestoreToBackground 
    };

    foreach (var disposal in disposalMethods) {
      var bitmap = new Bitmap(40, 40);
      using var graphics = Graphics.FromImage(bitmap);
      graphics.Clear(Color.Yellow);
      frames.Add(new Frame(bitmap, TimeSpan.FromMilliseconds(150), disposal));
    }

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
    frames.ForEach(f => f.Image.Dispose());
  }

  [Test]
  public void ToFile_WithCustomLoopCount_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "custom_loop.gif"));
    var dimensions = new Dimensions(20, 20);
    using var bitmap = new Bitmap(20, 20);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Purple);

    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));
    var loopCount = (LoopCount)5;

    Writer.ToFile(outputFile, dimensions, new[] { frame }, loopCount);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithCompressionEnabled_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "compressed.gif"));
    var dimensions = new Dimensions(60, 60);
    using var bitmap = new Bitmap(60, 60);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Orange);

    var frame = new Frame(bitmap, TimeSpan.FromMilliseconds(100));

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite, 
                  allowCompression: true);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_ThrowsArgumentNullException_WhenParametersAreNull() {
    var dimensions = new Dimensions(10, 10);
    var frames = new Frame[] { };

    Assert.Throws<ArgumentNullException>(() => 
      Writer.ToFile(null!, dimensions, frames, LoopCount.Infinite));
    
    Assert.Throws<ArgumentNullException>(() => 
      Writer.ToFile(new FileInfo("test.gif"), dimensions, null!, LoopCount.Infinite));
  }

  [Test]
  public void Frame_ConstructorsWork_Correctly() {
    using var bitmap = new Bitmap(10, 10);
    var duration = TimeSpan.FromMilliseconds(200);

    var frame1 = new Frame(bitmap, duration);
    Assert.That(frame1.Offset, Is.EqualTo(Offset.None));
    Assert.That(frame1.Duration, Is.EqualTo(duration));

    var offset = new Offset(5, 5);
    var frame2 = new Frame(offset, bitmap, duration, FrameDisposalMethod.DoNotDispose, 1, false);
    Assert.That(frame2.Offset, Is.EqualTo(offset));
    Assert.That(frame2.Disposal, Is.EqualTo(FrameDisposalMethod.DoNotDispose));
    Assert.That(frame2.TransparentColor, Is.EqualTo(1));
    Assert.That(frame2.UseLocalColorTable, Is.False);
  }

  [Test]
  public void Dimensions_CreatesCorrectly() {
    var dimensions = new Dimensions(123, 456);
    Assert.That(dimensions.Width, Is.EqualTo(123));
    Assert.That(dimensions.Height, Is.EqualTo(456));
  }

  [Test]
  public void LoopCount_CreatesCorrectly() {
    var infinite = LoopCount.Infinite;
    Assert.That(infinite.IsSet, Is.True);

    var finite = (LoopCount)10;
    Assert.That(finite.IsSet, Is.True);
    Assert.That(finite.Value, Is.EqualTo(10));
  }
}