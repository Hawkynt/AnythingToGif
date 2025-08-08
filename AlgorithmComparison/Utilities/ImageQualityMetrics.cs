using System;
using System.Drawing;
using System.Linq;

namespace AlgorithmComparison.Utilities;

// Additional image quality metrics implemented below

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
  ///   Calculates Signal-to-Noise Ratio (SNR) between two images.
  ///   Higher values indicate better quality.
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>SNR value in decibels (higher = better quality)</returns>
  public static double CalculateSNR(Bitmap original, Bitmap processed) {
    if (original.Width != processed.Width || original.Height != processed.Height)
      throw new ArgumentException("Images must have the same dimensions");

    double signalPower = 0;
    double noisePower = 0;

    for (var y = 0; y < original.Height; ++y)
    for (var x = 0; x < original.Width; ++x) {
      var origPixel = original.GetPixel(x, y);
      var procPixel = processed.GetPixel(x, y);

      // Signal power (original image)
      signalPower += origPixel.R * origPixel.R + origPixel.G * origPixel.G + origPixel.B * origPixel.B;

      // Noise power (difference)
      var rDiff = origPixel.R - procPixel.R;
      var gDiff = origPixel.G - procPixel.G;
      var bDiff = origPixel.B - procPixel.B;
      noisePower += rDiff * rDiff + gDiff * gDiff + bDiff * bDiff;
    }

    if (noisePower == 0) return double.PositiveInfinity; // Perfect match

    return 10 * Math.Log10(signalPower / noisePower);
  }

  /// <summary>
  ///   Calculates edge preservation ratio between original and processed images.
  ///   Values close to 1.0 indicate good edge preservation.
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>Edge preservation ratio (1.0 = perfect preservation)</returns>
  public static double CalculateEdgePreservation(Bitmap original, Bitmap processed) {
    var origEdginess = CalculateEdginess(original);
    var procEdginess = CalculateEdginess(processed);

    if (origEdginess == 0) return 1.0; // No edges in original

    return Math.Min(procEdginess / origEdginess, 2.0); // Cap at 2.0 to handle edge enhancement
  }

  /// <summary>
  ///   Calculates edge detection metric using Sobel operator.
  ///   Measures how well edges are preserved after dithering.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Average edge magnitude (higher = more edges)</returns>
  public static double CalculateEdginess(Bitmap image) {
    double totalEdgeMagnitude = 0;
    var pixelCount = 0;

    // Sobel operator kernels
    int[,] sobelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
    int[,] sobelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

    for (var y = 1; y < image.Height - 1; ++y)
    for (var x = 1; x < image.Width - 1; ++x) {
      double gx = 0, gy = 0;

      // Apply Sobel kernels
      for (var ky = -1; ky <= 1; ++ky)
      for (var kx = -1; kx <= 1; ++kx) {
        var pixel = image.GetPixel(x + kx, y + ky);
        var gray = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

        gx += gray * sobelX[ky + 1, kx + 1];
        gy += gray * sobelY[ky + 1, kx + 1];
      }

      // Calculate edge magnitude
      var magnitude = Math.Sqrt(gx * gx + gy * gy);
      totalEdgeMagnitude += magnitude;
      pixelCount++;
    }

    return totalEdgeMagnitude / pixelCount;
  }

  /// <summary>
  ///   Calculates contrast metric using standard deviation of luminance.
  ///   Higher values indicate better contrast.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Contrast value (standard deviation of luminance)</returns>
  public static double CalculateContrast(Bitmap image) {
    var luminanceValues = new System.Collections.Generic.List<double>();

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x) {
      var pixel = image.GetPixel(x, y);
      var luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
      luminanceValues.Add(luminance);
    }

    if (luminanceValues.Count == 0) return 0;

    var mean = luminanceValues.Average();
    var variance = luminanceValues.Select(l => (l - mean) * (l - mean)).Average();

    return Math.Sqrt(variance);
  }

  /// <summary>
  ///   Calculates color count in the image.
  ///   Useful for evaluating quantization effectiveness.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Number of unique colors</returns>
  public static int CalculateColorCount(Bitmap image) {
    var uniqueColors = new System.Collections.Generic.HashSet<int>();

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x) {
      var pixel = image.GetPixel(x, y);
      uniqueColors.Add(pixel.ToArgb());
    }

    return uniqueColors.Count;
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

  /// <summary>
  ///   Calculates histogram difference between two images using Chi-squared metric.
  ///   Measures color distribution similarity more accurately than simple difference.
  /// </summary>
  /// <param name="original">Original image</param>
  /// <param name="processed">Processed/dithered image</param>
  /// <returns>Chi-squared histogram difference (0 = identical distribution)</returns>
  public static double CalculateHistogramDifference(Bitmap original, Bitmap processed) {
    var origHist = CalculateRGBHistogram(original);
    var procHist = CalculateRGBHistogram(processed);

    double chiSquared = 0;
    var totalPixels = original.Width * original.Height;

    for (var channel = 0; channel < 3; ++channel) {
      for (var i = 0; i < 256; ++i) {
        var expected = (double)origHist[channel][i] / totalPixels;
        var observed = (double)procHist[channel][i] / totalPixels;
        
        if (expected > 0) {
          var diff = observed - expected;
          chiSquared += (diff * diff) / expected;
        }
      }
    }

    return chiSquared;
  }

  /// <summary>
  ///   Calculates unique color count in the image.
  ///   Useful for evaluating quantization effectiveness.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Number of unique colors</returns>
  public static int CalculateUniqueColorCount(Bitmap image) {
    var uniqueColors = new System.Collections.Generic.HashSet<int>();

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x) {
      var pixel = image.GetPixel(x, y);
      uniqueColors.Add(pixel.ToArgb());
    }

    return uniqueColors.Count;
  }

  /// <summary>
  ///   Calculates histogram color count - number of bins with non-zero values.
  ///   Measures color distribution complexity.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Number of populated histogram bins</returns>
  public static int CalculateHistogramColorCount(Bitmap image) {
    var histogram = CalculateRGBHistogram(image);
    var populatedBins = 0;

    for (var channel = 0; channel < 3; ++channel) {
      for (var i = 0; i < 256; ++i) {
        if (histogram[channel][i] > 0) {
          populatedBins++;
        }
      }
    }

    return populatedBins;
  }

  /// <summary>
  ///   Calculates entropy of the image's color histogram.
  ///   Measures randomness and complexity of color distribution.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Entropy value in bits</returns>
  public static double CalculateHistogramEntropy(Bitmap image) {
    var histogram = CalculateRGBHistogram(image);
    var totalPixels = image.Width * image.Height;
    double entropy = 0;

    for (var channel = 0; channel < 3; ++channel) {
      for (var i = 0; i < 256; ++i) {
        if (histogram[channel][i] > 0) {
          var probability = (double)histogram[channel][i] / totalPixels;
          entropy -= probability * Math.Log(probability, 2);
        }
      }
    }

    return entropy / 3; // Average across RGB channels
  }

  /// <summary>
  ///   Calculates color spread using standard deviation of RGB values.
  ///   Measures how spread out colors are in the color space.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Color spread metric</returns>
  public static double CalculateColorSpread(Bitmap image) {
    var rValues = new System.Collections.Generic.List<double>();
    var gValues = new System.Collections.Generic.List<double>();
    var bValues = new System.Collections.Generic.List<double>();

    for (var y = 0; y < image.Height; ++y)
    for (var x = 0; x < image.Width; ++x) {
      var pixel = image.GetPixel(x, y);
      rValues.Add(pixel.R);
      gValues.Add(pixel.G);
      bValues.Add(pixel.B);
    }

    var rStdDev = CalculateStandardDeviation(rValues);
    var gStdDev = CalculateStandardDeviation(gValues);
    var bStdDev = CalculateStandardDeviation(bValues);

    return (rStdDev + gStdDev + bStdDev) / 3;
  }

  /// <summary>
  ///   Calculates color uniformity using coefficient of variation.
  ///   Lower values indicate more uniform color distribution.
  /// </summary>
  /// <param name="image">Image to analyze</param>
  /// <returns>Color uniformity metric (0-1, lower = more uniform)</returns>
  public static double CalculateColorUniformity(Bitmap image) {
    var histogram = CalculateRGBHistogram(image);
    var totalPixels = image.Width * image.Height;
    
    double totalVariation = 0;
    var channelCount = 0;

    for (var channel = 0; channel < 3; ++channel) {
      var counts = histogram[channel].Where(c => c > 0).Select(c => (double)c).ToList();
      if (counts.Count > 1) {
        var mean = counts.Average();
        var stdDev = CalculateStandardDeviation(counts);
        if (mean > 0) {
          totalVariation += stdDev / mean; // Coefficient of variation
          channelCount++;
        }
      }
    }

    return channelCount > 0 ? totalVariation / channelCount : 0;
  }

  private static int[][] CalculateRGBHistogram(Bitmap image) {
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

  private static double CalculateStandardDeviation(System.Collections.Generic.IEnumerable<double> values) {
    var valueList = values.ToList();
    if (valueList.Count == 0) return 0;

    var mean = valueList.Average();
    var variance = valueList.Select(v => (v - mean) * (v - mean)).Average();
    return Math.Sqrt(variance);
  }
}