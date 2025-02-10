using System.Text;
using System.Text.Json;
using Api.Interfaces;
using Api.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load Environment Variables
DotNetEnv.Env.Load();

// Configure Kestrel to listen on port 80
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
});

// Validate Blob Storage Connection Early
var azureBlobStorageConnectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_STORAGE");

if (string.IsNullOrEmpty(azureBlobStorageConnectionString))
{
    throw new InvalidOperationException("Azure Blob Storage Connection String is missing.");
}

// Configure Services
builder.Services.AddHealthChecks()
    .AddCheck("BlobStorage", () =>
    {
        try
        {
            var client = new BlobServiceClient(azureBlobStorageConnectionString);
            client.GetBlobContainers().AsPages().GetEnumerator().MoveNext();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Blob Storage connection failed", ex);
        }
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policyBuilder =>
    {
        // Adjust the allowed origin to match your frontend (e.g., including the correct port if needed)
        policyBuilder.WithOrigins("http://localhost:80")
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

// Secure Blob Client Initialization
builder.Services.AddSingleton(x =>
{
    try
    {
        return new BlobServiceClient(
            azureBlobStorageConnectionString,
            new BlobClientOptions { Retry = { MaxRetries = 3 } });
    }
    catch (Exception ex)
    {
        var logger = x.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Failed to initialize Blob Service Client");
        throw;
    }
});

builder.Services.AddScoped<IImageUploadService, ImageUploadService>();

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

// Configure Cookie Authentication Middleware
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.Cookie.HttpOnly = true;
        /* options.Cookie.SecurePolicy = CookieSecurePolicy.Always; */
		options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.LoginPath = "/api/auth/google-login";
        options.LogoutPath = "/api/auth/logout";
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                // Return a 401 Unauthorized status for API calls instead of redirecting
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
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

// Startup Validation
try
{
    using var scope = app.Services.CreateScope();
    var blobClient = scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
    blobClient.GetBlobContainers().AsPages().GetEnumerator().MoveNext();
    app.Logger.LogInformation("All critical services initialized successfully");
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Critical service initialization failed");
    throw;
}

// Graceful Shutdown Handling
var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
appLifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application is shutting down...");
    // Add any necessary cleanup logic here
});

app.Run();

