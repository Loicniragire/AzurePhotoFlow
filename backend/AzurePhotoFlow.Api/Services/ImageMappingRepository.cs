using AzurePhotoFlow.Api.Data;
using AzurePhotoFlow.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AzurePhotoFlow.Api.Services;

/// <summary>
/// Repository implementation for ImageMapping operations
/// </summary>
public class ImageMappingRepository : IImageMappingRepository
{
    private readonly PhotoFlowDbContext _context;
    private readonly ILogger<ImageMappingRepository> _logger;

    public ImageMappingRepository(PhotoFlowDbContext context, ILogger<ImageMappingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImageMapping?> GetByIdAsync(Guid id)
    {
        return await _context.ImageMappings
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
    }

    public async Task<ImageMapping?> GetByObjectKeyAsync(string objectKey)
    {
        return await _context.ImageMappings
            .FirstOrDefaultAsync(x => x.ObjectKey == objectKey && x.IsActive);
    }

    public async Task<List<ImageMapping>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await _context.ImageMappings
            .Where(x => ids.Contains(x.Id) && x.IsActive)
            .ToListAsync();
    }

    public async Task<List<ImageMapping>> GetByProjectAsync(string projectName, bool activeOnly = true)
    {
        var query = _context.ImageMappings
            .Where(x => x.ProjectName == projectName);

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.UploadDate)
            .ToListAsync();
    }

    public async Task<List<ImageMapping>> GetByYearAsync(string year, bool activeOnly = true)
    {
        var query = _context.ImageMappings
            .Where(x => x.Year == year);

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.UploadDate)
            .ToListAsync();
    }

    public async Task<List<ImageMapping>> SearchAsync(
        string? projectName = null,
        string? year = null,
        string? directoryName = null,
        bool activeOnly = true,
        int skip = 0,
        int take = 100)
    {
        var query = _context.ImageMappings.AsQueryable();

        if (!string.IsNullOrEmpty(projectName))
        {
            query = query.Where(x => x.ProjectName == projectName);
        }

        if (!string.IsNullOrEmpty(year))
        {
            query = query.Where(x => x.Year == year);
        }

        if (!string.IsNullOrEmpty(directoryName))
        {
            query = query.Where(x => x.DirectoryName == directoryName);
        }

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.UploadDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ImageMapping> AddAsync(ImageMapping imageMapping)
    {
        try
        {
            imageMapping.UploadDate = DateTime.UtcNow;
            imageMapping.UpdatedDate = DateTime.UtcNow;

            _context.ImageMappings.Add(imageMapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added image mapping: {Id} -> {ObjectKey}", 
                imageMapping.Id, imageMapping.ObjectKey);

            return imageMapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding image mapping: {ObjectKey}", imageMapping.ObjectKey);
            throw;
        }
    }

    public async Task<List<ImageMapping>> AddRangeAsync(IEnumerable<ImageMapping> imageMappings)
    {
        try
        {
            var mappingsList = imageMappings.ToList();
            var now = DateTime.UtcNow;

            foreach (var mapping in mappingsList)
            {
                mapping.UploadDate = now;
                mapping.UpdatedDate = now;
            }

            _context.ImageMappings.AddRange(mappingsList);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added {Count} image mappings", mappingsList.Count);

            return mappingsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding image mappings batch");
            throw;
        }
    }

    public async Task<ImageMapping> UpdateAsync(ImageMapping imageMapping)
    {
        try
        {
            imageMapping.UpdatedDate = DateTime.UtcNow;

            _context.ImageMappings.Update(imageMapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated image mapping: {Id}", imageMapping.Id);

            return imageMapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating image mapping: {Id}", imageMapping.Id);
            throw;
        }
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        try
        {
            var mapping = await _context.ImageMappings.FindAsync(id);
            if (mapping == null)
            {
                return false;
            }

            mapping.IsActive = false;
            mapping.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Soft deleted image mapping: {Id}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting image mapping: {Id}", id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var mapping = await _context.ImageMappings.FindAsync(id);
            if (mapping == null)
            {
                return false;
            }

            _context.ImageMappings.Remove(mapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Hard deleted image mapping: {Id}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hard deleting image mapping: {Id}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string objectKey)
    {
        return await _context.ImageMappings
            .AnyAsync(x => x.ObjectKey == objectKey && x.IsActive);
    }

    public async Task<int> GetCountByProjectAsync(string projectName, bool activeOnly = true)
    {
        var query = _context.ImageMappings
            .Where(x => x.ProjectName == projectName);

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.CountAsync();
    }

    public async Task<List<Guid>> GetAllActiveGuidsAsync()
    {
        return await _context.ImageMappings
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();
    }
}