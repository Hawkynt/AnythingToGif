using System;

namespace AnythingToGif.Ditherers;

/// <summary>
/// Utility class for generating Bayer dithering matrices using recursive construction.
/// 
/// Bayer matrices are used in ordered dithering to create visually pleasing patterns
/// while maintaining spatial frequency characteristics. The matrices are generated
/// recursively using the formula:
/// 
/// B(2n) = [ 4*B(n) + 0*U(n)   4*B(n) + 2*U(n) ]
///         [ 4*B(n) + 3*U(n)   4*B(n) + 1*U(n) ]
/// 
/// Where B(n) is the n×n Bayer matrix and U(n) is an n×n matrix of all ones,
/// starting with the base case B(2) = [[0,2],[3,1]].
/// </summary>
public static class BayerMatrixGenerator {

  /// <summary>
  /// Generates a Bayer dithering matrix of the specified size using recursive construction.
  /// </summary>
  /// <param name="size">The size of the matrix (must be a power of 2 and at least 2)</param>
  /// <returns>A 2D byte array containing the Bayer matrix</returns>
  /// <exception cref="ArgumentException">Thrown when size is not a power of 2 or less than 2</exception>
  public static byte[,] Generate(int size) {
    if (size < 2 || (size & (size - 1)) != 0)
      throw new ArgumentException("Size must be a power of 2 and at least 2", nameof(size));

    return GenerateRecursive(size);
  }

  /// <summary>
  /// Internal recursive method for generating Bayer matrices.
  /// </summary>
  /// <param name="size">The size of the matrix (must be power of 2)</param>
  /// <returns>A 2D byte array containing the Bayer matrix</returns>
  private static byte[,] GenerateRecursive(int size) {
    if (size == 2) {
      // Base case: 2x2 Bayer matrix
      return new byte[,] {
        { 0, 2 },
        { 3, 1 }
      };
    }
    
    var halfSize = size / 2;
    var smallMatrix = GenerateRecursive(halfSize);
    var result = new byte[size, size];
    
    // Apply the recursive Bayer matrix construction formula:
    // B(2n) = [ 4*B(n) + 0*U(n)   4*B(n) + 2*U(n) ]
    //         [ 4*B(n) + 3*U(n)   4*B(n) + 1*U(n) ]
    for (var y = 0; y < halfSize; ++y) {
      for (var x = 0; x < halfSize; ++x) {
        var baseValue = (byte)(4 * smallMatrix[y, x]);
        
        // Top-left: 4*B(n) + 0
        result[y, x] = baseValue;
        
        // Top-right: 4*B(n) + 2  
        result[y, x + halfSize] = (byte)(baseValue + 2);
        
        // Bottom-left: 4*B(n) + 3
        result[y + halfSize, x] = (byte)(baseValue + 3);
        
        // Bottom-right: 4*B(n) + 1
        result[y + halfSize, x + halfSize] = (byte)(baseValue + 1);
      }
    }
    
    return result;
  }
}