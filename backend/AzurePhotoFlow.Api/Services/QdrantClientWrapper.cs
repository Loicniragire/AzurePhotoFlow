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
                
                // Create collection with 512-dimensional vectors (CLIP embedding size)
                var createRequest = new
                {
                    vectors = new
                    {
                        size = 512,
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
}
