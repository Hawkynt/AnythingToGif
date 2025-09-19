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
  private readonly bool _useSerpentine;

  private MatrixBasedDitherer(byte[,] matrix, byte divisor, bool useSerpentine = false) {
    this._matrix = matrix;
    this._divisor = divisor;
    this._useSerpentine = useSerpentine;
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

  public static IDitherer EqualFloydSteinberg { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 4 },
    { 4, 4, 4 }
  }, 16);

  public static IDitherer FalseFloydSteinberg { get; } = new MatrixBasedDitherer(new byte[,] {
    { X, 3 },
    { 3, 2 }
  }, 8);

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

  public static IDitherer StevensonArce { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, 0, X, 0, 32, 0 },
    { 12, 0, 26, 0, 30, 0, 16 },
    { 0, 12, 0, 26, 0, 12, 0 },
    { 5, 0, 12, 0, 12, 0, 5 }
  }, 200);

  public static IDitherer ShiauFan { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 4 },
    { 1, 1, 2 }
  }, 8);

  public static IDitherer ShiauFan2 { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, 0, X, 8 },
    { 1, 1, 2, 4 }
  }, 16);

  public static IDitherer Fan93 { get; } = new MatrixBasedDitherer(new byte[,] {
    { 0, X, 7 },
    { 1, 3, 5 }
  }, 16);

    public static IDitherer TwoD { get; } = new MatrixBasedDitherer(new byte[,] {
      { X, 1 },
      { 1, 0 }
    }, 2); 

    public static IDitherer Down { get; } = new MatrixBasedDitherer(new byte[,] {
      { X },
      { 1 }
    }, 1);

    public static IDitherer DoubleDown { get; } = new MatrixBasedDitherer(new byte[,] {
      { X, 0 },
      { 2, 0 },
      { 1, 1 }
    }, 4);

    public static IDitherer Diagonal { get; } = new MatrixBasedDitherer(new byte[,] {
      { X, 0 },
      { 0, 1 }
    }, 1);

    public static IDitherer VerticalDiamond { get; } = new MatrixBasedDitherer(new byte[,] {
      { 0, 0, X, 0, 0 },
      { 0, 3, 6, 3, 0 },
      { 1, 0, 2, 0, 1 }
    }, 16);
    
    public static IDitherer HorizontalDiamond { get; } = new MatrixBasedDitherer(new byte[,] {
      { X, 6, 2 },
      { 0, 3, 0 },
      { 0, 0, 1 }
    }, 12);

    public static IDitherer Diamond { get; } = new MatrixBasedDitherer(new byte[,] {
      { 0, 0, X, 6, 2 },
      { 0, 3, 6, 3, 0 },
      { 1, 0, 2, 0, 1 }
    }, 24);


  /// <summary>
  /// Creates a serpentine version of any MatrixBasedDitherer instance.
  /// </summary>
  public static IDitherer WithSerpentine(IDitherer baseDitherer) {
    if (baseDitherer is MatrixBasedDitherer matrix) {
      return new MatrixBasedDitherer(matrix._matrix, matrix._divisor, true);
    }
    throw new ArgumentException("Serpentine scanning only supported for MatrixBasedDitherer instances", nameof(baseDitherer));
  }

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {{
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var errors = new RgbError[width, height];
    var divisor = this._divisor;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var sw = Stopwatch.StartNew();
    for (var y = 0; y < height; ++y)
    {
      var reverseRow = this._useSerpentine && (y & 1) == 1;
      var xStart = reverseRow ? width - 1 : 0;
      var xEnd = reverseRow ? -1 : width;
      var xStep = reverseRow ? -1 : 1;

      for (var x = xStart; x != xEnd; x += xStep)
      {
        var offset = y * stride + x;
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

        var quantError = new RgbError
        {
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

    static int Clamp(int value)
    {
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
