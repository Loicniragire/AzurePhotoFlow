using AzurePhotoFlow.Services;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

namespace UnitTests;

[TestFixture]
public class OnnxImageEmbeddingModelTests
{
    private class FakeCollection : List<DisposableNamedOnnxValue>, IDisposableReadOnlyCollection<DisposableNamedOnnxValue>
    {
        public FakeCollection(IEnumerable<DisposableNamedOnnxValue> items) : base(items) {}
        public void Dispose()
        {
            foreach(var item in this) item.Dispose();
        }
    }

    private class FakeSession : IOnnxSession
    {
        public IReadOnlyCollection<NamedOnnxValue>? Inputs { get; private set; }
        public IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(IEnumerable<NamedOnnxValue> inputs)
        {
            Inputs = inputs.ToList();
            return new FakeCollection(Array.Empty<DisposableNamedOnnxValue>());
        }
    }

    [Test]
    public void GenerateEmbedding_ConvertsImageToTensor()
    {
        var session = new FakeSession();
        var model = new OnnxImageEmbeddingModel(session);

        using var image = new Image<Rgb24>(1,1);
        image[0,0] = new Rgb24(255,0,0);
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        var bytes = ms.ToArray();

        model.GenerateEmbedding(bytes);

        Assert.NotNull(session.Inputs);
        var input = session.Inputs!.First();
        var tensor = input.AsTensor<float>();
        Assert.AreEqual(new[]{1,3,224,224}, tensor.Dimensions.ToArray());
        Assert.AreEqual(1f, tensor[0,0,0,0], 1e-6);
        Assert.AreEqual(0f, tensor[0,1,0,0], 1e-6);
        Assert.AreEqual(0f, tensor[0,2,0,0], 1e-6);
    }
}
