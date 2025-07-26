using System.Drawing;

namespace AnythingToGif.ColorDistanceMetrics;

public interface IColorDistanceMetric {
  int Calculate(Color self, Color other);
}
