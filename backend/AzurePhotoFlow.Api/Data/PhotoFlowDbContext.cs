using Microsoft.EntityFrameworkCore;

namespace AzurePhotoFlow.Api.Data;

/// <summary>
/// Entity Framework DbContext for PhotoFlow database operations
/// </summary>
public class PhotoFlowDbContext : DbContext
{
    public PhotoFlowDbContext(DbContextOptions<PhotoFlowDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Image mappings table
    /// </summary>
    public DbSet<ImageMapping> ImageMappings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ImageMapping entity
        modelBuilder.Entity<ImageMapping>(entity =>
        {
            // Primary key
            entity.HasKey(e => e.Id);

            // Indexes for performance
            entity.HasIndex(e => e.ObjectKey)
                  .IsUnique()
                  .HasDatabaseName("IX_ImageMappings_ObjectKey");

            entity.HasIndex(e => e.ProjectName)
                  .HasDatabaseName("IX_ImageMappings_ProjectName");

            entity.HasIndex(e => e.Year)
                  .HasDatabaseName("IX_ImageMappings_Year");

            entity.HasIndex(e => e.UploadDate)
                  .HasDatabaseName("IX_ImageMappings_UploadDate");

            entity.HasIndex(e => e.IsActive)
                  .HasDatabaseName("IX_ImageMappings_IsActive");

            // Composite index for common queries
            entity.HasIndex(e => new { e.ProjectName, e.Year, e.IsActive })
                  .HasDatabaseName("IX_ImageMappings_ProjectName_Year_IsActive");

            // Configure properties
            entity.Property(e => e.ObjectKey)
                  .IsRequired()
                  .HasMaxLength(1000);

            entity.Property(e => e.FileName)
                  .IsRequired()
                  .HasMaxLength(255);

            entity.Property(e => e.ProjectName)
                  .HasMaxLength(255);

            entity.Property(e => e.ContentType)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.DirectoryName)
                  .HasMaxLength(255);

            entity.Property(e => e.Year)
                  .HasMaxLength(4);

            entity.Property(e => e.UploadDate)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedDate)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);
        });
    }
}