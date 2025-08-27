using System.Drawing;
using System.Drawing.Imaging;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class RiemersmaDithererTests {

  [Test]
  public void RiemarsmaDitherer_Default_DoesNotThrow() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Red);
    
    // Create a simple palette
    var palette = new[] { Color.Black, Color.White, Color.Red, Color.Green };
    
    // Create target bitmap data
    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4), 
      ImageLockMode.WriteOnly, 
      PixelFormat.Format8bppIndexed
    );
    
    try {
      using var locker = bitmap.Lock();
      
      // Test that dithering doesn't throw an exception
      Assert.DoesNotThrow(() => {
        RiemersmaDitherer.Default.Dither(locker, targetData, palette);
      });
    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void RiemarsmaDitherer_AllVariants_DoNotThrow() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    
    // Create a gradient
    for (var x = 0; x < 8; ++x) {
      for (var y = 0; y < 8; ++y) {
        var intensity = (x + y) * 255 / 14;
        graphics.FillRectangle(
          new SolidBrush(Color.FromArgb(intensity, intensity, intensity)), 
          x, y, 1, 1
        );
      }
    }
    
    // Create a simple palette
    var palette = new[] { Color.Black, Color.Gray, Color.White };
    
    var ditherers = new[] {
      RiemersmaDitherer.Default,
      RiemersmaDitherer.Small,
      RiemersmaDitherer.Large,
      RiemersmaDitherer.Linear
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
        Assert.DoesNotThrow(() => {
          ditherer.Dither(locker, targetData, palette);
        }, $"Ditherer {ditherer} should not throw");
        
      } finally {
        targetBitmap.UnlockBits(targetData);
      }
    }
  }

  [Test]
  public void RiemarsmaDitherer_ProducesValidOutput() {
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
      RiemersmaDitherer.Default.Dither(locker, targetData, palette);
      
      // Verify that the output contains only valid palette indices
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 4; ++y) {
          for (var x = 0; x < 4; ++x) {
            var index = ptr[y * targetData.Stride + x];
            Assert.That(index, Is.InRange(0, palette.Length - 1), 
              $"Pixel at ({x},{y}) has invalid palette index {index}");
          }
        }
      }
      
    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void RiemersmaDitherer_HandlesEmptyPalette() {
    // Create a simple test bitmap
    using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Red);
    
    // Create empty palette
    var emptyPalette = new Color[0];
    
    // Create target bitmap data
    using var targetBitmap = new Bitmap(4, 4, PixelFormat.Format8bppIndexed);
    var targetData = targetBitmap.LockBits(
      new Rectangle(0, 0, 4, 4), 
      ImageLockMode.WriteOnly, 
      PixelFormat.Format8bppIndexed
    );
    
    try {
      using var locker = bitmap.Lock();
      
      // Test that empty palette doesn't cause crashes
      Assert.DoesNotThrow(() => {
        RiemersmaDitherer.Default.Dither(locker, targetData, emptyPalette);
      });
      
      // Verify all pixels are set to 0 (default for empty palette)
      unsafe {
        var ptr = (byte*)targetData.Scan0;
        for (var y = 0; y < 4; ++y) {
          for (var x = 0; x < 4; ++x) {
            var index = ptr[y * targetData.Stride + x];
            Assert.That(index, Is.EqualTo(0), 
              $"Pixel at ({x},{y}) should be 0 for empty palette, but was {index}");
          }
        }
      }
      
    } finally {
      targetBitmap.UnlockBits(targetData);
    }
  }

  [Test]
  public void RiemersmaDitherer_ProducesDifferentResults() {
    // Create a complex test image that should show differences
    using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
    
    // Create a complex pattern
    for (var y = 0; y < 16; ++y) {
      for (var x = 0; x < 16; ++x) {
        var r = (x * 127 / 16) + 64;
        var g = (y * 127 / 16) + 64;
        var b = ((x + y) * 127 / 32) + 64;
        bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
      }
    }
    
    var palette = new[] { Color.Black, Color.White, Color.Red, Color.Green, Color.Blue };
    
    // Test that different variants produce different results
    using var result1 = CreateDitheredResult(bitmap, RiemersmaDitherer.Default, palette);
    using var result2 = CreateDitheredResult(bitmap, RiemersmaDitherer.Linear, palette);
    
    // Results should be different for this complex image
    Assert.That(BitmapsAreIdentical(result1, result2), Is.False,
      "Hilbert curve and linear traversal should produce different results for complex images");
  }

  private static Bitmap CreateDitheredResult(Bitmap source, IDitherer ditherer, Color[] palette) {
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
}