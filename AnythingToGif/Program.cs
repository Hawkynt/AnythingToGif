using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using AnythingToGif;
using FFmpeg.AutoGen;
using Gif;

class Program {
  static void Main() {

    ffmpeg.RootPath += "\\ffmpeg";
    
    var examplesDirectory = new DirectoryInfo("Examples");
    foreach (var file in examplesDirectory.EnumerateFiles().Where(i => i.Extension.IsAnyOf(".avi", ".xvid", ".divx", ".mpg", ".mp2", ".mp4", ".mkv")))
      ProcessVideoFile(file);
    foreach (var file in examplesDirectory.EnumerateFiles().Where(i => i.Extension.IsAnyOf(".jpg", ".png", ".bmp", ".tif")))
      ProcessImageFile(file);
    
    return;

    static void ProcessVideoFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      Console.WriteLine($"Converting {inputFile.Name}");

      var converter = new SingleImageHiColorGifConverter {
        FirstSubImageInitsBackground = true,
        Ditherer = MatrixBasedDitherer.FloydSteinberg,
        UseBackFilling = false,
        ColorOrdering = ColorOrderingMode.MostUsedFirst
      };

      Dimensions? dimensions=null;

      var subImages = ProcessFrames();
      var enumerator = subImages.GetEnumerator();
      if (!enumerator.MoveNext())
        return;

      if(dimensions!=null)
        _WriteGif(outputFile, dimensions.Value, Enumerate());

      enumerator?.Dispose();

      return;

      IEnumerable<Frame> Enumerate() {
        do {
          yield return enumerator.Current;
        }while(enumerator.MoveNext() ); 
      }

      IEnumerable<Frame> ProcessFrames() {
        var i = 0;
        var durationDelta = TimeSpan.Zero; /* this is for keeping track how much we're behind actual timecodes in the video because of gif frame limits */
        foreach (var (image, duration) in FrameServer()) {
          dimensions ??= new(image.Width,image.Height);

          converter.Quantizer = new OctreeQuantizer();
          converter.TotalFrameDuration = duration + durationDelta; /* if we're too early, keep displaying frames longer */
          Console.WriteLine($"Processing frame {++i}");

          var currentDuration = TimeSpan.Zero;
          foreach (var frame in converter.Convert(image)) {
            currentDuration += frame.Duration;
            yield return frame;
          }

          durationDelta = duration + durationDelta - currentDuration;
        }
      }

      IEnumerable<(Bitmap frame,TimeSpan duration)> FrameServer() {
        foreach (var image in VideoFrameExtractor.GetFrames(inputFile))
          yield return image;
      }


    }

    static void ProcessImageFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        FirstSubImageInitsBackground = true,
        Quantizer = new OctreeQuantizer(),
        Ditherer = MatrixBasedDitherer.FloydSteinberg,
        UseBackFilling = false,
        ColorOrdering = ColorOrderingMode.MostUsedFirst
      };

      using var image = Image.FromFile(inputFile.FullName);
      using var bitmap = new Bitmap(image);
      
      Console.WriteLine($"Converting {inputFile.Name}");
      var subImages=converter.Convert(bitmap);

      _WriteGif(outputFile, (Dimensions)image.Size, subImages);
    }


    static void _WriteGif(FileInfo file, Dimensions dimensions, IEnumerable<Frame> frames) 
      => Writer.ToFile(file, dimensions, frames, LoopCount.NotSet, allowCompression: true)
      ;

  }
}