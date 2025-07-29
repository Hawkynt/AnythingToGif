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
}