using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using AnythingToGif.ColorDistanceMetrics;
using AnythingToGif.Ditherers;
using AnythingToGif.Quantizers;
using AnythingToGif.Quantizers.FixedPalettes;
using AnythingToGif.Quantizers.Wrappers;
using CommandLine;
using CommandLine.Text;

namespace AnythingToGif.CLI;

internal class Options {

  public enum ColorDistanceMetric {
    [Description("Let application decide")]Default,
    [Description("Euclidean")] Euclidean,
    [Description("Euclidean (RGB only)")] EuclideanRGBOnly,
    [Description("Manhattan")] Manhattan,
    [Description("Manhattan (RGB only)")] ManhattanRGBOnly,
    [Description("CompuPhase")] CompuPhase,
    [Description("Weighted Euclidean (BT.709)")] EuclideanBT709,
    [Description("Weighted Euclidean (Nommyde)")] EuclideanNommyde,
    [Description("Weighted Euclidean (low red component)")] WeightedEuclideanLowRed,
    [Description("Weighted Euclidean (high red component)")] WeightedEuclideanHighRed,
    [Description("Weighted Manhattan (BT.709)")] ManhattanBT709,
    [Description("Weighted Manhattan (Nommyde)")] ManhattanNommyde,
    [Description("Weighted Manhattan (low red component)")] WeightedManhattanLowRed,
    [Description("Weighted Manhattan (high red component)")] WeightedManhattanHighRed,
    [Description("PNGQuant")] PNGQuant,
    [Description("Weighted YUV")] WeightedYuv,
    [Description("Weighted YCbCr")] WeightedYCbCr,
    [Description("CIEDE2000")] CieDe2000,
    [Description("CIE94 Textiles")] Cie94Textiles,
    [Description("CIE94 Graphic Arts")] Cie94GraphicArts,
  }

  public enum QuantizerMode {
    [Description("EGA 16-colors")] Ega16,
    [Description("VGA 256-colors")] Vga256,
    [Description("Web Safe palette")] WebSafe,
    [Description("Mac 8-bit system palette")] Mac8Bit,
    [Description("Median-Cut")] MedianCut,
    [Description("Octree")] Octree,
    [Description("Greedy Orthogonal Bi-Partitioning (Wu)")] GreedyOrthogonalBiPartitioning,
    [Description("Variance-Cut")] VarianceCut,
    [Description("Variance-Based")] VarianceBased,
    [Description("Binary Splitting")] BinarySplitting,
    [Description("Adaptive Distributing Units")] Adu
  }

  public enum DithererMode {
    [Description("None")] None,
    [Description("Floyd-Steinberg")] FloydSteinberg,
    [Description("Equally-Distributed Floyd-Steinberg")] EqualFloydSteinberg,
    [Description("False Floyd-Steinberg")] FalseFloydSteinberg,
    [Description("Jarvis-Judice-Ninke")] JarvisJudiceNinke,
    [Description("Stucki")] Stucki,
    [Description("Atkinson")] Atkinson,
    [Description("Burkes")] Burkes,
    [Description("Sierra")] Sierra,
    [Description("2-row Sierra")] TwoRowSierra,
    [Description("Sierra Lite")] SierraLite,
    [Description("Pigeon")] Pigeon,
    [Description("Stevenson-Arce")] StevensonArce,
    [Description("ShiauFan")] ShiauFan,
    [Description("ShiauFan2")] ShiauFan2,
    [Description("Fan93")] Fan93,
    [Description("TwoD")] TwoD,
    [Description("Down")] Down,
    [Description("DoubleDown")] DoubleDown,
    [Description("Diagonal")] Diagonal,
    [Description("VerticalDiamond")] VerticalDiamond,
    [Description("HorizontalDiamond")] HorizontalDiamond,
    [Description("Diamond")] Diamond,
    [Description("Bayer 2x2")] Bayer2x2,
    [Description("Bayer 4x4")] Bayer4x4,
    [Description("Bayer 8x8")] Bayer8x8,
    [Description("Bayer 16x16")] Bayer16x16,
    [Description("Halftone 8x8")] Halftone8x8,
    [Description("A-Dither XOR-Y149")] ADitherXorY149,
    [Description("A-Dither XOR-Y149 with Channel")] ADitherXorY149WithChannel,
    [Description("A-Dither XY Arithmetic")] ADitherXYArithmetic,
    [Description("A-Dither XY Arithmetic with Channel")] ADitherXYArithmeticWithChannel,
    [Description("A-Dither Uniform")] ADitherUniform,
    [Description("Riemersma (Default)")] RiemersmaDefault,
    [Description("Riemersma (Small)")] RiemersmaSmall,
    [Description("Riemersma (Large)")] RiemersmaLarge,
    [Description("Riemersma (Linear)")] RiemersmaLinear,
    [Description("White Noise (50%)")] WhiteNoise,
    [Description("White Noise (30%)")] WhiteNoiseLight,
    [Description("White Noise (70%)")] WhiteNoiseStrong,
    [Description("Blue Noise (50%)")] BlueNoise,
    [Description("Blue Noise (30%)")] BlueNoiseLight,
    [Description("Blue Noise (70%)")] BlueNoiseStrong,
    [Description("Brown Noise (50%)")] BrownNoise,
    [Description("Brown Noise (30%)")] BrownNoiseLight,
    [Description("Brown Noise (70%)")] BrownNoiseStrong,
    [Description("Pink Noise (50%)")] PinkNoise,
    [Description("Pink Noise (30%)")] PinkNoiseLight,
    [Description("Pink Noise (70%)")] PinkNoiseStrong,
    [Description("Knoll (Default)")] KnollDefault,
    [Description("Knoll (8x8 Bayer)")] KnollBayer8x8,
    [Description("Knoll (High Quality)")] KnollHighQuality,
    [Description("Knoll (Fast)")] KnollFast,
    [Description("N-Closest (Default)")] NClosestDefault,
    [Description("N-Closest (Weighted Random 5)")] NClosestWeightedRandom5,
    [Description("N-Closest (Round Robin 4)")] NClosestRoundRobin4,
    [Description("N-Closest (Luminance 6)")] NClosestLuminance6,
    [Description("N-Closest (Blue Noise 4)")] NClosestBlueNoise4,
    [Description("N-Convex (Default)")] NConvexDefault,
    [Description("N-Convex (Projection 6)")] NConvexProjection6,
    [Description("N-Convex (Spatial Pattern 3)")] NConvexSpatialPattern3,
    [Description("N-Convex (Weighted Random 5)")] NConvexWeightedRandom5,
    [Description("Adaptive (Quality Optimized)")] AdaptiveQualityOptimized,
    [Description("Adaptive (Balanced)")] AdaptiveBalanced,
    [Description("Adaptive (Performance Optimized)")] AdaptivePerformanceOptimized,
    [Description("Adaptive (Smart Selection)")] AdaptiveSmartSelection
  }

  [Value(0, MetaName = "input", HelpText = "Input directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _InputPath { get; set; } = Directory.GetCurrentDirectory();

  [Value(1, MetaName = "output", HelpText = "Output directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _OutputPath { get; set; } = Directory.GetCurrentDirectory();

  [Option('a', "useAntRefinement", Default = false, HelpText = "Whether to apply Ant-tree like iterative refinement after initial quantization.")]
  public bool UseAntRefinement { get; set; }

  [Option('b', "firstSubImageInitsBackground", Default = true, HelpText = "Whether the first sub-image initializes the background.")]
  public bool FirstSubImageInitsBackground { get; set; }

  [Option('c', "colorOrdering", Default = ColorOrderingMode.MostUsedFirst, HelpText = "Color ordering mode.")]
  public ColorOrderingMode ColorOrdering { get; set; }

  [Option('d', "ditherer", Default = DithererMode.FloydSteinberg, HelpText = "Ditherer to use.")]
  public DithererMode _Ditherer { get; set; }

  [Option("bayer", Default = 0, HelpText = "Generate 2^n Bayer matrix (e.g., --bayer 4 creates 16x16 matrix). When specified, overrides --ditherer. Valid range: 1-8.")]
  public int BayerIndex { get; set; }

  [Option('f', "useBackFilling", Default = false, HelpText = "Whether to use backfilling.")]
  public bool UseBackFilling { get; set; }

  [Option('i', "antIterations", Default = 25, HelpText = "Number of iterations for Ant-tree like refinement.")]
  public int AntIterations { get; set; }

  [Option('m', "metric", Default = ColorDistanceMetric.Default, HelpText = "Color distance metric to use.")]
  public ColorDistanceMetric _Metric { get; set; }

  [Option('n', "noCompression", Default = false, HelpText = "Whether to use compressed GIF files or not.")]
  public bool NoCompression { get; set; }

  [Option('p', "usePca", Default = false, HelpText = "Use PCA (Principal Component Analysis) preprocessing before quantization.")]
  public bool UsePca { get; set; }

  [Option('q', "quantizer", Default = QuantizerMode.Octree, HelpText = "Quantizer to use.")]
  public QuantizerMode _Quantizer { get; set; }

  public FileSystemInfo InputPath => File.Exists(this._InputPath) ? new FileInfo(this._InputPath) : new DirectoryInfo(this._InputPath);

  public FileSystemInfo OutputPath => Directory.Exists(this._OutputPath) ? new DirectoryInfo(this._OutputPath) : new FileInfo(this._OutputPath);

  public Func<Color, Color, int>? Metric => this._Metric switch {
    ColorDistanceMetric.Default => null,
    ColorDistanceMetric.Euclidean => Euclidean.Instance.Calculate,
    ColorDistanceMetric.EuclideanBT709 => WeightedEuclidean.BT709.Calculate,
    ColorDistanceMetric.EuclideanNommyde => WeightedEuclidean.Nommyde.Calculate,
    ColorDistanceMetric.EuclideanRGBOnly => WeightedEuclidean.RGBOnly.Calculate,
    ColorDistanceMetric.WeightedEuclideanHighRed => WeightedEuclidean.HighRed.Calculate,
    ColorDistanceMetric.WeightedEuclideanLowRed => WeightedEuclidean.LowRed.Calculate,
    ColorDistanceMetric.Manhattan => Manhattan.Instance.Calculate,
    ColorDistanceMetric.ManhattanBT709 => WeightedManhattan.BT709.Calculate,
    ColorDistanceMetric.ManhattanNommyde => WeightedManhattan.Nommyde.Calculate,
    ColorDistanceMetric.ManhattanRGBOnly => WeightedManhattan.RGBOnly.Calculate,
    ColorDistanceMetric.WeightedManhattanHighRed => WeightedManhattan.HighRed.Calculate,
    ColorDistanceMetric.WeightedManhattanLowRed => WeightedManhattan.LowRed.Calculate,
    ColorDistanceMetric.CompuPhase => CompuPhase.Instance.Calculate,
    ColorDistanceMetric.PNGQuant => PngQuant.Instance.Calculate,
    ColorDistanceMetric.WeightedYCbCr => WeightedYCbCr.Instance.Calculate,
    ColorDistanceMetric.WeightedYuv => WeightedYuv.Instance.Calculate,
    ColorDistanceMetric.CieDe2000 => CieDe2000.Instance.Calculate,
    ColorDistanceMetric.Cie94Textiles => Cie94.Textiles.Calculate,
    ColorDistanceMetric.Cie94GraphicArts => Cie94.GraphicArts.Calculate,
    _ => throw new("Unknown color distance metric")
  };

  public Func<IQuantizer> Quantizer => () => {
    IQuantizer q = this._Quantizer switch {
      QuantizerMode.Ega16 => new Ega16Quantizer(),
      QuantizerMode.Vga256 => new Vga256Quantizer(),
      QuantizerMode.WebSafe => new WebSafeQuantizer(),
      QuantizerMode.Mac8Bit => new Mac8BitQuantizer(),
      QuantizerMode.Octree => new OctreeQuantizer(),
      QuantizerMode.MedianCut => new MedianCutQuantizer(),
      QuantizerMode.GreedyOrthogonalBiPartitioning => new WuQuantizer(),
      QuantizerMode.VarianceCut => new VarianceCutQuantizer(),
      QuantizerMode.VarianceBased => new VarianceBasedQuantizer(),
      QuantizerMode.BinarySplitting => new BinarySplittingQuantizer(),
      QuantizerMode.Adu => new AduQuantizer(this.Metric ?? CompuPhase.Instance.Calculate),
      _ => throw new("Unknown quantizer")
    };

    if (this.UsePca)
      q = new PcaQuantizerWrapper(q);

    if (this.UseAntRefinement)
      q = new AntRefinementWrapper(q, this.AntIterations, this.Metric ?? CompuPhase.Instance.Calculate);

    return q;
  };

  public IDitherer Ditherer {
    get {
      // If BayerN is specified and valid, use it instead of the regular ditherer
      if (this.BayerIndex is >= 1 and <= 8) {
        var size = 1 << this.BayerIndex; // 2^n
        return OrderedDitherer.CreateBayer(size);
      }
      
      // Otherwise use the regular ditherer selection
      return this._Ditherer switch {
        DithererMode.FloydSteinberg => MatrixBasedDitherer.FloydSteinberg,
        DithererMode.EqualFloydSteinberg => MatrixBasedDitherer.EqualFloydSteinberg,
        DithererMode.FalseFloydSteinberg => MatrixBasedDitherer.FalseFloydSteinberg,
        DithererMode.JarvisJudiceNinke => MatrixBasedDitherer.JarvisJudiceNinke,
        DithererMode.Stucki => MatrixBasedDitherer.Stucki,
        DithererMode.Atkinson => MatrixBasedDitherer.Atkinson,
        DithererMode.Burkes => MatrixBasedDitherer.Burkes,
        DithererMode.Sierra => MatrixBasedDitherer.Sierra,
        DithererMode.TwoRowSierra => MatrixBasedDitherer.TwoRowSierra,
        DithererMode.SierraLite => MatrixBasedDitherer.SierraLite,
        DithererMode.Pigeon => MatrixBasedDitherer.Pigeon,
        DithererMode.StevensonArce => MatrixBasedDitherer.StevensonArce,
        DithererMode.ShiauFan => MatrixBasedDitherer.ShiauFan,
        DithererMode.ShiauFan2 => MatrixBasedDitherer.ShiauFan2,
        DithererMode.Fan93 => MatrixBasedDitherer.Fan93,
        DithererMode.TwoD => MatrixBasedDitherer.TwoD,
        DithererMode.Down => MatrixBasedDitherer.Down,
        DithererMode.DoubleDown => MatrixBasedDitherer.DoubleDown,
        DithererMode.Diagonal => MatrixBasedDitherer.Diagonal,
        DithererMode.VerticalDiamond => MatrixBasedDitherer.VerticalDiamond,
        DithererMode.HorizontalDiamond => MatrixBasedDitherer.HorizontalDiamond,
        DithererMode.Diamond => MatrixBasedDitherer.Diamond,
        DithererMode.Bayer2x2 => OrderedDitherer.Bayer2x2,
        DithererMode.Bayer4x4 => OrderedDitherer.Bayer4x4,
        DithererMode.Bayer8x8 => OrderedDitherer.Bayer8x8,
        DithererMode.Bayer16x16 => OrderedDitherer.Bayer16x16,
        DithererMode.Halftone8x8 => OrderedDitherer.Halftone8x8,
        DithererMode.ADitherXorY149 => ADitherer.XorY149,
        DithererMode.ADitherXorY149WithChannel => ADitherer.XorY149WithChannel,
        DithererMode.ADitherXYArithmetic => ADitherer.XYArithmetic,
        DithererMode.ADitherXYArithmeticWithChannel => ADitherer.XYArithmeticWithChannel,
        DithererMode.ADitherUniform => ADitherer.Uniform,
        DithererMode.RiemersmaDefault => RiemersmaDitherer.Default,
        DithererMode.RiemersmaSmall => RiemersmaDitherer.Small,
        DithererMode.RiemersmaLarge => RiemersmaDitherer.Large,
        DithererMode.RiemersmaLinear => RiemersmaDitherer.Linear,
        DithererMode.WhiteNoise => NoiseDitherer.White,
        DithererMode.WhiteNoiseLight => NoiseDitherer.WhiteLight,
        DithererMode.WhiteNoiseStrong => NoiseDitherer.WhiteStrong,
        DithererMode.BlueNoise => NoiseDitherer.Blue,
        DithererMode.BlueNoiseLight => NoiseDitherer.BlueLight,
        DithererMode.BlueNoiseStrong => NoiseDitherer.BlueStrong,
        DithererMode.BrownNoise => NoiseDitherer.Brown,
        DithererMode.BrownNoiseLight => NoiseDitherer.BrownLight,
        DithererMode.BrownNoiseStrong => NoiseDitherer.BrownStrong,
        DithererMode.PinkNoise => NoiseDitherer.Pink,
        DithererMode.PinkNoiseLight => NoiseDitherer.PinkLight,
        DithererMode.PinkNoiseStrong => NoiseDitherer.PinkStrong,
        DithererMode.KnollDefault => KnollDitherer.Default,
        DithererMode.KnollBayer8x8 => KnollDitherer.Bayer8x8,
        DithererMode.KnollHighQuality => KnollDitherer.HighQuality,
        DithererMode.KnollFast => KnollDitherer.Fast,
        DithererMode.NClosestDefault => NClosestDitherer.Default,
        DithererMode.NClosestWeightedRandom5 => NClosestDitherer.WeightedRandom5,
        DithererMode.NClosestRoundRobin4 => NClosestDitherer.RoundRobin4,
        DithererMode.NClosestLuminance6 => NClosestDitherer.Luminance6,
        DithererMode.NClosestBlueNoise4 => NClosestDitherer.BlueNoise4,
        DithererMode.NConvexDefault => NConvexDitherer.Default,
        DithererMode.NConvexProjection6 => NConvexDitherer.Projection6,
        DithererMode.NConvexSpatialPattern3 => NConvexDitherer.SpatialPattern3,
        DithererMode.NConvexWeightedRandom5 => NConvexDitherer.WeightedRandom5,
        DithererMode.AdaptiveQualityOptimized => AdaptiveDitherer.QualityOptimized,
        DithererMode.AdaptiveBalanced => AdaptiveDitherer.Balanced,
        DithererMode.AdaptivePerformanceOptimized => AdaptiveDitherer.PerformanceOptimized,
        DithererMode.AdaptiveSmartSelection => AdaptiveDitherer.SmartSelection,
        DithererMode.None => NoDitherer.Instance,
        _ => NoDitherer.Instance
      };
    }
  }

  public static void HandleParseError<T>(ParserResult<T> result, IEnumerable<Error> errors) {
    var helpText = HelpText.AutoBuild(result, h => {
      var thisAssembly = Assembly.GetExecutingAssembly();
      h.AdditionalNewLineAfterOption = false;
      var title = thisAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
      h.Heading = $"{title} {thisAssembly.GetName().Version}";
      h.Copyright = thisAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? CopyrightInfo.Default;
      h.AddPreOptionsLine(thisAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
      h.AddPreOptionsLine(string.Empty);
      h.AddPreOptionsLine($"Usage: {title} [<input>] [<options>] | <input> <output> [<options>]");

      h.AddPostOptionsLine("Color Distance Metrics:");
      foreach (var mode in Enum.GetValues(typeof(ColorDistanceMetric)))
        h.AddPostOptionsLine($"  {mode}: {GetEnumDescription((ColorDistanceMetric)mode)}");
      h.AddPostOptionsLine(string.Empty);
      
      h.AddPostOptionsLine("Quantizer Modes:");
      foreach (var mode in Enum.GetValues(typeof(QuantizerMode)))
        h.AddPostOptionsLine($"  {mode}: {GetEnumDescription((QuantizerMode)mode)}");
      h.AddPostOptionsLine(string.Empty);

      h.AddPostOptionsLine("Ditherer Modes:");
      foreach (var mode in Enum.GetValues(typeof(DithererMode)))
        h.AddPostOptionsLine($"  {mode}: {GetEnumDescription((DithererMode)mode)}");
      h.AddPostOptionsLine(string.Empty);

      h.AddPostOptionsLine("Color Ordering Modes:");
      foreach (var mode in Enum.GetValues(typeof(ColorOrderingMode)))
        h.AddPostOptionsLine($"  {mode}: {GetEnumDescription((ColorOrderingMode)mode)}");
      h.AddPostOptionsLine(string.Empty);

      return HelpText.DefaultParsingErrorsHandler(result, h);
    }, e => e, maxDisplayWidth: Console.BufferWidth);

    Console.WriteLine(helpText);
    Console.WriteLine("Insufficient arguments try '--help' for help.");

    return;

    static string? GetEnumDescription(Enum value) => value.GetType().GetField(value.ToString())!.GetCustomAttribute<DescriptionAttribute>()?.Description;
  }

}
