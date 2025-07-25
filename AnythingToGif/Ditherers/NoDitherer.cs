﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly record struct NoDitherer : IDitherer {
  
  public static IDitherer Instance { get; } = new NoDitherer();

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;

    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);
    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var color = source[x, y];
      var replacementColor = (byte)wrapper.FindClosestColorIndex(color);
      data[y * stride + x] = replacementColor;
    }
  }

}
