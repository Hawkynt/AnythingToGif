namespace Gif;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class Writer {
  
  private const byte GCT_PRESENT = 0x80;
  private const byte EXTENSION_INTRODUCER = 0x21;
  private const byte APPLICATION_EXTENSION = 0xFF;
  private const byte GRAPHIC_CONTROL_EXTENSION = 0xF9;
  private const byte BLOCK_TERMINATOR = 0x00;
  private const byte LCT_PRESENT = 0x80;
  private const byte LCT_NOT_PRESENT = 0x00;
  private const byte USE_TRANSPARENCY = 0x01;
  private const byte NO_TRANSPARENCY = 0x00;
  private const byte IMAGE_SEPARATOR = 0x2C;
  private const byte FILE_TERMINATOR = 0x3B;

  public static void ToFile(FileInfo outputFile, Dimensions dimensions, IEnumerable<Frame> frames,  LoopCount loopCount, byte backgroundColorIndex = 0, ColorResolution colorResolution = ColorResolution.Colored256, IReadOnlyList<Color>? globalColorTable = null) {
    ArgumentNullException.ThrowIfNull(outputFile);
    ArgumentNullException.ThrowIfNull(frames);

    var allFrames = frames.ToImmutableList();
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allFrames.MinOrDefault(f=>f.Duration.TotalMilliseconds,1));

    using var token = outputFile.StartWorkInProgress();
    using var stream = token.Open(FileAccess.Write);
    using var writer = new BinaryWriter(stream); 
    
    Writer._WriteHeader(writer);
    Writer._WriteLogicalScreenDescriptor(writer, dimensions, backgroundColorIndex, (byte)colorResolution, globalColorTable,0);

    if (globalColorTable is { Count: > 0 })
      Writer._WriteColorTable(writer, globalColorTable);

    if (loopCount.IsSet)
      Writer._WriteApplicationExtension(writer, loopCount.Value);

    var bufferForImageData = new byte[dimensions.Width * dimensions.Height].AsSpan();

    foreach (var frame in allFrames) {
      Writer._WriteGraphicsControlExtension(writer, frame.Duration, frame.Disposal, frame.TransparentColor);

      var frameSize = frame.Image.Size;
      if (frame.UseLocalColorTable) {
        var localColorTable = frame.Image.Palette.Entries;
        Writer._WriteImageDescriptor(writer, frameSize, frame.Offset, true, Writer._GetColorTableSize(localColorTable.Length).bitCountMinusOne);
        Writer._WriteColorTable(writer, localColorTable);
      } else
        Writer._WriteImageDescriptor(writer, frameSize, frame.Offset, false, 0);

      var indexedData = Writer._CopyImageToArray(frame.Image, bufferForImageData);
      Writer._WriteImageDataUncompressed(writer, indexedData);
    }

    Writer._WriteTrailer(writer);
  }

  private static void _WriteImageDataUncompressed(BinaryWriter writer, ReadOnlySpan<byte> buffer) {
    const int MAX_CHUNKSIZE = 255;

    ushort bitBuffer = 0;
    byte bitCount = 0;

    var chunkBuffer = new byte[MAX_CHUNKSIZE + 1];
    var chunkIndex = 0;
    
    const byte minCodeSize = 8; // The Minimum Code Size (8 bits per pixel)
    writer.Write(minCodeSize);

    // Calculate the clear code and EOI code
    const ushort clearCode = 1 << minCodeSize; // 256
    const ushort eoiCode = clearCode + 1;      // 257

    // Write each pixel value directly as an LZW code (abusing the LZW algorithm)
    var i = 0;
    foreach (var pixel in buffer) {

      // Write the clear code to initialize the LZW decoder
      if (i++ % 250 /* so we don't interfere with table generation on the decoder side */ == 0)
        WriteBits(clearCode, minCodeSize + 1);

      WriteBits(pixel, minCodeSize + 1);
    }

    // Write the end-of-information code
    WriteBits(eoiCode, minCodeSize + 1);

    FlushBits();
    FlushBuffer();

    writer.Write(Writer.BLOCK_TERMINATOR);

    return;

    void WriteBits(ushort bits, byte size) {
      bitBuffer |= (ushort)(bits << bitCount);
      bitCount += size;
      while (bitCount >= 8) {
        chunkBuffer[chunkIndex++] = (byte)(bitBuffer & 0xFF);
        bitBuffer >>= 8;
        bitCount -= 8;

        if (chunkIndex < MAX_CHUNKSIZE)
          continue;

        writer.Write((byte)MAX_CHUNKSIZE); // Write chunk size
        writer.Write(chunkBuffer, 0, MAX_CHUNKSIZE); // Write chunk data
        chunkIndex -= MAX_CHUNKSIZE; // Reset chunk index
      }
    }

    void FlushBits() {
      if (bitCount > 0)
        chunkBuffer[chunkIndex++] = (byte)bitBuffer;
    }

    void FlushBuffer() {
      if (chunkIndex <= 0)
        return;

      writer.Write((byte)chunkIndex); // Write remaining chunk size
      writer.Write(chunkBuffer, 0, chunkIndex); // Write remaining chunk data
    }
  }

  private static void _WriteApplicationExtension(BinaryWriter writer, ushort loopCount) {
    writer.Write(Writer.EXTENSION_INTRODUCER); // Extension Introducer
    writer.Write(Writer.APPLICATION_EXTENSION); // Application Extension Label
    writer.Write((byte)0x0B); // Block Size
    writer.Write("NETSCAPE"u8); // Application Identifier
    writer.Write("2.0"u8); // Application Authentication Code
    writer.Write((byte)0x03); // Block Size
    writer.Write((byte)0x01); // Sub-block Index
    writer.Write(loopCount); // Loop Count (0 means indefinite looping)
    writer.Write(Writer.BLOCK_TERMINATOR); // Block Terminator
  }

  private static void _WriteGraphicsControlExtension(BinaryWriter writer, TimeSpan frameTime, FrameDisposalMethod disposalMethod, byte? transparentColor) {
    writer.Write(Writer.EXTENSION_INTRODUCER); // Extension Introducer
    writer.Write(Writer.GRAPHIC_CONTROL_EXTENSION); // Graphic Control Label
    writer.Write((byte)0x04); // Block Size
    var packed = (byte)(((byte)disposalMethod << 2) | (transparentColor.HasValue ? Writer.USE_TRANSPARENCY : Writer.NO_TRANSPARENCY));
    writer.Write(packed);
    writer.Write((ushort)(frameTime.TotalMilliseconds / 10)); // Delay Time
    writer.Write(transparentColor ?? 0); // Transparent Color Index
    writer.Write(Writer.BLOCK_TERMINATOR); // Block Terminator
  }

  private static void _WriteImageDescriptor(BinaryWriter writer, Size dimensions, Offset offset, bool useLocalColorTable, byte sizeOfTableInBits) {
    writer.Write(Writer.IMAGE_SEPARATOR); // Image Separator
    writer.Write(offset.X); // Image Left Position
    writer.Write(offset.Y); // Image Top Position
    writer.Write((ushort)dimensions.Width); // Image Width
    writer.Write((ushort)dimensions.Height); // Image Height
    var packed = useLocalColorTable ? Writer.LCT_PRESENT : Writer.LCT_NOT_PRESENT;
    packed |= sizeOfTableInBits;
    writer.Write(packed); // Local Color Table Flag
  }

  private static void _WriteLogicalScreenDescriptor(BinaryWriter writer, Dimensions dimensions, byte backgroundColorIndex, byte colorResolutionInBitsMinusOne, IReadOnlyList<Color>? globalColorTable, byte pixelAspectRatio) {
    writer.Write(dimensions.Width);
    writer.Write(dimensions.Height);
    var packed = colorResolutionInBitsMinusOne << 4;
    if (globalColorTable is { Count: > 0 }) {
      packed |= Writer.GCT_PRESENT;
      packed |= Writer._GetColorTableSize(globalColorTable.Count).bitCountMinusOne;
    }

    writer.Write((byte)packed);
    writer.Write(backgroundColorIndex);
    writer.Write(pixelAspectRatio);
  }

  private static void _WriteColorTable(BinaryWriter writer, IReadOnlyList<Color> colorTable) {
    foreach (var color in colorTable) {
      writer.Write(color.R);
      writer.Write(color.G);
      writer.Write(color.B);
    }

    // Fill the rest of the color table to the next power of 2
    var size = Writer._GetColorTableSize(colorTable.Count);
    for (var i = colorTable.Count; i < size.numberOfEntries; ++i) {
      writer.Write((byte)0);
      writer.Write((byte)0);
      writer.Write((byte)0);
    }
  }

  private static unsafe ReadOnlySpan<byte> _CopyImageToArray(Bitmap frame, Span<byte> buffer) {
    ArgumentNullException.ThrowIfNull(frame);
    ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, frame.Width * frame.Height, nameof(buffer));

    fixed (byte* target = buffer) {
      var offset = target;
      var width = frame.Width;
      var height = frame.Height;

      BitmapData? bmpData = null;
      try {
        bmpData = frame.LockBits(new(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
        var rowPointer = (byte*)bmpData.Scan0;
        if (bmpData.Stride == width)
          new ReadOnlySpan<byte>(rowPointer, width * height).CopyTo(new(offset, width * height));
        else
          for (var y = 0; y < height; ++y) {
            new ReadOnlySpan<byte>(rowPointer, width).CopyTo(new(offset, width));
            offset += width;
            rowPointer += bmpData.Stride;
          }
      } finally {
        if (bmpData != null)
          frame.UnlockBits(bmpData);
      }

      return buffer[..(width * height)];
    }
  }

  private static (byte bitCountMinusOne, int numberOfEntries) _GetColorTableSize(int usedEntryCount) 
    => usedEntryCount switch {
      < 0 => throw new ArgumentOutOfRangeException(nameof(usedEntryCount)),
      <= 2 => (0, 2),
      <= 4 => (1, 4),
      <= 8 => (2, 8),
      <= 16 => (3, 16),
      <= 32 => (4, 32),
      <= 64 => (5, 64),
      <= 128 => (6, 128),
      <= 256 => (7, 256),
      _ => throw new ArgumentOutOfRangeException(nameof(usedEntryCount))
    };

  private static void _WriteHeader(BinaryWriter writer) => writer.Write("GIF89a"u8);
  private static void _WriteTrailer(BinaryWriter writer) => writer.Write(Writer.FILE_TERMINATOR);

}
