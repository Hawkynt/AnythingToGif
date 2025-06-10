using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly struct MatrixBasedDitherer : IDitherer {
  private readonly byte[,] _matrix;
  private readonly byte _divisor;
  private readonly byte _shift;
  private readonly byte _rowCount;
  private readonly byte _columnCount;

  private MatrixBasedDitherer(byte[,] matrix, byte divisor) {
    this._matrix = matrix;
    this._divisor = divisor;
    this._rowCount = (byte)this._matrix.GetLength(0);
    this._columnCount = (byte)this._matrix.GetLength(1);

    // find the column where the current pixel would be mapped to
    for (var i = 0; i < this._columnCount; ++i)
      if (matrix[0, i] == 0)
        ++this._shift;
      else
        break;
    
    --this._shift;
  }

  private const byte X = 0;

  public static IDitherer FloydSteinberg { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 7 },
    { 3, 5, 1 }
  }, 16);

  public static IDitherer FSEqual { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 4 },
    { 4, 4, 4 }
  }, 16);

  public static IDitherer Simple { get; } = new MatrixBasedDitherer(new byte[,] {
    { X, 1 }
  }, 1);

  public static IDitherer JarvisJudiceNinke { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 7, 5 },
    { 3, 5, 7, 5, 3 },
    { 1, 3, 5, 3, 1 }
  }, 48);

  public static IDitherer Stucki { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 8, 4 },
    { 2, 4, 8, 4, 2 },
    { 1, 2, 4, 2, 1 }
  }, 42);

  public static IDitherer Atkinson { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 1, 1 },
    { 1, 1, 1, 0 },
    { 0, 1, 0, 0 }
  }, 8);

  public static IDitherer Burkes { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 8, 4 },
    { 2, 4, 8, 4, 2 }
  }, 32);

  public static IDitherer Sierra { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 5, 3 },
    { 2, 4, 5, 4, 2 },
    { 0, 2, 3, 2, 0 }
  }, 32);

  public static IDitherer TwoRowSierra { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 4, 3 },
    { 1, 2, 3, 2, 1 }
  }, 16);

  public static IDitherer SierraLite { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 2 },
    { 1, 1, 0 }
  }, 4);

  public static IDitherer Pigeon { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 2, 1 },
    { 0, 2, 2, 2, 0 },
    { 1, 0, 1, 0, 1 }
  }, 14);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var errors = new RgbError[width, height];
    var divisor = this._divisor;
    var wrapper = new PaletteWrapper(palette);

    var sw = Stopwatch.StartNew();
    for (var y = 0; y < height; ++y) {
      var offset = y * stride;
      for (var x = 0; x < width; ++offset, ++x) {
        var oldColor = source[x, y];

        // Apply the accumulated error to the current pixel
        var rgbError = errors[x, y];
        var correctedColor = Color.FromArgb(
          Clamp(rgbError.red.FusedDivideAdd(divisor, oldColor.R)),
          Clamp(rgbError.green.FusedDivideAdd(divisor, oldColor.G)),
          Clamp(rgbError.blue.FusedDivideAdd(divisor, oldColor.B))
        );

        var closestColorIndex = (byte)wrapper.FindClosestColorIndex(correctedColor);

        var newColor = palette[closestColorIndex];
        data[offset] = closestColorIndex;

        var quantError = new RgbError {
          red = (short)(correctedColor.R - newColor.R),
          green = (short)(correctedColor.G - newColor.G),
          blue = (short)(correctedColor.B - newColor.B)
        };

        this._DistributeError(errors, x, y, quantError, width, height);
      }
    }
    sw.Stop();
    Trace.WriteLine($"Took {sw.ElapsedMilliseconds}ms");
    return;

    static int Clamp(int value) {
      // Clamp to [0, 255] using bitwise operations
      value &= ~(value >> 31);          // Sets value to 0 if negative
      value -= 255;                     // Subtract 255, negative if value <= 255
      value &= (value >> 31);           // Sets value to 0 if above 255
      return value + 255;               // Adds back 255 if clamped
    }

  }

  private void _DistributeError(RgbError[,] errors, int x, int y, RgbError error, int width, int height) {
    var rowCount = this._rowCount;
    var columnCount = this._columnCount;
    var shift = this._shift;

    for (var row = 0; row < rowCount; ++row) {
      var newY = y + row;
      if (newY < 0)
        continue;

      if (newY >= height)
        break;

      for (var column = 0; column < columnCount; ++column) {
        var newX = x + column - shift;
        if (newX < 0)
          continue;
        if (newX >= width)
          break;

        var errorDiffusionFactor = this._matrix[row, column];
        errors[newX, newY].MultiplyAdd(error, errorDiffusionFactor);
      }
    }
  }

  private struct RgbError {
    public short red;
    public short green;
    public short blue;

    public void MultiplyAdd(RgbError other, short factor) {
      this.red = other.red.FusedMultiplyAdd(factor, this.red);
      this.green = other.green.FusedMultiplyAdd(factor, this.green);
      this.blue = other.blue.FusedMultiplyAdd(factor, this.blue);
    }
  }

}
