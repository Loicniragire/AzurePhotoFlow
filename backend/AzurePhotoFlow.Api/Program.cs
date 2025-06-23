using System.Text;
using System.Text.Json;
using Api.Interfaces;
using Api.Models;
using AzurePhotoFlow.Api.Data;
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
    options.Limits.MaxRequestBodySize = UploadConfigHelper.GetMultipartBodyLengthLimit();
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
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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
                status = StatusCodes.Status400BadRequest,
                message = "Validation errors occurred",
                errors
            });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "AzurePhotoFlow API", 
        Version = "v1",
        Description = "AI-powered photo management API with semantic search, face recognition, and automated organization",
        Contact = new OpenApiContact
        {
            Name = "AzurePhotoFlow",
            Url = new Uri("https://github.com/loicniragire/AzurePhotoFlow")
        }
    });
    c.OperationFilter<SwaggerFileOperationFilter>();
    c.EnableAnnotations();
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
    c.MapType<DateTime>(() => new OpenApiSchema { Type = "string", Format = "date-time" });
    
    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Configure Structured Logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.JsonWriterOptions = new JsonWriterOptions { Indented = true };
});

builder.Services.AddMinioClient();
builder.Services.AddVectorStore();
builder.Services.AddPhotoFlowDatabase();

builder.Services.AddSingleton<InferenceSession>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    string modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "clip_vision_traced.pt";
    logger.LogInformation("[MODEL DEBUG] Loading CLIP vision model from: {ModelPath}", modelPath);

	// InferenceSession: A core class provided by the ONNX Runtime that encapsulates a model and provides
	// methods for executing inference. It loads an ONNX model and prepares it for efficient execution
	// using available hardware.
    var session = new InferenceSession(modelPath);
    logger.LogInformation("[MODEL DEBUG] CLIP vision model loaded successfully");
    return session;
});

builder.Services.AddScoped<IMetadataExtractorService, MetadataExtractorService>();
builder.Services.AddScoped<IImageUploadService, MinIOImageUploadService>();
builder.Services.AddSingleton<IQdrantClientWrapper, QdrantClientWrapper>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<IImageEmbeddingModel>(sp =>
{
    var visionSession = sp.GetRequiredService<InferenceSession>();
    
    // Try to load text model if available
    InferenceSession textSession = null;
    Dictionary<string, object> tokenizer = null;
    
    try
    {
        var clipModelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "/models/model.onnx";
        var modelsDir = Path.GetDirectoryName(clipModelPath);
        var textModelPath = Path.Combine(modelsDir, "text_model.onnx");
        var tokenizerPath = Path.Combine(modelsDir, "tokenizer");
        
        if (File.Exists(textModelPath))
        {
            var programLogger = sp.GetRequiredService<ILogger<Program>>();
            programLogger.LogInformation("[MODEL DEBUG] Loading CLIP text model from: {TextModelPath}", textModelPath);
            textSession = new InferenceSession(textModelPath);
            programLogger.LogInformation("[MODEL DEBUG] CLIP text model loaded successfully");
            
            // Load tokenizer if available (for future use)
            if (Directory.Exists(tokenizerPath))
            {
                programLogger.LogInformation("[MODEL DEBUG] Tokenizer directory found: {TokenizerPath}", tokenizerPath);
                tokenizer = new Dictionary<string, object>();
            }
            else
            {
                programLogger.LogInformation("[MODEL DEBUG] Tokenizer directory not found at: {TokenizerPath}", tokenizerPath);
            }
        }
        else
        {
            var programLogger = sp.GetRequiredService<ILogger<Program>>();
            programLogger.LogInformation("[MODEL DEBUG] Text model not found at: {TextModelPath}, using fallback text embeddings", textModelPath);
        }
    }
    catch (Exception ex)
    {
        var programLogger = sp.GetRequiredService<ILogger<Program>>();
        programLogger.LogWarning("Failed to load text model: {ErrorMessage}, using fallback text embeddings", ex.Message);
    }
    
    var logger = sp.GetRequiredService<ILogger<OnnxImageEmbeddingModel>>();
    return new OnnxImageEmbeddingModel(visionSession, textSession, tokenizer, logger);
});
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = UploadConfigHelper.GetMultipartBodyLengthLimit();
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
    app.UseDeveloperExceptionPage();
}

// Middleware Pipeline
app.UseRouting();
app.UseCors("AllowSpecificOrigin");

// Enable Swagger in all environments for API testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AzurePhotoFlow API v1");
    c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
});

app.UseGlobalExceptionHandling(app.Environment);

// Activate Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint();

app.MapControllers();

// Initialize Database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PhotoFlowDbContext>();
    context.Database.EnsureCreated();
}

// Graceful Shutdown Handling
app.UseShutdownLogging();

app.Run();
