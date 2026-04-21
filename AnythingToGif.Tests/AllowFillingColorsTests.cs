using System;
using System.Drawing;
using System.Linq;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class AllowFillingColorsTests {

  private static bool IsTransparent(Color c) => c.A == 0;

  [Test]
  public void AllowFillingColors_True_ShouldFillEmptyPaletteSlots() {
    var quantizer = TestQuantizers.Octree(allowFillingColors: true);
    var colors = new[] { Color.Red };

    var result = quantizer.ReduceColorsTo(8, colors);

    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(8));
    Assert.That(result[0].ToArgb(), Is.EqualTo(Color.Red.ToArgb()));

    var nonTransparentColors = result.Where(c => !IsTransparent(c)).ToArray();
    Assert.That(nonTransparentColors.Length, Is.GreaterThan(1),
      "Should fill empty palette slots with additional colors when AllowFillingColors is true");
  }

  [Test]
  public void AllowFillingColors_False_ShouldFillOnlyWithTransparent() {
    var quantizer = TestQuantizers.Octree(allowFillingColors: false);
    var colors = new[] { Color.Red };

    var result = quantizer.ReduceColorsTo(8, colors);

    Assert.That(result, Is.Not.Null);
    Assert.That(result.Length, Is.EqualTo(8));
    Assert.That(result[0].ToArgb(), Is.EqualTo(Color.Red.ToArgb()));

    for (var i = 1; i < result.Length; ++i)
      Assert.That(IsTransparent(result[i]), Is.True,
        $"Palette slot {i} should be transparent when AllowFillingColors is false");
  }

  [Test]
  public void AllowFillingColors_DefaultValue_ShouldBeTrue() {
    var quantizer = TestQuantizers.Octree();
    var result = quantizer.ReduceColorsTo(8, new[] { Color.Red });
    var nonTransparentCount = result.Count(c => !IsTransparent(c));
    Assert.That(nonTransparentCount, Is.GreaterThan(1),
      "Default AllowFillingColors should pad with non-transparent palette entries");
  }

  [Test]
  public void AllowFillingColors_OctreeQuantizer_ShouldRespectSetting() {
    var colors = new[] { Color.Blue, Color.Green };

    var withoutFill = TestQuantizers.Octree(allowFillingColors: false);
    var resultNoFill = withoutFill.ReduceColorsTo(6, colors);
    var nonTransparentCount = resultNoFill.Count(c => !IsTransparent(c));
    Assert.That(nonTransparentCount, Is.LessThanOrEqualTo(2),
      "Quantizer with allowFillingColors=false must not add extra colors");

    var withFill = TestQuantizers.Octree(allowFillingColors: true);
    var resultFilled = withFill.ReduceColorsTo(6, colors);
    var nonTransparentCount2 = resultFilled.Count(c => !IsTransparent(c));
    Assert.That(nonTransparentCount2, Is.GreaterThan(nonTransparentCount),
      "Quantizer with allowFillingColors=true must add extra colors");
  }

  [Test]
  public void AllowFillingColors_WithHistogram_ShouldRespectSetting() {
    var quantizer = TestQuantizers.Octree(allowFillingColors: false);
    var histogram = new[] { (Color.Red, 10u), (Color.Blue, 5u) };

    var result = quantizer.ReduceColorsTo(6, histogram);

    Assert.That(result.Length, Is.EqualTo(6));

    var nonTransparentCount = result.Count(c => !IsTransparent(c));
    Assert.That(nonTransparentCount, Is.LessThanOrEqualTo(2),
      "With allowFillingColors=false, only histogram colors should be non-transparent");
  }
}
