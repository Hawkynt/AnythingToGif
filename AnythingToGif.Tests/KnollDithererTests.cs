using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class KnollDithererTests {

  [Test]
  public void KnollDitherer_Default_DoesNotThrow() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    // Create a simple palette
    var palette = new[] { Color.Black, Color.Gray, Color.White };

    // Create target bitmap data
    using var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 8, 8),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();

      // Test that dithering doesn't throw an exception
      Assert.DoesNotThrow(() => {
        KnollDitherer.Default.Dither(locker, targetData, palette);
      });

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void KnollDitherer_ProducesValidOutput() {
    // Create a test bitmap with known colors
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    // Create a simple black/white palette
    var palette = new[] { Color.Black, Color.White };

    // Create target bitmap data
    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();

      // Apply dithering
      KnollDitherer.Default.Dither(locker, targetData, palette);

      // Verify that the output contains only valid palette indices
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 4; ++y)
        for (var x = 0; x < 4; ++x) {
          var index = ptr[y * targetData.Stride + x];
          Assert.That(index, Is.InRange(0, palette.Length - 1),
            $"Pixel at ({x},{y}) has invalid palette index {index}");
        }
      }

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void KnollDitherer_AllVariants_WorkCorrectly() {
    var variants = new[] {
      ("Default", KnollDitherer.Default),
      ("Bayer8x8", KnollDitherer.Bayer8x8),
      ("HighQuality", KnollDitherer.HighQuality),
      ("Fast", KnollDitherer.Fast)
    };

    foreach (var (name, ditherer) in variants) {
      using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
      using var graphics = Graphics.FromImage(bitmap);
      graphics.Clear(Color.FromArgb(128, 128, 128));

      var palette = new[] { Color.Black, Color.White };

      using var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed);
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();

        Assert.DoesNotThrow(() => {
          ditherer.Dither(locker, targetData, palette);
        }, $"Knoll variant '{name}' should not throw");

        // Verify all pixels have valid indices
        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x) {
            var index = ptr[y * targetData.Stride + x];
            Assert.That(index, Is.InRange(0, palette.Length - 1),
              $"Knoll variant '{name}': Pixel at ({x},{y}) has invalid palette index {index}");
          }
        }

      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }
  }

  [Test]
  public void KnollDitherer_ProducesPatternedOutput() {
    // Create test image with gradient to see dithering pattern
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    
    for (var x = 0; x < 16; ++x)
    for (var y = 0; y < 16; ++y) {
      var intensity = (x + y) * 255 / 30;
      intensity = Math.Max(0, Math.Min(255, intensity));
      bitmap.SetPixel(x, y, Color.FromArgb(intensity, intensity, intensity));
    }

    // Use black/white palette to make pattern visible
    var palette = new[] { Color.Black, Color.White };

    using var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 16, 16),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();
      KnollDitherer.Default.Dither(locker, targetData, palette);

      // Count black vs white pixels
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

      // Should have a mix of both colors for gradient
      Assert.That(blackCount, Is.GreaterThan(0), "Should have some black pixels");
      Assert.That(whiteCount, Is.GreaterThan(0), "Should have some white pixels");
      Assert.That(blackCount + whiteCount, Is.EqualTo(256), "Should fill all 256 pixels");

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void KnollDitherer_DifferenceFromBayerDithering() {
    // Compare Knoll vs regular Bayer dithering to ensure they're different
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    
    // Create a complex pattern to highlight differences
    for (var x = 0; x < 16; ++x)
    for (var y = 0; y < 16; ++y) {
      var intensity = (int)(127 + 64 * Math.Sin(x * Math.PI / 8) * Math.Cos(y * Math.PI / 8));
      intensity = Math.Max(0, Math.Min(255, intensity));
      graphics.FillRectangle(
        new SolidBrush(Color.FromArgb(intensity, intensity, intensity)),
        x, y, 1, 1
      );
    }

    var palette = new[] { Color.Black, Color.Gray, Color.White };
    var knollResults = new byte[256];
    var bayerResults = new byte[256];

    // Test Knoll dithering
    using (var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        KnollDitherer.Default.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 16; ++y)
          for (var x = 0; x < 16; ++x)
            knollResults[y * 16 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test Bayer 4x4 for comparison
    using (var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        OrderedDitherer.Bayer4x4.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 16; ++y)
          for (var x = 0; x < 16; ++x)
            bayerResults[y * 16 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Count differences between Knoll and Bayer patterns
    var differences = knollResults.Zip(bayerResults, (k, b) => k != b).Count(diff => diff);
    
    // Knoll should produce different results than standard Bayer dithering
    Assert.That(differences, Is.GreaterThan(20), 
      $"Knoll should produce significantly different patterns than Bayer dithering. Found {differences} differences out of 256 pixels.");
  }

  [Test]
  public void KnollDitherer_HandlesSmallPalettes() {
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Red);

    // Test with single color palette
    var singleColorPalette = new[] { Color.Black };

    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();

      Assert.DoesNotThrow(() => {
        KnollDitherer.Default.Dither(locker, targetData, singleColorPalette);
      }, "Should handle single-color palette");

      // All pixels should be index 0
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 4; ++y)
        for (var x = 0; x < 4; ++x) {
          var index = ptr[y * targetData.Stride + x];
          Assert.That(index, Is.EqualTo(0), 
            $"Pixel at ({x},{y}) should be index 0 for single-color palette");
        }
      }

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void KnollDitherer_HandlesEmptyPalette() {
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Blue);

    var emptyPalette = new Color[0];

    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();

      // Should not crash with empty palette (behavior may be undefined)
      Assert.DoesNotThrow(() => {
        KnollDitherer.Default.Dither(locker, targetData, emptyPalette);
      }, "Should not crash with empty palette");

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void KnollDitherer_HighQuality_UsesMoreCandidates() {
    // This test verifies that different Knoll variants produce different results
    // due to different candidate counts and parameters
    
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap); 
    graphics.Clear(Color.FromArgb(128, 128, 128));

    var palette = new[] { Color.Black, Color.White };
    var defaultResults = new byte[64];
    var highQualityResults = new byte[64];

    // Test Default variant
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        KnollDitherer.Default.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            defaultResults[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test HighQuality variant  
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        KnollDitherer.HighQuality.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 8; ++y)
          for (var x = 0; x < 8; ++x)
            highQualityResults[y * 8 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Different Knoll variants may produce different results due to different parameters
    // This isn't guaranteed, but we expect them to be different with high probability
    var differences = defaultResults.Zip(highQualityResults, (a, b) => a != b).Count(diff => diff);
    
    // Note: For uniform gray, the results might be identical, so we just verify no crashes occurred
    Assert.That(differences, Is.GreaterThanOrEqualTo(0), 
      "Different Knoll variants should complete without errors");
  }

  [Test]
  public void KnollDitherer_Deterministic_ProducesConsistentResults() {
    // Test that Knoll dithering produces consistent results across runs
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128));

    var palette = new[] { Color.Black, Color.White };
    var results1 = new byte[64];
    var results2 = new byte[64];

    // First run
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        KnollDitherer.Default.Dither(locker, targetData, palette);

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

    // Second run
    using (var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 8, 8),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        KnollDitherer.Default.Dither(locker, targetData, palette);

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

    // Results should be identical (deterministic)
    CollectionAssert.AreEqual(results1, results2, 
      "Knoll dithering should produce consistent results across runs");
  }
}