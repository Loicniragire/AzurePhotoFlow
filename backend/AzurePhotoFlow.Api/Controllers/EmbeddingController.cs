using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using AzurePhotoFlow.Shared;

namespace AzurePhotoFlow.Services;

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
    public string ProjectName { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public IFormFile? ZipFile { get; set; }
    public bool IsRawFiles { get; set; } = true;
    public string RawDirectoryName { get; set; } = string.Empty;
}
