using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;


/*
 * Blob storage structure:
 * 	- Year/
 * 	  - Datestamp/
 * 	    - Project name/
 * 	      - Directory name/
 * 	        - RawFiles/
 * 	          - Image files
* 	        - ProcessedFiles/
 * 	          - Image files
 */
[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IImageUploadService _imageUploadService;

    public ImageController(IImageUploadService imageUploadService)
    {
        _imageUploadService = imageUploadService;
    }

    /// <summary>
    /// Uploads a directory containing image files as a zip file.
    /// Path to the directory is constructed as: {timestamp.year}/{timestamp}/{projectName}/{directoryName}/{fileName}
    /// The directory is extracted and each image file is uploaded to the Azure Blob Storage.
    /// </summary>
    /// <param name="timeStamp">The timestamp to assign to this upload..</param>
    /// <param name="projectName">The name of the project.</param>
    /// <param name="directoryFile">The zip file containing the directory.</param>
    [HttpPost("upload-raw-files")]
    public async Task<IActionResult> UploadDirectory(DateTime timeStamp, string projectName, IFormFile directoryFile)
    {
        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);

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

    [HttpPost("upload-processed-files")]
    public async Task<IActionResult> UploadProcessedFiles(DateTime timeStamp, string projectName, string rawfileDirectoryName, IFormFile directoryFile)
    {
        if (directoryFile == null || directoryFile.Length == 0)
        {
            return BadRequest("A Zip file must be provided.");
        }

        try
        {
            var directoryName = Path.GetFileNameWithoutExtension(directoryFile.FileName);
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


    [HttpDelete("delete-project")]
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
}

