using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace AzurePhotoFlow.Services;

public class OnnxImageEmbeddingModel : IImageEmbeddingModel
{
    private readonly IOnnxSession _session;

    public OnnxImageEmbeddingModel(IOnnxSession session)
    {
        _session = session;
    }

    public float[] GenerateEmbedding(byte[] imageBytes)
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
        var first = results.FirstOrDefault();
        return first != null ? first.AsEnumerable<float>().ToArray() : Array.Empty<float>();
    }
}
