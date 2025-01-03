using System.IO.Compression;
using Api.Interfaces;
using Api.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Globalization;

public class ImageUploadService : IImageUploadService
{
    private const string ContainerName = "images";
	private const string DATE_FORMAT = "yyyy-MM-dd";
	private string[] ALLOWED_EXTENSIONS = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".CR3" };

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
        var blobs = containerClient.GetBlobsByHierarchy(prefix: $"{timestamp:DATE_FORMAT}/{projectName}");

        // Convert timestamp to the expected folder format
        string timestampFolder = $"{timestamp.Year}/{timestamp:DATE_FORMAT}";

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
        var destinationPath = GetDestinationPath(timestamp, projectName, isRawFiles ? directoryName : rawfileDirectoryName, isRawFiles);

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
        var basePath = $"{timestamp.Year}/{timestamp:DATE_FORMAT}/{projectName}/{directoryName}";
        return isRawFiles ? $"{basePath}/RawFiles" : $"{basePath}/ProcessedFiles";
    }

    private bool IsImageFile(string fileName)
    {
        var fileExtension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ALLOWED_EXTENSIONS.Contains(fileExtension);
    }

    public async Task<List<ProjectInfo>> GetProjects(string? year, string? projectName, DateTime? timestamp = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var projects = new List<ProjectInfo>();

        // Start recursive processing from the root
        await ProcessHierarchy(containerClient, string.Empty, projects, year, projectName, timestamp);

        _log.LogInformation($"Found {projects.Count} projects");

        return projects;
    }

    private async Task ProcessHierarchy(
        BlobContainerClient containerClient,
        string prefix,
        List<ProjectInfo> projects,
        string year,
        string projectName,
        DateTime? timestamp)
    {
        await foreach (var blobItem in containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/"))
        {
            if (blobItem.IsPrefix)
            {
                // Extract the blob path structure: year/datestamp/projectname/
                var parts = blobItem.Prefix.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 3) // year/datestamp/projectname
                {
                    var currentYear = parts[0];
                    var currentDateStamp = parts[1];
                    var currentProjectName = parts[2];

                    _log.LogInformation($"Year: {currentYear}, DateStamp: {currentDateStamp}, ProjectName: {currentProjectName}");

                    // Filter by year
                    if (!string.IsNullOrEmpty(year) && currentYear != year)
                    {
                        _log.LogInformation($"Skipped year: retrieved:{currentYear} searching:{year}");
                        continue;
                    }

                    // Filter by project name
                    if (!string.IsNullOrEmpty(projectName) && !currentProjectName.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        _log.LogInformation($"Skipped: retrieved:{currentProjectName} searching:{projectName}");
                        continue;
                    }

                    // Filter by date stamp
                    if (timestamp.HasValue)
                    {
                        if (!DateTime.TryParseExact(currentDateStamp,DATE_FORMAT, null, DateTimeStyles.None, out var parsedDate) ||
                            parsedDate.Date != timestamp.Value.Date)
                        {
                            continue;
                        }
                    }

                    _log.LogInformation($"Adding project: {currentProjectName} - {currentDateStamp}");

                    // Create a ProjectInfo object
                    var projectInfo = new ProjectInfo
                    {
                        Name = currentProjectName,
                        Datestamp = DateTime.ParseExact(currentDateStamp,DATE_FORMAT, null)
                    };

                    // Add directory details
                    var projectPrefix = $"{currentYear}/{currentDateStamp}/{currentProjectName}/";
                    projectInfo.Directories = await GetDirectoryDetails(containerClient, projectPrefix);

                    projects.Add(projectInfo);
                    _log.LogInformation($"Added project: {currentProjectName} - {currentDateStamp}");
                }
                else
                {
                    // Drill down further into the hierarchy
                    await ProcessHierarchy(containerClient, blobItem.Prefix, projects, year, projectName, timestamp);
                }
            }
        }
    }

    private async Task<List<ProjectDirectory>> GetDirectoryDetails(BlobContainerClient containerClient, string projectPrefix)
    {
        var directories = new List<ProjectDirectory>();

        await foreach (var blobItem in containerClient.GetBlobsByHierarchyAsync(prefix: projectPrefix, delimiter: "/"))
        {
            if (blobItem.IsPrefix)
            {
                var directoryName = blobItem.Prefix.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                var rawFilesCount = await CountFiles(containerClient, $"{blobItem.Prefix}RawFiles/");
                var processedFilesCount = await CountFiles(containerClient, $"{blobItem.Prefix}ProcessedFiles/");

                directories.Add(new ProjectDirectory
                {
                    Name = directoryName,
                    RawFilesCount = rawFilesCount,
                    ProcessedFilesCount = processedFilesCount
                });
            }
        }

        return directories;
    }

    private async Task<int> CountFiles(BlobContainerClient containerClient, string prefix)
    {
        var count = 0;

        // Use GetBlobsByHierarchyAsync to traverse and count files
        await foreach (var blobItem in containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/"))
        {
            if (!blobItem.IsPrefix) // Count only files, not prefixes
            {
                count++;
            }
        }

        return count;
    }



}

