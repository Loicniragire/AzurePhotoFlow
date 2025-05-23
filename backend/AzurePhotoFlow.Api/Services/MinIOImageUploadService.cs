using System.Globalization;
using Api.Interfaces;
using Api.Models;
using System.IO.Compression;
using Minio;
using Minio.DataModel.Args;
using Newtonsoft.Json;
using AzurePhotoFlow.POCO.QueueModels;
using System.Text.RegularExpressions;

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
			if(!MinIODirectoryHelper.IsValidBucketName(BucketName))
				throw new InvalidOperationException(
					$"Invalid bucket name '{BucketName}'. " +
					$"Bucket names must be lowercase and match the regex: {MinIODirectoryHelper.IsValidBucketName(BucketName)}");

            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(BucketName));
        }

        string destinationPath = MinIODirectoryHelper.GetDestinationPath(
            timestamp, projectName,
            isRawFiles ? directoryName : rawfileDirectoryName,
            isRawFiles);

        // If we're uploading *processed* files, verify that raw files are present first.
        if (!isRawFiles)
        {
            string rawPath = MinIODirectoryHelper.GetDestinationPath(
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

                if (!MinIODirectoryHelper.IsDirectDescendant(entry, directoryName) || !MinIODirectoryHelper.IsImageFile(entry.Name))
                {
                    _log.LogInformation("Skipped: {Entry}", entry.FullName);
                    continue;
                }

                totalCount++;

                // Build “object key” → bucket/folder structure is defined purely by slashes.
                string relPath = MinIODirectoryHelper.GetRelativePath(entry.FullName, directoryName);
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
                                     .WithContentType(MinIODirectoryHelper.GetMimeType(entry.Name));

					// Log putArgs for debugging
					_log.LogDebug("PutArgs: {PutArgs}", putArgs);

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
}

