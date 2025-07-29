using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class AduQuantizer(Func<Color, Color, int> distanceFunc, int iterationCount = 10) : QuantizerBase {
  private const double InitialLearningRate = 0.01; // Reduced initial learning rate
  private const double MinLearningRate = 0.001; // Minimum learning rate

  protected override Color[] _ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var colorsWithCounts = histogram.ToArray();
    
    // 1. Initialize units (palette colors)
    var palette = new List<Color>();
    
    // Initialize palette with well-distributed colors from the input histogram
    // Sort by frequency to get the most common colors first
    var sortedColors = colorsWithCounts.OrderByDescending(c => c.count).ToArray();
    
    // Initialize with most frequent colors to ensure good starting distribution
    for (var i = 0; i < numberOfColors && i < sortedColors.Length; ++i)
      palette.Add(sortedColors[i].color);
    
    // Fill remaining slots with evenly spaced colors if needed
    while (palette.Count < numberOfColors && sortedColors.Length > 0) {
      var step = Math.Max(1, sortedColors.Length / (numberOfColors - palette.Count));
      for (var i = palette.Count; i < sortedColors.Length && palette.Count < numberOfColors; i += step)
        if (!palette.Contains(sortedColors[i].color))
          palette.Add(sortedColors[i].color);

      break;
    }
    
    // Parameters for competitive learning
    for (var iteration = 0; iteration < iterationCount; ++iteration) {
      // Use exponential decay for learning rate with minimum threshold
      var learningRate = Math.Max(MinLearningRate, 
        InitialLearningRate * Math.Exp(-3.0 * iteration / iterationCount));

      // Shuffle colors each iteration to avoid bias
      colorsWithCounts.Shuffle();

      foreach (var (inputColor, count) in colorsWithCounts) {
        // Apply count-based weighting to the learning process
        var weightedLearningRate = learningRate * Math.Min(1.0, Math.Log(count + 1) / 10.0);

        var winningUnitIndex = 0;
        var minDistance = distanceFunc(inputColor, palette[0]);
        for (var i = 1; i < palette.Count; ++i) {
          var current = distanceFunc(inputColor, palette[i]);
          if (current >= minDistance)
            continue;

          minDistance = current;
          winningUnitIndex = i;
        }
        
        // Update winning unit
        var winningColor = palette[winningUnitIndex];
        var deltaR = inputColor.R - winningColor.R;
        var deltaG = inputColor.G - winningColor.G;
        var deltaB = inputColor.B - winningColor.B;
        
        var newR = (int)(winningColor.R + weightedLearningRate * deltaR);
        var newG = (int)(winningColor.G + weightedLearningRate * deltaG);
        var newB = (int)(winningColor.B + weightedLearningRate * deltaB);

        palette[winningUnitIndex] = Color.FromArgb(
          Math.Clamp(newR, 0, 255),
          Math.Clamp(newG, 0, 255),
          Math.Clamp(newB, 0, 255)
        );

        // Update neighboring units with reduced learning rate
        var neighborLearningRate = weightedLearningRate * 0.1;
        for (var i = 0; i < palette.Count; ++i) {
          if (i == winningUnitIndex) 
            continue;
          
          var neighborColor = palette[i];
          var neighborDistance = distanceFunc(winningColor, neighborColor);
          
          if (neighborDistance >= minDistance * 2)
            continue;

          // Only update close neighbors
          var neighborInfluence = neighborLearningRate * Math.Exp(-neighborDistance / 1000.0);
            
          var nR = (int)(neighborColor.R + neighborInfluence * deltaR);
          var nG = (int)(neighborColor.G + neighborInfluence * deltaG);
          var nB = (int)(neighborColor.B + neighborInfluence * deltaB);
            
          palette[i] = Color.FromArgb(
            Math.Clamp(nR, 0, 255),
            Math.Clamp(nG, 0, 255),
            Math.Clamp(nB, 0, 255)
          );
        }
      }
    }
    
    return palette.ToArray();
  }
  
}