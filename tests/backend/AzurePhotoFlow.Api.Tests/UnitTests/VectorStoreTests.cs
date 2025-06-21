using Api.Models;
using Api.Interfaces;
using AzurePhotoFlow.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;
using System;
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
        
        // Verify that the ID is a valid UUID (not the original key)
        Assert.IsTrue(Guid.TryParse(point.Id.Uuid, out _), "Point ID should be a valid UUID");
        Assert.AreNotEqual("key", point.Id.Uuid, "Point ID should not be the original key");
        
        // Verify that the original key is preserved in the payload
        Assert.AreEqual("key", point.Payload["path"].StringValue);
        Assert.AreEqual("key", point.Payload["object_key"].StringValue);
    }

    [Test]
    public async Task UpsertAsync_GeneratesDeterministicUUIDs()
    {
        var mockWrapper = new Mock<IQdrantClientWrapper>();
        IEnumerable<PointStruct>? firstReceived = null;
        IEnumerable<PointStruct>? secondReceived = null;
        
        var callCount = 0;
        mockWrapper.Setup(w => w.UpsertAsync(It.IsAny<string>(), It.IsAny<IEnumerable<PointStruct>>()))
            .Callback<string, IEnumerable<PointStruct>>((c, pts) => 
            {
                if (callCount == 0) firstReceived = pts;
                else secondReceived = pts;
                callCount++;
            })
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<QdrantVectorStore>>();
        var store = new QdrantVectorStore(logger.Object, mockWrapper.Object);

        var embeddings = new List<ImageEmbedding>
        {
            new ImageEmbedding("test-file-path.jpg", new float[] { 1f })
        };

        // Call twice with the same ObjectKey
        await store.UpsertAsync(embeddings);
        await store.UpsertAsync(embeddings);

        Assert.NotNull(firstReceived);
        Assert.NotNull(secondReceived);
        
        var firstPoint = firstReceived!.Single();
        var secondPoint = secondReceived!.Single();
        
        // Same ObjectKey should generate the same UUID
        Assert.AreEqual(firstPoint.Id.Uuid, secondPoint.Id.Uuid, 
            "Same ObjectKey should generate the same UUID deterministically");
        
        // Verify UUID is valid
        Assert.IsTrue(Guid.TryParse(firstPoint.Id.Uuid, out _), "Generated UUID should be valid");
    }
}
