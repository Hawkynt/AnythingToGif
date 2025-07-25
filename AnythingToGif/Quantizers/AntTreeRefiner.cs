using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AnythingToGif.Extensions;

namespace AnythingToGif.Quantizers;

public static class AntTreeRefiner {

  private const int DefaultIterations = 25; // Number of iterations for ATCQ-like refinement
  
  public static Color[] RefinePalette(Color[] initialPalette, IEnumerable<(Color color, uint count)> histogram)
    => RefinePalette(initialPalette, histogram, DefaultIterations)
    ;

  public static Color[] RefinePalette(Color[] initialPalette, IEnumerable<(Color color, uint count)> histogram, int iterations) {
    var originalColorsWithCounts = histogram.ToList();
    var result = ((IEnumerable<Color>)initialPalette).ToArray(); // Explicitly cast to resolve ambiguity

    Dictionary<Color, List<(Color color, uint count)>> newPaletteClusters = [];
    var nextPalette = new List<Color>();
    for (var i = 0; i < iterations; ++i) {
      newPaletteClusters.Clear();
      foreach (var paletteColor in result)
        newPaletteClusters[paletteColor] = [];

      // Assign each original color to the closest palette color
      foreach (var (originalColor, count) in originalColorsWithCounts) {
        var closestPaletteColor = result
          .OrderBy(pc => originalColor.EuclideanDistance(pc))
          .First()
          ;

        newPaletteClusters[closestPaletteColor].Add((originalColor, count));
      }

      // Recalculate palette colors based on assigned clusters
      nextPalette.Clear();
      foreach (var paletteColor in result) {
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
      result = nextPalette.ToArray();
    }

    return result;
  }
}