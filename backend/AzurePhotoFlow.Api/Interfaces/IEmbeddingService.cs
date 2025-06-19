using System.Collections.Generic;
using Api.Models;
using AzurePhotoFlow.Services;

namespace Api.Interfaces;

public interface IEmbeddingService
{
    IAsyncEnumerable<ImageEmbedding> GenerateEmbeddingsAsync(IAsyncEnumerable<ImageEmbeddingInput> images);
    
    /// <summary>
    /// Generate embedding vector for a text query using CLIP text encoder.
    /// </summary>
    /// <param name="text">The text query to generate embedding for</param>
    /// <returns>512-dimensional embedding vector</returns>
    Task<float[]> GenerateTextEmbeddingAsync(string text);
}
