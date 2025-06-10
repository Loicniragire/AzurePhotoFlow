namespace AzurePhotoFlow.Services;

/// <summary>
/// Represents an embedding vector associated with an image key.
/// </summary>
public record ImageEmbedding(string ObjectKey, float[] Vector);
