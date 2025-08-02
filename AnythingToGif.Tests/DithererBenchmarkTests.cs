using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AnythingToGif.Ditherers;
using AnythingToGif.Tests.Utilities;
using NUnit.Framework;

namespace AnythingToGif.Tests;

/// <summary>
/// Comprehensive benchmark tests for dithering algorithms.
/// Tests performance, quality metrics, and comparative analysis.
/// </summary>
[TestFixture]
public class DithererBenchmarkTests {

  private readonly Color[] _testPalette16 = PerformanceBenchmark.CreateTestPalette(16, PerformanceBenchmark.PaletteType.Uniform);
  private readonly Color[] _testPalette256 = PerformanceBenchmark.CreateTestPalette(256, PerformanceBenchmark.PaletteType.Weighted);

  [Test]
  public void BenchmarkMatrixBasedDitherers() {
    using var testImage = PerformanceBenchmark.CreateTestImage(256, 256, PerformanceBenchmark.TestImagePattern.Gradient);
    
    var ditherers = new Dictionary<string, IDitherer> {
      ["Floyd-Steinberg"] = MatrixBasedDitherer.FloydSteinberg,
      ["Jarvis-Judice-Ninke"] = MatrixBasedDitherer.JarvisJudiceNinke,
      ["Stucki"] = MatrixBasedDitherer.Stucki,
      ["Atkinson"] = MatrixBasedDitherer.Atkinson,
      ["Sierra"] = MatrixBasedDitherer.Sierra
    };

    var results = PerformanceBenchmark.BenchmarkMultiple(ditherers, testImage, _testPalette16);
    var report = PerformanceBenchmark.FormatResults(results);
    
    TestContext.WriteLine("Matrix-Based Ditherer Performance:");
    TestContext.WriteLine(report);
    
    // Verify all algorithms completed successfully
    Assert.That(results.Count, Is.EqualTo(ditherers.Count));
    Assert.That(results.All(r => r.ExecutionTime.TotalMilliseconds > 0), Is.True);
  }

  [Test]
  public void BenchmarkOrderedDitherers() {
    using var testImage = PerformanceBenchmark.CreateTestImage(512, 512, PerformanceBenchmark.TestImagePattern.Geometric);
    
    var ditherers = new Dictionary<string, IDitherer> {
      ["Bayer 2x2"] = OrderedDitherer.Bayer2x2,
      ["Bayer 4x4"] = OrderedDitherer.Bayer4x4,
      ["Bayer 8x8"] = OrderedDitherer.Bayer8x8,
      ["Bayer 16x16"] = OrderedDitherer.Bayer16x16,
      ["Halftone 8x8"] = OrderedDitherer.Halftone8x8
    };

    var results = PerformanceBenchmark.BenchmarkMultiple(ditherers, testImage, _testPalette256);
    var report = PerformanceBenchmark.FormatResults(results);
    
    TestContext.WriteLine("Ordered Ditherer Performance:");
    TestContext.WriteLine(report);
    
    // Ordered ditherers should generally be faster than error diffusion
    var fastestTime = results.First().ExecutionTime;
    Assert.That(fastestTime.TotalMilliseconds, Is.LessThan(1000), "Ordered dithering should be fast");
  }

  [Test]
  public void BenchmarkAdvancedDitherers() {
    using var testImage = PerformanceBenchmark.CreateTestImage(256, 256, PerformanceBenchmark.TestImagePattern.Photo);
    
    var ditherers = new Dictionary<string, IDitherer> {
      ["Riemersma Default"] = RiemersmaDitherer.Default,
      ["Riemersma Large"] = RiemersmaDitherer.Large,
      ["White Noise"] = NoiseDitherer.White,
      ["Blue Noise"] = NoiseDitherer.Blue,
      ["Knoll Default"] = KnollDitherer.Default,
      ["Knoll High Quality"] = KnollDitherer.HighQuality
    };

    var results = PerformanceBenchmark.BenchmarkMultiple(ditherers, testImage, _testPalette16);
    var report = PerformanceBenchmark.FormatResults(results);
    
    TestContext.WriteLine("Advanced Ditherer Performance:");
    TestContext.WriteLine(report);
    
    // Advanced algorithms may be slower but should still complete reasonably fast
    Assert.That(results.All(r => r.ExecutionTime.TotalMilliseconds < 5000), Is.True);
  }

  [Test]
  public void CompareImageQualityMetrics() {
    using var originalImage = PerformanceBenchmark.CreateTestImage(128, 128, PerformanceBenchmark.TestImagePattern.Gradient);
    
    var ditherers = new Dictionary<string, IDitherer> {
      ["Floyd-Steinberg"] = MatrixBasedDitherer.FloydSteinberg,
      ["Bayer 4x4"] = OrderedDitherer.Bayer4x4,
      ["Blue Noise"] = NoiseDitherer.Blue,
      ["Knoll Default"] = KnollDitherer.Default
    };

    TestContext.WriteLine("Image Quality Comparison (Lower MSE/Higher PSNR/SSIM = Better):");
    TestContext.WriteLine("Algorithm            MSE      PSNR(dB)  SSIM     Perceptual  Histogram");
    TestContext.WriteLine("------------------------------------------------------------------------");

    foreach (var (name, ditherer) in ditherers) {
      using var ditheredImage = DitherImage(originalImage, ditherer, _testPalette16);
      
      var mse = ImageQualityMetrics.CalculateMSE(originalImage, ditheredImage);
      var psnr = ImageQualityMetrics.CalculatePSNR(originalImage, ditheredImage);
      var ssim = ImageQualityMetrics.CalculateSSIM(originalImage, ditheredImage);
      var perceptual = ImageQualityMetrics.CalculatePerceptualDifference(originalImage, ditheredImage);
      var histogram = ImageQualityMetrics.CalculateHistogramDifference(originalImage, ditheredImage);
      
      TestContext.WriteLine($"{name,-20} {mse,8:F2} {psnr,8:F2}  {ssim,7:F3}  {perceptual,10:F2}  {histogram,9:F3}");
      
      // Quality metrics should be reasonable
      Assert.That(mse, Is.LessThan(20000), $"{name} MSE should be reasonable");
      Assert.That(psnr, Is.GreaterThan(5), $"{name} PSNR should be positive");
      Assert.That(ssim, Is.GreaterThan(-1).And.LessThan(1), $"{name} SSIM should be in valid range");
    }
  }

  [Test]
  public void TestDifferentImagePatterns() {
    var patterns = new[] {
      PerformanceBenchmark.TestImagePattern.Gradient,
      PerformanceBenchmark.TestImagePattern.Noise,
      PerformanceBenchmark.TestImagePattern.Geometric,
      PerformanceBenchmark.TestImagePattern.Photo
    };

    var ditherer = MatrixBasedDitherer.FloydSteinberg;
    
    TestContext.WriteLine("Performance vs Image Pattern:");
    TestContext.WriteLine("Pattern      Time(ms)  Pixels/sec  MSE      PSNR(dB)");
    TestContext.WriteLine("----------------------------------------------------");

    foreach (var pattern in patterns) {
      using var testImage = PerformanceBenchmark.CreateTestImage(200, 200, pattern);
      
      var benchmarkResult = PerformanceBenchmark.BenchmarkDitherer(
        ditherer, pattern.ToString(), testImage, _testPalette16, warmupRuns: 1, measurementRuns: 3);
      
      using var ditheredImage = DitherImage(testImage, ditherer, _testPalette16);
      var mse = ImageQualityMetrics.CalculateMSE(testImage, ditheredImage);
      var psnr = ImageQualityMetrics.CalculatePSNR(testImage, ditheredImage);
      
      TestContext.WriteLine($"{pattern,-12} {benchmarkResult.ExecutionTime.TotalMilliseconds,8:F1} {benchmarkResult.PixelsPerSecond,10:F0} {mse,8:F1} {psnr,8:F2}");
    }
  }

  [Test]
  public void TestPaletteSizeImpact() {
    using var testImage = PerformanceBenchmark.CreateTestImage(256, 256, PerformanceBenchmark.TestImagePattern.Photo);
    var ditherer = MatrixBasedDitherer.FloydSteinberg;
    
    var paletteSizes = new[] { 4, 8, 16, 32, 64, 128, 256 };
    
    TestContext.WriteLine("Performance vs Palette Size:");
    TestContext.WriteLine("Palette Size  Time(ms)  MSE      PSNR(dB)  SSIM");
    TestContext.WriteLine("----------------------------------------------");

    foreach (var size in paletteSizes) {
      var palette = PerformanceBenchmark.CreateTestPalette(size, PerformanceBenchmark.PaletteType.Uniform);
      
      var benchmarkResult = PerformanceBenchmark.BenchmarkDitherer(
        ditherer, $"Palette{size}", testImage, palette, warmupRuns: 1, measurementRuns: 3);
      
      using var ditheredImage = DitherImage(testImage, ditherer, palette);
      var mse = ImageQualityMetrics.CalculateMSE(testImage, ditheredImage);
      var psnr = ImageQualityMetrics.CalculatePSNR(testImage, ditheredImage);
      var ssim = ImageQualityMetrics.CalculateSSIM(testImage, ditheredImage);
      
      TestContext.WriteLine($"{size,12} {benchmarkResult.ExecutionTime.TotalMilliseconds,9:F1} {mse,8:F1} {psnr,8:F2} {ssim,6:F3}");
      
      // Larger palettes should generally produce better quality (lower MSE, higher PSNR/SSIM)
      Assert.That(psnr, Is.GreaterThan(5), $"PSNR should be reasonable for palette size {size}");
    }
  }

  [Test]
  public void StressTestLargeImages() {
    var sizes = new[] { (512, 512), (1024, 1024), (2048, 1024) };
    var ditherer = OrderedDitherer.Bayer4x4; // Use fast ordered dithering for stress test
    
    TestContext.WriteLine("Stress Test - Large Images:");
    TestContext.WriteLine("Size         Time(ms)  Memory(MB)  Pixels/sec");
    TestContext.WriteLine("----------------------------------------------");

    foreach (var (width, height) in sizes) {
      using var testImage = PerformanceBenchmark.CreateTestImage(width, height, PerformanceBenchmark.TestImagePattern.Gradient);
      
      var result = PerformanceBenchmark.BenchmarkDitherer(
        ditherer, $"{width}x{height}", testImage, _testPalette16, warmupRuns: 1, measurementRuns: 1);
      
      TestContext.WriteLine($"{width}x{height,-8} {result.ExecutionTime.TotalMilliseconds,9:F1} {result.MemoryUsed / (1024.0 * 1024.0),10:F2} {result.PixelsPerSecond,10:F0}");
      
      // Ensure reasonable performance even for large images
      Assert.That(result.ExecutionTime.TotalSeconds, Is.LessThan(30), $"Processing {width}x{height} should complete within 30 seconds");
    }
  }

  private static Bitmap DitherImage(Bitmap source, IDitherer ditherer, Color[] palette) {
    var target = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
    var targetData = target.LockBits(
      new Rectangle(0, 0, source.Width, source.Height),
      System.Drawing.Imaging.ImageLockMode.WriteOnly,
      System.Drawing.Imaging.PixelFormat.Format8bppIndexed
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