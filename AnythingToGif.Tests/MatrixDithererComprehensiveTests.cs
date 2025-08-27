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
///   Comprehensive tests for all MatrixBasedDitherer algorithms to ensure
///   they are properly implemented and accessible.
/// </summary>
[TestFixture]
public class MatrixDithererComprehensiveTests {
  private static readonly Color[] TestPalette = {
    Color.Black, Color.White, Color.Red, Color.Green, Color.Blue,
    Color.Yellow, Color.Cyan, Color.Magenta
  };

  [Test]
  public void AllMatrixBasedDitherers_AreAccessible() {
    // Get all static properties of MatrixBasedDitherer that return IDitherer
    var matrixDithererType = typeof(MatrixBasedDitherer);
    var matrixDitherers = matrixDithererType
      .GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(p => typeof(IDitherer).IsAssignableFrom(p.PropertyType))
      .Select(p => new { Name = p.Name, Ditherer = (IDitherer)p.GetValue(null)! })
      .ToList();

    // Verify we have the expected algorithms
    var expectedAlgorithms = new[] {
      "FloydSteinberg", "EqualFloydSteinberg", "FalseFloydSteinberg", 
      "Simple", "JarvisJudiceNinke", "Stucki", "Atkinson", "Burkes",
      "Sierra", "TwoRowSierra", "SierraLite", "Pigeon", "StevensonArce",
      "ShiauFan", "ShiauFan2", "Fan93", "TwoD", "Down", "DoubleDown",
      "Diagonal", "VerticalDiamond", "HorizontalDiamond", "Diamond"
    };

    Assert.That(matrixDitherers.Count, Is.GreaterThanOrEqualTo(23), 
      "Should have at least 23 matrix-based dithering algorithms");

    foreach (var expectedAlgorithm in expectedAlgorithms) {
      Assert.That(matrixDitherers.Any(md => md.Name == expectedAlgorithm), Is.True,
        $"MatrixBasedDitherer should have {expectedAlgorithm} algorithm");
    }

    TestContext.WriteLine($"Found {matrixDitherers.Count} MatrixBasedDitherer algorithms:");
    foreach (var md in matrixDitherers.OrderBy(md => md.Name)) {
      TestContext.WriteLine($"  - {md.Name}");
    }
  }

  [Test]
  public void AllMatrixBasedDitherers_ProduceValidOutput() {
    var matrixDithererType = typeof(MatrixBasedDitherer);
    var matrixDitherers = matrixDithererType
      .GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(p => typeof(IDitherer).IsAssignableFrom(p.PropertyType))
      .Select(p => new { Name = p.Name, Ditherer = (IDitherer)p.GetValue(null)! })
      .ToList();

    using var testImage = CreateTestImage(32, 32);

    foreach (var md in matrixDitherers) {
      Assert.DoesNotThrow(() => {
        using var result = ApplyDithering(testImage, md.Ditherer, TestPalette);
        
        Assert.That(result.Width, Is.EqualTo(testImage.Width), 
          $"{md.Name}: Result width should match input");
        Assert.That(result.Height, Is.EqualTo(testImage.Height), 
          $"{md.Name}: Result height should match input");

        // Verify all pixels are valid palette indices
        VerifyValidPaletteIndices(result, TestPalette.Length, md.Name);
      }, $"MatrixBasedDitherer.{md.Name} should not throw exceptions");
    }
  }

  [Test]
  public void MatrixBasedDitherers_HandleEdgeCases() {
    var testDitherers = new[] {
      MatrixBasedDitherer.FloydSteinberg,
      MatrixBasedDitherer.JarvisJudiceNinke,
      MatrixBasedDitherer.Atkinson,
      MatrixBasedDitherer.SierraLite,
      MatrixBasedDitherer.Simple,
      MatrixBasedDitherer.TwoD,
      MatrixBasedDitherer.Down,
      MatrixBasedDitherer.Diamond
    };

    foreach (var ditherer in testDitherers) {
      var dithererName = GetDithererName(ditherer);

      // Test with single color palette
      using var testImage = CreateTestImage(16, 16);
      var singleColorPalette = new[] { Color.Red };
      
      Assert.DoesNotThrow(() => {
        using var result = ApplyDithering(testImage, ditherer, singleColorPalette);
        VerifyAllPixelsAreValue(result, 0, $"{dithererName} with single color palette");
      }, $"{dithererName} should handle single color palette");

      // Test with very small image
      using var tinyImage = CreateTestImage(2, 2);
      Assert.DoesNotThrow(() => {
        using var result = ApplyDithering(tinyImage, ditherer, TestPalette);
        Assert.That(result.Width, Is.EqualTo(2));
        Assert.That(result.Height, Is.EqualTo(2));
      }, $"{dithererName} should handle very small images");
    }
  }

  [Test]
  public void MatrixBasedDitherers_ProduceDifferentResults() {
    using var testImage = CreateComplexTestImage(32, 32);

    // Test that different algorithms produce different results
    using var floydResult = ApplyDithering(testImage, MatrixBasedDitherer.FloydSteinberg, TestPalette);
    using var jarvisResult = ApplyDithering(testImage, MatrixBasedDitherer.JarvisJudiceNinke, TestPalette);
    using var atkinsonResult = ApplyDithering(testImage, MatrixBasedDitherer.Atkinson, TestPalette);

    // While results might theoretically be identical for simple images, 
    // for complex images they should typically differ
    var resultsAreAllIdentical = 
      BitmapsAreIdentical(floydResult, jarvisResult) && 
      BitmapsAreIdentical(floydResult, atkinsonResult);

    // For a complex test image, algorithms should produce different results
    Assert.That(resultsAreAllIdentical, Is.False,
      "Different matrix dithering algorithms should produce different results for complex images");
  }

  [Test]
  public void MatrixBasedDitherers_PerformanceCharacteristics() {
    using var testImage = CreateTestImage(64, 64);

    var performanceTests = new[] {
      ("Simple", MatrixBasedDitherer.Simple),
      ("FloydSteinberg", MatrixBasedDitherer.FloydSteinberg),
      ("JarvisJudiceNinke", MatrixBasedDitherer.JarvisJudiceNinke),
      ("StevensonArce", MatrixBasedDitherer.StevensonArce)
    };

    var results = new List<(string Name, long ElapsedMs)>();

    foreach (var (name, ditherer) in performanceTests) {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      
      using var result = ApplyDithering(testImage, ditherer, TestPalette);
      
      stopwatch.Stop();
      results.Add((name, stopwatch.ElapsedMilliseconds));

      // All should complete within reasonable time (10 seconds for test image)
      Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10000),
        $"{name} should complete within reasonable time");
    }

    TestContext.WriteLine("Performance comparison (64x64 image):");
    foreach (var (name, elapsed) in results.OrderBy(r => r.ElapsedMs)) {
      TestContext.WriteLine($"  {name}: {elapsed}ms");
    }
  }

  private static Bitmap CreateTestImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        var r = (x * 255) / width;
        var g = (y * 255) / height;
        var b = ((x + y) * 255) / (width + height);
        bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
      }
    }
    
    return bitmap;
  }

  private static Bitmap CreateComplexTestImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    for (var y = 0; y < height; ++y) {
      for (var x = 0; x < width; ++x) {
        // Create complex pattern that should show differences between algorithms
        var r = (x * 71 + y * 37 + x * x) % 256;
        var g = (x * 113 + y * 73 + y * y) % 256;
        var b = (x * 157 + y * 191 + x * y) % 256;
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

  private static void VerifyValidPaletteIndices(Bitmap result, int paletteSize, string algorithmName) {
    var resultData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
      ImageLockMode.ReadOnly, result.PixelFormat);
    try {
      unsafe {
        var data = (byte*)resultData.Scan0;
        for (var y = 0; y < result.Height; ++y) {
          var offset = y * resultData.Stride;
          for (var x = 0; x < result.Width; ++x, ++offset) {
            var pixelValue = data[offset];
            Assert.That(pixelValue, Is.LessThan(paletteSize),
              $"{algorithmName}: Pixel value {pixelValue} at ({x},{y}) exceeds palette size");
          }
        }
      }
    } finally {
      result.UnlockBits(resultData);
    }
  }

  private static void VerifyAllPixelsAreValue(Bitmap result, byte expectedValue, string testName) {
    var resultData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
      ImageLockMode.ReadOnly, result.PixelFormat);
    try {
      unsafe {
        var data = (byte*)resultData.Scan0;
        for (var y = 0; y < result.Height; ++y) {
          var offset = y * resultData.Stride;
          for (var x = 0; x < result.Width; ++x, ++offset) {
            var pixelValue = data[offset];
            Assert.That(pixelValue, Is.EqualTo(expectedValue),
              $"{testName}: Expected all pixels to be {expectedValue}, but pixel at ({x},{y}) is {pixelValue}");
          }
        }
      }
    } finally {
      result.UnlockBits(resultData);
    }
  }

  private static bool BitmapsAreIdentical(Bitmap bitmap1, Bitmap bitmap2) {
    if (bitmap1.Width != bitmap2.Width || bitmap1.Height != bitmap2.Height)
      return false;

    var data1 = bitmap1.LockBits(new Rectangle(0, 0, bitmap1.Width, bitmap1.Height),
      ImageLockMode.ReadOnly, bitmap1.PixelFormat);
    var data2 = bitmap2.LockBits(new Rectangle(0, 0, bitmap2.Width, bitmap2.Height),
      ImageLockMode.ReadOnly, bitmap2.PixelFormat);

    try {
      unsafe {
        var ptr1 = (byte*)data1.Scan0;
        var ptr2 = (byte*)data2.Scan0;
        
        for (var y = 0; y < bitmap1.Height; ++y) {
          var offset1 = y * data1.Stride;
          var offset2 = y * data2.Stride;
          
          for (var x = 0; x < bitmap1.Width; ++x, ++offset1, ++offset2) {
            if (ptr1[offset1] != ptr2[offset2])
              return false;
          }
        }
      }
    } finally {
      bitmap1.UnlockBits(data1);
      bitmap2.UnlockBits(data2);
    }

    return true;
  }

  private static string GetDithererName(IDitherer ditherer) {
    // Try to get a readable name for the ditherer
    var type = ditherer.GetType();
    if (type == typeof(MatrixBasedDitherer)) {
      // For matrix ditherers, we can't easily get the specific algorithm name
      // without reflection, so just return the type name
      return "MatrixBasedDitherer";
    }
    return type.Name;
  }
}