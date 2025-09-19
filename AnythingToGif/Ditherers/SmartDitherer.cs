using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using BitmapExtensions = System.Drawing.BitmapExtensions;

namespace AnythingToGif.Ditherers;

/// <summary>
/// AI-powered content-aware ditherer that applies different algorithms to different image regions.
/// </summary>
public readonly struct SmartDitherer(SmartDitheringConfig? config = null) : IDitherer {
  
  private readonly SmartDitheringConfig _config = config ?? SmartDitheringConfig.Default;

  public void Dither(BitmapExtensions.IBitmapLocker source, BitmapData target, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric = null) {
    using var analyzableBitmap = CreateAnalyzableBitmap(source);
    var strategyMap = ContentAnalyzer.AnalyzeImage(analyzableBitmap);
    var ditherers = this.CreateStrategyDitherers();
    this.ApplyRegionAwareDithering(source, target, palette, strategyMap, ditherers, colorDistanceMetric);
  }

  private static Bitmap CreateAnalyzableBitmap(BitmapExtensions.IBitmapLocker source) {
    var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
    
    // Copy from IBitmapLocker to regular Bitmap for analysis
    for (var y = 0; y < source.Height; ++y)
    for (var x = 0; x < source.Width; ++x) {
      bitmap.SetPixel(x, y, source[x, y]);
    }
    
    return bitmap;
  }

  private Dictionary<DitheringStrategy, IDitherer> CreateStrategyDitherers() {
    return new Dictionary<DitheringStrategy, IDitherer> {
      // Structure-preserving: Sharp, edge-aware dithering
      [DitheringStrategy.StructurePreserving] = this._config.StructurePreservingDitherer,
      
      // Smooth gradients: Blue noise characteristics, minimal patterns
      [DitheringStrategy.SmoothGradient] = this._config.SmoothGradientDitherer,
      
      // Detail enhancing: Aggressive error diffusion for textures
      [DitheringStrategy.DetailEnhancing] = this._config.DetailEnhancingDitherer,
      
      // Extreme luminance: Special handling for very dark/bright areas
      [DitheringStrategy.ExtremeLuminance] = this._config.ExtremeLuminanceDitherer,
      
      // Balanced: General-purpose fallback
      [DitheringStrategy.Balanced] = this._config.BalancedDitherer
    };
  }

  private unsafe void ApplyRegionAwareDithering(
    BitmapExtensions.IBitmapLocker source, 
    BitmapData target, 
    IReadOnlyList<Color> palette,
    DitheringStrategy[,] strategyMap,
    Dictionary<DitheringStrategy, IDitherer> ditherers,
    Func<Color, Color, int>? colorDistanceMetric) {
    
    var width = source.Width;
    var height = source.Height;
    var stride = target.Stride;
    var targetData = (byte*)target.Scan0;
    
    var strategyBuffers = this.CreateStrategyBuffers(width, height, ditherers, palette, colorDistanceMetric);
    Console.WriteLine("Applying specialized ditherers...");
    this.ApplyAllDitherers(source, strategyBuffers, palette, colorDistanceMetric);
    
    Console.WriteLine("Compositing results with region-aware blending...");
    this.CompositeResults(targetData, stride, width, height, strategyMap, strategyBuffers);
    
    foreach (var buffer in strategyBuffers.Values)
      Marshal.FreeHGlobal(buffer.Scan0);
  }

  private Dictionary<DitheringStrategy, BitmapData> CreateStrategyBuffers(int width, int height, Dictionary<DitheringStrategy, IDitherer> ditherers, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric) {
    var buffers = new Dictionary<DitheringStrategy, BitmapData>();
    
    foreach (var strategy in ditherers.Keys) {
      // Allocate buffer for this strategy's results
      var buffer = new BitmapData {
        Width = width,
        Height = height,
        Stride = width, // 1 byte per pixel for palette index
        PixelFormat = PixelFormat.Format8bppIndexed,
        Scan0 = Marshal.AllocHGlobal(width * height)
      };
      
      buffers[strategy] = buffer;
    }
    
    return buffers;
  }

  private void ApplyAllDitherers(BitmapExtensions.IBitmapLocker source, Dictionary<DitheringStrategy, BitmapData> strategyBuffers, IReadOnlyList<Color> palette, Func<Color, Color, int>? colorDistanceMetric) {
    var ditherers = this.CreateStrategyDitherers();
    
    foreach (var (strategy, ditherer) in ditherers) {
      if (!strategyBuffers.ContainsKey(strategy))
        continue;

      Console.WriteLine($"   Applying {strategy} ditherer...");
      ditherer.Dither(source, strategyBuffers[strategy], palette, colorDistanceMetric);
    }
  }

  private unsafe void CompositeResults(byte* targetData, int stride, int width, int height, DitheringStrategy[,] strategyMap, Dictionary<DitheringStrategy, BitmapData> strategyBuffers) {
    
    // Track strategy usage for statistics
    var strategyStats = new Dictionary<DitheringStrategy, int>();
    
    for (var y = 0; y < height; ++y)
    for (var x = 0; x < width; ++x) {
      var strategy = strategyMap[x, y];
      var targetOffset = y * stride + x;
      
      // Get the pixel from the appropriate strategy buffer
      if (strategyBuffers.ContainsKey(strategy)) {
        var strategyBuffer = (byte*)strategyBuffers[strategy].Scan0;
        var strategyOffset = y * strategyBuffers[strategy].Stride + x;
        targetData[targetOffset] = strategyBuffer[strategyOffset];
      } else {
        // Fallback to balanced strategy
        var balancedBuffer = (byte*)strategyBuffers[DitheringStrategy.Balanced].Scan0;
        var balancedOffset = y * strategyBuffers[DitheringStrategy.Balanced].Stride + x;
        targetData[targetOffset] = balancedBuffer[balancedOffset];
      }
      
      // Track usage statistics
      strategyStats[strategy] = strategyStats.GetValueOrDefault(strategy, 0) + 1;
    }
    
    // Print interesting statistics
    PrintStrategyStatistics(strategyStats, width * height);
  }

  private static void PrintStrategyStatistics(Dictionary<DitheringStrategy, int> stats, int totalPixels) {
    Console.WriteLine("📊 SMART DITHERING STATS:");
    
    foreach (var (strategy, count) in stats) {
      var percentage = (double)count / totalPixels * 100;
      Console.WriteLine($"   {strategy}: {percentage:F1}% ({count:N0} pixels)");
    }
  }

  // Static factory methods for common configurations
  public static SmartDitherer Default => new(SmartDitheringConfig.Default);
  public static SmartDitherer HighQuality => new(SmartDitheringConfig.HighQuality);
  public static SmartDitherer Fast => new(SmartDitheringConfig.Fast);
}

/// <summary>
/// Configuration for smart dithering strategies.
/// Allows customization of which ditherers to use for each content type.
/// </summary>
public class SmartDitheringConfig(
  IDitherer? structurePreserving = null,
  IDitherer? smoothGradient = null,
  IDitherer? detailEnhancing = null,
  IDitherer? extremeLuminance = null,
  IDitherer? balanced = null) {
  
  public IDitherer StructurePreservingDitherer { get; set; } = structurePreserving ?? MatrixBasedDitherer.Atkinson; // Sharp, preserves edges
  public IDitherer SmoothGradientDitherer { get; set; } = smoothGradient ?? MatrixBasedDitherer.Stucki; // Good for gradients
  public IDitherer DetailEnhancingDitherer { get; set; } = detailEnhancing ?? MatrixBasedDitherer.JarvisJudiceNinke; // Aggressive error diffusion
  public IDitherer ExtremeLuminanceDitherer { get; set; } = extremeLuminance ?? MatrixBasedDitherer.FloydSteinberg; // Reliable fallback
  public IDitherer BalancedDitherer { get; set; } = balanced ?? MatrixBasedDitherer.FloydSteinberg; // Classic choice

  public static SmartDitheringConfig Default => new();
  
  public static SmartDitheringConfig HighQuality => new(
    structurePreserving: StructureAwareDitherer.Default, // Use the fancy structure-aware algorithm
    smoothGradient: MatrixBasedDitherer.Stucki,
    detailEnhancing: MatrixBasedDitherer.JarvisJudiceNinke,
    extremeLuminance: MatrixBasedDitherer.Sierra,
    balanced: MatrixBasedDitherer.FloydSteinberg
  );
  
  public static SmartDitheringConfig Fast => new(
    structurePreserving: MatrixBasedDitherer.Simple,
    smoothGradient: MatrixBasedDitherer.FalseFloydSteinberg, // Faster alternative
    detailEnhancing: MatrixBasedDitherer.FloydSteinberg,
    extremeLuminance: MatrixBasedDitherer.Simple,
    balanced: MatrixBasedDitherer.FalseFloydSteinberg
  );
}