using System;

namespace AnythingToGif.Extensions;

using System.IO;

internal static partial class FileInfoExtensions {

  public static bool LooksLikeVideo(this FileInfo @this) => @this.Extension.IsAnyOf(StringComparison.OrdinalIgnoreCase, ".xvid", ".divx", ".mpg", ".mp2", ".mp4", ".mkv");
  public static bool LooksLikeImage(this FileInfo @this) => @this.Extension.IsAnyOf(StringComparison.OrdinalIgnoreCase, ".jpg", ".png", ".bmp", ".tif");

}