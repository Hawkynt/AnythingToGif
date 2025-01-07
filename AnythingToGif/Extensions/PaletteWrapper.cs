using System;
using System.Collections.Generic;
using System.Drawing;
using AnythingToGif.Extensions;
using ColorExtensions = AnythingToGif.Extensions.ColorExtensions;

namespace AnythingToGif;

public class PaletteWrapper {

  private readonly ColorExtensions.ColorProxy[] _palette;
  
  private readonly Dictionary<Color, int> _cache;

  public PaletteWrapper(IEnumerable<Color> original) {
    var palette = new ColorExtensions.ColorProxy[256];
    this._cache = new Dictionary<Color, int>(512);

    var i = 0;
    foreach (var c in original) {
      palette[i] = new(c);
      this._cache.Add(c, i);
      ++i;
    }

    if (i < 256) {
      this._palette = new ColorExtensions.ColorProxy[i];
      Array.Copy(palette, this._palette, i);
    } else
      this._palette = palette;

  }

  public int FindClosestColorIndex(Color color) {
    if(this._cache.TryGetValue(color, out var result))
      return result;

    result = this._palette.FindClosestColorIndex(new ColorExtensions.ColorProxy(color));
    lock(this._cache)
      this._cache[color] = result;

    return result;
  }
}
