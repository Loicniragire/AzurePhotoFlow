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

    public async Task<float[]> GenerateTextEmbeddingAsync(string text)
    {
        try
        {
            _logger.LogInformation("Generating text embedding for query: {Query}", text);
            
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text query cannot be null or empty", nameof(text));
            }

            // Generate embedding using the CLIP text encoder (simplified implementation)
            var embedding = await Task.Run(() => _embeddingModel.GenerateTextEmbedding(text));
            
            _logger.LogDebug("Generated text embedding with {Dimensions} dimensions for query: {Query}", 
                embedding.Length, text);
            
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate text embedding for query: {Query}", text);
            throw;
        }
    }
}
