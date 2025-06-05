using Api.Interfaces;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Qdrant.Client;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AzurePhotoFlow.Services;

public class EmbeddingGeneratorService : IEmbeddingGeneratorService
{
    private const string BucketName = "photostore";
    private readonly IMinioClient _minioClient;
    private readonly QdrantClient _qdrantClient;
    private readonly ILogger<EmbeddingGeneratorService> _logger;
    private readonly InferenceSession _session;
    private readonly string _collection;

    public EmbeddingGeneratorService(IMinioClient minioClient, QdrantClient qdrantClient, ILogger<EmbeddingGeneratorService> logger)
    {
        _minioClient = minioClient;
        _qdrantClient = qdrantClient;
        _logger = logger;
        _collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "images";
        string modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "model.onnx";
        _session = new InferenceSession(modelPath);
    }

    public async Task GenerateAsync(string projectName, string directoryName, DateTime timestamp)
    {
        string prefix = MinIODirectoryHelper.GetDestinationPath(timestamp, projectName, directoryName, true) + "/";
        var listArgs = new ListObjectsArgs().WithBucket(BucketName).WithPrefix(prefix).WithRecursive(true);

        await foreach (var item in _minioClient.ListObjectsEnumAsync(listArgs))
        {
            if (item.IsDir) continue;

            using var ms = new MemoryStream();
            await _minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(BucketName).WithObject(item.Key).WithCallbackStream(s => s.CopyTo(ms)));
            ms.Position = 0;
            float[] vector = GenerateEmbedding(ms.ToArray());

            var point = new Qdrant.Client.PointStruct
            {
                Id = item.Key,
                Vectors = vector,
                Payload = new Dictionary<string, object> { ["path"] = item.Key }
            };
            await _qdrantClient.UpsertAsync(_collection, new[] { point });
        }
    }

    private float[] GenerateEmbedding(byte[] imageBytes)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        image.Mutate(x => x.Resize(224, 224));
        var tensor = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
        for (int y = 0; y < 224; y++)
        {
            var span = image.GetPixelRowSpan(y);
            for (int x = 0; x < 224; x++)
            {
                var p = span[x];
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
