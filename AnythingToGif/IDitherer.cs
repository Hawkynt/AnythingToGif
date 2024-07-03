using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

public interface IDitherer {
  void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette);
}
