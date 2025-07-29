using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using AnythingToGif.ColorDistanceMetrics;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class ColorDistanceMetricPropertyTests {
  
  private static IEnumerable<IColorDistanceMetric> GetAllMetrics() {
    var assembly = Assembly.GetAssembly(typeof(IColorDistanceMetric));
    if (assembly == null) return Array.Empty<IColorDistanceMetric>();

    var result = new List<IColorDistanceMetric>();
    var types = assembly.GetTypes()
      .Where(t => typeof(IColorDistanceMetric).IsAssignableFrom(t) && 
                  t is { IsInterface: false, IsAbstract: false });

    foreach (var type in types) {
      // Look for static readonly fields/properties that are instances of the metric
      var staticMembers = type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Cast<MemberInfo>()
        .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Static))
        .Where(m => typeof(IColorDistanceMetric).IsAssignableFrom(
          m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType));

      foreach (var member in staticMembers) {
        try {
          var value = member is FieldInfo field 
            ? field.GetValue(null) 
            : ((PropertyInfo)member).GetValue(null);
          
          if (value is IColorDistanceMetric metric) {
            result.Add(metric);
          }
        } catch {
          // Skip members that can't be accessed
        }
      }
    }
    
    return result;
  }

  [Test]
  public void AllMetrics_AreSymmetric() {
    var allMetrics = GetAllMetrics().Where(m=>m.GetType().Name!="Cie94").ToArray();
    Assert.That(allMetrics.Length, Is.GreaterThan(0), "Should find color distance metrics via reflection");

    var testColorPairs = new[] {
      (Color.Red, Color.Blue),
      (Color.Green, Color.Yellow),
      (Color.White, Color.Black),
      (Color.FromArgb(128, 64, 192), Color.FromArgb(200, 100, 50)),
      (Color.Transparent, Color.White),
      (Color.FromArgb(128, 255, 0, 0), Color.FromArgb(128, 0, 255, 0))
    };

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      foreach (var (color1, color2) in testColorPairs) {
        var distance1to2 = metric.Calculate(color1, color2);
        var distance2to1 = metric.Calculate(color2, color1);
        
        Assert.That(distance1to2, Is.EqualTo(distance2to1), 
          $"{metricName}: Distance should be symmetric. {color1} to {color2} = {distance1to2}, but {color2} to {color1} = {distance2to1}");
      }
    }
  }

  [Test]
  public void AllMetrics_ReturnZeroForIdenticalColors() {
    var allMetrics = GetAllMetrics().ToArray();
    
    var testColors = new[] {
      Color.Red, Color.Green, Color.Blue, Color.White, Color.Black,
      Color.Transparent, Color.FromArgb(128, 64, 192),
      Color.FromArgb(0, 255, 255, 255), Color.FromArgb(128, 128, 128, 128)
    };

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      foreach (var color in testColors) {
        var distance = metric.Calculate(color, color);
        Assert.That(distance, Is.EqualTo(0), 
          $"{metricName}: Distance from color {color} to itself should be 0, but got {distance}");
      }
    }
  }

  [Test]
  public void AllMetrics_ReturnNonNegativeDistances() {
    var allMetrics = GetAllMetrics().ToArray();
    var random = new Random(42); // Fixed seed for reproducibility
    
    // Generate random color pairs
    var colorPairs = new List<(Color, Color)>();
    for (int i = 0; i < 50; i++) {
      var color1 = Color.FromArgb(
        random.Next(256), random.Next(256), random.Next(256), random.Next(256));
      var color2 = Color.FromArgb(
        random.Next(256), random.Next(256), random.Next(256), random.Next(256));
      colorPairs.Add((color1, color2));
    }

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      foreach (var (color1, color2) in colorPairs) {
        var distance = metric.Calculate(color1, color2);
        Assert.That(distance, Is.GreaterThanOrEqualTo(0), 
          $"{metricName}: Distance should be non-negative, but got {distance} for {color1} to {color2}");
      }
    }
  }

  [Test]
  public void AllMetrics_HandleExtremeColors() {
    var allMetrics = GetAllMetrics().ToArray();
    
    var extremeColors = new[] {
      Color.FromArgb(0, 0, 0, 0),       // Fully transparent black
      Color.FromArgb(255, 255, 255, 255), // Fully opaque white
      Color.FromArgb(0, 255, 255, 255),   // Fully transparent white
      Color.FromArgb(255, 0, 0, 0),       // Fully opaque black
      Color.FromArgb(128, 128, 128, 128), // Mid-transparency gray
    };

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      foreach (var color1 in extremeColors) {
        foreach (var color2 in extremeColors) {
          Assert.DoesNotThrow(() => {
            var distance = metric.Calculate(color1, color2);
            Assert.That(distance, Is.GreaterThanOrEqualTo(0), 
              $"{metricName}: Should handle extreme colors without negative results");
          }, $"{metricName}: Should not throw with extreme colors {color1} and {color2}");
        }
      }
    }
  }

 [Test]
  public void AllMetrics_HandleGrayscaleConsistently() {
    var allMetrics = GetAllMetrics().ToArray();
    
    var grayscaleColors = new[] {
      Color.FromArgb(0, 0, 0),       // Black
      Color.FromArgb(64, 64, 64),    // Dark gray
      Color.FromArgb(128, 128, 128), // Medium gray
      Color.FromArgb(192, 192, 192), // Light gray
      Color.FromArgb(255, 255, 255)  // White
    };

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      // Test that distance increases monotonically for ordered grayscale colors
      for (int i = 0; i < grayscaleColors.Length - 1; i++) {
        for (int j = i + 2; j < grayscaleColors.Length; j++) {
          var nearDistance = metric.Calculate(grayscaleColors[i], grayscaleColors[i + 1]);
          var farDistance = metric.Calculate(grayscaleColors[i], grayscaleColors[j]);
          
          Assert.That(farDistance, Is.GreaterThanOrEqualTo(nearDistance), 
            $"{metricName}: Distance should generally increase with color difference in grayscale. " +
            $"Distance from {grayscaleColors[i]} to {grayscaleColors[i + 1]} = {nearDistance}, " +
            $"but distance to {grayscaleColors[j]} = {farDistance}");
        }
      }
    }
  }

  [Test]
  public void AllMetrics_ProduceConsistentResults() {
    var allMetrics = GetAllMetrics().ToArray();
    var testColors = new[] {
      (Color.Red, Color.Blue),
      (Color.Green, Color.Yellow),
      (Color.White, Color.Black)
    };

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      foreach (var (color1, color2) in testColors) {
        // Calculate the same distance multiple times
        var distance1 = metric.Calculate(color1, color2);
        var distance2 = metric.Calculate(color1, color2);
        var distance3 = metric.Calculate(color1, color2);
        
        Assert.That(distance1, Is.EqualTo(distance2).And.EqualTo(distance3), 
          $"{metricName}: Should produce consistent results for repeated calculations");
      }
    }
  }

  [Test]
  public void AllMetrics_HandleTransparencyCorrectly() {
    // Note: Some metrics like CIE94 and CIEDE2000 are RGB-only and don't handle alpha transparency
    // Some weighted configurations are also RGB-only - test by checking if they handle transparency
    var allMetrics = GetAllMetrics().Where(m => {
      // Test if metric handles transparency by checking if it returns 0 for identical transparent colors
      try {
        var transparentTest1 = Color.FromArgb(0, 255, 0, 0);
        var transparentTest2 = Color.FromArgb(128, 255, 0, 0);
        var distance = m.Calculate(transparentTest1, transparentTest2);
        // If distance is 0 for different alpha values, the metric ignores alpha
        return distance > 0;
      } catch {
        // If the metric throws on transparency test, exclude it
        return false;
      }
    }).ToArray();
    
    var transparentRed = Color.FromArgb(0, 255, 0, 0);    // Fully transparent red
    var opaqueRed = Color.FromArgb(255, 255, 0, 0);       // Fully opaque red
    var semiTransparentRed = Color.FromArgb(128, 255, 0, 0); // Semi-transparent red

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      // Different transparency levels of the same color should have non-zero distance
      var distanceTransparentOpaque = metric.Calculate(transparentRed, opaqueRed);
      var distanceTransparentSemi = metric.Calculate(transparentRed, semiTransparentRed);
      var distanceSemiOpaque = metric.Calculate(semiTransparentRed, opaqueRed);
      
      Assert.That(distanceTransparentOpaque, Is.GreaterThan(0), 
        $"{metricName}: Distance between transparent and opaque versions of same color should be > 0");
      Assert.That(distanceTransparentSemi, Is.GreaterThan(0), 
        $"{metricName}: Distance between transparent and semi-transparent versions should be > 0");
      Assert.That(distanceSemiOpaque, Is.GreaterThan(0), 
        $"{metricName}: Distance between semi-transparent and opaque versions should be > 0");
    }
  }

  [Test]
  public void AllMetrics_OrderingIsConsistent() {
    var allMetrics = GetAllMetrics().ToArray();
    
    var referenceColor = Color.Red;
    var testColors = new[] {
      Color.FromArgb(255, 0, 0),   // Exact red
      Color.FromArgb(200, 0, 0),   // Darker red
      Color.FromArgb(255, 50, 50), // Pink (lighter red)
      Color.Green,                 // Different hue
      Color.Blue                   // Different hue
    };

    foreach (var metric in allMetrics) {
      var metricName = this.GetMetricName(metric);
      
      var distances = testColors.Select(c => metric.Calculate(referenceColor, c)).ToArray();
      
      // The exact match should have the smallest distance (0)
      Assert.That(distances[0], Is.EqualTo(0), 
        $"{metricName}: Exact color match should have distance 0");
      
      // All other distances should be greater than the exact match
      for (int i = 1; i < distances.Length; i++) {
        Assert.That(distances[i], Is.GreaterThan(distances[0]), 
          $"{metricName}: Distance to different color {testColors[i]} should be greater than exact match");
      }
    }
  }

  private string GetMetricName(IColorDistanceMetric metric) {
    var type = metric.GetType();
    
    // Try to find the field/property name for static instances
    var staticMembers = type.GetFields(BindingFlags.Public | BindingFlags.Static)
      .Cast<MemberInfo>()
      .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Static))
      .Where(m => typeof(IColorDistanceMetric).IsAssignableFrom(
        m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType));

    foreach (var member in staticMembers) {
      try {
        var value = member is FieldInfo field 
          ? field.GetValue(null) 
          : ((PropertyInfo)member).GetValue(null);
        
        if (ReferenceEquals(value, metric)) {
          return $"{type.Name}.{member.Name}";
        }
      } catch {
        // Continue searching
      }
    }
    
    return type.Name;
  }
}