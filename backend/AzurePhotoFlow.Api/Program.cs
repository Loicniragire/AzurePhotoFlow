using Api.Interfaces;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load Environment Variables
DotNetEnv.Env.Load();

// Configure Kestrel for HTTP and HTTPS
builder.WebHost.ConfigureKestrel(options =>
{

    string certDirectory = Directory.GetCurrentDirectory();

    // Check if running in Docker
    bool isInDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    // Determine certificate path
    string certFile = isInDocker
        ? Environment.GetEnvironmentVariable("CERTIFICATE_PATH")
		:Path.Combine(certDirectory, "certs", "https", "aspnetapp.pfx");
    if (!File.Exists(certFile))
    {
        throw new FileNotFoundException("Certificate file not found", certFile);
    }

    options.ListenAnyIP(80); // HTTP
    options.ListenAnyIP(443, listenOptions =>
    {
        string certPassword = Environment.GetEnvironmentVariable("CERTIFICATE_PASSWORD");
		Console.WriteLine($"Certificate Password: {certPassword}");
        listenOptions.UseHttps(certFile, certPassword);
    }); // HTTPS
});


var azureBlobStorageConnectionString = builder.Configuration.GetConnectionString("AzureBlobStorage")
                                   ?? Environment.GetEnvironmentVariable("AZURE_BLOB_STORAGE");

if (string.IsNullOrEmpty(azureBlobStorageConnectionString))
{
    Console.WriteLine("Azure Blob Storage Connection String is not set.");
    throw new InvalidOperationException("Azure Blob Storage Connection String is missing.");
}

Console.WriteLine($"Azure Blob Storage Connection String: {azureBlobStorageConnectionString}");

// Add CORS 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", builder =>
    {
        builder.WithOrigins(
				"http://localhost:3000", // local development
				"https://azurephotoflowfe.azurewebsites.net" // production
				) 
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials(); // Required for cookies/auth headers
    });
});

// Configure Services
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use PascalCase for serialization
    options.JsonSerializerOptions.WriteIndented = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AzurePhotoFlow API", Version = "v1" });

    // Add support for form-data file uploads
    c.OperationFilter<SwaggerFileOperationFilter>();

    // Enable annotation
    c.EnableAnnotations();

    // Map IFormFile explicitly for Swagger
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // Map DateTime explicitly for Swagger
    c.MapType<DateTime>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "date-time"
    });
});

// Logging Configuration
builder.Logging.ClearProviders(); // Optional: Clears default providers
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

// Add Azure Blob Storage Service
builder.Services.AddSingleton(x => new BlobServiceClient(azureBlobStorageConnectionString));

// Add custom services
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();

// Allow large file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
});

// Build the application
var app = builder.Build();

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware Pipeline
app.UseCors("AllowSpecificOrigin");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();

