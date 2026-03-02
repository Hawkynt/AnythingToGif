using System;
using System.Drawing;
using System.Drawing.Imaging;
using AnythingToGif.Ditherers;
using NUnit.Framework;

namespace AnythingToGif.Tests;

[TestFixture]
public class AdaptiveMatrixDithererTests {

  [Test]
  public void AdaptiveMatrixDitherer_Default_CreatesInstance() {
    // Arrange & Act
    var ditherer = AdaptiveMatrixDitherer.Default;
    
    // Assert
    Assert.That(ditherer, Is.InstanceOf<IDitherer>());
  }

  [Test]
  public void AdaptiveMatrixDitherer_Aggressive_CreatesInstance() {
    // Arrange & Act
    var ditherer = AdaptiveMatrixDitherer.Aggressive;
    
    // Assert
    Assert.That(ditherer, Is.InstanceOf<IDitherer>());
  }

  [Test]
  public void AdaptiveMatrixDitherer_Conservative_CreatesInstance() {
    // Arrange & Act
    var ditherer = AdaptiveMatrixDitherer.Conservative;
    
    // Assert
    Assert.That(ditherer, Is.InstanceOf<IDitherer>());
  }

  [Test]
  public void AdaptiveMatrixDitherer_StaticFactories_CreateDifferentConfigs() {
    // Arrange & Act
    var defaultDitherer = AdaptiveMatrixDitherer.Default;
    var aggressiveDitherer = AdaptiveMatrixDitherer.Aggressive;
    var conservativeDitherer = AdaptiveMatrixDitherer.Conservative;
    
    // Assert - They should be different instances (struct comparison)
    Assert.That(defaultDitherer, Is.Not.EqualTo(aggressiveDitherer));
    Assert.That(defaultDitherer, Is.Not.EqualTo(conservativeDitherer));
    Assert.That(aggressiveDitherer, Is.Not.EqualTo(conservativeDitherer));
  }

}