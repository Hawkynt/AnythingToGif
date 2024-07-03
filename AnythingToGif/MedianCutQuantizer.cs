using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif;

public class MedianCutQuantizer : QuantizerBase {
  
  /// <inheritdoc />
  public override Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) {
    var colors = usedColors.ToList();
    var cubes = new List<ColorCube> { new(colors) };

    while (cubes.Count < numberOfColors) {
      var largestCube = cubes.OrderByDescending(c => c.Volume).First();
      cubes.Remove(largestCube);
      var splitCubes = largestCube.Split();
      cubes.AddRange(splitCubes);
    }

    return cubes.Select(c => c.AverageColor).ToArray();
  }

  
  private class ColorCube(IEnumerable<Color> colors) {
    private readonly List<Color> colors = colors.ToList();

    public int Volume => this.GetVolume();

    public Color AverageColor => this.GetAverageColor();

    private int GetVolume() {
      int rMin = this.colors.Min(c => c.R);
      int rMax = this.colors.Max(c => c.R);
      int gMin = this.colors.Min(c => c.G);
      int gMax = this.colors.Max(c => c.G);
      int bMin = this.colors.Min(c => c.B);
      int bMax = this.colors.Max(c => c.B);
      return (rMax - rMin) * (gMax - gMin) * (bMax - bMin);
    }

    private Color GetAverageColor() {
      var r = (int)this.colors.Average(c => c.R);
      var g = (int)this.colors.Average(c => c.G);
      var b = (int)this.colors.Average(c => c.B);
      return Color.FromArgb(r, g, b);
    }

    public IEnumerable<ColorCube> Split() {
      var rRange = this.colors.Max(c => c.R) - this.colors.Min(c => c.R);
      var gRange = this.colors.Max(c => c.G) - this.colors.Min(c => c.G);
      var bRange = this.colors.Max(c => c.B) - this.colors.Min(c => c.B);

      Func<Color, int> getComponent;
      if (rRange >= gRange && rRange >= bRange)
        getComponent = c => c.R;
      else if (gRange >= rRange && gRange >= bRange)
        getComponent = c => c.G;
      else
        getComponent = c => c.B;

      this.colors.Sort((c1, c2) => getComponent(c1).CompareTo(getComponent(c2)));
      var medianIndex = this.colors.Count >> 1;
      return [
        new(this.colors[..(medianIndex-1)]), 
        new(this.colors[medianIndex..])
      ];
    }
  }
}