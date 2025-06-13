namespace Api.Models;

public class ImageUploadResult
{
    public string ObjectKey { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    private ImageUploadResult(string objectKey, bool success, string? error)
    {
        ObjectKey = objectKey;
        Success = success;
        ErrorMessage = error;
    }

    public static ImageUploadResult Ok(string objectKey) => new ImageUploadResult(objectKey, true, null);
    public static ImageUploadResult Fail(string objectKey, string error) => new ImageUploadResult(objectKey, false, error);
}
