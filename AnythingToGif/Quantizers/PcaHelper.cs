using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace AnythingToGif.Quantizers;

internal sealed class PcaHelper {
  private readonly Vector<double> _mean;
  private readonly Matrix<double> _eigenvectors;
  private readonly double[] _min;
  private readonly double[] _max;

  public PcaHelper(IEnumerable<Color> colors) {
    var builder = Vector<double>.Build;
    var rows = colors.Select(c => builder.Dense([c.R, c.G, c.B])).ToList();
    if (rows.Count == 0) {
      this._mean = builder.Dense(3);
      this._eigenvectors = Matrix<double>.Build.DenseIdentity(3);
      this._min = [0, 0, 0];
      this._max = [1, 1, 1];
      return;
    }

    this._mean = rows.Aggregate(builder.Dense(3), (acc, v) => acc + v) / rows.Count;
    var centered = rows.Select(v => v - this._mean).ToList();
    var matrix = Matrix<double>.Build.DenseOfRowVectors(centered);
    var cov = matrix.TransposeThisAndMultiply(matrix) / (rows.Count - 1.0);
    var evd = cov.Evd();
    this._eigenvectors = evd.EigenVectors;
    var transformed = matrix * this._eigenvectors;

    this._min = new double[3];
    this._max = new double[3];
    for (var i = 0; i < 3; ++i) {
      var column = transformed.Column(i);
      this._min[i] = column.Minimum();
      this._max[i] = column.Maximum();
      if (this._min[i] == this._max[i]) {
        this._min[i] = 0;
        this._max[i] = 1;
      }
    }
  }

  public IEnumerable<Color> TransformColors(IEnumerable<Color> colors) {
    foreach (var c in colors)
      yield return this.Transform(c);
  }

  public IEnumerable<Color> InverseTransformColors(IEnumerable<Color> colors) {
    foreach (var c in colors)
      yield return this.InverseTransform(c);
  }

  public Color Transform(Color color) {
    var vec = Vector<double>.Build.Dense([color.R, color.G, color.B]);
    var centered = vec - this._mean;
    var t = centered * this._eigenvectors;
    var r = ScaleToByte(t[0], 0);
    var g = ScaleToByte(t[1], 1);
    var b = ScaleToByte(t[2], 2);
    return Color.FromArgb(r, g, b);
  }

  public Color InverseTransform(Color color) {
    var t0 = Unscale(color.R, 0);
    var t1 = Unscale(color.G, 1);
    var t2 = Unscale(color.B, 2);
    var vec = Vector<double>.Build.Dense([t0, t1, t2]);
    var orig = this._mean + this._eigenvectors * vec;
    return Color.FromArgb(Clamp((int)Math.Round(orig[0])), Clamp((int)Math.Round(orig[1])), Clamp((int)Math.Round(orig[2])));
  }

  private int ScaleToByte(double value, int index)
    => Clamp((int)Math.Round((value - this._min[index]) / (this._max[index] - this._min[index]) * 255.0));

  private double Unscale(int value, int index)
    => value / 255.0 * (this._max[index] - this._min[index]) + this._min[index];

  private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
}
