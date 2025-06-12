using System.Text;
using System.Text.Json;
using Api.Interfaces;
using Api.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using AzurePhotoFlow.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Qdrant.Client;
using Microsoft.ML.OnnxRuntime;

var builder = WebApplication.CreateBuilder(args);
// Load Environment Variables
DotNetEnv.Env.Load();
// Configure Kestrel to listen on port 80
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
});

// Configure Services
builder.Services.AddHealthChecks();
var allowedOrigins = CorsConfigHelper.GetAllowedOrigins();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policyBuilder =>
    {
        policyBuilder.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.WriteIndented = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return new BadRequestObjectResult(new
            {
                Status = StatusCodes.Status400BadRequest,
                Message = "Validation errors occurred",
                Errors = errors
            });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AzurePhotoFlow API", Version = "v1" });
    c.OperationFilter<SwaggerFileOperationFilter>();
    c.EnableAnnotations();
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
    c.MapType<DateTime>(() => new OpenApiSchema { Type = "string", Format = "date-time" });
});

// Configure Structured Logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.JsonWriterOptions = new JsonWriterOptions { Indented = true };
});

// Configure MinIO Client
builder.Services.AddSingleton(x =>
{
        var minioEndpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
        var minioAccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY");
        var minioSecretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY");

	if (string.IsNullOrEmpty(minioEndpoint) || string.IsNullOrEmpty(minioAccessKey) || string.IsNullOrEmpty(minioSecretKey))
	{
		throw new InvalidOperationException("MinIO configuration is missing.");
	}

	return new MinioClient()
	.WithEndpoint(minioEndpoint)
	.WithCredentials(minioAccessKey, minioSecretKey)
        .Build();
});

builder.Services.AddSingleton(_ =>
{
    var qdrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
    var qdrantPort = int.Parse(Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6333");
    
    return new QdrantClient(qdrantHost, qdrantPort, https: false);
});

builder.Services.AddSingleton(_ =>
{
    string modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "clip_vision_traced.pt";

	// InferenceSession: A core class provided by the ONNX Runtime that encapsulates a model and provides
	// methods for executing inference. It loads an ONNX model and prepares it for efficient execution
	// using available hardware.
    return new InferenceSession(modelPath);
});

builder.Services.AddScoped<IMetadataExtractorService, MetadataExtractorService>();
builder.Services.AddScoped<IImageUploadService, MinIOImageUploadService>();
builder.Services.AddSingleton<IQdrantClientWrapper, QdrantClientWrapper>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<IImageEmbeddingModel>(sp =>
{
    var session = sp.GetRequiredService<InferenceSession>();
    return new OnnxImageEmbeddingModel(session);
});
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600; // 100MB
});

// Retrieve and Validate Environment Variables
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
var googleClientId = Environment.GetEnvironmentVariable("VITE_GOOGLE_CLIENT_ID");

if (string.IsNullOrEmpty(jwtSecretKey))
{
    throw new Exception("JWT_SECRET_KEY is not set! Add it as an environment variable.");
}

if (string.IsNullOrEmpty(googleClientId))
{
    throw new Exception("VITE_GOOGLE_CLIENT_ID is not set! Add it as an environment variable.");
}
var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey));

builder.Services.AddSingleton(securityKey);
builder.Services.AddSingleton(new GoogleConfig { ClientId = googleClientId });
builder.Services.AddSingleton<JwtService>();

// Configure JWT Bearer authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Remove or comment out the Google authority since you are not validating Google-issued tokens.
        // options.Authority = "https://accounts.google.com";

        // Set the expected audience and issuer to match the values in your generated JWT.
        options.Audience = "loicportraits.azurewebsites.net";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "loicportraits.azurewebsites.net",
            ValidateAudience = true,
            ValidAudience = "loicportraits.azurewebsites.net",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Use the same secret key that you use to generate your JWT.
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? string.Empty))
        };

        // Optional: Add detailed logging to help diagnose authentication failures.
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "JWT Authentication failed.");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Temporary Logging Middleware to Capture the Host Header
app.Use(async (context, next) =>
{
    var hostHeader = context.Request.Headers["Host"].ToString();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Incoming request Host header: {HostHeader}", hostHeader);
    await next.Invoke();
});

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// Development Configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

// Middleware Pipeline
app.UseRouting();
app.UseCors("AllowSpecificOrigin");

app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception?.Message);

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An unexpected error occurred. Please try again later.",
            Details = app.Environment.IsDevelopment() ? exception?.ToString() : null
        }));
    });
});

// Activate Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var result = JsonSerializer.Serialize(new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Exception = e.Value.Exception?.Message
            }),
            Duration = report.TotalDuration
        });

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(result);
    }
});

app.MapControllers();

// Graceful Shutdown Handling
var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
appLifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application is shutting down...");
    // Add any necessary cleanup logic here
});

if(app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
else
{
	app.UseExceptionHandler("/error");
}

app.Run();
