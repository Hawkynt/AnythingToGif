using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class PcaQuantizerWrapper : IQuantizer {
  private readonly IQuantizer _inner;

  public PcaQuantizerWrapper(IQuantizer inner) {
    this._inner = inner;
  }

  public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) {
    var colors = usedColors.ToList();
    if (colors.Count == 0)
      return [];
    var pca = new PcaHelper(colors);
    var transformed = pca.TransformColors(colors);
    var quantized = this._inner.ReduceColorsTo(numberOfColors, transformed);
    return pca.InverseTransformColors(quantized).ToArray();
  }

  public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var colors = histogram.Select(h => h.color).ToList();
    if (colors.Count == 0)
      return [];
    var pca = new PcaHelper(colors);
    var transformed = histogram.Select(h => (pca.Transform(h.color), h.count));
    var quantized = this._inner.ReduceColorsTo(numberOfColors, transformed);
    return pca.InverseTransformColors(quantized).ToArray();
  }
}
