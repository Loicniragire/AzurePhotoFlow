using Api.Models;
using Api.Interfaces;
using Azure.Storage.Blobs;

public class ImageUploadService : IImageUploadService
{
	private const string ContainerName = "images";
    private readonly BlobServiceClient _blobServiceClient;

    public ImageUploadService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<ImageMetadata> UploadImageAsync(IFormFile file)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(file.FileName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, true);

        return new ImageMetadata
        {
            FileName = file.FileName,
            Url = blobClient.Uri.ToString(),
            UploadedAt = DateTime.UtcNow
        };
    }
}

