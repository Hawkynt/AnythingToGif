namespace Gif;

using System;
using System.Drawing;

public readonly record struct Offset(ushort X, ushort Y) {
  public static Offset None = new(0, 0);

  public static Offset FromPoint(Point point) {
    ArgumentOutOfRangeException.ThrowIfNegative(point.X,nameof(point));
    ArgumentOutOfRangeException.ThrowIfNegative(point.Y, nameof(point));
    ArgumentOutOfRangeException.ThrowIfGreaterThan(point.X,ushort.MaxValue, nameof(point));
    ArgumentOutOfRangeException.ThrowIfGreaterThan(point.Y, ushort.MaxValue, nameof(point));
    return new((ushort)point.X, (ushort)point.Y);
  }

  public static explicit operator Offset(Point point) => FromPoint(point);

}