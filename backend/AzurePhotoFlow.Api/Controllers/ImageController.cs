using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;
using System.IO.Compression;
using AzurePhotoFlow.Shared;
using System.Collections.Generic;
using System.IO;
using AzurePhotoFlow.Services;

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
        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);
            _logger.LogInformation($"Uploading directory {directoryName} for project {projectName} at timestamp {timeStamp}", directoryName, projectName, timeStamp);

            var images = ExtractImagesForEmbedding(directoryFile, projectName, directoryName, timeStamp, true, directoryName);
            var embeddings = new List<ImageEmbedding>();
            await foreach (var e in _embeddingService.GenerateEmbeddingsAsync(images))
            {
                embeddings.Add(e);
            }
            await _vectorStore.UpsertAsync(embeddings);

            var extractedFiles = await _imageUploadService.ExtractAndUploadImagesAsync(directoryFile, projectName, directoryName, timeStamp);

            return Ok(new
            {
                Message = "Directory uploaded and files extracted successfully.",
                Files = extractedFiles
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
        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);
            var images = ExtractImagesForEmbedding(directoryFile, projectName, directoryName, timeStamp, false, rawfileDirectoryName);
            var embeddings = new List<ImageEmbedding>();
            await foreach (var e in _embeddingService.GenerateEmbeddingsAsync(images))
            {
                embeddings.Add(e);
            }
            await _vectorStore.UpsertAsync(embeddings);
            // Upload processed files and ensure corresponding raw files path exists
            var extractedFiles = await _imageUploadService.ExtractAndUploadImagesAsync(
                directoryFile,
                projectName,
                directoryName,
                timeStamp,
                isRawFiles: false,
                   rawfileDirectoryName);

            return Ok(new
            {
                Message = "Processed directory uploaded and files extracted successfully.",
                Files = extractedFiles
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

    private static async IAsyncEnumerable<ImageEmbeddingInput> ExtractImagesForEmbedding(
        IFormFile zip,
        string projectName,
        string directoryName,
        DateTime timeStamp,
        bool isRaw,
        string rawDir)
    {
        await using var stream = zip.OpenReadStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        string destDir = isRaw ? directoryName : rawDir;
        string prefix = MinIODirectoryHelper.GetDestinationPath(timeStamp, projectName, destDir, isRaw);

        foreach (var entry in archive.Entries)
        {
            if (!MinIODirectoryHelper.IsDirectDescendant(entry, directoryName) ||
                !MinIODirectoryHelper.IsImageFile(entry.Name))
                continue;

            await using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            string objectKey = $"{prefix}/{MinIODirectoryHelper.GetRelativePath(entry.FullName, directoryName)}";
            yield return new ImageEmbeddingInput(objectKey, ms.ToArray());
        }
    }
}

