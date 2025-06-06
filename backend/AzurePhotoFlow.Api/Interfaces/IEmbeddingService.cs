using System.Collections.Generic;
using AzurePhotoFlow.Services;

namespace Api.Interfaces;

public interface IEmbeddingService
{
    Task GenerateAsync(IEnumerable<ImageEmbeddingInput> images);
}
