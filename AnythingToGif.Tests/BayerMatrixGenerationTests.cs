using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class BayerMatrixGenerationTests {

  [Test]
  public void CreateBayer_ValidSizes_DoesNotThrow() {
    var validSizes = new[] { 2, 4, 8, 16, 32, 64 };
    
    foreach (var size in validSizes) {
      Assert.DoesNotThrow(() => {
        var ditherer = OrderedDitherer.CreateBayer(size);
        Assert.That(ditherer, Is.Not.Null, $"Bayer matrix of size {size} should not be null");
      }, $"Should not throw for valid size {size}");
    }
  }

  [Test]
  public void CreateBayer_InvalidSizes_ThrowsArgumentException() {
    var invalidSizes = new[] { 0, 1, 3, 5, 6, 7, 9, 10, 15, 17 };
    
    foreach (var size in invalidSizes) {
      Assert.Throws<ArgumentException>(() => {
        OrderedDitherer.CreateBayer(size);
      }, $"Should throw ArgumentException for invalid size {size}");
    }
  }

  [Test]
  public void CreateBayer_Size2_MatchesExpectedPattern() {
    var ditherer = OrderedDitherer.CreateBayer(2);
    
    // Test that 2x2 produces the expected base pattern by comparing with known Bayer2x2
    using var testBitmap = new Bitmap(2, 2, PixelFormat.Format24bppRgb);
    testBitmap.SetPixel(0, 0, Color.FromArgb(128, 128, 128));
    testBitmap.SetPixel(1, 0, Color.FromArgb(128, 128, 128)); 
    testBitmap.SetPixel(0, 1, Color.FromArgb(128, 128, 128));
    testBitmap.SetPixel(1, 1, Color.FromArgb(128, 128, 128));

    var palette = new[] { Color.Black, Color.White };
    var results1 = new byte[4];
    var results2 = new byte[4];

    // Test generated Bayer matrix
    using (var targetBitmap = new Bitmap(2, 2, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 2, 2),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        ditherer.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 2; ++y)
          for (var x = 0; x < 2; ++x)
            results1[y * 2 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test known Bayer2x2
    using (var targetBitmap = new Bitmap(2, 2, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 2, 2),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        OrderedDitherer.Bayer2x2.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 2; ++y)
          for (var x = 0; x < 2; ++x)
            results2[y * 2 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    CollectionAssert.AreEqual(results1, results2, "Generated 2x2 Bayer matrix should match known Bayer2x2");
  }

  [Test]
  public void CreateBayer_Size4_MatchesExpectedPattern() {
    var ditherer = OrderedDitherer.CreateBayer(4);
    
    // Test that 4x4 produces the expected pattern by comparing with known Bayer4x4
    using var testBitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(testBitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    var palette = new[] { Color.Black, Color.White };
    var results1 = new byte[16];
    var results2 = new byte[16];

    // Test generated Bayer matrix
    using (var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 4, 4),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        ditherer.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 4; ++y)
          for (var x = 0; x < 4; ++x)
            results1[y * 4 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test known Bayer4x4
    using (var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 4, 4),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        OrderedDitherer.Bayer4x4.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 4; ++y)
          for (var x = 0; x < 4; ++x)
            results2[y * 4 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    CollectionAssert.AreEqual(results1, results2, "Generated 4x4 Bayer matrix should match known Bayer4x4");
  }

  [Test]
  public void CreateBayer_Size8_MatchesExpectedPattern() {
    var ditherer = OrderedDitherer.CreateBayer(8);
    
    // Test that 8x8 produces the expected pattern by comparing with known Bayer8x8
    using var testBitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(testBitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    var palette = new[] { Color.Black, Color.White };
    var results1 = new byte[64];
    var results2 = new byte[64];

    // Test generated Bayer matrix
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        ditherer.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            results1[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test known Bayer8x8
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        OrderedDitherer.Bayer8x8.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            results2[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    CollectionAssert.AreEqual(results1, results2, "Generated 8x8 Bayer matrix should match known Bayer8x8");
  }

  [Test]
  public void CreateBayer_Size16_ProducesValidOutput() {
    var ditherer = OrderedDitherer.CreateBayer(16);
    
    using var testBitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(testBitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    var palette = new[] { Color.Black, Color.White };

    using var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 16, 16),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = testBitmap.Lock();
      
      Assert.DoesNotThrow(() => {
        ditherer.Dither(locker, targetData, palette);
      }, "16x16 Bayer matrix should not throw during dithering");

      // Verify output contains valid palette indices
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 16; ++y)
        for (var x = 0; x < 16; ++x) {
          var index = ptr[y * targetData.Stride + x];
          Assert.That(index, Is.InRange(0, palette.Length - 1),
            $"Pixel at ({x},{y}) has invalid palette index {index}");
        }
      }

      // For medium gray, should have both black and white pixels
      int blackCount = 0, whiteCount = 0;
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 16; ++y)
        for (var x = 0; x < 16; ++x) {
          var index = ptr[y * targetData.Stride + x];
          if (index == 0) blackCount++;
          else if (index == 1) whiteCount++;
        }
      }

      Assert.That(blackCount, Is.GreaterThan(0), "Should have some black pixels");
      Assert.That(whiteCount, Is.GreaterThan(0), "Should have some white pixels");

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void CreateBayer_LargerSizes_ProduceValidDitherers() {
    var largeSizes = new[] { 32, 64 };
    
    foreach (var size in largeSizes) {
      var ditherer = OrderedDitherer.CreateBayer(size);
      
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
        }, $"Bayer matrix of size {size} should not throw during dithering");

        // Verify all indices are valid
        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x) {
            var index = ptr[y * targetData.Stride + x];
            Assert.That(index, Is.InRange(0, palette.Length - 1),
              $"Size {size}: Pixel at ({x},{y}) has invalid palette index {index}");
          }
        }

      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }
  }

  [Test]
  public void CreateBayer_DifferentSizes_ProduceDifferentPatterns() {
    // Compare 4x4 vs 8x8 to ensure they produce different patterns
    var bayer4x4 = OrderedDitherer.CreateBayer(4);
    var bayer8x8 = OrderedDitherer.CreateBayer(8);
    
    using var testBitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(testBitmap);
    
    // Create a more varied gradient pattern to make differences more apparent
    for (var x = 0; x < 8; ++x)
    for (var y = 0; y < 8; ++y) {
      var intensity = (int)(127 + 64 * Math.Sin(x * Math.PI / 4) * Math.Cos(y * Math.PI / 4));
      intensity = Math.Max(0, Math.Min(255, intensity));
      graphics.FillRectangle(
        new SolidBrush(Color.FromArgb(intensity, intensity, intensity)),
        x, y, 1, 1
      );
    }

    var palette = new[] { Color.Black, Color.Gray, Color.White };
    var results4x4 = new byte[64];
    var results8x8 = new byte[64];

    // Test 4x4 Bayer
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        bayer4x4.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            results4x4[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test 8x8 Bayer
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = testBitmap.Lock();
        bayer8x8.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            results8x8[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Count differences
    var differences = results4x4.Zip(results8x8, (a, b) => a != b).Count(diff => diff);
    
    Assert.That(differences, Is.GreaterThan(0), 
      $"4x4 and 8x8 Bayer matrices should produce at least some different patterns. Found {differences} differences out of 64 pixels.");
  }
}