using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AnythingToGif.Extensions;

namespace AnythingToGif.Quantizers;

public class WuAntQuantizer : QuantizerBase {
  private const int DefaultIterations = 25; // Number of iterations for ATCQ-like refinement

  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var originalColorsWithCounts = histogram.ToList();

    // Step 1: Initial Palette Generation using WuQuantizer
    var wuQuantizer = new WuQuantizer();
    var currentPalette = wuQuantizer.ReduceColorsTo(numberOfColors, originalColorsWithCounts);

    // Step 2: Iterative Refinement (Simplified ATCQ-like approach)
    Dictionary<Color, List<(Color color, uint count)>> newPaletteClusters = [];
    for (var i = 0; i < DefaultIterations; ++i) {
      newPaletteClusters.Clear();
      foreach (var paletteColor in currentPalette)
        newPaletteClusters[paletteColor] = [];

      // Assign each original color to the closest palette color
      foreach (var (originalColor, count) in originalColorsWithCounts) {
        var closestPaletteColor = currentPalette
          .OrderBy(pc => originalColor.EuclideanDistance(pc))
          .First()
          ;

        newPaletteClusters[closestPaletteColor].Add((originalColor, count));
      }

      // Recalculate palette colors based on assigned clusters
      var nextPalette = new List<Color>();
      foreach (var paletteColor in currentPalette) {
        var cluster = newPaletteClusters[paletteColor];
        
        // If a cluster is empty, keep the old palette color or reinitialize (e.g., to black)
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
      currentPalette = nextPalette.ToArray();
    }

    return currentPalette;
  }
}
