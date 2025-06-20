using Microsoft.AspNetCore.Http.Features;

namespace AzurePhotoFlow.Services;

public static class UploadConfigHelper
{
    private const long DefaultLimit = 104_857_600; // 100MB

    public static long GetMultipartBodyLengthLimit()
    {
        var env = Environment.GetEnvironmentVariable("MAX_UPLOAD_SIZE_MB");
        if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var mb) && mb > 0)
        {
            return mb * 1024L * 1024L;
        }
        return DefaultLimit;
    }
}
