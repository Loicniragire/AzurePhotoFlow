using System.Globalization;
using Api.Interfaces;
using Api.Models;
using System.IO.Compression;
using Minio;
using Minio.DataModel.Args;
using Newtonsoft.Json;
using AzurePhotoFlow.POCO.QueueModels;

namespace AzurePhotoFlow.Services;

public class MinIOImageUploadService : IImageUploadService
{
    private const string BucketName = "photo‑store";
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinIOImageUploadService> _log;
    private readonly IMetadataExtractorService _metadataExtractorService;
    /* private readonly IMessageQueueingService _messageQueueingService; */

    public MinIOImageUploadService(
        IMinioClient minioClient,
        ILogger<MinIOImageUploadService> logger,
        IMetadataExtractorService metadataExtractorService)
        /* IMessageQueueingService messageQueueingService) */
    {
        _minioClient = minioClient;
        _log = logger;
        _metadataExtractorService = metadataExtractorService;
        /* _messageQueueingService = messageQueueingService; */
    }

    public async Task<UploadResponse> ExtractAndUploadImagesAsync(
        IFormFile directoryFile,
        string projectName,
        string directoryName,
        DateTime timestamp,
        bool isRawFiles = true,
        string rawfileDirectoryName = "")
    {
        // Ensure bucket exists (idempotent and cheap).
        if (!await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(BucketName)))
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(BucketName));
        }

        string destinationPath = GetDestinationPath(
            timestamp, projectName,
            isRawFiles ? directoryName : rawfileDirectoryName,
            isRawFiles);

        // If we're uploading *processed* files, verify that raw files are present first.
        if (!isRawFiles)
        {
            string rawPath = GetDestinationPath(
                timestamp, projectName, rawfileDirectoryName, isRawFiles);
            if (!await DoesPathExistAsync(rawPath))
                throw new InvalidOperationException(
                    $"Raw files path '{rawPath}' does not exist. " +
                    $"Processed files cannot be uploaded without corresponding raw files.");
        }

        int uploadedCount = 0;
        int totalCount = 0;

        using var zipStream = directoryFile.OpenReadStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            try
            {
                _log.LogInformation("Processing entry: {Entry}", entry.FullName);

                if (!IsDirectDescendant(entry, directoryName) || !IsImageFile(entry.Name))
                {
                    _log.LogInformation("Skipped: {Entry}", entry.FullName);
                    continue;
                }

                totalCount++;

                // Build “object key” → bucket/folder structure is defined purely by slashes.
                string relPath = GetRelativePath(entry.FullName, directoryName);
                string objectKey = $"{destinationPath}/{relPath}";

                // Copy entry to temp file so we know the exact length (needed by PutObjectAsync).
                string tmp = Path.GetTempFileName();
                try
                {
                    await using (var entryStream = entry.Open())
                    await using (var tmpStream = File.Create(tmp))
                    {
                        await entryStream.CopyToAsync(tmpStream);
                    }

                    await using var uploadStream = File.OpenRead(tmp);

                    // ---- MinIO upload ----
                    var putArgs = new PutObjectArgs()
                                     .WithBucket(BucketName)
                                     .WithObject(objectKey)
                                     .WithStreamData(uploadStream)
                                     .WithObjectSize(uploadStream.Length)
                                     .WithContentType(GetMimeType(entry.Name));

                    await _minioClient.PutObjectAsync(putArgs);

                    // Grab server metadata (ETag, VersionId, LastModified).
                    var stat = await _minioClient.StatObjectAsync(
                                   new StatObjectArgs()
                                       .WithBucket(BucketName)
                                       .WithObject(objectKey));

                    // Reset stream for EXIF extraction.
                    uploadStream.Position = 0;
                    var metadata = new ImageMetadata
                    {
                        Id = stat.VersionId ?? stat.ETag,
                        BlobUri = $"s3://{BucketName}/{objectKey}",
                        UploadedBy = "Admin",
                        UploadDate = stat.LastModified,
                        CameraGeneratedMetadata =
                            _metadataExtractorService.GetCameraGeneratedMetadata(uploadStream)
                    };

                    string serialized = JsonConvert.SerializeObject(
                        metadata,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                    /* await _messageQueueingService.EnqueueMessageAsync(serialized); */

                    _log.LogInformation("Uploaded: {Key}", objectKey);
                    uploadedCount++;
                }
                finally
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed processing entry {Entry}", entry.FullName);
            }
        }

        return new UploadResponse { UploadedCount = uploadedCount, OriginalCount = totalCount };
    }

    public Task Delete(string projectName, DateTime timestamp) => throw new NotImplementedException();
    public Task<List<ProjectInfo>> GetProjects(string year, string projectName, DateTime? ts)
                                                                    => throw new NotImplementedException();


    /// <summary>Checks whether at least one object exists under <paramref name="prefix"/>.</summary>
    private async Task<bool> DoesPathExistAsync(string prefix, CancellationToken ct = default)
    {
        var listArgs = new ListObjectsArgs()
                           .WithBucket(BucketName)
                           .WithPrefix(prefix.TrimEnd('/') + '/')
                           .WithRecursive(true);

        await foreach (var _ in _minioClient.ListObjectsEnumAsync(listArgs, ct).WithCancellation(ct))
        {

            return true;
        }
        return false;
    }

    private static bool IsDirectDescendant(ZipArchiveEntry entry, string parentDir) =>
        entry.FullName.StartsWith(parentDir + "/", StringComparison.OrdinalIgnoreCase) &&
        entry.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2;

    private static bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp" or ".gif";
    }

    private static string GetMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" or ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }


    /// <summary>
    /// Removes the first path segment (<paramref name="parentDirectory"/>) from
    /// <paramref name="entryFullName"/> and returns the remainder.
    ///
    /// Examples
    /// ─────────────────────────────────────────────────────────────
    ///   parentDirectory = "CameraA"
    ///   entryFullName   = "CameraA/IMG_0001.jpg"      →  "IMG_0001.jpg"
    ///
    ///   parentDirectory = "CameraA"
    ///   entryFullName   = "CameraA/Sub/IMG_0002.jpg"  →  "Sub/IMG_0002.jpg"
    ///
    ///   parentDirectory = "CameraA"
    ///   entryFullName   = "Other/IMG_0003.jpg"        →  "Other/IMG_0003.jpg"  (no match)
    /// </summary>
    private static string GetRelativePath(string entryFullName, string parentDirectory)
    {
        if (string.IsNullOrEmpty(entryFullName) || string.IsNullOrEmpty(parentDirectory))
            return entryFullName;

        // ZipArchiveEntry.FullName always uses forward slashes, independent of OS.
        string prefix = parentDirectory.TrimEnd('/') + '/';

        return entryFullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               ? entryFullName[prefix.Length..]      // slice off the prefix
               : entryFullName;                      // parentDirectory not present – return unchanged
    }

    private static string GetDestinationPath(
        DateTime timestamp,
        string projectName,
        string directoryName,
        bool isRawFiles)
    {
        // 1)  yyyy/MM/dd   → keeps objects in date partitions for easier lifecycle rules
        // 2)  project name → one level per customer / shoot / collection
        // 3)  category     → RawFiles | ProcessedFiles
        // 4)  directory    → typically the folder inside the ZIP
        //
        // Result example:
        //   2025/05/13/WeddingSmith/RawFiles/CameraA
        //   2025/05/13/WeddingSmith/ProcessedFiles/CameraA

        string datePart = timestamp.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        string category = isRawFiles ? "RawFiles" : "ProcessedFiles";

        return $"{datePart}/{Sanitize(projectName)}/{category}/{Sanitize(directoryName)}";
    }

    /// <summary>
    /// Removes or normalises characters that are illegal or awkward in S‑3 keys,
    /// Azure blob names, or Windows paths (`\`, `..`, leading `/`, etc.).
    /// </summary>
    private static string Sanitize(string value)
    {
        return value
            .Trim()
            .Replace('\\', '/')
            .Replace("..", string.Empty)
            .Trim('/')
            .Replace("  ", " ");   // collapse double spaces, optional
    }
}
