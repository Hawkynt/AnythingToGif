using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AnythingToGif.Gif;
using FileFormat.Gif;
using NUnit.Framework;

namespace AnythingToGif.Tests;

/// <summary>The GIF output adapter: the two things this tool adds on top of the canonical codec —
/// the crash-safe file swap and the frame-window crop that makes a high-colour layer stack
/// affordable — plus the GDI+ bridge that stays here rather than going into a portable package.</summary>
[TestFixture]
public class GifOutputTests {
  private readonly string _testOutputDirectory = Path.Combine(Path.GetTempPath(), "AnythingToGifOutputTests");

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

  private FileInfo _Out(string name) => new(Path.Combine(this._testOutputDirectory, name));

  private static Frame _Frame(ushort w, ushort h, byte[] pixels, FrameDisposalMethod disposal, int delayMs = 100) => new() {
    Left = 0, Top = 0, Width = w, Height = h,
    PixelData = pixels,
    Delay = TimeSpan.FromMilliseconds(delayMs),
    DisposalMethod = disposal,
    LocalColorTable = [0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 255, 0],
  };

  [Test]
  public void FrameAfterDoNotDispose_IsCroppedToItsChangedRegion() {
    // First frame stays on the canvas, so everything the second frame leaves at the background
    // index is already on screen and must not be carried in its LZW stream.
    var full = new byte[16 * 16];
    var sparse = new byte[16 * 16];
    sparse[5 * 16 + 6] = 1;
    sparse[5 * 16 + 7] = 2;

    var file = this._Out("cropped.gif");
    GifOutput.ToFile(file, new Dimensions(16, 16), [
      _Frame(16, 16, full, FrameDisposalMethod.DoNotDispose),
      _Frame(16, 16, sparse, FrameDisposalMethod.DoNotDispose),
    ], LoopCount.Infinite);

    var reread = Reader.FromFile(file);
    Assert.That(reread.Frames, Has.Count.EqualTo(2));
    Assert.That(reread.Frames[0].Width, Is.EqualTo(16), "the first frame is never cropped");
    Assert.That(reread.Frames[1].Width, Is.EqualTo(2));
    Assert.That(reread.Frames[1].Height, Is.EqualTo(1));
    Assert.That(reread.Frames[1].Left, Is.EqualTo(6));
    Assert.That(reread.Frames[1].Top, Is.EqualTo(5));
    Assert.That(reread.Frames[1].PixelData, Is.EqualTo(new byte[] { 1, 2 }));
  }

  [Test]
  public void FrameAfterOtherDisposals_IsNotCropped() {
    var sparse = new byte[16 * 16];
    sparse[0] = 1;

    var file = this._Out("uncropped.gif");
    GifOutput.ToFile(file, new Dimensions(16, 16), [
      _Frame(16, 16, sparse, FrameDisposalMethod.RestoreToBackground),
      _Frame(16, 16, sparse, FrameDisposalMethod.RestoreToBackground),
    ], LoopCount.Infinite);

    var reread = Reader.FromFile(file);
    Assert.That(reread.Frames[1].Width, Is.EqualTo(16));
    Assert.That(reread.Frames[1].Height, Is.EqualTo(16));
  }

  [Test]
  public void AllBackgroundFrameAfterDoNotDispose_CollapsesToOnePixel() {
    var full = new byte[8 * 8];
    var file = this._Out("collapsed.gif");
    GifOutput.ToFile(file, new Dimensions(8, 8), [
      _Frame(8, 8, full, FrameDisposalMethod.DoNotDispose),
      _Frame(8, 8, new byte[8 * 8], FrameDisposalMethod.DoNotDispose),
    ], LoopCount.Infinite);

    var reread = Reader.FromFile(file);
    Assert.That(reread.Frames[1].Width, Is.EqualTo(1));
    Assert.That(reread.Frames[1].Height, Is.EqualTo(1));
  }

  [Test]
  public void CompressionOff_IsLargerThanCompressionOn_AndBothReadBack() {
    var pixels = new byte[64 * 64];
    for (var i = 0; i < pixels.Length; ++i) pixels[i] = (byte)(i % 4);

    var on = this._Out("comp_on.gif");
    var off = this._Out("comp_off.gif");
    GifOutput.ToFile(on, new Dimensions(64, 64), [_Frame(64, 64, pixels, FrameDisposalMethod.Unspecified)],
      LoopCount.Infinite, allowCompression: true);
    GifOutput.ToFile(off, new Dimensions(64, 64), [_Frame(64, 64, pixels, FrameDisposalMethod.Unspecified)],
      LoopCount.Infinite, allowCompression: false);

    Assert.That(off.Length, Is.GreaterThan(on.Length));
    Assert.That(Reader.FromFile(on).Frames[0].PixelData, Is.EqualTo(pixels));
    Assert.That(Reader.FromFile(off).Frames[0].PixelData, Is.EqualTo(pixels));
  }

  [Test]
  public void ZeroDelayFrame_IsRejected() {
    var file = this._Out("zero_delay.gif");
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      GifOutput.ToFile(file, new Dimensions(4, 4),
        [_Frame(4, 4, new byte[16], FrameDisposalMethod.Unspecified, 0)], LoopCount.Infinite));
  }

  [Test]
  public void GlobalColorTable_OfAnyLength_IsAccepted() {
    var file = this._Out("gct.gif");
    var colors = new List<Color> { Color.Red, Color.Lime, Color.Blue };
    GifOutput.ToFile(file, new Dimensions(4, 4),
      [_Frame(4, 4, new byte[16], FrameDisposalMethod.Unspecified)], LoopCount.Infinite,
      globalColorTable: colors);

    var reread = Reader.FromFile(file);
    Assert.That(reread.LogicalScreenDescriptor.HasGlobalColorTable, Is.True);
    Assert.That(reread.GlobalColorTable!.ToColors()[0].ToArgb(), Is.EqualTo(Color.Red.ToArgb()));
    Assert.That(reread.LogicalScreenDescriptor.GlobalColorTableEntryCount, Is.EqualTo(4),
      "three colours are padded out to the four entries the format can express");
  }

  [Test]
  public void FramesAreConsumedLazily_OneAtATime() {
    var live = 0;
    var peak = 0;

    IEnumerable<Frame> Produce() {
      for (var i = 0; i < 6; ++i) {
        ++live;
        if (live > peak) peak = live;
        yield return _Frame(8, 8, new byte[64], FrameDisposalMethod.Unspecified);
        --live;
      }
    }

    GifOutput.ToFile(this._Out("lazy.gif"), new Dimensions(8, 8), Produce(), LoopCount.Infinite);

    Assert.That(peak, Is.EqualTo(1), "a high-colour stack must never be materialised in full");
  }

  [Test]
  public void NullArguments_Throw() {
    Assert.Throws<ArgumentNullException>(() =>
      GifOutput.ToFile(null!, new Dimensions(4, 4), Array.Empty<Frame>(), LoopCount.Infinite));
    Assert.Throws<ArgumentNullException>(() =>
      GifOutput.ToFile(this._Out("x.gif"), new Dimensions(4, 4), null!, LoopCount.Infinite));
  }

  [Test]
  public void Interop_ConvertsSizesPointsAndColorTables() {
    Assert.That(new Size(12, 34).ToDimensions(), Is.EqualTo(new Dimensions(12, 34)));
    Assert.That(new Point(5, 6).ToOffset(), Is.EqualTo(new Offset(5, 6)));

    var table = new List<Color> { Color.FromArgb(1, 2, 3), Color.FromArgb(4, 5, 6) }.ToColorTable();
    Assert.That(table, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6 }));

    var back = table.ToColors();
    Assert.That(back, Has.Length.EqualTo(2));
    Assert.That(back[1].ToArgb(), Is.EqualTo(Color.FromArgb(255, 4, 5, 6).ToArgb()));
    Assert.That(((byte[]?)null).ToColors(), Is.Empty);
  }

  [Test]
  public void FrameFactory_TakesPaletteAndPixelsFromAnIndexedBitmap() {
    using var bmp = new Bitmap(3, 2, PixelFormat.Format8bppIndexed);
    var palette = bmp.Palette;
    palette.Entries[0] = Color.Red;
    palette.Entries[1] = Color.Lime;
    bmp.Palette = palette;

    var frame = GifFrameFactory.FromBitmap(bmp, TimeSpan.FromMilliseconds(40),
      FrameDisposalMethod.DoNotDispose, transparentColorIndex: 1, position: new Offset(7, 8));

    Assert.That(frame.Size, Is.EqualTo(new Dimensions(3, 2)));
    Assert.That(frame.Position, Is.EqualTo(new Offset(7, 8)));
    Assert.That(frame.Delay, Is.EqualTo(TimeSpan.FromMilliseconds(40)));
    Assert.That(frame.DisposalMethod, Is.EqualTo(FrameDisposalMethod.DoNotDispose));
    Assert.That(frame.TransparentColorIndex, Is.EqualTo((byte)1));
    Assert.That(frame.IndexedPixels, Has.Length.EqualTo(6));
    Assert.That(frame.LocalColorTable, Is.Not.Null);
    Assert.That(frame.LocalColorTable![0..6], Is.EqualTo(new byte[] { 255, 0, 0, 0, 255, 0 }));
  }

  [Test]
  public void FrameFactory_WithoutLocalTable_LeavesItNull() {
    using var bmp = new Bitmap(2, 2, PixelFormat.Format8bppIndexed);
    var frame = GifFrameFactory.FromBitmap(bmp, TimeSpan.FromMilliseconds(10), useLocalColorTable: false);
    Assert.That(frame.LocalColorTable, Is.Null);
  }

  [Test]
  public void FrameFactory_NullBitmap_Throws()
    => Assert.Throws<ArgumentNullException>(() => GifFrameFactory.FromBitmap(null!, TimeSpan.FromSeconds(1)));

  [Test]
  public void FrameProductionFailure_Propagates() {
    // Frames arrive lazily, so a converter that throws part-way through does so from inside the
    // writer. The exception must not be swallowed by the file token's disposal.
    var file = this._Out("aborted.gif");

    IEnumerable<Frame> Explode() {
      yield return _Frame(8, 8, new byte[64], FrameDisposalMethod.Unspecified);
      throw new InvalidOperationException("frame production failed");
    }

    Assert.Throws<InvalidOperationException>(() =>
      GifOutput.ToFile(file, new Dimensions(8, 8), Explode(), LoopCount.Infinite));
  }
}
