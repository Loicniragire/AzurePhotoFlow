using System.Collections.Generic;
using AzurePhotoFlow.Services;

namespace Api.Interfaces;

public interface IEmbeddingService
{
    Task<IEnumerable<ImageEmbedding>> GenerateEmbeddingsAsync(IEnumerable<ImageEmbeddingInput> images);
}
