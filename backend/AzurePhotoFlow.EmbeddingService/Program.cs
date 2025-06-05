using AzurePhotoFlow.Services;
using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

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

builder.Services.AddSingleton<IEmbeddingGeneratorService, EmbeddingGeneratorService>();

var app = builder.Build();

app.MapPost("/generate", async ([FromBody]EmbeddingRequest req, IEmbeddingGeneratorService generator) =>
{
    await generator.GenerateAsync(req.ProjectName, req.DirectoryName, req.Timestamp);
    return Results.Ok();
});

app.Run();

public record EmbeddingRequest(string ProjectName, string DirectoryName, DateTime Timestamp);
