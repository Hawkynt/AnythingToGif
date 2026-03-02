#define OPTIMIZE_MEMORY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

  public static void ToFile(FileInfo outputFile, Dimensions dimensions, IEnumerable<Frame> frames,
    LoopCount loopCount, byte backgroundColorIndex = 0,
    ColorResolution colorResolution = ColorResolution.Colored256, IReadOnlyList<Color>? globalColorTable = null,
    bool allowCompression = false) {
    ArgumentNullException.ThrowIfNull(outputFile);
    ArgumentNullException.ThrowIfNull(frames);

    using var token = outputFile.StartWorkInProgress();
    using var stream = token.Open(FileAccess.Write);
    using var writer = new BinaryWriter(stream);

    _WriteHeader(writer);
    _WriteLogicalScreenDescriptor(writer, dimensions, backgroundColorIndex, (byte)colorResolution, globalColorTable,
      0);

    if (globalColorTable is { Count: > 0 })
      _WriteColorTable(writer, globalColorTable);

    if (loopCount.IsSet)
      _WriteApplicationExtension(writer, loopCount.Value);

    var lastFrameDisposalMethod = FrameDisposalMethod.Unspecified;
    foreach (var frame in frames) {
      ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frame.Delay.TotalMilliseconds);

      var pixels = frame.IndexedPixels;
      int frameW = frame.Size.Width;
      int frameH = frame.Size.Height;
      var frameOffset = frame.Position;

      if (lastFrameDisposalMethod == FrameDisposalMethod.DoNotDispose)
        (pixels, frameW, frameH, frameOffset) = _OptimizeFrameWindow(pixels, frameW, frameH, frameOffset, backgroundColorIndex);

      lastFrameDisposalMethod = frame.DisposalMethod;
      _WriteGraphicsControlExtension(writer, frame.Delay, frame.DisposalMethod, frame.TransparentColorIndex);

      var frameSize = new Size(frameW, frameH);
      if (frame.LocalColorTable != null) {
        _WriteImageDescriptor(writer, frameSize, frameOffset, true,
          _GetColorTableSize(frame.LocalColorTable.Length).bitCountMinusOne);
        _WriteColorTable(writer, frame.LocalColorTable);
      } else {
        _WriteImageDescriptor(writer, frameSize, frameOffset, false, 0);
      }

      _WriteImageData(writer, pixels, allowCompression, 8);
    }

    _WriteTrailer(writer);
  }

  private static (byte[] pixels, int width, int height, Offset offset) _OptimizeFrameWindow(
    byte[] pixels, int width, int height, Offset offset, byte backgroundColorIndex) {
    var top = height;
    var bottom = 0;
    var left = width;
    var right = 0;

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x)
      if (pixels[y * width + x] != backgroundColorIndex) {
        if (y < top) top = y;
        if (y > bottom) bottom = y;
        if (x < left) left = x;
        if (x > right) right = x;
      }

    if (bottom < top)
      return ([backgroundColorIndex], 1, 1, offset);

    var newW = right - left + 1;
    var newH = bottom - top + 1;

    if (newW == width && newH == height)
      return (pixels, width, height, offset);

    var trimmed = new byte[newW * newH];
    for (var y = 0; y < newH; ++y)
      Array.Copy(pixels, (top + y) * width + left, trimmed, y * newW, newW);

    return (trimmed, newW, newH, new Offset(offset.X + left, offset.Y + top));
  }

  private static void _WriteImageData(BinaryWriter writer, ReadOnlySpan<byte> indexedData, bool allowCompression,
    byte bitsPerPixel) {
    writer.Write((byte)(bitsPerPixel == 1 ? 2 : bitsPerPixel));

    if (allowCompression)
      _WriteImageDataCompressed(writer, indexedData, bitsPerPixel);
    else
      _WriteImageDataUncompressed(writer, indexedData, bitsPerPixel);

    writer.Write(BLOCK_TERMINATOR);
  }

  private static void _WriteImageDataUncompressed(BinaryWriter writer, ReadOnlySpan<byte> buffer, byte bitsPerPixel) {
    var clearCode = (ushort)(1 << bitsPerPixel);
    var eoiCode = (ushort)(clearCode + 1);

    // Write each pixel value directly as an LZW code (abusing the LZW algorithm)
    var currentEncodingBitCount = (byte)(bitsPerPixel + 1);

    var bitWriter = new BitWriter(new PacketWriter(writer));

    var i = 0;
    foreach (var pixel in buffer) {
      if (i++ % (512 /* the first entry where 9 bits wouldn't be enough */ - eoiCode -
                 1) /* so we don't interfere with table generation on the decoder side */ == 0)
        bitWriter.Write(clearCode, currentEncodingBitCount);

      bitWriter.Write(pixel, currentEncodingBitCount);
    }

    bitWriter.Write(eoiCode, currentEncodingBitCount);
    bitWriter.Flush();
  }

  private static void _WriteImageDataCompressed(BinaryWriter writer, ReadOnlySpan<byte> buffer, byte bitsPerPixel) {
    var clearCode = (ushort)(1 << bitsPerPixel);
    var eoiCode = (ushort)(clearCode + 1);

    var bitWriter = new BitWriter(new PacketWriter(writer));

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
      node.AddOrUpdate(pixel, new Trie(highestCodeInDictionary));

      ++nextCode;

      var highestCodepointWithCurrentBitCount = (1 << currentEncodingBitCount) - 1;
      if (highestCodeInDictionary + 1 > highestCodepointWithCurrentBitCount + 1)
        if (currentEncodingBitCount >= 12) {
          bitWriter.Write(clearCode, currentEncodingBitCount);
          root = InitializeDictionary(out nextCode, out currentEncodingBitCount);
        } else {
          ++currentEncodingBitCount;
        }

      node = root.GetValueOrNull(pixel)!;
    }

    bitWriter.Write(node.Key, currentEncodingBitCount);
    bitWriter.Write(eoiCode, currentEncodingBitCount);
    bitWriter.Flush();

    return;

    Trie InitializeDictionary(out ushort nextAvailableCodePoint, out byte bitsNeededForEncoding) {
      var result = new Trie(clearCode);
      for (ushort i = 0; i < clearCode; ++i)
        result.AddOrUpdate(i, new Trie(i));

      nextAvailableCodePoint = (ushort)(eoiCode + 1);
      bitsNeededForEncoding = (byte)(bitsPerPixel + 1);

      return result;
    }
  }

  private static void _WriteApplicationExtension(BinaryWriter writer, ushort loopCount) {
    writer.Write(EXTENSION_INTRODUCER); // Extension Introducer
    writer.Write(APPLICATION_EXTENSION); // Application Extension Label
    writer.Write((byte)0x0B); // Block Size
    writer.Write("NETSCAPE"u8); // Application Identifier
    writer.Write("2.0"u8); // Application Authentication Code
    writer.Write((byte)0x03); // Block Size
    writer.Write((byte)0x01); // Sub-block Index
    writer.Write(loopCount); // Loop Count (0 means indefinite looping)
    writer.Write(BLOCK_TERMINATOR); // Block Terminator
  }

  private static void _WriteGraphicsControlExtension(BinaryWriter writer, TimeSpan frameTime,
    FrameDisposalMethod disposalMethod, byte? transparentColor) {
    writer.Write(EXTENSION_INTRODUCER); // Extension Introducer
    writer.Write(GRAPHIC_CONTROL_EXTENSION); // Graphic Control Label
    writer.Write((byte)0x04); // Block Size
    var packed = (byte)(((byte)disposalMethod << 2) |
                        (transparentColor.HasValue ? USE_TRANSPARENCY : NO_TRANSPARENCY));
    writer.Write(packed);
    writer.Write((ushort)(frameTime.TotalMilliseconds / 10)); // Delay Time
    writer.Write(transparentColor ?? 0); // Transparent Color Index
    writer.Write(BLOCK_TERMINATOR); // Block Terminator
  }

  private static void _WriteImageDescriptor(BinaryWriter writer, Size dimensions, Offset offset,
    bool useLocalColorTable, byte sizeOfTableInBits) {
    writer.Write(IMAGE_SEPARATOR); // Image Separator
    writer.Write(offset.X); // Image Left Position
    writer.Write(offset.Y); // Image Top Position
    writer.Write((ushort)dimensions.Width); // Image Width
    writer.Write((ushort)dimensions.Height); // Image Height
    var packed = useLocalColorTable ? LCT_PRESENT : LCT_NOT_PRESENT;
    packed |= sizeOfTableInBits;
    writer.Write(packed); // Local Color Table Flag
  }

  private static void _WriteLogicalScreenDescriptor(BinaryWriter writer, Dimensions dimensions,
    byte backgroundColorIndex, byte colorResolutionInBitsMinusOne, IReadOnlyList<Color>? globalColorTable,
    byte pixelAspectRatio) {
    writer.Write(dimensions.Width);
    writer.Write(dimensions.Height);
    var packed = colorResolutionInBitsMinusOne << 4;
    if (globalColorTable is { Count: > 0 }) {
      packed |= GCT_PRESENT;
      packed |= _GetColorTableSize(globalColorTable.Count).bitCountMinusOne;
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
    var size = _GetColorTableSize(colorTable.Count);
    for (var i = colorTable.Count; i < size.numberOfEntries; ++i) {
      writer.Write((byte)0);
      writer.Write((byte)0);
      writer.Write((byte)0);
    }
  }

  private static (byte bitCountMinusOne, int numberOfEntries) _GetColorTableSize(int usedEntryCount) {
    return usedEntryCount switch {
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
  }

  private static void _WriteHeader(BinaryWriter writer) {
    writer.Write("GIF89a"u8);
  }

  private static void _WriteTrailer(BinaryWriter writer) {
    writer.Write(FILE_TERMINATOR);
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

  [DebuggerDisplay($"{{{nameof(Key)}}}")]
  private class Trie(ushort k) {
    public ushort Key => k;

#if OPTIMIZE_MEMORY

    private readonly Dictionary<ushort, Trie> _children = new();
    public Trie? GetValueOrNull(ushort key) => this._children.TryGetValue(key, out var result) ? result : null;
    public void AddOrUpdate(ushort key, Trie value) => this._children[key] = value;

#else
    private readonly Trie?[] _children = new Trie[256];
    public Trie? GetValueOrNull(ushort key) => this._children[key];
    public void AddOrUpdate(ushort key, Trie value) => this._children[key] = value;

#endif
  }
}
