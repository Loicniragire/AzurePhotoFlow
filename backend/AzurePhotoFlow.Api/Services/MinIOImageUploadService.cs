using System.Globalization;
using Api.Interfaces;
using Api.Models;
using System.IO.Compression;
using Minio;
using Minio.DataModel.Args;
using Newtonsoft.Json;
using AzurePhotoFlow.POCO.QueueModels;
using AzurePhotoFlow.Shared;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Minio.DataModel;
using System.Collections.Generic; // Added for HashSet
using System.Net;
using AzurePhotoFlow.Api.Data;
using AzurePhotoFlow.Api.Interfaces;

namespace AzurePhotoFlow.Services;

public class MinIOImageUploadService : IImageUploadService
{
    private const string BucketName = "photostore";
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinIOImageUploadService> _log;
    private readonly IMetadataExtractorService _metadataExtractorService;
    private readonly IImageMappingRepository _imageMappingRepository;
    /* private readonly IMessageQueueingService _messageQueueingService; */

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MinIOImageUploadService(
        IMinioClient minioClient,
        ILogger<MinIOImageUploadService> logger,
        IMetadataExtractorService metadataExtractorService,
        IImageMappingRepository imageMappingRepository,
        IServiceScopeFactory serviceScopeFactory)
    /* IMessageQueueingService messageQueueingService) */
    {
        _minioClient = minioClient;
        _log = logger;
        _metadataExtractorService = metadataExtractorService;
        _imageMappingRepository = imageMappingRepository;
        _serviceScopeFactory = serviceScopeFactory;
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
            if (!MinIODirectoryHelper.IsValidBucketName(BucketName))
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
		_log.LogInformation("Destination path: {Path}", destinationPath);

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
        var uploadedFiles = new List<UploadedFileInfo>();

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

                string tmp = Path.GetTempFileName();
                try
                {
                    byte[] imageBytes;
                    await using (var entryStream = entry.Open())
                    {
                        using var ms = new MemoryStream();
                        await entryStream.CopyToAsync(ms);
                        imageBytes = ms.ToArray();
                    }

                    await File.WriteAllBytesAsync(tmp, imageBytes);
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
                    var cameraMetadata = _metadataExtractorService.GetCameraGeneratedMetadata(uploadStream);
                    
                    // Create ImageMapping record
                    var imageMapping = new ImageMapping
                    {
                        Id = Guid.NewGuid(),
                        ObjectKey = objectKey,
                        FileName = entry.Name,
                        ProjectName = projectName,
                        UploadDate = DateTime.UtcNow,
                        FileSize = entry.Length,
                        ContentType = MinIODirectoryHelper.GetMimeType(entry.Name),
                        DirectoryName = directoryName,
                        Year = timestamp.ToString("yyyy"),
                        Width = cameraMetadata?.ImageWidth,
                        Height = cameraMetadata?.ImageHeight,
                        MetadataJson = JsonConvert.SerializeObject(cameraMetadata, 
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                        IsActive = true
                    };

                    // Save mapping to database using a new scope to be consistent
                    using var scope = _serviceScopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IImageMappingRepository>();
                    await repository.AddAsync(imageMapping);

                    // Add to uploaded files list
                    uploadedFiles.Add(new UploadedFileInfo
                    {
                        Id = imageMapping.Id,
                        FileName = entry.Name,
                        ObjectKey = objectKey,
                        Success = true,
                        FileSize = entry.Length,
                        ContentType = MinIODirectoryHelper.GetMimeType(entry.Name),
                        Width = cameraMetadata?.ImageWidth,
                        Height = cameraMetadata?.ImageHeight
                    });

                    // Legacy metadata processing (for backward compatibility)
                    var metadata = new ImageMetadata
                    {
                        Id = stat.VersionId ?? stat.ETag,
                        BlobUri = $"s3://{BucketName}/{objectKey}",
                        UploadedBy = "Admin",
                        UploadDate = stat.LastModified,
                        CameraGeneratedMetadata = cameraMetadata
                    };

                    string serialized = JsonConvert.SerializeObject(
                        metadata,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                    /* await _messageQueueingService.EnqueueMessageAsync(serialized); */

                    _log.LogInformation("Uploaded: {Key} -> GUID: {Id}", objectKey, imageMapping.Id);
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
                
                // Add failed upload to the list
                uploadedFiles.Add(new UploadedFileInfo
                {
                    Id = Guid.Empty,
                    FileName = entry.Name,
                    ObjectKey = $"{destinationPath}/{MinIODirectoryHelper.GetRelativePath(entry.FullName, directoryName)}",
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileSize = entry.Length,
                    ContentType = MinIODirectoryHelper.GetMimeType(entry.Name)
                });
            }
        }

        return new UploadResponse 
        { 
            UploadedCount = uploadedCount, 
            OriginalCount = totalCount,
            Files = uploadedFiles
        };
    }

    public Task Delete(string projectName, DateTime timestamp) => throw new NotImplementedException();

    public async Task<List<ProjectInfo>> GetProjectsAsync(
            string? year,
            string? projectName,
            DateTime? timestamp = null,
            CancellationToken ct = default)
    {
        var projectsBag = new ConcurrentBag<ProjectInfo>();

        // Start at the “root” of the bucket (empty prefix).
		_log.LogInformation($"GetProjectsAsync() - Project name: {projectName}, Year: {year}, Timestamp: {timestamp?.ToString("yyyy-MM-dd")}");
        await ProcessHierarchyAsync(
                prefix: String.Empty,
                projects: projectsBag,
                year, projectName, timestamp,
                ct);

        var result = projectsBag.ToList();
        _log.LogInformation("Found {Count} projects", result.Count);
        return result;
    }

    private async Task ProcessHierarchyAsync(
            string prefix,
            ConcurrentBag<ProjectInfo> projects,
            string? year,
            string? projectName,
            DateTime? timestamp,
            CancellationToken ct)
    {
		/*
		   Directory structure:
			└─ 2025-01-04/                ← level 0  (date folder)
			   └─ Test project/           ← level 1  (project)
				  ├─ RawFiles/            ← level 2
				  └─ ProcessedFiles/
		 *
		 */

        // Ask MinIO for *immediate* children only (delimiter = “/”).
        _log.LogDebug($"Processing prefix: {prefix} - Bucket name: {BucketName} - projectName: {projectName}");
        var listArgs = new ListObjectsArgs()
                           .WithBucket(BucketName)
                           .WithPrefix(prefix)
                           .WithRecursive(false);



        var recursiveTasks = new List<Task>();

        // check if bucket exists
        if (!await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(BucketName)))
        {
            _log.LogWarning("Bucket {Bucket} does not exist", BucketName);
            return;
        }


        await foreach (Item item in _minioClient
                                   .ListObjectsEnumAsync(listArgs, ct)
                                   .WithCancellation(ct))
        {
            // We only care about “sub-folders” (prefixes).
            if (!item.IsDir)
                continue;

            string dirPrefix = item.Key;  // always ends with “/”

			_log.LogInformation($"Processing directory: {dirPrefix}");

            string[] parts = dirPrefix.Split(
                                '/',
                                StringSplitOptions.RemoveEmptyEntries);

			_log.LogInformation("Parts: {Parts}", string.Join(", ", parts));

            if (parts.Length == 2)
            {
                string currentDateStamp = parts[0];
                string currentProjectNameFromKey = parts[1];
				string decodedProjectNameFromKey = System.Net.WebUtility.UrlDecode(currentProjectNameFromKey);
				_log.LogInformation($"CurrentProjectNameFromKey: {currentProjectNameFromKey}, CurrentDateStamp: {currentDateStamp}");
                string currentYear = currentDateStamp[..4];

                // ---------------- filters ----------------
                if (!string.IsNullOrEmpty(year) && currentYear != year)
                    continue;

                if (!string.IsNullOrEmpty(projectName) &&
                    !decodedProjectNameFromKey.Equals(projectName,
                                              StringComparison.OrdinalIgnoreCase))
				{
					_log.LogInformation("Skipping project {Project} as it does not match the filter {Filter}",
										decodedProjectNameFromKey, projectName);

                    continue;
				}

                if (timestamp.HasValue &&
                    (!DateTime.TryParseExact(currentDateStamp, "yyyy-MM-dd",
                                             CultureInfo.InvariantCulture,
                                             DateTimeStyles.None,
                                             out var parsedDate) ||
                     parsedDate.Date != timestamp.Value.Date))
                    continue;
                // -----------------------------------------

                // Parse once for the output DTO.
                if (!DateTime.TryParseExact(currentDateStamp, "yyyy-MM-dd",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None,
                                            out var finalDate))
                {
                    _log.LogWarning("Unable to parse date: {Stamp}", currentDateStamp);
                    continue;
                }

                var projectInfo = new ProjectInfo
                {
                    Name = decodedProjectNameFromKey,
                    Datestamp = finalDate,
                    Directories = await GetDirectoryDetailsAsync(dirPrefix, ct)
                };

                projects.Add(projectInfo);
                _log.LogInformation("Added {Project} ({Stamp})",
                                    decodedProjectNameFromKey, currentDateStamp);
            }
            else
            {
                // Go one level deeper — run in parallel.
                recursiveTasks.Add(
                    ProcessHierarchyAsync(dirPrefix, projects,
                                          year, projectName, timestamp, ct));
            }
        }

        // Await children once we have queued them all.
        if (recursiveTasks.Count > 0)
            await Task.WhenAll(recursiveTasks);
    }

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

private async Task<List<ProjectDirectory>> GetDirectoryDetailsAsync(
    string            projectPrefix,   // “…/{project}/”, ends with '/'
    CancellationToken ct = default)
{
    // Bucket may contain thousands of rolls; use a dictionary to merge the
    // two categories fast.

	_log.LogInformation("Getting directory details for prefix: {Prefix}", projectPrefix);
    var lookup = new Dictionary<string, ProjectDirectory>(
                     StringComparer.OrdinalIgnoreCase);

    foreach (string category in new[] { "RawFiles", "ProcessedFiles" })
    {
        string categoryPrefix = $"{projectPrefix}{category}/";
		categoryPrefix = WebUtility.UrlDecode(categoryPrefix);

        _log.LogInformation("Processing category prefix: {CategoryPrefix}", categoryPrefix);
        var rollNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var listObjectsArgs = new ListObjectsArgs()
            .WithBucket(BucketName)
            .WithPrefix(categoryPrefix)
            .WithRecursive(true);

        await foreach (var item in _minioClient.ListObjectsEnumAsync(listObjectsArgs, ct).WithCancellation(ct))
        {
			_log.LogInformation("Loop - Processing item: {ItemKey}, IsDir: {IsDir}", item.Key, item.IsDir);
            if (item.IsDir)
            {
				_log.LogInformation("Skipping directory item: {ItemKey}", item.Key);
                continue; // Skip directories, we infer rolls from file paths
            }

            string objectKey = item.Key;
            _log.LogInformation("Processing object key: {ObjectKey}", objectKey);

            if (!objectKey.StartsWith(categoryPrefix))
            {
                _log.LogWarning("Object key {ObjectKey} does not start with expected category prefix {CategoryPrefix}. Skipping.", objectKey, categoryPrefix);
                continue;
            }

            string pathInsideCategory = objectKey.Substring(categoryPrefix.Length); // e.g., "Roll1/img.jpg" or "img.jpg"

			_log.LogInformation("Path inside category: {PathInsideCategory}", pathInsideCategory);

            string[] pathParts = pathInsideCategory.Split(new[] { '/' }, 2); // {"Roll1", "img.jpg"} or {"img.jpg"}

            if (pathParts.Length > 1 && !string.IsNullOrEmpty(pathParts[0]))
            {
                // This means pathParts[0] is a directory segment (a roll name)
                // and pathParts[1] is the file/object name within that roll.
                string rollName = pathParts[0];
                if (!string.IsNullOrWhiteSpace(rollName)) // Ensure rollName is not empty or just whitespace
                {
                    rollNames.Add(rollName);
                    _log.LogInformation("Added roll name: {RollName} from object {ObjectKey}", rollName, objectKey);
                }
            }
            // else: file is directly in category folder (e.g. RawFiles/image.jpg), not in a roll. These are ignored for ProjectDirectory.
        }

        _log.LogInformation("Found {Count} unique roll(s) for category {Category}: {Rolls}", rollNames.Count, category, string.Join(", ", rollNames));

        foreach (string rollName in rollNames)
        {
            string rollPrefixForCount = $"{categoryPrefix}{rollName}/";
            // _log.LogDebug("Counting files for roll: {RollName} with prefix {RollPrefixForCount}", rollName, rollPrefixForCount);
            int fileCount = await CountFilesAsync(rollPrefixForCount, ct);
            // _log.LogInformation("File count for roll {RollName} in category {Category} is {FileCount}", rollName, category, fileCount);


            if (!lookup.TryGetValue(rollName, out var pd))
            {
                pd = new ProjectDirectory { Name = rollName };
                lookup.Add(rollName, pd);
            }

            if (category.Equals("RawFiles", StringComparison.OrdinalIgnoreCase))
            {
                pd.RawFilesCount = fileCount;
                // _log.LogDebug("Updated RawFilesCount for roll {RollName} to {FileCount}", rollName, fileCount);
            }
            else
            {
                pd.ProcessedFilesCount = fileCount;
                // _log.LogDebug("Updated ProcessedFilesCount for roll {RollName} to {FileCount}", rollName, fileCount);
            }
        }
    }

    return lookup.Values.ToList();
}

    private async Task<int> CountFilesAsync(string prefix, CancellationToken ct)
    {
        int count = 0;

        var countArgs = new ListObjectsArgs()
                           .WithBucket(BucketName)
                           .WithPrefix(prefix)
                           .WithRecursive(true);   // walk entire subtree

        await foreach (var obj in _minioClient
                                  .ListObjectsEnumAsync(countArgs, ct)
                                  .WithCancellation(ct))
        {
            if (!obj.IsDir)      // ignore synthetic folder entries
                count++;
        }

        return count;
    }

    public async Task<ImageUploadResult> UploadImageFromBytesAsync(byte[] imageData, string objectKey, string fileName)
    {
        try
        {
            // Ensure bucket exists
            if (!await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName)))
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
            }

            using var stream = new MemoryStream(imageData);
            var putArgs = new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(MinIODirectoryHelper.GetMimeType(fileName));

            await _minioClient.PutObjectAsync(putArgs);
            
            // Extract metadata if needed
            stream.Position = 0;
            var metadata = new ImageMetadata
            {
                BlobUri = $"s3://{BucketName}/{objectKey}",
                UploadedBy = "Admin", 
                UploadDate = DateTime.UtcNow,
                CameraGeneratedMetadata = _metadataExtractorService.GetCameraGeneratedMetadata(stream)
            };

            _log.LogInformation("Uploaded image: {ObjectKey}", objectKey);
            return ImageUploadResult.Ok(objectKey);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to upload image {ObjectKey}", objectKey);
            return ImageUploadResult.Fail(objectKey, ex.Message);
        }
    }

    public async Task<(ImageUploadResult uploadResult, ImageMapping? imageMapping)> UploadImageFromBytesWithMappingAsync(
        byte[] imageData, string objectKey, string fileName, string projectName, string directoryName, DateTime timestamp)
    {
        try
        {
            // Ensure bucket exists
            if (!await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName)))
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
            }

            using var stream = new MemoryStream(imageData);
            var putArgs = new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(MinIODirectoryHelper.GetMimeType(fileName));

            await _minioClient.PutObjectAsync(putArgs);
            
            // Extract metadata
            stream.Position = 0;
            var cameraMetadata = _metadataExtractorService.GetCameraGeneratedMetadata(stream);
            
            // Create ImageMapping record
            var imageMapping = new ImageMapping
            {
                Id = Guid.NewGuid(),
                ObjectKey = objectKey,
                FileName = fileName,
                ProjectName = projectName,
                UploadDate = DateTime.UtcNow,
                FileSize = imageData.Length,
                ContentType = MinIODirectoryHelper.GetMimeType(fileName),
                DirectoryName = directoryName,
                Year = timestamp.ToString("yyyy"),
                Width = cameraMetadata?.ImageWidth,
                Height = cameraMetadata?.ImageHeight,
                MetadataJson = JsonConvert.SerializeObject(cameraMetadata, 
                    new JsonSerializerSettings 
                    { 
                        NullValueHandling = NullValueHandling.Ignore,
                        Converters = { new NewtonsoftRationalConverter() }
                    }),
                IsActive = true
            };

            // Save mapping to database using a new scope to avoid threading issues
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IImageMappingRepository>();
            await repository.AddAsync(imageMapping);

            _log.LogInformation("Uploaded image with mapping: {ObjectKey} -> GUID: {Id}", objectKey, imageMapping.Id);
            return (ImageUploadResult.Ok(objectKey), imageMapping);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to upload image with mapping {ObjectKey}", objectKey);
            return (ImageUploadResult.Fail(objectKey, ex.Message), null);
        }
    }

    public async Task<(UploadResponse uploadResponse, List<ImageEmbeddingInput> embeddings)> ProcessZipOptimizedAsync(
        IFormFile directoryFile,
        string projectName,
        string directoryName,
        DateTime timestamp,
        bool isRawFiles = true,
        string rawfileDirectoryName = "")
    {
        // Ensure bucket exists
        if (!await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName)))
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
        }

        var embeddings = new List<ImageEmbeddingInput>();
        var uploadTasks = new List<Task<(ImageUploadResult, ImageMapping?)>>();
        var uploadedFiles = new List<UploadedFileInfo>();
        int totalCount = 0;

        string destinationPath = MinIODirectoryHelper.GetDestinationPath(
            timestamp, projectName,
            isRawFiles ? directoryName : rawfileDirectoryName,
            isRawFiles);

        // If uploading processed files, verify raw files exist
        if (!isRawFiles)
        {
            string rawPath = MinIODirectoryHelper.GetDestinationPath(
                timestamp, projectName, rawfileDirectoryName, true);
            if (!await DoesPathExistAsync(rawPath))
                throw new InvalidOperationException(
                    $"Raw files path '{rawPath}' does not exist.");
        }

        using var zipStream = directoryFile.OpenReadStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // Process each image entry once
        foreach (var entry in archive.Entries)
        {
            if (!MinIODirectoryHelper.IsDirectDescendant(entry, directoryName) ||
                !MinIODirectoryHelper.IsImageFile(entry.Name))
                continue;

            totalCount++;

            try
            {
                await using var entryStream = entry.Open();
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
                var imageData = ms.ToArray();

                string relPath = MinIODirectoryHelper.GetRelativePath(entry.FullName, directoryName);
                string objectKey = $"{destinationPath}/{relPath}";

                // Add to embeddings list
                embeddings.Add(new ImageEmbeddingInput(objectKey, imageData));

                // Start upload task with mapping
                var uploadTask = UploadImageFromBytesWithMappingAsync(imageData, objectKey, entry.Name, projectName, directoryName, timestamp);
                uploadTasks.Add(uploadTask);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed processing entry {Entry}", entry.FullName);
                
                // Add failed upload to the list
                string relPath = MinIODirectoryHelper.GetRelativePath(entry.FullName, directoryName);
                string objectKey = $"{destinationPath}/{relPath}";
                
                uploadedFiles.Add(new UploadedFileInfo
                {
                    Id = Guid.Empty,
                    FileName = entry.Name,
                    ObjectKey = objectKey,
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileSize = entry.Length,
                    ContentType = MinIODirectoryHelper.GetMimeType(entry.Name)
                });
            }
        }

        // Wait for all uploads to complete
        var uploadResults = await Task.WhenAll(uploadTasks);
        
        // Process results and build file list
        foreach (var (uploadResult, imageMapping) in uploadResults)
        {
            if (uploadResult.Success && imageMapping != null)
            {
                uploadedFiles.Add(new UploadedFileInfo
                {
                    Id = imageMapping.Id,
                    FileName = imageMapping.FileName,
                    ObjectKey = imageMapping.ObjectKey,
                    Success = true,
                    FileSize = imageMapping.FileSize,
                    ContentType = imageMapping.ContentType,
                    Width = imageMapping.Width,
                    Height = imageMapping.Height
                });
            }
            else if (!uploadResult.Success)
            {
                // Find existing failed entry or create new one
                var existingFailed = uploadedFiles.FirstOrDefault(f => f.ObjectKey == uploadResult.ObjectKey && !f.Success);
                if (existingFailed != null)
                {
                    existingFailed.ErrorMessage = uploadResult.ErrorMessage;
                }
            }
        }
        
        int uploadedCount = uploadResults.Count(r => r.Item1.Success);

        var uploadResponse = new UploadResponse 
        { 
            UploadedCount = uploadedCount, 
            OriginalCount = totalCount,
            Files = uploadedFiles
        };

        return (uploadResponse, embeddings);
    }

    public async Task<Stream?> GetImageStreamAsync(string objectKey)
    {
        try
        {
            _log.LogInformation("Getting image stream for object key: {ObjectKey}", objectKey);
            
            // Check if object exists first
            var args = new StatObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey);
                
            try
            {
                await _minioClient.StatObjectAsync(args);
            }
            catch (Exception)
            {
                _log.LogWarning("Object not found: {ObjectKey}", objectKey);
                return null;
            }
            
            // Get the object stream
            var memoryStream = new MemoryStream();
            var getArgs = new GetObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream) => {
                    await stream.CopyToAsync(memoryStream);
                });
            
            await _minioClient.GetObjectAsync(getArgs);
            
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error getting image stream for object key: {ObjectKey}", objectKey);
            return null;
        }
    }
}

