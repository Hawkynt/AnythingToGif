using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Hawkynt.ColorProcessing;
using Hawkynt.ColorProcessing.Dithering;
using Hawkynt.Drawing.ColorDomain;
using DithererRegistry = Hawkynt.ColorProcessing.Dithering.DithererRegistry;
using QuantizerRegistry = Hawkynt.ColorProcessing.Quantization.QuantizerRegistry;
using UpstreamOrderedDitherer = Hawkynt.ColorProcessing.Dithering.OrderedDitherer;

namespace AnythingToGif.CLI;

internal class Options {

  public enum ColorDistanceMetric {
    [Description("Let application decide")] Default,
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

  /// <summary>Maps the CLI metric enum onto the upstream <see cref="ColorMetric"/>.</summary>
  private static readonly Dictionary<ColorDistanceMetric, ColorMetric> _metricMap = new() {
    [ColorDistanceMetric.Euclidean] = ColorMetric.Euclidean,
    [ColorDistanceMetric.EuclideanRGBOnly] = ColorMetric.EuclideanRgbOnly,
    [ColorDistanceMetric.EuclideanBT709] = ColorMetric.EuclideanBT709,
    [ColorDistanceMetric.EuclideanNommyde] = ColorMetric.EuclideanNommyde,
    [ColorDistanceMetric.WeightedEuclideanLowRed] = ColorMetric.WeightedEuclideanLowRed,
    [ColorDistanceMetric.WeightedEuclideanHighRed] = ColorMetric.WeightedEuclideanHighRed,
    [ColorDistanceMetric.Manhattan] = ColorMetric.Manhattan,
    [ColorDistanceMetric.ManhattanRGBOnly] = ColorMetric.ManhattanRgbOnly,
    [ColorDistanceMetric.ManhattanBT709] = ColorMetric.ManhattanBT709,
    [ColorDistanceMetric.ManhattanNommyde] = ColorMetric.ManhattanNommyde,
    [ColorDistanceMetric.WeightedManhattanLowRed] = ColorMetric.WeightedManhattanLowRed,
    [ColorDistanceMetric.WeightedManhattanHighRed] = ColorMetric.WeightedManhattanHighRed,
    [ColorDistanceMetric.CompuPhase] = ColorMetric.CompuPhase,
    [ColorDistanceMetric.PNGQuant] = ColorMetric.PngQuant,
    [ColorDistanceMetric.WeightedYuv] = ColorMetric.WeightedYuv,
    [ColorDistanceMetric.WeightedYCbCr] = ColorMetric.WeightedYCbCr,
    [ColorDistanceMetric.CieDe2000] = ColorMetric.CieDe2000,
    [ColorDistanceMetric.Cie94Textiles] = ColorMetric.Cie94Textiles,
    [ColorDistanceMetric.Cie94GraphicArts] = ColorMetric.Cie94GraphicArts,
  };

  /// <summary>
  /// Aliases for quantizer short names that don't match upstream registry display names.
  /// </summary>
  private static readonly Dictionary<string, string> _quantizerAliases = new(StringComparer.OrdinalIgnoreCase) {
    ["GreedyOrthogonalBiPartitioning"] = "Wu",
    ["Adu"] = "ADU",
    ["MedianCut"] = "Median Cut",
    ["VarianceBased"] = "Variance Based",
    ["VarianceCut"] = "Variance Cut",
    ["BinarySplitting"] = "Binary Splitting",
    ["Ega16"] = "EGA 16",
    ["Vga256"] = "VGA 256",
    ["WebSafe"] = "Web Safe",
    ["Mac8Bit"] = "Mac 8-Bit",
  };

  /// <summary>
  /// Disambiguates short ditherer names whose suffix-match would hit multiple registry entries.
  /// </summary>
  private static readonly Dictionary<string, string> _ditherAliases = new(StringComparer.OrdinalIgnoreCase) {
    ["None"] = "NoDithering_Instance",
    ["Bayer2x2"] = "Ordered_Bayer2x2",
    ["Bayer4x4"] = "Ordered_Bayer4x4",
    ["Bayer8x8"] = "Ordered_Bayer8x8",
    ["Bayer16x16"] = "Ordered_Bayer16x16",
    ["Default"] = "ErrorDiffusion_FloydSteinberg",
    ["Instance"] = "Ostromoukhov_Instance",
  };

  [Value(0, MetaName = "input", HelpText = "Input directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _InputPath { get; set; } = Directory.GetCurrentDirectory();

  [Value(1, MetaName = "output", HelpText = "Output directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _OutputPath { get; set; } = Directory.GetCurrentDirectory();

  [Option('a', "useAntRefinement", Default = false, HelpText = "Whether to apply k-means-style iterative refinement after initial quantization.")]
  public bool UseAntRefinement { get; set; }

  [Option('b', "firstSubImageInitsBackground", Default = true, HelpText = "Whether the first sub-image initializes the background.")]
  public bool FirstSubImageInitsBackground { get; set; }

  [Option('c', "colorOrdering", Default = ColorOrderingMode.MostUsedFirst, HelpText = "Color ordering mode.")]
  public ColorOrderingMode ColorOrdering { get; set; }

  [Option('d', "ditherer", Default = "FloydSteinberg", HelpText = "Ditherer name. Use --help to list all available names (resolved against the upstream DithererRegistry).")]
  public string _Ditherer { get; set; } = "FloydSteinberg";

  [Option("bayer", Default = 0, HelpText = "Generate 2^n Bayer matrix (e.g., --bayer 4 creates 16x16 matrix). When specified, overrides --ditherer. Valid range: 1-8.")]
  public int BayerIndex { get; set; }

  [Option('f', "useBackFilling", Default = false, HelpText = "Whether to use backfilling.")]
  public bool UseBackFilling { get; set; }

  [Option('i', "antIterations", Default = 25, HelpText = "Number of iterations for k-means refinement.")]
  public int AntIterations { get; set; }

  [Option('m', "metric", Default = ColorDistanceMetric.Default, HelpText = "Color distance metric to use.")]
  public ColorDistanceMetric _Metric { get; set; }

  [Option('n', "noCompression", Default = false, HelpText = "Whether to use compressed GIF files or not.")]
  public bool NoCompression { get; set; }

  [Option('p', "usePca", Default = false, HelpText = "Use PCA (Principal Component Analysis) preprocessing before quantization.")]
  public bool UsePca { get; set; }

  [Option('q', "quantizer", Default = "Octree", HelpText = "Quantizer name (resolved against the upstream QuantizerRegistry). Use --help to list available names.")]
  public string _Quantizer { get; set; } = "Octree";

  [Option("maxFrames", Default = 0, HelpText = "Maximum number of frames to generate for non-video data. 0 means no limit.")]
  public int MaxFrames { get; set; }

  [Option("frameDuration", Default = 10, HelpText = "Frame duration in milliseconds for non-video data (default: 10ms).")]
  public int FrameDurationMs { get; set; }

  [Option("totalTime", Default = 0.0, HelpText = "Total animation time in seconds for non-video data. 0 means use frame count.")]
  public double TotalTimeSeconds { get; set; }

  [Option("serpentine", Default = false, HelpText = "Apply serpentine (boustrophedon) scanning to error-diffusion ditherers to reduce directional artifacts.")]
  public bool UseSerpentine { get; set; }

  [Option("disallowFillingColors", Default = false, HelpText = "Prevent quantizer to fill empty palette slots with additional colors (Black, White, RGB primaries, etc.). When enabled, only fills with transparent colors.")]
  public bool DisallowFillingColors { get; set; }

  public FileSystemInfo InputPath => File.Exists(this._InputPath) ? new FileInfo(this._InputPath) : new DirectoryInfo(this._InputPath);

  public FileSystemInfo OutputPath => Directory.Exists(this._OutputPath) ? new DirectoryInfo(this._OutputPath) : new FileInfo(this._OutputPath);

  /// <summary>
  /// CLI-selected metric translated to a runtime delegate via upstream <see cref="ColorMetric"/>.
  /// <see langword="null"/> when the user picked <see cref="ColorDistanceMetric.Default"/>.
  /// </summary>
  public Func<Color, Color, int>? Metric => _metricMap.TryGetValue(this._Metric, out var m) ? m.AsFunc() : null;

  public TimeSpan FrameDuration => TimeSpan.FromMilliseconds(this.FrameDurationMs);

  public TimeSpan? TotalTime => this.TotalTimeSeconds > 0 ? TimeSpan.FromSeconds(this.TotalTimeSeconds) : null;

  public Func<IColorQuantizer> Quantizer => () => {
    var q = this.ResolveQuantizer(this._Quantizer ?? "Octree");

    if (this.UsePca)
      q = new PcaColorQuantizerWrapper(q);

    if (this.UseAntRefinement)
      q = new KMeansColorRefinementWrapper(q, this.AntIterations, this.Metric ?? ColorMetric.CompuPhase.AsFunc());

    return q;
  };

  public IColorDitherer Ditherer {
    get {
      // --bayer N override: explicit Bayer matrix size 2^N
      if (this.BayerIndex is >= 1 and <= 8) {
        var bayer = new UpstreamOrderedDitherer(BayerMatrix.Generate(1 << this.BayerIndex));
        return MaybeSerpentine(new ColorDithererAdapter(bayer));
      }

      var name = this._Ditherer ?? "FloydSteinberg";
      return MaybeSerpentine(this.ResolveDitherer(name));
    }
  }

  /// <summary>
  /// Resolves a ditherer name through the upstream <see cref="ColorDithererRegistry"/>,
  /// applying our small alias map for legacy short names.
  /// </summary>
  internal IColorDitherer ResolveDitherer(string name) {
    if (_ditherAliases.TryGetValue(name, out var canonical))
      name = canonical;

    var resolved = ColorDithererRegistry.FindByName(name);
    if (resolved == null)
      throw new ArgumentException($"Unknown ditherer '{name}'. Use --help to list available names.");
    return resolved;
  }

  private IColorDitherer MaybeSerpentine(IColorDitherer d) {
    if (!this.UseSerpentine)
      return d;
    if (d is ColorDithererAdapter a && a.Inner is ErrorDiffusion ed)
      return new ColorDithererAdapter(ed.Serpentine);
    return d; // serpentine only applies to error-diffusion ditherers; others pass through.
  }

  /// <summary>
  /// Resolves a quantizer by name via the upstream <see cref="ColorQuantizerRegistry"/>,
  /// with a tiny alias map for legacy CLI spellings. The <c>--disallowFillingColors</c>
  /// flag is threaded into the adapter's palette-fill policy.
  /// </summary>
  internal IColorQuantizer ResolveQuantizer(string name) {
    var allowFill = !this.DisallowFillingColors;

    if (_quantizerAliases.TryGetValue(name, out var canonical))
      name = canonical;

    var resolved = ColorQuantizerRegistry.FindByName(name, allowFill);
    if (resolved == null)
      throw new ArgumentException($"Unknown quantizer '{name}'. Use --help to list available names.");
    return resolved;
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

      h.AddPostOptionsLine("Quantizer names (upstream registry, sorted):");
      foreach (var q in QuantizerRegistry.All)
        h.AddPostOptionsLine($"  {q.Name}{(q.Author == null ? string.Empty : $" — {q.Author}")}");
      h.AddPostOptionsLine(string.Empty);
      h.AddPostOptionsLine("Quantizer aliases (legacy CLI names):");
      foreach (var pair in _quantizerAliases.OrderBy(k => k.Key))
        h.AddPostOptionsLine($"  {pair.Key} -> {pair.Value}");
      h.AddPostOptionsLine(string.Empty);

      h.AddPostOptionsLine("Ditherer names (upstream registry, sorted):");
      foreach (var d in DithererRegistry.All)
        h.AddPostOptionsLine($"  {d.Name}{(d.Description == null ? string.Empty : $" — {d.Description}")}");
      h.AddPostOptionsLine(string.Empty);
      h.AddPostOptionsLine("Ditherer aliases (short names + special):");
      foreach (var pair in _ditherAliases.OrderBy(k => k.Key))
        h.AddPostOptionsLine($"  {pair.Key} -> {pair.Value}");
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
