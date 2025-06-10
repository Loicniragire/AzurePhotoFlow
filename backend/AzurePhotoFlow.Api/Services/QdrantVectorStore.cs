using Qdrant.Client.Grpc;
using System.Linq;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace AzurePhotoFlow.Services;

public class QdrantVectorStore : IVectorStore
{
    private readonly IQdrantClientWrapper _client;
    private readonly string _collection;

    public QdrantVectorStore(IQdrantClientWrapper client)
    {
        _client = client;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
    }

    public async Task UpsertAsync(IEnumerable<ImageEmbedding> embeddings)
    {
        var points = embeddings.Select(e =>
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = e.ObjectKey },
                Vectors = e.Vector
            };
            point.Payload.Add("path", new QdrantValue { StringValue = e.ObjectKey });
            return point;
        });
        await _client.UpsertAsync(_collection, points);
    }
}
