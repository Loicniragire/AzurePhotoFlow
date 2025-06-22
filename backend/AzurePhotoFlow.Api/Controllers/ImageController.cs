using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;

/// <summary>
/// Image upload, management, and project organization endpoints.
/// Handles ZIP archive processing, AI analysis, and file organization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IImageUploadService _imageUploadService;
    private readonly ILogger<ImageController> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public ImageController(ILogger<ImageController> logger,
                           IImageUploadService imageUploadService,
                           IEmbeddingService embeddingService,
                           IVectorStore vectorStore)
    {
        _imageUploadService = imageUploadService;
        _logger = logger;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Upload a ZIP archive containing images and automatically organize them with AI processing.
    /// </summary>
    /// <param name="timeStamp">Timestamp for organizing uploads (ISO 8601 format)</param>
    /// <param name="projectName">Project name for categorizing the upload</param>
    /// <param name="directoryFile">ZIP file containing images to upload</param>
    /// <returns>Upload results with processing status and file information</returns>
    /// <response code="200">Successfully uploaded and processed images</response>
    /// <response code="400">Invalid request parameters or file format</response>
    /// <response code="401">Unauthorized - requires authentication</response>
    /// <response code="413">File too large</response>
    /// <remarks>
    /// This endpoint accepts a ZIP archive containing image files and processes them with AI features:
    /// 
    /// **Features:**
    /// - Automatic image extraction from ZIP archives
    /// - AI-powered image embeddings generation using CLIP model
    /// - EXIF metadata extraction
    /// - Face detection and recognition
    /// - OCR (text extraction from images)
    /// - Organized storage with hierarchical structure
    /// 
    /// **File Organization:**
    /// Images are stored using the path structure: `{timestamp}/{projectName}/{directoryName}/{fileName}`
    /// 
    /// **Supported Formats:**
    /// - ZIP archives (.zip)
    /// - Image formats: JPEG, PNG, GIF, BMP, TIFF, WebP
    /// 
    /// **Processing:**
    /// - Each image generates vector embeddings for semantic search
    /// - Metadata is extracted and indexed for filtering
    /// - Face recognition data is computed for people search
    /// - OCR text is extracted for text-based search
    /// 
    /// **Example:**
    /// 
    ///     POST /api/image/raw
    ///     Content-Type: multipart/form-data
    ///     Authorization: Bearer {jwt_token}
    ///     
    ///     timeStamp: 2024-01-15T10:30:00.000Z
    ///     projectName: "Wedding_Photos"
    ///     directoryFile: [ZIP file containing images]
    /// 
    /// **Response includes:**
    /// - Upload statistics (files processed, errors, processing time)
    /// - Individual file processing results
    /// - Generated metadata and AI analysis results
    /// 
    /// </remarks>
	[Authorize(Roles = "FullAccess")]
    [HttpPost("raw")]
    public async Task<IActionResult> UploadDirectory(DateTime timeStamp, string projectName, IFormFile directoryFile)
    {
        if (timeStamp == default)
        {
            return BadRequest("Timestamp must be provided.");
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            return BadRequest("Project name must be provided.");
        }

        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);
            _logger.LogInformation($"Uploading directory {directoryName} for project {projectName} at timestamp {timeStamp}", directoryName, projectName, timeStamp);

            var result = await ProcessZipFileOptimizedAsync(directoryFile, projectName, directoryName, timeStamp, true, directoryName);

            return Ok(new
            {
                message = "Directory uploaded and files extracted successfully.",
                files = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

	/// <summary>
	/// Upload processed/edited images associated with an existing raw file collection.
	/// </summary>
	/// <param name="timeStamp">Timestamp matching the original raw file upload</param>
	/// <param name="projectName">Project name matching the original raw file upload</param>
	/// <param name="rawfileDirectoryName">Directory name of the associated raw files (e.g., "roll1", "roll2")</param>
	/// <param name="directoryFile">ZIP file containing processed/edited images</param>
	/// <returns>Upload results for processed image files</returns>
	/// <response code="200">Successfully uploaded processed images</response>
	/// <response code="400">Invalid request parameters or missing associated raw files</response>
	/// <response code="401">Unauthorized - requires authentication</response>
	/// <remarks>
	/// This endpoint uploads processed or edited versions of previously uploaded raw images.
	/// It maintains the relationship between raw and processed files through the directory structure.
	/// 
	/// **File Organization:**
	/// Processed files are stored as: `{timestamp.year}/{timestamp}/{projectName}/Processed/{rawfileDirectoryName}/{fileName}`
	/// 
	/// **Use Cases:**
	/// - Upload edited/retouched versions of photos
	/// - Associate processed images with specific film rolls or sessions
	/// - Maintain version control between raw and processed images
	/// - Link final deliverables to original captures
	/// 
	/// **Example Workflow:**
	/// 1. Upload raw files: `/api/image/raw` with projectName="Wedding" and directory="roll1"
	/// 2. Process/edit the images externally
	/// 3. Upload processed files: `/api/image/processed` with same projectName="Wedding" and rawfileDirectoryName="roll1"
	/// 
	/// **Requirements:**
	/// - The referenced raw file directory must exist
	/// - Timestamp should match the original raw file upload
	/// - Project name must match the original raw file upload
	/// 
	/// **Processing:**
	/// - AI embeddings are generated for processed images
	/// - Metadata links to original raw files
	/// - Separate indexing for processed vs raw versions
	/// 
	/// </remarks>
    [HttpPost("processed")]
    public async Task<IActionResult> UploadProcessedFiles(DateTime timeStamp, string projectName, string rawfileDirectoryName, IFormFile directoryFile)
    {
        if (timeStamp == default)
        {
            return BadRequest("Timestamp must be provided.");
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            return BadRequest("Project name must be provided.");
        }

        if (string.IsNullOrWhiteSpace(rawfileDirectoryName))
        {
            return BadRequest("Raw directory name must be provided.");
        }

        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);
            var result = await ProcessZipFileOptimizedAsync(directoryFile, projectName, directoryName, timeStamp, false, rawfileDirectoryName);

            return Ok(new
            {
                message = "Processed directory uploaded and files extracted successfully.",
                files = result
            });
        }
        catch (InvalidOperationException ex)
        {
            // Handle specific exception for missing raw files
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete an entire project and all its associated files, images, and metadata.
    /// </summary>
    /// <param name="projectName">Name of the project to delete</param>
    /// <param name="timestamp">Timestamp of the specific project instance to delete</param>
    /// <returns>Confirmation of successful deletion</returns>
    /// <response code="200">Project successfully deleted</response>
    /// <response code="400">Invalid project name or timestamp</response>
    /// <response code="401">Unauthorized - requires authentication</response>
    /// <response code="404">Project not found</response>
    /// <remarks>
    /// **⚠️ WARNING: This operation is irreversible!**
    /// 
    /// This endpoint permanently deletes:
    /// - All raw and processed image files
    /// - All generated AI embeddings and metadata
    /// - All face recognition data
    /// - All OCR text data
    /// - All project structure and organization
    /// 
    /// **Deletion Scope:**
    /// - Removes the entire project directory: `{timestamp.year}/{timestamp}/{projectName}/`
    /// - Deletes all subdirectories (RawFiles, Processed, etc.)
    /// - Removes vector database entries for all images in the project
    /// - Cleans up associated metadata and search indexes
    /// 
    /// **Use Cases:**
    /// - Clean up test or temporary projects
    /// - Remove projects that are no longer needed
    /// - Comply with data retention policies
    /// - Free up storage space
    /// 
    /// **Example:**
    /// 
    ///     DELETE /api/image/projects?projectName=Wedding_2024&timestamp=2024-01-15T10:30:00.000Z
    /// 
    /// </remarks>
    [HttpDelete("projects")]
    public async Task<IActionResult> DeleteProject(string projectName, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return BadRequest("Project name must be provided.");
        }

        try
        {
            await _imageUploadService.Delete(projectName, timestamp);
            return Ok($"Project '{projectName}' deleted successfully.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieve a list of projects with optional filtering and detailed information.
    /// </summary>
    /// <param name="year">Filter projects by year (e.g., "2024")</param>
    /// <param name="projectName">Filter by specific project name</param>
    /// <param name="timestamp">Filter by specific timestamp (yyyy-MM-dd format)</param>
    /// <returns>List of projects with metadata and statistics</returns>
    /// <response code="200">Successfully retrieved project list</response>
    /// <response code="400">Invalid filter parameters</response>
    /// <response code="401">Unauthorized - requires authentication</response>
    /// <remarks>
    /// This endpoint provides a comprehensive view of all projects in the system,
    /// with optional filtering capabilities and detailed metadata for each project.
    /// 
    /// **Returned Information:**
    /// - Project name and creation timestamp
    /// - Total number of images (raw and processed)
    /// - Storage statistics and file sizes
    /// - Project directory structure
    /// - Upload and last modified dates
    /// - AI processing status
    /// 
    /// **Filtering Options:**
    /// - **Year**: Filter by creation year (e.g., "2024")
    /// - **Project Name**: Exact or partial match on project name
    /// - **Timestamp**: Filter by specific upload date (yyyy-MM-dd)
    /// 
    /// **Use Cases:**
    /// - Browse available projects for search
    /// - Monitor storage usage by project
    /// - Audit project creation and modification dates
    /// - Find projects for management or deletion
    /// - Generate reports on photo collection organization
    /// 
    /// **Examples:**
    /// 
    ///     GET /api/image/projects
    ///     GET /api/image/projects?year=2024
    ///     GET /api/image/projects?projectName=Wedding
    ///     GET /api/image/projects?timestamp=2024-01-15
    ///     GET /api/image/projects?year=2024&projectName=Wedding
    /// 
    /// **Response Format:**
    /// 
    ///     [
    ///       {
    ///         "name": "Wedding_2024",
    ///         "timestamp": "2024-01-15T10:30:00Z",
    ///         "totalImages": 150,
    ///         "rawImages": 120,
    ///         "processedImages": 30,
    ///         "totalSizeBytes": 2500000000,
    ///         "lastModified": "2024-01-16T09:15:00Z"
    ///       }
    ///     ]
    /// 
    /// </remarks>
    [HttpGet("projects")]
    [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<ProjectInfo>))]
    public async Task<IActionResult> GetProjects([FromQuery] string year = null,
                                                 [FromQuery] string projectName = null,
                                                 [FromQuery] string timestamp = null)
    {
        try
        {
            _logger.LogInformation("Getting projects");

            // Validate and parse timestamp if provided. The client may send the
            // date in various formats (e.g. from a browser date picker).  Try a
            // general parse and normalise to a date-only value.
            DateTime? parsedTimestamp = null;
            if (!string.IsNullOrEmpty(timestamp))
            {
                if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var validDate))
                {
                    return BadRequest("Invalid timestamp format. Use 'yyyy-MM-dd'.");
                }
                parsedTimestamp = validDate.Date;
            }

            var projects = await _imageUploadService.GetProjectsAsync(year, projectName, parsedTimestamp);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Optimized method that processes ZIP file once for both embeddings and uploads.
    /// Uses the new service method that eliminates duplicate ZIP processing.
    /// </summary>
    private async Task<UploadResponse> ProcessZipFileOptimizedAsync(
        IFormFile directoryFile,
        string projectName,
        string directoryName,
        DateTime timeStamp,
        bool isRawFiles,
        string rawfileDirectoryName)
    {
        const int batchSize = 10; // Process embeddings in batches
        const int maxConcurrentEmbeddings = 3; // Limit concurrent embedding generation
        
        // Use the optimized service method that processes ZIP once
        var (uploadResponse, embeddingInputs) = await _imageUploadService.ProcessZipOptimizedAsync(
            directoryFile, projectName, directoryName, timeStamp, isRawFiles, rawfileDirectoryName);
        
        // Check if embeddings are enabled
        var enableEmbeddings = Environment.GetEnvironmentVariable("ENABLE_EMBEDDINGS")?.ToLower() == "true";
        
        if (!enableEmbeddings)
        {
            _logger.LogInformation("Embeddings disabled via ENABLE_EMBEDDINGS environment variable");
            return uploadResponse;
        }
        
        // Process embeddings in batches with concurrency control
        var semaphore = new SemaphoreSlim(maxConcurrentEmbeddings);
        var allEmbeddings = new List<ImageEmbedding>();
        
        for (int i = 0; i < embeddingInputs.Count; i += batchSize)
        {
            var batch = embeddingInputs.Skip(i).Take(batchSize);
            var embeddingTasks = batch.Select(async input => 
            {
                await semaphore.WaitAsync();
                try
                {
                    await foreach (var embedding in _embeddingService.GenerateEmbeddingsAsync(
                        CreateAsyncEnumerable(input)))
                    {
                        return embedding;
                    }
                    throw new InvalidOperationException($"Failed to generate embedding for {input.ObjectKey}");
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            var batchEmbeddings = await Task.WhenAll(embeddingTasks);
            allEmbeddings.AddRange(batchEmbeddings);
            
            // Upsert batch to vector store
            if (batchEmbeddings.Length > 0)
            {
                try
                {
                    _logger.LogInformation("Preparing to upsert {Count} embeddings to vector store", batchEmbeddings.Length);
                    foreach (var embedding in batchEmbeddings)
                    {
                        _logger.LogDebug("Embedding - ObjectKey: {ObjectKey}, Vector Length: {VectorLength}", 
                            embedding.ObjectKey, embedding.Vector?.Length ?? 0);
                    }
                    
                    await _vectorStore.UpsertAsync(batchEmbeddings);
                    _logger.LogInformation("Successfully upserted {Count} embeddings to vector store", batchEmbeddings.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert {Count} embeddings to vector store. Error: {ErrorMessage}", 
                        batchEmbeddings.Length, ex.Message);
					throw;
                }
            }
        }
        
        return uploadResponse;
    }
    
    /// <summary>
    /// Serve an image file from storage by its object key/path.
    /// </summary>
    /// <param name="objectKey">The object key/path of the image in storage</param>
    /// <returns>The image file as a stream</returns>
    [HttpGet("{*objectKey}")]
    [SwaggerResponse(StatusCodes.Status200OK, "Image file", typeof(FileStreamResult))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Image not found")]
    public async Task<IActionResult> GetImage(string objectKey)
    {
        try
        {
            _logger.LogInformation("Serving image: {ObjectKey}", objectKey);
            
            // Get the image stream from the upload service
            var imageStream = await _imageUploadService.GetImageStreamAsync(objectKey);
            
            if (imageStream == null)
            {
                _logger.LogWarning("Image not found: {ObjectKey}", objectKey);
                return NotFound($"Image not found: {objectKey}");
            }
            
            // Determine content type based on file extension
            var extension = Path.GetExtension(objectKey).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
            
            return File(imageStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving image: {ObjectKey}", objectKey);
            return StatusCode(500, "Internal server error while serving image");
        }
    }

    private static async IAsyncEnumerable<ImageEmbeddingInput> CreateAsyncEnumerable(ImageEmbeddingInput input)
    {
        yield return input;
        await Task.CompletedTask;
    }
}

