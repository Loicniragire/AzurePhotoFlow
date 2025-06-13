using AzurePhotoFlow.Services;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using NUnit.Framework;
using Qdrant.Client;
using Api.Interfaces;

namespace unitTests;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [SetUp]
    public void Setup()
    {
        Environment.SetEnvironmentVariable("MINIO_ENDPOINT", "localhost:9000");
        Environment.SetEnvironmentVariable("MINIO_ACCESS_KEY", "access");
        Environment.SetEnvironmentVariable("MINIO_SECRET_KEY", "secret");
        Environment.SetEnvironmentVariable("QDRANT_HOST", "localhost");
        Environment.SetEnvironmentVariable("QDRANT_PORT", "6333");
    }

    [Test]
    public void AddMinioClient_RegistersSingleton()
    {
        var services = new ServiceCollection();
        services.AddMinioClient();
        var provider = services.BuildServiceProvider();

        var client1 = provider.GetRequiredService<IMinioClient>();
        var client2 = provider.GetRequiredService<IMinioClient>();

        Assert.IsInstanceOf<MinioClient>(client1);
        Assert.AreSame(client1, client2);
    }

    [Test]
    public void AddVectorStore_RegistersDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVectorStore();
        var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IVectorStore>();
        var wrapper = provider.GetRequiredService<IQdrantClientWrapper>();
        var qdrant = provider.GetRequiredService<QdrantClient>();

        Assert.IsInstanceOf<QdrantVectorStore>(store);
        Assert.IsInstanceOf<QdrantClientWrapper>(wrapper);
        Assert.IsInstanceOf<QdrantClient>(qdrant);
    }
}
