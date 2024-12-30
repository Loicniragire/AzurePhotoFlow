using Api.Interfaces;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// Load Environment Variables
DotNetEnv.Env.Load();

Console.WriteLine($"Azure Blob Storage Connection String: {Environment.GetEnvironmentVariable("AZURE_BLOB_STORAGE")}");

// Configure services
builder.Services.AddControllers(); // Add support for MVC controllers
builder.Services.AddEndpointsApiExplorer(); // Enable API explorer for Swagger or similar tools
builder.Services.AddSwaggerGen(); // Add Swagger for API documentation

// Add BlobServiceClient for Azure Blob Storage
var azureBlobStorageConnectionString = builder.Configuration.GetConnectionString("AzureBlobStorage") 
                                   ?? Environment.GetEnvironmentVariable("AZURE_BLOB_STORAGE");
builder.Services.AddSingleton(x => new BlobServiceClient(azureBlobStorageConnectionString));

// Add custom services
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();

// Allow large files to be uploaded
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // Set limit to 100MB
});

var app = builder.Build();

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add middleware
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Map controllers
app.MapControllers();

app.Run();

