namespace AzurePhotoFlow.Services;

public interface IImageEmbeddingModel
{
    float[] GenerateEmbedding(byte[] imageBytes);
}
