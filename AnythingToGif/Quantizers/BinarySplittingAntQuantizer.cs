using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class BinarySplittingAntQuantizer : QuantizerBase {
 
  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var originalColorsWithCounts = histogram.ToList();

    // Step 1: Initial Palette Generation using BinarySplittingQuantizer
    var binarySplittingQuantizer = new BinarySplittingQuantizer();
    var initialPalette = binarySplittingQuantizer.ReduceColorsTo(numberOfColors, originalColorsWithCounts);

    // Step 2: Iterative Refinement using AntTreeRefiner
    return AntTreeRefiner.RefinePalette(initialPalette, originalColorsWithCounts);
  }
}