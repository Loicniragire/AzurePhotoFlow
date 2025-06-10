using Api.Interfaces;
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

    public Task<IEnumerable<ImageEmbedding>> GenerateEmbeddingsAsync(IEnumerable<ImageEmbeddingInput> images)
    {
        var embeddings = images.Select(i =>
        {
            var vector = _embeddingModel.GenerateEmbedding(i.ImageBytes);
            return new ImageEmbedding(i.ObjectKey, vector);
        }).ToList();

        return Task.FromResult<IEnumerable<ImageEmbedding>>(embeddings);
    }
}
