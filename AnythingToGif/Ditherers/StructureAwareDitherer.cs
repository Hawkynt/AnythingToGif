using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace AnythingToGif.Ditherers;

/// <summary>
/// Structure-aware error diffusion dithering that preserves image structure and details.
/// Uses circular error distribution with priority-based or distance-based ordering.
/// </summary>
public readonly record struct StructureAwareDitherer : IDitherer {

  private readonly int _radius;
  private readonly bool _usePriorityOrder;
  private readonly string _name;
  private readonly List<(int dx, int dy, float weight)> _errorKernel;

  public static readonly StructureAwareDitherer Default = new("Structure-Aware Default", 2, false);
  public static readonly StructureAwareDitherer Priority = new("Structure-Aware Priority", 3, true);
  public static readonly StructureAwareDitherer Large = new("Structure-Aware Large", 4, false);

  public StructureAwareDitherer(string name, int radius, bool usePriorityOrder) {
    this._name = name;
    this._radius = radius;
    this._usePriorityOrder = usePriorityOrder;
    this._errorKernel = this.GenerateCircularKernel();
  }

  private List<(int dx, int dy, float weight)> GenerateCircularKernel() {
    var kernel = new List<(int, int, float)>();
    float totalWeight = 0;

    // Generate circular error distribution kernel with steep drop-off
    for (var dy = 0; dy <= this._radius; ++dy)
    for (var dx = -this._radius; dx <= this._radius; ++dx) {
      if (dx == 0 && dy == 0) continue; // Skip current pixel

      var distance = (float)Math.Sqrt(dx * dx + dy * dy);
      if (!(distance <= this._radius))
        continue;

      // Steep drop-off from center using exponential decay
      var weight = (float)Math.Exp(-distance * 2.0);
        
      // Adjust weight based on direction to avoid artifacts
      if (dy == 0) weight *= 1.2f; // Horizontal neighbors get slightly more weight
      if (dx == 0) weight *= 1.1f; // Vertical neighbors get moderate weight
        
      kernel.Add((dx, dy, weight));
      totalWeight += weight;
    }

    // Normalize weights
    for (var i = 0; i < kernel.Count; ++i) {
      var (dx, dy, weight) = kernel[i];
      kernel[i] = (dx, dy, weight / totalWeight);
    }

    return kernel;
  }

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    
    // use this wrapper to find closest color matches instead of doing your own stuff
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var paletteArray = palette.ToArray();
    var errorR = new float[width, height];
    var errorG = new float[width, height];
    var errorB = new float[width, height];

    // Pre-calculate image gradients for structure awareness
    var gradients = CalculateGradients(source);

    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        var pixel = source[x, y];

        // Add accumulated error
        var newR = Math.Clamp(pixel.R + errorR[x, y], 0, 255);
        var newG = Math.Clamp(pixel.G + errorG[x, y], 0, 255);
        var newB = Math.Clamp(pixel.B + errorB[x, y], 0, 255);

        var newColor = Color.FromArgb(pixel.A, (int)newR, (int)newG, (int)newB);
        
        // Find closest palette color with structure awareness
        var closestColor = FindStructureAwareColor(newColor, paletteArray, gradients[x, y], colorDistanceMetric);
        var closestIndex = Array.IndexOf(paletteArray, closestColor);
        data[y * stride + x] = (byte)closestIndex;

        // Calculate error
        var errR = newR - closestColor.R;
        var errG = newG - closestColor.G;
        var errB = newB - closestColor.B;

        if (errR == 0 && errG == 0 && errB == 0) continue;

        // Distribute error using structure-aware kernel
        this.DistributeError(x, y, errR, errG, errB, width, height, errorR, errorG, errorB, gradients);
      }
    }
  }

  private static float[,] CalculateGradients(BitmapExtensions.IBitmapLocker source) {
    var width = source.Width;
    var height = source.Height;
    var gradients = new float[width, height];

    for (var y = 1; y < height - 1; ++y) {
      for (var x = 1; x < width - 1; ++x) {
        // Calculate Sobel gradient magnitude
        var pixels = new Color[9];
        for (var dy = -1; dy <= 1; ++dy) {
          for (var dx = -1; dx <= 1; ++dx) {
            pixels[(dy + 1) * 3 + (dx + 1)] = source[x + dx, y + dy];
          }
        }

        // Sobel X kernel: -1 0 1, -2 0 2, -1 0 1
        float gx = -pixels[0].GetLuminance() + pixels[2].GetLuminance() +
                   -2 * pixels[3].GetLuminance() + 2 * pixels[5].GetLuminance() +
                   -pixels[6].GetLuminance() + pixels[8].GetLuminance();

        // Sobel Y kernel: -1 -2 -1, 0 0 0, 1 2 1  
        float gy = -pixels[0].GetLuminance() - 2 * pixels[1].GetLuminance() - pixels[2].GetLuminance() +
                   pixels[6].GetLuminance() + 2 * pixels[7].GetLuminance() + pixels[8].GetLuminance();

        gradients[x, y] = (float)Math.Sqrt(gx * gx + gy * gy) / 255.0f;
      }
    }

    return gradients;
  }

  private static Color FindStructureAwareColor(Color target, Color[] palette, float gradient, Func<Color, Color, int>? distanceMetric) {
    // In high-gradient areas, prefer colors that preserve edges
    if (gradient > 0.3f) {
      // Find colors that maintain contrast
      var candidates = palette
        .Select(c => new { Color = c, Distance = distanceMetric?.Invoke(target, c) ?? GetDefaultDistance(target, c) })
        .OrderBy(x => x.Distance)
        .Take(3)
        .ToArray();

      // Prefer higher contrast in edge areas
      var targetLuminance = target.GetLuminance();
      return candidates
        .OrderByDescending(c => Math.Abs(c.Color.GetLuminance() - targetLuminance))
        .First().Color;
    }

    // Normal closest color selection for smooth areas
    return palette[FindClosestIndex(target, palette, distanceMetric)];
  }

  private void DistributeError(int x, int y, float errR, float errG, float errB, 
    int width, int height, float[,] errorR, float[,] errorG, float[,] errorB, float[,] gradients) {
    
    var currentGradient = gradients[x, y];
    var candidates = new List<(int tx, int ty, float weight, float priority)>();

    // Calculate priority for each kernel position
    foreach (var (dx, dy, weight) in this._errorKernel) {
      var tx = x + dx;
      var ty = y + dy;

      if (tx >= 0 && tx < width && ty >= 0 && ty < height) {
        var priority = this._usePriorityOrder ? CalculatePriority(x, y, tx, ty, currentGradient, gradients) : weight;
        candidates.Add((tx, ty, weight, priority));
      }
    }

    // Sort by priority if using priority order
    if (this._usePriorityOrder) {
      candidates.Sort((a, b) => b.priority.CompareTo(a.priority));
    }

    // Distribute error with structure awareness
    float totalDistributed = 0;
    foreach (var (tx, ty, weight, priority) in candidates) {
      var adjustedWeight = weight;
      
      // Boost error diffusion toward similar gradient areas
      var targetGradient = gradients[tx, ty];
      var gradientSimilarity = 1.0f - Math.Abs(currentGradient - targetGradient);
      adjustedWeight *= (0.7f + 0.3f * gradientSimilarity);
      
      errorR[tx, ty] += errR * adjustedWeight;
      errorG[tx, ty] += errG * adjustedWeight;
      errorB[tx, ty] += errB * adjustedWeight;
      
      totalDistributed += adjustedWeight;
    }
  }

  private static float CalculatePriority(int x, int y, int tx, int ty, float currentGradient, float[,] gradients) {
    var distance = (float)Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y));
    var targetGradient = gradients[tx, ty];
    
    // Higher priority for closer pixels in similar gradient areas
    var gradientSimilarity = 1.0f - Math.Abs(currentGradient - targetGradient);
    return gradientSimilarity / (1.0f + distance);
  }

  public override string ToString() => this._name;

  private static int FindClosestIndex(Color target, Color[] palette, Func<Color, Color, int>? distanceMetric) {
    var bestIndex = 0;
    var bestDistance = distanceMetric?.Invoke(target, palette[0]) ?? GetDefaultDistance(target, palette[0]);
    
    for (var i = 1; i < palette.Length; ++i) {
      var distance = distanceMetric?.Invoke(target, palette[i]) ?? GetDefaultDistance(target, palette[i]);
      if (distance < bestDistance) {
        bestDistance = distance;
        bestIndex = i;
      }
    }
    
    return bestIndex;
  }

  private static int GetDefaultDistance(Color a, Color b) {
    var dr = a.R - b.R;
    var dg = a.G - b.G;
    var db = a.B - b.B;
    return dr * dr + dg * dg + db * db;
  }
}