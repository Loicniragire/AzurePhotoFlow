using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;

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
    /// Uploads a directory containing image files as a zip file.
    /// Path to the directory is constructed as: {timestamp}/{projectName}/{directoryName}/{fileName}
    /// The directory is extracted and each image file is uploaded to the Azure Blob Storage.
    /// </summary>
    /// <param name="timeStamp">The timestamp to assign to this upload..</param>
    /// <param name="projectName">The name of the project.</param>
    /// <param name="directoryFile">The zip file containing the directory.</param>
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
                Message = "Directory uploaded and files extracted successfully.",
                Files = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

	/// <summary>
	/// Uploads a directory containing processed image files as a zip file.
	/// Path to the directory is constructed as: {timestamp.year}/{timestamp}/{projectName}/{directoryName}/{fileName}
	/// The directory is extracted and each image file is uploaded to the Azure Blob Storage.
	/// </summary>
	/// <param name="timeStamp">The timestamp to assign to this upload..</param>
	/// <param name="projectName">The name of the project.</param>
	/// <param name="rawfileDirectoryName">The name of the directory within this project for which to associate the
	/// processed files. Suppose a project have 5 rolls of film named roll1, roll2,,roll5. This parameter states the
	/// roll to associate these processed files.</param>
	/// <param name="directoryFile">The zip file containing the directory.</param>
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
                Message = "Processed directory uploaded and files extracted successfully.",
                Files = result
            });
        }
        catch (InvalidOperationException ex)
        {
            // Handle specific exception for missing raw files
            return BadRequest(new
            {
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a project and all its associated files.
    /// Deletes at the project level, which includes all directories and files within the project.
    /// </summary>
    /// <param name="projectName">The name of the project to delete.</param>
    /// <param name="timestamp">The timestamp of the project to delete.</param>
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
    
    private static async IAsyncEnumerable<ImageEmbeddingInput> CreateAsyncEnumerable(ImageEmbeddingInput input)
    {
        yield return input;
        await Task.CompletedTask;
    }
}

