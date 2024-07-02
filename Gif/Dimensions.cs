using System.Drawing;

namespace Gif;

using System;

public readonly record struct Dimensions(ushort Width, ushort Height) {

  public static Dimensions FromSize(Size size) {
    ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
    ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));
    ArgumentOutOfRangeException.ThrowIfGreaterThan(size.Width, ushort.MaxValue, nameof(size));
    ArgumentOutOfRangeException.ThrowIfGreaterThan(size.Height, ushort.MaxValue, nameof(size));
    return new((ushort)size.Width, (ushort)size.Height);
  }

  public static explicit operator Dimensions(Size size) => Dimensions.FromSize(size);

}
