using System.Collections.Generic;
using Api.Models;

namespace Api.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(IEnumerable<ImageEmbedding> embeddings);
    
    /// <summary>
    /// Search for similar vectors using cosine similarity.
    /// </summary>
    /// <param name="queryVector">The query vector to search for</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="threshold">Minimum similarity threshold (0.0 to 1.0)</param>
    /// <param name="filter">Optional metadata filter conditions</param>
    /// <returns>List of similar vectors with similarity scores and metadata</returns>
    Task<IEnumerable<VectorSearchResult>> SearchAsync(float[] queryVector, int limit = 20, double threshold = 0.5, Dictionary<string, object>? filter = null);
}

/// <summary>
/// Represents a single result from vector similarity search.
/// </summary>
public class VectorSearchResult
{
    /// <summary>
    /// The object key/path from the vector payload.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Similarity score between query and result vector (0.0 to 1.0).
    /// </summary>
    public double SimilarityScore { get; set; }
    
    /// <summary>
    /// Additional metadata stored with the vector.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
