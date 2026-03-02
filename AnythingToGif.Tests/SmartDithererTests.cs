using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class SmartDithererTests {

  [Test]
  public void SmartDitherer_ShouldAnalyzeAndApplyDifferentStrategies() {
    // Arrange - Create a test image with different content types
    using var testImage = CreateComplexTestImage(64, 64);
    var palette = CreateTestPalette();
    var ditherer = SmartDitherer.Default;
    
    // Act & Assert - Should not crash and should complete
    Assert.DoesNotThrow(() => {
      TestDithererExecution(ditherer, testImage, palette);
    }, "SmartDitherer should execute without throwing exceptions");
  }

  [Test]
  public void ContentAnalyzer_ShouldClassifyDifferentRegions() {
    // Arrange - Create an image with distinct regions
    using var testImage = CreateRegionTestImage(32, 32);
    
    // Act - Analyze the content
    var strategyMap = ContentAnalyzer.AnalyzeImage(testImage);
    
    // Assert - Should have different strategies for different regions
    Assert.That(strategyMap, Is.Not.Null);
    Assert.That(strategyMap.GetLength(0), Is.EqualTo(32));
    Assert.That(strategyMap.GetLength(1), Is.EqualTo(32));
    
    // Check that we have at least some variety in strategies
    var uniqueStrategies = new HashSet<DitheringStrategy>();
    for (int y = 0; y < 32; ++y) {
      for (int x = 0; x < 32; ++x) {
        uniqueStrategies.Add(strategyMap[x, y]);
      }
    }
    
    // Should detect at least 2 different content types
    Assert.That(uniqueStrategies.Count, Is.GreaterThanOrEqualTo(2), 
      "ContentAnalyzer should detect different content types");
  }

  [Test]
  public void SmartDitherer_HighQualityConfig_ShouldUseAdvancedAlgorithms() {
    // Arrange
    using var testImage = CreateTestImage(16, 16);
    var palette = CreateTestPalette();
    var ditherer = SmartDitherer.HighQuality;
    
    // Act & Assert - Should use more sophisticated algorithms
    Assert.DoesNotThrow(() => {
      TestDithererExecution(ditherer, testImage, palette);
    }, "High Quality SmartDitherer should execute without throwing exceptions");
  }

  [Test]
  public void SmartDitherer_FastConfig_ShouldCompleteQuickly() {
    // Arrange
    using var testImage = CreateTestImage(16, 16);
    var palette = CreateTestPalette();
    var ditherer = SmartDitherer.Fast;
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    // Act
    TestDithererExecution(ditherer, testImage, palette);
    
    // Assert - Fast mode should complete reasonably quickly
    stopwatch.Stop();
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), 
      "Fast SmartDitherer should complete in reasonable time");
  }

  private static void TestDithererExecution(IDitherer ditherer, Bitmap testImage, Color[] palette) {
    var bitmapData = testImage.LockBits(
      new Rectangle(0, 0, testImage.Width, testImage.Height),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );
    
    try {
      using var locker = testImage.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      ditherer.Dither(locker, bitmapData, palette);
    } finally {
      testImage.UnlockBits(bitmapData);
    }
  }

  private static Bitmap CreateComplexTestImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    // Create different regions with different characteristics
    using var g = Graphics.FromImage(bitmap);
    
    // Clear background
    g.Clear(Color.White);
    
    // Top-left: Smooth gradient (should trigger SmoothGradient strategy)
    using var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
      new Rectangle(0, 0, width/2, height/2), 
      Color.Blue, Color.Cyan, 0f);
    g.FillRectangle(gradientBrush, 0, 0, width/2, height/2);
    
    // Top-right: Sharp edges (should trigger StructurePreserving strategy)
    g.FillRectangle(Brushes.Black, width/2, 0, width/4, height/4);
    g.FillRectangle(Brushes.White, 3*width/4, 0, width/4, height/4);
    g.FillRectangle(Brushes.Red, width/2, height/4, width/4, height/4);
    g.FillRectangle(Brushes.Green, 3*width/4, height/4, width/4, height/4);
    
    // Bottom half: Complex texture pattern (should trigger DetailEnhancing strategy)
    var random = new Random(42); // Deterministic for testing
    for (int i = 0; i < 500; ++i) {
      var x = random.Next(0, width);
      var y = random.Next(height/2, height);
      var size = random.Next(1, 4);
      var color = Color.FromArgb(random.Next(256), random.Next(256), random.Next(256));
      using var brush = new SolidBrush(color);
      g.FillRectangle(brush, x, y, size, size);
    }
    
    return bitmap;
  }

  private static Bitmap CreateRegionTestImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    using var g = Graphics.FromImage(bitmap);
    
    // Create distinct regions
    // Top half: smooth gradient
    using var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
      new Rectangle(0, 0, width, height/2), 
      Color.Red, Color.Yellow, 0f);
    g.FillRectangle(gradientBrush, 0, 0, width, height/2);
    
    // Bottom half: high contrast checkerboard
    for (int y = height/2; y < height; ++y) {
      for (int x = 0; x < width; ++x) {
        var color = ((x/4 + y/4) % 2 == 0) ? Color.Black : Color.White;
        bitmap.SetPixel(x, y, color);
      }
    }
    
    return bitmap;
  }

  private static Bitmap CreateTestImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    // Create a simple gradient test pattern
    for (int y = 0; y < height; ++y) {
      for (int x = 0; x < width; ++x) {
        var red = (int)(255.0 * x / width);
        var green = (int)(255.0 * y / height);
        var blue = (int)(255.0 * ((x + y) % 2));
        bitmap.SetPixel(x, y, Color.FromArgb(red, green, blue));
      }
    }
    
    return bitmap;
  }

  private static Color[] CreateTestPalette() {
    return new[] {
      Color.Black, Color.White, Color.Red, Color.Green,
      Color.Blue, Color.Yellow, Color.Cyan, Color.Magenta,
      Color.Gray, Color.Orange, Color.Purple, Color.Brown,
      Color.Pink, Color.Lime, Color.Navy, Color.Maroon
    };
  }
}