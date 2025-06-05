namespace Api.Interfaces;

public interface IImageEmbeddingService
{
    Task StoreEmbeddingAsync(string objectKey, byte[] imageBytes);
}
