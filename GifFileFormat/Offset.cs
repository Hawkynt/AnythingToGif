namespace Hawkynt.GifFileFormat;

using System;
using System.Drawing;

public readonly record struct Offset(ushort X, ushort Y) {
  public static Offset None = new(0, 0);

  public Offset(int x, int y) : this((ushort)x, (ushort)y) {
    ArgumentOutOfRangeException.ThrowIfNegative(x);
    ArgumentOutOfRangeException.ThrowIfNegative(y);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(x, ushort.MaxValue);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(y, ushort.MaxValue);
  }

  public static Offset FromPoint(Point point) => new(point.X, point.Y);

  public static explicit operator Offset(Point point) => Offset.FromPoint(point);

}