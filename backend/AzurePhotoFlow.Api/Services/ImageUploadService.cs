using System.IO.Compression;
using Api.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class ImageUploadService : IImageUploadService
{
    private const string ContainerName = "images";
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ImageUploadService> _log;

    public ImageUploadService(ILogger<ImageUploadService> logger, BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
        _log = logger;
    }

    public async Task<string> UploadFileWithPathAsync(IFormFile file, string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, true);

        return blobClient.Uri.ToString(); // Return the URL of the uploaded blob
    }

    public async Task Delete(string projectName, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name cannot be null or empty", nameof(projectName));

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobs = containerClient.GetBlobsByHierarchy(prefix: $"{timestamp:yyyy-MM-dd}/{projectName}");

        // Convert timestamp to the expected folder format
        string timestampFolder = $"{timestamp.Year}/{timestamp:yyyy-MM-dd}";

        // Define the blob prefix to search for
        string blobPrefix = $"{timestampFolder}/{projectName}";

        // List all blobs with the specified prefix
        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: blobPrefix))
        {
            BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
            await blobClient.DeleteIfExistsAsync();
        }
    }

    public async Task<List<string>> ExtractAndUploadImagesAsync(
        IFormFile directoryFile,
        string projectName,
        string directoryName,
        DateTime timestamp,
        bool isRawFiles = true,
		string rawfileDirectoryName = "")
    {
        var uploadResults = new List<string>();
        var destinationPath = GetDestinationPath(timestamp, projectName, isRawFiles?directoryName:rawfileDirectoryName, isRawFiles);

        // If uploading processed files, check that the corresponding raw files path exists
        if (!isRawFiles)
        {
            var rawFilesPath = GetDestinationPath(timestamp, projectName, rawfileDirectoryName, isRawFiles);
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);

            // Ensure the raw files path exists
            var rawFilesExist = await DoesPathExistAsync(containerClient, rawFilesPath, "ProcessedFiles");
            if (!rawFilesExist)
            {
                throw new InvalidOperationException($"Raw files path '{rawFilesPath}' does not exist. Processed files cannot be uploaded without corresponding raw files.");
            }
        }

        using var zipStream = directoryFile.OpenReadStream();
        using var archive = new System.IO.Compression.ZipArchive(zipStream);

        foreach (var entry in archive.Entries)
        {
            try
            {
                // Log the entry being processed
                _log.LogInformation($"Processing entry: {entry.FullName}");

                // Validate entry against the parent directory
                if (!IsDirectDescendant(entry, directoryName))
                {
                    _log.LogInformation($"Skipped: {entry.FullName} (Not a direct descendant of '{directoryName}')");
                    continue; // Skip invalid entries
                }

                // Check if the entry is a valid image file
                if (!IsImageFile(entry.Name))
                {
                    _log.LogInformation($"Skipped: {entry.FullName} (Invalid image file)");
                    continue;
                }

                // Construct blob path
                var relativePath = GetRelativePath(entry.FullName, directoryName);
                var blobPath = $"{destinationPath}/{relativePath}";

                using var entryStream = entry.Open();

                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobClient = containerClient.GetBlobClient(blobPath);

                await blobClient.UploadAsync(entryStream, overwrite: true);

                // Add the blob URL to the results
                uploadResults.Add(blobClient.Uri.ToString());

                _log.LogInformation($"Uploaded: {blobPath}");
            }
            catch (Exception ex)
            {
                _log.LogError($"Failed to process entry: {entry.FullName}. Error: {ex.Message}");
            }
        }

        return uploadResults;
    }

    private bool IsDirectDescendant(ZipArchiveEntry entry, string directoryName)
    {
        // Ensure the entry starts with the directoryName
        if (!entry.FullName.StartsWith($"{directoryName}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Ensure the file is a direct child (no additional slashes after directoryName/)
        var relativePath = entry.FullName.Substring(directoryName.Length + 1); // Skip the directoryName and slash
        return !relativePath.Contains('/');
    }

    private bool IsRootFile(ZipArchiveEntry entry)
    {
        // Root files should have no directory prefix in FullName
        return entry.FullName == entry.Name;
    }

    private string GetRelativePath(string fullName, string directoryName)
    {
        // Remove the directoryName prefix and the trailing slash
        return fullName.Substring(directoryName.Length + 1);
    }

    private bool IsValidEntry(ZipArchiveEntry entry, string directoryName)
    {
        // Skip directories or empty entries
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            _log.LogInformation($"Skipped: {entry.FullName} (Empty name or directory)");
            return false;
        }

        // Exclude system-generated directories like "__MACOSX"
        if (entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.Contains("/__MACOSX/", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogInformation($"Skipped: {entry.FullName} (System-generated metadata)");
            return false;
        }

        // Exclude files not in the specified directory
        if (!entry.FullName.StartsWith($"{directoryName}/", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogInformation($"Skipped: {entry.FullName} (Does not belong to directory '{directoryName}')");
            return false;
        }

        // Skip invalid image files
        if (!IsImageFile(entry.Name))
        {
            _log.LogInformation($"Skipped: {entry.FullName} (Invalid image file)");
            return false;
        }

        return true; // Valid entry
    }

    private async Task<bool> DoesPathExistAsync(BlobContainerClient containerClient, string pathPrefix, string removeSuffixDirectory = "")
    {
		// if remoceSuffixDirectory is not empty, remove the last directory from the pathPrefix
		if (!string.IsNullOrEmpty(removeSuffixDirectory))
		{
			pathPrefix = pathPrefix.Substring(0, pathPrefix.LastIndexOf(removeSuffixDirectory));
		}
        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: pathPrefix))
        {
            return true; // If any blob exists under the prefix, return true
        }
        return false; // No blobs found under the prefix
    }

    private string GetDestinationPath(DateTime timestamp, string projectName, string directoryName, bool isRawFiles = true)
    {
        var basePath = $"{timestamp.Year}/{timestamp:yyyy-MM-dd}/{projectName}/{directoryName}";
        return isRawFiles ? $"{basePath}/RawFiles" : $"{basePath}/ProcessedFiles";
    }

    private bool IsImageFile(string fileName)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
        var fileExtension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return allowedExtensions.Contains(fileExtension);
    }
}

