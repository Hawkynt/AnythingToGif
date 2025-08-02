using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

/// <summary>
/// Thomas Knoll's ordered dithering algorithm implementation.
/// 
/// This algorithm generates multiple color candidates for each pixel by accumulating quantization error,
/// then uses an ordered matrix to select the final color from the candidate list. This approach produces
/// high-quality dithering by minimizing the difference between original and dithered pixels.
/// 
/// Based on research from expired Adobe patent and implementations from:
/// - https://bisqwit.iki.fi/story/howto/dither/jy/
/// - https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/
/// </summary>
public readonly struct KnollDitherer(byte[,] matrix, int candidateCount = 16, float errorMultiplier = 0.5f) : IDitherer {

  /// <summary>
  /// Creates a Knoll ditherer using a 4x4 Bayer matrix with default parameters.
  /// This matches the original algorithm's specification.
  /// </summary>
  public static IDitherer Default { get; } = CreateFromBayerSize(4);

  /// <summary>
  /// Creates a Knoll ditherer using an 8x8 Bayer matrix for finer dithering patterns.
  /// </summary>
  public static IDitherer Bayer8x8 { get; } = CreateFromBayerSize(8);

  /// <summary>
  /// Creates a Knoll ditherer with high candidate count for maximum quality.
  /// Uses more candidates per pixel for better color distribution.
  /// </summary>
  public static IDitherer HighQuality { get; } = CreateFromBayerSize(4, candidateCount: 32, errorMultiplier: 0.75f);

  /// <summary>
  /// Creates a Knoll ditherer with fewer candidates for faster processing.
  /// </summary>
  public static IDitherer Fast { get; } = CreateFromBayerSize(2, candidateCount: 8, errorMultiplier: 0.25f);

  /// <summary>
  /// Creates a Knoll ditherer with a Bayer matrix of the specified size.
  /// Reuses the matrix generation logic from OrderedDitherer.
  /// </summary>  
  /// <param name="size">The size of the Bayer matrix (must be power of 2)</param>
  /// <param name="candidateCount">Number of color candidates to generate per pixel</param>
  /// <param name="errorMultiplier">Error accumulation multiplier</param>
  /// <returns>A KnollDitherer with the generated Bayer matrix</returns>
  public static IDitherer CreateFromBayerSize(int size, int candidateCount = 16, float errorMultiplier = 0.5f) 
    => new KnollDitherer(BayerMatrixGenerator.Generate(size), candidateCount, errorMultiplier)
  ;

  public unsafe void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var data = (byte*)target.Scan0;
    var wrapper = new PaletteWrapper(palette, colorDistanceMetric);

    var matrixWidth = matrix.GetLength(1);
    var matrixHeight = matrix.GetLength(0);
    var maxThreshold = matrixWidth * matrixHeight - 1;

    for (var y = 0; y < height; ++y) {
      var offset = y * stride;
      var matrixY = y % matrixHeight;

      for (var x = 0; x < width; ++offset, ++x) {
        var originalColor = source[x, y];
        var matrixX = x % matrixWidth;

        // Generate candidate colors using Knoll's error accumulation method
        var candidates = GenerateCandidates(originalColor, palette, wrapper,candidateCount,errorMultiplier);

        // Handle empty candidates (e.g., empty palette)
        if (candidates.Count == 0) {
          data[offset] = 0; // Default to index 0 for empty palette
          continue;
        }

        // Use matrix threshold to select from candidates
        var thresholdValue = matrix[matrixY, matrixX];
        var candidateIndex = (thresholdValue * candidates.Count) / (maxThreshold + 1);
        candidateIndex = Math.Min(candidateIndex, candidates.Count - 1);

        var selectedColorIndex = candidates[candidateIndex];
        data[offset] = (byte)selectedColorIndex;
      }
    }

    return;

    List<int> GenerateCandidates(Color originalColor, IReadOnlyList<Color> palette, PaletteWrapper wrapper, int candidateCount, float errorMultiplier) {
      var candidates = new List<int>(candidateCount);

      // Handle empty palette edge case
      if (palette.Count == 0)
        return candidates; // Return empty list for empty palette

      // Initialize goal color to the original color
      var goalR = (float)originalColor.R;
      var goalG = (float)originalColor.G;
      var goalB = (float)originalColor.B;
      var goalA = (float)originalColor.A;

      for (var i = 0; i < candidateCount; ++i) {
        // Find closest color to current goal
        var goalColor = Color.FromArgb(
          Math.Max(0, Math.Min(255, (int)goalA)),
          Math.Max(0, Math.Min(255, (int)goalR)),
          Math.Max(0, Math.Min(255, (int)goalG)),
          Math.Max(0, Math.Min(255, (int)goalB))
        );

        var closestIndex = wrapper.FindClosestColorIndex(goalColor);
        var closestColor = palette[closestIndex];
        candidates.Add(closestIndex);

        // Update goal by adding the quantization error multiplied by error factor
        var errorR = (originalColor.R - closestColor.R) * errorMultiplier;
        var errorG = (originalColor.G - closestColor.G) * errorMultiplier;
        var errorB = (originalColor.B - closestColor.B) * errorMultiplier;
        var errorA = (originalColor.A - closestColor.A) * errorMultiplier;

        goalR += errorR;
        goalG += errorG;
        goalB += errorB;
        goalA += errorA;
      }

      // Sort candidates by luminance for better visual distribution
      candidates.Sort((a, b) => {
        var colorA = palette[a];
        var colorB = palette[b];
        var luminanceA = 0.299 * colorA.R + 0.587 * colorA.G + 0.114 * colorA.B;
        var luminanceB = 0.299 * colorB.R + 0.587 * colorB.G + 0.114 * colorB.B;
        return luminanceA.CompareTo(luminanceB);
      });

      return candidates;
    }
  }

}