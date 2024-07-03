using System;
using System.Collections.Generic;
using System.Drawing;

namespace AnythingToGif;

public class OctreeQuantizer : QuantizerBase {

  private class Node {
    public Node?[] Children { get; } = new Node[8];
    public int ChildrenCount { get; set; }
    public ulong RSum { get; set; }
    public ulong GSum { get; set; }
    public ulong BSum { get; set; }
    public ulong ReferencesCount { get; set; }
    public ulong PixelCount { get; set; }

    public Color CreateColor() {
      var r = (double)this.RSum / this.PixelCount;
      var g = (double)this.GSum / this.PixelCount;
      var b = (double)this.BSum / this.PixelCount;

      return Color.FromArgb((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(b));
    }
    
  }

  private readonly Node _root = new();
  private int _colorsCount;
  private const int _MAX_DEPTH = 7;
  private uint _minimumReferenceCount;

  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    foreach (var (color, count) in histogram)
      this._AddColor(this._root, color, 0, count);

    return this._MergeAndGeneratePalette(numberOfColors);
  }

  private void _GetMinimumReferenceCount(Node currentNode) {
    if (currentNode.ReferencesCount < this._minimumReferenceCount)
      this._minimumReferenceCount = (uint)currentNode.ReferencesCount;

    foreach (var childNode in currentNode.Children)
      if (childNode != null)
        this._GetMinimumReferenceCount(childNode);
  }

  private void _MergeLeast(Node currentNode, uint minCount, uint maxColors) {
    if (currentNode.ReferencesCount > minCount || this._colorsCount == maxColors)
      return;

    for (var i = 0; i < currentNode.Children.Length; ++i) {
      var childNode = currentNode.Children[i];
      if (childNode is { ChildrenCount: > 0 }) {
        this._MergeLeast(childNode, minCount, maxColors);
        continue;
      }

      if (childNode == null || currentNode.ReferencesCount > minCount)
        continue;

      currentNode.PixelCount += childNode.PixelCount;
      currentNode.RSum += childNode.RSum;
      currentNode.GSum += childNode.GSum;
      currentNode.BSum += childNode.BSum;
      
      currentNode.Children[i] = null;
      --currentNode.ChildrenCount;
      --this._colorsCount;

      if (this._colorsCount >= maxColors)
        continue;

      currentNode.Children[i] = new() {
        RSum = currentNode.RSum,
        GSum = currentNode.GSum,
        BSum = currentNode.BSum,
        PixelCount = currentNode.PixelCount
      };

      ++currentNode.ChildrenCount;
      ++this._colorsCount;
      break;
    }

    if (currentNode.ChildrenCount == 0)
      ++this._colorsCount;
  }
  
  private void _FillPalette(Node currentNode, Color[] palette, ref int index) {
    if (currentNode.ChildrenCount == 0)
      palette[index++] = currentNode.CreateColor();

    foreach (var childNode in currentNode.Children)
      if (childNode != null)
        this._FillPalette(childNode, palette, ref index);
  }

  private void _SetupColor(Node node, Color color, ulong pixelCount) {
    this._colorsCount += node.ReferencesCount == 0 ? 1 : 0;

    ++node.ReferencesCount;
    ++node.PixelCount;
    //node.PixelCount += pixelCount;
    node.RSum += color.R;
    node.GSum += color.G;
    node.BSum += color.B;
  }

  private void _AddColor(Node node, Color color, int level, ulong pixelCount) {
    for (;;) {
      if (level >= OctreeQuantizer._MAX_DEPTH) {
        this._SetupColor(node, color, pixelCount);
        return;
      }

      var index = // bits: 0b00000RGB
          (((color.R >> (7 - level)) & 1) << 2)
          | (((color.G >> (7 - level)) & 1) << 1)
          | ((color.B >> (7 - level)) & 1)
        ;

      ++node.ReferencesCount;

      if (node.Children[index] == null) {
        node.Children[index] = new();
        ++node.ChildrenCount;
      }

      node = node.Children[index]!;
      ++level;
    }
  }

  private Color[] _MergeAndGeneratePalette(uint desiredColors) {
    this._minimumReferenceCount = uint.MaxValue;
    this._GetMinimumReferenceCount(this._root);
    var least = this._minimumReferenceCount;

    if (desiredColors > 2) {
      desiredColors -= 2;
      while (this._colorsCount > desiredColors) {
        this._MergeLeast(this._root, least, desiredColors);
        least += this._minimumReferenceCount;
      }
    }

    var result = new Color[this._colorsCount + 2];
    var index = 2;
    result[0] = Color.Black;
    result[1] = Color.White;
    this._FillPalette(this._root, result, ref index);
    return result;
  }

}
