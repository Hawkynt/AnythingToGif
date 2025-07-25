using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class AduQuantizer(Func<Color, Color, int> distanceFunc, int iterationCount = 100) : QuantizerBase {
  private const double InitialLearningRate = 0.1; // Initial learning rate

  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var colorsWithCounts = histogram.ToArray();
    if (!colorsWithCounts.Any())
      return [];
    
    // 1. Initialize units (palette colors)
    var palette = new List<Color>();
    
    // Initialize palette with random colors from the input histogram
    colorsWithCounts.Shuffle();

    // the original algorithm started with only one random color
    palette.AddRange(colorsWithCounts.Take(numberOfColors).Select(c=>c.color));
    
    // Parameters for competitive learning
    for (var iteration = 0; iteration < iterationCount; ++iteration) {
      var learningRate = InitialLearningRate * (1.0 - (double)iteration / iterationCount);
      foreach (var (inputColor, count) in colorsWithCounts) {

        var winningUnitIndex = 0;
        for (int i = 1, delta = distanceFunc(inputColor, palette[winningUnitIndex]); i < palette.Count; ++i) {
          var current = distanceFunc(inputColor, palette[i]);
          if (current >= delta)
            continue;

          delta=current;
          winningUnitIndex=i;
        }


        var winningColor = palette[winningUnitIndex];
        var newR = (int)(winningColor.R + learningRate * (inputColor.R - winningColor.R));
        var newG = (int)(winningColor.G + learningRate * (inputColor.G - winningColor.G));
        var newB = (int)(winningColor.B + learningRate * (inputColor.B - winningColor.B));

        // the original algorithm also created new colors in the palette somehow

        palette[winningUnitIndex] = Color.FromArgb(
          Math.Clamp(newR, 0, 255),
          Math.Clamp(newG, 0, 255),
          Math.Clamp(newB, 0, 255)
        );

      }
    }

    return palette.ToArray();
  }

}