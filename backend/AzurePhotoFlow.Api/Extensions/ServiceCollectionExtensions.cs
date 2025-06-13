using Microsoft.Extensions.DependencyInjection;
using Minio;
using Qdrant.Client;
using Api.Interfaces;
using AzurePhotoFlow.Services;

namespace AzurePhotoFlow.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMinioClient(this IServiceCollection services)
    {
        services.AddSingleton<IMinioClient>(_ =>
        {
            var endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
            var accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY");
            var secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY");

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("MinIO configuration is missing.");
            }

            return new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .Build();
        });

        return services;
    }

    public static IServiceCollection AddVectorStore(this IServiceCollection services)
    {
        services.AddSingleton(_ =>
        {
            var host = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
            var port = int.Parse(Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6333");
            return new QdrantClient(host, port, https: false);
        });

        services.AddSingleton<IQdrantClientWrapper, QdrantClientWrapper>();
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        return services;
    }
}
