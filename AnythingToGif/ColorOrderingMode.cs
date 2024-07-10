using System.ComponentModel;

namespace AnythingToGif;

public enum ColorOrderingMode {
  [Description("Purely random")] Random = -1,

  [Description("Ordered by usage, the most used first")]
  MostUsedFirst = 0,
  
  
  FromCenter = 1,

  [Description("Ordered by usage, the least used first")]
  LeastUsedFirst = 2,

  [Description("Ordered by luminance, the brightest colors first")]
  HighLuminanceFirst = 3,

  [Description("Ordered by luminance, the darkest colors first")]
  LowLuminanceFirst = 4,
}
