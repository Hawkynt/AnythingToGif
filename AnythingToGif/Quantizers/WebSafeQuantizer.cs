using System.Collections.Generic;
using System.Drawing;

namespace AnythingToGif.Quantizers;

public sealed class WebSafeQuantizer : FixedPaletteQuantizer {
  private static Color[] _CreatePalette() {
    var list = new List<Color>(216);
    int[] steps = { 0, 51, 102, 153, 204, 255 };
    foreach (var r in steps)
      foreach (var g in steps)
        foreach (var b in steps)
          list.Add(Color.FromArgb(r, g, b));
    return list.ToArray();
  }

  private static readonly Color[] Palette = _CreatePalette();

  public WebSafeQuantizer() : base(Palette) { }
}
