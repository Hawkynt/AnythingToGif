using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace AnythingToGif.Quantizers;

public class BinarySplittingQuantizer : QuantizerBase {
  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    var colorsWithCounts = histogram.ToList();
    var cubes = new List<ColorCube> { new(colorsWithCounts) };

    while (cubes.Count < numberOfColors) {
      // Select the leaf j with the largest eigenvalue (or largest total squared variation)
      var largestEigenvalueCube = cubes.OrderByDescending(c => c.LargestEigenvalue).FirstOrDefault();

      if (largestEigenvalueCube == null || largestEigenvalueCube.Colors.Count == 0) {
        break;
      }

      cubes.Remove(largestEigenvalueCube);
      var splitCubes = largestEigenvalueCube.Split();
      cubes.AddRange(splitCubes);
    }

    return cubes.Select(c => c.AverageColor).ToArray();
  }

  private class ColorCube {
    public List<(Color color, uint count)> Colors { get; }
    public Color AverageColor { get; private set; }
    public double LargestEigenvalue { get; private set; }

    private Vector<double> _m; // sum of colors
    private double _N; // number of elements
    private Matrix<double> _R; // sum of p * p^T

    public ColorCube(List<(Color color, uint count)> colors) {
      this.Colors = colors;
      this._m = Vector<double>.Build.Dense(3); // Initialize to avoid CS8618 warning
      this._R = Matrix<double>.Build.Dense(3, 3); // Initialize to avoid CS8618 warning
      this.CalculateMetrics();
    }

    private void CalculateMetrics() {
      if (!this.Colors.Any()) {
        this.AverageColor = Color.Black;
        this.LargestEigenvalue = 0;
        this._m = Vector<double>.Build.Dense(3);
        this._N = 0;
        this._R = Matrix<double>.Build.Dense(3, 3);
        return;
      }

      long totalCount = 0;

      this._m = Vector<double>.Build.Dense(3);
      this._R = Matrix<double>.Build.Dense(3, 3);

      foreach (var (color, count) in this.Colors) {
        var p = Vector<double>.Build.Dense([color.R, color.G, color.B]);
        this._m += p * count;
        this._R += p.ToColumnMatrix() * p.ToRowMatrix() * count;
        totalCount += count;
      }

      this._N = totalCount;

      if (totalCount == 0) {
        this.AverageColor = Color.Black;
        this.LargestEigenvalue = 0;
        return;
      }

      var avgR = (int)Math.Round(this._m[0] / this._N);
      var avgG = (int)Math.Round(this._m[1] / this._N);
      var avgB = (int)Math.Round(this._m[2] / this._N);
      this.AverageColor = Color.FromArgb(avgR, avgG, avgB);

      // Calculate Covariance Matrix: R_j - (1/N_j) * m_j * m_j^T
      var covarianceMatrix = this._R - (this._m.ToColumnMatrix() * this._m.ToRowMatrix()) / this._N;

      // Perform Symmetric Eigenvalue Decomposition
      var evd = covarianceMatrix.Evd();
      this.LargestEigenvalue = evd.EigenValues.Max(c => c.Real);
    }

    public IEnumerable<ColorCube> Split() {
      if (!this.Colors.Any())
        return [];

      // Recalculate covariance matrix and eigenvectors (as they might have changed if colors were removed)
      long totalCount = 0;

      var current_m = Vector<double>.Build.Dense(3);
      var current_R = Matrix<double>.Build.Dense(3, 3);

      foreach (var (color, count) in this.Colors) {
        var p = Vector<double>.Build.Dense([color.R, color.G, color.B]);
        current_m += p * count;
        current_R += p.ToColumnMatrix() * p.ToRowMatrix() * count;
        totalCount += count;
      }

      var current_N = totalCount;
      var covarianceMatrix = current_R - (current_m.ToColumnMatrix() * current_m.ToRowMatrix()) / current_N;
      var evd = covarianceMatrix.Evd();

      // Find the principal eigenvector (corresponding to the largest eigenvalue)
      Vector<double>? principalEigenvector = null;
      double maxEigenvalue = -1;

      for (var i = 0; i < evd.EigenValues.Count; ++i) {
        if (!(evd.EigenValues[i].Real > maxEigenvalue))
          continue;

        maxEigenvalue = evd.EigenValues[i].Real;
        principalEigenvector = evd.EigenVectors.Column(i);
      }

      // Fallback if no principal eigenvector is found (should not happen with valid covariance matrix)
      if (principalEigenvector == null)
        return [this];

      // Split colors based on projection onto the principal eigenvector
      var firstHalf = new List<(Color color, uint count)>();
      var secondHalf = new List<(Color color, uint count)>();

      var meanProjection = Vector<double>.Build.Dense([this.AverageColor.R, this.AverageColor.G, this.AverageColor.B]).DotProduct(principalEigenvector);

      foreach (var item in this.Colors) {
        var p = Vector<double>.Build.Dense([item.color.R, item.color.G, item.color.B]);
        var projection = p.DotProduct(principalEigenvector);
        (projection < meanProjection ? firstHalf : secondHalf).Add(item);
      }

      // Ensure both halves are non-empty. If one is empty, it means the split was ineffective.
      if (!firstHalf.Any() || !secondHalf.Any())
        return [this];

      return [new ColorCube(firstHalf), new ColorCube(secondHalf)];
    }
  }
}
