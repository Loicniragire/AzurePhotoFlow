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

    public async Task<string> UploadFileWithPathAsync(IFormFile file, string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, true);

        return blobClient.Uri.ToString(); // Return the URL of the uploaded blob
    }

    public async Task<List<string>> ExtractAndUploadImagesAsync(IFormFile directoryFile, string directoryName)
    {
        var uploadResults = new List<string>();

        using var zipStream = directoryFile.OpenReadStream();
        using var archive = new System.IO.Compression.ZipArchive(zipStream);

        foreach (var entry in archive.Entries)
        {
            // Check if the entry is a valid image file
            if (!string.IsNullOrWhiteSpace(entry.Name) && IsImageFile(entry.FullName))
            {
                var blobPath = $"original/{directoryName}/{entry.Name}";

                // Open the entry stream and upload it
                using var entryStream = entry.Open();

                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobClient = containerClient.GetBlobClient(blobPath);

                await blobClient.UploadAsync(entryStream, overwrite: true);

                // Add the blob URL to the results
                uploadResults.Add(blobClient.Uri.ToString());
            }
        }

        return uploadResults;
    }

    private bool IsImageFile(string fileName)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
        var fileExtension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return allowedExtensions.Contains(fileExtension);
    }

}

