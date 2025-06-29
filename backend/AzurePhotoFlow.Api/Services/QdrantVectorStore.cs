using Api.Interfaces;
using Api.Models;
using Qdrant.Client.Grpc;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AzurePhotoFlow.Api.Interfaces;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace AzurePhotoFlow.Services;

public class QdrantVectorStore : IVectorStore
{
    private readonly IQdrantClientWrapper _client;
	private readonly ILogger<QdrantVectorStore> _logger;
    private readonly IImageMappingRepository _imageMappingRepository;
    private readonly string _collection;

    public QdrantVectorStore(ILogger<QdrantVectorStore> logger, IQdrantClientWrapper client, IImageMappingRepository imageMappingRepository)
    {
        _client = client;
		_logger = logger;
        _imageMappingRepository = imageMappingRepository;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
    }

    public async Task UpsertAsync(IEnumerable<ImageEmbedding> embeddings)
    {
        try
        {
            var embeddingsList = embeddings.ToList();
            _logger.LogInformation("QdrantVectorStore: Starting upsert of {Count} embeddings to collection '{Collection}'", 
                embeddingsList.Count, _collection);

            // Get image mappings for all object keys
            var objectKeys = embeddingsList.Select(e => e.ObjectKey).ToList();
            var imageMappings = new Dictionary<string, Guid>();
            
            foreach (var objectKey in objectKeys)
            {
                var mapping = await _imageMappingRepository.GetByObjectKeyAsync(objectKey);
                if (mapping != null)
                {
                    imageMappings[objectKey] = mapping.Id;
                }
                else
                {
                    // Fallback to old method if mapping not found
                    var fallbackGuid = GenerateUuidFromObjectKey(objectKey);
                    imageMappings[objectKey] = Guid.Parse(fallbackGuid);
                    _logger.LogWarning("QdrantVectorStore: No mapping found for ObjectKey: {ObjectKey}, using fallback GUID: {Guid}", 
                        objectKey, fallbackGuid);
                }
            }

            var points = embeddingsList.Select(e =>
            {
                _logger.LogDebug("QdrantVectorStore: Processing embedding for ObjectKey: {ObjectKey}, Vector null: {VectorNull}, Vector length: {VectorLength}", 
                    e.ObjectKey, e.Vector == null, e.Vector?.Length ?? 0);
                
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = imageMappings[e.ObjectKey].ToString() },
                    Vectors = e.Vector
                };
                
                // Store both path and GUID for backward compatibility and future queries
                point.Payload.Add("path", new QdrantValue { StringValue = e.ObjectKey });
                point.Payload.Add("object_key", new QdrantValue { StringValue = e.ObjectKey });
                point.Payload.Add("guid", new QdrantValue { StringValue = imageMappings[e.ObjectKey].ToString() });
                
                // Extract metadata from object key path
                // Expected format: {year}/{timestamp}/{projectName}/{directoryName}/{fileName}
                // Example: "2025-06-21/Search testing/RawFiles/Test/_A8A9030.jpeg"
                var pathParts = e.ObjectKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length >= 1)
                {
                    // Try to extract year from first part (YYYY-MM-DD format)
                    var datePart = pathParts[0];
                    if (datePart.Length >= 4 && DateTime.TryParseExact(datePart.Substring(0, 4), "yyyy", null, System.Globalization.DateTimeStyles.None, out _))
                    {
                        point.Payload.Add("year", new QdrantValue { StringValue = datePart.Substring(0, 4) });
                    }
                }
                if (pathParts.Length >= 2)
                {
                    // Second part is usually project/timestamp - use as project_name for now
                    var projectPart = pathParts[1];
                    point.Payload.Add("project_name", new QdrantValue { StringValue = projectPart });
                }
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

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(float[] queryVector, int limit = 20, double threshold = 0.5, double? maxThreshold = null, Dictionary<string, object>? filter = null)
    {
        try
        {
            var maxThresholdText = maxThreshold.HasValue ? maxThreshold.Value.ToString("F4") : "null";
            _logger.LogInformation("QdrantVectorStore: Starting vector search in collection '{Collection}' with limit {Limit}, threshold {Threshold}, and maxThreshold {MaxThreshold}", 
                _collection, limit, threshold, maxThresholdText);

            // Log detailed query information
            _logger.LogInformation("QdrantVectorStore: Query vector details - Length: {VectorLength}, First 5 values: [{Values}]",
                queryVector.Length, 
                string.Join(", ", queryVector.Take(5).Select(v => v.ToString("F4"))));
            
            if (filter != null && filter.Any())
            {
                _logger.LogInformation("QdrantVectorStore: Applied filters: {Filters}", 
                    string.Join(", ", filter.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }
            else
            {
                _logger.LogInformation("QdrantVectorStore: No filters applied");
            }

            _logger.LogDebug("QdrantVectorStore: Executing search request for collection '{Collection}'", _collection);
            var searchResponse = await _client.SearchAsync(_collection, queryVector, limit, threshold, maxThreshold, filter);
            
            var results = searchResponse.Points.Select(point => new VectorSearchResult
            {
                // For backward compatibility, still use object_key as primary identifier
                ObjectKey = point.Payload.ContainsKey("object_key") ? 
                    point.Payload["object_key"].ToString() ?? "" :
                    point.Payload.ContainsKey("path") ? point.Payload["path"].ToString() ?? "" : "",
                // Add GUID for new system
                Id = point.Payload.ContainsKey("guid") ? 
                    point.Payload["guid"].ToString() ?? point.Id :
                    point.Id,
                SimilarityScore = point.Score,
                Metadata = point.Payload
            }).ToList();

            _logger.LogInformation("QdrantVectorStore: Found {ResultCount} results for vector search in collection '{Collection}'", 
                results.Count, _collection);
            
            return results;
        }
        catch (Exception ex)
        {
            // Check if the error is due to collection not existing or being empty
            if (ex.Message.Contains("doesn't exist") || ex.Message.Contains("Not found"))
            {
                _logger.LogWarning("QdrantVectorStore: Collection '{Collection}' not found or empty. This usually means no images have been uploaded and processed yet.", _collection);
                return new List<VectorSearchResult>(); // Return empty results instead of throwing
            }
            
            _logger.LogError(ex, "QdrantVectorStore: Failed to search vectors in collection '{Collection}'. Error: {ErrorMessage}", 
                _collection, ex.Message);
            throw;
        }
    }

    public async Task<float[]?> GetEmbeddingAsync(string objectKey)
    {
        try
        {
            _logger.LogInformation("QdrantVectorStore: Retrieving embedding for ObjectKey: {ObjectKey} from collection '{Collection}'", 
                objectKey, _collection);

            // Get the actual GUID from the database instead of generating one
            var mapping = await _imageMappingRepository.GetByObjectKeyAsync(objectKey);
            if (mapping == null)
            {
                _logger.LogWarning("QdrantVectorStore: No image mapping found for ObjectKey: {ObjectKey}", objectKey);
                return null;
            }

            // Use the actual GUID from the database as the point ID
            var pointId = mapping.Id.ToString();
            var pointData = await _client.GetPointAsync(_collection, pointId);
            
            if (pointData == null)
            {
                _logger.LogWarning("QdrantVectorStore: No embedding found for ObjectKey: {ObjectKey} with GUID: {PointId}", objectKey, pointId);
                return null;
            }

            _logger.LogInformation("QdrantVectorStore: Successfully retrieved embedding for ObjectKey: {ObjectKey} with GUID: {PointId}", objectKey, pointId);
            return pointData.Vector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore: Failed to retrieve embedding for ObjectKey: {ObjectKey} from collection '{Collection}'. Error: {ErrorMessage}", 
                objectKey, _collection, ex.Message);
            throw;
        }
    }

    public async Task<long> GetTotalCountAsync(Dictionary<string, object>? filter = null)
    {
        try
        {
            _logger.LogInformation("QdrantVectorStore: Getting total count for collection '{Collection}'", _collection);
            
            var count = await _client.GetCountAsync(_collection, filter);
            
            _logger.LogInformation("QdrantVectorStore: Collection '{Collection}' has {Count} total images", _collection, count);
            return count;
        }
        catch (Exception ex)
        {
            // Check if the error is due to collection not existing
            if (ex.Message.Contains("doesn't exist") || ex.Message.Contains("Not found"))
            {
                _logger.LogWarning("QdrantVectorStore: Collection '{Collection}' not found. This usually means no images have been uploaded and processed yet.", _collection);
                return 0; // Return 0 instead of throwing
            }
            
            _logger.LogError(ex, "QdrantVectorStore: Failed to get total count for collection '{Collection}'. Error: {ErrorMessage}", 
                _collection, ex.Message);
            throw;
        }
    }

    public async Task<string> GetCollectionNameAsync()
    {
        return await Task.FromResult(_collection);
    }

    private string GenerateUuidFromObjectKey(string objectKey)
    {
        // Generate a deterministic UUID based on the object key using SHA-256 hash
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(objectKey));
        
        // Use the first 16 bytes of the hash to create a UUID
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        
        // Set version (4) and variant bits to make it a valid UUID v4
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant bits
        
        return new Guid(guidBytes).ToString();
    }

}
