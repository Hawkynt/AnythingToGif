using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

public readonly struct NoiseDitherer(NoiseDitherer.NoiseType noiseType, double intensity = 0.5, int? seed = null) : IDitherer {
  private readonly double _intensity = Math.Clamp(intensity, 0.0, 1.0);

  public enum NoiseType {
    White,
    Blue,
    Brown,
    Pink
  }

  public static IDitherer White { get; } = new NoiseDitherer(NoiseType.White, 0.5);
  public static IDitherer WhiteLight { get; } = new NoiseDitherer(NoiseType.White, 0.3);
  public static IDitherer WhiteStrong { get; } = new NoiseDitherer(NoiseType.White, 0.7);
  
  public static IDitherer Blue { get; } = new NoiseDitherer(NoiseType.Blue, 0.5);
  public static IDitherer BlueLight { get; } = new NoiseDitherer(NoiseType.Blue, 0.3);
  public static IDitherer BlueStrong { get; } = new NoiseDitherer(NoiseType.Blue, 0.7);
  
  public static IDitherer Brown { get; } = new NoiseDitherer(NoiseType.Brown, 0.5);
  public static IDitherer BrownLight { get; } = new NoiseDitherer(NoiseType.Brown, 0.3);
  public static IDitherer BrownStrong { get; } = new NoiseDitherer(NoiseType.Brown, 0.7);
  
  public static IDitherer Pink { get; } = new NoiseDitherer(NoiseType.Pink, 0.5);
  public static IDitherer PinkLight { get; } = new NoiseDitherer(NoiseType.Pink, 0.3);
  public static IDitherer PinkStrong { get; } = new NoiseDitherer(NoiseType.Pink, 0.7);

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var intensity = this._intensity;
    switch (noiseType) {
      case NoiseType.White:
        Process(new WhiteNoiseGenerator(seed ?? 42));
        break;
      case
        NoiseType.Blue:
        Process(new BlueNoiseGenerator(seed ?? 42, width, height));
        break;
      case NoiseType.Brown:
        Process(new BrownNoiseGenerator(seed ?? 42));
        break;
      case NoiseType.Pink:
        Process(new PinkNoiseGenerator(seed ?? 42));
        break;
      default:
        Process(new WhiteNoiseGenerator(seed ?? 42));
        break;
    }

    return;
    
    void Process<TNoise>(TNoise noiseGenerator) where TNoise : INoiseGenerator {
      for (var y = 0; y < height; ++y) {
        var offset = y * stride;
        for (var x = 0; x < width; ++offset, ++x) {
          var originalColor = source[x, y];

          // Generate noise value for this pixel
          var noiseValue = noiseGenerator.GetNoise(x, y);
          var thresholdInt = (int)(noiseValue * intensity * 255);

          // Apply noise to each color channel
          var r = Math.Clamp(originalColor.R + thresholdInt, 0, 255);
          var g = Math.Clamp(originalColor.G + thresholdInt, 0, 255);
          var b = Math.Clamp(originalColor.B + thresholdInt, 0, 255);

          data[offset] = (byte)wrapper.FindClosestColorIndex(Color.FromArgb(r, g, b));
        }
      }
    }
  }

  

  // Base interface for noise generators
  private interface INoiseGenerator {
    double GetNoise(int x, int y);
  }

  // White noise: completely random, uniform distribution
  private readonly struct WhiteNoiseGenerator(int seed) : INoiseGenerator {
    public double GetNoise(int x, int y) {
      // Use coordinate-based seeding for consistent noise per pixel
      var pixelSeed = seed ^ (x * 73856093) ^ (y * 19349663);
      var random = new Random(pixelSeed);
      return random.NextDouble() * 2.0 - 1.0; // Range [-1, 1]
    }
  }

  // Blue noise: high-frequency emphasis, good spatial distribution
  private readonly struct BlueNoiseGenerator(int seed, int width, int height) : INoiseGenerator {
    private readonly double[,] _blueNoiseTexture= _GenerateBlueNoiseTexture(seed, width,height);

    public double GetNoise(int x, int y) => this._blueNoiseTexture[x, y];

    private static double[,] _GenerateBlueNoiseTexture(int seed, int width, int height) {
      // Simplified blue noise generation using Mitchell's best-candidate algorithm
      var texture = new double[width, height];
      var random = new Random(seed);
      
      // Initialize with white noise
      for (var y = 0; y < height; ++y)
      for (var x = 0; x < width; ++x)
        texture[x, y] = random.NextDouble() * 2.0 - 1.0;

      // Apply blue noise filtering (simplified approach)
      var filtered = new double[width, height];
      for (var y = 0; y < height; ++y)
      for (var x = 0; x < width; ++x) {
        var sum = 0.0;
        var count = 0;

        // Sample neighborhood with high-frequency emphasis
        for (var dy = -2; dy <= 2; ++dy)
        for (var dx = -2; dx <= 2; ++dx) {
          var nx = (x + dx + width) % width;
          var ny = (y + dy + height) % height;
          var distance = Math.Sqrt(dx * dx + dy * dy);

          if (!(distance > 0))
            continue;

          // High-frequency emphasis: weight inversely with distance
          var weight = 1.0 / distance;
          sum += texture[nx, ny] * weight;
          count++;
        }

        filtered[x, y] = count > 0 ? sum / count : texture[x, y];
      }

      return filtered;
    }
  }

  // Brown noise: low-frequency emphasis, Brownian motion characteristics
  private readonly struct BrownNoiseGenerator(int seed) : INoiseGenerator {
    public double GetNoise(int x, int y) {
      // Brownian motion simulation using coordinate-based random walk
      var value = 0.0;
      
      // Create correlated noise by summing scaled random values
      for (var scale = 1; scale <= 8; scale *= 2) {
        var coordX = x / scale;
        var coordY = y / scale;
        var localSeed = seed ^ (coordX * 73856093) ^ (coordY * 19349663) ^ scale;
        var rng = new Random(localSeed);
        
        // Lower frequencies get higher weight (brown noise characteristic)
        var weight = 1.0 / (scale * scale);
        value += (rng.NextDouble() * 2.0 - 1.0) * weight;
      }
      
      // Normalize to [-1, 1] range
      return Math.Clamp(value, -1.0, 1.0);
    }
  }

  // Pink noise: 1/f noise, balanced between white and brown
  private readonly struct PinkNoiseGenerator(int seed) : INoiseGenerator {
    public double GetNoise(int x, int y) {
      // Pink noise using summed octaves with 1/f scaling
      var value = 0.0;
      var amplitude = 1.0;
      var totalAmplitude = 0.0;
      
      // Sum multiple octaves with decreasing amplitude
      for (var octave = 1; octave <= 6; ++octave) {
        var coordX = x / octave;
        var coordY = y / octave;
        var localSeed = seed ^ (coordX * 73856093) ^ (coordY * 19349663) ^ octave;
        var rng = new Random(localSeed);
        
        value += (rng.NextDouble() * 2.0 - 1.0) * amplitude;
        totalAmplitude += amplitude;
        amplitude *= 0.5; // Each octave has half the amplitude (1/f characteristic)
      }
      
      // Normalize
      return totalAmplitude > 0 ? value / totalAmplitude : 0.0;
    }
  }
}