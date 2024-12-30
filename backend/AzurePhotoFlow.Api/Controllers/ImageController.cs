using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IImageUploadService _imageUploadService;

    public ImageController(IImageUploadService imageUploadService)
    {
        _imageUploadService = imageUploadService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Invalid file.");
        }

        try
        {
            var metadata = await _imageUploadService.UploadImageAsync(file);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}

