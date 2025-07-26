namespace AnythingToGif.ColorDistanceMetrics;
internal static class Weights {

  public static class LowRed {
    public const int Red = 2;
    public const int Green = 4;
    public const int Blue = 3;
    public const int Alpha = 1;
  }

  public static class HighRed {
    public const int Red = 3;
    public const int Green = 4;
    public const int Blue = 2;
    public const int Alpha = 1;
  }

  public static class BT709 {
    public const int Red = 2126;
    public const int Green = 7152;
    public const int Blue = 722;
    public const int Alpha = 10000;
    public const int Divisor = 10000;
  }

  // https://github.com/igor-bezkrovny/image-quantization/issues/4#issuecomment-235155320
  public static class Nommyde {
    public const int Red = 4984;
    public const int Green = 8625;
    public const int Blue = 2979;
    public const int Alpha = 10000;
    public const int Divisor = 10000;
  }

}
