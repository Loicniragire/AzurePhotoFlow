using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnitTests;

namespace unitTests;

[TestFixture]
public class EmbeddingServiceTests
{
    [Test]
    public async Task GenerateEmbeddingsAsync_ReturnsVectors()
    {
        // Arrange: Get actual image data from embedded resource
        var imagePath = "Images/film/img20220920_0124.jpg";
        using var imageStream = TestHelper.GetEmbeddedResource(imagePath);
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        var imageBytes = memoryStream.ToArray();

        // Use same model path logic as the application (Program.cs)
        var modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "clip_vision_traced.pt";
        
        // Try multiple possible model locations for CI/CD compatibility
        var possiblePaths = new[]
        {
            modelPath, // From environment variable or default
            Path.Combine(Directory.GetCurrentDirectory(), "models", "model.onnx"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "models", "model.onnx"),
            "/Users/loicniragire/Workspace/AzurePhotoFlow/models/model.onnx" // Local development fallback
        };

        string? validModelPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                validModelPath = path;
                break;
            }
        }

        // Skip test if no model found (graceful degradation for CI/CD)
        if (validModelPath == null)
        {
            Assert.Ignore($"ONNX model not found. Searched paths: {string.Join(", ", possiblePaths)}");
            return;
        }

        Console.WriteLine($"Using ONNX model at: {validModelPath}");
        
        // Use real ONNX model instead of mock
        using var session = new InferenceSession(validModelPath);
        var embeddingModel = new OnnxImageEmbeddingModel(session);

        var logger = new Mock<ILogger<EmbeddingService>>();
        var service = new EmbeddingService(logger.Object, embeddingModel);

        var inputs = new List<ImageEmbeddingInput>
        {
            new ImageEmbeddingInput("img", imageBytes)
        };

        // Act
        var results = new List<ImageEmbedding>();
        await foreach(var e in service.GenerateEmbeddingsAsync(inputs.ToAsyncEnumerable()))
        {
            results.Add(e);
        }

        // Assert
        var embedding = results.Single();
        Assert.AreEqual("img", embedding.ObjectKey);
        Assert.IsNotNull(embedding.Vector);
        Assert.Greater(embedding.Vector.Length, 0, "Embedding vector should not be empty");
        
        // Verify the embedding contains actual float values (not all zeros)
        Assert.IsTrue(embedding.Vector.Any(v => v != 0), "Embedding should contain non-zero values");
        
        Console.WriteLine($"Generated embedding with {embedding.Vector.Length} dimensions");
        Console.WriteLine($"First 5 values: [{string.Join(", ", embedding.Vector.Take(5).Select(f => f.ToString("F4")))}]");
    }
}
