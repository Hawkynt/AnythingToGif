using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers.FixedPalettes;

public sealed class WebSafeQuantizer() : FixedPaletteQuantizer(Palette) {
  private static Color[] _CreatePalette() {
    int[] steps = [0, 51, 102, 153, 204, 255];
    
    return (
      from r in steps
      from g in steps
      from b in steps
      select Color.FromArgb(r, g, b)
    ).ToArray();
  }

  private static readonly Color[] Palette = _CreatePalette();
}
