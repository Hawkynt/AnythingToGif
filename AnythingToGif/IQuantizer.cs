using System.Collections.Generic;
using System.Drawing;

namespace AnythingToGif;

public interface IQuantizer {
  Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors);
  Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color, uint count)> histogram);
}
