using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using AnythingToGif;

public readonly struct MatrixBasedDitherer : IDitherer {
  private readonly int[,] _matrix;
  private readonly int _divider;

  private MatrixBasedDitherer(int[,] matrix, int divider) {
    this._matrix = matrix;
    this._divider = divider;
  }

  public static IDitherer FloydSteinberg { get; } = new MatrixBasedDitherer(new[,] { 
    { 0, 0, 7 }, 
    { 3, 5, 1 }
  }, 16);

  public static IDitherer Simple { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 1 }
  }, 1);

  public static IDitherer JarvisJudiceNinke { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 0, 7, 5 },
    { 3, 5, 7, 5, 3 },
    { 1, 3, 5, 3, 1 }
  }, 48);

  public static IDitherer Stucki { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 0, 8, 4 },
    { 2, 4, 8, 4, 2 },
    { 1, 2, 4, 2, 1 }
  }, 42);

  public static IDitherer Atkison { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 1, 1 },
    { 1, 1, 1, 0 },
    { 0, 1, 0, 0 }
  }, 8);

  public static IDitherer Burkes { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 0, 8, 4 },
    { 2, 4, 8, 4, 2 }
  }, 32);

  public static IDitherer Sierra { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 0, 5, 3 },
    { 2, 4, 5, 4, 2 },
    { 0, 2, 3, 2, 0 }
  }, 32);

  public static IDitherer TwoRowSierra { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 0, 4, 3 },
    { 1, 2, 3, 2, 1 }
  }, 16);

  public static IDitherer SierraLite { get; } = new MatrixBasedDitherer(new[,] {
    { 0, 0, 2 },
    { 1, 1, 0 }
  }, 4);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var errors = new RgbError[width, height];

    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++) {
      var oldColor = source[x, y];

      // Apply the accumulated error to the current pixel
      var correctedColor = Color.FromArgb(
        Clamp(oldColor.R + errors[x, y].red),
        Clamp(oldColor.G + errors[x, y].green),
        Clamp(oldColor.B + errors[x, y].blue)
      );

      var closestColorIndex = (byte)palette.FindClosestColorIndex(correctedColor);
      var newColor = palette[closestColorIndex];
      data[y * stride + x] = closestColorIndex;

      var quantError = new RgbError {
        red = correctedColor.R - newColor.R,
        green = correctedColor.G - newColor.G,
        blue = correctedColor.B - newColor.B
      };

      this.DistributeError(errors, x, y, quantError, width, height);
    }

    return;

    static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

  }

  // TODO: something seems off here with center index
  private void DistributeError(RgbError[,] errors, int x, int y, RgbError error, int width, int height) {
    var rowCount = this._matrix.GetLength(0);
    var columnCount = this._matrix.GetLength(1);
    for (var row = 0; row < rowCount; ++row) {
      for (var column = 0; column < columnCount; ++column) {
        var newX = x + column - 1;
        var newY = y + row;
        if (newX < 0 || newX >= width || newY < 0 || newY >= height)
          continue;

        errors[newX, newY].red += error.red * this._matrix[row, column] / this._divider;
        errors[newX, newY].green += error.green * this._matrix[row, column] / this._divider;
        errors[newX, newY].blue += error.blue * this._matrix[row, column] / this._divider;
      }
    }
  }

  private struct RgbError {
    public int red;
    public int green;
    public int blue;
  }

}
