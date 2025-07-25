using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class VarianceCutQuantizer : QuantizerBase {
  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var colorsWithCounts = histogram.ToList();
    var cubes = new List<ColorCube> { new(colorsWithCounts) };

    while (cubes.Count < numberOfColors) {
      var largestCube = cubes.OrderByDescending(c => c.SumOfSquaredError).FirstOrDefault();

      if (largestCube == null || largestCube.Colors.Count == 0) {
        // No more cubes to split or no colors left
        break;
      }

      cubes.Remove(largestCube);
      var splitCubes = largestCube.Split();
      cubes.AddRange(splitCubes);
    }

    return cubes.Select(c => c.AverageColor).ToArray();
  }

  private class ColorCube {
    public List<(Color color, uint count)> Colors { get; }
    public double SumOfSquaredError { get; private set; }
    public Color AverageColor { get; private set; }

    public ColorCube(List<(Color color, uint count)> colors) {
      Colors = colors;
      CalculateMetrics();
    }

    private void CalculateMetrics() {
      if (!Colors.Any()) {
        SumOfSquaredError = 0;
        AverageColor = Color.Black;
        return;
      }

      long sumR = 0, sumG = 0, sumB = 0;
      long totalCount = 0;

      foreach (var (color, count) in Colors) {
        sumR += color.R * count;
        sumG += color.G * count;
        sumB += color.B * count;
        totalCount += count;
      }

      if (totalCount == 0) {
        SumOfSquaredError = 0;
        AverageColor = Color.Black;
        return;
      }

      var avgR = (int)Math.Round((double)sumR / totalCount);
      var avgG = (int)Math.Round((double)sumG / totalCount);
      var avgB = (int)Math.Round((double)sumB / totalCount);
      AverageColor = Color.FromArgb(avgR, avgG, avgB);

      double sse = 0;
      foreach (var (color, count) in Colors) {
        sse += (Math.Pow(color.R - avgR, 2) + Math.Pow(color.G - avgG, 2) + Math.Pow(color.B - avgB, 2)) * count;
      }
      SumOfSquaredError = sse;
    }

    public IEnumerable<ColorCube> Split() {
      if (!Colors.Any()) {
        return Enumerable.Empty<ColorCube>();
      }

      // Find the axis with the greatest variance
      double varianceR = CalculateVariance(c => c.R);
      double varianceG = CalculateVariance(c => c.G);
      double varianceB = CalculateVariance(c => c.B);

      Func<Color, int> getComponent;
      if (varianceR >= varianceG && varianceR >= varianceB) {
        getComponent = c => c.R;
      } else if (varianceG >= varianceR && varianceG >= varianceB) {
        getComponent = c => c.G;
      } else {
        getComponent = c => c.B;
      }

      // Sort colors by the selected component
      Colors.Sort((c1, c2) => getComponent(c1.color).CompareTo(getComponent(c2.color)));

      // Split at the mean point along the selected axis
      var meanComponent = getComponent(AverageColor);
      var splitIndex = Colors.FindIndex(item => getComponent(item.color) >= meanComponent);

      if (splitIndex == -1 || splitIndex == 0 || splitIndex == Colors.Count) {
        // If mean is outside the range or all colors are on one side,
        // fall back to median split to ensure progress
        splitIndex = Colors.Count / 2;
      }

      var firstHalf = Colors.Take(splitIndex).ToList();
      var secondHalf = Colors.Skip(splitIndex).ToList();

      return [new ColorCube(firstHalf), new ColorCube(secondHalf)];
    }

    private double CalculateVariance(Func<Color, int> selector) {
      if (!Colors.Any()) return 0;

      long sum = 0;
      long totalCount = 0;
      foreach (var (color, count) in Colors) {
        sum += selector(color) * count;
        totalCount += count;
      }

      if (totalCount == 0) return 0;

      var mean = (double)sum / totalCount;
      double variance = 0;
      foreach (var (color, count) in Colors) {
        variance += Math.Pow(selector(color) - mean, 2) * count;
      }
      return variance / totalCount;
    }
  }
}
