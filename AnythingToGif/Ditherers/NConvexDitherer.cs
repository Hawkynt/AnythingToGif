using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

/// <summary>
///   N-Convex dithering algorithm that finds the N closest colors and creates
///   a convex hull in color space, then selects colors within this convex region
///   to achieve better color mixing and gradients.
///   This algorithm is particularly effective for smooth gradients and natural images
///   where intermediate colors can be approximated by mixing nearby palette colors.
/// </summary>
public readonly struct NConvexDitherer(int n, NConvexDitherer.ConvexStrategy strategy = NConvexDitherer.ConvexStrategy.Barycentric) : IDitherer {
  /// <summary>
  ///   Strategy for selecting colors within the convex hull.
  /// </summary>
  public enum ConvexStrategy {
    /// <summary>Use barycentric coordinates to weight colors by distance</summary>
    Barycentric,

    /// <summary>Project original color onto convex hull surface</summary>
    Projection,

    /// <summary>Use spatial dithering pattern within convex hull</summary>
    SpatialPattern,

    /// <summary>Random selection weighted by convex position</summary>
    WeightedRandom
  }

  /// <summary>
  ///   Creates an N-Convex ditherer with barycentric weighting using 4 closest colors.
  /// </summary>
  public static IDitherer Default { get; } = new NConvexDitherer(4, ConvexStrategy.Barycentric);

  /// <summary>
  ///   Creates an N-Convex ditherer with projection strategy using 6 closest colors.
  ///   Projects the original color onto the convex hull surface.
  /// </summary>
  public static IDitherer Projection6 { get; } = new NConvexDitherer(6, ConvexStrategy.Projection);

  /// <summary>
  ///   Creates an N-Convex ditherer with spatial pattern using 3 closest colors.
  ///   Uses spatial coordinates to select within the convex hull.
  /// </summary>
  public static IDitherer SpatialPattern3 { get; } = new NConvexDitherer(3, ConvexStrategy.SpatialPattern);

  /// <summary>
  ///   Creates an N-Convex ditherer with weighted random selection using 5 closest colors.
  /// </summary>
  public static IDitherer WeightedRandom5 { get; } = new NConvexDitherer(5, ConvexStrategy.WeightedRandom);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var random = new Random(42); // Fixed seed for reproducibility

    for (var y = 0; y < height; ++y) {
      var offset = y * stride;

      for (var x = 0; x < width; ++offset, ++x) {
        var originalColor = source[x, y];

        // Find N closest colors
        var closestColors = FindNClosestColors(originalColor, palette, wrapper, n);

        if (closestColors.Count == 0) {
          data[offset] = 0; // Default to index 0 for empty palette
          continue;
        }

        if (closestColors.Count == 1) {
          data[offset] = (byte)closestColors[0].index;
          continue;
        }

        // Select color using convex hull strategy
        var selectedIndex = SelectColorFromConvexHull(closestColors, originalColor, palette, x, y, random);
        data[offset] = (byte)selectedIndex;
      }
    }
  }

  private static List<(int index, double distance)> FindNClosestColors(
    Color originalColor,
    IReadOnlyList<Color> palette,
    PaletteWrapper wrapper,
    int n) {
    if (palette.Count == 0) return new List<(int, double)>();

    var distances = new List<(int index, double distance)>(palette.Count);

    // Calculate distances to all colors using simple RGB distance
    for (var i = 0; i < palette.Count; ++i) {
      var color = palette[i];
      var rDiff = originalColor.R - color.R;
      var gDiff = originalColor.G - color.G;
      var bDiff = originalColor.B - color.B;
      var distance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
      distances.Add((i, distance));
    }

    // Sort by distance and take N closest
    return distances
      .OrderBy(d => d.distance)
      .Take(Math.Min(n, palette.Count))
      .ToList();
  }

  private int SelectColorFromConvexHull(
    List<(int index, double distance)> closestColors,
    Color originalColor,
    IReadOnlyList<Color> palette,
    int x, int y,
    Random random) {
    return strategy switch {
      ConvexStrategy.Barycentric => SelectByBarycentric(closestColors, originalColor, palette),
      ConvexStrategy.Projection => SelectByProjection(closestColors, originalColor, palette),
      ConvexStrategy.SpatialPattern => SelectBySpatialPattern(closestColors, x, y),
      ConvexStrategy.WeightedRandom => SelectByWeightedRandom(closestColors, originalColor, palette, random),
      _ => closestColors[0].index
    };
  }

  private static int SelectByBarycentric(
    List<(int index, double distance)> closestColors,
    Color originalColor,
    IReadOnlyList<Color> palette) {
    // Calculate barycentric weights based on distance
    var totalInverseDistance = 0.0;
    var weights = new double[closestColors.Count];

    for (var i = 0; i < closestColors.Count; ++i) {
      // Use inverse distance weighting (closer colors have more influence)
      var weight = 1.0 / (closestColors[i].distance + 1.0); // +1 to avoid division by zero
      weights[i] = weight;
      totalInverseDistance += weight;
    }

    // Normalize weights
    for (var i = 0; i < weights.Length; ++i)
      weights[i] /= totalInverseDistance;

    // Find the color with highest weight, considering spatial variation
    var bestIndex = 0;
    var bestWeight = weights[0];

    for (var i = 1; i < weights.Length; ++i)
      if (weights[i] > bestWeight) {
        bestWeight = weights[i];
        bestIndex = i;
      }

    return closestColors[bestIndex].index;
  }

  private static int SelectByProjection(
    List<(int index, double distance)> closestColors,
    Color originalColor,
    IReadOnlyList<Color> palette) {
    return closestColors.Count switch {
      // Project original color onto the line/plane formed by closest colors
      // Linear interpolation between two colors
      2 => ProjectOntoLine(closestColors, originalColor, palette),
      // Project onto triangle/polygon formed by colors
      >= 3 => ProjectOntoPolygon(closestColors, originalColor, palette),
      _ => closestColors[0].index
    };
  }

  private static int ProjectOntoLine(
    List<(int index, double distance)> closestColors,
    Color originalColor,
    IReadOnlyList<Color> palette) {
    var color1 = palette[closestColors[0].index];
    var color2 = palette[closestColors[1].index];

    // Calculate projection parameter t
    var dx = color2.R - color1.R;
    var dy = color2.G - color1.G;
    var dz = color2.B - color1.B;

    var px = originalColor.R - color1.R;
    var py = originalColor.G - color1.G;
    var pz = originalColor.B - color1.B;

    var dotProduct = dx * px + dy * py + dz * pz;
    var lengthSquared = dx * dx + dy * dy + dz * dz;

    if (lengthSquared == 0) return closestColors[0].index;

    var t = dotProduct / (double)lengthSquared;

    // Choose color based on projection parameter
    return t < 0.5 ? closestColors[0].index : closestColors[1].index;
  }

  private static int ProjectOntoPolygon(
    List<(int index, double distance)> closestColors,
    Color originalColor,
    IReadOnlyList<Color> palette) {
    // Simplified polygon projection - use centroid-based approach
    var centroidR = 0.0;
    var centroidG = 0.0;
    var centroidB = 0.0;

    foreach (var (index, _) in closestColors) {
      var color = palette[index];
      centroidR += color.R;
      centroidG += color.G;
      centroidB += color.B;
    }

    centroidR /= closestColors.Count;
    centroidG /= closestColors.Count;
    centroidB /= closestColors.Count;

    // Find closest color to the direction from centroid to original
    var bestIndex = 0;
    var bestScore = double.MaxValue;

    for (var i = 0; i < closestColors.Count; ++i) {
      var color = palette[closestColors[i].index];

      // Calculate how well this color aligns with the centroid->original direction
      var dirR = originalColor.R - centroidR;
      var dirG = originalColor.G - centroidG;
      var dirB = originalColor.B - centroidB;

      var colorR = color.R - centroidR;
      var colorG = color.G - centroidG;
      var colorB = color.B - centroidB;

      // Dot product normalized by magnitudes (cosine similarity)
      var dotProduct = dirR * colorR + dirG * colorG + dirB * colorB;
      var dirMagnitude = Math.Sqrt(dirR * dirR + dirG * dirG + dirB * dirB);
      var colorMagnitude = Math.Sqrt(colorR * colorR + colorG * colorG + colorB * colorB);

      if (dirMagnitude > 0 && colorMagnitude > 0) {
        var similarity = dotProduct / (dirMagnitude * colorMagnitude);
        var score = 1.0 - similarity; // Lower score = better alignment

        if (score < bestScore) {
          bestScore = score;
          bestIndex = i;
        }
      }
    }

    return closestColors[bestIndex].index;
  }

  private static int SelectBySpatialPattern(
    List<(int index, double distance)> closestColors,
    int x, int y) {
    // Use spatial coordinates to create a pattern within the convex hull
    var patternValue = (x * 73 + y * 97) % closestColors.Count;
    return closestColors[patternValue].index;
  }

  private static int SelectByWeightedRandom(
    List<(int index, double distance)> closestColors,
    Color originalColor,
    IReadOnlyList<Color> palette,
    Random random) {
    // Calculate weights based on both distance and color similarity
    var weights = new double[closestColors.Count];
    var totalWeight = 0.0;

    for (var i = 0; i < closestColors.Count; ++i) {
      var color = palette[closestColors[i].index];

      // Combine distance weight with color similarity weight
      var distanceWeight = 1.0 / (closestColors[i].distance + 1.0);

      // Color similarity in RGB space
      var rDiff = Math.Abs(originalColor.R - color.R);
      var gDiff = Math.Abs(originalColor.G - color.G);
      var bDiff = Math.Abs(originalColor.B - color.B);
      var colorSimilarity = 1.0 / (rDiff + gDiff + bDiff + 1.0);

      weights[i] = distanceWeight * colorSimilarity;
      totalWeight += weights[i];
    }

    if (totalWeight == 0)
      return closestColors[0].index;

    // Weighted random selection
    var randomValue = random.NextDouble() * totalWeight;
    var cumulativeWeight = 0.0;

    for (var i = 0; i < weights.Length; ++i) {
      cumulativeWeight += weights[i];
      if (randomValue <= cumulativeWeight)
        return closestColors[i].index;
    }

    return closestColors[^1].index; // Fallback
  }
}
