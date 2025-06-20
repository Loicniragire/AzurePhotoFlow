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
    
    /// <summary>
    /// Get the embedding vector for a specific object key.
    /// </summary>
    /// <param name="objectKey">The object key to retrieve the embedding for</param>
    /// <returns>The embedding vector if found, null otherwise</returns>
    Task<float[]?> GetEmbeddingAsync(string objectKey);
    
    /// <summary>
    /// Get the total number of images in the vector store.
    /// </summary>
    /// <param name="filter">Optional metadata filter conditions</param>
    /// <returns>The total count of images matching the filter</returns>
    Task<long> GetTotalCountAsync(Dictionary<string, object>? filter = null);
}
