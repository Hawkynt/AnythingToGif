using System;
using System.Drawing;  
using System.Drawing.Imaging;
using AnythingToGif.CLI;
using AnythingToGif.Ditherers;
using NUnit.Framework;
using static AnythingToGif.CLI.Options;

namespace AnythingToGif.Tests;

[TestFixture]
public class BayerNCLITests {

  [Test]
  public void BayerN_DefaultValue_DoesNotOverrideDitherer() {
    var options = new Options {
      BayerIndex = 0, // Default value
      _Ditherer = DithererMode.FloydSteinberg
    };
    
    var ditherer = options.Ditherer;
    Assert.That(ditherer, Is.EqualTo(MatrixBasedDitherer.FloydSteinberg), 
      "When BayerN is 0 (default), should use regular ditherer selection");
  }

  [Test]
  public void BayerN_ValidValues_OverrideDitherer() {
    var testCases = new[] {
      (n: 1, expectedSize: 2),
      (n: 2, expectedSize: 4), 
      (n: 3, expectedSize: 8),
      (n: 4, expectedSize: 16),
      (n: 5, expectedSize: 32),
      (n: 6, expectedSize: 64),
      (n: 7, expectedSize: 128),
      (n: 8, expectedSize: 256),
    };

    foreach (var (n, expectedSize) in testCases) {
      var options = new Options {
        BayerIndex = n,
        _Ditherer = DithererMode.FloydSteinberg // This should be ignored
      };
      
      var ditherer = options.Ditherer;
      Assert.That(ditherer, Is.Not.Null, $"BayerN={n} should produce a valid ditherer");
      Assert.That(ditherer, Is.InstanceOf<OrderedDitherer>(), 
        $"BayerN={n} should produce an OrderedDitherer");
      
      // Test that it works like expected
      Assert.DoesNotThrow(() => {
        TestDithererWorksCorrectly(ditherer, expectedSize);
      }, $"BayerN={n} (size {expectedSize}) should work correctly");
    }
  }

  [Test]
  public void BayerN_InvalidValues_DoesNotOverrideDitherer() {
    var invalidValues = new[] { -1, 0, 9, 100, 10 };
    
    foreach (var invalidN in invalidValues) {
      var options = new Options {
        BayerIndex = invalidN,
        _Ditherer = DithererMode.FloydSteinberg
      };
      
      var ditherer = options.Ditherer;
      Assert.That(ditherer, Is.EqualTo(MatrixBasedDitherer.FloydSteinberg), 
        $"Invalid BayerN={invalidN} should fall back to regular ditherer selection");
    }
  }

  [Test]
  public void BayerN3_EquivalentToBayer8x8() {
    var bayerNOptions = new Options { BayerIndex = 3 };
    var bayer8x8Options = new Options { _Ditherer = DithererMode.Bayer8x8 };
    
    var bayerNDitherer = bayerNOptions.Ditherer;
    var bayer8x8Ditherer = bayer8x8Options.Ditherer;
    
    // Test they produce identical results on the same input
    using var testBitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(testBitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    var palette = new[] { Color.Black, Color.White };
    var resultsBayerN = new byte[64];
    var resultsBayer8x8 = new byte[64];

    // Test BayerN=3 
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        bayerNDitherer.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            resultsBayerN[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test Bayer8x8
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        bayer8x8Ditherer.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            resultsBayer8x8[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    CollectionAssert.AreEqual(resultsBayerN, resultsBayer8x8, 
      "BayerN=3 should produce identical results to Bayer8x8");
  }

  [Test]
  public void BayerN_LargerSizes_ProduceValidDithering() {
    var largeNValues = new[] { 4, 5, 6 }; // 16x16, 32x32, 64x64
    
    foreach (var n in largeNValues) {
      var options = new Options { BayerIndex = n };
      var ditherer = options.Ditherer;
      
      using var testBitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
      using var graphics = Graphics.FromImage(testBitmap);
      graphics.Clear(Color.FromArgb(128, 128, 128));

      var palette = new[] { Color.Black, Color.White };

      using var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed);
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        
        Assert.DoesNotThrow(() => {
          ditherer.Dither(locker, targetData, palette);
        }, $"BayerN={n} should not throw during dithering");

        // Verify output is valid
        int blackCount = 0, whiteCount = 0;
        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x) {
            var index = ptr[y * targetData.Stride + x];
            Assert.That(index, Is.InRange(0, palette.Length - 1),
              $"BayerN={n}: Pixel at ({x},{y}) has invalid palette index {index}");
            if (index == 0) blackCount++;
            else if (index == 1) whiteCount++;
          }
        }

        // For medium gray, should have at least some pixels (might be all one color for large matrices)
        Assert.That(blackCount + whiteCount, Is.EqualTo(64), $"BayerN={n} should fill all 64 pixels");
        // Large matrices might produce homogeneous results for small test images, so just verify valid output

      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }
  }

  private void TestDithererWorksCorrectly(IDitherer ditherer, int expectedSize) {
    using var testBitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(testBitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128));

    var palette = new[] { Color.Black, Color.White };

    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = testBitmap.Lock();
      ditherer.Dither(locker, targetData, palette);

      // Verify all pixels have valid palette indices
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 4; ++y)
        for (var x = 0; x < 4; ++x) {
          var index = ptr[y * targetData.Stride + x];
          Assert.That(index, Is.InRange(0, palette.Length - 1),
            $"Expected size {expectedSize}: Pixel at ({x},{y}) has invalid palette index {index}");
        }
      }

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }
}