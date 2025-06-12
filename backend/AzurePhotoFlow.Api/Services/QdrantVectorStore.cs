using Api.Interfaces;
using Api.Models;
using Qdrant.Client.Grpc;
using System.Linq;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace AzurePhotoFlow.Services;

public class QdrantVectorStore : IVectorStore
{
    private readonly IQdrantClientWrapper _client;
	private readonly ILogger<QdrantVectorStore> _logger;
    private readonly string _collection;

    public QdrantVectorStore(ILogger<QdrantVectorStore> logger, IQdrantClientWrapper client)
    {
        _client = client;
		_logger = logger;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
    }

    public async Task UpsertAsync(IEnumerable<ImageEmbedding> embeddings)
    {
        try
        {
            var embeddingsList = embeddings.ToList();
            _logger.LogInformation("QdrantVectorStore: Starting upsert of {Count} embeddings to collection '{Collection}'", 
                embeddingsList.Count, _collection);

            var points = embeddingsList.Select(e =>
            {
                _logger.LogDebug("QdrantVectorStore: Processing embedding for ObjectKey: {ObjectKey}, Vector null: {VectorNull}, Vector length: {VectorLength}", 
                    e.ObjectKey, e.Vector == null, e.Vector?.Length ?? 0);
                
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = e.ObjectKey },
                    Vectors = e.Vector
                };
                point.Payload.Add("path", new QdrantValue { StringValue = e.ObjectKey });
                return point;
            });

            _logger.LogInformation("QdrantVectorStore: Sending upsert request to Qdrant for collection '{Collection}'", _collection);
            await _client.UpsertAsync(_collection, points);
            _logger.LogInformation("QdrantVectorStore: Successfully upserted {Count} points to collection '{Collection}'", 
                embeddingsList.Count, _collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore: Failed to upsert embeddings to collection '{Collection}'. Error: {ErrorMessage}", 
                _collection, ex.Message);
            throw;
        }
    }
}
