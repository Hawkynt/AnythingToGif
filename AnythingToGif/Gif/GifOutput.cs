using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using FileFormat.Gif;

namespace AnythingToGif.Gif;

/// <summary>Writes an animation to a .gif through the canonical codec, adding the two things this
/// tool needs on top of it: a crash-safe file swap, and the frame-window crop that makes a
/// high-colour layer stack affordable.</summary>
/// <remarks>
/// The codec's <see cref="GifStreamWriter"/> is what makes this work at all — a high-colour GIF is
/// built as up to a few hundred full-canvas 8bpp layers, and materialising them all before writing a
/// byte would cost hundreds of megabytes for a single frame of video.
/// </remarks>
public static class GifOutput {

  /// <summary>Writes <paramref name="frames"/> to <paramref name="outputFile"/>, consuming the
  /// sequence one frame at a time.</summary>
  /// <param name="outputFile">Destination. Written through a work-in-progress token, so an
  /// interrupted run leaves no half-written .gif in its place.</param>
  /// <param name="dimensions">Logical screen size.</param>
  /// <param name="frames">Frames, consumed lazily.</param>
  /// <param name="loopCount">NETSCAPE2.0 loop count.</param>
  /// <param name="backgroundColorIndex">Logical Screen Descriptor background index.</param>
  /// <param name="colorResolution">Logical Screen Descriptor colour-resolution field.</param>
  /// <param name="globalColorTable">Optional global colour table.</param>
  /// <param name="allowCompression">When false every pixel goes out as a literal LZW code. The
  /// stream stays spec-legal and any decoder reads it; it is simply much bigger, which is the right
  /// trade when a few hundred layers are being produced under a time budget.</param>
  public static void ToFile(
    FileInfo outputFile,
    Dimensions dimensions,
    IEnumerable<Frame> frames,
    LoopCount loopCount,
    byte backgroundColorIndex = 0,
    ColorResolution colorResolution = ColorResolution.Colored256,
    IReadOnlyList<Color>? globalColorTable = null,
    bool allowCompression = false) {
    ArgumentNullException.ThrowIfNull(outputFile);
    ArgumentNullException.ThrowIfNull(frames);

    using var token = outputFile.StartWorkInProgress();
    using var stream = token.Open(FileAccess.Write);

    Writer.WriteTo(
      stream,
      dimensions,
      _Prepare(frames, backgroundColorIndex),
      loopCount,
      backgroundColorIndex,
      colorResolution,
      globalColorTable is { Count: > 0 } ? globalColorTable.ToColorTable() : null,
      allowCompression ? GifWriteOptions.Default : GifWriteOptions.NoCompression);
  }

  /// <summary>Crops a frame to the region that carries information whenever the frame before it was
  /// left on the canvas — everything still equal to the background is already on screen, so the LZW
  /// stream does not have to carry it.</summary>
  private static IEnumerable<Frame> _Prepare(IEnumerable<Frame> frames, byte backgroundColorIndex) {
    var previousDisposal = FrameDisposalMethod.Unspecified;

    foreach (var frame in frames) {
      ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frame.Delay.TotalMilliseconds);

      var emitted = frame;
      if (previousDisposal == FrameDisposalMethod.DoNotDispose) {
        var (pixels, size, position) =
          GifFrameWindow.Trim(frame.PixelData, frame.Size, frame.Position, backgroundColorIndex);
        if (!ReferenceEquals(pixels, frame.PixelData))
          emitted = new Frame {
            Left = position.X, Top = position.Y, Width = size.Width, Height = size.Height,
            LocalColorTable = frame.LocalColorTable,
            LocalColorTableSorted = frame.LocalColorTableSorted,
            IsInterlaced = frame.IsInterlaced,
            PixelData = pixels,
            Delay = frame.Delay,
            DisposalMethod = frame.DisposalMethod,
            UserInputFlag = frame.UserInputFlag,
            TransparentColorIndex = frame.TransparentColorIndex,
          };
      }

      previousDisposal = frame.DisposalMethod;
      yield return emitted;
    }
  }
}
