using System;
using System.Drawing;
using System.Drawing.Imaging;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class NoiseDithererTests {

  [Test]
  public void NoiseDitherer_AllVariants_DoNotThrow() {
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
      NoiseDitherer.White,
      NoiseDitherer.WhiteLight,
      NoiseDitherer.WhiteStrong,
      NoiseDitherer.Blue,
      NoiseDitherer.BlueLight,
      NoiseDitherer.BlueStrong,
      NoiseDitherer.Brown,
      NoiseDitherer.BrownLight,
      NoiseDitherer.BrownStrong,
      NoiseDitherer.Pink,
      NoiseDitherer.PinkLight,
      NoiseDitherer.PinkStrong
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
  public void NoiseDitherer_White_ProducesValidOutput() {
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
      NoiseDitherer.White.Dither(locker, targetData, palette);

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
  public void NoiseDitherer_Blue_ProducesValidOutput() {
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
      NoiseDitherer.Blue.Dither(locker, targetData, palette);

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
  public void NoiseDitherer_Brown_ProducesValidOutput() {
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
      NoiseDitherer.Brown.Dither(locker, targetData, palette);

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
  public void NoiseDitherer_Pink_ProducesValidOutput() {
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
      NoiseDitherer.Pink.Dither(locker, targetData, palette);

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
  public void NoiseDitherer_DifferentIntensities_ProduceDifferentResults() {
    // Create a larger test bitmap with gradient to better show noise differences
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);

    // Create a gradient that will be sensitive to noise intensity differences
    for (var x = 0; x < 16; ++x)
    for (var y = 0; y < 16; ++y) {
      // Use values around the threshold where noise makes the biggest difference
      var intensity = 96 + (x + y) * 2; // Range from 96 to 158 (around middle gray)
      intensity = Math.Min(255, intensity);
      graphics.FillRectangle(
        new SolidBrush(Color.FromArgb(intensity, intensity, intensity)),
        x, y, 1, 1
      );
    }

    // Use a 3-color palette to make differences more visible
    var palette = new[] { Color.Black, Color.Gray, Color.White };

    var results = new byte[3][];
    var ditherers = new[] { NoiseDitherer.WhiteLight, NoiseDitherer.White, NoiseDitherer.WhiteStrong };

    for (var i = 0; i < ditherers.Length; ++i) {
      using var targetBitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed);
      var targetData = targetBitmap.LockBits(
        new Rectangle(0, 0, 16, 16),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      try {
        using var locker = bitmap.Lock();
        ditherers[i].Dither(locker, targetData, palette);

        // Copy the result
        results[i] = new byte[256];
        unsafe {
          var ptr = (byte*)targetData.Scan0;
          for (var y = 0; y < 16; ++y)
          for (var x = 0; x < 16; ++x) {
            results[i][y * 16 + x] = ptr[y * targetData.Stride + x];
          }
        }
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }

    // Count differences between intensity levels
    var lightVsNormalDiffs = 0;
    var normalVsStrongDiffs = 0;

    for (var i = 0; i < 256; ++i) {
      if (results[0][i] != results[1][i]) lightVsNormalDiffs++;
      if (results[1][i] != results[2][i]) normalVsStrongDiffs++;
    }

    // At least 10% of pixels should differ between intensity levels for a gradient image
    Assert.That(lightVsNormalDiffs, Is.GreaterThan(25),
      $"Light vs Normal should differ in at least 25 pixels, but only {lightVsNormalDiffs} differ");
    Assert.That(normalVsStrongDiffs, Is.GreaterThan(25),
      $"Normal vs Strong should differ in at least 25 pixels, but only {normalVsStrongDiffs} differ");
  }

  [Test]
  public void NoiseDitherer_Deterministic_ProducesConsistentResults() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.FromArgb(128, 128, 128)); // Medium gray

    // Create a simple palette
    var palette = new[] { Color.Black, Color.White };

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
        NoiseDitherer.White.Dither(locker, targetData, palette);

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
        NoiseDitherer.White.Dither(locker, targetData, palette);

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
    CollectionAssert.AreEqual(results1, results2, "Noise dithering should produce consistent results across runs");
  }
}