using System.Collections.Generic;

namespace AzurePhotoFlow.Services;

public interface IVectorStore
{
    Task UpsertAsync(IEnumerable<ImageEmbedding> embeddings);
}
