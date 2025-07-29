using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace AnythingToGif.Quantizers;

public abstract partial class QuantizerBase:IQuantizer {
  #region Implementation of IQuantizer

  /// <inheritdoc />
  public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) {
    switch (numberOfColors) {
      case <= 0:
        return [];
      case 1:
        return [usedColors.FirstOrDefault(Color.Transparent)];
    }

    var used = usedColors.DistinctBy(c=>c.ToArgb()).ToArray();
    return this._GenerateFinalPalette(
      used.Length < numberOfColors
      ? used
      : this._ReduceColorsTo(numberOfColors, used.Select(c => (c, 1u)))
      ,numberOfColors)
      ;
  }

  /// <inheritdoc />
  public Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) {
    switch (numberOfColors) {
      case <= 0:
        return [];
      case 1:
        return [histogram.FirstOrDefault((Color.Transparent, 0U)).Item1];
    }

    var used = histogram.GroupBy(h => h.color.ToArgb()).Select(g => (color: Color.FromArgb(g.Key), g.Sum(h => h.count))).ToArray();
    return this._GenerateFinalPalette(
        used.Length < numberOfColors
          ? used.Select(h => h.color)
          : this._ReduceColorsTo(numberOfColors, used.Select(h => h.color))
        , numberOfColors)
      ;
  }

  #endregion

  private Color[] _GenerateFinalPalette(IEnumerable<Color> proposedPalette, byte numberOfColors) {
    var distinctColors = proposedPalette.DistinctBy(c => c.ToArgb()).ToArray();
    var result = new Color[numberOfColors];
    var index = 0;
    
    // Get at most numberOfColors from distinctColors
    var colorsToTake = Math.Min(distinctColors.Length, numberOfColors);
    for (; index < colorsToTake; ++index)
      result[index] = distinctColors[index];
    
    // If still color entries left, add black, white, transparent
    if (index < numberOfColors) {
      var basicColors = new[] { Color.Black, Color.White, Color.Transparent };
      foreach (var color in basicColors) {
        if (index >= numberOfColors) 
          break;

        // only add if not already there
        if (result.Take(index).All(c => c.ToArgb() != color.ToArgb()))
          result[index++] = color;

      }
    }
    
    // If still colors left, add Red,Green,Blue,Cyan,Yellow,Magenta,Gray repeatedly in varying shades
    if (index >= numberOfColors)
      return result;

    var primaryColors = new[] { Color.Red, Color.Lime, Color.Blue, Color.Cyan, Color.Yellow, Color.Magenta, Color.LightGray };
    var shadeFactors = new[] { 1.0, 0.75, 0.5, 0.25, 0.1 }; // From purest down to dark
      
    foreach (var shadeFactor in shadeFactors) {
      foreach (var baseColor in primaryColors) {
        var shadedColor = Color.FromArgb(
          (int)(baseColor.R * shadeFactor),
          (int)(baseColor.G * shadeFactor),
          (int)(baseColor.B * shadeFactor)
        );

        // only add if not already there
        if (result.Take(index).Any(c => c.ToArgb() == shadedColor.ToArgb()))
          continue;

        result[index++] = shadedColor;
        if (index >= numberOfColors)
          return result;
      }
    }
      
    // Fill any remaining slots with generated colors
    for (; index < numberOfColors; ++index)
      result[index] = Color.FromArgb(
        (index * 37) % 256,
        (index * 73) % 256,
        (index * 109) % 256
      );

    return result;
  }

  protected virtual Color[] _ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors) => this._ReduceColorsTo(numberOfColors, usedColors.Select(c => (c, 1u)));
  protected virtual Color[] _ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram) => this._ReduceColorsTo(numberOfColors, histogram.Select(h => h.color));


}