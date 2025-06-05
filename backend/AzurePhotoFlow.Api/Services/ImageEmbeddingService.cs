using Api.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLaborsImage = SixLabors.ImageSharp.Image;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace AzurePhotoFlow.Services;

public class ImageEmbeddingService : IImageEmbeddingService
{
    private readonly QdrantClient _qdrantClient;
    private readonly InferenceSession _session;
    private readonly ILogger<ImageEmbeddingService> _logger;
    private readonly string _collection;

    public ImageEmbeddingService(QdrantClient qdrantClient, InferenceSession session, ILogger<ImageEmbeddingService> logger)
    {
        _qdrantClient = qdrantClient;
        _session = session;
        _logger = logger;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
    }

    public async Task StoreEmbeddingAsync(string objectKey, byte[] imageBytes)
    {
        try
        {
            float[] vector = GenerateEmbedding(imageBytes);
            var point = new PointStruct
            {
                Id = new PointId { Uuid = objectKey },
                Vectors = vector
            };
            point.Payload.Add("path", new QdrantValue { StringValue = objectKey });
            await _qdrantClient.UpsertAsync(_collection, new[] { point });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store embedding for {ObjectKey}", objectKey);
        }
    }

    private float[] GenerateEmbedding(byte[] imageBytes)
    {
        using var image = SixLaborsImage.Load<Rgb24>(imageBytes);
        image.Mutate(x => x.Resize(224, 224));
        var tensor = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var p = image[x, y];
                tensor[0, 0, y, x] = p.R / 255f;
                tensor[0, 1, y, x] = p.G / 255f;
                tensor[0, 2, y, x] = p.B / 255f;
            }
        }
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) };
        using var results = _session.Run(inputs);
        return results.First().AsEnumerable<float>().ToArray();
    }
}
