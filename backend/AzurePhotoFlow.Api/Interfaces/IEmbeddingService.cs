using System.Collections.Generic;
using Api.Models;
using AzurePhotoFlow.Services;

namespace Api.Interfaces;

public interface IEmbeddingService
{
    IAsyncEnumerable<ImageEmbedding> GenerateEmbeddingsAsync(IAsyncEnumerable<ImageEmbeddingInput> images);
}
