using AzurePhotoFlow.Api.Models;
using NUnit.Framework;

namespace AzurePhotoFlow.Api.Tests.UnitTests;

[TestFixture]
public class EmbeddingConfigurationTests
{
    [Test]
    public void EmbeddingConfiguration_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new EmbeddingConfiguration();
        
        // Assert
        Assert.That(config.EmbeddingDimension, Is.EqualTo(512));
        Assert.That(config.ModelVariant, Is.EqualTo("base"));
        Assert.That(config.DistanceMetric, Is.EqualTo("Cosine"));
        Assert.That(config.EnableTextPreprocessing, Is.True);
        Assert.That(config.MaxTokenLength, Is.EqualTo(77));
        Assert.That(config.ImageInputSize, Is.EqualTo(224));
        
        // Should not throw
        Assert.DoesNotThrow(() => config.Validate());
    }

    [TestCase("base", 512)]
    [TestCase("large", 768)]
    [TestCase("huge", 1024)]
    public void GetDimensionForVariant_ValidVariants_ShouldReturnCorrectDimensions(string variant, int expectedDimension)
    {
        // Act
        var dimension = EmbeddingConfiguration.GetDimensionForVariant(variant);
        
        // Assert
        Assert.That(dimension, Is.EqualTo(expectedDimension));
    }

    [TestCase("base", "openai/clip-vit-base-patch32")]
    [TestCase("large", "openai/clip-vit-large-patch14")]
    [TestCase("huge", "laion/CLIP-ViT-H-14-laion2B-s32B-b79K")]
    public void GetModelNameForVariant_ValidVariants_ShouldReturnCorrectModelNames(string variant, string expectedModel)
    {
        // Act
        var modelName = EmbeddingConfiguration.GetModelNameForVariant(variant);
        
        // Assert
        Assert.That(modelName, Is.EqualTo(expectedModel));
    }

    [Test]
    public void GetDimensionForVariant_InvalidVariant_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => EmbeddingConfiguration.GetDimensionForVariant("invalid"));
    }

    [Test]
    public void GetModelNameForVariant_InvalidVariant_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => EmbeddingConfiguration.GetModelNameForVariant("invalid"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void Validate_InvalidEmbeddingDimension_ShouldThrowArgumentException(int invalidDimension)
    {
        // Arrange
        var config = new EmbeddingConfiguration
        {
            EmbeddingDimension = invalidDimension
        };
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [TestCase("InvalidMetric")]
    [TestCase("")]
    [TestCase("cosine")] // Wrong case
    public void Validate_InvalidDistanceMetric_ShouldThrowArgumentException(string invalidMetric)
    {
        // Arrange
        var config = new EmbeddingConfiguration
        {
            DistanceMetric = invalidMetric
        };
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [TestCase("Cosine")]
    [TestCase("Dot")]
    [TestCase("Euclidean")]
    public void Validate_ValidDistanceMetrics_ShouldNotThrow(string validMetric)
    {
        // Arrange
        var config = new EmbeddingConfiguration
        {
            DistanceMetric = validMetric
        };
        
        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => config.Validate());
    }

    [TestCase("invalidvariant")]
    [TestCase("")]
    [TestCase("Base")] // Wrong case
    public void Validate_InvalidModelVariant_ShouldThrowArgumentException(string invalidVariant)
    {
        // Arrange
        var config = new EmbeddingConfiguration
        {
            ModelVariant = invalidVariant
        };
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void Validate_ConfigurationWithLargeModel_ShouldBeValid()
    {
        // Arrange
        var config = new EmbeddingConfiguration
        {
            EmbeddingDimension = 768,
            ModelVariant = "large",
            DistanceMetric = "Dot",
            EnableTextPreprocessing = false,
            MaxTokenLength = 100,
            ImageInputSize = 256
        };
        
        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => config.Validate());
    }

    [Test]
    public void Validate_ConfigurationWithHugeModel_ShouldBeValid()
    {
        // Arrange
        var config = new EmbeddingConfiguration
        {
            EmbeddingDimension = 1024,
            ModelVariant = "huge",
            DistanceMetric = "Euclidean",
            EnableTextPreprocessing = true,
            MaxTokenLength = 77,
            ImageInputSize = 224
        };
        
        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => config.Validate());
    }
}