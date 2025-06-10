using Qdrant.Client.Grpc;

namespace Api.Interfaces;

public interface IQdrantClientWrapper
{
    Task UpsertAsync(string collection, IEnumerable<PointStruct> points);
}
