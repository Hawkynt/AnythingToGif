using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
    using var bitmap = _CreateIndexedBitmap(100, 100, Color.Red);

    var frame = Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(100));
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
      using var bitmap = _CreateIndexedBitmap(50, 50, color);
      frames.Add(Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(200)));
    }

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithGlobalColorTable_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "global_color_table.gif"));
    var dimensions = new Dimensions(25, 25);
    using var bitmap = _CreateIndexedBitmap(25, 25, Color.Blue);

    var globalColorTable = new List<Color> { Color.Red, Color.Green, Color.Blue };
    var frame = Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(100));

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite,
      globalColorTable: globalColorTable);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithTransparency_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "transparent.gif"));
    var dimensions = new Dimensions(30, 30);
    using var bitmap = _CreateIndexedBitmap(30, 30, Color.Red);

    var frame = Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(100), transparentColorIndex: 0);

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
      using var bitmap = _CreateIndexedBitmap(40, 40, Color.Yellow);
      frames.Add(Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(150), disposal));
    }

    Writer.ToFile(outputFile, dimensions, frames, LoopCount.Infinite);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithCustomLoopCount_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "custom_loop.gif"));
    var dimensions = new Dimensions(20, 20);
    using var bitmap = _CreateIndexedBitmap(20, 20, Color.Purple);

    var frame = Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(100));
    var loopCount = (LoopCount)5;

    Writer.ToFile(outputFile, dimensions, new[] { frame }, loopCount);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_WithCompressionEnabled_CreatesValidGif() {
    var outputFile = new FileInfo(Path.Combine(this._testOutputDirectory, "compressed.gif"));
    var dimensions = new Dimensions(60, 60);
    using var bitmap = _CreateIndexedBitmap(60, 60, Color.Orange);

    var frame = Frame.FromBitmap(bitmap, TimeSpan.FromMilliseconds(100));

    Writer.ToFile(outputFile, dimensions, new[] { frame }, LoopCount.Infinite,
      allowCompression: true);

    Assert.That(outputFile.Exists, Is.True);
    Assert.That(outputFile.Length, Is.GreaterThan(0));
  }

  [Test]
  public void ToFile_ThrowsArgumentNullException_WhenParametersAreNull() {
    var dimensions = new Dimensions(10, 10);
    var frames = Array.Empty<Frame>();

    Assert.Throws<ArgumentNullException>(() =>
      Writer.ToFile(null!, dimensions, frames, LoopCount.Infinite));

    Assert.Throws<ArgumentNullException>(() =>
      Writer.ToFile(new FileInfo("test.gif"), dimensions, null!, LoopCount.Infinite));
  }

  [Test]
  public void Frame_FromBitmapAndProperties_WorkCorrectly() {
    using var bitmap = _CreateIndexedBitmap(10, 10, Color.Red);
    var duration = TimeSpan.FromMilliseconds(200);

    var frame1 = Frame.FromBitmap(bitmap, duration);
    Assert.That(frame1.Position, Is.EqualTo(Offset.None));
    Assert.That(frame1.Delay, Is.EqualTo(duration));

    var offset = new Offset(5, 5);
    var frame2 = Frame.FromBitmap(bitmap, duration, FrameDisposalMethod.DoNotDispose, 1, false, offset);
    Assert.That(frame2.Position, Is.EqualTo(offset));
    Assert.That(frame2.DisposalMethod, Is.EqualTo(FrameDisposalMethod.DoNotDispose));
    Assert.That(frame2.TransparentColorIndex, Is.EqualTo(1));
    Assert.That(frame2.LocalColorTable, Is.Null);
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

  private static Bitmap _CreateIndexedBitmap(int width, int height, Color fillColor) {
    var bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
    var palette = bmp.Palette;
    palette.Entries[0] = fillColor;
    bmp.Palette = palette;
    return bmp;
  }
}
