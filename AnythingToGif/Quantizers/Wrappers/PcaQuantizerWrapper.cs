using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers.Wrappers;

public class PcaQuantizerWrapper(IQuantizer inner) : IQuantizer
{
    public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors)
    {
        var colors = usedColors.ToList();
        if (colors.Count == 0)
            return [];

        var pca = new PcaHelper(colors);
        var transformed = pca.TransformColors(colors);
        var quantized = inner.ReduceColorsTo(numberOfColors, transformed);
        return pca.InverseTransformColors(quantized).ToArray();
    }

    public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram)
    {
        var colors = histogram.ToList();
        if (colors.Count == 0)
            return [];

        var pca = new PcaHelper(colors.Select(c => c.color));
        var transformed = colors.Select(h => (pca.Transform(h.color), h.count));
        var quantized = inner.ReduceColorsTo(numberOfColors, transformed);
        return pca.InverseTransformColors(quantized).ToArray();
    }
}
