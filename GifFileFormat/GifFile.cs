using System.Collections.Generic;
using System.Drawing;

namespace Hawkynt.GifFileFormat;

/// <summary>Represents a fully parsed GIF file.</summary>
public sealed class GifFile(
  string version,
  Dimensions logicalScreenSize,
  Color[]? globalColorTable,
  LoopCount loopCount,
  byte backgroundColorIndex,
  IReadOnlyList<Frame> frames) {
  public string Version { get; } = version;
  public Dimensions LogicalScreenSize { get; } = logicalScreenSize;
  public Color[]? GlobalColorTable { get; } = globalColorTable;
  public LoopCount LoopCount { get; } = loopCount;
  public byte BackgroundColorIndex { get; } = backgroundColorIndex;
  public IReadOnlyList<Frame> Frames { get; } = frames;
}
