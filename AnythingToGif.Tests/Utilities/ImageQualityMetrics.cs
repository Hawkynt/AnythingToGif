using System;
using System.Drawing;

namespace AnythingToGif.Tests.Utilities;

/// <summary>
///   Provides image quality metrics for comparing original and dithered images.
///   These metrics help evaluate the effectiveness of different dithering algorithms.
/// </summary>
public static class ImageQualityMetrics {
  /// <summary>
  ///   Calculates Mean Squared Error (MSE) between two images.
  ///   Lower values indicate better quality (0 = identical images).
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>MSE value (0 = perfect, higher = worse quality)</returns>
  public static double CalculateMSE(Bitmap original, Bitmap processed) {
    if (original.Width != processed.Width || original.Height != processed.Height)
      throw new ArgumentException("Images must have the same dimensions");

    double sumSquaredErrors = 0;
    var pixelCount = 0;

    for (var y = 0; y < original.Height; ++y)
    for (var x = 0; x < original.Width; ++x) {
      var origPixel = original.GetPixel(x, y);
      var procPixel = processed.GetPixel(x, y);

      var rError = origPixel.R - procPixel.R;
      var gError = origPixel.G - procPixel.G;
      var bError = origPixel.B - procPixel.B;

      sumSquaredErrors += rError * rError + gError * gError + bError * bError;
      pixelCount++;
    }

    return sumSquaredErrors / (pixelCount * 3); // Divide by 3 for RGB channels
  }

  /// <summary>
  ///   Calculates Peak Signal-to-Noise Ratio (PSNR) in decibels.
  ///   Higher values indicate better quality (>30dB is generally good).
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>PSNR in decibels (higher = better quality)</returns>
  public static double CalculatePSNR(Bitmap original, Bitmap processed) {
    var mse = CalculateMSE(original, processed);
    if (mse == 0) return double.PositiveInfinity; // Perfect match

    var maxPixelValue = 255.0; // 8-bit images
    return 20 * Math.Log10(maxPixelValue / Math.Sqrt(mse));
  }

  /// <summary>
  ///   Calculates Structural Similarity Index (SSIM).
  ///   Returns value between -1 and 1, where 1 indicates identical images.
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>SSIM value (-1 to 1, higher = better quality)</returns>
  public static double CalculateSSIM(Bitmap original, Bitmap processed) {
    if (original.Width != processed.Width || original.Height != processed.Height)
      throw new ArgumentException("Images must have the same dimensions");

    // Constants for SSIM calculation
    const double c1 = 6.5025; // (0.01 * 255)^2
    const double c2 = 58.5225; // (0.03 * 255)^2

    // Calculate means
    var (meanOrig, meanProc) = CalculateMeans(original, processed);

    // Calculate variances and covariance
    var (varOrig, varProc, covar) = CalculateVariancesAndCovariance(original, processed, meanOrig, meanProc);

    // SSIM formula
    var numerator = (2 * meanOrig * meanProc + c1) * (2 * covar + c2);
    var denominator = (meanOrig * meanOrig + meanProc * meanProc + c1) * (varOrig + varProc + c2);

    return numerator / denominator;
  }

  /// <summary>
  ///   Calculates perceptual difference using weighted RGB channels.
  ///   Uses luminance-weighted formula similar to human vision.
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>Perceptual difference (0 = identical, higher = more different)</returns>
  public static double CalculatePerceptualDifference(Bitmap original, Bitmap processed) {
    if (original.Width != processed.Width || original.Height != processed.Height)
      throw new ArgumentException("Images must have the same dimensions");

    double totalDifference = 0;
    var pixelCount = 0;

    for (var y = 0; y < original.Height; ++y)
    for (var x = 0; x < original.Width; ++x) {
      var origPixel = original.GetPixel(x, y);
      var procPixel = processed.GetPixel(x, y);

      // Convert to luminance-weighted difference
      var rDiff = (origPixel.R - procPixel.R) * 0.299;
      var gDiff = (origPixel.G - procPixel.G) * 0.587;
      var bDiff = (origPixel.B - procPixel.B) * 0.114;

      totalDifference += Math.Abs(rDiff) + Math.Abs(gDiff) + Math.Abs(bDiff);
      pixelCount++;
    }

    return totalDifference / pixelCount;
  }

  /// <summary>
  ///   Calculates histogram difference between two images.
  ///   Measures color distribution similarity.
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>Histogram difference (0 = identical distribution, higher = more different)</returns>
  public static double CalculateHistogramDifference(Bitmap original, Bitmap processed) {
    var origHist = CalculateHistogram(original);
    var procHist = CalculateHistogram(processed);

    double totalDifference = 0;
    for (var i = 0; i < 256; ++i) {
      totalDifference += Math.Abs(origHist[0][i] - procHist[0][i]); // R
      totalDifference += Math.Abs(origHist[1][i] - procHist[1][i]); // G
      totalDifference += Math.Abs(origHist[2][i] - procHist[2][i]); // B
    }

    var totalPixels = original.Width * original.Height;
    return totalDifference / (totalPixels * 3);
  }

  private static (double meanOrig, double meanProc) CalculateMeans(Bitmap original, Bitmap processed) {
    double sumOrig = 0, sumProc = 0;
    var pixelCount = 0;

    for (var y = 0; y < original.Height; ++y)
    for (var x = 0; x < original.Width; ++x) {
      var origPixel = original.GetPixel(x, y);
      var procPixel = processed.GetPixel(x, y);

      // Convert to grayscale for SSIM calculation
      var origGray = 0.299 * origPixel.R + 0.587 * origPixel.G + 0.114 * origPixel.B;
      var procGray = 0.299 * procPixel.R + 0.587 * procPixel.G + 0.114 * procPixel.B;

      sumOrig += origGray;
      sumProc += procGray;
      pixelCount++;
    }

    return (sumOrig / pixelCount, sumProc / pixelCount);
  }

  private static (double varOrig, double varProc, double covar) CalculateVariancesAndCovariance(
    Bitmap original, Bitmap processed, double meanOrig, double meanProc) {
    double sumVarOrig = 0, sumVarProc = 0, sumCovar = 0;
    var pixelCount = 0;

    for (var y = 0; y < original.Height; ++y)
    for (var x = 0; x < original.Width; ++x) {
      var origPixel = original.GetPixel(x, y);
      var procPixel = processed.GetPixel(x, y);

      var origGray = 0.299 * origPixel.R + 0.587 * origPixel.G + 0.114 * origPixel.B;
      var procGray = 0.299 * procPixel.R + 0.587 * procPixel.G + 0.114 * procPixel.B;

      var origDiff = origGray - meanOrig;
      var procDiff = procGray - meanProc;

      sumVarOrig += origDiff * origDiff;
      sumVarProc += procDiff * procDiff;
      sumCovar += origDiff * procDiff;
      pixelCount++;
    }

    return (sumVarOrig / pixelCount, sumVarProc / pixelCount, sumCovar / pixelCount);
  }

  private static int[][] CalculateHistogram(Bitmap image) {
    var histogram = new int[3][]; // RGB channels
    for (var i = 0; i < 3; ++i)
      histogram[i] = new int[256];

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x) {
      var pixel = image.GetPixel(x, y);
      histogram[0][pixel.R]++; // Red
      histogram[1][pixel.G]++; // Green
      histogram[2][pixel.B]++; // Blue
    }

    return histogram;
  }
}
