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
    /// Uploads a zipped file to storage
    /// Ignores subdirectory files.
    /// </summary>
    Task<UploadResponse> ExtractAndUploadImagesAsync(IFormFile directoryFile,
                                                          string projectName,
														  string directoryName,
														  DateTime timestamp,
                                                          bool isRawFiles = true,
                                                          string rawfileDirectoryName = "");

    Task<List<ProjectInfo>> GetProjectsAsync(string year, string projectName, DateTime? timestamp, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads a single image from byte array to storage
    /// </summary>
    Task<ImageUploadResult> UploadImageFromBytesAsync(byte[] imageData, string objectKey, string fileName);
    
    /// <summary>
    /// Optimized method to process zip file once for both embeddings and uploads
    /// </summary>
    Task<(UploadResponse uploadResponse, List<ImageEmbeddingInput> embeddings)> ProcessZipOptimizedAsync(
        IFormFile directoryFile,
        string projectName,
        string directoryName,
        DateTime timestamp,
        bool isRawFiles = true,
        string rawfileDirectoryName = "");
    
    /// <summary>
    /// Get image stream by object key for serving images
    /// </summary>
    Task<Stream?> GetImageStreamAsync(string objectKey);
}


