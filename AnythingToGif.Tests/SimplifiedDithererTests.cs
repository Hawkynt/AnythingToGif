using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class SimplifiedDithererTests {
  
  private static IEnumerable<IDitherer> GetAllDitherers() {
    var assembly = Assembly.GetAssembly(typeof(IDitherer));
    if (assembly == null) return Array.Empty<IDitherer>();

    var result = new List<IDitherer>();
    var types = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);
    
    foreach (var type in types) {
      var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => typeof(IDitherer).IsAssignableFrom(p.PropertyType));
      
      foreach (var property in properties) {
        try {
          var ditherer = property.GetValue(null) as IDitherer;
          if (ditherer != null) {
            result.Add(ditherer);
          }
        } catch {
          // Skip properties that can't be accessed
        }
      }
    }
    
    return result;
  }

  [Test]
  public void AllDitherers_CanBeDiscovered() {
    var allDitherers = GetAllDitherers().ToArray();
    Assert.That(allDitherers.Length, Is.GreaterThan(0), "Should find ditherers via reflection");
    
    // Verify we found common ditherers
    var dithererNames = allDitherers.Select(d => this.GetDithererName(d)).ToArray();
    
    Assert.That(dithererNames, Contains.Item("NoDitherer.Instance"), "Should find NoDitherer.Instance");
    Assert.That(dithererNames.Any(n => n.Contains("OrderedDitherer")), Is.True, "Should find OrderedDitherer ditherers");
    Assert.That(dithererNames.Any(n => n.Contains("FloydSteinberg")), Is.True, "Should find Floyd-Steinberg ditherer");
  }

  [Test]
  public void AllDitherers_ExistAndAreNotNull() {
    var allDitherers = GetAllDitherers().ToArray();
    
    foreach (var ditherer in allDitherers) {
      var dithererName = this.GetDithererName(ditherer);
      Assert.That(ditherer, Is.Not.Null, $"{dithererName}: Ditherer instance should not be null");
    }
  }

  [Test]
  public void AllDitherers_HaveValidTypes() {
    var allDitherers = GetAllDitherers().ToArray();
    
    foreach (var ditherer in allDitherers) {
      var dithererName = this.GetDithererName(ditherer);
      var type = ditherer.GetType();
      
      Assert.That(typeof(IDitherer).IsAssignableFrom(type), Is.True, 
        $"{dithererName}: Should implement IDitherer interface");
      Assert.That(type.IsValueType || type.IsClass, Is.True, 
        $"{dithererName}: Should be a value type or class");
    }
  }

  [Test]
  public void AllDitherers_HaveUniqueInstances() {
    var allDitherers = GetAllDitherers().ToArray();
    var uniqueDitherers = allDitherers.Distinct().ToArray();
    
    // Should have unique instances (allowing for value type behavior)
    Assert.That(uniqueDitherers.Length, Is.GreaterThan(0), "Should have at least one unique ditherer");
    
    // Verify we have multiple different ditherer types
    var uniqueTypes = allDitherers.Select(d => d.GetType()).Distinct().ToArray();
    Assert.That(uniqueTypes.Length, Is.GreaterThan(1), "Should have multiple different ditherer types");
  }

  [Test]
  public void CommonDitherers_ExistByName() {
    var allDitherers = GetAllDitherers().ToArray();
    var dithererNames = allDitherers.Select(d => this.GetDithererName(d)).ToArray();
    
    var expectedDitherers = new[] {
      "NoDitherer.Instance",
      "OrderedDitherer.Bayer2x2",
      "OrderedDitherer.Bayer4x4", 
      "OrderedDitherer.Bayer8x8",
      "MatrixBasedDitherer.FloydSteinberg",
      "MatrixBasedDitherer.JarvisJudiceNinke",
      "MatrixBasedDitherer.Stucki"
    };

    foreach (var expectedName in expectedDitherers) {
      Assert.That(dithererNames, Contains.Item(expectedName), 
        $"Should find ditherer: {expectedName}");
    }
  }

  [Test]
  public void AllDitherers_ImplementCorrectInterface() {
    var allDitherers = GetAllDitherers().ToArray();
    
    foreach (var ditherer in allDitherers) {
      var dithererName = this.GetDithererName(ditherer);
      var type = ditherer.GetType();
      
      // Verify it implements IDitherer
      Assert.That(ditherer, Is.InstanceOf<IDitherer>(), 
        $"{dithererName}: Should implement IDitherer");
      
      // Verify it has the Dither method
      var ditherMethod = type.GetMethod("Dither");
      Assert.That(ditherMethod, Is.Not.Null, 
        $"{dithererName}: Should have Dither method");
    }
  }

  [Test]
  public void DithererDiscovery_IsConsistent() {
    // Call GetAllDitherers multiple times and ensure consistent results
    var ditherers1 = GetAllDitherers().ToArray();
    var ditherers2 = GetAllDitherers().ToArray();
    
    Assert.That(ditherers1.Length, Is.EqualTo(ditherers2.Length), 
      "Should return consistent number of ditherers");
    
    var names1 = ditherers1.Select(this.GetDithererName).OrderBy(n => n).ToArray();
    var names2 = ditherers2.Select(this.GetDithererName).OrderBy(n => n).ToArray();
    
    Assert.That(names1, Is.EqualTo(names2), 
      "Should return consistent ditherer names");
  }

  [Test]
  public void OrderedDitherers_HaveDifferentMatrixSizes() {
    var allDitherers = GetAllDitherers().ToArray();
    var orderedDitherers = allDitherers
      .Where(d => this.GetDithererName(d).Contains("OrderedDitherer"))
      .ToArray();
    
    Assert.That(orderedDitherers.Length, Is.GreaterThanOrEqualTo(4), 
      "Should find at least 4 OrderedDitherers (Bayer2x2, Bayer4x4, Bayer8x8, Halftone8x8)");
    
    var orderedNames = orderedDitherers.Select(this.GetDithererName).ToArray();
    Assert.That(orderedNames, Contains.Item("OrderedDitherer.Bayer2x2"));
    Assert.That(orderedNames, Contains.Item("OrderedDitherer.Bayer4x4"));
    Assert.That(orderedNames, Contains.Item("OrderedDitherer.Bayer8x8"));
    Assert.That(orderedNames, Contains.Item("OrderedDitherer.Halftone8x8"));
  }

  [Test]
  public void MatrixBasedDitherers_ExistInVariety() {
    var allDitherers = GetAllDitherers().ToArray();
    var matrixDitherers = allDitherers
      .Where(d => this.GetDithererName(d).Contains("MatrixBasedDitherer"))
      .ToArray();
    
    Assert.That(matrixDitherers.Length, Is.GreaterThanOrEqualTo(5), 
      "Should find at least 5 matrix-based ditherers");
    
    var matrixNames = matrixDitherers.Select(this.GetDithererName).ToArray();
    Assert.That(matrixNames, Contains.Item("MatrixBasedDitherer.FloydSteinberg"));
    Assert.That(matrixNames, Contains.Item("MatrixBasedDitherer.JarvisJudiceNinke"));
    Assert.That(matrixNames, Contains.Item("MatrixBasedDitherer.Stucki"));
    Assert.That(matrixNames, Contains.Item("MatrixBasedDitherer.Atkinson"));
  }

  private string GetDithererName(IDitherer ditherer) {
    var type = ditherer.GetType();
    
    // Try to find the property name for static instances
    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(p => typeof(IDitherer).IsAssignableFrom(p.PropertyType));
    
    foreach (var prop in properties) {
      try {
        if (ReferenceEquals(prop.GetValue(null), ditherer)) {
          return $"{type.Name}.{prop.Name}";
        }
      } catch {
        // Continue searching
      }
    }
    
    return type.Name;
  }
}