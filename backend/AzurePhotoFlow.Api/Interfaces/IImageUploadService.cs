using Api.Models;

namespace Api.Interfaces;
public interface IImageUploadService
{
    /// <summary>
    /// Deletes a project and all contents associated with it.
    /// </summary>
    /// <param name="projectName">The name of the project.</param>
    /// <param name="timestamp">The timestamp of the project.</param>
    /// <param name="isRawFiles">Indicates whether the files are raw files or processed files.</param>
    /// <returns></returns>
    Task Delete(string projectName, DateTime timestamp);

    /// <summary>
    /// Uploads a zipped file to the Azure Blob Storage. 
    /// Ignores subdirectory files.
    /// </summary>
    ///
    Task<List<ImageMetadata>> ExtractAndUploadImagesAsync(IFormFile directoryFile,
                                                          string projectName,
                                                       string directoryName,
                                                       DateTime timestamp,
                                                          bool isRawFiles = true,
                                                          string rawfileDirectoryName = "");

    Task<List<ProjectInfo>> GetProjects(string year, string projectName, DateTime? timestamp);
}


