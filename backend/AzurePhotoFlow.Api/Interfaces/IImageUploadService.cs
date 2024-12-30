using Api.Models;

namespace Api.Interfaces;
public interface IImageUploadService
{
	Task<ImageMetadata> UploadImageAsync(IFormFile image);
    Task<List<string>> ExtractAndUploadImagesAsync(IFormFile directoryFile, string directoryName);
}


