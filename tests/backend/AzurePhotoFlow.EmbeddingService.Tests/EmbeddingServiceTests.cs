using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Qdrant.Client.Grpc;

namespace unitTests;

[TestFixture]
public class EmbeddingServiceTests
{
    [Test]
    public async Task GenerateAsync_UpsertsEachImageWithEmbedding()
    {
        var qdrantMock = new Mock<IQdrantClientWrapper>();
        var loggerMock = new Mock<ILogger<EmbeddingService>>();
        var modelMock = new Mock<IImageEmbeddingModel>();
        var vector = new float[] { 0.1f, 0.2f };
        modelMock.Setup(m => m.GenerateEmbedding(It.IsAny<byte[]>())).Returns(vector);

        var service = new EmbeddingService(qdrantMock.Object, loggerMock.Object, modelMock.Object);

        var images = new[]
        {
            new ImageEmbeddingInput("obj1", new byte[]{1}),
            new ImageEmbeddingInput("obj2", new byte[]{2})
        };

        await service.GenerateAsync(images);

        qdrantMock.Verify(c => c.UpsertAsync(
            It.Is<string>(s => s == "images"),
            It.Is<IEnumerable<PointStruct>>(p =>
                p.Single().Id.Uuid == "obj1" &&
                p.Single().Vectors.Vector.Data.SequenceEqual(vector))
        ), Times.Once);

        qdrantMock.Verify(c => c.UpsertAsync(
            It.Is<string>(s => s == "images"),
            It.Is<IEnumerable<PointStruct>>(p =>
                p.Single().Id.Uuid == "obj2" &&
                p.Single().Vectors.Vector.Data.SequenceEqual(vector))
        ), Times.Once);

        modelMock.Verify(m => m.GenerateEmbedding(It.IsAny<byte[]>()), Times.Exactly(2));
    }
}
