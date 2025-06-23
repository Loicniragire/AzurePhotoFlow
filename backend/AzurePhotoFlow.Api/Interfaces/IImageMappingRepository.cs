using AzurePhotoFlow.Api.Data;

namespace AzurePhotoFlow.Api.Interfaces;

/// <summary>
/// Repository interface for ImageMapping operations
/// </summary>
public interface IImageMappingRepository
{
    /// <summary>
    /// Get image mapping by GUID
    /// </summary>
    Task<ImageMapping?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get image mapping by object key
    /// </summary>
    Task<ImageMapping?> GetByObjectKeyAsync(string objectKey);

    /// <summary>
    /// Get multiple image mappings by GUIDs
    /// </summary>
    Task<List<ImageMapping>> GetByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Get all image mappings for a project
    /// </summary>
    Task<List<ImageMapping>> GetByProjectAsync(string projectName, bool activeOnly = true);

    /// <summary>
    /// Get image mappings by year
    /// </summary>
    Task<List<ImageMapping>> GetByYearAsync(string year, bool activeOnly = true);

    /// <summary>
    /// Search image mappings with filters
    /// </summary>
    Task<List<ImageMapping>> SearchAsync(
        string? projectName = null,
        string? year = null,
        string? directoryName = null,
        bool activeOnly = true,
        int skip = 0,
        int take = 100);

    /// <summary>
    /// Add new image mapping
    /// </summary>
    Task<ImageMapping> AddAsync(ImageMapping imageMapping);

    /// <summary>
    /// Add multiple image mappings
    /// </summary>
    Task<List<ImageMapping>> AddRangeAsync(IEnumerable<ImageMapping> imageMappings);

    /// <summary>
    /// Update existing image mapping
    /// </summary>
    Task<ImageMapping> UpdateAsync(ImageMapping imageMapping);

    /// <summary>
    /// Soft delete image mapping
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id);

    /// <summary>
    /// Hard delete image mapping
    /// </summary>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Check if object key exists
    /// </summary>
    Task<bool> ExistsAsync(string objectKey);

    /// <summary>
    /// Get count of images by project
    /// </summary>
    Task<int> GetCountByProjectAsync(string projectName, bool activeOnly = true);

    /// <summary>
    /// Get all active GUIDs for Qdrant migration
    /// </summary>
    Task<List<Guid>> GetAllActiveGuidsAsync();
}