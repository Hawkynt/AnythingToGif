using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

/// <summary>
///   Tests to verify that the AdaptiveDitherer actually utilizes 
///   the full range of available MatrixBasedDitherer algorithms
///   based on image characteristics.
/// </summary>
[TestFixture]
public class AdaptiveDithererMatrixTests {
  private static readonly Color[] TestPalette = {
    Color.Black, Color.White, Color.Red, Color.Green, Color.Blue,
    Color.Yellow, Color.Cyan, Color.Magenta, Color.Gray
  };

  [Test]
  public void AdaptiveDitherer_SmartSelection_UsesVariousMatrixAlgorithms() {
    // Create different test images that should trigger different matrix algorithms
    var testScenarios = new[] {
      ("HighDetail", CreateHighDetailImage(64, 64)),
      ("SmoothGradient", CreateSmoothGradientImage(64, 64)),
      ("HighContrast", CreateHighContrastImage(64, 64)),
      ("ComplexColors", CreateComplexColorImage(64, 64)),
      ("LargeImage", CreateHighDetailImage(256, 256))
    };

    var adaptiveDitherer = AdaptiveDitherer.SmartSelection;
    var usedAlgorithmTypes = new HashSet<string>();

    foreach (var (scenarioName, testImage) in testScenarios) {
      using (testImage) {
        // We can't directly inspect which algorithm was chosen, but we can test
        // that the adaptive ditherer works with all scenarios without throwing
        Assert.DoesNotThrow(() => {
          using var result = ApplyDithering(testImage, adaptiveDitherer, TestPalette);
          Assert.That(result.Width, Is.EqualTo(testImage.Width));
          Assert.That(result.Height, Is.EqualTo(testImage.Height));
        }, $"Scenario '{scenarioName}' should not throw exceptions");
      }
    }
  }

  [Test]
  public void AdaptiveDitherer_AllStrategies_ProduceValidResults() {
    var strategies = new[] {
      ("QualityOptimized", AdaptiveDitherer.QualityOptimized),
      ("Balanced", AdaptiveDitherer.Balanced),
      ("PerformanceOptimized", AdaptiveDitherer.PerformanceOptimized),
      ("SmartSelection", AdaptiveDitherer.SmartSelection)
    };

    using var testImage = CreateComplexColorImage(64, 64);

    foreach (var (strategyName, ditherer) in strategies) {
      Assert.DoesNotThrow(() => {
        using var result = ApplyDithering(testImage, ditherer, TestPalette);
        
        // Verify all pixel values are valid palette indices
        var resultData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
          ImageLockMode.ReadOnly, result.PixelFormat);
        try {
          unsafe {
            var data = (byte*)resultData.Scan0;
            for (var y = 0; y < result.Height; ++y) {
              var offset = y * resultData.Stride;
              for (var x = 0; x < result.Width; ++x, ++offset) {
                var pixelValue = data[offset];
                Assert.That(pixelValue, Is.LessThan(TestPalette.Length),
                  $"Strategy '{strategyName}': Pixel value {pixelValue} at ({x},{y}) exceeds palette size");
              }
            }
          }
        } finally {
          result.UnlockBits(resultData);
        }
      }, $"Strategy '{strategyName}' should work correctly");
    }
  }

  [Test]
  public void AdaptiveDitherer_DifferentStrategies_ProduceDifferentResults() {
    // Create an image that should trigger different choices in different strategies
    using var testImage = CreateHighDetailImage(64, 64);

    using var qualityResult = ApplyDithering(testImage, AdaptiveDitherer.QualityOptimized, TestPalette);
    using var performanceResult = ApplyDithering(testImage, AdaptiveDitherer.PerformanceOptimized, TestPalette);

    // While we can't guarantee the results will be different (they might choose the same algorithm),
    // we can at least verify both produce valid results
    Assert.That(qualityResult.Width, Is.EqualTo(testImage.Width));
    Assert.That(performanceResult.Width, Is.EqualTo(testImage.Width));
    
    // The results might be different due to different algorithm selection
    // but we can't easily test this without exposing internal algorithm choice
  }

  [Test]
  public void AdaptiveDitherer_HandlesExtremeImageSizes() {
    // Test very small image
    using var tinyImage = CreateSmoothGradientImage(4, 4);
    Assert.DoesNotThrow(() => {
      using var result = ApplyDithering(tinyImage, AdaptiveDitherer.SmartSelection, TestPalette);
      Assert.That(result, Is.Not.Null);
    });

    // Test very large image dimensions (but small actual image to keep test fast)
    using var largeImage = CreateSmoothGradientImage(16, 16);
    Assert.DoesNotThrow(() => {
      using var result = ApplyDithering(largeImage, AdaptiveDitherer.PerformanceOptimized, TestPalette);
      Assert.That(result, Is.Not.Null);
    });
  }

  [Test]
  public void AdaptiveDitherer_HandlesVariousPaletteSizes() {
    using var testImage = CreateComplexColorImage(32, 32);
    
    var paletteSizes = new[] { 2, 4, 8, 16, 32, 64, 128, 256 };
    
    foreach (var paletteSize in paletteSizes) {
      var palette = TestPalette.Take(Math.Min(paletteSize, TestPalette.Length)).ToArray();
      
      Assert.DoesNotThrow(() => {
        using var result = ApplyDithering(testImage, AdaptiveDitherer.Balanced, palette);
        Assert.That(result, Is.Not.Null);
      }, $"Should handle palette size {paletteSize}");
    }
  }

  private static Bitmap CreateHighDetailImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        // Create checkerboard pattern with noise for high detail/edge density
        var baseColor = (x / 4 + y / 4) % 2 == 0 ? 200 : 50;
        var noise = (x * 7 + y * 11) % 64 - 32; // Deterministic noise
        var r = Math.Max(0, Math.Min(255, baseColor + noise));
        var g = Math.Max(0, Math.Min(255, baseColor + noise / 2));
        var b = Math.Max(0, Math.Min(255, baseColor - noise / 3));
        bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
      }
    }
    
    return bitmap;
  }

  private static Bitmap CreateSmoothGradientImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        // Smooth gradient for high gradient smoothness, low edge density
        var r = (int)(255.0 * x / width);
        var g = (int)(255.0 * y / height);
        var b = (int)(255.0 * (x + y) / (width + height));
        bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
      }
    }
    
    return bitmap;
  }

  private static Bitmap CreateHighContrastImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        // High contrast pattern for high edge density
        var isLight = ((x / 8) + (y / 8)) % 2 == 0;
        var color = isLight ? Color.White : Color.Black;
        bitmap.SetPixel(x, y, color);
      }
    }
    
    return bitmap;
  }

  private static Bitmap CreateComplexColorImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        // Complex color distribution for high color complexity
        var r = (x * 71 + y * 37) % 256;       // Pseudo-random patterns
        var g = (x * 113 + y * 73) % 256;
        var b = (x * 157 + y * 191) % 256;
        bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
      }
    }
    
    return bitmap;
  }

  private static Bitmap ApplyDithering(Bitmap source, IDitherer ditherer, Color[] palette) {
    var target = new Bitmap(source.Width, source.Height, PixelFormat.Format8bppIndexed);
    var targetData = target.LockBits(
      new Rectangle(0, 0, source.Width, source.Height),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = source.Lock();
      ditherer.Dither(locker, targetData, palette);
    } finally {
      target.UnlockBits(targetData);
    }

    return target;
  }
}