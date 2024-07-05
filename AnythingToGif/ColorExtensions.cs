using System.Collections.Generic;
using System.Drawing;

namespace AnythingToGif;

public static class ColorExtensions {

  public static int FindClosestColorIndex(this IEnumerable<Color> palette, Color color) {
    var closestIndex = 0;
    var closestDistance = double.MaxValue;

    var r1 = color.R;
    var g1 = color.G;
    var b1 = color.B;
    var i = -1;
    foreach (var paletteColor in palette) {
      ++i;
      
      // maybe there's something better than this rude alpha check
      if(color.A!= paletteColor.A) 
        continue;
      
      var r2 = paletteColor.R;
      var g2 = paletteColor.G;
      var b2 = paletteColor.B;

      var rMean = (r1 + r2) >> 1;
      var r = r1 - r2;
      var g = g1 - g2;
      var b = b1 - b2;
      r *= r;
      g *= g;
      b *= b;

      var distance = (((512 + rMean) * r) >> 8) + 4 * g + (((767 - rMean) * b) >> 8);
      if (distance >= closestDistance)
        continue;

      if (distance <= 1)
        return i;

      closestDistance = distance;
      closestIndex = i;
    }

    return closestIndex;
  }

}
