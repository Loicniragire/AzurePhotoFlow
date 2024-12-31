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

    [HttpPost("upload-directory")]
    public async Task<IActionResult> UploadDirectory(DateTime timeStamp, IFormFile directoryFile)
    {
        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);

            var extractedFiles = await _imageUploadService.ExtractAndUploadImagesAsync(directoryFile, directoryName, timeStamp);

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

}

