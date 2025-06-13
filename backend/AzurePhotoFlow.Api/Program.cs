using System.Text;
using System.Text.Json;
using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

builder.Services.AddMinioClient();
builder.Services.AddVectorStore();

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

app.UseHostHeaderLogging();
app.UseSecurityHeaders();

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

app.UseGlobalExceptionHandling(app.Environment);

// Activate Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint();

app.MapControllers();

// Graceful Shutdown Handling

app.UseShutdownLogging();

app.Run();
