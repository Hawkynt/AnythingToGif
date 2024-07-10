using System;

namespace AnythingToGif.Extensions;

using System.IO;

internal static partial class FileInfoExtensions {

  public static bool LooksLikeVideo(this FileInfo @this) => @this.Extension.IsAnyOf(StringComparison.OrdinalIgnoreCase,
    ".xvid", ".divx", ".mpg", ".mpeg", ".mp2", ".mp4", ".m4v", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".f4v", ".webm", ".3gp", ".3g2", ".vob", ".ogv", ".ts", ".m2ts", ".mts", ".mxf")
  ;

  public static bool LooksLikeImage(this FileInfo @this) => @this.Extension.IsAnyOf(StringComparison.OrdinalIgnoreCase,
    ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif", ".ico")
  ;

}