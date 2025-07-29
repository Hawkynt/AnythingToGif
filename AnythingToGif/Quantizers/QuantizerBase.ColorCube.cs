using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

partial class QuantizerBase {
  
  protected class ColorCube {
    public List<(Color color, uint count)> Colors { get; }
    public double WeightedVariance { get; private set; }
    public Color AverageColor { get; private set; }

    public ColorCube(List<(Color color, uint count)> colors) {
      this.Colors = colors;
      this.CalculateMetrics();
    }

    private void CalculateMetrics() {
      if (!this.Colors.Any()) {
        this.WeightedVariance = 0;
        this.AverageColor = Color.Black;
        return;
      }

      long sumR = 0, sumG = 0, sumB = 0;
      long totalCount = 0;

      foreach (var (color, count) in this.Colors) {
        sumR += color.R * count;
        sumG += color.G * count;
        sumB += color.B * count;
        totalCount += count;
      }

      if (totalCount == 0) {
        this.WeightedVariance = 0;
        this.AverageColor = Color.Black;
        return;
      }

      var avgR = (int)Math.Round((double)sumR / totalCount);
      var avgG = (int)Math.Round((double)sumG / totalCount);
      var avgB = (int)Math.Round((double)sumB / totalCount);
      this.AverageColor = Color.FromArgb(avgR, avgG, avgB);

      // Calculate projected variances for R, G, B
      var sigmaR2 = this.CalculateProjectedVariance(c => c.R, avgR);
      var sigmaG2 = this.CalculateProjectedVariance(c => c.G, avgG);
      var sigmaB2 = this.CalculateProjectedVariance(c => c.B, avgB);

      // Weighted Variance (Equation 9 from paper)
      this.WeightedVariance = (sigmaR2 + sigmaG2 + sigmaB2) * totalCount; // WL * (sigma_r^2 + sigma_g^2 + sigma_b^2)
    }

    private double CalculateProjectedVariance(Func<Color, int> selector, int mean) {
      double variance = 0;
      foreach (var (color, count) in this.Colors)
        variance += Math.Pow(selector(color) - mean, 2) * count;

      return variance / this.Colors.Sum(c => c.count);
    }

    public IEnumerable<ColorCube> Split() {
      if (!this.Colors.Any()) {
        return Enumerable.Empty<ColorCube>();
      }

      // Initialize with default values to avoid null warnings
      Func<Color, int> splitComponent = c => c.R; // Default to R component
      int optimalThreshold = this.Colors.Min(c => c.color.R); // Default to min R value
      var minWeightedSum = double.MaxValue;

      // Calculate projected distributions and their properties for each axis
      var rDist = this.GetProjectedDistribution(c => c.R);
      var gDist = this.GetProjectedDistribution(c => c.G);
      var bDist = this.GetProjectedDistribution(c => c.B);

      var rSplit = FindOptimalSplit(rDist, this.AverageColor.R);
      var gSplit = FindOptimalSplit(gDist, this.AverageColor.G);
      var bSplit = FindOptimalSplit(bDist, this.AverageColor.B);

      // Choose the axis with the smallest weighted sum of projected variances
      // Only update if a valid split was found for that axis (i.e., WeightedSum is not MaxValue)
      if (rSplit.WeightedSum < minWeightedSum) {
        minWeightedSum = rSplit.WeightedSum;
        splitComponent = c => c.R;
        optimalThreshold = rSplit.OptimalThreshold;
      }

      if (gSplit.WeightedSum < minWeightedSum) {
        minWeightedSum = gSplit.WeightedSum;
        splitComponent = c => c.G;
        optimalThreshold = gSplit.OptimalThreshold;
      }

      if (bSplit.WeightedSum < minWeightedSum) {
        minWeightedSum = bSplit.WeightedSum;
        splitComponent = c => c.B;
        optimalThreshold = bSplit.OptimalThreshold;
      }

      // If no valid split was found (minWeightedSum is still MaxValue), return the current cube as is.
      // This prevents infinite loops if a cube cannot be split further.
      if (minWeightedSum == double.MaxValue)
        return [this];

      // Split the colors at the optimal threshold along the chosen axis
      var firstHalf = new List<(Color color, uint count)>();
      var secondHalf = new List<(Color color, uint count)>();

      foreach (var item in this.Colors) {
        if (splitComponent(item.color) < optimalThreshold) {
          firstHalf.Add(item);
        } else {
          secondHalf.Add(item);
        }
      }

      // Ensure both halves are non-empty. If one is empty, it means the split was ineffective.
      // In such a case, return the original cube as a single element to prevent infinite loops.
      if (!firstHalf.Any() || !secondHalf.Any())
        return [this];

      return [new ColorCube(firstHalf), new ColorCube(secondHalf)];
    }

    private Dictionary<int, uint> GetProjectedDistribution(Func<Color, int> selector) {
      var distribution = new Dictionary<int, uint>();
      foreach (var (color, count) in this.Colors) {
        var componentValue = selector(color);
        if (!distribution.TryAdd(componentValue, count))
          distribution[componentValue] += count;

      }
      return distribution;
    }

    private static (int OptimalThreshold, double WeightedSum) FindOptimalSplit(Dictionary<int, uint> distribution, int mean) {
      var minWeightedSum = double.MaxValue;
      var optimalThreshold = 0;

      var sortedValues = distribution.Keys.OrderBy(v => v).ToList();
      foreach (var threshold in sortedValues) {

        // Split distribution into two parts based on threshold
        var threshold1 = threshold;
        var dist1 = distribution.Where(kv => kv.Key < threshold1).ToDictionary(kv => kv.Key, kv => kv.Value);
        var dist2 = distribution.Where(kv => kv.Key >= threshold).ToDictionary(kv => kv.Key, kv => kv.Value);

        if (!dist1.Any() || !dist2.Any()) 
          continue; // Must have two non-empty parts

        var sum1 = dist1.Sum(kv => (double)kv.Key * kv.Value);
        long count1 = dist1.Sum(kv => kv.Value);
        var mean1 = count1 > 0 ? sum1 / count1 : 0;

        var sum2 = dist2.Sum(kv => (double)kv.Key * kv.Value);
        long count2 = dist2.Sum(kv => kv.Value);
        var mean2 = count2 > 0 ? sum2 / count2 : 0;

        // Calculate weighted sum of projected variances (Equation 11 from paper)
        var weightedSum = dist1.Sum(kv => Math.Pow(kv.Key - mean1, 2) * kv.Value) + dist2.Sum(kv => Math.Pow(kv.Key - mean2, 2) * kv.Value);
        if (!(weightedSum < minWeightedSum))
          continue;

        minWeightedSum = weightedSum;
        optimalThreshold = threshold;
      }

      return (optimalThreshold, minWeightedSum);
    }
  }
}
