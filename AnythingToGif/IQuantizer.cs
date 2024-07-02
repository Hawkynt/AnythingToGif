using System.Collections.Generic;
using System.Drawing;

public interface IQuantizer {
  Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<Color> usedColors);
  Color[] ReduceColorsTo(byte numberOfColors, IEnumerable<(Color color,int count)> histogram);
}
