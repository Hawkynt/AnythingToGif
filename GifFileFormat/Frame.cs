using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Hawkynt.GifFileFormat;

/// <summary>Represents a single GIF frame with indexed pixel data and optional local color table.</summary>
public sealed class Frame(
  byte[] indexedPixels,
  Dimensions size,
  Offset position,
  Color[]? localColorTable,
  TimeSpan delay,
  FrameDisposalMethod disposalMethod,
  byte? transparentColorIndex,
  bool isInterlaced = false) {
  public byte[] IndexedPixels { get; } = indexedPixels;
  public Dimensions Size { get; } = size;
  public Offset Position { get; } = position;
  public Color[]? LocalColorTable { get; } = localColorTable;
  public TimeSpan Delay { get; } = delay;
  public FrameDisposalMethod DisposalMethod { get; } = disposalMethod;
  public byte? TransparentColorIndex { get; } = transparentColorIndex;
  public bool IsInterlaced { get; } = isInterlaced;

  /// <summary>Creates a new Frame with the same data but a different delay.</summary>
  public Frame WithDelay(TimeSpan newDelay) => new(
    this.IndexedPixels,
    this.Size,
    this.Position,
    this.LocalColorTable,
    newDelay,
    this.DisposalMethod,
    this.TransparentColorIndex,
    this.IsInterlaced
  );

  /// <summary>
  ///   Creates a <see cref="Frame" /> from an 8bpp indexed <see cref="Bitmap" />,
  ///   extracting the palette and pixel indices.
  /// </summary>
  public static unsafe Frame FromBitmap(
    Bitmap image,
    TimeSpan duration,
    FrameDisposalMethod disposal = FrameDisposalMethod.Unspecified,
    byte? transparentColorIndex = null,
    bool useLocalColorTable = true,
    Offset? position = null
  ) {
    ArgumentNullException.ThrowIfNull(image);

    var width = image.Width;
    var height = image.Height;
    var palette = image.Palette.Entries;
    var pixels = new byte[width * height];

    BitmapData? bmpData = null;
    try {
      bmpData = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
      var src = (byte*)bmpData.Scan0;
      var stride = bmpData.Stride;

      if (stride == width)
        fixed (byte* dst = pixels)
          Buffer.MemoryCopy(src, dst, pixels.Length, pixels.Length);
      else
        for (var y = 0; y < height; ++y)
          fixed (byte* dst = pixels)
            Buffer.MemoryCopy(src + y * stride, dst + y * width, width, width);
    } finally {
      if (bmpData != null)
        image.UnlockBits(bmpData);
    }

    return new Frame(
      pixels,
      new Dimensions(width, height),
      position ?? Offset.None,
      useLocalColorTable ? palette : null,
      duration,
      disposal,
      transparentColorIndex,
      false
    );
  }
}
