using Api.Interfaces;
using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace unitTests;

[TestFixture]
public class EmbeddingServiceTests
{
    [Test]
    public async Task GenerateEmbeddingsAsync_ReturnsVectors()
    {
        var mockModel = new Mock<IImageEmbeddingModel>();
        mockModel.Setup(m => m.GenerateEmbedding(It.IsAny<byte[]>())).Returns(new float[] { 0.5f });

        var logger = new Mock<ILogger<EmbeddingService>>();
        var service = new EmbeddingService(logger.Object, mockModel.Object);

        var inputs = new List<ImageEmbeddingInput>
        {
            new ImageEmbeddingInput("img", new byte[] {1,2})
        };

        var result = await service.GenerateEmbeddingsAsync(inputs);

        var embedding = result.Single();
        Assert.AreEqual("img", embedding.ObjectKey);
        Assert.AreEqual(0.5f, embedding.Vector[0]);
    }
}
