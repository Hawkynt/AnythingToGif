using System;
using System.Collections.Generic;
using System.Drawing;
using AnythingToGif.Extensions;
using ColorExtensions = AnythingToGif.Extensions.ColorExtensions;

namespace AnythingToGif;

public class PaletteWrapper {

  private readonly Color[] _palette;
  private readonly Dictionary<Color, int> _cache;
  private readonly Func<Color[], Color, int> _colorIndexFinder;

  public PaletteWrapper(IEnumerable<Color> original, Func<Color, Color, int>? metric = null) {
    var palette = new Color[256];
    this._cache = new(512);
    this._colorIndexFinder = metric == null
      ? ColorExtensions.FindClosestColorIndex
      : (p, c) => p.FindClosestColorIndex(c, metric)
      ;

    var i = 0;
    foreach (var c in original) {
      palette[i] = c;
      this._cache.TryAdd(c, i);
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
    
    result = this._colorIndexFinder(this._palette, color);
    lock(this._cache)
      this._cache[color] = result;

    return result;
  }
}
