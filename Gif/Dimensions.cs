using System.Drawing;

namespace Gif;

using System;

public readonly record struct Dimensions(ushort Width, ushort Height) {

  public Dimensions(int width, int height) : this((ushort)width, (ushort)height) {
    ArgumentOutOfRangeException.ThrowIfNegative(width);
    ArgumentOutOfRangeException.ThrowIfNegative(height);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(width, ushort.MaxValue);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(height, ushort.MaxValue);
  }

  public static Dimensions FromSize(Size size) => new(size.Width, size.Height);

  public static explicit operator Dimensions(Size size) => Dimensions.FromSize(size);

}
