namespace AzurePhotoFlow.Services;

public interface IImageEmbeddingModel
{
    float[] GenerateEmbedding(byte[] imageBytes);
    
    /// <summary>
    /// Generate embedding for text input using CLIP text encoder.
    /// </summary>
    /// <param name="text">Text to generate embedding for</param>
    /// <returns>512-dimensional embedding vector</returns>
    float[] GenerateTextEmbedding(string text);
}
