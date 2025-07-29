using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public partial class VarianceBasedQuantizer : QuantizerBase {
  protected override Color[] _ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var colorsWithCounts = histogram.ToList();
    var cubes = new List<ColorCube> { new(colorsWithCounts) };

    while (cubes.Count < numberOfColors) {
      var largestVarianceCube = cubes.OrderByDescending(c => c.WeightedVariance).FirstOrDefault();
      if (largestVarianceCube == null || largestVarianceCube.Colors.Count == 0)
        break;

      cubes.Remove(largestVarianceCube);
      var splitCubes = largestVarianceCube.Split();
      cubes.AddRange(splitCubes);
    }

    return cubes.Select(c => c.AverageColor).ToArray();
  }
}
