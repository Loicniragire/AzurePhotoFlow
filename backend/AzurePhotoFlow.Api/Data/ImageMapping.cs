using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzurePhotoFlow.Api.Data;

/// <summary>
/// Entity model for mapping GUIDs to MinIO object keys and image metadata
/// </summary>
[Table("ImageMappings")]
public class ImageMapping
{
    /// <summary>
    /// Unique identifier for the image
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// MinIO object key/path for the image file
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Original filename as uploaded
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Project name for organization
    /// </summary>
    [MaxLength(255)]
    public string? ProjectName { get; set; }

    /// <summary>
    /// Upload timestamp
    /// </summary>
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// MIME content type
    /// </summary>
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Image width in pixels
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Image height in pixels
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Directory/category name within project
    /// </summary>
    [MaxLength(255)]
    public string? DirectoryName { get; set; }

    /// <summary>
    /// Year extracted from upload date or file metadata
    /// </summary>
    [MaxLength(4)]
    public string? Year { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata as JSON string
    /// </summary>
    public string? MetadataJson { get; set; }
}