using System.Net.Http.Json;
using Api.Interfaces;

namespace AzurePhotoFlow.Services;

public class EmbeddingNotificationService : IEmbeddingNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingNotificationService> _logger;
    private readonly string _serviceUrl;

    public EmbeddingNotificationService(HttpClient httpClient,
                                         ILogger<EmbeddingNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serviceUrl = Environment.GetEnvironmentVariable("EMBEDDING_SERVICE_URL")
                     ?? throw new InvalidOperationException("EMBEDDING_SERVICE_URL is not configured.");
    }

    public async Task NotifyAsync(string projectName, string directoryName, DateTime timestamp)
    {
        var payload = new
        {
            ProjectName = projectName,
            DirectoryName = directoryName,
            Timestamp = timestamp
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_serviceUrl.TrimEnd('/')}/generate", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Embedding service responded with status {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify embedding service");
        }
    }
}
