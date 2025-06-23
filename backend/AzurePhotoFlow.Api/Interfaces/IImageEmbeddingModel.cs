namespace AzurePhotoFlow.Services;

public interface IImageEmbeddingModel
{
    float[] GenerateEmbedding(byte[] imageBytes);
    
    /// <summary>
    /// Generate embedding for text input using CLIP text encoder.
    /// </summary>
    /// <param name="text">Text to generate embedding for</param>
    /// <returns>Embedding vector with dimension specified in configuration (512/768/1024)</returns>
    float[] GenerateTextEmbedding(string text);
}
