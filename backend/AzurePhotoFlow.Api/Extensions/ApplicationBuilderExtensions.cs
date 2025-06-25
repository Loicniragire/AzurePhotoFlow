using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Api.Interfaces;
using AzurePhotoFlow.Api.Services;

namespace AzurePhotoFlow.Services;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseHostHeaderLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var hostHeader = context.Request.Headers["Host"].ToString();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Incoming request Host header: {HostHeader}", hostHeader);
            await next.Invoke();
        });
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            await next();
        });
    }

    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        return app.UseExceptionHandler(appError =>
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
                    Details = env.IsDevelopment() ? exception?.ToString() : null
                }));
            });
        });
    }

    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                // Add vector store count debugging
                long vectorStoreCount = 0;
                try
                {
                    var vectorStore = context.RequestServices.GetService<IVectorStore>();
                    if (vectorStore != null)
                    {
                        vectorStoreCount = await vectorStore.GetTotalCountAsync();
                    }
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Failed to get vector store count in health check");
                }

                // Add tokenizer health check
                TokenizerHealthResult? tokenizerHealth = null;
                try
                {
                    var tokenizerHealthService = context.RequestServices.GetService<TokenizerHealthService>();
                    if (tokenizerHealthService != null)
                    {
                        tokenizerHealth = tokenizerHealthService.CheckTokenizerHealth();
                    }
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Failed to check tokenizer health");
                }

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
                    Duration = report.TotalDuration,
                    VectorStoreCount = vectorStoreCount,
                    TokenizerHealth = tokenizerHealth != null ? new
                    {
                        IsHealthy = tokenizerHealth.IsHealthy,
                        Issues = tokenizerHealth.Issues,
                        VocabularySize = tokenizerHealth.VocabularySize,
                        Files = tokenizerHealth.FileValidations.Select(f => new
                        {
                            FileName = f.FileName,
                            IsValid = f.IsValid,
                            ErrorMessage = f.ErrorMessage,
                            FileSizeBytes = f.FileSizeBytes,
                            LastModified = f.LastModified
                        }),
                        CheckedAt = tokenizerHealth.CheckedAt
                    } : null
                });

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(result);
            }
        });

        return endpoints;
    }

    public static IApplicationBuilder UseShutdownLogging(this IApplicationBuilder app)
    {
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Application is shutting down...");
        });

        return app;
    }
}
