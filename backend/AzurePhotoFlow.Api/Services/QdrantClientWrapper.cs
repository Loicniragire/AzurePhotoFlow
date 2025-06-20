using Api.Interfaces;
using Qdrant.Client.Grpc;
using System.Text;
using System.Text.Json;

namespace AzurePhotoFlow.Services;

public class QdrantClientWrapper : IQdrantClientWrapper
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<QdrantClientWrapper> _logger;

    public QdrantClientWrapper(HttpClient httpClient, ILogger<QdrantClientWrapper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var host = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6333";
        _baseUrl = $"http://{host}:{port}";
        
        _logger.LogInformation("QdrantClientWrapper: Initialized with base URL: {BaseUrl}", _baseUrl);
    }

    public async Task UpsertAsync(string collection, IEnumerable<PointStruct> points)
    {
        try
        {
            // First, ensure collection exists
            await EnsureCollectionExists(collection);
            
            // Convert PointStruct to Qdrant REST API format
            var restPoints = points.Select(ConvertToRestPoint).ToArray();
            
            var requestBody = new
            {
                points = restPoints
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{_baseUrl}/collections/{collection}/points", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("QdrantClientWrapper: Failed to upsert points. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Failed to upsert points: {response.StatusCode} - {errorContent}");
            }
            
            _logger.LogInformation("QdrantClientWrapper: Successfully upserted {Count} points to collection '{Collection}'", 
                restPoints.Length, collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantClientWrapper: Error upserting points to collection '{Collection}'", collection);
            throw;
        }
    }
    
    private async Task EnsureCollectionExists(string collection)
    {
        try
        {
            // Check if collection exists
            var response = await _httpClient.GetAsync($"{_baseUrl}/collections/{collection}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("QdrantClientWrapper: Collection '{Collection}' does not exist, creating it", collection);
                
                // Create collection with 38400-dimensional vectors (CLIP vision model output size)
                var createRequest = new
                {
                    vectors = new
                    {
                        size = 38400,
                        distance = "Cosine"
                    }
                };
                
                var json = JsonSerializer.Serialize(createRequest, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var createResponse = await _httpClient.PutAsync($"{_baseUrl}/collections/{collection}", content);
                
                if (!createResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create collection: {createResponse.StatusCode} - {errorContent}");
                }
                
                _logger.LogInformation("QdrantClientWrapper: Successfully created collection '{Collection}'", collection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantClientWrapper: Error ensuring collection '{Collection}' exists", collection);
            throw;
        }
    }
    
    private object ConvertToRestPoint(PointStruct point)
    {
        return new
        {
            id = point.Id.Uuid,
            vector = point.Vectors.Vector.Data.ToArray(),
            payload = point.Payload.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertQdrantValue(kvp.Value)
            )
        };
    }
    
    public async Task<SearchResult> SearchAsync(string collection, float[] vector, int limit, double threshold, Dictionary<string, object>? filter = null)
    {
        try
        {
            _logger.LogInformation("QdrantClientWrapper: Searching collection '{Collection}' with limit {Limit} and threshold {Threshold}", 
                collection, limit, threshold);

            var searchRequest = new
            {
                vector = vector,
                limit = limit,
                score_threshold = threshold,
                with_payload = true,
                filter = filter != null && filter.Any() ? CreateFilter(filter) : null
            };

            var json = JsonSerializer.Serialize(searchRequest, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/collections/{collection}/points/search", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("QdrantClientWrapper: Failed to search. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Search failed: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<SearchResponseWrapper>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var result = new SearchResult
            {
                Points = searchResponse?.Result?.Select(p => new SearchResultPoint
                {
                    Id = p.Id,
                    Score = p.Score,
                    Payload = p.Payload ?? new Dictionary<string, object>()
                }).ToList() ?? new List<SearchResultPoint>()
            };

            _logger.LogInformation("QdrantClientWrapper: Found {ResultCount} results for search in collection '{Collection}'", 
                result.Points.Count, collection);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantClientWrapper: Error searching collection '{Collection}'", collection);
            throw;
        }
    }

    private object? CreateFilter(Dictionary<string, object> filter)
    {
        var conditions = new List<object>();

        foreach (var kvp in filter)
        {
            if (kvp.Value is string stringValue)
            {
                conditions.Add(new
                {
                    key = kvp.Key,
                    match = new { value = stringValue }
                });
            }
        }

        if (!conditions.Any())
            return null;

        return new { must = conditions };
    }

    private object ConvertQdrantValue(Qdrant.Client.Grpc.Value value)
    {
        if (!string.IsNullOrEmpty(value.StringValue))
            return value.StringValue;
        if (value.IntegerValue != 0)
            return value.IntegerValue;
        if (value.DoubleValue != 0)
            return value.DoubleValue;
        if (value.BoolValue)
            return value.BoolValue;
        
        return value.StringValue ?? "";
    }

    // Helper classes for deserialization
    private class SearchResponseWrapper
    {
        public List<SearchResponsePoint>? Result { get; set; }
    }

    private class SearchResponsePoint
    {
        public string Id { get; set; } = string.Empty;
        public double Score { get; set; }
        public Dictionary<string, object>? Payload { get; set; }
    }

    public async Task<PointData?> GetPointAsync(string collection, string pointId)
    {
        try
        {
            _logger.LogInformation("QdrantClientWrapper: Retrieving point '{PointId}' from collection '{Collection}'", 
                pointId, collection);

            var response = await _httpClient.GetAsync($"{_baseUrl}/collections/{collection}/points/{pointId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("QdrantClientWrapper: Point '{PointId}' not found in collection '{Collection}'", 
                    pointId, collection);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("QdrantClientWrapper: Failed to get point. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Get point failed: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var pointResponse = JsonSerializer.Deserialize<PointResponseWrapper>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            if (pointResponse?.Result == null)
            {
                _logger.LogWarning("QdrantClientWrapper: Point '{PointId}' not found in response from collection '{Collection}'", 
                    pointId, collection);
                return null;
            }

            var result = new PointData
            {
                Id = pointResponse.Result.Id,
                Vector = pointResponse.Result.Vector ?? Array.Empty<float>(),
                Payload = pointResponse.Result.Payload ?? new Dictionary<string, object>()
            };

            _logger.LogInformation("QdrantClientWrapper: Successfully retrieved point '{PointId}' from collection '{Collection}'", 
                pointId, collection);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantClientWrapper: Error retrieving point '{PointId}' from collection '{Collection}'", 
                pointId, collection);
            throw;
        }
    }

    // Helper classes for point retrieval deserialization
    private class PointResponseWrapper
    {
        public PointResponseData? Result { get; set; }
    }

    private class PointResponseData
    {
        public string Id { get; set; } = string.Empty;
        public float[]? Vector { get; set; }
        public Dictionary<string, object>? Payload { get; set; }
    }
}
