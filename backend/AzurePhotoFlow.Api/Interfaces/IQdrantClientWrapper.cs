using Qdrant.Client.Grpc;

namespace Api.Interfaces;

public interface IQdrantClientWrapper
{
    Task UpsertAsync(string collection, IEnumerable<PointStruct> points);
    
    /// <summary>
    /// Search for similar vectors in the collection.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="vector">Query vector</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="threshold">Minimum similarity threshold</param>
    /// <param name="filter">Optional metadata filter</param>
    /// <returns>Search results with scores and metadata</returns>
    Task<SearchResult> SearchAsync(string collection, float[] vector, int limit, double threshold, Dictionary<string, object>? filter = null);
}

/// <summary>
/// Represents search results from Qdrant.
/// </summary>
public class SearchResult
{
    public List<SearchResultPoint> Points { get; set; } = new();
}

/// <summary>
/// Represents a single search result point.
/// </summary>
public class SearchResultPoint
{
    public string Id { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
}
