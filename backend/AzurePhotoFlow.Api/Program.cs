using Api.Interfaces;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers(); // Add support for MVC controllers
builder.Services.AddEndpointsApiExplorer(); // Enable API explorer for Swagger or similar tools
builder.Services.AddSwaggerGen(); // Add Swagger for API documentation

// Add BlobServiceClient for Azure Blob Storage
builder.Services.AddSingleton(x =>
    new BlobServiceClient(builder.Configuration.GetConnectionString("AzureBlobStorage")));

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

