using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers.FixedPalettes;

public abstract class FixedPaletteQuantizer(IEnumerable<Color> palette) : QuantizerBase {
  private readonly Color[] _palette = palette.ToArray();

  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) => this._palette.Take(numberOfColors).ToArray();
}
