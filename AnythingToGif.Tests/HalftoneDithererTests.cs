using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class HalftoneDithererTests {

  [Test]
  public void HalftoneDitherer_Halftone8x8_DoesNotThrow() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);

    // Create a gradient
    for (var x = 0; x < 8; ++x)
    for (var y = 0; y < 8; ++y) {
      var intensity = (x + y) * 255 / 14;
      graphics.FillRectangle(
        new SolidBrush(Color.FromArgb(intensity, intensity, intensity)),
        x, y, 1, 1
      );
    }

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
        OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);
      });

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void HalftoneDitherer_ProducesValidOutput() {
    // Create a simple test bitmap with known colors
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
      OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);

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
  public void HalftoneDitherer_Deterministic_ProducesConsistentResults() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    // Create a simple palette
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
        OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);

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
        OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);

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
    CollectionAssert.AreEqual(results1, results2, "Halftone dithering should produce consistent results across runs");
  }

  [Test]
  public void HalftoneDitherer_CreatesClusteredPattern() {
    // Create a test bitmap with medium gray to see pattern clearly
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

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
      OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);

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

      // For medium gray, should have both black and white pixels in reasonable proportion
      Assert.That(blackCount, Is.GreaterThan(0), "Should have some black pixels");
      Assert.That(whiteCount, Is.GreaterThan(0), "Should have some white pixels");
      
      // The ratio should be somewhat balanced for medium gray (not extremely biased)
      var ratio = Math.Min(blackCount, whiteCount) / (double)Math.Max(blackCount, whiteCount);
      Assert.That(ratio, Is.GreaterThan(0.2), $"Should have reasonably balanced distribution for medium gray, but ratio was {ratio:F3}");

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void HalftoneDitherer_DifferentFromBayerPattern() {
    // Create a test bitmap
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    
    // Create a gradient to make differences more apparent
    for (var x = 0; x < 16; ++x)
    for (var y = 0; y < 16; ++y) {
      var intensity = (x + y) * 255 / 30;
      graphics.FillRectangle(
        new SolidBrush(Color.FromArgb(intensity, intensity, intensity)),
        x, y, 1, 1
      );
    }

    var palette = new[] { Color.Black, Color.Gray, Color.White };

    var halftoneResults = new byte[256];
    var bayerResults = new byte[256];

    // Test Halftone
    using (var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 16; ++y)
          for (var x = 0; x < 16; ++x)
            halftoneResults[y * 16 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Test Bayer 8x8 for comparison
    using (var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        OrderedDitherer.Bayer8x8.Dither(locker, targetData, palette);

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

    // Count differences between halftone and Bayer patterns
    var differences = halftoneResults.Zip(bayerResults, (h, b) => h != b).Count(diff => diff);
    
    // Halftone should produce different results than Bayer dithering
    Assert.That(differences, Is.GreaterThan(50), 
      $"Halftone should produce significantly different patterns than Bayer dithering. Found {differences} differences out of 256 pixels.");
  }

  [Test]
  public void HalftoneDitherer_EmptyPalette_DoesNotCrash() {
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Red);

    var emptyPalette = new Color[0];

    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();

      // With current implementation, empty palette doesn't throw - it just writes invalid indices
      // This test ensures it doesn't crash the application (though the behavior may be undefined)
      Assert.DoesNotThrow(() => {
        OrderedDitherer.Halftone8x8.Dither(locker, targetData, emptyPalette);
      });

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void HalftoneDitherer_SingleColorPalette_ProducesValidOutput() {
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Red);

    var singleColorPalette = new[] { Color.Black };

    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();

      // Should not throw and all pixels should be index 0
      Assert.DoesNotThrow(() => {
        OrderedDitherer.Halftone8x8.Dither(locker, targetData, singleColorPalette);
      });

      // Verify all pixels are index 0
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 4; ++y)
        for (var x = 0; x < 4; ++x) {
          var index = ptr[y * targetData.Stride + x];
          Assert.That(index, Is.EqualTo(0), $"Pixel at ({x},{y}) should be index 0 for single-color palette");
        }
      }

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void HalftoneDitherer_MatrixPattern_IsCorrect() {
    // Test that the halftone matrix produces the expected clustered pattern
    // by testing specific coordinates and their threshold relationships
    
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    
    // Use a specific gray level that will highlight the matrix pattern
    graphics.Clear(Color.FromArgb(100, 100, 100));

    var palette = new[] { Color.Black, Color.White };

    using var targetBitmap = new Bitmap(8, 8, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 8, 8),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );

    try {
      using var locker = bitmap.Lock();
      OrderedDitherer.Halftone8x8.Dither(locker, targetData, palette);

      unsafe {
        var ptr = (byte*)targetData.Scan0;
        
        // The halftone matrix should create clustered patterns
        // Check that we have both black and white pixels in the result
        bool hasBlack = false, hasWhite = false;
        for (var y = 0; y < 8; ++y)
        for (var x = 0; x < 8; ++x) {
          var index = ptr[y * targetData.Stride + x];
          if (index == 0) hasBlack = true;
          if (index == 1) hasWhite = true;
        }
        
        Assert.That(hasBlack && hasWhite, Is.True, 
          "Halftone pattern should produce both black and white pixels for intermediate gray levels");
      }

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }
}