using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using AnythingToGif;
using AnythingToGif.CLI;
using AnythingToGif.Extensions;
using CommandLine;
using CommandLine.Text;
using FFmpeg.AutoGen;
using Hawkynt.GifFileFormat;

class Program {

  static void Main(string[] args) {

    const string OUTPUT_EXTENSION = ".gif";

    ffmpeg.RootPath += "\\ffmpeg";

    var parser = new Parser(with => with.HelpWriter = null);
    var result = parser.ParseArguments<Options>(args);

    result
      .WithParsed(RunOptions)
      .WithNotParsed(errs => Options.HandleParseError(result, errs))
      ;

    return;
    
    static void RunOptions(Options configuration) {
      var inputFileOrDirectory = configuration.InputPath;
      var outputFileOrDirectory = configuration.OutputPath;

      switch (inputFileOrDirectory) {
        case FileInfo singleInputFile: {
          if (singleInputFile.LooksLikeImage())
            ProcessImageFile(singleInputFile, outputFileOrDirectory is DirectoryInfo d ? d.File(singleInputFile.WithNewExtension(OUTPUT_EXTENSION).Name) : outputFileOrDirectory as FileInfo ?? throw new($"Unknown output path {outputFileOrDirectory.Name}"));
          else if (singleInputFile.LooksLikeVideo())
            ProcessVideoFile(singleInputFile, outputFileOrDirectory is DirectoryInfo d ? d.File(singleInputFile.WithNewExtension(OUTPUT_EXTENSION).Name) : outputFileOrDirectory as FileInfo ?? throw new($"Unknown output path {outputFileOrDirectory.Name}"));
          else
            throw new($"Unknown file-type {singleInputFile.Name}");

          return;
        }
        case DirectoryInfo directory: {
          switch (outputFileOrDirectory) {
            case DirectoryInfo outputDirectory: {
              
              foreach (var file in directory.EnumerateFiles().Where(i => i.LooksLikeVideo()))
                ProcessVideoFile(file, outputDirectory.File(file.WithNewExtension(OUTPUT_EXTENSION).Name));
              
              foreach (var file in directory.EnumerateFiles().Where(i => i.LooksLikeImage()))
                ProcessImageFile(file, outputDirectory.File(file.WithNewExtension(OUTPUT_EXTENSION).Name));
              
              break;
            }
            case FileInfo outputFile:
              throw new NotImplementedException("Combining multiple files into one not yet supported");
            default:
              throw new($"Unknown output path {outputFileOrDirectory.Name}");
          }

          return;
        }
        default:
          throw new($"Unknown input path {inputFileOrDirectory.Name}");
      }

      return;

      void ProcessVideoFile(FileInfo inputFile, FileInfo outputFile) {

        Console.WriteLine($"Converting video {inputFile.Name}");

        var converter = new SingleImageHiColorGifConverter {
          FirstSubImageInitsBackground = configuration.FirstSubImageInitsBackground,
          Ditherer = configuration.Ditherer,
          UseBackFilling = configuration.UseBackFilling,
          ColorOrdering = configuration.ColorOrdering
        };

        Dimensions? dimensions = null;

        var subImages = ProcessFrames();
        var enumerator = subImages.GetEnumerator();
        if (!enumerator.MoveNext())
          return;

        if (dimensions != null)
          _WriteGif(outputFile, dimensions.Value, Enumerate());

        enumerator?.Dispose();

        return;

        IEnumerable<Frame> Enumerate() {
          do {
            yield return enumerator.Current;
          } while (enumerator.MoveNext());
        }

        IEnumerable<Frame> ProcessFrames() {
          var i = 0;
          var durationDelta = TimeSpan.Zero; /* this is for keeping track how much we're behind actual timecodes in the video because of gif frame limits */
          foreach (var (image, duration) in FrameServer()) {
            dimensions ??= new(image.Width, image.Height);

            converter.Quantizer = configuration.Quantizer();
            converter.TotalFrameDuration = duration + durationDelta; /* if we're too early, keep displaying frames for longer */
            Console.WriteLine($"Processing frame {++i}, on-screen for {duration.Milliseconds}ms, already off by {durationDelta.Milliseconds}ms");

            var currentDuration = TimeSpan.Zero;
            foreach (var frame in converter.Convert(image)) {
              currentDuration += frame.Duration;
              yield return frame;
            }

            durationDelta = duration + durationDelta - currentDuration;
          }
        }

        IEnumerable<(Bitmap frame, TimeSpan duration)> FrameServer() {
          foreach (var image in VideoFrameExtractor.GetFrames(inputFile))
            yield return image;
        }
        
      }

      void ProcessImageFile(FileInfo inputFile, FileInfo outputFile) {
        var converter = new SingleImageHiColorGifConverter {
          FirstSubImageInitsBackground = configuration.FirstSubImageInitsBackground,
          Quantizer = configuration.Quantizer(),
          Ditherer = configuration.Ditherer,
          UseBackFilling = configuration.UseBackFilling,
          ColorOrdering = configuration.ColorOrdering
        };

        using var image = Image.FromFile(inputFile.FullName);
        using var bitmap = new Bitmap(image);

        Console.WriteLine($"Converting image {inputFile.Name}");
        var subImages = converter.Convert(bitmap);

        _WriteGif(outputFile, (Dimensions)image.Size, subImages);
      }

      void _WriteGif(FileInfo file, Dimensions dimensions, IEnumerable<Frame> frames)
        => Writer.ToFile(file, dimensions, frames, LoopCount.NotSet, allowCompression: !configuration.NoCompression)
      ;

    }

  }

}