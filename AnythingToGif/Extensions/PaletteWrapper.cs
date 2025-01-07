using System;
using System.Collections.Generic;
using System.Drawing;
using AnythingToGif.Extensions;

namespace AnythingToGif;

public class PaletteWrapper {

  private readonly Color[] _palette;
  
  private readonly Dictionary<Color, int> _cache;

  public PaletteWrapper(IEnumerable<Color> original) {
    var palette = new Color[256];
    this._cache = new Dictionary<Color, int>(512);

    var i = 0;
    foreach (var c in original) {
      palette[i] = c;
      this._cache.Add(c, i);
      ++i;
    }

    if (i < 256) {
      this._palette = new Color[i];
      Array.Copy(palette, this._palette, i);
    } else
      this._palette = palette;

  }

  public int FindClosestColorIndex(Color color) {
    if(this._cache.TryGetValue(color, out var result))
      return result;
    
    result = this._palette.FindClosestColorIndex(color);
    lock(this._cache)
      this._cache[color] = result;

    return result;
  }
}
