using Qdrant.Client.Grpc;

namespace AzurePhotoFlow.Services;

public interface IQdrantClientWrapper
{
    Task UpsertAsync(string collection, IEnumerable<PointStruct> points);
}
