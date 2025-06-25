using Api.Interfaces;
using Qdrant.Client.Grpc;
using System.Text;
using System.Text.Json;
using AzurePhotoFlow.Api.Models;

namespace AzurePhotoFlow.Services;

public class QdrantClientWrapper : IQdrantClientWrapper
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<QdrantClientWrapper> _logger;
    private readonly EmbeddingConfiguration _config;

    public QdrantClientWrapper(HttpClient httpClient, EmbeddingConfiguration config, ILogger<QdrantClientWrapper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        var host = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6333";
        _baseUrl = $"http://{host}:{port}";
        
        _logger.LogInformation("QdrantClientWrapper: Initialized with base URL: {BaseUrl}, EmbeddingDimension: {Dimension}, DistanceMetric: {Metric}", 
            _baseUrl, _config.EmbeddingDimension, _config.DistanceMetric);
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
                
                // Create collection with configured embedding dimensions and distance metric
                var createRequest = new
                {
                    vectors = new
                    {
                        size = _config.EmbeddingDimension,
                        distance = _config.DistanceMetric
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
                
                _logger.LogInformation("QdrantClientWrapper: Successfully created collection '{Collection}' with {Dimensions} dimensions using {Metric} distance", 
                    collection, _config.EmbeddingDimension, _config.DistanceMetric);
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
    
    public async Task<SearchResult> SearchAsync(string collection, float[] vector, int limit, double threshold, double? maxThreshold = null, Dictionary<string, object>? filter = null)
    {
        try
        {
            var maxThresholdText = maxThreshold.HasValue ? maxThreshold.Value.ToString("F4") : "null";
            _logger.LogInformation("QdrantClientWrapper: Searching collection '{Collection}' with limit {Limit}, threshold {Threshold}, and maxThreshold {MaxThreshold}", 
                collection, limit, threshold, maxThresholdText);

            // If maxThreshold is specified, we need to potentially fetch more results to filter them afterward
            var searchLimit = maxThreshold.HasValue ? Math.Max(limit * 2, 100) : limit;
            
            var searchRequest = new
            {
                vector = vector,
                limit = searchLimit,
                score_threshold = threshold,
                with_payload = true,
                filter = filter != null && filter.Any() ? CreateFilter(filter) : null
            };

            var json = JsonSerializer.Serialize(searchRequest, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            // Log the exact Qdrant search request details
            _logger.LogInformation("QdrantClientWrapper: Sending search request to Qdrant");
            _logger.LogInformation("QdrantClientWrapper: Request URL: {Url}", $"{_baseUrl}/collections/{collection}/points/search");
            _logger.LogInformation("QdrantClientWrapper: Vector length: {VectorLength}, First 5 values: [{Values}]", 
                vector.Length, string.Join(", ", vector.Take(5).Select(v => v.ToString("F4"))));
            
            // Log the filter object if present
            if (searchRequest.filter != null)
            {
                var filterJson = JsonSerializer.Serialize(searchRequest.filter, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true 
                });
                _logger.LogInformation("QdrantClientWrapper: Applied filter: {Filter}", filterJson);
            }
            else
            {
                _logger.LogInformation("QdrantClientWrapper: No filter applied");
            }

            // Log the complete request body (truncated if too long)
            var truncatedJson = json.Length > 500 ? json.Substring(0, 500) + "..." : json;
            _logger.LogInformation("QdrantClientWrapper: Request body: {RequestBody}", truncatedJson);

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
            
            // Log response status and basic info
            _logger.LogInformation("QdrantClientWrapper: Received response from Qdrant. Status: {StatusCode}, Content length: {ContentLength}", 
                response.StatusCode, responseContent.Length);
            
            var searchResponse = JsonSerializer.Deserialize<SearchResponseWrapper>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var allPoints = searchResponse?.Result?.Select(p => new SearchResultPoint
            {
                Id = p.Id,
                Score = p.Score,
                Payload = p.Payload ?? new Dictionary<string, object>()
            }).ToList() ?? new List<SearchResultPoint>();

            // Apply maxThreshold filtering if specified
            var filteredPoints = allPoints;
            if (maxThreshold.HasValue)
            {
                var originalCount = filteredPoints.Count;
                filteredPoints = filteredPoints.Where(p => p.Score <= maxThreshold.Value).ToList();
                var filteredCount = filteredPoints.Count;
                
                if (originalCount != filteredCount)
                {
                    _logger.LogInformation("QdrantClientWrapper: Filtered {FilteredOut} results that exceeded maxThreshold {MaxThreshold}. Results: {OriginalCount} -> {FilteredCount}", 
                        originalCount - filteredCount, maxThreshold.Value, originalCount, filteredCount);
                }
            }
            
            // Limit to the requested count
            var finalPoints = filteredPoints.Take(limit).ToList();

            var result = new SearchResult
            {
                Points = finalPoints
            };

            _logger.LogInformation("QdrantClientWrapper: Found {ResultCount} results for search in collection '{Collection}'", 
                result.Points.Count, collection);

            // Log detailed results info
            if (result.Points.Any())
            {
                _logger.LogInformation("QdrantClientWrapper: Top result - ID: {TopId}, Score: {TopScore}", 
                    result.Points.First().Id, result.Points.First().Score);
                
                // Log score distribution
                var scores = result.Points.Select(p => p.Score).ToList();
                _logger.LogInformation("QdrantClientWrapper: Score range - Min: {MinScore:F4}, Max: {MaxScore:F4}, Avg: {AvgScore:F4}", 
                    scores.Min(), scores.Max(), scores.Average());
            }
            else
            {
                _logger.LogWarning("QdrantClientWrapper: No results found despite collection having data");
            }

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

    public async Task<long> GetCountAsync(string collection, Dictionary<string, object>? filter = null)
    {
        try
        {
            _logger.LogInformation("QdrantClientWrapper: Getting count for collection '{Collection}' with filter: {FilterCount} items", 
                collection, filter?.Count ?? 0);

            var filterObj = filter != null && filter.Any() ? CreateFilter(filter) : null;
            _logger.LogInformation("QdrantClientWrapper: Created filter object: {FilterObject}", 
                filterObj != null ? JsonSerializer.Serialize(filterObj) : "null");

            var countRequest = new
            {
                filter = filterObj
            };

            var json = JsonSerializer.Serialize(countRequest, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var requestUrl = $"{_baseUrl}/collections/{collection}/points/count";
            _logger.LogInformation("QdrantClientWrapper: Making count request to URL: {RequestUrl}", requestUrl);
            _logger.LogInformation("QdrantClientWrapper: Request body: {RequestBody}", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("QdrantClientWrapper: Collection '{Collection}' not found", collection);
                return 0;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("QdrantClientWrapper: Failed to get count. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Get count failed: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("QdrantClientWrapper: Raw count response: {ResponseContent}", responseContent);
            
            var countResponse = JsonSerializer.Deserialize<CountResponseWrapper>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            _logger.LogInformation("QdrantClientWrapper: Deserialized countResponse - Result null: {ResultNull}, Result.Count: {ResultCount}", 
                countResponse?.Result == null, countResponse?.Result?.Count);

            var count = countResponse?.Result?.Count ?? 0;
            _logger.LogInformation("QdrantClientWrapper: Collection '{Collection}' has {Count} points", collection, count);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantClientWrapper: Error getting count for collection '{Collection}'", collection);
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

    // Helper classes for count response deserialization
    private class CountResponseWrapper
    {
        public CountResponseData? Result { get; set; }
    }

    private class CountResponseData
    {
        public long Count { get; set; }
    }
}
