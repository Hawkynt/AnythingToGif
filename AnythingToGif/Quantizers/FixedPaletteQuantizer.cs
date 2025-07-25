using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public abstract class FixedPaletteQuantizer : QuantizerBase {
  private readonly Color[] _palette;

  protected FixedPaletteQuantizer(IEnumerable<Color> palette) {
    this._palette = palette.ToArray();
  }

  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) =>
    this._palette.Take(numberOfColors).ToArray();
}
