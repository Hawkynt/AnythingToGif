using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers.FixedPalettes;

public sealed class Vga256Quantizer() : FixedPaletteQuantizer(Palette) {
  private static Color[] _CreatePalette() {
    var list = new List<Color>(256);
    list.AddRange(Ega16Quantizer.Palette);
    int[] steps = [0, 51, 102, 153, 204, 255];
    list.AddRange(
      from r in steps 
      from g in steps 
      from b in steps 
      select Color.FromArgb(r, g, b)
    );

    for (var i = 0; i < 24; ++i) {
      var v = 8 + i * 10;
      list.Add(Color.FromArgb(v, v, v));
    }

    return list.ToArray();
  }

  private static readonly Color[] Palette = _CreatePalette();
}
