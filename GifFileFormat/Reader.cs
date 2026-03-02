using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Hawkynt.GifFileFormat;

public static class Reader {
  private const byte EXTENSION_INTRODUCER = 0x21;
  private const byte APPLICATION_EXTENSION = 0xFF;
  private const byte GRAPHIC_CONTROL_EXTENSION = 0xF9;
  private const byte COMMENT_EXTENSION = 0xFE;
  private const byte PLAIN_TEXT_EXTENSION = 0x01;
  private const byte IMAGE_SEPARATOR = 0x2C;
  private const byte FILE_TERMINATOR = 0x3B;

  public static GifFile FromFile(FileInfo file) {
    ArgumentNullException.ThrowIfNull(file);
    if (!file.Exists)
      throw new FileNotFoundException("GIF file not found.", file.FullName);

    using var stream = file.OpenRead();
    return FromStream(stream);
  }

  public static GifFile FromStream(Stream stream) {
    ArgumentNullException.ThrowIfNull(stream);
    using var reader = new BinaryReader(stream);

    var version = _ReadHeader(reader);
    var (logicalScreenSize, backgroundColorIndex, globalColorTableSize, colorResolutionBits) =
      _ReadLogicalScreenDescriptor(reader);

    Color[]? globalColorTable = null;
    if (globalColorTableSize > 0)
      globalColorTable = _ReadColorTable(reader, globalColorTableSize);

    var frames = new List<Frame>();
    var loopCount = LoopCount.NotSet;

    // per-frame GCE state
    var pendingDelay = TimeSpan.Zero;
    var pendingDisposal = FrameDisposalMethod.Unspecified;
    byte? pendingTransparentIndex = null;

    for (;;) {
      int marker;
      try {
        marker = reader.ReadByte();
      } catch (EndOfStreamException) {
        break;
      }

      switch (marker) {
        case FILE_TERMINATOR:
          goto done;

        case EXTENSION_INTRODUCER:
          var label = reader.ReadByte();
          switch (label) {
            case GRAPHIC_CONTROL_EXTENSION:
              (pendingDelay, pendingDisposal, pendingTransparentIndex) =
                _ReadGraphicsControlExtension(reader);
              break;
            case APPLICATION_EXTENSION:
              var appLoopCount = _ReadApplicationExtension(reader);
              if (appLoopCount.IsSet)
                loopCount = appLoopCount;
              break;
            default:
              _SkipSubBlocks(reader);
              break;
          }

          break;

        case IMAGE_SEPARATOR:
          var frame = _ReadImageBlock(reader, pendingDelay, pendingDisposal, pendingTransparentIndex);
          frames.Add(frame);
          pendingDelay = TimeSpan.Zero;
          pendingDisposal = FrameDisposalMethod.Unspecified;
          pendingTransparentIndex = null;
          break;
      }
    }

    done:
    return new GifFile(version, logicalScreenSize, globalColorTable, loopCount, backgroundColorIndex, frames);
  }

  private static string _ReadHeader(BinaryReader reader) {
    Span<byte> sig = stackalloc byte[6];
    if (reader.Read(sig) < 6)
      throw new InvalidDataException("Not a valid GIF file: too short.");

    if (sig[0] != (byte)'G' || sig[1] != (byte)'I' || sig[2] != (byte)'F')
      throw new InvalidDataException("Not a valid GIF file: missing GIF signature.");

    var version = $"{(char)sig[3]}{(char)sig[4]}{(char)sig[5]}";
    if (version is not ("87a" or "89a"))
      throw new InvalidDataException($"Unsupported GIF version: {version}");

    return version;
  }

  private static (Dimensions size, byte backgroundColorIndex, int globalColorTableSize, int colorResolutionBits)
    _ReadLogicalScreenDescriptor(BinaryReader reader) {
    var width = reader.ReadUInt16();
    var height = reader.ReadUInt16();
    var packed = reader.ReadByte();
    var backgroundColorIndex = reader.ReadByte();
    _ = reader.ReadByte(); // pixel aspect ratio (ignored)

    var hasGlobalColorTable = (packed & 0x80) != 0;
    var colorResolutionBits = ((packed >> 4) & 0x07) + 1;
    var globalColorTableSize = hasGlobalColorTable ? 1 << ((packed & 0x07) + 1) : 0;

    return (new Dimensions(width, height), backgroundColorIndex, globalColorTableSize, colorResolutionBits);
  }

  private static Color[] _ReadColorTable(BinaryReader reader, int entryCount) {
    var colors = new Color[entryCount];
    for (var i = 0; i < entryCount; ++i) {
      var r = reader.ReadByte();
      var g = reader.ReadByte();
      var b = reader.ReadByte();
      colors[i] = Color.FromArgb(255, r, g, b);
    }

    return colors;
  }

  private static (TimeSpan delay, FrameDisposalMethod disposal, byte? transparentIndex) _ReadGraphicsControlExtension(
    BinaryReader reader) {
    var blockSize = reader.ReadByte(); // should be 4
    var packed = reader.ReadByte();
    var delayHundredths = reader.ReadUInt16();
    var transparentColorIndex = reader.ReadByte();
    var terminator = reader.ReadByte(); // block terminator

    var disposal = (FrameDisposalMethod)((packed >> 2) & 0x07);
    var hasTransparency = (packed & 0x01) != 0;

    return (
      TimeSpan.FromMilliseconds(delayHundredths * 10),
      disposal,
      hasTransparency ? transparentColorIndex : null
    );
  }

  private static LoopCount _ReadApplicationExtension(BinaryReader reader) {
    var blockSize = reader.ReadByte(); // should be 11
    Span<byte> appId = stackalloc byte[blockSize];
    reader.Read(appId);

    // Check for NETSCAPE2.0 loop extension
    if (blockSize == 11
        && appId[0] == (byte)'N' && appId[1] == (byte)'E' && appId[2] == (byte)'T'
        && appId[3] == (byte)'S' && appId[4] == (byte)'C' && appId[5] == (byte)'A'
        && appId[6] == (byte)'P' && appId[7] == (byte)'E'
        && appId[8] == (byte)'2' && appId[9] == (byte)'.' && appId[10] == (byte)'0') {
      var subBlockSize = reader.ReadByte(); // should be 3
      if (subBlockSize >= 3) {
        var subBlockIndex = reader.ReadByte(); // should be 1
        var loopValue = reader.ReadUInt16();
        // skip remaining bytes if subBlockSize > 3
        for (var i = 3; i < subBlockSize; ++i)
          reader.ReadByte();
        _SkipSubBlocks(reader);
        return new LoopCount(loopValue);
      }
    }

    // Not NETSCAPE — skip remaining sub-blocks
    _SkipSubBlocks(reader);
    return LoopCount.NotSet;
  }

  private static Frame _ReadImageBlock(BinaryReader reader, TimeSpan delay, FrameDisposalMethod disposal,
    byte? transparentIndex) {
    var left = reader.ReadUInt16();
    var top = reader.ReadUInt16();
    var width = reader.ReadUInt16();
    var height = reader.ReadUInt16();
    var packed = reader.ReadByte();

    var hasLocalColorTable = (packed & 0x80) != 0;
    var isInterlaced = (packed & 0x40) != 0;
    var localColorTableSize = hasLocalColorTable ? 1 << ((packed & 0x07) + 1) : 0;

    Color[]? localColorTable = null;
    if (localColorTableSize > 0)
      localColorTable = _ReadColorTable(reader, localColorTableSize);

    var lzwMinCodeSize = reader.ReadByte();
    var compressedData = _ReadSubBlocks(reader);
    var pixelCount = width * height;
    var decodedPixels = _DecodeLzw(compressedData, lzwMinCodeSize, pixelCount);

    if (isInterlaced)
      decodedPixels = _Deinterlace(decodedPixels, width, height);

    return new Frame(
      decodedPixels,
      new Dimensions(width, height),
      new Offset(left, top),
      localColorTable,
      delay,
      disposal,
      transparentIndex,
      isInterlaced
    );
  }

  private static byte[] _ReadSubBlocks(BinaryReader reader) {
    var result = new List<byte>(4096);
    for (;;) {
      var blockSize = reader.ReadByte();
      if (blockSize == 0)
        break;

      var block = reader.ReadBytes(blockSize);
      if (block.Length < blockSize)
        throw new InvalidDataException("Unexpected end of GIF sub-block data.");

      result.AddRange(block);
    }

    return result.ToArray();
  }

  private static void _SkipSubBlocks(BinaryReader reader) {
    for (;;) {
      var blockSize = reader.ReadByte();
      if (blockSize == 0)
        break;

      reader.BaseStream.Seek(blockSize, SeekOrigin.Current);
    }
  }

  private static byte[] _DecodeLzw(byte[] compressedData, byte lzwMinCodeSize, int pixelCount) {
    var clearCode = 1 << lzwMinCodeSize;
    var eoiCode = clearCode + 1;
    var output = new byte[pixelCount];
    var outputIndex = 0;

    // LZW table: each entry is (prefix index, suffix byte, length)
    // Table entries 0..clearCode-1 are single-byte entries
    var tablePrefix = new int[4096];
    var tableSuffix = new byte[4096];
    var tableLength = new int[4096];

    var codeSize = lzwMinCodeSize + 1;
    var codeMask = (1 << codeSize) - 1;
    var nextCode = eoiCode + 1;
    var tableSize = nextCode;

    // Initialize table with single-byte entries
    for (var i = 0; i < clearCode; ++i) {
      tablePrefix[i] = -1;
      tableSuffix[i] = (byte)i;
      tableLength[i] = 1;
    }

    // Bit reader state
    var bitBuffer = 0;
    var bitsAvailable = 0;
    var dataIndex = 0;

    int ReadCode() {
      while (bitsAvailable < codeSize) {
        if (dataIndex >= compressedData.Length)
          return eoiCode;

        bitBuffer |= compressedData[dataIndex++] << bitsAvailable;
        bitsAvailable += 8;
      }

      var code = bitBuffer & codeMask;
      bitBuffer >>= codeSize;
      bitsAvailable -= codeSize;
      return code;
    }

    void OutputCode(int code) {
      if (code < 0 || code >= tableSize)
        return;

      var length = tableLength[code];
      if (outputIndex + length > output.Length)
        length = output.Length - outputIndex;

      // Walk chain and write in reverse, then reverse
      var pos = outputIndex + length - 1;
      var c = code;
      for (var i = 0; i < length; ++i) {
        if (pos >= 0 && pos < output.Length)
          output[pos--] = tableSuffix[c];
        c = tablePrefix[c];
      }

      outputIndex += length;
    }

    byte GetFirstByte(int code) {
      while (code >= 0 && tablePrefix[code] >= 0)
        code = tablePrefix[code];
      return tableSuffix[code];
    }

    // Read initial clear code
    var code = ReadCode();
    if (code != clearCode)
      throw new InvalidDataException("LZW stream must begin with a clear code.");

    var prevCode = -1;

    while (outputIndex < pixelCount) {
      code = ReadCode();
      if (code == eoiCode)
        break;

      if (code == clearCode) {
        codeSize = lzwMinCodeSize + 1;
        codeMask = (1 << codeSize) - 1;
        tableSize = eoiCode + 1;
        nextCode = tableSize;
        prevCode = -1;
        continue;
      }

      if (prevCode < 0) {
        // First code after clear
        OutputCode(code);
        prevCode = code;
        continue;
      }

      if (code < tableSize) {
        // Code is in table
        OutputCode(code);
        if (nextCode < 4096) {
          tablePrefix[nextCode] = prevCode;
          tableSuffix[nextCode] = GetFirstByte(code);
          tableLength[nextCode] = tableLength[prevCode] + 1;
          ++nextCode;
          ++tableSize;
        }
      } else if (code == tableSize) {
        // Special KwKwK case: code not yet in table, but we can infer it
        var firstByte = GetFirstByte(prevCode);
        if (nextCode < 4096) {
          tablePrefix[nextCode] = prevCode;
          tableSuffix[nextCode] = firstByte;
          tableLength[nextCode] = tableLength[prevCode] + 1;
          ++nextCode;
          ++tableSize;
        }

        OutputCode(code);
      }

      // Increase code size when needed
      if (tableSize >= codeMask + 1 && codeSize < 12) {
        ++codeSize;
        codeMask = (1 << codeSize) - 1;
      }

      prevCode = code;
    }

    return output;
  }

  /// <summary>
  ///     Deinterlaces GIF pixel data from 4-pass interlace order to sequential rows.
  ///     GIF interlace passes: {0,8,16,...}, {4,12,20,...}, {2,6,10,...}, {1,3,5,...}
  /// </summary>
  private static byte[] _Deinterlace(byte[] interlacedPixels, int width, int height) {
    var result = new byte[width * height];
    var sourceRow = 0;

    // Pass 1: rows 0, 8, 16, ...
    for (var y = 0; y < height; y += 8)
      _CopyRow(interlacedPixels, result, sourceRow++, y, width);

    // Pass 2: rows 4, 12, 20, ...
    for (var y = 4; y < height; y += 8)
      _CopyRow(interlacedPixels, result, sourceRow++, y, width);

    // Pass 3: rows 2, 6, 10, ...
    for (var y = 2; y < height; y += 4)
      _CopyRow(interlacedPixels, result, sourceRow++, y, width);

    // Pass 4: rows 1, 3, 5, ...
    for (var y = 1; y < height; y += 2)
      _CopyRow(interlacedPixels, result, sourceRow++, y, width);

    return result;
  }

  private static void _CopyRow(byte[] source, byte[] destination, int sourceRow, int destRow, int width) =>
    Array.Copy(source, sourceRow * width, destination, destRow * width, width);

}