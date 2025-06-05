using AzurePhotoFlow.Services;
using Api.Interfaces;
using Minio;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddControllers();

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "minio";
    var accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? "minioadmin";
    var secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? "minioadmin";
    return new MinioClient().WithEndpoint(endpoint).WithCredentials(accessKey, secretKey).Build();
});

builder.Services.AddSingleton(_ =>
{
    var url = Environment.GetEnvironmentVariable("QDRANT_URL") ?? "http://localhost:6333";
    return new QdrantClient(url);
});

builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

var app = builder.Build();

app.MapControllers();

app.Run();
