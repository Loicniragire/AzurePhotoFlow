using Api.Interfaces;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;
using System.Collections.Generic;
using System.Linq;
using AzurePhotoFlow.Services;
using Google.Protobuf.Collections;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace AzurePhotoFlow.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly IQdrantClientWrapper _qdrantClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly IImageEmbeddingModel _embeddingModel;
    private readonly string _collection;

    public EmbeddingService(IQdrantClientWrapper qdrantClient, ILogger<EmbeddingService> logger, IImageEmbeddingModel embeddingModel)
    {
        _qdrantClient = qdrantClient;
        _logger = logger;
        _embeddingModel = embeddingModel;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
    }

    public async Task GenerateAsync(IEnumerable<ImageEmbeddingInput> images)
    {
        foreach (var image in images)
        {
            float[] vector = _embeddingModel.GenerateEmbedding(image.ImageBytes);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = image.ObjectKey },
                Vectors = vector
            };
            point.Payload.Add("path", new QdrantValue { StringValue = image.ObjectKey });
            await _qdrantClient.UpsertAsync(_collection, new[] { point });
        }
    }
}
