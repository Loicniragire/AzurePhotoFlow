using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AzurePhotoFlow.Services;

public class QdrantClientWrapper : IQdrantClientWrapper
{
    private readonly QdrantClient _client;

    public QdrantClientWrapper(QdrantClient client)
    {
        _client = client;
    }

    public async Task UpsertAsync(string collection, IEnumerable<PointStruct> points)
    {
        await _client.UpsertAsync(collection, points.ToList());
    }
}
