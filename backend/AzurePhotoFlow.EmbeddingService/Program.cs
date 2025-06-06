using AzurePhotoFlow.Services;
using Api.Interfaces;
using Qdrant.Client;
using Microsoft.ML.OnnxRuntime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddControllers();


builder.Services.AddSingleton(_ =>
{
    var url = Environment.GetEnvironmentVariable("QDRANT_URL") ?? "http://localhost:6333";
    return new QdrantClient(url);
});
builder.Services.AddSingleton<IQdrantClientWrapper, QdrantClientWrapper>();
builder.Services.AddSingleton<IImageEmbeddingModel>(_ =>
{
    string modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "model.onnx";
    var session = new InferenceSession(modelPath);
    return new OnnxImageEmbeddingModel(session);
});
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

var app = builder.Build();

app.MapControllers();

app.Run();
