using System;
using System.Drawing;
using System.Drawing.Imaging;
using FileFormat.Gif;

namespace AnythingToGif.Gif;

/// <summary>Builds codec <see cref="Frame"/>s out of 8bpp indexed <see cref="Bitmap"/>s.</summary>
public static class GifFrameFactory {

  /// <summary>Creates a <see cref="Frame"/> from an 8bpp indexed <see cref="Bitmap"/>, taking its
  /// palette and pixel indices.</summary>
  /// <param name="image">Source bitmap. Locked as <see cref="PixelFormat.Format8bppIndexed"/>.</param>
  /// <param name="duration">How long the frame stays on screen.</param>
  /// <param name="disposal">Disposal method for the Graphic Control Extension.</param>
  /// <param name="transparentColorIndex">Palette index to treat as transparent, or <c>null</c>.</param>
  /// <param name="useLocalColorTable">When true the bitmap's palette becomes the frame's local colour
  /// table; when false the frame relies on the file's global one.</param>
  /// <param name="position">Where the frame sits on the logical screen. Defaults to the origin.</param>
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

    var offset = position ?? Offset.None;
    return new Frame {
      Left = offset.X,
      Top = offset.Y,
      Width = (ushort)width,
      Height = (ushort)height,
      LocalColorTable = useLocalColorTable ? palette.ToColorTable() : null,
      PixelData = pixels,
      Delay = duration,
      DisposalMethod = disposal,
      TransparentColorIndex = transparentColorIndex,
    };
  }
}
