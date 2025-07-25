using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using AnythingToGif.Ditherers;
using AnythingToGif.Quantizers;
using CommandLine;
using CommandLine.Text;
using ColorExtensions = AnythingToGif.Extensions.ColorExtensions;

namespace AnythingToGif.CLI;

internal class Options {

  public enum ColorDistanceMetric {
    Default,
    [Description("Euclidean")] Euclidean,
    [Description("Euclidean (BT709)")] EuclideanBT709,
    [Description("Euclidean (Nommyde)")] EuclideanNommyde,
    [Description("Manhattan")] Manhattan,
    [Description("Manhattan (BT709)")] ManhattanBT709,
    [Description("ManhattanNommyde")] ManhattanNommyde,
    [Description("CompuPhase")] CompuPhase,
    [Description("Weighted Euclidean (low red component)")] WeightedEuclideanLowRed,
    [Description("Weighted Euclidean (high red component)")] WeightedEuclideanHighRed,
  }

  public enum QuantizerMode {
    [Description("EGA 16-colors + transparency")] Ega16,
    [Description("VGA 256-colors + transparency")] Vga256,
    [Description("Web Safe palette + transparency")] WebSafe,
    [Description("Mac 8-bit system palette + transparency")] Mac8Bit,
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
    [Description("Equal Floyd-Steinberg")] EqualFloydSteinberg,
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
    [Description("Bayer 2x2")] Bayer2x2,
    [Description("Bayer 4x4")] Bayer4x4,
    [Description("Bayer 8x8")] Bayer8x8,
    [Description("A-Dither XOR-Y149")] ADitherXorY149,
    [Description("A-Dither XOR-Y149 with Channel")] ADitherXorY149WithChannel,
    [Description("A-Dither XY Arithmetic")] ADitherXYArithmetic,
    [Description("A-Dither XY Arithmetic with Channel")] ADitherXYArithmeticWithChannel,
    [Description("A-Dither Uniform")] ADitherUniform
  }

  [Value(0, MetaName = "input", HelpText = "Input directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _InputPath { get; set; } = Directory.GetCurrentDirectory();

  [Value(1, MetaName = "output", HelpText = "Output directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _OutputPath { get; set; } = Directory.GetCurrentDirectory();

  [Option('m', "metric", Default = ColorDistanceMetric.Default, HelpText = "Color distance metric to use.")]
  public ColorDistanceMetric _Metric { get; set; }

  [Option('q', "quantizer", Default = QuantizerMode.Octree, HelpText = "Quantizer to use.")]
  public QuantizerMode _Quantizer { get; set; }

  [Option('d', "ditherer", Default = DithererMode.FloydSteinberg, HelpText = "Ditherer to use.")]
  public DithererMode _Ditherer { get; set; }

  [Option('f', "useBackFilling", Default = false, HelpText = "Whether to use backfilling.")]
  public bool UseBackFilling { get; set; }

  [Option('b', "firstSubImageInitsBackground", Default = true, HelpText = "Whether the first sub-image initializes the background.")]
  public bool FirstSubImageInitsBackground { get; set; }

  [Option('p', "usePca", Default = false, HelpText = "Use PCA (Principal Component Analysis) preprocessing before quantization.")]
  public bool UsePca { get; set; }

  [Option('c', "colorOrdering", Default = ColorOrderingMode.MostUsedFirst, HelpText = "Color ordering mode.")]
  public ColorOrderingMode ColorOrdering { get; set; }

  [Option('n', "noCompression", Default = false, HelpText = "Whether to use compressed GIF files or not.")]
  public bool NoCompression { get; set; }

  [Option('a', "useAntRefinement", Default = false, HelpText = "Whether to apply Ant-tree like iterative refinement after initial quantization.")]
  public bool UseAntRefinement { get; set; }

  [Option('i', "antIterations", Default = 25, HelpText = "Number of iterations for Ant-tree like refinement.")]
  public int AntIterations { get; set; }

  public FileSystemInfo InputPath => File.Exists(this._InputPath) ? new FileInfo(this._InputPath) : new DirectoryInfo(this._InputPath);

  public FileSystemInfo OutputPath => Directory.Exists(this._OutputPath) ? new DirectoryInfo(this._OutputPath) : new FileInfo(this._OutputPath);

  public Func<Color, Color, int>? Metric => this._Metric switch {
    ColorDistanceMetric.Default => null,
    ColorDistanceMetric.Euclidean => ColorExtensions.EuclideanDistance,
    ColorDistanceMetric.EuclideanBT709 => ColorExtensions.EuclideanBT709Distance,
    ColorDistanceMetric.EuclideanNommyde => ColorExtensions.EuclideanNommydeDistance,
    ColorDistanceMetric.WeightedEuclideanHighRed => ColorExtensions.WeightedEuclideanDistanceHighRed,
    ColorDistanceMetric.WeightedEuclideanLowRed => ColorExtensions.WeightedEuclideanDistanceLowRed,
    ColorDistanceMetric.Manhattan => ColorExtensions.ManhattanDistance,
    ColorDistanceMetric.ManhattanBT709 => ColorExtensions.ManhattanBT709Distance,
    ColorDistanceMetric.ManhattanNommyde => ColorExtensions.ManhattanNommydeDistance,
    ColorDistanceMetric.CompuPhase => ColorExtensions.CompuPhaseDistance,
    _ => throw new("Unknown color distance metric")
  };

  public Func<IQuantizer> Quantizer => () => {
    IQuantizer q = this._Quantizer switch {
      QuantizerMode.Octree => new OctreeQuantizer(),
      QuantizerMode.MedianCut => new MedianCutQuantizer(),
      QuantizerMode.GreedyOrthogonalBiPartitioning => new WuQuantizer(),
      QuantizerMode.VarianceCut => new VarianceCutQuantizer(),
      QuantizerMode.VarianceBased => new VarianceBasedQuantizer(),
      QuantizerMode.BinarySplitting => new BinarySplittingQuantizer(),
      QuantizerMode.Ega16 => new Ega16Quantizer(),
      QuantizerMode.Vga256 => new Vga256Quantizer(),
      QuantizerMode.WebSafe => new WebSafeQuantizer(),
      QuantizerMode.Mac8Bit => new Mac8BitQuantizer(),
      QuantizerMode.Adu => new AduQuantizer(this.Metric ?? ColorExtensions.CompuPhaseDistance),
      _ => throw new("Unknown quantizer")
    };

    if (this.UsePca)
      q = new PcaQuantizerWrapper(q);

    if (this.UseAntRefinement)
      q = new AntRefinementWrapper(q, this.AntIterations, this.Metric ?? ColorExtensions.CompuPhaseDistance);

    return q;
  };

  public IDitherer Ditherer => this._Ditherer switch {
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
    DithererMode.Bayer2x2 => BayerDitherer.Bayer2x2,
    DithererMode.Bayer4x4 => BayerDitherer.Bayer4x4,
    DithererMode.Bayer8x8 => BayerDitherer.Bayer8x8,
    DithererMode.ADitherXorY149 => ADitherer.XorY149,
    DithererMode.ADitherXorY149WithChannel => ADitherer.XorY149WithChannel,
    DithererMode.ADitherXYArithmetic => ADitherer.XYArithmetic,
    DithererMode.ADitherXYArithmeticWithChannel => ADitherer.XYArithmeticWithChannel,
    DithererMode.ADitherUniform => ADitherer.Uniform,
    DithererMode.None => NoDitherer.Instance,
    _ => NoDitherer.Instance
  };

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
