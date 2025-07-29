using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AnythingToGif.Quantizers;

public class MedianCutQuantizer : QuantizerBase {
  
  /// <inheritdoc />
  protected override Color[] _ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) {
    var cubes = new List<ColorCube> { new(usedColors) };

    while (cubes.Count < numberOfColors) {
      var largestCube = cubes.OrderByDescending(c => c.Volume).FirstOrDefault();
      if (largestCube is not { ColorCount: > 1 }) 
        break;
      
      cubes.Remove(largestCube);
      var splitCubes = largestCube.Split();
      cubes.AddRange(splitCubes);
    }

    return cubes.Select(c => c.AverageColor).ToArray();
  }

  
  private class ColorCube(IEnumerable<Color> colors) {
    private readonly List<Color> colors = colors.ToList();

    public int Volume => this.GetVolume();
    public int ColorCount => this.colors.Count;

    public Color AverageColor => this.GetAverageColor();

    private int GetVolume() {
      if(this.colors.Count==0) 
        return 0;

      int rMin = this.colors.Min(c => c.R);
      int rMax = this.colors.Max(c => c.R);
      int gMin = this.colors.Min(c => c.G);
      int gMax = this.colors.Max(c => c.G);
      int bMin = this.colors.Min(c => c.B);
      int bMax = this.colors.Max(c => c.B);
      return (rMax - rMin) * (gMax - gMin) * (bMax - bMin);
    }

    private Color GetAverageColor() {
      if (this.colors.Count == 0) 
        return Color.Black;

      var r = (int)this.colors.Average(c => c.R);
      var g = (int)this.colors.Average(c => c.G);
      var b = (int)this.colors.Average(c => c.B);
      return Color.FromArgb(r, g, b);
    }

    public IEnumerable<ColorCube> Split() {
      if (this.colors.Count <= 1)
        return [this]; // Cannot split a cube with 1 or fewer colors
      
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
      
      // Ensure we don't create empty ranges
      if (medianIndex == 0) 
        medianIndex = 1;
      if (medianIndex >= this.colors.Count) 
        medianIndex = this.colors.Count - 1;
      
      return [
        new(this.colors[..medianIndex]), 
        new(this.colors[medianIndex..])
      ];
    }
  }
}