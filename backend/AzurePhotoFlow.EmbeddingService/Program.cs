using AzurePhotoFlow.Services;
using Api.Interfaces;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddControllers();


builder.Services.AddSingleton(_ =>
{
    var url = Environment.GetEnvironmentVariable("QDRANT_URL") ?? "http://localhost:6333";
    return new QdrantClient(url);
});

builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

var app = builder.Build();

app.MapControllers();

app.Run();
