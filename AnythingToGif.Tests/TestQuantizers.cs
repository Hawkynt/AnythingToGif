using Hawkynt.Drawing.ColorDomain;

namespace AnythingToGif.Tests;

/// <summary>
/// Test helper for producing <see cref="IColorQuantizer"/> instances via the upstream
/// extension registry. Mirrors the upstream <c>QuantizerAttribute.DisplayName</c>
/// values; if a name lookup fails, double-check spacing/casing in
/// <c>System.Drawing.Extensions/ColorProcessing/Quantization/&lt;Name&gt;Quantizer.cs</c>.
/// </summary>
internal static class TestQuantizers {

  public static IColorQuantizer Octree(bool allowFillingColors = true) => Resolve("Octree", allowFillingColors);
  public static IColorQuantizer MedianCut(bool allowFillingColors = true) => Resolve("Median Cut", allowFillingColors);
  public static IColorQuantizer Wu(bool allowFillingColors = true) => Resolve("Wu", allowFillingColors);
  public static IColorQuantizer VarianceBased(bool allowFillingColors = true) => Resolve("Variance Based", allowFillingColors);
  public static IColorQuantizer VarianceCut(bool allowFillingColors = true) => Resolve("Variance Cut", allowFillingColors);
  public static IColorQuantizer BinarySplitting(bool allowFillingColors = true) => Resolve("Binary Splitting", allowFillingColors);
  public static IColorQuantizer Adu(bool allowFillingColors = true) => Resolve("ADU", allowFillingColors);
  public static IColorQuantizer Ega16(bool allowFillingColors = true) => Resolve("EGA 16", allowFillingColors);
  public static IColorQuantizer Vga256(bool allowFillingColors = true) => Resolve("VGA 256", allowFillingColors);
  public static IColorQuantizer WebSafe(bool allowFillingColors = true) => Resolve("Web Safe", allowFillingColors);
  public static IColorQuantizer Mac8Bit(bool allowFillingColors = true) => Resolve("Mac 8-Bit", allowFillingColors);

  private static IColorQuantizer Resolve(string name, bool allowFillingColors)
    => ColorQuantizerRegistry.FindByName(name, allowFillingColors)
       ?? throw new System.InvalidOperationException($"Quantizer '{name}' not found in upstream registry");
}
