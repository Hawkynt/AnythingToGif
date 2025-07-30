using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class ADithererTests {

  [Test]
  public void ADitherer_AllVariants_DoNotThrow() {
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

    var ditherers = new[] {
      ADitherer.XorY149,
      ADitherer.XorY149WithChannel,
      ADitherer.XYArithmetic,
      ADitherer.XYArithmeticWithChannel,
      ADitherer.Uniform
    };

    foreach (var ditherer in ditherers) {
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
        Assert.DoesNotThrow(() => { ditherer.Dither(locker, targetData, palette); }, $"Ditherer {ditherer} should not throw");

      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }
  }

  [Test]
  public void ADitherer_XorY149_ProducesValidOutput() {
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
      ADitherer.XorY149.Dither(locker, targetData, palette);

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
  public void ADitherer_XorY149WithChannel_ProducesValidOutput() {
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
      ADitherer.XorY149WithChannel.Dither(locker, targetData, palette);

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
  public void ADitherer_XYArithmetic_ProducesValidOutput() {
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
      ADitherer.XYArithmetic.Dither(locker, targetData, palette);

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
  public void ADitherer_XYArithmeticWithChannel_ProducesValidOutput() {
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
      ADitherer.XYArithmeticWithChannel.Dither(locker, targetData, palette);

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
  public void ADitherer_Uniform_ProducesValidOutput() {
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
      ADitherer.Uniform.Dither(locker, targetData, palette);

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
  public void ADitherer_Uniform_ProducesConsistentOutput() {
    // Create a simple test bitmap with uniform color
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

      // Apply uniform dithering (should produce same output for all pixels)
      ADitherer.Uniform.Dither(locker, targetData, palette);

      // Get all pixel values
      byte[] values = new byte[16];
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var i = 0; i < 16; ++i) {
          values[i] = ptr[i];
        }
      }

      // All pixels should have the same value since uniform adds constant 0.5 mask
      var firstValue = values[0];
      Assert.That(values.All(v => v == firstValue), Is.True,
        "Uniform dithering should produce consistent output for uniform input");

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void ADitherer_ChannelVsNonChannel_ProduceDifferentResults() {
    // Create a larger test bitmap with strong color variations to amplify channel differences
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    
    // Create a pattern with strong different R,G,B values to maximize channel effects
    for (var x = 0; x < 16; ++x)
    for (var y = 0; y < 16; ++y) {
      // Create strong color variations that should trigger different channel calculations
      var r = (x * 16) % 256;
      var g = (y * 16) % 256;  
      var b = ((x + y) * 8) % 256;
      graphics.FillRectangle(
        new SolidBrush(Color.FromArgb(r, g, b)),
        x, y, 1, 1
      );
    }

    // Create a palette with well-separated colors to maximize differences
    var palette = new[] { Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Magenta, Color.Cyan, Color.Black, Color.White };

    var results1 = new byte[256];
    var results2 = new byte[256];

    // Test XorY149 vs XorY149WithChannel
    using (var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        ADitherer.XorY149.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 16; ++y)
          for (var x = 0; x < 16; ++x)
            results1[y * 16 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    using (var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed)) {
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        ADitherer.XorY149WithChannel.Dither(locker, targetData, palette);

        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 16; ++y)
          for (var x = 0; x < 16; ++x)
            results2[y * 16 + x] = ptr[y * targetData.Stride + x];
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Count differences
    var differences = results1.Zip(results2, (a, b) => a != b).Count(diff => diff);
    
    // At least some pixels should differ between channel and non-channel variants
    Assert.That(differences, Is.GreaterThan(0),
      $"Channel vs non-channel variants should produce different results for color images. Found {differences} differences out of 256 pixels.");
  }

  [Test]
  public void ADitherer_Deterministic_ProducesConsistentResults() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    // Create a simple palette
    var palette = new[] { Color.Black, Color.White };

    foreach (var ditherer in new[] { ADitherer.XorY149, ADitherer.XorY149WithChannel, ADitherer.XYArithmetic, ADitherer.XYArithmeticWithChannel, ADitherer.Uniform }) {
      var results1 = new byte[16];
      var results2 = new byte[16];

      // First run
      using (var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed)) {
        var targetData = targetBitmap.LockBits(
          new Rectangle(0, 0, 4, 4),
          ImageLockMode.WriteOnly,
          PixelFormat.Format8bppIndexed
        );

        try {
          using var locker = bitmap.Lock();
          ditherer.Dither(locker, targetData, palette);

          unsafe {
            var ptr = (byte*)targetData.Scan0;
            for (var i = 0; i < 16; ++i)
              results1[i] = ptr[i];
          }
        } finally {
          targetBitmap.UnlockBits(targetData);
        }
      }

      // Second run
      using (var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed)) {
        var targetData = targetBitmap.LockBits(
          new Rectangle(0, 0, 4, 4),
          ImageLockMode.WriteOnly,
          PixelFormat.Format8bppIndexed
        );

        try {
          using var locker = bitmap.Lock();
          ditherer.Dither(locker, targetData, palette);

          unsafe {
            var ptr = (byte*)targetData.Scan0;
            for (var i = 0; i < 16; ++i)
              results2[i] = ptr[i];
          }
        } finally {
          targetBitmap.UnlockBits(targetData);
        }
      }

      // Results should be identical (deterministic)
      CollectionAssert.AreEqual(results1, results2, $"A-dithering {ditherer} should produce consistent results across runs");
    }
  }

  [Test]
  public void ADitherer_EmptyPalette_DoesNotCrash() {
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
        ADitherer.XorY149.Dither(locker, targetData, emptyPalette);
      });

    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void ADitherer_SingleColorPalette_ProducesValidOutput() {
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
        ADitherer.XorY149.Dither(locker, targetData, singleColorPalette);
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
  public void ADitherer_MaskCalculations_ProduceExpectedRanges() {
    // Test that mask calculations produce values in expected ranges
    // This tests the internal logic without exposing private methods

    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    
    // Use a color that will be sensitive to mask variations (middle gray)
    graphics.Clear(Color.FromArgb(128, 128, 128));

    // Use extreme palette to amplify mask effects
    var palette = new[] { Color.Black, Color.White };

    // Test each variant to ensure they produce varied output patterns
    var variants = new[] {
      ("XorY149", ADitherer.XorY149),
      ("XorY149WithChannel", ADitherer.XorY149WithChannel),
      ("XYArithmetic", ADitherer.XYArithmetic),
      ("XYArithmeticWithChannel", ADitherer.XYArithmeticWithChannel),
      ("Uniform", ADitherer.Uniform)
    };

    foreach (var (name, ditherer) in variants) {
      using var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed);
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        ditherer.Dither(locker, targetData, palette);

        // Count black vs white pixels
        int blackCount = 0, whiteCount = 0;
        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var i = 0; i < 256; ++i) {
            if (ptr[i] == 0) blackCount++;
            else if (ptr[i] == 1) whiteCount++;
          }
        }

        if (name == "Uniform") {
          // Uniform should produce the same result for all pixels
          Assert.That(blackCount == 256 || whiteCount == 256, Is.True,
            $"{name} should produce uniform output (all black or all white)");
        } else {
          // Non-uniform variants should produce mixed results for middle gray
          Assert.That(blackCount > 0 && whiteCount > 0, Is.True,
            $"{name} should produce mixed black and white pixels for middle gray input");
          
          // Should have reasonable distribution (not extremely biased)
          var ratio = Math.Min(blackCount, whiteCount) / (double)Math.Max(blackCount, whiteCount);
          Assert.That(ratio, Is.GreaterThan(0.1), // At least 10% of minority color
            $"{name} should have reasonably balanced distribution, but ratio was {ratio:F3}");
        }

      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }
  }
}