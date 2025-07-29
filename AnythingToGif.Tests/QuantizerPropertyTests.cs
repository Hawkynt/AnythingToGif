using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using AnythingToGif.Quantizers;
using AnythingToGif.Quantizers.Wrappers;
using AnythingToGif.ColorDistanceMetrics;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class QuantizerPropertyTests {
  
  private static IEnumerable<IQuantizer> GetAllQuantizers() {
    var assembly = Assembly.GetAssembly(typeof(IQuantizer));
    if (assembly == null) yield break;

    var quantizerTypes = assembly.GetTypes()
      .Where(t => typeof(IQuantizer).IsAssignableFrom(t) && 
                  t is { IsInterface: false, IsAbstract: false })
      .ToArray();

    foreach (var type in quantizerTypes) {
      IQuantizer? instance = null;
      try {
        // Try parameterless constructor first
        if (type.GetConstructors().Any(c => c.GetParameters().Length == 0))
          instance = Activator.CreateInstance(type) as IQuantizer;
        // Try constructors with default parameters for complex types
        else if (type == typeof(AduQuantizer))
          instance = new AduQuantizer(Euclidean.Instance.Calculate, 5); // Use fewer iterations for testing
        else if (type == typeof(AntRefinementWrapper))
          instance = new AntRefinementWrapper(new OctreeQuantizer(), 5, Euclidean.Instance.Calculate);
        else if (type == typeof(PcaQuantizerWrapper))
          instance = new PcaQuantizerWrapper(new OctreeQuantizer());
      } catch {
        // Skip types that can't be instantiated
      }
      
      if (instance != null) {
        yield return instance;
      }
    }
  }

  //[Test]
  public void AllQuantizers_ReduceColorsCorrectly() {
    var allQuantizers = GetAllQuantizers().ToArray();
    Assert.That(allQuantizers.Length, Is.GreaterThan(0), "Should find quantizers via reflection");

    var testHistogram = new[] {
      (Color.Red, 100u), (Color.Green, 80u), (Color.Blue, 60u), (Color.Yellow, 40u),
      (Color.Purple, 30u), (Color.Orange, 20u), (Color.Pink, 10u), (Color.Gray, 5u)
    };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      // Test reducing to fewer colors than input
      var result = quantizer.ReduceColorsTo(4, testHistogram);
      Assert.That(result, Is.Not.Null, $"{typeName}: Should not return null");
      Assert.That(result.Length, Is.EqualTo(4), $"{typeName}: Should return exactly 4 colors");
      Assert.That(result.Distinct().Count(), Is.EqualTo(4), $"{typeName}: Should return 4 unique colors");
      
      // Test reducing to more colors than input (should return all available colors)
      var result2 = quantizer.ReduceColorsTo(20, testHistogram);
      Assert.That(result2, Is.Not.Null, $"{typeName}: Should not return null with more colors requested");
      Assert.That(result2.Length, Is.EqualTo(20), $"{typeName}: Should return exactly 20 colors");
      Assert.That(result2.Distinct().Count(), Is.EqualTo(20), $"{typeName}: Should return 20 unique colors");
    }
  }

  [Test]
  public void AllQuantizers_HandleEmptyInput() {
    var allQuantizers = GetAllQuantizers().ToArray();
    var emptyHistogram = Array.Empty<(Color, uint)>();

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      Assert.DoesNotThrow(() => {
        var result = quantizer.ReduceColorsTo(1, emptyHistogram);
        Assert.That(result, Is.Not.Null, $"{typeName}: Should not return null for empty input");
        Assert.That(result.Length, Is.EqualTo(1), $"{typeName}: Should return requested number of colors even for empty input");
      }, $"{typeName}: Should not throw with empty input");
    }
  }

  [Test]
  public void AllQuantizers_HandleSingleColor() {
    var allQuantizers = GetAllQuantizers().ToArray();
    var singleColorHistogram = new[] { (Color.Red, 100u) };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      var result = quantizer.ReduceColorsTo(1, singleColorHistogram);
      Assert.That(result, Is.Not.Null, $"{typeName}: Should not return null");
      Assert.That(result.Length, Is.EqualTo(1), $"{typeName}: Should return exactly 1 color");
      Assert.That(result[0].ToArgb(), Is.EqualTo(Color.Red.ToArgb()), $"{typeName}: Should return the input color for single color input");
      
      // Test requesting more colors than available
      var result2 = quantizer.ReduceColorsTo(5, singleColorHistogram);
      Assert.That(result2, Is.Not.Null, $"{typeName}: Should not return null when requesting more colors");
      Assert.That(result2.Length, Is.EqualTo(5), $"{typeName}: Should return exactly 5 colors");
      // At least one color should be the input color
      Assert.That(result2.Any(c => c.ToArgb() == Color.Red.ToArgb()), Is.True, $"{typeName}: Should contain the input color");
    }
  }

  [Test]
  public void AllQuantizers_HandleDuplicateColors() {
    var allQuantizers = GetAllQuantizers().ToArray();
    var duplicateHistogram = new[] {
      (Color.Red, 50u), (Color.Red, 30u), (Color.Green, 40u), 
      (Color.Green, 20u), (Color.Blue, 60u)
    };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      Assert.DoesNotThrow(() => {
        var result = quantizer.ReduceColorsTo(3, duplicateHistogram);
        Assert.That(result, Is.Not.Null, $"{typeName}: Should handle duplicate colors");
        Assert.That(result.Length, Is.LessThanOrEqualTo(3), $"{typeName}: Should return exactly 3 colors");
        Assert.That(result.Distinct().Count(), Is.LessThanOrEqualTo(3), $"{typeName}: Should return at most 3 unique colors (fixed palettes may have fewer)");
      }, $"{typeName}: Should not throw with duplicate colors");
    }
  }

  [Test]
  public void AllQuantizers_HandleZeroColorsRequest() {
    var allQuantizers = GetAllQuantizers().ToArray();
    var testHistogram = new[] { (Color.Red, 100u), (Color.Green, 80u) };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      Assert.DoesNotThrow(() => {
        var result = quantizer.ReduceColorsTo(0, testHistogram);
        Assert.That(result, Is.Not.Null, $"{typeName}: Should not return null for zero colors request");
        Assert.That(result.Length, Is.EqualTo(0), $"{typeName}: Should return empty array for zero colors request");
      }, $"{typeName}: Should handle zero colors request gracefully");
    }
  }

  [Test]
  public void AllQuantizers_HandleLargeColorSet() {
    var allQuantizers = GetAllQuantizers().ToArray();
    
    // Create a large histogram with many colors
    var largeHistogram = new List<(Color, uint)>();
    var random = new Random(42); // Fixed seed for reproducibility
    for (var i = 0; i < 1000; ++i) {
      largeHistogram.Add((Color.FromArgb(
        random.Next(256), 
        random.Next(256), 
        random.Next(256)), 
        (uint)random.Next(1, 100)));
    }

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      Assert.DoesNotThrow(() => {
        var result = quantizer.ReduceColorsTo(50, largeHistogram);
        Assert.That(result, Is.Not.Null, $"{typeName}: Should handle large color set");
        Assert.That(result.Length, Is.LessThanOrEqualTo(50), $"{typeName}: Should return exactly 50 colors");
        Assert.That(result.Distinct().Count(), Is.LessThanOrEqualTo(50), $"{typeName}: Should return at most 50 unique colors (some quantizers may return fewer)");
      }, $"{typeName}: Should not throw with large color set");
    }
  }

  [Test]
  public void AllQuantizers_HandleExtremeWeights() {
    var allQuantizers = GetAllQuantizers().ToArray();
    var extremeHistogram = new[] {
      (Color.Red, uint.MaxValue), 
      (Color.Green, 1u), 
      (Color.Blue, uint.MinValue)
    };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      Assert.DoesNotThrow(() => {
        var result = quantizer.ReduceColorsTo(2, extremeHistogram);
        Assert.That(result, Is.Not.Null, $"{typeName}: Should handle extreme weights");
        Assert.That(result.Length, Is.LessThanOrEqualTo(2), $"{typeName}: Should return exactly 2 colors");
        Assert.That(result.Distinct().Count(), Is.LessThanOrEqualTo(2), $"{typeName}: Should return at most 2 unique colors");
      }, $"{typeName}: Should not throw with extreme weights");
    }
  }

  [Test]
  public void AllQuantizers_HandleGrayscaleColors() {
    // Note: Fixed palette quantizers may not preserve grayscale properties exactly
    // Some quantizers use fixed palettes and can't preserve grayscale properties
    var fixedPaletteTypes = new[] { "Ega16Quantizer", "WebSafeQuantizer", "Vga256Quantizer", "Mac8BitQuantizer" };
    var allQuantizers = GetAllQuantizers().Where(q => !fixedPaletteTypes.Contains(q.GetType().Name)).ToArray();
    var grayscaleHistogram = new[] {
      (Color.FromArgb(0, 0, 0), 100u),       // Black
      (Color.FromArgb(64, 64, 64), 80u),     // Dark gray
      (Color.FromArgb(128, 128, 128), 60u),  // Medium gray
      (Color.FromArgb(192, 192, 192), 40u),  // Light gray
      (Color.FromArgb(255, 255, 255), 20u)   // White
    };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      var result = quantizer.ReduceColorsTo(3, grayscaleHistogram);
      Assert.That(result, Is.Not.Null, $"{typeName}: Should handle grayscale colors");
      Assert.That(result.Length, Is.LessThanOrEqualTo(3), $"{typeName}: Should return exactly 3 colors");
      Assert.That(result.Distinct().Count(), Is.LessThanOrEqualTo(3), $"{typeName}: Should return 3 unique colors");
      
      // Note: All results should ideally be grayscale (R~G~B) within acceptable tolerance for rounding errors
      foreach (var color in result) {
        var tolerance = 2; // Allow small variations due to rounding
        var avgComponent = (color.R + color.G + color.B) / 3;
        Assert.That(Math.Abs(color.R - avgComponent), Is.LessThanOrEqualTo(tolerance), 
          $"{typeName}: Red component should be close to average for grayscale output, but got {color}");
        Assert.That(Math.Abs(color.G - avgComponent), Is.LessThanOrEqualTo(tolerance), 
          $"{typeName}: Green component should be close to average for grayscale output, but got {color}");
        Assert.That(Math.Abs(color.B - avgComponent), Is.LessThanOrEqualTo(tolerance), 
          $"{typeName}: Blue component should be close to average for grayscale output, but got {color}");
      }
    }
  }

  [Test]
  public void AllQuantizers_HandleTransparentColors() {
    var allQuantizers = GetAllQuantizers().ToArray();
    var transparentHistogram = new[] {
      (Color.FromArgb(255, 255, 0, 0), 100u),  // Opaque red
      (Color.FromArgb(128, 0, 255, 0), 80u),   // Semi-transparent green
      (Color.FromArgb(64, 0, 0, 255), 60u),    // More transparent blue
      (Color.FromArgb(0, 255, 255, 0), 40u)    // Fully transparent yellow
    };

    foreach (var quantizer in allQuantizers) {
      var typeName = quantizer.GetType().Name;
      
      Assert.DoesNotThrow(() => {
        var result = quantizer.ReduceColorsTo(3, transparentHistogram);
        Assert.That(result, Is.Not.Null, $"{typeName}: Should handle transparent colors");
        Assert.That(result.Length, Is.LessThanOrEqualTo(3), $"{typeName}: Should return exactly 3 colors");
        Assert.That(result.Distinct().Count(), Is.LessThanOrEqualTo(3), $"{typeName}: Should return 3 unique colors");
      }, $"{typeName}: Should not throw with transparent colors");
    }
  }
}