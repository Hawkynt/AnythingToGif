using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class AntRefinementWrapper(IQuantizer baseQuantizer, int iterations, Func<Color, Color, int> colorDistanceMetric)
  : QuantizerBase {

  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var originalColorsWithCounts = histogram.ToList();
    var result = baseQuantizer.ReduceColorsTo(numberOfColors, originalColorsWithCounts);

    // Then, refine the palette using the Ant-tree like iterative process
    Dictionary<Color, List<(Color color, uint count)>> newPaletteClusters = [];
    var nextPalette = new List<Color>();
    for (var i = 0; i < iterations; ++i) {
      newPaletteClusters.Clear();
      foreach (var paletteColor in result)
        newPaletteClusters[paletteColor] = [];

      // Assign each original color to the closest palette color using the provided metric
      foreach (var (originalColor, count) in originalColorsWithCounts) {
        var closestPaletteColor = result
          .OrderBy(pc => colorDistanceMetric(originalColor, pc))
          .First()
          ;

        newPaletteClusters[closestPaletteColor].Add((originalColor, count));
      }

      // Recalculate palette colors based on assigned clusters
      nextPalette.Clear();
      foreach (var paletteColor in result) {
        var cluster = newPaletteClusters[paletteColor];
        
        // If a cluster is empty, keep the old palette color
        if (!cluster.Any()) {
          nextPalette.Add(paletteColor);
          continue;
        }

        long sumR = 0, sumG = 0, sumB = 0;
        long totalCount = 0;

        foreach (var (color, count) in cluster) {
          sumR += color.R * count;
          sumG += color.G * count;
          sumB += color.B * count;
          totalCount += count;
        }

        nextPalette.Add(Color.FromArgb(
          (int)Math.Round((double)sumR / totalCount),
          (int)Math.Round((double)sumG / totalCount),
          (int)Math.Round((double)sumB / totalCount)
        ));

      }
      result = nextPalette.ToArray();
    }

    return result;
  }
}