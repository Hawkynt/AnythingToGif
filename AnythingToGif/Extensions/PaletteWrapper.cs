using System;
using System.Collections.Generic;
using System.Drawing;
using AnythingToGif.Extensions;
using AnythingToGif.ColorDistanceMetrics;
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
      palette.AsSpan()[..i].CopyTo(this._palette);
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

/// <summary>
/// High-performance generic version of PaletteWrapper using struct-based color metrics
/// </summary>
public class PaletteWrapper<TMetric> where TMetric : struct, IColorDistanceMetric {
  private readonly Color[] _palette;
  private readonly Dictionary<Color, int> _cache;
  private readonly TMetric _metric;

  public PaletteWrapper(IEnumerable<Color> original, TMetric metric) {
    var palette = new Color[256];
    this._cache = new(512);
    this._metric = metric;

    var i = 0;
    foreach (var c in original) {
      palette[i] = c;
      this._cache.TryAdd(c, i);
      ++i;
    }

    if (i < 256) {
      this._palette = new Color[i];
      palette.AsSpan()[..i].CopyTo(this._palette);
    } else
      this._palette = palette;
  }

  public int FindClosestColorIndex(Color color) {
    if (this._cache.TryGetValue(color, out var result))
      return result;
    
    result = this.FindClosestColorIndexInPalette(color);
    lock (this._cache)
      this._cache[color] = result;

    return result;
  }

  private int FindClosestColorIndexInPalette(Color color) {
    var result = 0;
    for (int i = 1, bestDistance = this._metric.Calculate(color, this._palette[0]); i < this._palette.Length; ++i) {
      var distance = this._metric.Calculate(color, this._palette[i]);
      if (distance >= bestDistance)
        continue;

      bestDistance = distance;
      result = i;
    }

    return result;
  }
}
