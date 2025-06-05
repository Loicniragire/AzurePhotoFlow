using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzurePhotoFlow.Services;

[ApiController]
[Route("api/[controller]")]
public class EmbeddingController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;

    public EmbeddingController(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] EmbeddingRequest request)
    {
        await _embeddingService.GenerateAsync(request.ProjectName, request.DirectoryName, request.Timestamp);
        return Ok();
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { status = "Embedding service is running" });
    }
}

public record EmbeddingRequest(string ProjectName, string DirectoryName, DateTime Timestamp);
