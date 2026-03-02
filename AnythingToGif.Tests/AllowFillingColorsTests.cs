using System;
using System.Drawing;
using System.Linq;
using AnythingToGif.Quantizers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class AllowFillingColorsTests {

  [Test]
  public void AllowFillingColors_True_ShouldFillEmptyPaletteSlots() {
    // Arrange
    var quantizer = new OctreeQuantizer { AllowFillingColors = true };
    var colors = new[] { Color.Red }; // Only one color
    
    // Act - Request 8 colors from just 1 input color
    var result = quantizer.ReduceColorsTo(8, colors);
    
    // Assert
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(8));
    
    // Should have the original color
    Assert.That(result[0], Is.EqualTo(Color.Red));
    
    // Should fill remaining slots with additional colors (not just transparent)
    var nonTransparentColors = result.Where(c => c != Color.Transparent).ToArray();
    Assert.That(nonTransparentColors.Length, Is.GreaterThan(1), 
      "Should fill empty palette slots with additional colors when AllowFillingColors is true");
  }

  [Test]
  public void AllowFillingColors_False_ShouldFillOnlyWithTransparent() {
    // Arrange
    var quantizer = new OctreeQuantizer { AllowFillingColors = false };
    var colors = new[] { Color.Red }; // Only one color
    
    // Act - Request 8 colors from just 1 input color
    var result = quantizer.ReduceColorsTo(8, colors);
    
    // Assert
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(8));
    
    // Should have the original color
    Assert.That(result[0], Is.EqualTo(Color.Red));
    
    // All remaining slots should be transparent
    for (int i = 1; i < result.Length; ++i) {
      Assert.That(result[i], Is.EqualTo(Color.Transparent), 
        $"Palette slot {i} should be transparent when AllowFillingColors is false");
    }
  }

  [Test]
  public void AllowFillingColors_DefaultValue_ShouldBeTrue() {
    // Arrange
    var quantizer = new OctreeQuantizer();
    
    // Assert
    Assert.That(quantizer.AllowFillingColors, Is.True, 
      "AllowFillingColors should default to true");
  }

  [Test]
  public void AllowFillingColors_OctreeQuantizer_ShouldRespectSetting() {
    // Test the main quantizer type to ensure it respects the setting
    var quantizer = new OctreeQuantizer();
    var colors = new[] { Color.Blue, Color.Green }; // Two colors only

    // Test with AllowFillingColors = false
    quantizer.AllowFillingColors = false;
    var result = quantizer.ReduceColorsTo(6, colors);
    
    // Should have original colors and rest transparent
    var nonTransparentCount = result.Count(c => c != Color.Transparent);
    Assert.That(nonTransparentCount, Is.LessThanOrEqualTo(2), 
      "OctreeQuantizer should not add extra colors when AllowFillingColors is false");
      
    // Test with AllowFillingColors = true  
    quantizer.AllowFillingColors = true;
    result = quantizer.ReduceColorsTo(6, colors);
    
    var nonTransparentCount2 = result.Count(c => c != Color.Transparent);
    Assert.That(nonTransparentCount2, Is.GreaterThan(nonTransparentCount), 
      "OctreeQuantizer should add extra colors when AllowFillingColors is true");
  }

  [Test]
  public void AllowFillingColors_WithHistogram_ShouldRespectSetting() {
    // Arrange
    var quantizer = new OctreeQuantizer { AllowFillingColors = false };
    var histogram = new[] { 
      (Color.Red, 100u), 
      (Color.Blue, 50u) 
    };
    
    // Act - Request more colors than we have in histogram
    var result = quantizer.ReduceColorsTo(5, histogram);
    
    // Assert
    Assert.That(result.Length, Is.EqualTo(5));
    
    // Should have the original colors (allowing for color representation differences)
    Assert.That(result[0].ToArgb(), Is.EqualTo(Color.Red.ToArgb()));
    Assert.That(result[1].ToArgb(), Is.EqualTo(Color.Blue.ToArgb()));
    
    // Remaining slots should be transparent
    for (int i = 2; i < result.Length; ++i) {
      Assert.That(result[i], Is.EqualTo(Color.Transparent));
    }
  }

  [Test]
  public void AllowFillingColors_ExactColorCount_ShouldNotFillAnyway() {
    // Arrange
    var quantizer = new OctreeQuantizer { AllowFillingColors = false };
    var colors = new[] { Color.Red, Color.Green, Color.Blue };
    
    // Act - Request exactly the same number of colors we have
    var result = quantizer.ReduceColorsTo(3, colors);
    
    // Assert - Should work the same regardless of AllowFillingColors setting
    Assert.That(result.Length, Is.EqualTo(3));
    
    // Check that the result contains colors similar to our input (quantization may change exact values)
    var inputArgbs = colors.Select(c => c.ToArgb()).ToArray();
    foreach (var resultColor in result) {
      var found = inputArgbs.Any(argb => ColorDistance(Color.FromArgb(argb), resultColor) < 10);
      Assert.That(found, Is.True, $"Result color {resultColor} should be similar to one of the input colors");
    }
  }

  [Test]
  public void AllowFillingColors_SufficientInputColors_ShouldNotFill() {
    // Arrange
    var quantizer = new OctreeQuantizer { AllowFillingColors = false };
    var colors = new[] { Color.Red, Color.Green, Color.Blue };
    
    // Act - Request same number of colors as input
    var result = quantizer.ReduceColorsTo(3, colors);
    
    // Assert - Should not need to fill since we have enough colors  
    Assert.That(result.Length, Is.EqualTo(3));
    
    // No transparent colors should be needed
    var transparentCount = result.Count(c => c == Color.Transparent);
    Assert.That(transparentCount, Is.EqualTo(0),
      "Should not need transparent colors when input has sufficient colors");
  }

  private static int ColorDistance(Color c1, Color c2) {
    return Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
  }
}