using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using CommandLine;
using CommandLine.Text;

namespace AnythingToGif.CLI;

internal class Options {
  public enum QuantizerMode {
    [Description("Median-Cut")] MedianCut,
    [Description("Octree")] Octree,
    [Description("Greedy Orthogonal Bi-Partitioning (Wu)")]
    GreedyOrthogonalBiPartitioning
  }

  public enum DithererMode {
    [Description("None")] None,
    [Description("Floyd-Steinberg")] FloydSteinberg,
    [Description("Jarvis-Judice-Ninke")] JarvisJudiceNinke,
    [Description("Stucki")] Stucki,
    [Description("Atkinson")] Atkinson,
    [Description("Burkes")] Burkes,
    [Description("Sierra")] Sierra,
    [Description("2-row Sierra")] TwoRowSierra,
    [Description("Sierra Lite")] SierraLite,
    [Description("Pigeon")] Pigeon
  }

  [Value(0, MetaName = "input", HelpText = "Input directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _InputPath { get; set; } = Directory.GetCurrentDirectory();

  [Value(1, MetaName = "output", HelpText = "Output directory or file. If not specified, defaults to the current directory.", Required = false)]
  public string _OutputPath { get; set; } = Directory.GetCurrentDirectory();

  [Option('q', "quantizer", Default = QuantizerMode.Octree, HelpText = "Quantizer to use.")]
  public QuantizerMode _Quantizer { get; set; }

  [Option('d', "ditherer", Default = DithererMode.FloydSteinberg, HelpText = "Ditherer to use.")]
  public DithererMode _Ditherer { get; set; }

  [Option('f', "useBackFilling", Default = false, HelpText = "Whether to use backfilling.")]
  public bool UseBackFilling { get; set; }

  [Option('b', "firstSubImageInitsBackground", Default = true, HelpText = "Whether the first sub-image initializes the background.")]
  public bool FirstSubImageInitsBackground { get; set; }

  [Option('c', "colorOrdering", Default = ColorOrderingMode.MostUsedFirst, HelpText = "Color ordering mode.")]
  public ColorOrderingMode ColorOrdering { get; set; }

  [Option('n', "noCompression", Default = false, HelpText = "Whether to use compressed GIF files or not.")]
  public bool NoCompression { get; set; }

  public FileSystemInfo InputPath => File.Exists(this._InputPath) ? new FileInfo(this._InputPath) : new DirectoryInfo(this._InputPath);

  public FileSystemInfo OutputPath => Directory.Exists(this._OutputPath) ? new DirectoryInfo(this._OutputPath) : new FileInfo(this._OutputPath);

  public Func<IQuantizer> Quantizer => this._Quantizer switch {
    QuantizerMode.Octree => () => new OctreeQuantizer(),
    QuantizerMode.MedianCut => () => new MedianCutQuantizer(),
    QuantizerMode.GreedyOrthogonalBiPartitioning => () => new WuQuantizer(),
    _ => throw new("Unknown quantizer")
  };

  public IDitherer Ditherer => this._Ditherer switch {
    DithererMode.FloydSteinberg => MatrixBasedDitherer.FloydSteinberg,
    DithererMode.JarvisJudiceNinke => MatrixBasedDitherer.JarvisJudiceNinke,
    DithererMode.Stucki => MatrixBasedDitherer.Stucki,
    DithererMode.Atkinson => MatrixBasedDitherer.Atkinson,
    DithererMode.Burkes => MatrixBasedDitherer.Burkes,
    DithererMode.Sierra => MatrixBasedDitherer.Sierra,
    DithererMode.TwoRowSierra => MatrixBasedDitherer.TwoRowSierra,
    DithererMode.SierraLite => MatrixBasedDitherer.SierraLite,
    DithererMode.Pigeon => MatrixBasedDitherer.Pigeon,
    DithererMode.None => NoDitherer.Instance,
    _ => NoDitherer.Instance
  };

  public static void HandleParseError<T>(ParserResult<T> result, IEnumerable<Error> errors) {
    var helpText = HelpText.AutoBuild(result, h => {
      var thisAssembly = Assembly.GetExecutingAssembly();
      h.AdditionalNewLineAfterOption = false;
      h.Heading = $"{thisAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title} {thisAssembly.GetName().Version}";
      h.Copyright = thisAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? CopyrightInfo.Default;
      h.AddPreOptionsLine(thisAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);

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
