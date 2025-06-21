namespace AzurePhotoFlow.Services;

public static class CorsConfigHelper
{
    public static string[] GetAllowedOrigins()
    {
        var origins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
        if (string.IsNullOrWhiteSpace(origins))
        {
            // Default development origins for frontend
            return new[] { 
                "http://localhost:3000",
                "http://localhost:5173", 
                "http://localhost:8080",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:5173",
                "http://127.0.0.1:8080",
                "https://localhost:3000",
                "https://localhost:5173",
                "https://localhost:8080",
                "https://localhost:8443"
            };
        }
        return origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
