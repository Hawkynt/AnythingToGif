using System.Drawing;

namespace Hawkynt.GifFileFormat;

using System;

public readonly record struct Frame(Offset Offset, Bitmap Image, TimeSpan Duration, FrameDisposalMethod Disposal = FrameDisposalMethod.Unspecified, byte? TransparentColor = null, bool UseLocalColorTable = true) {
  public Frame(Bitmap image, TimeSpan duration, FrameDisposalMethod disposal = FrameDisposalMethod.Unspecified, byte? transparentColor = null, bool useLocalColorTable = true) : this(Offset.None, image, duration, disposal, transparentColor, useLocalColorTable) { }
}