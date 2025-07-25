using System.Collections.Generic;
using System.Drawing;

namespace AnythingToGif.Quantizers;

public sealed class Vga256Quantizer : FixedPaletteQuantizer {
  private static Color[] _CreatePalette() {
    var list = new List<Color>(256);
    list.AddRange(Ega16Quantizer.Palette);
    int[] steps = { 0, 51, 102, 153, 204, 255 };
    foreach (var r in steps)
      foreach (var g in steps)
        foreach (var b in steps)
          list.Add(Color.FromArgb(r, g, b));
    for (var i = 0; i < 24; ++i) {
      var v = 8 + i * 10;
      list.Add(Color.FromArgb(v, v, v));
    }
    return list.ToArray();
  }

  private static readonly Color[] Palette = _CreatePalette();

  public Vga256Quantizer() : base(Palette) { }
}
