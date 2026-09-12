using System;
using System.Collections.Generic;
using System.Drawing;
using FileFormat.Gif;

namespace AnythingToGif.Gif;

/// <summary>Bridges <see cref="System.Drawing"/> types onto the GIF codec in
/// <c>Hawkynt.FileFormats.Images</c>.</summary>
/// <remarks>
/// The codec is a <c>net8.0</c> library that speaks in packed RGB byte triplets and takes no
/// dependency on GDI+, which is the reason it runs anywhere. This converter stays here rather than
/// moving into that package: a <see cref="Bitmap"/> or a <see cref="Color"/> in its signatures would
/// put <c>System.Drawing.Common</c> in front of every consumer of an otherwise portable image
/// library, and the conversion is a handful of lines that only a GDI+-based tool needs.
/// </remarks>
public static class GifInterop {

  /// <summary>The codec's <see cref="Dimensions"/> for a GDI+ <see cref="Size"/>.</summary>
  public static Dimensions ToDimensions(this Size size) => new(size.Width, size.Height);

  /// <summary>The codec's <see cref="Offset"/> for a GDI+ <see cref="Point"/>.</summary>
  public static Offset ToOffset(this Point point) => new(point.X, point.Y);

  /// <summary>A colour list as the packed RGB triplets a GIF colour table is made of. The codec pads
  /// the table out to the power-of-two entry count the format requires, so a list of any length
  /// works.</summary>
  public static byte[] ToColorTable(this IReadOnlyList<Color> colors) {
    ArgumentNullException.ThrowIfNull(colors);
    var table = new byte[colors.Count * 3];
    for (var i = 0; i < colors.Count; ++i) {
      table[i * 3] = colors[i].R;
      table[i * 3 + 1] = colors[i].G;
      table[i * 3 + 2] = colors[i].B;
    }

    return table;
  }

  /// <summary>A GIF colour table read back as GDI+ colours.</summary>
  public static Color[] ToColors(this byte[]? colorTable) {
    if (colorTable == null)
      return [];

    var colors = new Color[colorTable.Length / 3];
    for (var i = 0; i < colors.Length; ++i)
      colors[i] = Color.FromArgb(255, colorTable[i * 3], colorTable[i * 3 + 1], colorTable[i * 3 + 2]);

    return colors;
  }
}
