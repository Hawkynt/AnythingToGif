namespace Hawkynt.GifFileFormat;

using System;

public readonly struct LoopCount(ushort? value) {

  public ushort Value => value ?? throw new InvalidOperationException("No loop count specified");
  public bool IsInfinite => value == 0;
  public bool IsSet => value != null;
  public bool IsNotSet => value == null;

  public static implicit operator LoopCount(ushort? value) => new(value);
  public static implicit operator LoopCount(ushort value) => new(value);

  public static readonly LoopCount NotSet = new(null);
  public static readonly LoopCount Infinite = new(0);
  public static readonly LoopCount Once = new(1);
  public static readonly LoopCount Twice = new(2);

}