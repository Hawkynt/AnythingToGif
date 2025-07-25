using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers.FixedPalettes;

public sealed class Mac8BitQuantizer() : FixedPaletteQuantizer(Palette) {
  private static Color[] _CreatePalette() {
    int[] rg = [0, 36, 73, 109, 146, 182, 219, 255];
    int[] b = [0, 85, 170, 255];
    return (
      from r in rg 
      from g in rg 
      from blue in b 
      select Color.FromArgb(r, g, blue)
    ).ToArray();
  }

  private static readonly Color[] Palette = _CreatePalette();
}
