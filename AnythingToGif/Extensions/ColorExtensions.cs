using System.Drawing;

namespace AnythingToGif.Extensions;

internal static class ColorExtensions {

  public unsafe struct ColorProxy {
    private fixed byte _argb[4];

    public ColorProxy(Color source) {
      var value = source.ToArgb();
      fixed (byte* ptr = this._argb)
        *(int*)ptr = value;
    }

    public byte B => this._argb[0];
    public byte G => this._argb[1];
    public byte R => this._argb[2];
    public byte A => this._argb[3];

    public override int GetHashCode() {
      fixed (byte* ptr = this._argb)
        return *(int*)ptr;
    }
  }


  public static int FindClosestColorIndex(this ColorProxy[] palette, ColorProxy color) {
    var a1 = color.A;
    var r1 = color.R;
    var g1 = color.G;
    var b1 = color.B;
    
    var closestIndex = -1;
    var closestDistance = int.MaxValue;
    for (var i = 0; i < palette.Length; i++) {
      var paletteColor = palette[i];

      // maybe there's something better than this rude alpha check
      if (a1 != paletteColor.A)
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

      var distance = (((512 + rMean) * r) >> 8) + (g << 2) + (((767 - rMean) * b) >> 8);
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
