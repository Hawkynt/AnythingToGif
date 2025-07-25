using System.Collections.Generic;
using System.Drawing;

namespace AnythingToGif.Quantizers;

public sealed class Mac8BitQuantizer : FixedPaletteQuantizer {
  private static Color[] _CreatePalette() {
    var list = new List<Color>(256);
    int[] rg = { 0, 36, 73, 109, 146, 182, 219, 255 };
    int[] b = { 0, 85, 170, 255 };
    foreach (var r in rg)
      foreach (var g in rg)
        foreach (var blue in b)
          list.Add(Color.FromArgb(r, g, blue));
    return list.ToArray();
  }

  private static readonly Color[] Palette = _CreatePalette();

  public Mac8BitQuantizer() : base(Palette) { }
}
