namespace AzurePhotoFlow.Services;

public static class CorsConfigHelper
{
    public static string[] GetAllowedOrigins()
    {
        var origins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
        if (string.IsNullOrWhiteSpace(origins))
        {
            return new[] { "http://localhost" };
        }
        return origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
