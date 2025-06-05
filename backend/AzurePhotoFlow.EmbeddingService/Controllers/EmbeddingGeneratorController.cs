using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzurePhotoFlow.Services;

[ApiController]
[Route("")]
public class EmbeddingGeneratorController : ControllerBase
{
    private readonly IEmbeddingGeneratorService _generatorService;

    public EmbeddingGeneratorController(IEmbeddingGeneratorService generatorService)
    {
        _generatorService = generatorService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] EmbeddingRequest request)
    {
        await _generatorService.GenerateAsync(request.ProjectName, request.DirectoryName, request.Timestamp);
        return Ok();
    }
}

public record EmbeddingRequest(string ProjectName, string DirectoryName, DateTime Timestamp);
