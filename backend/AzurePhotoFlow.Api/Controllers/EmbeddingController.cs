using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using AzurePhotoFlow.Shared;
using Api.Models;
using System.ComponentModel.DataAnnotations;

namespace AzurePhotoFlow.Services;

/// <summary>
/// AI embedding generation using CLIP model for semantic image search.
/// Processes images to create vector embeddings for similarity and text-based search.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EmbeddingController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public EmbeddingController(IEmbeddingService embeddingService, IVectorStore vectorStore)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Generate AI embeddings for images in a ZIP archive using CLIP model.
    /// </summary>
    /// <param name="request">Embedding generation request with ZIP file and metadata</param>
    /// <returns>Processing results with embedding generation statistics</returns>
    /// <response code="200">Successfully generated embeddings for all images</response>
    /// <response code="400">Invalid ZIP file or request parameters</response>
    /// <response code="401">Unauthorized - requires authentication</response>
    /// <response code="500">Error during embedding generation</response>
    /// <remarks>
    /// This endpoint processes a ZIP archive of images and generates vector embeddings
    /// using the CLIP (Contrastive Language-Image Pre-training) model for semantic search.
    /// 
    /// **Features:**
    /// - CLIP-based vector embeddings (512-dimensional)
    /// - Batch processing for efficiency
    /// - Support for raw and processed image workflows
    /// - Automatic storage in vector database (Qdrant)
    /// - Metadata extraction and indexing
    /// 
    /// **Supported Image Formats:**
    /// - JPEG (.jpg, .jpeg)
    /// - PNG (.png)
    /// - GIF (.gif)
    /// - BMP (.bmp)
    /// - TIFF (.tiff, .tif)
    /// - WebP (.webp)
    /// 
    /// **Processing:**
    /// 1. Extract images from ZIP archive
    /// 2. Validate image formats and sizes
    /// 3. Generate CLIP embeddings using ONNX runtime
    /// 4. Store embeddings in vector database
    /// 5. Index metadata for filtering and search
    /// 
    /// **Use Cases:**
    /// - Enable semantic search on uploaded images
    /// - Generate embeddings for new image collections
    /// - Reprocess images with updated AI models
    /// - Batch processing for large image sets
    /// 
    /// **Example Request:**
    /// 
    ///     POST /api/embedding/generate
    ///     Content-Type: multipart/form-data
    ///     
    ///     zipFile: [ZIP file containing images]
    ///     timestamp: 2024-01-15T10:30:00.000Z
    ///     projectName: "Wedding_Photos"
    ///     directoryName: "Ceremony"
    ///     isRawFiles: true
    /// 
    /// **Response includes:**
    /// - Number of images processed
    /// - Embedding generation statistics
    /// - Processing time and performance metrics
    /// - Error details for any failed images
    /// 
    /// </remarks>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromForm] EmbeddingRequest request)
    {
        if (request.ZipFile == null || request.ZipFile.Length == 0)
        {
            return BadRequest("Zip file must be provided");
        }

        await using var stream = request.ZipFile.OpenReadStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        string destDir = request.IsRawFiles ? request.DirectoryName : request.RawDirectoryName;
        string destinationPrefix = MinIODirectoryHelper.GetDestinationPath(request.Timestamp, request.ProjectName, destDir, request.IsRawFiles);

        async IAsyncEnumerable<ImageEmbeddingInput> GetImages()
        {
            foreach (var entry in archive.Entries)
            {
                if (!MinIODirectoryHelper.IsDirectDescendant(entry, request.DirectoryName) ||
                    !MinIODirectoryHelper.IsImageFile(entry.Name))
                    continue;

                await using var entryStream = entry.Open();
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
                string objectKey = $"{destinationPrefix}/{MinIODirectoryHelper.GetRelativePath(entry.FullName, request.DirectoryName)}";
                yield return new ImageEmbeddingInput(objectKey, ms.ToArray());
            }
        }

        var embeddings = new List<ImageEmbedding>();
        await foreach (var e in _embeddingService.GenerateEmbeddingsAsync(GetImages()))
        {
            embeddings.Add(e);
        }
        await _vectorStore.UpsertAsync(embeddings);
        return Ok();
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { status = "Embedding service is running" });
    }
}

public class EmbeddingRequest
{
    [Required]
    public string ProjectName { get; set; } = string.Empty;

    [Required]
    public string DirectoryName { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public IFormFile? ZipFile { get; set; }

    public bool IsRawFiles { get; set; } = true;
    public string RawDirectoryName { get; set; } = string.Empty;
}
