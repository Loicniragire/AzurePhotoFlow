using Api.Interfaces;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AzurePhotoFlow.Services;
using Google.Protobuf.Collections;
using QdrantValue = Qdrant.Client.Grpc.Value;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace AzurePhotoFlow.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly QdrantClient _qdrantClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly InferenceSession _session;
    private readonly string _collection;

    public EmbeddingService(QdrantClient qdrantClient, ILogger<EmbeddingService> logger)
    {
        _qdrantClient = qdrantClient;
        _logger = logger;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
        string modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "model.onnx";
        _session = new InferenceSession(modelPath);
    }

    public async Task GenerateAsync(IEnumerable<ImageEmbeddingInput> images)
    {
        foreach (var image in images)
        {
            float[] vector = GenerateEmbedding(image.ImageBytes);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = image.ObjectKey },
                Vectors = vector
            };
            point.Payload.Add("path", new QdrantValue { StringValue = image.ObjectKey });
            await _qdrantClient.UpsertAsync(_collection, new[] { point });
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
