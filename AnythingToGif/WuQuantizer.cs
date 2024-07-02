using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public class WuQuantizer : IQuantizer {

  /// <inheritdoc />
  public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, int count)> histogram) {
    var smallHistogram = new int[32, 32, 32];

    foreach (var (color, count) in histogram) {
      var r = color.R >> 3;
      var g = color.G >> 3;
      var b = color.B >> 3;
      smallHistogram[r, g, b] += count;
    }

    var cubes = new List<ColorCube> { new(smallHistogram) };
    while (cubes.Count < numberOfColors) {
      var largestCube = cubes.OrderByDescending(c => c.Volume).First();
      cubes.Remove(largestCube);

      var splitCubes = largestCube.Split();
      cubes.AddRange(splitCubes);
    }

    return cubes.Select(c => c.AverageColor).ToArray();
  }

  public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) => this.ReduceColorsTo(numberOfColors, usedColors.Select(c => (c, 1)));

  private class ColorCube(int[,,] histogram, int rMin = 0, int rMax = 31, int gMin = 0, int gMax = 31, int bMin = 0, int bMax = 31) {

    public int Volume => (rMax - rMin) * (gMax - gMin) * (bMax - bMin);

    public Color AverageColor => this.GetAverageColor();

    private Color GetAverageColor() {

      long rSum = 0, gSum = 0, bSum = 0, count = 0;
      for (var r = rMin; r <= rMax; ++r)
      for (var g = gMin; g <= gMax; ++g)
      for (var b = bMin; b <= bMax; ++b) {
        var histCount = histogram[r, g, b];
        rSum += r * histCount;
        gSum += g * histCount;
        bSum += b * histCount;
        count += histCount;
      }

      if (count == 0)
        return Color.Transparent;

      return Color.FromArgb((int)(rSum / count) << 3, (int)(gSum / count) << 3, (int)(bSum / count) << 3);
    }

    public IEnumerable<ColorCube> Split() {
      var rRange = rMax - rMin;
      var gRange = gMax - gMin;
      var bRange = bMax - bMin;

      int mid;
      if (rRange >= gRange && rRange >= bRange) {
        mid = rMin + rRange >> 1;
        return [
          new(histogram, rMin, mid, gMin, gMax, bMin, bMax),
          new(histogram, mid + 1, rMax, gMin, gMax, bMin, bMax)
        ];
      }

      if (gRange >= rRange && gRange >= bRange) {
        mid = gMin + gRange >> 1;
        return [
          new(histogram, rMin, rMax, gMin, mid, bMin, bMax),
          new(histogram, rMin, rMax, mid + 1, gMax, bMin, bMax)
        ];
      }

      mid = bMin + bRange >> 1;
      return [
        new(histogram, rMin, rMax, gMin, gMax, bMin, mid), 
        new(histogram, rMin, rMax, gMin, gMax, mid + 1, bMax)
      ];
    }
  }
}
