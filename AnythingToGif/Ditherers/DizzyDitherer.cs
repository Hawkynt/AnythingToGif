using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace AnythingToGif.Ditherers;

/// <summary>
/// Dizzy Dithering algorithm - a novel error diffusion method that produces 
/// results similar to blue noise dithering (December 2024).
/// Based on Liam Appelbe's research for improved spatial frequency distribution.
/// </summary>
public readonly record struct DizzyDitherer : IDitherer {

  private readonly Random _random;
  private readonly float _randomness;
  private readonly int _spiralRadius;

  public static readonly DizzyDitherer Default = new(0.15f, 3);
  public static readonly DizzyDitherer HighQuality = new(0.1f, 4);
  public static readonly DizzyDitherer Fast = new(0.2f, 2);

  public DizzyDitherer(float randomness = 0.15f, int spiralRadius = 3) {
    this._random = new Random(42); // Fixed seed for reproducible results
    this._randomness = Math.Clamp(randomness, 0.0f, 1.0f);
    this._spiralRadius = Math.Clamp(spiralRadius, 1, 6);
  }

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);
    var errorR = new float[width, height];
    var errorG = new float[width, height];
    var errorB = new float[width, height];

    // Generate spiral pattern for "dizzy" error distribution
    var spiralPattern = this.GenerateSpiralPattern();

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
        var pixel = source[x, y];

        // Add accumulated error
        var newR = Math.Clamp(pixel.R + errorR[x, y], 0, 255);
        var newG = Math.Clamp(pixel.G + errorG[x, y], 0, 255);
        var newB = Math.Clamp(pixel.B + errorB[x, y], 0, 255);

        var newColor = Color.FromArgb(pixel.A, (int)newR, (int)newG, (int)newB);
        
        // Find closest palette color
        var closestIndex = wrapper.FindClosestColorIndex(newColor);
        var closestColor = palette[closestIndex];
        data[y * stride + x] = (byte)closestIndex;

        // Calculate error
        var errR = newR - closestColor.R;
        var errG = newG - closestColor.G;
        var errB = newB - closestColor.B;

        if (errR == 0 && errG == 0 && errB == 0) continue;

        // Distribute error using "dizzy" spiral pattern with blue noise characteristics
        this.DistributeErrorDizzy(x, y, errR, errG, errB, width, height, errorR, errorG, errorB, spiralPattern);
    }
  }

  private (int dx, int dy, float weight)[] GenerateSpiralPattern() {
    var pattern = new List<(int dx, int dy, float weight)>();
    float totalWeight = 0;

    // Generate spiral points with blue noise distribution
    for (var radius = 1; radius <= this._spiralRadius; ++radius) {
      var angleStep = (float)(2 * Math.PI / (radius * 6)); // More points on outer spirals
      
      for (float angle = 0; angle < 2 * Math.PI; angle += angleStep) {
        // Add some randomness to create blue noise characteristics
        var randomAngle = angle + (this._random.NextSingle() - 0.5f) * this._randomness;
        var randomRadius = radius + (this._random.NextSingle() - 0.5f) * this._randomness * 0.5f;
        
        var dx = (int)Math.Round(randomRadius * Math.Cos(randomAngle));
        var dy = (int)Math.Round(randomRadius * Math.Sin(randomAngle));
        
        // Skip center point and duplicates
        if (dx == 0 && dy == 0) continue;
        if (pattern.Exists(p => p.dx == dx && p.dy == dy)) continue;
        
        // Weight based on distance with blue noise adjustment
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        var weight = this.CalculateBlueNoiseWeight(distance, dx, dy);
        
        pattern.Add((dx, dy, weight));
        totalWeight += weight;
      }
    }

    // Normalize weights
    for (var i = 0; i < pattern.Count; ++i) {
      var (dx, dy, weight) = pattern[i];
      pattern[i] = (dx, dy, weight / totalWeight);
    }

    return pattern.ToArray();
  }

  private float CalculateBlueNoiseWeight(float distance, int dx, int dy) {
    // Blue noise weight calculation - suppresses low frequencies
    var baseWeight = 1.0f / (1.0f + distance * distance * 0.5f);
    
    // Add blue noise characteristics by suppressing regular patterns
    var noiseComponent = (float)(0.5 + 0.3 * Math.Sin(dx * 2.1 + dy * 3.7) + 0.2 * Math.Cos(dx * 1.3 - dy * 2.9));
    
    // Boost high-frequency components (blue noise characteristic)
    var frequency = (float)Math.Sqrt(dx * dx + dy * dy);
    var frequencyBoost = Math.Min(1.0f, frequency / this._spiralRadius);
    
    return baseWeight * noiseComponent * (0.7f + 0.3f * frequencyBoost);
  }

  private void DistributeErrorDizzy(int x, int y, float errR, float errG, float errB, 
    int width, int height, float[,] errorR, float[,] errorG, float[,] errorB,
    (int dx, int dy, float weight)[] spiralPattern) {
    
    // Add temporal variation to make the pattern "dizzy"
    var timePhase = (x * 7 + y * 11) % 100 / 100.0f;
    
    foreach (var (dx, dy, weight) in spiralPattern) {
      // Apply "dizzy" rotation based on position
      var rotationAngle = timePhase * (float)Math.PI * 0.25f + (x + y) * 0.1f;
      var cos = (float)Math.Cos(rotationAngle);
      var sin = (float)Math.Sin(rotationAngle);
      
      // Rotate the offset vector
      var rotatedDx = (int)Math.Round(dx * cos - dy * sin);
      var rotatedDy = (int)Math.Round(dx * sin + dy * cos);
      
      var targetX = x + rotatedDx;
      var targetY = y + rotatedDy;

      if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height)
        continue;

      // Apply some randomness to the weight for blue noise characteristics
      var randomWeight = weight * (0.8f + 0.4f * this._random.NextSingle());
        
      errorR[targetX, targetY] += errR * randomWeight;
      errorG[targetX, targetY] += errG * randomWeight;
      errorB[targetX, targetY] += errB * randomWeight;
    }
  }

  public override string ToString() => $"Dizzy Dithering (r={this._randomness:F2}, s={this._spiralRadius})";
}