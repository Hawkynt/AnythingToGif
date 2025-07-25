using System.Drawing;

namespace AnythingToGif.Quantizers.FixedPalettes;

public sealed class Ega16Quantizer() : FixedPaletteQuantizer(Palette) {
  internal static readonly Color[] Palette = [
    Color.FromArgb(0, 0, 0),
    Color.FromArgb(0, 0, 170),
    Color.FromArgb(0, 170, 0),
    Color.FromArgb(0, 170, 170),
    Color.FromArgb(170, 0, 0),
    Color.FromArgb(170, 0, 170),
    Color.FromArgb(170, 85, 0),
    Color.FromArgb(170, 170, 170),
    Color.FromArgb(85, 85, 85),
    Color.FromArgb(85, 85, 255),
    Color.FromArgb(85, 255, 85),
    Color.FromArgb(85, 255, 255),
    Color.FromArgb(255, 85, 85),
    Color.FromArgb(255, 85, 255),
    Color.FromArgb(255, 255, 85),
    Color.FromArgb(255, 255, 255)
  ];
}
