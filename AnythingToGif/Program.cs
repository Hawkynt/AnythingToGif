using System;
using System.Drawing;
using System.IO;
using System.Linq;
using AnythingToGif;
using FFmpeg.AutoGen;

class Program {
  static void Main() {

    ffmpeg.RootPath += "\\ffmpeg";
    
    var examplesDirectory = new DirectoryInfo("Examples");
    foreach (var file in examplesDirectory.EnumerateFiles().Where(i=>i.Extension.IsAnyOf(".jpg",".png",".bmp",".tif")))
      ProcessFile(file);

    return;

    static void ProcessFile(FileInfo inputFile) {
      var outputFile = inputFile.WithNewExtension(".gif");

      var converter = new SingleImageHiColorGifConverter {
        TotalFrameDuration = TimeSpan.FromSeconds(0),
        MinimumSubImageDuration = TimeSpan.FromMilliseconds(10),
        FirstSubImageInitsBackground = true,
        Quantizer = new OctreeQuantizer(),
        Ditherer = MatrixBasedDitherer.FloydSteinberg,
        UseBackFilling = false,
        ColorOrdering = ColorOrderingMode.MostUsedFirst
      };

      using var image = Image.FromFile(inputFile.FullName);
      using var bitmap = new Bitmap(image);
      converter.Convert(bitmap, outputFile);
    }
  }
}