using System.Collections.Generic;
using Api.Models;

namespace Api.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(IEnumerable<ImageEmbedding> embeddings);
}
