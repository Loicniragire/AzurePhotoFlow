using Api.Models;
using Api.Interfaces;
using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Qdrant.Client.Grpc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace unitTests;

[TestFixture]
public class VectorStoreTests
{
    [Test]
    public async Task UpsertAsync_DelegatesToClientWrapper()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        IEnumerable<PointStruct>? received = null;
        mockWrapper.Setup(w => w.UpsertAsync(It.IsAny<string>(), It.IsAny<IEnumerable<PointStruct>>()))
            .Callback<string, IEnumerable<PointStruct>>((c, pts) => received = pts)
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<QdrantVectorStore>>();
        var store = new QdrantVectorStore(logger.Object, mockWrapper.Object);
        var embeddings = new List<ImageEmbedding>
        {
            new ImageEmbedding("key", new float[] { 1f })
        };

        await store.UpsertAsync(embeddings);

        Assert.NotNull(received);
        var point = received!.Single();
        Assert.AreEqual("key", point.Id.Uuid);
        Assert.AreEqual("key", point.Payload["path"].StringValue);
    }
}
