using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

/// <summary>
///   Tests for the newly implemented dithering algorithms:
///   N-Closest, N-Convex, and Adaptive dithering.
/// </summary>
[TestFixture]
public class NewDitherersTests {
  private static readonly Color[] TestPalette = {
    Color.Black, Color.White, Color.Red, Color.Green, Color.Blue,
    Color.Yellow, Color.Cyan, Color.Magenta, Color.Gray
  };

  [Test]
  [TestCase(typeof(NClosestDitherer))]
  [TestCase(typeof(NConvexDitherer))]
  [TestCase(typeof(AdaptiveDitherer))]
  public void NewDitherers_ShouldNotThrowExceptions(Type dithererType) {
    using var testBitmap = CreateTestBitmap(64, 64);

    // Get all static properties that return IDitherer
    var ditherers = dithererType
      .GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(p => typeof(IDitherer).IsAssignableFrom(p.PropertyType))
      .Select(p => (IDitherer)p.GetValue(null)!)
      .ToList();

    Assert.That(ditherers.Count, Is.GreaterThan(0), $"Expected to find static IDitherer properties in {dithererType.Name}");

    foreach (var ditherer in ditherers)
      Assert.DoesNotThrow(() => {
          using var result = ApplyDithering(testBitmap, ditherer, TestPalette);
          Assert.That(result.Width, Is.EqualTo(testBitmap.Width));
          Assert.That(result.Height, Is.EqualTo(testBitmap.Height));
        }, $"Dithering with {ditherer.GetType().Name} should not throw exceptions");
  }

  [Test]
  public void NClosestDitherer_AllVariants_ProduceValidOutput() {
    using var testBitmap = CreateTestBitmap(32, 32);

    var ditherers = new[] {
      NClosestDitherer.Default,
      NClosestDitherer.WeightedRandom5,
      NClosestDitherer.RoundRobin4,
      NClosestDitherer.Luminance6,
      NClosestDitherer.BlueNoise4
    };

    foreach (var ditherer in ditherers) {
      using var result = ApplyDithering(testBitmap, ditherer, TestPalette);

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
                $"Pixel value {pixelValue} at ({x},{y}) exceeds palette size");
            }
          }
        }
      } finally {
        result.UnlockBits(resultData);
      }
    }
  }

  [Test]
  public void NConvexDitherer_AllVariants_ProduceValidOutput() {
    using var testBitmap = CreateTestBitmap(32, 32);

    var ditherers = new[] {
      NConvexDitherer.Default,
      NConvexDitherer.Projection6,
      NConvexDitherer.SpatialPattern3,
      NConvexDitherer.WeightedRandom5
    };

    foreach (var ditherer in ditherers) {
      using var result = ApplyDithering(testBitmap, ditherer, TestPalette);

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
                $"Pixel value {pixelValue} at ({x},{y}) exceeds palette size");
            }
          }
        }
      } finally {
        result.UnlockBits(resultData);
      }
    }
  }

  [Test]
  public void AdaptiveDitherer_AllVariants_ProduceValidOutput() {
    using var testBitmap = CreateTestBitmap(32, 32);

    var ditherers = new[] {
      AdaptiveDitherer.QualityOptimized,
      AdaptiveDitherer.Balanced,
      AdaptiveDitherer.PerformanceOptimized,
      AdaptiveDitherer.SmartSelection
    };

    foreach (var ditherer in ditherers) {
      using var result = ApplyDithering(testBitmap, ditherer, TestPalette);

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
                $"Pixel value {pixelValue} at ({x},{y}) exceeds palette size");
            }
          }
        }
      } finally {
        result.UnlockBits(resultData);
      }
    }
  }

  [Test]
  public void NewDitherers_HandleEmptyPalette_Gracefully() {
    using var testBitmap = CreateTestBitmap(16, 16);
    var emptyPalette = Array.Empty<Color>();

    var ditherers = new IDitherer[] {
      NClosestDitherer.Default,
      NConvexDitherer.Default,
      AdaptiveDitherer.Balanced
    };

    foreach (var ditherer in ditherers)
      Assert.DoesNotThrow(() => {
          using var result = ApplyDithering(testBitmap, ditherer, emptyPalette);
          // With empty palette, all pixels should be 0 (default)
          var resultData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.ReadOnly, result.PixelFormat);
          try {
            unsafe {
              var data = (byte*)resultData.Scan0;
              for (var y = 0; y < result.Height; ++y) {
                var offset = y * resultData.Stride;
                for (var x = 0; x < result.Width; ++x, ++offset) {
                  var pixelValue = data[offset];
                  Assert.That(pixelValue, Is.EqualTo(0), "Empty palette should result in all pixels being 0");
                }
              }
            }
          } finally {
            result.UnlockBits(resultData);
          }
        }, $"Ditherer {ditherer.GetType().Name} should handle empty palette gracefully");
  }

  [Test]
  public void NewDitherers_HandleSingleColorPalette_Gracefully() {
    using var testBitmap = CreateTestBitmap(16, 16);
    var singleColorPalette = new[] { Color.Red };

    var ditherers = new IDitherer[] {
      NClosestDitherer.Default,
      NConvexDitherer.Default,
      AdaptiveDitherer.Balanced
    };

    foreach (var ditherer in ditherers)
      Assert.DoesNotThrow(() => {
          using var result = ApplyDithering(testBitmap, ditherer, singleColorPalette);
          // With single color palette, all pixels should be 0 (first/only color)
          var resultData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.ReadOnly, result.PixelFormat);
          try {
            unsafe {
              var data = (byte*)resultData.Scan0;
              for (var y = 0; y < result.Height; ++y) {
                var offset = y * resultData.Stride;
                for (var x = 0; x < result.Width; ++x, ++offset) {
                  var pixelValue = data[offset];
                  Assert.That(pixelValue, Is.EqualTo(0), "Single color palette should result in all pixels being 0");
                }
              }
            }
          } finally {
            result.UnlockBits(resultData);
          }
        }, $"Ditherer {ditherer.GetType().Name} should handle single color palette gracefully");
  }

  [Test]
  public void AdaptiveDitherer_SelectsDifferentAlgorithms_ForDifferentImages() {
    // This test verifies that the adaptive ditherer actually makes different choices
    // based on image characteristics by testing with contrasting image types

    using var gradientImage = CreateGradientBitmap(64, 64);
    using var noiseImage = CreateNoiseBitmap(64, 64);
    using var edgeImage = CreateEdgeBitmap(64, 64);

    var adaptiveDitherer = AdaptiveDitherer.SmartSelection;

    // Apply dithering to different image types
    using var gradientResult = ApplyDithering(gradientImage, adaptiveDitherer, TestPalette);
    using var noiseResult = ApplyDithering(noiseImage, adaptiveDitherer, TestPalette);
    using var edgeResult = ApplyDithering(edgeImage, adaptiveDitherer, TestPalette);

    // All results should be valid
    Assert.That(gradientResult.Width, Is.EqualTo(64));
    Assert.That(noiseResult.Width, Is.EqualTo(64));
    Assert.That(edgeResult.Width, Is.EqualTo(64));

    // Results should be different (adaptive algorithm chose different approaches)
    Assert.That(BitmapsAreIdentical(gradientResult, noiseResult), Is.False,
      "Gradient and noise images should produce different dithered results");
    Assert.That(BitmapsAreIdentical(gradientResult, edgeResult), Is.False,
      "Gradient and edge images should produce different dithered results");
  }

  [Test]
  public void NewDitherers_Performance_ReasonableForSmallImages() {
    using var testBitmap = CreateTestBitmap(128, 128);

    var ditherers = new IDitherer[] {
      NClosestDitherer.Default,
      NConvexDitherer.Default,
      AdaptiveDitherer.PerformanceOptimized // Should be fastest variant
    };

    foreach (var ditherer in ditherers) {
      var stopwatch = Stopwatch.StartNew();

      using var result = ApplyDithering(testBitmap, ditherer, TestPalette);

      stopwatch.Stop();

      // Should complete within reasonable time (5 seconds for small image)
      Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000),
        $"Ditherer {ditherer.GetType().Name} took too long: {stopwatch.ElapsedMilliseconds}ms");
    }
  }

  private static Bitmap CreateTestBitmap(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

    // Create a gradient pattern
    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var r = x * 255 / width;
      var g = y * 255 / height;
      var b = (x + y) * 255 / (width + height);
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }

    return bitmap;
  }

  private static Bitmap CreateGradientBitmap(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var intensity = (x + y) * 255 / (width + height);
      bitmap.SetPixel(x, y, Color.FromArgb(intensity, intensity, intensity));
    }

    return bitmap;
  }

  private static Bitmap CreateNoiseBitmap(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    var random = new Random(42);

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var r = random.Next(256);
      var g = random.Next(256);
      var b = random.Next(256);
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }

    return bitmap;
  }

  private static Bitmap CreateEdgeBitmap(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      // Create checker pattern (high edge density)
      var isWhite = (x / 8 + y / 8) % 2 == 0;
      var color = isWhite ? Color.White : Color.Black;
      bitmap.SetPixel(x, y, color);
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

  private static bool BitmapsAreIdentical(Bitmap bitmap1, Bitmap bitmap2) {
    if (bitmap1.Width != bitmap2.Width || bitmap1.Height != bitmap2.Height)
      return false;

    for (var y = 0; y < bitmap1.Height; ++y)
    for (var x = 0; x < bitmap1.Width; ++x)
      if (bitmap1.GetPixel(x, y).ToArgb() != bitmap2.GetPixel(x, y).ToArgb())
        return false;

    return true;
  }
}
