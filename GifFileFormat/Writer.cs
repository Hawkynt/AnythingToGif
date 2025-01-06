#define OPTIMIZE_MEMORY

using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Hawkynt.GifFileFormat;

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

  public static unsafe void ToFile(FileInfo outputFile, Dimensions dimensions, IEnumerable<Frame> frames, LoopCount loopCount, byte backgroundColorIndex = 0, ColorResolution colorResolution = ColorResolution.Colored256, IReadOnlyList<Color>? globalColorTable = null, bool allowCompression = false, bool disposeFramesAfterWrite = false) {
    ArgumentNullException.ThrowIfNull(outputFile);
    ArgumentNullException.ThrowIfNull(frames);
    
    using var token = outputFile.StartWorkInProgress();
    using var stream = token.Open(FileAccess.Write);
    using var writer = new BinaryWriter(stream);

    Writer._WriteHeader(writer);
    Writer._WriteLogicalScreenDescriptor(writer, dimensions, backgroundColorIndex, (byte)colorResolution, globalColorTable, 0);

    if (globalColorTable is { Count: > 0 })
      Writer._WriteColorTable(writer, globalColorTable);

    if (loopCount.IsSet)
      Writer._WriteApplicationExtension(writer, loopCount.Value);

    var bufferForImageData = new byte[dimensions.Width * dimensions.Height].AsSpan();

    var lastFrameDisposalMethod = FrameDisposalMethod.Unspecified;
    foreach (var frame in frames) {
      ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frame.Duration.TotalMilliseconds);

      var frameSize = frame.Image.Size;
      var frameOffset = frame.Offset;
      var frameOrigin = Point.Empty;
      if (lastFrameDisposalMethod == FrameDisposalMethod.DoNotDispose)
        OptimizeFrameWindow(ref frameSize, ref frameOffset, ref frameOrigin, frame.Image, backgroundColorIndex);

      lastFrameDisposalMethod = frame.Disposal;
      Writer._WriteGraphicsControlExtension(writer, frame.Duration, frame.Disposal, frame.TransparentColor);
      
      if (frame.UseLocalColorTable) {
        var localColorTable = frame.Image.Palette.Entries;
        Writer._WriteImageDescriptor(writer, frameSize, frameOffset, true, Writer._GetColorTableSize(localColorTable.Length).bitCountMinusOne);
        Writer._WriteColorTable(writer, localColorTable);
      } else
        Writer._WriteImageDescriptor(writer, frameSize, frameOffset, false, 0);

      var indexedData = Writer._CopyImageToArray(frame.Image, bufferForImageData, frameOrigin, frameSize);
      if (disposeFramesAfterWrite)
        frame.Image.Dispose();

      Writer._WriteImageData(writer, indexedData, allowCompression, 8);
    }

    Writer._WriteTrailer(writer);
    return;

    static void OptimizeFrameWindow(ref Size size, ref Offset offset, ref Point origin, Bitmap image, byte backgroundColorIndex) {
      var bitmapData = image.LockBits(new Rectangle(origin, size), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
      try {
        var top = origin.Y;
        var left = origin.X;
        var bottom = size.Height;
        var right = size.Width;
        Parallel.Invoke(
          () => top = FindTopMostNonBackgroundPixel((byte*)bitmapData.Scan0, bitmapData.Stride, top, bottom, left, right, backgroundColorIndex),
          () => bottom = FindBottomMostNonBackgroundPixel((byte*)bitmapData.Scan0, bitmapData.Stride, top, bottom, left, right, backgroundColorIndex)
        );
        Parallel.Invoke(
          () => left = FindLeftMostNonBackgroundPixel((byte*)bitmapData.Scan0, bitmapData.Stride, top, bottom, left, right, backgroundColorIndex),
          () => right = FindRightMostNonBackgroundPixel((byte*)bitmapData.Scan0, bitmapData.Stride, top, bottom, left, right, backgroundColorIndex)
        );

        var offsetL = left - origin.X;
        var offsetT = top - origin.Y;
        var offsetR = size.Width - right + offsetL;
        var offsetB = size.Height - bottom + offsetT;

        offset = new(offset.X + offsetL, offset.Y + offsetT);
        origin = new(origin.X + offsetL, origin.Y + offsetT);
        size = new(size.Width - offsetR, size.Height - offsetB);
      } finally {
        image.UnlockBits(bitmapData);
      }
    }

    static int FindTopMostNonBackgroundPixel(byte* imageData, int stride, int top, int bottom, int left, int right, byte backgroundColor) {
      for (var y = top; y < bottom; ++y) {
        var row = imageData + y * stride + left;
        for (var x = left; x < right; ++x, ++row)
          if (*row != backgroundColor)
            return y;
      }

      return bottom;
    }

    static int FindBottomMostNonBackgroundPixel(byte* imageData, int stride, int top, int bottom, int left, int right, byte backgroundColor) {
      for (var y = bottom - 1; y >= top; --y) {
        var row = imageData + y * stride + left;
        for (var x = left; x < right; ++x, ++row)
          if (*row != backgroundColor)
            return y + 1;
      }

      return top + 1;
    }

    static int FindLeftMostNonBackgroundPixel(byte* imageData, int stride, int top, int bottom, int left, int right,byte backgroundColor) {
      for (var x = left; x < right; ++x) {
        var row = imageData + top * stride + x;
        for (var y = top; y < bottom; ++y, row+=stride)
          if (*row != backgroundColor)
            return x;
      }

      return right;
    }

    static int FindRightMostNonBackgroundPixel(byte* imageData, int stride, int top, int bottom, int left, int right, byte backgroundColor) {
      for (var x = right - 1; x >= left; --x) {
        var row = imageData + top * stride + x;
        for (var y = top; y < bottom; ++y, row+=stride)
          if (*row != backgroundColor)
            return x + 1;
      }

      return left + 1;
    }

  }

  private static void _WriteImageData(BinaryWriter writer, ReadOnlySpan<byte> indexedData, bool allowCompression, byte bitsPerPixel) {
    writer.Write((byte)(bitsPerPixel == 1 ? 2 : bitsPerPixel));

    if (allowCompression)
      Writer._WriteImageDataCompressed(writer, indexedData, bitsPerPixel);
    else
      Writer._WriteImageDataUncompressed(writer, indexedData, bitsPerPixel);

    writer.Write(Writer.BLOCK_TERMINATOR);
  }
  
  private struct BitWriter(PacketWriter writer) {

    private uint _buffer;
    private byte _index;

    public void Write(ushort value, byte numberOfBitsToWrite) {
      this._buffer |= (uint)value << this._index;
      this._index += numberOfBitsToWrite;
      while (this._index >= 8) {
        writer.Write((byte)(this._buffer & 0xFF));
        this._buffer >>= 8;
        this._index -= 8;
      }
    }

    public void Flush() {
      if (this._index > 0)
        writer.Write((byte)this._buffer);

      writer.Flush();
    }
  }

  private struct PacketWriter(BinaryWriter writer) {

    private const byte MAX_PACKET_SIZE = 255;
    private readonly byte[] _buffer = new byte[MAX_PACKET_SIZE];
    private int _index;

    public void Write(byte value) {
      this._buffer[this._index++] = value;

      if (this._index < MAX_PACKET_SIZE)
        return;

      writer.Write(MAX_PACKET_SIZE); // Write chunk size
      writer.Write(this._buffer, 0, MAX_PACKET_SIZE); // Write chunk data
      this._index -= MAX_PACKET_SIZE; // Reset chunk index
    }

    public readonly void Flush() {
      if (this._index <= 0)
        return;

      writer.Write((byte)this._index); // Write remaining chunk size
      writer.Write(this._buffer, 0, this._index); // Write remaining chunk data
    }

  }

  private static void _WriteImageDataUncompressed(BinaryWriter writer, ReadOnlySpan<byte> buffer, byte bitsPerPixel) {

    var clearCode = (ushort)(1 << bitsPerPixel);
    var eoiCode = (ushort)(clearCode + 1);

    // Write each pixel value directly as an LZW code (abusing the LZW algorithm)
    var currentEncodingBitCount = (byte)(bitsPerPixel + 1);

    var bitWriter = new BitWriter(new PacketWriter(writer));

    var i = 0;
    foreach (var pixel in buffer) {
      if (i++ % (512 /* the first entry where 9 bits wouldn't be enough */ - eoiCode - 1) /* so we don't interfere with table generation on the decoder side */ == 0)
        bitWriter.Write(clearCode, currentEncodingBitCount);

      bitWriter.Write(pixel, currentEncodingBitCount);
    }

    bitWriter.Write(eoiCode, currentEncodingBitCount);
    bitWriter.Flush();

  }

  [DebuggerDisplay($"{{{nameof(Trie.Key)}}}")]
  private class Trie(ushort k) {
    public ushort Key => k;

#if OPTIMIZE_MEMORY

    private readonly Dictionary<ushort, Trie> _children = new();
    public Trie? GetValueOrNull(ushort key) => this._children.TryGetValue(key,out var result) ? result : null;
    public void AddOrUpdate(ushort key, Trie value) => this._children[key] = value;

#else

    private readonly Trie?[] _children = new Trie[256];
    public Trie? GetValueOrNull(ushort key) => this._children[key];
    public void AddOrUpdate(ushort key, Trie value) => this._children[key] = value;

#endif

  }

  private static void _WriteImageDataCompressed(BinaryWriter writer, ReadOnlySpan<byte> buffer, byte bitsPerPixel) {

    var clearCode = (ushort)(1 << bitsPerPixel);
    var eoiCode = (ushort)(clearCode + 1);

    var bitWriter = new BitWriter(new(writer));

    Trie root;
    var node = root = InitializeDictionary(out var nextCode, out var currentEncodingBitCount);
    foreach (var pixel in buffer) {
      var child = node.GetValueOrNull(pixel);
      if (child != null) {
        node = child;
        continue;
      }

      bitWriter.Write(node.Key, currentEncodingBitCount);
      var highestCodeInDictionary = nextCode;
      node.AddOrUpdate(pixel, new(highestCodeInDictionary));
      
      ++nextCode;
      
      var highestCodepointWithCurrentBitCount = (1 << currentEncodingBitCount) - 1;
      if ((highestCodeInDictionary + 1) > (highestCodepointWithCurrentBitCount + 1))
        if (currentEncodingBitCount >= 12) {
          bitWriter.Write(clearCode, currentEncodingBitCount);
          root = InitializeDictionary(out nextCode, out currentEncodingBitCount);
        } else 
          ++currentEncodingBitCount;

      node = root.GetValueOrNull(pixel)!;
    }

    bitWriter.Write(node.Key, currentEncodingBitCount);
    bitWriter.Write(eoiCode, currentEncodingBitCount);
    bitWriter.Flush();

    return;

    Trie InitializeDictionary(out ushort nextAvailableCodePoint, out byte bitsNeededForEncoding) {
      var result = new Trie(clearCode);
      for (ushort i = 0; i < clearCode; ++i)
        result.AddOrUpdate(i, new(i));

      nextAvailableCodePoint = (ushort)(eoiCode + 1);
      bitsNeededForEncoding = (byte)(bitsPerPixel + 1);

      return result;
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

  private static unsafe ReadOnlySpan<byte> _CopyImageToArray(Bitmap frame, Span<byte> buffer, Point origin, Size size) {
    ArgumentNullException.ThrowIfNull(frame);
    ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, frame.Width * frame.Height, nameof(buffer));

    fixed (byte* target = buffer) {
      var offset = target;
      var width = size.Width;
      var height = size.Height;

      BitmapData? bmpData = null;
      try {

        bmpData = frame.LockBits(new(origin, size), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
        var rowPointer = (byte*)bmpData.Scan0;
        if (bmpData.Stride == width)
          Buffer.MemoryCopy(rowPointer, offset, width * height, width * height);
        else
          for (var y = 0; y < height; ++y) {
            Buffer.MemoryCopy(rowPointer, offset, width, width);
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
