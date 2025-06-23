using Minio;
using Qdrant.Client;
using Api.Interfaces;
using AzurePhotoFlow.Api.Data;
using AzurePhotoFlow.Api.Interfaces;
using AzurePhotoFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

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

            // Remove http:// prefix if present, as MinIO client expects just hostname:port
            var cleanEndpoint = endpoint.Replace("http://", "").Replace("https://", "");

            return new MinioClient()
                .WithEndpoint(cleanEndpoint)
                .WithCredentials(accessKey, secretKey)
                .Build();
        });

        return services;
    }

    public static IServiceCollection AddVectorStore(this IServiceCollection services)
    {
        services.AddHttpClient<IQdrantClientWrapper, QdrantClientWrapper>();
        services.AddSingleton(provider =>
        {
            var host = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
            var portEnv = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6333";
            var port = int.TryParse(portEnv, out var p) ? p : 6333;
            return new QdrantClient(host, port);
        });
        services.AddScoped<IVectorStore, QdrantVectorStore>();

        return services;
    }

    public static IServiceCollection AddPhotoFlowDatabase(this IServiceCollection services)
    {
        services.AddDbContext<PhotoFlowDbContext>(options =>
        {
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") 
                ?? "Data Source=photoflow.db;Cache=Shared";
            
            options.UseSqlite(connectionString);
            
            // Enable sensitive data logging in development
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Register repository
        services.AddScoped<IImageMappingRepository, ImageMappingRepository>();

        return services;
    }
}
