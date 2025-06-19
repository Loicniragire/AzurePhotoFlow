using Api.Interfaces;
using Api.Models;
using Qdrant.Client.Grpc;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
                
                // Generate a deterministic UUID from the ObjectKey to maintain consistency
                var uuid = GenerateUuidFromString(e.ObjectKey);
                
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = uuid },
                    Vectors = e.Vector
                };
                point.Payload.Add("path", new QdrantValue { StringValue = e.ObjectKey });
                point.Payload.Add("object_key", new QdrantValue { StringValue = e.ObjectKey });
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

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(float[] queryVector, int limit = 20, double threshold = 0.5, Dictionary<string, object>? filter = null)
    {
        try
        {
            _logger.LogInformation("QdrantVectorStore: Starting vector search in collection '{Collection}' with limit {Limit} and threshold {Threshold}", 
                _collection, limit, threshold);

            _logger.LogDebug("QdrantVectorStore: Executing search request for collection '{Collection}'", _collection);
            var searchResponse = await _client.SearchAsync(_collection, queryVector, limit, threshold, filter);
            
            var results = searchResponse.Points.Select(point => new VectorSearchResult
            {
                ObjectKey = point.Payload.ContainsKey("object_key") ? 
                    point.Payload["object_key"].ToString() ?? "" :
                    point.Payload.ContainsKey("path") ? point.Payload["path"].ToString() ?? "" : "",
                SimilarityScore = point.Score,
                Metadata = point.Payload
            }).ToList();

            _logger.LogInformation("QdrantVectorStore: Found {ResultCount} results for vector search in collection '{Collection}'", 
                results.Count, _collection);
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore: Failed to search vectors in collection '{Collection}'. Error: {ErrorMessage}", 
                _collection, ex.Message);
            throw;
        }
    }

    private static string GenerateUuidFromString(string input)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        
        // Convert MD5 hash to UUID format (version 3)
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30); // Version 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // Variant bits
        
        var uuid = new Guid(hash);
        return uuid.ToString();
    }
}
