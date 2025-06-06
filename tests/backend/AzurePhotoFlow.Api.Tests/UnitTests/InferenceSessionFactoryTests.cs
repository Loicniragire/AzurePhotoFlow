using AzurePhotoFlow.Services;
using NUnit.Framework;

namespace unitTests;

[TestFixture]
public class InferenceSessionFactoryTests
{
    [Test]
    public void Create_WithMissingModelPath_Throws()
    {
        Environment.SetEnvironmentVariable("CLIP_MODEL_PATH", null);
        Assert.Throws<InvalidOperationException>(() => InferenceSessionFactory.Create());
    }

    [Test]
    public void Create_WithNonexistentModelPath_Throws()
    {
        Environment.SetEnvironmentVariable("CLIP_MODEL_PATH", "/tmp/nonexistent.onnx");
        Assert.Throws<InvalidOperationException>(() => InferenceSessionFactory.Create());
    }
}
