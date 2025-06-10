using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace AnythingToGif.Quantizers;

public abstract class QuantizerBase:IQuantizer {
  #region Implementation of IQuantizer

  /// <inheritdoc />
  public virtual Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) => this.ReduceColorsTo(numberOfColors, (IEnumerable<(Color color, uint count)>)usedColors.Select(c => (c, 1)));

  /// <inheritdoc />
  public virtual Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) => ReduceColorsTo(numberOfColors, histogram.Select(h => h.color));

  #endregion
}