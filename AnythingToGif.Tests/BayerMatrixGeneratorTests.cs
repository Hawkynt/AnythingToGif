using System;
using System.Collections.Generic;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class BayerMatrixGeneratorTests {

  [Test]
  public void Generate_ValidSizes_ProducesCorrectMatrices() {
    var validSizes = new[] { 2, 4, 8, 16, 32, 64 };
    
    foreach (var size in validSizes) {
      var matrix = BayerMatrixGenerator.Generate(size);
      
      Assert.That(matrix, Is.Not.Null, $"Matrix for size {size} should not be null");
      Assert.That(matrix.GetLength(0), Is.EqualTo(size), $"Matrix height should be {size}");
      Assert.That(matrix.GetLength(1), Is.EqualTo(size), $"Matrix width should be {size}");
      
      // Verify all values are in valid range [0, size*size-1]
      var maxValue = size * size - 1;
      for (var y = 0; y < size; ++y) {
        for (var x = 0; x < size; ++x) {
          var value = matrix[y, x];
          Assert.That(value, Is.InRange(0, maxValue), 
            $"Matrix[{y},{x}] = {value} should be in range [0, {maxValue}]");
        }
      }
    }
  }

  [Test]
  public void Generate_InvalidSizes_ThrowsArgumentException() {
    var invalidSizes = new[] { 0, 1, 3, 5, 6, 7, 9, 10, 15, 17 };
    
    foreach (var size in invalidSizes) {
      Assert.Throws<ArgumentException>(() => {
        BayerMatrixGenerator.Generate(size);
      }, $"Should throw ArgumentException for invalid size {size}");
    }
  }

  [Test]
  public void Generate_Size2_MatchesExpectedBaseCase() {
    var matrix = BayerMatrixGenerator.Generate(2);
    
    var expected = new byte[,] {
      { 0, 2 },
      { 3, 1 }
    };
    
    Assert.That(matrix.GetLength(0), Is.EqualTo(2));
    Assert.That(matrix.GetLength(1), Is.EqualTo(2));
    
    for (var y = 0; y < 2; ++y) {
      for (var x = 0; x < 2; ++x) {
        Assert.That(matrix[y, x], Is.EqualTo(expected[y, x]), 
          $"Matrix[{y},{x}] should match expected base case");
      }
    }
  }

  [Test]
  public void Generate_Size4_MatchesExpectedPattern() {
    var matrix = BayerMatrixGenerator.Generate(4);
    
    var expected = new byte[,] {
      { 0, 8, 2, 10 },
      { 12, 4, 14, 6 },
      { 3, 11, 1, 9 },
      { 15, 7, 13, 5 }
    };
    
    Assert.That(matrix.GetLength(0), Is.EqualTo(4));
    Assert.That(matrix.GetLength(1), Is.EqualTo(4));
    
    for (var y = 0; y < 4; ++y) {
      for (var x = 0; x < 4; ++x) {
        Assert.That(matrix[y, x], Is.EqualTo(expected[y, x]), 
          $"Matrix[{y},{x}] should match expected 4x4 pattern");
      }
    }
  }

  [Test]
  public void Generate_ConsistentResults() {
    // Test that the generator produces consistent results across calls
    var matrix1 = BayerMatrixGenerator.Generate(8);
    var matrix2 = BayerMatrixGenerator.Generate(8);
    
    Assert.That(matrix1.GetLength(0), Is.EqualTo(matrix2.GetLength(0)));
    Assert.That(matrix1.GetLength(1), Is.EqualTo(matrix2.GetLength(1)));
    
    for (var y = 0; y < 8; ++y) {
      for (var x = 0; x < 8; ++x) {
        Assert.That(matrix1[y, x], Is.EqualTo(matrix2[y, x]), 
          $"Matrix values should be consistent across calls at [{y},{x}]");
      }
    }
  }

  [Test]
  public void Generate_AllValuesUnique() {
    // Test that all values in a matrix are unique (property of Bayer matrices)
    var sizes = new[] { 2, 4, 8 };
    
    foreach (var size in sizes) {
      var matrix = BayerMatrixGenerator.Generate(size);
      var values = new HashSet<byte>();
      
      for (var y = 0; y < size; ++y) {
        for (var x = 0; x < size; ++x) {
          var value = matrix[y, x];
          Assert.That(values.Contains(value), Is.False, 
            $"Value {value} at [{y},{x}] should be unique in {size}x{size} matrix");
          values.Add(value);
        }
      }
      
      Assert.That(values.Count, Is.EqualTo(size * size), 
        $"Should have {size * size} unique values in {size}x{size} matrix");
    }
  }
}