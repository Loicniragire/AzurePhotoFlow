using Api.Interfaces;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace AzurePhotoFlow.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly ILogger<EmbeddingService> _logger;
    private readonly IImageEmbeddingModel _embeddingModel;

    public EmbeddingService(ILogger<EmbeddingService> logger, IImageEmbeddingModel embeddingModel)
    {
        _logger = logger;
        _embeddingModel = embeddingModel;
    }

    public async IAsyncEnumerable<ImageEmbedding> GenerateEmbeddingsAsync(IAsyncEnumerable<ImageEmbeddingInput> images)
    {
        await foreach (var i in images)
        {
            var vector = _embeddingModel.GenerateEmbedding(i.ImageBytes);
            yield return new ImageEmbedding(i.ObjectKey, vector);
        }
    }
}
