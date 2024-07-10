using System.Drawing;

namespace Gif;

using System;

public readonly record struct Dimensions(ushort Width, ushort Height) {

  public static Dimensions Empty { get; } = new(0, 0);

  public Dimensions(int width, int height) : this((ushort)width, (ushort)height) {
    ArgumentOutOfRangeException.ThrowIfNegative(width);
    ArgumentOutOfRangeException.ThrowIfNegative(height);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(width, ushort.MaxValue);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(height, ushort.MaxValue);
  }

  public static Dimensions FromSize(Size size) => new(size.Width, size.Height);

  public static explicit operator Dimensions(Size size) => Dimensions.FromSize(size);

  public Dimensions With(Dimensions other) => new(Math.Max(this.Width, other.Width), Math.Max(this.Height, other.Height));

}
