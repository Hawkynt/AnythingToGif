using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AnythingToGif.Ditherers;
using NUnit.Framework;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Tests;

[TestFixture]
public class NewDitherersIntegrationTests {

  [Test]
  public void OstromoukhovDitherer_ShouldProcessImageWithoutErrors() {
    // Arrange
    var ditherer = OstromoukhovDitherer.Instance;
    var palette = CreateTestPalette();
    
    // Act & Assert
    using var testImage = CreateTestImage(32, 32);
    TestDithererExecution(ditherer, testImage, palette);
  }

  [Test]
  public void YliluomaAlgorithms_ShouldProcessImageWithoutErrors() {
    // Arrange
    var algorithms = new[] {
      YliluomaDitherer.Algorithm1,
      YliluomaDitherer.Algorithm2,
      YliluomaDitherer.Algorithm3
    };
    var palette = CreateTestPalette();

    // Act & Assert
    foreach (var ditherer in algorithms) {
      using var testImage = CreateTestImage(32, 32);
      TestDithererExecution(ditherer, testImage, palette);
    }
  }

  [Test]
  public void SerpentineDitherers_ShouldProcessImageWithoutErrors() {
    // Arrange
    var baseDitherers = new IDitherer[] {
      MatrixBasedDitherer.FloydSteinberg,
      MatrixBasedDitherer.Stucki,
      MatrixBasedDitherer.JarvisJudiceNinke,
      MatrixBasedDitherer.Atkinson,
      MatrixBasedDitherer.Burkes
    };
    var palette = CreateTestPalette();

    // Act & Assert
    foreach (var baseDitherer in baseDitherers) {
      using var testImage = CreateTestImage(32, 32);
      var serpentineDitherer = MatrixBasedDitherer.WithSerpentine(baseDitherer);
      TestDithererExecution(serpentineDitherer, testImage, palette);
    }
  }

  [Test]
  public void StructureAwareDitherers_ShouldProcessImageWithoutErrors() {
    // Arrange
    var ditherers = new[] {
      StructureAwareDitherer.Default,
      StructureAwareDitherer.Priority,
      StructureAwareDitherer.Large
    };
    var palette = CreateTestPalette();

    // Act & Assert
    foreach (var ditherer in ditherers) {
      using var testImage = CreateTestImage(32, 32);
      TestDithererExecution(ditherer, testImage, palette);
    }
  }

  [Test]
  public void DizzyDitherers_ShouldProcessImageWithoutErrors() {
    // Arrange
    var ditherers = new[] {
      DizzyDitherer.Default,
      DizzyDitherer.HighQuality,
      DizzyDitherer.Fast
    };
    var palette = CreateTestPalette();

    // Act & Assert
    foreach (var ditherer in ditherers) {
      using var testImage = CreateTestImage(32, 32);
      TestDithererExecution(ditherer, testImage, palette);
    }
  }

  [Test]
  public void AllNewDitherers_ShouldProduceDifferentResults() {
    // Arrange
    var palette = CreateTestPalette();
    using var testImage = CreateTestImage(16, 16);
    
    var ditherers = new IDitherer[] {
      OstromoukhovDitherer.Instance,
      YliluomaDitherer.Algorithm1,
      MatrixBasedDitherer.WithSerpentine(MatrixBasedDitherer.FloydSteinberg),
      StructureAwareDitherer.Default,
      DizzyDitherer.Default
    };

    var results = new byte[ditherers.Length][];

    // Act
    for (int i = 0; i < ditherers.Length; ++i) {
      using var clonedImage = CloneBitmap(testImage);
      results[i] = ExecuteDithererAndGetBytes(ditherers[i], clonedImage, palette);
    }

    // Assert - Each ditherer should produce different results
    for (int i = 0; i < results.Length; ++i) {
      for (int j = i + 1; j < results.Length; ++j) {
        Assert.That(results[i], Is.Not.EqualTo(results[j]),
          $"Ditherers {i} and {j} produced identical results, which is unexpected for different algorithms");
      }
    }
  }

  [Test]
  public void NewDitherers_ShouldRespectPaletteBounds() {
    // Arrange
    var palette = CreateTestPalette();
    var maxPaletteIndex = palette.Length - 1;
    
    var ditherers = new IDitherer[] {
      OstromoukhovDitherer.Instance,
      YliluomaDitherer.Algorithm2,
      MatrixBasedDitherer.WithSerpentine(MatrixBasedDitherer.Stucki),
      StructureAwareDitherer.Priority,
      DizzyDitherer.HighQuality
    };

    // Act & Assert
    foreach (var ditherer in ditherers) {
      using var testImage = CreateTestImage(16, 16);
      var result = ExecuteDithererAndGetBytes(ditherer, testImage, palette);
      
      // All values should be within palette bounds
      Assert.That(result.All(b => b <= maxPaletteIndex),
        $"Ditherer {ditherer.GetType().Name} produced out-of-bounds palette indices");
    }
  }

  private static void TestDithererExecution(IDitherer ditherer, Bitmap testImage, Color[] palette) {
    Assert.DoesNotThrow(() => {
      ExecuteDithererAndGetBytes(ditherer, testImage, palette);
    }, $"Ditherer {ditherer.GetType().Name} should execute without throwing exceptions");
  }

  private static byte[] ExecuteDithererAndGetBytes(IDitherer ditherer, Bitmap image, Color[] palette) {
    var bitmapData = image.LockBits(
      new Rectangle(0, 0, image.Width, image.Height),
      ImageLockMode.WriteOnly,
      PixelFormat.Format8bppIndexed
    );
    
    try {
      using var locker = image.Lock(ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      ditherer.Dither(locker, bitmapData, palette);
      
      // Read back the result
      var bytes = new byte[image.Width * image.Height];
      unsafe {
        var ptr = (byte*)bitmapData.Scan0;
        for (int i = 0; i < bytes.Length; ++i) {
          bytes[i] = ptr[i];
        }
      }
      
      return bytes;
    } finally {
      image.UnlockBits(bitmapData);
    }
  }

  private static Bitmap CreateTestImage(int width, int height) {
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    
    using var g = Graphics.FromImage(bitmap);
    // Create a gradient test pattern
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

  private static Bitmap CloneBitmap(Bitmap source) {
    var clone = new Bitmap(source.Width, source.Height, source.PixelFormat);
    using var g = Graphics.FromImage(clone);
    g.DrawImage(source, 0, 0);
    return clone;
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